using System.Globalization;
using System.Text.Json;
using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elifoot.Infrastructure.Catalog;

public sealed class WorldCreatorService(IServiceScopeFactory factory, CatalogSyncService sync, IWorldRepository worlds,
    LocalHistoricalCatalog? localCatalog = null)
{
    public static readonly (string Code, string Name)[] PriorityCountries = [("BR", "Brasil"), ("AR", "Argentina"),
        ("GB", "Inglaterra"), ("ES", "Espanha"), ("DE", "Alemanha"), ("IT", "Itália"), ("FR", "França"), ("PT", "Portugal")];

    public async Task<List<CatalogCountryOption>> HistoricalCountries(FootballGender gender, CancellationToken ct)
    {
        if (!Enum.IsDefined(gender)) throw new ArgumentException("Categoria inválida.");
        var catalog = localCatalog ?? throw new KeyNotFoundException("Catálogo histórico local desativado.");
        return LocalCountries((await catalog.GetAsync(ct)).Clubs, gender);
    }

    public async Task<List<HistoricalYearOption>> HistoricalYears(FootballGender gender, string[] countries, CancellationToken ct)
    {
        ValidateHistoricalCountries(countries, gender);
        var catalog = localCatalog ?? throw new KeyNotFoundException("Catálogo histórico local desativado.");
        var index = await catalog.GetAsync(ct);
        return index.Clubs.Where(c => c.Gender == gender && countries.Contains(c.CountryCode))
            .GroupBy(c => c.Season)
            .OrderByDescending(year => int.Parse(year.Key, CultureInfo.InvariantCulture))
            .Select(year => new HistoricalYearOption(year.Key, year.Count(), year.GroupBy(c => c.CountryCode)
                .OrderBy(country => country.First().CountryName, StringComparer.OrdinalIgnoreCase)
                .Select(country => new HistoricalYearCountry(country.Key, country.First().CountryName, country.Count())).ToList()))
            .ToList();
    }

    private static List<CatalogCountryOption> LocalCountries(IEnumerable<EligibleCatalogClub> clubs, FootballGender? gender) =>
        clubs.Where(c => gender == null || c.Gender == gender)
            .GroupBy(c => c.CountryCode)
            .Select(country => new CatalogCountryOption(country.Key, country.First().CountryName,
                country.Count(c => c.Gender == FootballGender.Male), country.Count(c => c.Gender == FootballGender.Female)))
            .OrderBy(country => country.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

    private static void ValidateHistoricalCountries(IReadOnlyCollection<string>? countries, FootballGender gender)
    {
        if (!Enum.IsDefined(gender)) throw new ArgumentException("Categoria inválida.");
        if (countries == null || countries.Count is < 1 or > 8 ||
            countries.Any(string.IsNullOrWhiteSpace) || countries.Distinct(StringComparer.Ordinal).Count() != countries.Count)
            throw new ArgumentException("Selecione de 1 a 8 países sem repetição.");
    }

    private WorldCreatorRequest NormalizeCatalogRequest(WorldCreatorRequest request)
    {
        if (localCatalog == null)
        {
            if (request.CatalogYears != null)
                throw new ArgumentException("A seleção de anos exige o catálogo histórico local.");
            return request;
        }

        ValidateHistoricalCountries(request.Countries, request.Gender);
        var years = request.CatalogYears ?? [request.Season];
        if (years.Count == 0 || years.Any(year => !int.TryParse(year, NumberStyles.None, CultureInfo.InvariantCulture, out var value) ||
                value <= 0 || year != value.ToString(CultureInfo.InvariantCulture)) ||
            years.Distinct(StringComparer.Ordinal).Count() != years.Count)
            throw new ArgumentException("Selecione pelo menos um ano válido do catálogo, sem repetição.");

        var ordered = years.OrderByDescending(year => int.Parse(year, CultureInfo.InvariantCulture)).ToList();
        return request with { Season = ordered[0], CatalogYears = ordered, UseSavedData = true };
    }

    private static CatalogSelection SelectLocal(LocalHistoricalCatalogIndex index, List<string> countries, FootballGender gender,
        string season, IReadOnlyCollection<string> years, bool validateCoverage = false)
    {
        var category = index.Clubs.Where(c => c.Gender == gender).ToList();
        var clubs = category.Where(c => countries.Contains(c.CountryCode) && years.Contains(c.Season)).ToList();
        if (validateCoverage && countries.Any(code => !category.Any(c => c.CountryCode == code)))
            throw new ArgumentException("Um dos países escolhidos ficou sem clubes nesta categoria. Revise os países.");
        if (validateCoverage && years.Any(year => !clubs.Any(c => c.Season == year)))
            throw new ArgumentException("Um dos anos escolhidos ficou sem clubes para esses países. Revise os anos.");

        var references = clubs.SelectMany(c => c.References).DistinctBy(reference => reference.SnapshotId).ToList();
        var warnings = countries.Where(code => !clubs.Any(c => c.CountryCode == code))
            .Select(code => $"Sem clubes nos anos escolhidos para {category.FirstOrDefault(c => c.CountryCode == code)?.CountryName ?? code}.")
            .ToList();
        return new(gender, season, countries, HistoricalCatalogPolicy.Classify(clubs), references, warnings);
    }

    public async Task<List<string>> Seasons(CancellationToken ct = default)
    {
        if (localCatalog != null)
        {
            var index = await localCatalog.GetAsync(ct);
            return index.Clubs.Select(c => c.Season).Distinct()
                .OrderByDescending(year => int.Parse(year, CultureInfo.InvariantCulture)).ToList();
        }

        using var scope = factory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var candidates = await (from catalogScope in db.Scopes.AsNoTracking()
            where catalogScope.DataType == CatalogDataType.Clubs && catalogScope.Season.Length == 4
            join snapshot in db.Snapshots.AsNoTracking() on catalogScope.ActiveSnapshotId equals (Guid?)snapshot.Id
            where catalogScope.Key == snapshot.ScopeKey
            select new { Scope = catalogScope, Snapshot = snapshot }).ToListAsync(ct);

        var now = DateTime.UtcNow;
        var years = new HashSet<string>(StringComparer.Ordinal);
        foreach (var candidate in candidates)
        {
            var year = candidate.Scope.Season;
            if (years.Contains(year) || year == "0000" || !year.All(char.IsAsciiDigit)) continue;

            var snapshot = candidate.Snapshot;
            var package = CatalogImportCodec.ReadJson(snapshot.PayloadJson);
            var reference = new CatalogReference(snapshot.Id, candidate.Scope.ToKey(), snapshot.Version,
                snapshot.UpdatedAt, snapshot.ValidatedAt, snapshot.IsFixture, "Saved", null, package.Rights);
            if (package.Clubs.Count > 0 && CatalogRightsPolicy.StorageIssue(package, now) == null &&
                CatalogRightsPolicy.WorldIssue(reference, now) == null)
                years.Add(year);
        }
        return years.OrderDescending(StringComparer.Ordinal).ToList();
    }

    public Task<List<CatalogCountryOption>> Countries(string season, CancellationToken ct = default) =>
        Countries(season, null, ct);

    public async Task<List<CatalogCountryOption>> Countries(string season, FootballGender? gender, CancellationToken ct)
    {
        if (gender is { } requestedGender && !Enum.IsDefined(requestedGender))
            throw new ArgumentException("Categoria inválida.");

        if (localCatalog != null)
        {
            var index = await localCatalog.GetAsync(ct);
            return LocalCountries(index.Clubs.Where(c => c.Season == season), gender);
        }

        using var scope = factory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var countries = await db.Countries.AsNoTracking().ToListAsync(ct);
        if (countries.Count == 0) return [];

        var codes = countries.Select(country => country.Code).ToList();
        async Task<Dictionary<string, int>> AvailableClubs(FootballGender category)
        {
            var selection = await Select(codes, category, season, saved: true, ct);
            return selection.Clubs
                .Where(club => club.Verification == null || club.Verification.Status == CatalogClubVerificationStatus.REAL_CLUB_VERIFIED)
                .GroupBy(club => club.CountryCode)
                .ToDictionary(group => group.Key, group => group.Count());
        }

        Dictionary<string, int> male = gender is null or FootballGender.Male ? await AvailableClubs(FootballGender.Male) : [];
        Dictionary<string, int> female = gender is null or FootballGender.Female ? await AvailableClubs(FootballGender.Female) : [];
        return countries.OrderBy(country => country.Name)
            .Select(country => new CatalogCountryOption(country.Code, country.Name,
                male.GetValueOrDefault(country.Code), female.GetValueOrDefault(country.Code)))
            .Where(country => gender == null || country.MaleClubs > 0 || country.FemaleClubs > 0)
            .ToList();
    }
    public async Task<CatalogSelection> Select(List<string> countries, FootballGender gender, string season, bool saved, CancellationToken ct = default)
    {
        if (countries == null || countries.Count == 0 || countries.Count > 512 || countries.Distinct().Count() != countries.Count || !Enum.IsDefined(gender) || string.IsNullOrWhiteSpace(season))
            throw new ArgumentException("Seleção de países/categoria/temporada inválida.");
        if (localCatalog != null)
            return SelectLocal(await localCatalog.GetAsync(ct), countries, gender, season, new[] { season });
        if (!saved) await sync.RefreshAsync(countries, gender, season, ct);
        using var scope = factory.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var states = await db.Scopes.AsNoTracking().Where(s => countries.Contains(s.CountryCode) && s.Gender == gender && s.Season == season).ToListAsync(ct);
        var ids = states.Where(s => s.ActiveSnapshotId != null).Select(s => s.ActiveSnapshotId!.Value).ToList();
        var snapshots = await db.Snapshots.AsNoTracking().Where(s => ids.Contains(s.Id)).ToListAsync(ct);
        var keys = states.Select(s => s.Key).ToList();
        var jobs = await db.SyncJobs.AsNoTracking().Where(j => keys.Contains(j.ScopeKey)).OrderByDescending(j => j.StartedAt).ToListAsync(ct);
        var refs = new List<CatalogReference>();
        var packages = new List<(CatalogImportPackage Data, CatalogReference Ref)>();
        var warnings = new List<string>();
        foreach (var state in states)
        {
            var job = jobs.FirstOrDefault(j => j.ScopeKey == state.Key);
            var shot = snapshots.SingleOrDefault(s => s.Id == state.ActiveSnapshotId);
            string status = job?.Status == "Failed" ? "Fallback" : state.LeaseOwner != null && state.LeaseUntil > DateTime.UtcNow ? "Updating" :
                state.NextAttempt <= DateTime.UtcNow ? "Saved" : "Cached";
            string? warning = job?.Status == "Failed" ? $"{state.CountryCode}/{state.DataType}: {job.Error}; usando última versão válida." : null;
            if (warning != null) warnings.Add(warning);
            if (shot == null) continue;
            var reference = new CatalogReference(shot.Id, state.ToKey(), shot.Version, shot.UpdatedAt, shot.ValidatedAt, shot.IsFixture, status, warning, CatalogImportCodec.ReadJson(shot.PayloadJson).Rights);
            refs.Add(reference); packages.Add((CatalogImportCodec.ReadJson(shot.PayloadJson), reference));
        }
        var usablePackages = packages.Where(p => CatalogRightsPolicy.WorldIssue(p.Ref, DateTime.UtcNow) == null &&
            CatalogRightsPolicy.StorageIssue(p.Data, DateTime.UtcNow) == null).ToList();
        foreach (var p in packages.Except(usablePackages)) warnings.Add($"{p.Data.Scope.Provider}: snapshot indisponível por restrição/expiração da licença.");
        var aliases = await db.Set<CatalogClubIdentityAlias>().AsNoTracking().Where(a => countries.Contains(a.CountryCode) && a.Gender == gender).ToListAsync(ct);
        var identities = await db.Set<CatalogClubIdentity>().AsNoTracking().Where(i => countries.Contains(i.CountryCode) && i.Gender == gender).ToListAsync(ct);
        var conflicts = await db.Set<CatalogIdentityConflict>().AsNoTracking().Where(c => c.Status == "Open").ToListAsync(ct);
        var aliasIndex = aliases.ToDictionary(a => a.Key, a => a.ClubId, StringComparer.Ordinal);
        Guid Identity(string country, string provider, string id)
        {
            string key = CatalogIdentityService.AliasKey(country, gender, provider, id);
            return aliasIndex.TryGetValue(key, out var identity) ? identity : CatalogIdentityService.StableId(key);
        }
        var candidates = usablePackages.Where(p => p.Data.Scope.DataType == CatalogDataType.Clubs).SelectMany(p => p.Data.Clubs.Select(c =>
            (Data: p.Data, Ref: p.Ref, Club: c, Identity: Identity(p.Data.Scope.CountryCode, p.Data.Scope.Provider, c.ExternalId))));
        var unique = new List<EligibleCatalogClub>();
        foreach (var group in candidates.GroupBy(c => c.Identity))
        {
            var chosen = group.OrderByDescending(c => c.Ref.UpdatedAt).ThenBy(c => c.Data.Scope.Provider, StringComparer.Ordinal).First();
            var team = identities.SingleOrDefault(i => i.Id == group.Key);
            string key = team?.CanonicalKey ?? WorldCreatorEngine.ClubKey(chosen.Data.Scope.CountryCode, gender, chosen.Data.Scope.Provider, chosen.Club.ExternalId);
            bool Matches(string provider, string id) => Identity(chosen.Data.Scope.CountryCode, provider, id) == group.Key;
            var related = usablePackages.Where(p => p.Data.Scope.CountryCode == chosen.Data.Scope.CountryCode).ToList();
            var squad = related.Where(p => p.Data.Scope.DataType == CatalogDataType.Squads && (p.Data.Players.Any(a => Matches(a.ClubProvider, a.ClubExternalId)) ||
                    p.Data.RosterReports?.Any(r => Matches(r.ClubProvider, r.ClubExternalId)) == true))
                .OrderByDescending(p => {
                    var usable=p.Data.Players.Where(a=>Matches(a.ClubProvider,a.ClubExternalId)&&CatalogMembershipPolicy.Usable(a,DateOnly.FromDateTime(DateTime.UtcNow))).ToList();
                    return usable.Count>=11&&usable.Any(a=>a.Position=="G")&&usable.Count(a=>a.Position!="G")>=10;
                }).ThenByDescending(p => p.Ref.UpdatedAt).ThenBy(p => p.Data.Scope.Provider, StringComparer.Ordinal).FirstOrDefault();
            var ranking = related.Where(p => p.Data.Scope.DataType == CatalogDataType.Rankings && p.Data.Rankings.Any(r => Matches(r.ClubProvider, r.ClubExternalId)))
                .OrderByDescending(p => p.Ref.UpdatedAt).ThenBy(p => p.Data.Scope.Provider, StringComparer.Ordinal).FirstOrDefault();
            var asOf = squad.Data?.RosterAsOf ?? DateOnly.FromDateTime(squad.Ref?.UpdatedAt ?? DateTime.UtcNow);
            var documented = squad.Data?.Players.Where(p => Matches(p.ClubProvider, p.ClubExternalId)).ToList() ?? [];
            var players = documented.Where(p => CatalogMembershipPolicy.Usable(p, asOf))
                .Select(p => squad.Data!.AllowPartialSquads || p.IdentityOrigin != null ? p with {
                    CanonicalPlayerId = p.CanonicalPlayerId ?? $"{p.Source}:{p.Gender}:{p.PlayerExternalId}" } : p).ToList();
            var rank = ranking.Data?.Rankings.Where(r => Matches(r.ClubProvider, r.ClubExternalId)).OrderBy(r => r.Rank).FirstOrDefault();
            var references = new List<CatalogReference> { chosen.Ref };
            if (squad.Data != null) references.Add(squad.Ref!);
            if (ranking.Data != null) references.Add(ranking.Ref!);
            var rosterReport = squad.Data?.RosterReports?.FirstOrDefault(r => Matches(r.ClubProvider, r.ClubExternalId));
            var issues = rosterReport?.Issues.ToList() ?? [];
            if (rank?.Method?.Contains("disconnected", StringComparison.Ordinal) == true) issues.Add("Elo estimado de resultados reais; comparação entre ligas desconectadas é provisória e de baixa confiança.");
            if (documented.Any(p => p.MembershipEvidence?.Status != null && p.MembershipEvidence.Status != CatalogMembershipStatus.DatedActive))
                issues.Add("Vínculos históricos/abertos não confirmam elenco atual; excluídos dos atletas utilizáveis.");
            if (chosen.Club.Verification is { } verification && (!verification.CurrentSeasonConfirmed || verification.Status != CatalogClubVerificationStatus.REAL_CLUB_VERIFIED))
                issues.Add($"{verification.Status}: existência/atividade conforme fonte; participação na temporada atual não presumida.");
            if (ranking.Data != null && ranking.Ref!.UpdatedAt < DateTime.UtcNow.AddDays(-7)) issues.Add("Ranking desatualizado: data de referência preservada; não apresentado como atual.");
            var linkedAliases = aliases.Where(a => a.ClubId == group.Key).Select(a => new CatalogClubAlias(a.Provider, a.ExternalId)).ToList();
            var identityConflicts = conflicts.Where(c => c.LeftClubId == group.Key || c.RightClubId == group.Key).Select(c => c.Id).ToList();
            if (identityConflicts.Count > 0) issues.Add("Identidade semelhante em outro fornecedor: revisão explícita necessária; não foi feita fusão por nome.");
            unique.Add(new(key, chosen.Club.ExternalId, chosen.Data.Scope.Provider, chosen.Club.Name, chosen.Club.ShortName,
                chosen.Data.Scope.CountryCode, chosen.Data.CountryName, gender, chosen.Club.LeagueName, season,
                chosen.Club.PrimaryColor, chosen.Club.SecondaryColor, rank?.Rank, players, references, false, group.Key, rank,
                linkedAliases, issues, squad.Data?.AllowPartialSquads == true ? rosterReport?.ReceivedPlayers ?? players.Count : null, identityConflicts, chosen.Club.Verification,
                documented.Count, documented.Count(p => p.MembershipEvidence?.Status is CatalogMembershipStatus.OpenEndedUnconfirmed or CatalogMembershipStatus.Unknown),
                documented.Where(p=>!CatalogMembershipPolicy.Usable(p,asOf) && p.IdentityOrigin==CatalogDataOrigin.Real)
                    .Select(p=>p with {PlayerOrigin=WorldPlayerOrigin.REAL_HISTORICAL_REFERENCE}).ToList(),asOf));
        }
        unique = CatalogRosterReconciliation016A.Resolve(unique);
        var comparable = unique.Select(c => c.RankingInfo?.ComparableGroup).Where(g => g != null).Distinct().ToList();
        if (comparable.Count > 1)
        {
            warnings.Add("Classificações de universos diferentes não são um ranking mundial; ordem provisória explícita para equipes sem ranking comparável.");
            string? shared = comparable.FirstOrDefault(g => !g!.StartsWith("domestic:", StringComparison.Ordinal));
            unique = unique.Select(c => c.RankingInfo?.ComparableGroup != null && c.RankingInfo.ComparableGroup != shared ? c with { Rank = null } : c).ToList();
        }
        unique = unique.OrderBy(c => c.Key, StringComparer.Ordinal).Select((c, index) => c.RankingInfo == null && c.References.Any(r => !r.IsFixture)
            ? c with { RankingInfo = new(c.ExternalId, c.Provider, c.Gender, c.Season, index + 1, "elifoot-provisional013b",
                c.References.Max(r => r.UpdatedAt), null, c.Verification == null ? "ProvisionalIdentifierOrder-v1" : "Unranked-v2", 0, $"provisional:{gender}:{season}", CatalogDataOrigin.Estimated) } : c).ToList();
        foreach (var country in countries)
            if (!unique.Any(c => c.CountryCode == country)) warnings.Add($"{country}: sem cobertura válida para {gender}/{season}.");
        if (refs.Any(r => r.IsFixture)) warnings.Add(refs.All(r => r.IsFixture)
            ? "FIXTURES DE TESTE — clubes, atletas e rankings fictícios; nenhuma carga real."
            : "CATÁLOGO MISTO — existem fixtures junto dos dados reais; consulte a origem de cada snapshot.");
        var colors=scope.ServiceProvider.GetService<ClubColorService>();
        if(colors!=null)unique=await colors.Enrich(unique,ct);
        return new(gender, season, countries, unique, refs, warnings);
    }
    private async Task<CatalogSelection> SelectionForRequest(WorldCreatorRequest request,CancellationToken ct)
    {
        if (localCatalog != null)
            return SelectLocal(await localCatalog.GetAsync(ct), request.Countries, request.Gender, request.Season, request.CatalogYears!, validateCoverage: true);
        if(!request.UseWorldCatalogRules)return await Select(request.Countries,request.Gender,request.Season,request.UseSavedData,ct);
        if(request.Countries.Count is <1 or >8 || request.Countries.Distinct().Count()!=request.Countries.Count)throw new ArgumentException("Selecione de 1 a 8 países sem repetição.");
        using var scope=factory.CreateScope();var db=scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var service=scope.ServiceProvider.GetService<World016B.WorldCatalogService016B>();
        if(!request.UseSavedData && service!=null)foreach(var country in request.Countries)await service.Queue(country,request.Gender,request.Season,ct);
        var allCountries=(await db.Countries.Select(c=>c.Code).ToListAsync(ct)).Concat(request.Countries).Distinct().ToList();
        var all=await Select(allCountries,request.Gender,request.Season,true,ct);
        var classified=service==null?WorldCatalogRules016B.Classify(all.Clubs):await service.Project(all.Clubs,ct);
        // Classification is global in the available catalogue, before filtering the chosen countries.
        return all with {Countries=request.Countries.ToList(),Clubs=classified.Where(c=>request.Countries.Contains(c.CountryCode)).ToList(),
            References=all.References.Where(x=>request.Countries.Contains(x.Scope.CountryCode)).ToList()};
    }
    public async Task<CatalogSelection> SelectForWorld(WorldCreatorRequest request, CancellationToken ct = default)
    {
        request = NormalizeCatalogRequest(request);
        var selection=await SelectionForRequest(request,ct);
        if(request.DataMode==null)return selection;
        var asOf=DateOnly.FromDateTime(DateTime.UtcNow);var errors=new List<string>();var warnings=selection.Warnings.ToList();
        var eligible=PlayableWorldPolicy.Select(request,selection,asOf,errors,warnings);
        return selection with {Clubs=eligible,Warnings=warnings.Concat(errors).Distinct().ToList(),
            DataSummary=PlayableWorldPolicy.Summary(request,selection,[],asOf)};
    }
    public async Task<List<CatalogCoverageReport>> Coverage(List<string> countries, FootballGender gender, string season, CancellationToken ct = default)
    {
        var selection = await Select(countries, gender, season, true, ct);
        return countries.Select(code => {
            var clubs = selection.Clubs.Where(c => c.CountryCode == code).ToList();
            var references = selection.References.Where(r => r.Scope.CountryCode == code).ToList();
            var players = clubs.SelectMany(c => c.Players).ToList();
            int complete = clubs.Count(c => InitialRosterPolicy.Prepare(c, 0, false).Issues.Count == 0);
            return new CatalogCoverageReport(code, clubs.FirstOrDefault()?.CountryName ?? code, gender, season,
                clubs.Select(c => c.League).Distinct().ToList(), clubs.Count, players.Count, complete, clubs.Count - complete,
                players.Count(p => p.IdentityOrigin == CatalogDataOrigin.Real), players.Count(p => p.StrengthEstimate?.Origin == CatalogDataOrigin.Estimated),
                players.Count(p => p.IdentityOrigin == CatalogDataOrigin.Generated), references,
                references.Select(r => r.Rights?.License ?? (r.IsFixture ? "Fixture" : "Direitos não informados")).Distinct().ToList(),
                selection.Warnings.Where(w => w.StartsWith(code, StringComparison.Ordinal)).Concat(clubs.SelectMany(c => c.CoverageWarnings ?? [])).Distinct().ToList(),
                clubs.Sum(c => c.DocumentedPlayers ?? c.Players.Count), clubs.Sum(c => c.UncertainPlayers ?? 0),
                clubs.Count(c => c.Verification?.Status == CatalogClubVerificationStatus.REAL_CLUB_VERIFIED),
                clubs.Count(c => c.Verification?.Status == CatalogClubVerificationStatus.REAL_CLUB_HISTORICAL),
                clubs.Count(c => c.Verification?.Status == CatalogClubVerificationStatus.REAL_CLUB_UNVERIFIED));
        }).ToList();
    }
    public async Task<WorldCreatorPreview> Preview(Guid owner, WorldCreatorRequest request, CancellationToken ct = default)
    {
        request = NormalizeCatalogRequest(request);
        using var optionsScope=factory.CreateScope();
        if(request.UseWorldCatalogRules && optionsScope.ServiceProvider.GetService<World016B.WorldCatalogOptions016B>() is {} settings)request=request with {EliteFullSquadRankCutoff=settings.EliteFullSquadRankCutoff};
        var selection = await SelectionForRequest(request,ct);
        var preview = WorldCreatorEngine.Preview(request, selection);
        using var scope = factory.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        db.Drafts.Add(new() { Id = preview.DraftId, OwnerId = owner, RequestJson = JsonSerializer.Serialize(request, CatalogImportCodec.Json),
            PreviewJson = JsonSerializer.Serialize(preview, CatalogImportCodec.Json), ExpiresAt = DateTime.UtcNow.AddHours(2) });
        await db.SaveChangesAsync(ct); return preview;
    }
    public async Task<WorldCreatorRequest?> LastConfiguration(Guid owner, CancellationToken ct = default)
    {
        using var scope = factory.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var preset = await db.Presets.AsNoTracking().SingleOrDefaultAsync(p => p.OwnerId == owner, ct);
        return preset == null ? null : JsonSerializer.Deserialize<WorldCreatorRequest>(preset.RequestJson, CatalogImportCodec.Json);
    }
    public async Task<GameWorld> Confirm(Guid owner, Guid draftId, CancellationToken ct = default)
    {
        using var scope = factory.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var draft = await db.Drafts.AsNoTracking().SingleOrDefaultAsync(d => d.Id == draftId && d.OwnerId == owner, ct)
            ?? throw new KeyNotFoundException("Prévia não encontrada para este usuário.");
        if (draft.ExpiresAt < DateTime.UtcNow && draft.WorldJson == null) throw new ArgumentException("Prévia expirada; gere uma nova.");
        using var receiptType=JsonDocument.Parse(draft.RequestJson);
        if(receiptType.RootElement.TryGetProperty("assemblyConfirmation",out _))throw new ArgumentException("Use a confirmação da montagem privada.");
        var request = JsonSerializer.Deserialize<WorldCreatorRequest>(draft.RequestJson, CatalogImportCodec.Json)!;
        if (draft.WorldJson == null)
        {
            var preview = JsonSerializer.Deserialize<WorldCreatorPreview>(draft.PreviewJson, CatalogImportCodec.Json)!;
            foreach (var reference in preview.References)
                if (CatalogRightsPolicy.WorldIssue(reference, DateTime.UtcNow, request.CatalogYears != null) is { } issue)
                    throw new ArgumentException(issue);
            var generated = WorldCreatorEngine.Create(request, preview);
            if(request.RequireHostStart||request.UseWorldCatalogRules)generated.InitialLobby=new(){HostCoachId=owner};
            generated.GameModeId=request.GameModeId;
            var json = JsonSerializer.Serialize(generated, CatalogImportCodec.Json);
            await db.Drafts.Where(d => d.Id == draftId && d.OwnerId == owner && d.WorldJson == null)
                .ExecuteUpdateAsync(d => d.SetProperty(x => x.WorldId, generated.Id).SetProperty(x => x.WorldJson, json), ct);
            draft = await db.Drafts.AsNoTracking().SingleAsync(d => d.Id == draftId, ct);
        }
        var world = await worlds.GetWorldAsync(draft.WorldId!.Value, ct);
        if (world == null)
        {
            world = JsonSerializer.Deserialize<GameWorld>(draft.WorldJson!, CatalogImportCodec.Json)!;
            await worlds.SaveWorldAsync(world, ct);
        }
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO \"wc013a_WorldCreationPreset\" (\"OwnerId\", \"RequestJson\", \"UpdatedAt\") VALUES ({owner}, {draft.RequestJson}, {DateTime.UtcNow}) ON CONFLICT (\"OwnerId\") DO UPDATE SET \"RequestJson\" = EXCLUDED.\"RequestJson\", \"UpdatedAt\" = EXCLUDED.\"UpdatedAt\"", ct);
        return world;
    }
}
