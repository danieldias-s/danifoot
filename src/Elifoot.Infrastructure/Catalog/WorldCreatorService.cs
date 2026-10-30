using System.Text.Json;
using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elifoot.Infrastructure.Catalog;

public sealed class WorldCreatorService(IServiceScopeFactory factory, CatalogSyncService sync, IWorldRepository worlds)
{
    public static readonly (string Code, string Name)[] PriorityCountries = [("BR", "Brasil"), ("AR", "Argentina"),
        ("GB", "Inglaterra"), ("ES", "Espanha"), ("DE", "Alemanha"), ("IT", "Itália"), ("FR", "França"), ("PT", "Portugal")];
    public async Task<List<CatalogCountryOption>> Countries(string season, CancellationToken ct = default)
    {
        using var scope = factory.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
        var countries = await db.Countries.AsNoTracking().ToListAsync(ct);
        if (countries.Count == 0) return [];
        var male = await Select(countries.Select(x => x.Code).ToList(), FootballGender.Male, season, true, ct);
        var female = await Select(countries.Select(x => x.Code).ToList(), FootballGender.Female, season, true, ct);
        return countries.OrderBy(c => c.Name).Select(c => new CatalogCountryOption(c.Code, c.Name,
            male.Clubs.Count(x => x.CountryCode == c.Code), female.Clubs.Count(x => x.CountryCode == c.Code))).ToList();
    }
    public async Task<CatalogSelection> Select(List<string> countries, FootballGender gender, string season, bool saved, CancellationToken ct = default)
    {
        if (countries == null || countries.Count == 0 || countries.Count > 64 || countries.Distinct().Count() != countries.Count || !Enum.IsDefined(gender) || string.IsNullOrWhiteSpace(season))
            throw new ArgumentException("Seleção de países/categoria/temporada inválida.");
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
            var reference = new CatalogReference(shot.Id, state.ToKey(), shot.Version, shot.UpdatedAt, shot.ValidatedAt, shot.IsFixture, status, warning);
            refs.Add(reference); packages.Add((CatalogImportCodec.ReadJson(shot.PayloadJson), reference));
        }
        var clubPackages = packages.Where(p => p.Data.Scope.DataType == CatalogDataType.Clubs)
            .GroupBy(p => (p.Data.Scope.CountryCode, p.Data.Scope.League)).Select(g => g.OrderByDescending(p => p.Ref.UpdatedAt).ThenBy(p => p.Data.Scope.Provider).First());
        var clubs = new Dictionary<string, EligibleCatalogClub>();
        foreach (var (data, reference) in clubPackages)
        foreach (var club in data.Clubs)
        {
            string key = WorldCreatorEngine.ClubKey(data.Scope.CountryCode, gender, data.Scope.Provider, club.ExternalId);
            var related = packages.Where(p => p.Data.Scope.CountryCode == data.Scope.CountryCode &&
                (p.Data.Scope.League == data.Scope.League || p.Data.Scope.League == "*" || data.Scope.League == "*")).ToList();
            var squad = related.Where(p => p.Data.Scope.DataType == CatalogDataType.Squads &&
                p.Data.Players.Any(a => a.ClubProvider == data.Scope.Provider && a.ClubExternalId == club.ExternalId))
                .OrderByDescending(p => p.Ref.UpdatedAt).ThenBy(p => p.Data.Scope.Provider).FirstOrDefault();
            var ranking = related.Where(p => p.Data.Scope.DataType == CatalogDataType.Rankings &&
                p.Data.Rankings.Any(r => r.ClubProvider == data.Scope.Provider && r.ClubExternalId == club.ExternalId))
                .OrderByDescending(p => p.Ref.UpdatedAt).ThenBy(p => p.Data.Scope.Provider).FirstOrDefault();
            var players = squad.Data?.Players.Where(p => p.ClubProvider == data.Scope.Provider && p.ClubExternalId == club.ExternalId).ToList() ?? [];
            int? rank = ranking.Data?.Rankings.Single(r => r.ClubProvider == data.Scope.Provider && r.ClubExternalId == club.ExternalId).Rank;
            var used = new List<CatalogReference> { reference };
            if (squad.Data != null) used.Add(squad.Ref);
            if (ranking.Data != null) used.Add(ranking.Ref);
            clubs.TryAdd(key, new(key, club.ExternalId, data.Scope.Provider, club.Name, club.ShortName, data.Scope.CountryCode,
                data.CountryName, gender, club.LeagueName, season, club.PrimaryColor, club.SecondaryColor, rank, players, used));
        }
        // Prefer a single supplier per named team rather than counting alternate provider IDs as extra clubs.
        var unique = clubs.Values.GroupBy(c => (c.CountryCode, Name: c.Name.Trim().ToUpperInvariant()))
            .Select(g => g.OrderBy(c => c.Provider, StringComparer.Ordinal).First()).ToList();
        foreach (var country in countries)
            if (!unique.Any(c => c.CountryCode == country)) warnings.Add($"{country}: sem cobertura válida para {gender}/{season}.");
        if (refs.Any(r => r.IsFixture)) warnings.Add("FIXTURES DE TESTE — clubes, atletas e rankings fictícios; nenhuma carga real.");
        return new(gender, season, countries, unique, refs, warnings);
    }
    public async Task<WorldCreatorPreview> Preview(Guid owner, WorldCreatorRequest request, CancellationToken ct = default)
    {
        var selection = await Select(request.Countries, request.Gender, request.Season, request.UseSavedData, ct);
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
        var request = JsonSerializer.Deserialize<WorldCreatorRequest>(draft.RequestJson, CatalogImportCodec.Json)!;
        if (draft.WorldJson == null)
        {
            var preview = JsonSerializer.Deserialize<WorldCreatorPreview>(draft.PreviewJson, CatalogImportCodec.Json)!;
            var generated = WorldCreatorEngine.Create(request, preview);
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
