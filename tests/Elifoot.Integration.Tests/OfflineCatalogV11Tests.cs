using System.Text.Json;
using System.Text.Json.Nodes;
using Elifoot.Contracts;
using Elifoot.Domain;
using Elifoot.Infrastructure.Catalog;

namespace Elifoot.Integration.Tests;

public sealed class OfflineCatalogV11Tests
{
    private sealed class Files : IDisposable
    {
        private readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("elifoot-v11-test-");
        public string Root => folder.FullName;
        public string Identity => Path.Combine(Root, "identidades", "synthetic", "identidade.json");
        public string Annual => Path.Combine(Root, "brasil", "1998", "clubes", "c.json");
        public async Task Save(string path, object value)
        { Directory.CreateDirectory(Path.GetDirectoryName(path)!); await File.WriteAllTextAsync(path, JsonSerializer.Serialize(value, OfflineCatalogLoader.Json)); }
        public async Task Edit(string path, Action<JsonObject> edit)
        { var node = JsonNode.Parse(await File.ReadAllTextAsync(path))!.AsObject(); edit(node); await File.WriteAllTextAsync(path, node.ToJsonString()); }
        public async Task Write(int year = 1998)
        {
            await Save(Identity, new OfflineIdentityDefinition("1.1", "synthetic", "Clube sintético", "Sintético", "SYN", ["#123456", "#FFFFFF"], null));
            await Save(Path.Combine(Root, "brasil", year.ToString(), "manifest.json"),
                new OfflineManifest("1.1", $"br-{year}", new("BR", "Brasil", "brasil"), year, "Sintético", "READY", ["clubes/c.json"]));
            var players = Enumerable.Range(0, 11).Select(i => new OfflinePlayerV11($"p-{i}", $"Atleta sintético {i}",
                i == 0 ? "G" : i < 5 ? "D" : i < 9 ? "M" : "A", "BR", i % 6, i == 10)).ToList();
            await Save(Path.Combine(Root, "brasil", year.ToString(), "clubes", "c.json"), new OfflineClubV11("1.1", "synthetic", "BR", year, 20,
                new(200, "MANUAL:synthetic-scale", new(year, 7, 1)), 1, null,
                new("coach", "Treinador sintético", "BR", new(["4-4-2", "4-3-3"], ["5-3-2", "4-5-1"])), players, "READY"));
        }
        public Task<OfflineCatalogReport> Load() => new OfflineCatalogLoader(Root).LoadAsync();
        public void Dispose()
        { Assert.StartsWith(Path.GetFullPath(Path.GetTempPath()), folder.FullName, StringComparison.OrdinalIgnoreCase); Assert.StartsWith("elifoot-v11-test-", folder.Name); folder.Delete(true); }
    }
    private static List<EligibleCatalogClub> Clubs(OfflineCatalogReport report) => report.Catalogs.SelectMany(c =>
        c.Clubs.Select(s => OfflineWorldFactory.Eligible(s, c.Manifest!.Gender))).ToList();

    [Fact]
    public async Task IdentityIsSharedAcrossYearsWithOneCachedCrestAndFrozenHash()
    {
        using var files = new Files(); await files.Write(); await files.Write(2026);
        await files.Edit(files.Identity, n => n["crestFile"] = "escudo.png");
        string crest = Path.Combine(Path.GetDirectoryName(files.Identity)!, "escudo.png");
        await File.WriteAllBytesAsync(crest, [137,80,78,71,13,10,26,10]);
        var report = await files.Load(); Assert.True(report.Valid); Assert.All(report.Catalogs, c => Assert.True(c.Ready));
        var a = report.Catalogs[0].Clubs[0]; var b = report.Catalogs[1].Clubs[0];
        Assert.Same(a.V11!.Identity, b.V11!.Identity); Assert.Same(a.CrestDataUrl, b.CrestDataUrl);
        Assert.Null(a.Definition.Stadium); Assert.All(a.Definition.Players!, p => Assert.Null(p.Strength));
        Assert.Empty(OfflineWorldFactory.Eligible(a, FootballGender.Male).Players);
        string frozen = JsonSerializer.Serialize(a);
        await files.Edit(files.Identity, n => n["name"] = "Nome posterior");
        var after = await files.Load(); Assert.NotEqual(a.SourceHash, after.Catalogs[0].Clubs[0].SourceHash);
        Assert.Equal(frozen, JsonSerializer.Serialize(a));
        var restored = JsonSerializer.Deserialize<OfflineClubSource>(frozen)!;
        Assert.Equal(a.V11.Identity.Name, restored.V11!.Identity.Name);
        Assert.Equal(a.CrestDataUrl, restored.CrestDataUrl);
    }

    [Theory]
    [InlineData("colors", "[\"#123456\"]")]
    [InlineData("colors", "[\"blue\",\"#FFFFFF\"]")]
    [InlineData("crestFile", "\"missing.png\"")]
    [InlineData("crestFile", "\"../escape.png\"")]
    [InlineData("crestFile", "\"https://example.invalid/a.png\"")]
    [InlineData("catalogClubId", "\"other\"")]
    public async Task InvalidIdentityNeverBecomesAvailable(string field, string value)
    {
        using var files = new Files(); await files.Write(); await files.Edit(files.Identity, n => n[field] = JsonNode.Parse(value));
        var report = await files.Load(); Assert.False(report.Valid); Assert.Contains(report.Issues, i => i.Code == "IDENTITY");
    }

    [Fact]
    public async Task MissingAndDuplicateIdentityAreRejected()
    {
        using var files = new Files(); await files.Write();
        var copy = Path.Combine(files.Root, "identidades", "another", "identidade.json");
        Directory.CreateDirectory(Path.GetDirectoryName(copy)!); File.Copy(files.Identity, copy);
        Assert.False((await files.Load()).Valid);
        File.Delete(files.Identity);
        var report = await files.Load(); Assert.False(report.Valid); Assert.Empty(report.Catalogs[0].Clubs);
    }

    [Fact]
    public async Task SameGlobalClubCannotAppearInTwoCountriesInSameYear()
    {
        using var files = new Files(); await files.Write();
        var annual = JsonSerializer.Deserialize<OfflineClubV11>(await File.ReadAllTextAsync(files.Annual), OfflineCatalogLoader.Json)!;
        await files.Save(Path.Combine(files.Root, "espanha", "1998", "manifest.json"),
            new OfflineManifest("1.1", "es-1998", new("ES", "Espanha", "espanha"), 1998, "Sintético", "READY", ["clubes/c.json"]));
        await files.Save(Path.Combine(files.Root, "espanha", "1998", "clubes", "c.json"), annual with {
            Country = "ES", Coach = annual.Coach! with { Id = "other-coach" }, Players = annual.Players!.Select(p => p with { Id = "other-"+p.Id }).ToList() });
        var report = await files.Load(); Assert.False(report.Valid);
        Assert.All(report.Catalogs, c => Assert.Contains(c.Issues, i => i.Code == "DUPLICATE_CLUB"));
    }

    [Theory]
    [InlineData("strength", "25")]
    [InlineData("strength", "null")]
    [InlineData("name", "null")]
    [InlineData("position", "\"GK\"")]
    [InlineData("nationality", "\"XX\"")]
    [InlineData("behavior", "6")]
    [InlineData("behavior", "-1")]
    [InlineData("behavior", "null")]
    [InlineData("isStar", "true")]
    public async Task InvalidV11PlayersAreRejected(string field, string value)
    {
        using var files = new Files(); await files.Write(); await files.Edit(files.Annual, n => n["players"]![0]![field] = JsonNode.Parse(value));
        Assert.False((await files.Load()).Valid);
    }

    [Theory]
    [InlineData("G", false)] [InlineData("D", false)] [InlineData("M", true)] [InlineData("A", true)]
    public async Task StarRestrictionUsesPositionOnly(string position, bool valid)
    {
        using var files = new Files(); await files.Write();
        await files.Edit(files.Annual, n => { n["players"]![10]!["position"] = position; n["players"]![10]!["isStar"] = true; });
        Assert.Equal(valid, (await files.Load()).Valid);
    }

    [Fact]
    public async Task DraftUnknownBehaviorStaysNullAndNeverDefaultsToFairPlay()
    {
        using var files = new Files(); await files.Write();
        await files.Edit(files.Annual, n => { n["status"] = "DRAFT"; n["players"]![0]!["behavior"] = null; });
        await files.Edit(Path.Combine(files.Root, "brasil", "1998", "manifest.json"), n => n["status"] = "DRAFT");
        var report = await files.Load(); Assert.True(report.Valid); Assert.False(report.Catalogs[0].Ready);
        Assert.Null(report.Catalogs[0].Clubs[0].Definition.Players![0].Behavior);
    }

    [Theory]
    [InlineData(1, true)] [InlineData(20, true)] [InlineData(0, false)] [InlineData(21, false)]
    public async Task TeamStrengthRange(int strength, bool valid)
    {
        using var files = new Files(); await files.Write(); await files.Edit(files.Annual, n => n["teamStrength"] = strength);
        Assert.Equal(valid, (await files.Load()).Valid);
    }

    [Theory]
    [InlineData("name", "\"Duplicated name\"")]
    [InlineData("colors", "[\"#123456\",\"#FFFFFF\"]")]
    [InlineData("teamStrength", "null")]
    [InlineData("historicalRanking", "{\"rating\":20,\"source\":\"x\",\"referenceDate\":\"2026-01-01\"}")]
    [InlineData("historicalRanking", "{\"rating\":20,\"source\":null,\"referenceDate\":\"1998-01-01\"}")]
    public async Task InvalidAnnualDataAreRejected(string field, string value)
    {
        using var files = new Files(); await files.Write(); await files.Edit(files.Annual, n => n[field] = JsonNode.Parse(value));
        Assert.False((await files.Load()).Valid);
    }

    private static async Task<List<EligibleCatalogClub>> Candidates(Files files, int count)
    {
        await files.Write(); var first = Clubs(await files.Load())[0];
        return Enumerable.Range(0, count).Select(i => first with { Key = $"c-{i:000}", CountryCode = i % 3 == 0 ? "ES" : i % 3 == 1 ? "GB" : "IT",
            OfflineSource = first.OfflineSource! with { V11 = first.OfflineSource!.V11! with {
                TeamStrength = i < 9 ? 20 : Math.Max(1, 19 - i / 20), HistoricalRanking = new(1000 - i, "MANUAL:synthetic", new(1998, 7, 1)), TieBreakPriority = null } } }).ToList();
    }
    private static OfflineWorldRequest Request(List<string>? keys = null, List<ClubAssignment>? assignments = null) => new("Sintético", ["br-1998"], 72, ClubKeys: keys, Assignments: assignments);

    [Fact]
    public async Task NineForce20RankByComparableRatingAndNinthKeeps20InSecondDivision()
    {
        using var files = new Files(); var candidates = await Candidates(files, 128);
        var preview = OfflineWorldFactory.Preview(Request(), candidates.AsEnumerable().Reverse().ToList());
        Assert.Equal(Enumerable.Range(0, 8).Select(i => $"c-{i:000}"), preview.Clubs.Where(c => c.Division == 1).Select(c => c.Club.Key));
        var ninth = preview.Clubs.Single(c => c.Club.Key == "c-008"); Assert.Equal(2, ninth.Division); Assert.Equal(20, ninth.Club.OfflineSource!.V11!.TeamStrength);
        Assert.Equal([8,8,8,8,96], Enumerable.Range(1,5).Select(d => preview.Clubs.Count(c => c.Division == d)));
        Assert.Equal([OfflineCatalogRanking.ForceBlock], preview.Errors);
        Assert.Contains("bloqueada", Assert.Throws<ArgumentException>(() => OfflineWorldFactory.Create(Request(), preview)).Message);
        Assert.All(preview.Clubs, c => Assert.Empty(c.Club.Players));
    }

    [Fact]
    public async Task IncompatibleRankingsAndUnresolvedBoundaryRequireManualDecision()
    {
        using var files = new Files(); var candidates = await Candidates(files, 128);
        candidates[0] = candidates[0] with { OfflineSource = candidates[0].OfflineSource! with { V11 = candidates[0].OfflineSource!.V11! with { HistoricalRanking = new(1e9, "INCOMPATIBLE", new(1998,7,1)) } } };
        var preview = OfflineWorldFactory.Preview(Request(), candidates);
        Assert.Contains(preview.Warnings, w => w.Contains("incompatíveis")); Assert.Contains(preview.Errors, e => e.StartsWith("Empate"));
        var manual = candidates.Take(9).Select((c,i) => new ClubAssignment(c.Key, i < 8 ? 1 : 2)).ToList();
        var resolved = OfflineWorldFactory.Preview(Request(assignments: manual), candidates);
        Assert.DoesNotContain(resolved.Errors, e => e.StartsWith("Empate"));
        for (int i = 0; i < 9; i++) candidates[i] = candidates[i] with { OfflineSource = candidates[i].OfflineSource! with { V11 = candidates[i].OfflineSource!.V11! with { TieBreakPriority = 9-i } } };
        resolved = OfflineWorldFactory.Preview(Request(), candidates);
        Assert.DoesNotContain(resolved.Errors, e => e.StartsWith("Empate"));
        Assert.Equal("c-000", resolved.Clubs.First(c => c.Division == 2).Club.Key);
    }

    [Fact]
    public async Task ThreeCountries384CandidatesKeepAllAndManual128SelectionIsExplicit()
    {
        using var files = new Files(); var candidates = await Candidates(files, 384);
        var ranked = OfflineCatalogRanking.Order(candidates); Assert.Equal(384, ranked.Count);
        var selected = ranked.Skip(1).Take(128).Select(c => c.Key).ToList();
        var preview = OfflineWorldFactory.Preview(Request(selected), candidates);
        Assert.Equal(128, preview.Clubs.Count); Assert.DoesNotContain(preview.Clubs, c => c.Club.Key == "c-000");
        Assert.Equal(3, preview.Clubs.Select(c => c.Club.CountryCode).Distinct().Count()); Assert.Equal(384, candidates.Count);
        Assert.Contains(OfflineWorldFactory.Preview(Request(), candidates).Errors, e => e.Contains("exatamente 128"));
    }

    [Fact]
    public async Task MissingRankingCanUseManualPriorityAndTieInsideDivisionIsOnlyPresentation()
    {
        using var files = new Files(); var candidates = await Candidates(files, 128);
        candidates = candidates.Select((c, i) => c with { OfflineSource = c.OfflineSource! with { V11 = c.OfflineSource!.V11! with {
            HistoricalRanking = null, TieBreakPriority = i < 2 ? 0 : i } } }).ToList();
        var preview = OfflineWorldFactory.Preview(Request(), candidates);
        Assert.Equal([OfflineCatalogRanking.ForceBlock], preview.Errors);
        Assert.Equal(candidates[0].Key, preview.Clubs[0].Club.Key);
        await files.Edit(files.Annual, n => { n["historicalRanking"] = null; n["tieBreakPriority"] = 1; });
        Assert.True((await files.Load()).Catalogs[0].Ready);
    }

    [Fact]
    public async Task LoaderAndSelectionRead384RealSyntheticFilesAcrossThreeCountriesWithoutQuotas()
    {
        using var files = new Files(); await files.Write();
        var original = JsonSerializer.Deserialize<OfflineClubV11>(await File.ReadAllTextAsync(files.Annual), OfflineCatalogLoader.Json)!;
        var identity = JsonSerializer.Deserialize<OfflineIdentityDefinition>(await File.ReadAllTextAsync(files.Identity), OfflineCatalogLoader.Json)!;
        int index = 0;
        foreach (var (slug, code) in new[] { ("espanha", "ES"), ("inglaterra", "GB"), ("italia", "IT") })
        {
            var references = new List<string>();
            for (int j = 0; j < 128; j++, index++)
            {
                string id = $"synthetic-{index:000}";
                await files.Save(Path.Combine(files.Root, "identidades", id, "identidade.json"), identity with { CatalogClubId = id });
                var annual = original with { CatalogClubId = id, Country = code, HistoricalRanking = new(1000-index, "MANUAL:synthetic", new(1998,7,1)),
                    Coach = original.Coach! with { Id = $"coach-{index}" }, Players = original.Players!.Select(p => p with { Id = $"{id}-{p.Id}" }).ToList() };
                string reference = $"clubes/c-{j:000}.json"; references.Add(reference);
                await files.Save(Path.Combine(files.Root, slug, "1998", reference), annual);
            }
            await files.Save(Path.Combine(files.Root, slug, "1998", "manifest.json"),
                new OfflineManifest("1.1", code, new(code, slug, slug), 1998, "Sintético", "READY", references));
        }
        await files.Save(Path.Combine(files.Root, "_pesquisa", "never-load", "bad.json"), new { ignored = true });
        var loader = new OfflineCatalogLoader(files.Root);
        var report = await loader.LoadAsync(); Assert.True(report.Valid); Assert.All(report.Catalogs, c => Assert.True(c.Ready));
        var service = new OfflineWorldCreatorService(loader, null!, null!);
        var selected = await service.Select(["ES", "GB", "IT"]);
        Assert.Equal(384, selected.Count); Assert.Equal(3, selected.Select(c => c.CountryCode).Distinct().Count());
        Assert.All(selected.Take(128), c => Assert.Equal("ES", c.CountryCode)); // no quota
        Assert.Equal("synthetic-000", selected[0].OfflineSource!.V11!.Identity.CatalogClubId);
        Assert.Equal("synthetic-383", selected[^1].OfflineSource!.V11!.Identity.CatalogClubId);
    }
}
