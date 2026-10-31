using System.Globalization;
using System.Text.Json;
using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Domain;
using Elifoot.Infrastructure;
using Elifoot.Infrastructure.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elifoot.RealDataImporter;

public static class CoverageAudit
{
    private sealed class OfflineSource(CatalogScopeKey scope) : ICatalogSource
    {
        public CatalogScopeKey Scope => scope;
        public Task<CatalogImportPackage> FetchAsync(CancellationToken ct) => throw new HttpRequestException("Controlled offline verification, no network request");
    }
    public static async Task Run(string outputDirectory, string connectionEnvironment)
    {
        string connection = Environment.GetEnvironmentVariable(connectionEnvironment) ?? throw new ArgumentException("Conexão isolada ausente.");
        var parsed = new Npgsql.NpgsqlConnectionStringBuilder(connection);
        if (!LocalCatalogTarget.Catalog(parsed)) throw new ArgumentException("Banco isolado obrigatório.");
        var worldConnection = Environment.GetEnvironmentVariable(parsed.Port == 55435 ? "ELIFOOT_014A_REAL_WORLD_PG" : "ELIFOOT_013B_WORLD_PG") ?? throw new ArgumentException("Configure explicitamente o banco de mundos isolado; sem fallback.");
        var worldParsed = new Npgsql.NpgsqlConnectionStringBuilder(worldConnection);
        if (!LocalCatalogTarget.Worlds(parsed, worldParsed)) throw new ArgumentException("Banco de mundos isolado obrigatório.");
        var services = new ServiceCollection(); services.AddDbContext<CatalogDbContext>(o => o.UseNpgsql(connection));
        services.AddDbContext<ElifootDbContext>(o => o.UseNpgsql(worldConnection)); services.AddSingleton<IWorldRepository, EfWorldRepository>();
        services.AddSingleton(new CatalogSyncOptions()); services.AddSingleton<CatalogSyncService>(); services.AddSingleton<WorldCreatorService>();
        await using var provider = services.BuildServiceProvider(); var service = provider.GetRequiredService<WorldCreatorService>();
        using (var testScope = provider.CreateScope())
        {
            var db = testScope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            var state = await db.Scopes.SingleAsync(s => s.CountryCode == "BR" && s.Gender == FootballGender.Male && s.Provider == "openfootball" && s.DataType == CatalogDataType.Clubs);
            var shot = await db.Snapshots.SingleAsync(s => s.Id == state.ActiveSnapshotId);
            await db.Scopes.Where(s => s.Key == state.Key).ExecuteUpdateAsync(s => s.SetProperty(x => x.NextAttempt, DateTime.UtcNow.AddDays(-1)));
            var offline = new CatalogSyncService(provider.GetRequiredService<IServiceScopeFactory>(), [new OfflineSource(state.ToKey())], new CatalogSyncOptions { Retries = 0 });
            await offline.RefreshAsync(["BR"], FootballGender.Male, "2026");
            var cached = await service.Select(["BR"], FootballGender.Male, "2026", true);
            var reference = cached.References.Single(r => r.Scope == state.ToKey());
            if (reference.Status != "Fallback" || reference.SnapshotId != shot.Id) throw new ArgumentException("Falha não preservou o último snapshot.");
            Directory.CreateDirectory(outputDirectory);
            await File.WriteAllTextAsync(Path.Combine(outputDirectory, "fallback-postgres.json"), JsonSerializer.Serialize(new { reference.SnapshotId, reference.Status, reference.Warning, noExternalRequests = true, sameSnapshot = true }, CatalogImportCodec.Json));
            await provider.GetRequiredService<CatalogSyncService>().ImportAsync(CatalogImportCodec.ReadJson(shot.PayloadJson));
        }
        var countries = WorldCreatorService.PriorityCountries.Select(c => c.Code).ToList(); var reports = new List<CatalogCoverageReport>();
        var selections = new List<CatalogSelection>(); var worlds = new List<object>(); var owner = CatalogIdentityService.StableId("coverage-controlled-audit-owner");
        var previousWorlds = await provider.GetRequiredService<IWorldRepository>().ListWorldsAsync();
        var before = previousWorlds.ToDictionary(w => w.Id, w => JsonSerializer.Serialize(w, CatalogImportCodec.Json));
        foreach (var gender in Enum.GetValues<FootballGender>())
        {
            reports.AddRange(await service.Coverage(countries, gender, "2026"));
            var selection = await service.Select(countries, gender, "2026", true); selections.Add(selection);
            var verified = selection.Clubs.Where(c => c.Verification?.Status == CatalogClubVerificationStatus.REAL_CLUB_VERIFIED).ToList();
            var reserved = verified.OrderBy(c => c.Rank ?? int.MaxValue).ThenBy(c => c.Key, StringComparer.Ordinal).Take(32).Select(c => c.Key).ToHashSet();
            var representatives = countries.Select(code => verified.Where(c => c.CountryCode == code).OrderByDescending(c => c.Players.Count).ThenBy(c => c.Key, StringComparer.Ordinal).FirstOrDefault())
                .Where(c => c != null && !reserved.Contains(c.Key)).Select(c => new ClubAssignment(c!.Key, 5)).ToList();
            var request = new WorldCreatorRequest($"AUDITORIA COVERAGE {gender}: clubes reais + atletas fictícios autorizados", gender, "2026", countries,
                WorldAssemblyMode.Automatic, DistrictFillMode.Random, 130013, representatives, UseSavedData: true, MaxHumans: 1);
            var strict = WorldCreatorEngine.Preview(request, selection);
            var preview = await service.Preview(owner, request with { AllowFictionalPlayers = true });
            if (preview.Errors.Count > 0) throw new ArgumentException(string.Join("; ", preview.Errors.Take(8)));
            if (preview.Clubs.Count != 128 || preview.Clubs.Any(c => c.Club.IsFictional || c.Club.Verification?.Status != CatalogClubVerificationStatus.REAL_CLUB_VERIFIED))
                throw new ArgumentException("Auditoria não obteve 128 identidades reais verificadas, sem clubes fictícios/históricos.");
            var world = await service.Confirm(owner, preview.DraftId); await provider.GetRequiredService<IWorldRepository>().SaveWorldAsync(world);
            var original = JsonSerializer.Serialize(world, CatalogImportCodec.Json); EfWorldRepository.ClearCacheForTesting();
            var recovered = await provider.GetRequiredService<IWorldRepository>().GetWorldAsync(world.Id);
            if (JsonSerializer.Serialize(recovered, CatalogImportCodec.Json) != original) throw new ArgumentException("Snapshot alterado na recuperação.");
            worlds.Add(new { gender, worldId = world.Id, preview.DraftId, strictRealOnlyErrors = strict.Errors, realClubs = world.Clubs.Count, representedCountries = preview.Clubs.Select(c => c.Club.CountryCode).Distinct().Order().ToList(),
                realPlayers = preview.Clubs.SelectMany(c => c.Club.Players).Count(p => p.IdentityOrigin == CatalogDataOrigin.Real),
                generatedPlayers = preview.Clubs.SelectMany(c => c.Club.Players).Count(p => p.IdentityOrigin == CatalogDataOrigin.Generated),
                hasFictionalClubs = preview.Clubs.Any(c => c.Club.IsFictional), snapshotHash = world.CatalogOrigin!.DataHash, reloadVerified = true,
                mode = "CONTROLLED TEST: real club identities, explicitly generated roster completion, NOT complete real squads" });
        }
        EfWorldRepository.ClearCacheForTesting();
        foreach (var pair in before)
        {
            var loaded = await provider.GetRequiredService<IWorldRepository>().GetWorldAsync(pair.Key);
            if (JsonSerializer.Serialize(loaded, CatalogImportCodec.Json) != pair.Value) throw new ArgumentException("Mundo existente foi alterado.");
        }
        Directory.CreateDirectory(outputDirectory);
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "coverage.json"), JsonSerializer.Serialize(new { reports, worlds, existingWorldsPreserved = before.Count }, new JsonSerializerOptions(CatalogImportCodec.Json) { WriteIndented = true }));
        await File.WriteAllTextAsync(Path.Combine(outputDirectory, "catalog-selections.json"), JsonSerializer.Serialize(selections, CatalogImportCodec.Json));
        string Csv(object? value) => "\"" + Convert.ToString(value, CultureInfo.InvariantCulture)?.Replace("\"", "\"\"") + "\"";
        var clubLines = new List<string> { "Country,Gender,Season,Name,Provider,ExternalId,InternalCatalogClubId,Status,CurrentSeasonConfirmed,SourceSeason,DocumentedPlayers,CurrentUsablePlayers,UncertainPlayers,RankingMethod,RankingScore,RankingConfidence,Evidence" };
        foreach (var selection in selections)
        foreach (var c in selection.Clubs) clubLines.Add(string.Join(",", new object?[] { c.CountryCode, c.Gender, c.Season, c.Name, c.Provider, c.ExternalId, c.InternalCatalogClubId,
            c.Verification?.Status, c.Verification?.CurrentSeasonConfirmed, c.Verification?.SourceSeason, c.DocumentedPlayers, c.Players.Count, c.UncertainPlayers,
            c.RankingInfo?.Method, c.RankingInfo?.Score, c.RankingInfo?.Confidence, c.Verification?.Evidence }.Select(Csv)));
        await File.WriteAllLinesAsync(Path.Combine(parsed.Port == 55435 ? outputDirectory : "docs", "REAL_CLUBS_BY_COUNTRY_013B.csv"), clubLines);
        var rosterLines = new List<string> { "Country,Gender,Season,Clubs,VerifiedClubs,HistoricalClubs,UnverifiedClubs,DocumentedPlayerLinks,UncertainPlayerLinks,CurrentUsablePlayers,CompleteCurrentSquads,IncompleteCurrentSquads,GeneratedInCatalog" };
        foreach (var r in reports) rosterLines.Add(string.Join(",", new object?[] { r.CountryCode, r.Gender, r.Season, r.Clubs, r.VerifiedClubs, r.HistoricalClubs, r.UnverifiedClubs,
            r.DocumentedPlayers, r.UncertainPlayers, r.Players, r.CompleteSquads, r.PartialSquads, r.GeneratedPlayers }.Select(Csv)));
        await File.WriteAllLinesAsync(Path.Combine(parsed.Port == 55435 ? outputDirectory : "docs", "REAL_ROSTERS_BY_COUNTRY_013B.csv"), rosterLines);
        Console.WriteLine(JsonSerializer.Serialize(new { clubs = selections.Sum(s => s.Clubs.Count), documentedLinks = reports.Sum(r => r.DocumentedPlayers), currentUsablePlayers = reports.Sum(r => r.Players),
            completeCurrentRosters = reports.Sum(r => r.CompleteSquads), controlledWorlds = worlds.Count, existingWorldsPreserved = before.Count }, CatalogImportCodec.Json));
    }
}
