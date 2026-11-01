using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elifoot.Infrastructure.Catalog;

public interface ICatalogSource
{
    CatalogScopeKey Scope { get; }
    Task<CatalogImportPackage> FetchAsync(CancellationToken ct);
    TimeSpan? FetchTimeout => null;
}
public sealed class CatalogSyncOptions
{
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(2);
    public int Retries { get; set; } = 1;
    public Dictionary<CatalogDataType, TimeSpan> Frequencies { get; } = new() {
        [CatalogDataType.Clubs] = TimeSpan.FromDays(1), [CatalogDataType.Squads] = TimeSpan.FromHours(12),
        [CatalogDataType.Rankings] = TimeSpan.FromDays(7), [CatalogDataType.Countries] = TimeSpan.FromDays(30), [CatalogDataType.Leagues] = TimeSpan.FromDays(7) };
}
public sealed class CatalogBusyException() : Exception("Atualização compartilhada em andamento; use os dados salvos.");

public sealed class AuthorizedJsonCatalogSource(HttpClient client, CatalogScopeKey scope, Uri endpoint,
    bool storageAuthorized, bool commercialAuthorized, string license, string? bearerToken = null) : ICatalogSource
{
    public CatalogScopeKey Scope => scope;
    public async Task<CatalogImportPackage> FetchAsync(CancellationToken ct)
    {
        if (!storageAuthorized || !commercialAuthorized || string.IsNullOrWhiteSpace(license))
            throw new InvalidOperationException("Fornecedor sem autorização de armazenamento e uso comercial.");
        using var message = new HttpRequestMessage(HttpMethod.Get, endpoint);
        if (!string.IsNullOrWhiteSpace(bearerToken)) message.Headers.Authorization = new("Bearer", bearerToken);
        using var response = await client.SendAsync(message, HttpCompletionOption.ResponseHeadersRead, ct);
        if (response.StatusCode == HttpStatusCode.TooManyRequests) throw new HttpRequestException("Rate limit", null, response.StatusCode);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > 20_000_000) throw new ArgumentException("Pacote excede 20 MB.");
        await using var input = await response.Content.ReadAsStreamAsync(ct);
        using var output = new MemoryStream();
        byte[] buffer = new byte[8192]; int read;
        while ((read = await input.ReadAsync(buffer, ct)) > 0)
        { if (output.Length + read > 20_000_000) throw new ArgumentException("Pacote excede 20 MB."); await output.WriteAsync(buffer.AsMemory(0, read), ct); }
        return CatalogImportCodec.ReadJson(System.Text.Encoding.UTF8.GetString(output.ToArray())) with {
            StorageAuthorized = storageAuthorized, CommercialUseAuthorized = commercialAuthorized, LicenseReference = license };
    }
}

public sealed class CatalogSyncService(IServiceScopeFactory factory, IEnumerable<ICatalogSource> sources, CatalogSyncOptions options)
{
    private readonly ICatalogSource[] _sources = sources.OrderBy(s => s.Scope.DataType switch {
        CatalogDataType.Countries => 0, CatalogDataType.Leagues => 1, CatalogDataType.Clubs => 2, CatalogDataType.Squads => 3, _ => 4 }).ThenBy(s => s.Scope.Key, StringComparer.Ordinal).ToArray();
    private readonly SemaphoreSlim _networkGate = new(4);
    private CatalogDbContext Db(IServiceScope scope) => scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
    public async Task EnsureScopeAsync(CatalogScopeKey key, CancellationToken ct = default)
    {
        using var scope = factory.CreateScope(); var db = Db(scope);
        if (await db.Scopes.AnyAsync(s => s.Key == key.Key, ct)) return;
        db.Scopes.Add(new() { Key = key.Key, CountryCode = key.CountryCode, Gender = key.Gender, League = key.League,
            Season = key.Season, DataType = key.DataType, Provider = key.Provider });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { db.ChangeTracker.Clear(); if (!await db.Scopes.AnyAsync(s => s.Key == key.Key, ct)) throw; }
    }
    public async Task<Guid> ImportAsync(CatalogImportPackage package, CancellationToken ct = default)
    {
        await EnsureScopeAsync(package.Scope, ct);
        var owner = Guid.NewGuid();
        if (!await Acquire(package.Scope.Key, owner, false, ct)) throw new CatalogBusyException();
        var job = await StartJob(package.Scope.Key, ct);
        try
        {
            Guid id = await Commit(package, owner, ct);
            await Finish(job, "Succeeded", 1, null, ct); await RefreshColors(package,ct); return id;
        }
        catch (Exception e)
        { await Finish(job, "Failed", 1, ErrorCode(e), CancellationToken.None); throw; }
        finally { await Release(package.Scope.Key, owner); }
    }
    public async Task<List<Guid>> ImportBatch016AAsync(List<CatalogImportPackage> packages, CancellationToken ct = default, Func<CatalogDbContext,CancellationToken,Task>? linkIdentities = null)
    {
        if (packages.Count == 0 || packages.Count > 12 || packages.Select(p=>p.Scope.Key).Distinct().Count()!=packages.Count)
            throw new ArgumentException("Lote de importação inválido.");
        var ordered=packages.OrderBy(p=>p.Scope.DataType==CatalogDataType.Clubs?0:1).ThenBy(p=>p.Scope.Key,StringComparer.Ordinal).ToList();
        var owner=Guid.NewGuid();var held=new List<string>();
        try {
            foreach(var p in ordered) {
                Validate(p);await EnsureScopeAsync(p.Scope,ct);
                if(!await Acquire(p.Scope.Key,owner,false,ct))throw new CatalogBusyException();held.Add(p.Scope.Key);
            }
            using var scope=factory.CreateScope();var db=Db(scope);await using var transaction=await db.Database.BeginTransactionAsync(ct);
            if(linkIdentities!=null)await linkIdentities(db,ct);
            var result=new List<Guid>();foreach(var p in ordered)result.Add(await Commit(p,owner,ct,db));
            await transaction.CommitAsync(ct);return result;
        } finally {foreach(string key in held)await Release(key,owner);}
    }
    public async Task RefreshAsync(IEnumerable<string> countries, FootballGender gender, string season, CancellationToken ct = default)
    {
        var selected = countries.ToHashSet();
        await Task.WhenAll(_sources.Where(s => selected.Contains(s.Scope.CountryCode) && s.Scope.Gender == gender && s.Scope.Season == season)
            .GroupBy(s => s.Scope.CountryCode).Select(async group => { foreach (var source in group) await Sync(source, ct); }));
    }
    // Explicit one-shot scheduling; no hosted service or persistent timer is installed.
    public async Task RunDueAsync(CancellationToken ct = default)
    { foreach (var source in _sources) await Sync(source, ct); }
    private async Task Sync(ICatalogSource source, CancellationToken ct)
    {
        if (source.Scope.DataType is CatalogDataType.Squads or CatalogDataType.Rankings)
        {
            using var dependencyScope = factory.CreateScope(); var dependencyDb = Db(dependencyScope);
            bool clubsUpdating = await dependencyDb.Scopes.AnyAsync(s => s.CountryCode == source.Scope.CountryCode && s.Gender == source.Scope.Gender &&
                s.Season == source.Scope.Season && s.DataType == CatalogDataType.Clubs && s.LeaseUntil > DateTime.UtcNow &&
                (s.League == source.Scope.League || s.League == "*" || source.Scope.League == "*"), ct);
            if (clubsUpdating) return; // The owner of the club update will continue with dependent sources.
        }
        await EnsureScopeAsync(source.Scope, ct);
        var owner = Guid.NewGuid();
        if (!await Acquire(source.Scope.Key, owner, true, ct)) return;
        var job = await StartJob(source.Scope.Key, ct); int attempts = 0;
        try
        {
            await _networkGate.WaitAsync(ct);
            try
            {
                for (int i = 0; ; i++)
                {
                    attempts++;
                    try
                    {
                        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
                        timeout.CancelAfter(source.FetchTimeout ?? options.Timeout);
                        var package = await source.FetchAsync(timeout.Token).WaitAsync(timeout.Token);
                        if (package.Scope != source.Scope) throw new ArgumentException("Escopo da resposta difere do solicitado.");
                        await Commit(package, owner, ct); await RefreshColors(package,ct); break;
                    }
                    catch (Exception e) when (i < Math.Clamp(options.Retries, 0, 1) && !ct.IsCancellationRequested &&
                        (e is OperationCanceledException || e is HttpRequestException h && h.StatusCode != HttpStatusCode.TooManyRequests)) { }
                }
            }
            finally { _networkGate.Release(); }
            await Finish(job, "Succeeded", attempts, null, ct);
        }
        catch (Exception e)
        {
            await Finish(job, "Failed", attempts, ErrorCode(e), CancellationToken.None);
            using var scope = factory.CreateScope(); var db = Db(scope);
            await db.Scopes.Where(s => s.Key == source.Scope.Key && s.LeaseOwner == owner)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.NextAttempt, DateTime.UtcNow.AddMinutes(15)));
        }
        finally { await Release(source.Scope.Key, owner); }
    }
    private async Task RefreshColors(CatalogImportPackage package,CancellationToken ct) {
        if(package.Scope.DataType!=CatalogDataType.Clubs||package.IsFixture)return;
        using var scope=factory.CreateScope();var colors=scope.ServiceProvider.GetService<ClubColorService>();
        if(colors==null)return;
        var creator=scope.ServiceProvider.GetService<WorldCreatorService>();if(creator==null)return;
        var selected=await creator.Select([package.Scope.CountryCode],package.Scope.Gender,package.Scope.Season,true,ct);
        await colors.Refresh(selected.Clubs,ct:ct);
    }
    private static string ErrorCode(Exception e) => e switch {
        OperationCanceledException => "Timeout/cancelamento", HttpRequestException h when h.StatusCode == HttpStatusCode.TooManyRequests => "RateLimit",
        HttpRequestException h when h.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => "AutenticacaoOuLicenca",
        HttpRequestException => "ApiIndisponivel", DbUpdateException => "Banco", ArgumentException => "RespostaInvalida", _ => e.GetType().Name };
    private async Task<bool> Acquire(string key, Guid owner, bool dueOnly, CancellationToken ct)
    {
        using var scope = factory.CreateScope(); var db = Db(scope); var now = DateTime.UtcNow;
        return await db.Scopes.Where(s => s.Key == key && (s.LeaseUntil == null || s.LeaseUntil < now) && (!dueOnly || s.NextAttempt <= now))
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.LeaseOwner, owner).SetProperty(x => x.LeaseUntil, now.AddMinutes(10)), ct) == 1;
    }
    private async Task Release(string key, Guid owner)
    {
        using var scope = factory.CreateScope(); var db = Db(scope);
        await db.Scopes.Where(s => s.Key == key && s.LeaseOwner == owner)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.LeaseOwner, (Guid?)null).SetProperty(x => x.LeaseUntil, (DateTime?)null));
    }
    private async Task<Guid> StartJob(string key, CancellationToken ct)
    {
        using var scope = factory.CreateScope(); var db = Db(scope);
        var job = new CatalogSyncJob { Id = Guid.NewGuid(), ScopeKey = key, Status = "Running", StartedAt = DateTime.UtcNow };
        db.SyncJobs.Add(job); await db.SaveChangesAsync(ct); return job.Id;
    }
    private async Task Finish(Guid id, string status, int attempts, string? error, CancellationToken ct)
    {
        using var scope = factory.CreateScope(); var db = Db(scope);
        await db.SyncJobs.Where(s => s.Id == id).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, status)
            .SetProperty(x => x.Attempts, attempts).SetProperty(x => x.Error, error).SetProperty(x => x.FinishedAt, DateTime.UtcNow), ct);
    }
    private async Task<Guid> Commit(CatalogImportPackage input, Guid owner, CancellationToken ct, CatalogDbContext? batchDb = null)
    {
        if (input?.Scope == null || input.Players == null || input.Clubs == null || input.Rankings == null) throw new ArgumentException("Pacote incompleto.");
        var p = input with { Players = input.Players.Select(x => x with { Position = CatalogImportCodec.Position(x.Position) }).ToList() };
        Validate(p);
        if (CatalogRightsPolicy.StorageIssue(p, DateTime.UtcNow) is { } rightsIssue) throw new ArgumentException(rightsIssue);
        using var scope = batchDb == null ? factory.CreateScope() : null; var db = batchDb ?? Db(scope!);
        await using var tx = batchDb == null ? await db.Database.BeginTransactionAsync(ct) : null;
        var state = await db.Scopes.SingleAsync(s => s.Key == p.Scope.Key, ct);
        if (state.LeaseOwner != owner || state.LeaseUntil < DateTime.UtcNow) throw new CatalogBusyException();
        if (p.Scope.DataType is CatalogDataType.Squads or CatalogDataType.Rankings)
        {
            var catalogIds = await db.Scopes.Where(s => s.CountryCode == p.Scope.CountryCode && s.Gender == p.Scope.Gender &&
                s.Season == p.Scope.Season && s.DataType == CatalogDataType.Clubs && s.ActiveSnapshotId != null &&
                (s.League == p.Scope.League || s.League == "*" || p.Scope.League == "*")).Select(s => s.ActiveSnapshotId!.Value).ToListAsync(ct);
            var clubSnapshots = await db.Snapshots.Where(s => catalogIds.Contains(s.Id)).ToListAsync(ct);
            var known = clubSnapshots.SelectMany(s => { var data = CatalogImportCodec.ReadJson(s.PayloadJson);
                return data.Clubs.Select(c => (Provider: data.Scope.Provider, Id: c.ExternalId)); }).ToHashSet();
            var knownIdentityIds = await db.Set<CatalogClubIdentityAlias>().Where(a => a.CountryCode == p.Scope.CountryCode && a.Gender == p.Scope.Gender).ToListAsync(ct);
            var activeIdentities = knownIdentityIds.Where(a => known.Contains((a.Provider, a.ExternalId))).Select(a => a.ClubId).ToHashSet();
            foreach (var alias in knownIdentityIds.Where(a => activeIdentities.Contains(a.ClubId))) known.Add((alias.Provider, alias.ExternalId));
            if (p.RosterReports?.Any(r => !known.Contains((r.ClubProvider, r.ClubExternalId))) == true) throw new ArgumentException("Relatório de elenco referencia clube desconhecido.");
            if (p.Players.Any(x => !known.Contains((x.ClubProvider, x.ClubExternalId))) ||
                p.Rankings.Any(x => !known.Contains((x.ClubProvider, x.ClubExternalId))))
                throw new ArgumentException("Elenco/ranking referencia clube ausente neste país, categoria, liga e temporada; importe os clubes primeiro.");
        }
        var previous = state.ActiveSnapshotId == null ? null : await db.Snapshots.FindAsync([state.ActiveSnapshotId.Value], ct);
        int Count(CatalogImportPackage v) => v.Scope.DataType switch { CatalogDataType.Clubs => v.Clubs.Count, CatalogDataType.Squads => v.Players.Count, CatalogDataType.Countries => v.CountryData?.Count ?? 0, CatalogDataType.Leagues => v.LeagueData?.Count ?? 0, _ => v.Rankings.Count };
        if (previous != null && (p.UpdatedAt < previous.UpdatedAt || !p.AllowCountDecrease && Count(p) < Count(CatalogImportCodec.ReadJson(previous.PayloadJson))))
            throw new ArgumentException("Atualização mais antiga ou com perda inesperada de itens.");
        if (previous != null && p.Scope.DataType == CatalogDataType.Squads && p.AllowPartialSquads)
        {
            var old = CatalogImportCodec.ReadJson(previous.PayloadJson);
            foreach (var oldSquad in old.Players.GroupBy(x => (x.ClubProvider, x.ClubExternalId)))
            {
                var current = p.Players.Where(x => (x.ClubProvider, x.ClubExternalId) == oldSquad.Key).ToList();
                bool Good(IEnumerable<CatalogAthlete> roster) => roster.Count() >= 11 && roster.Any(x => x.Position == "G") && roster.Count(x => x.Position != "G") >= 10;
                if (Good(oldSquad) && !Good(current)) throw new ArgumentException("Atualização parcial não pode substituir elenco completo válido.");
            }
        }
        string json = JsonSerializer.Serialize(p, CatalogImportCodec.Json);
        string hash = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(json)));
        var snapshot = await db.Snapshots.SingleOrDefaultAsync(s => s.ScopeKey == p.Scope.Key && s.Version == p.Version, ct);
        if (snapshot != null && snapshot.Hash != hash && JsonSerializer.Serialize(CatalogImportCodec.ReadJson(snapshot.PayloadJson), CatalogImportCodec.Json) != json) throw new ArgumentException("Versão repetida com conteúdo diferente.");
        if (snapshot == null)
        {
            snapshot = new() { Id = Guid.NewGuid(), ScopeKey = p.Scope.Key, Version = p.Version, Hash = hash,
                PayloadJson = json, UpdatedAt = p.UpdatedAt, ValidatedAt = DateTime.UtcNow, IsFixture = p.IsFixture };
            db.Snapshots.Add(snapshot);
            await CatalogIdentityService.RegisterAsync(db, p, ct);
            foreach (var league in p.Clubs.GroupBy(c => c.LeagueExternalId))
                db.Add(new CatalogLeague { SnapshotId = snapshot.Id, ExternalId = league.Key, Name = league.First().LeagueName });
            foreach (var league in p.LeagueData ?? [])
                db.Add(new CatalogLeague { SnapshotId = snapshot.Id, ExternalId = league.ExternalId, Name = league.Name });
            foreach (var c in p.Clubs) db.Add(new CatalogClub { SnapshotId = snapshot.Id, ExternalId = c.ExternalId, Name = c.Name, LeagueExternalId = c.LeagueExternalId });
            foreach (var player in p.Players)
            {
                db.Add(new CatalogPlayer { SnapshotId = snapshot.Id, ExternalId = player.PlayerExternalId, Name = player.PlayerName, Position = player.Position, Strength = player.Strength });
                db.Add(new CatalogSquad { SnapshotId = snapshot.Id, PlayerExternalId = player.PlayerExternalId,
                    ClubKey = WorldCreatorEngine.ClubKey(p.Scope.CountryCode, p.Scope.Gender, player.ClubProvider, player.ClubExternalId) });
            }
            foreach (var r in p.Rankings) db.Add(new CatalogRanking { SnapshotId = snapshot.Id,
                ClubKey = WorldCreatorEngine.ClubKey(p.Scope.CountryCode, p.Scope.Gender, r.ClubProvider, r.ClubExternalId), Rank = r.Rank, UpdatedAt = r.UpdatedAt });
        }
        // Upserts prevent distinct scope synchronizations racing on shared country/provider metadata.
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"wc013a_CatalogCountry\" (\"Code\", \"Name\") VALUES ({p.Scope.CountryCode}, {p.CountryName}) ON CONFLICT (\"Code\") DO UPDATE SET \"Name\" = EXCLUDED.\"Name\"", ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"wc013a_CatalogSeason\" (\"Name\") VALUES ({p.Scope.Season}) ON CONFLICT (\"Name\") DO NOTHING", ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"wc013a_CatalogProvider\" (\"Id\", \"LicenseReference\", \"StorageAuthorized\", \"CommercialUseAuthorized\") VALUES ({p.Scope.Provider}, {p.LicenseReference}, {p.StorageAuthorized}, {p.CommercialUseAuthorized}) ON CONFLICT (\"Id\") DO UPDATE SET \"LicenseReference\" = EXCLUDED.\"LicenseReference\", \"StorageAuthorized\" = EXCLUDED.\"StorageAuthorized\", \"CommercialUseAuthorized\" = EXCLUDED.\"CommercialUseAuthorized\"", ct);
        await db.SaveChangesAsync(ct);
        int updated = await db.Scopes.Where(s => s.Key == p.Scope.Key && s.LeaseOwner == owner && s.LeaseUntil > DateTime.UtcNow)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.ActiveSnapshotId, snapshot.Id).SetProperty(x => x.LastSuccess, DateTime.UtcNow)
                .SetProperty(x => x.NextAttempt, DateTime.UtcNow.Add(options.Frequencies[p.Scope.DataType])), ct);
        if (updated != 1) throw new CatalogBusyException();
        if (tx != null) await tx.CommitAsync(ct); return snapshot.Id;
    }
    public static void Validate(CatalogImportPackage p)
    {
        var k = p.Scope;
        if (!Enum.IsDefined(k.Gender) || !Enum.IsDefined(k.DataType) || k.CountryCode.Length is < 2 or > 3 || k.CountryCode != k.CountryCode.ToUpperInvariant() ||
            new[] { k.League, k.Season, k.Provider, p.CountryName, p.Version, p.LicenseReference }.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Manifesto inválido.");
        int count = k.DataType switch { CatalogDataType.Clubs => p.Clubs.Count, CatalogDataType.Squads => p.Players.Count, CatalogDataType.Countries => p.CountryData?.Count ?? 0, CatalogDataType.Leagues => p.LeagueData?.Count ?? 0, _ => p.Rankings.Count };
        if (!p.StorageAuthorized || p.IsFixture && !p.CommercialUseAuthorized) throw new ArgumentException("Licença não autorizada.");
        if (!p.Complete || p.ExpectedPages < 1 || p.ExpectedPages != p.ReceivedPages || (count == 0 && !(p.AllowPartialSquads && k.DataType == CatalogDataType.Squads && p.RosterReports?.Count > 0)) || count != p.ExpectedItems ||
            p.UpdatedAt.Kind != DateTimeKind.Utc || p.UpdatedAt > DateTime.UtcNow.AddMinutes(5)) throw new ArgumentException("Resposta vazia/incompleta, páginas ou data inválidas.");
        if (k.DataType != CatalogDataType.Clubs && p.Clubs.Count != 0 || k.DataType != CatalogDataType.Squads && p.Players.Count != 0 || k.DataType != CatalogDataType.Rankings && p.Rankings.Count != 0)
            throw new ArgumentException("Pacote mistura tipos de dados.");
        if (k.DataType != CatalogDataType.Countries && p.CountryData?.Count > 0 || k.DataType != CatalogDataType.Leagues && p.LeagueData?.Count > 0)
            throw new ArgumentException("Metadados misturam tipos de dados.");
        if (p.CountryData?.Any(x => x.Code != k.CountryCode || x.Name != p.CountryName) == true || p.CountryData?.Select(x => x.Code).Distinct().Count() != p.CountryData?.Count)
            throw new ArgumentException("País incompatível com o escopo.");
        if (p.LeagueData?.Any(x => string.IsNullOrWhiteSpace(x.ExternalId) || string.IsNullOrWhiteSpace(x.Name) || x.Gender != k.Gender || x.Season != k.Season) == true ||
            p.LeagueData?.Select(x => x.ExternalId).Distinct().Count() != p.LeagueData?.Count)
            throw new ArgumentException("Liga duplicada ou incompatível com categoria/temporada.");
        if (p.Clubs.Any(c => string.IsNullOrWhiteSpace(c.ExternalId) || string.IsNullOrWhiteSpace(c.Name) || string.IsNullOrWhiteSpace(c.LeagueExternalId)) ||
            p.Clubs.Select(c => c.ExternalId).Distinct().Count() != p.Clubs.Count)
            throw new ArgumentException("Clubes duplicados ou inválidos.");
        if (p.Players.Any(x => x.Gender != k.Gender || x.Season != k.Season || x.Source != k.Provider ||
            string.IsNullOrWhiteSpace(x.PlayerExternalId) || string.IsNullOrWhiteSpace(x.ClubExternalId) || string.IsNullOrWhiteSpace(x.ClubProvider) ||
            string.IsNullOrWhiteSpace(x.PlayerName) || x.Strength is < 1 or > 50 || x.UpdatedAt.Kind != DateTimeKind.Utc || x.UpdatedAt > p.UpdatedAt) ||
            p.Players.Select(x => x.PlayerExternalId).Distinct().Count() != p.Players.Count)
            throw new ArgumentException("Elenco inconsistente, categoria/temporada incorreta ou jogadores duplicados.");
        foreach (var squad in p.Players.GroupBy(x => (x.ClubProvider, x.ClubExternalId)))
            if (!p.AllowPartialSquads && (squad.Count() is < 11 or > 24 || !squad.Any(x => x.Position == "G") || squad.Count(x => x.Position != "G") < 10) || p.AllowPartialSquads && squad.Count() > (p.Scope.League.StartsWith("016a:",StringComparison.Ordinal) ? 500 : 100))
                throw new ArgumentException("Elenco incompleto ou sem goleiro.");
        if (p.AllowPartialSquads && (p.RosterReports == null || p.RosterReports.Count == 0 || p.RosterAsOf == null ||
            p.RosterReports.Select(r => (r.ClubProvider, r.ClubExternalId)).Distinct().Count() != p.RosterReports.Count ||
            p.RosterReports.Any(r => r.ReceivedPlayers < r.UsablePlayers || r.UsablePlayers != p.Players.Count(x => x.ClubProvider == r.ClubProvider && x.ClubExternalId == r.ClubExternalId))))
            throw new ArgumentException("Elenco parcial exige relatório completo de cobertura e data.");
        if (p.Players.Any(x => x.MembershipStart > x.MembershipEnd || x.Performance?.Rating is { } rating && !double.IsFinite(rating)))
            throw new ArgumentException("Dados individuais/vínculos inválidos.");
        if (!p.IsFixture && p.Players.Any(x => x.IdentityOrigin == null || x.StrengthEstimate == null ||
            x.StrengthEstimate.Origin is not (CatalogDataOrigin.Estimated or CatalogDataOrigin.Generated) || string.IsNullOrWhiteSpace(x.StrengthEstimate.Method)))
            throw new ArgumentException("Atletas reais exigem origem da identidade e método da estimativa de força explicitamente identificados.");
        if (p.Players.Any(x => x.StrengthEstimate != null && (!double.IsFinite(x.StrengthEstimate.Confidence) || x.StrengthEstimate.Confidence < 0 || x.StrengthEstimate.Confidence > 1) ||
            x.Performance?.Minutes < 0 || x.Performance?.Appearances < 0 || x.Performance?.Goals < 0 || x.Performance?.Assists < 0))
            throw new ArgumentException("Estatísticas/confiança inválidas.");
        if (!p.IsFixture && p.Rankings.Any(r => string.IsNullOrWhiteSpace(r.Method) || r.Origin == null || r.Confidence is null or < 0 or > 1 || !double.IsFinite(r.Confidence ?? double.NaN) || string.IsNullOrWhiteSpace(r.ComparableGroup)))
            throw new ArgumentException("Ranking real/derivado exige método, origem, confiança e universo comparável.");
        if (p.Rankings.Any(x => x.Gender != k.Gender || x.Season != k.Season || x.Source != k.Provider || x.Rank < 1 ||
            string.IsNullOrWhiteSpace(x.ClubExternalId) || string.IsNullOrWhiteSpace(x.ClubProvider) || x.UpdatedAt.Kind != DateTimeKind.Utc || x.UpdatedAt > p.UpdatedAt) ||
            p.Rankings.Select(x => (x.ClubProvider, x.ClubExternalId)).Distinct().Count() != p.Rankings.Count)
            throw new ArgumentException("Ranking incompatível ou duplicado.");
    }
}
