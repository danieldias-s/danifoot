using System.Text.Json;
using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Domain;
using Elifoot.Infrastructure;
using Elifoot.Infrastructure.Catalog;
using Elifoot.Infrastructure.Catalog.Providers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elifoot.RealDataImporter;

public static class CoverageBundleImporter
{
    public static async Task Run(string file, string connectionEnvironment, CancellationToken ct = default)
    {
        string connection = Environment.GetEnvironmentVariable(connectionEnvironment) ?? throw new ArgumentException("Conexão isolada ausente.");
        var parsed = new Npgsql.NpgsqlConnectionStringBuilder(connection);
        if (parsed.Host != "127.0.0.1" || parsed.Port != 55434 || parsed.Database != "elifoot_013b_catalog")
            throw new ArgumentException("Use somente o catálogo 013B em 127.0.0.1:55434.");
        using var document = JsonDocument.Parse(await File.ReadAllTextAsync(file, ct)); var root = document.RootElement;
        if (!root.TryGetProperty("readyForImport", out var ready) || !ready.GetBoolean()) throw new ArgumentException("Colete e verifique a categoria dos atletas antes da importação.");
        var packages = JsonSerializer.Deserialize<List<CatalogImportPackage>>(root.GetProperty("packages").GetRawText(), CatalogImportCodec.Json)!;
        var services = new ServiceCollection(); services.AddDbContext<CatalogDbContext>(o => o.UseNpgsql(connection));
        services.AddSingleton(new CatalogSyncOptions()); services.AddSingleton<IWorldRepository, InMemoryWorldRepository>();
        services.AddSingleton<CatalogSyncService>(); services.AddSingleton<WorldCreatorService>(); await using var provider = services.BuildServiceProvider();
        var sync = provider.GetRequiredService<CatalogSyncService>();
        foreach (var package in packages.Where(p => p.Scope.DataType == CatalogDataType.Clubs))
        { await sync.ImportAsync(package, ct); Console.WriteLine($"Clubs {package.Scope.CountryCode}/{package.Scope.Gender}/{package.Scope.Provider}: {package.Clubs.Count}"); }
        foreach (var map in root.GetProperty("mappings").EnumerateArray())
        {
            using var scope = provider.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            string country = map.GetProperty("countryCode").GetString()!, targetProvider = map.GetProperty("targetProvider").GetString()!, targetId = map.GetProperty("targetExternalId").GetString()!;
            var gender = Enum.Parse<FootballGender>(map.GetProperty("gender").GetString()!);
            var key = CatalogIdentityService.AliasKey(country, gender, targetProvider, targetId);
            var identity = await db.Set<CatalogClubIdentityAlias>().SingleAsync(a => a.Key == key, ct);
            await CatalogIdentityService.MapAsync(db, new(country, gender, map.GetProperty("provider").GetString()!, map.GetProperty("externalId").GetString()!, identity.ClubId,
                map.GetProperty("evidence").GetString()!), ct);
        }
        foreach (var package in packages.Where(p => p.Scope.DataType == CatalogDataType.Squads))
        { await sync.ImportAsync(package, ct); Console.WriteLine($"Squads {package.Scope.CountryCode}/{package.Scope.Gender}: {package.Players.Count} documented identities; current evidence is separate"); }
        var maleClubs = packages.Where(p => p.Scope.Provider == "openfootball" && p.Scope.DataType == CatalogDataType.Clubs).ToList();
        var countryPrefixes = new Dictionary<string, string> { ["br"] = "BR", ["ar"] = "AR", ["en"] = "GB", ["es"] = "ES", ["de"] = "DE", ["it"] = "IT", ["fr"] = "FR", ["pt"] = "PT" };
        var allMatches = new Dictionary<string, List<CatalogHistoricalResult>>();
        var referenceDate = new DateTime(2026, 10, 30, 23, 59, 59, DateTimeKind.Utc);
        string Normalize(string name) => string.Concat(name.Normalize(System.Text.NormalizationForm.FormD).Where(char.IsLetterOrDigit)).ToLowerInvariant();
        var nativeNames = root.GetProperty("seasonTeams").EnumerateArray().ToList();
        foreach (var result in root.GetProperty("results").EnumerateArray())
        {
            string sourceFile = result.GetProperty("file").GetString()!;
            var prefix = System.Text.RegularExpressions.Regex.Match(sourceFile, @"_([a-z]{2})\.").Groups[1].Value;
            if (!countryPrefixes.TryGetValue(prefix, out string? country)) throw new ArgumentException("País do arquivo de resultados não revisado.");
            var clubs = maleClubs.Single(p => p.Scope.CountryCode == country).Clubs;
            string Resolve(string name)
            {
                var direct = clubs.Where(c => c.Name == name).ToList(); if (direct.Count == 1) return direct[0].ExternalId;
                var records = nativeNames.Where(n => n.GetProperty("country").GetString() == country &&
                    n.GetProperty("aliases").EnumerateArray().Any(a => Normalize(a.GetString()!) == Normalize(name))).ToList();
                var ids = records.SelectMany(r => clubs.Where(c => c.Name == r.GetProperty("name").GetString()).Select(c => c.ExternalId)).Distinct().ToList();
                if (ids.Count != 1) throw new ArgumentException($"Identidade ambígua/não revisada em resultados: {country}/{name}."); return ids[0];
            }
            var matchSet = OpenFootballResultParser.Parse(result.GetProperty("content").GetString()!, referenceDate, Resolve);
            if (!allMatches.TryGetValue(country, out var matches)) { matches = []; allMatches.Add(country, matches); }
            matches.AddRange(matchSet.Results);
        }
        foreach (var pair in allMatches)
        {
            var clubs = maleClubs.Single(p => p.Scope.CountryCode == pair.Key);
            var matches = pair.Value.DistinctBy(m => (m.PlayedAt, m.HomeClubId, m.AwayClubId)).ToList();
            var ranking = DerivedClubRanking.CalculateOpenEvidence(clubs.Clubs.Select(c => c.ExternalId), matches, "openfootball", FootballGender.Male, "2026", referenceDate);
            var latest = matches.Max(m => m.PlayedAt);
            ranking = ranking.Select(r => r with { UpdatedAt = latest }).ToList();
            var package = clubs with { Scope = clubs.Scope with { Provider = "openfootball-derived-elo", DataType = CatalogDataType.Rankings }, Clubs = [], Rankings = ranking,
                ExpectedItems = ranking.Count, UpdatedAt = clubs.UpdatedAt, Version = "pending", RosterAsOf = null };
            package = package with { Version = "open-results-elo-v2-" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(package, CatalogImportCodec.Json))) };
            await sync.ImportAsync(package, ct); Console.WriteLine($"Derived ranking {pair.Key}: {ranking.Count} clubs from {matches.Count} played results; NOT official/global calibrated");
        }
        var service = provider.GetRequiredService<WorldCreatorService>();
        var countries = WorldCreatorService.PriorityCountries.Select(c => c.Code).ToList();
        var male = await service.Coverage(countries, FootballGender.Male, "2026", ct); var female = await service.Coverage(countries, FootballGender.Female, "2026", ct);
        await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(file)!, "database-coverage.json"), JsonSerializer.Serialize(new { male, female }, CatalogImportCodec.Json), ct);
    }
}
