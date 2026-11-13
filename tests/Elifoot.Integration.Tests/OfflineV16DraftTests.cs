using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Domain;
using Elifoot.Infrastructure.Catalog;

namespace Elifoot.Integration.Tests;

public sealed class OfflineV16DraftTests
{
    private static string Root()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory.Parent != null && !File.Exists(Path.Combine(directory.FullName,"Elifoot.slnx"))) directory=directory.Parent;
        return Path.Combine(directory.FullName,"catalogos");
    }
    [Fact]
    public async Task ConsolidatedSpanishDraftIsStructurallyValidButCannotCreateWorld()
    {
        var report=await new OfflineCatalogLoader(Root()).LoadAsync();
        Assert.True(report.Valid);
        var catalog=Assert.Single(report.Catalogs,c=>c.Manifest?.CatalogId=="es-2026-principal");
        Assert.False(catalog.Ready);Assert.Equal("DRAFT",catalog.Manifest!.Status);
        Assert.Equal(172, catalog.Clubs.Count);
        Assert.Equal(172, catalog.Manifest.Clubs!.Distinct().Count());
        Assert.Single(catalog.Manifest.Clubs!,c=>c=="clubes/real-madrid.json");
        var club=Assert.Single(catalog.Clubs,c=>c.Definition.CatalogClubId=="real-madrid");
        Assert.Equal("DRAFT",club.Definition.Status);
        Assert.Equal(20, club.V11!.TeamStrength);
        Assert.Equal(15d, club.V11.HistoricalRanking!.Rating);
        Assert.Null(club.V11.TieBreakPriority);
        Assert.Equal("jose-mourinho-2026", club.Definition.Coach!.Id);
        Assert.Equal(82, catalog.Clubs.Count(c => c.V11!.HistoricalRanking != null));
        Assert.Equal(90, catalog.Clubs.Count(c => c.V11!.HistoricalRanking == null));
        Assert.Equal(23,club.Definition.Players!.Count);
        Assert.All(club.Definition.Players,p=>{Assert.Null(p.Strength);Assert.Null(p.Behavior);});
        Assert.Contains(catalog.Issues,i=>i.Code=="RANKING_RESOLUTION"&&!i.IsError);
        Assert.Contains(catalog.Issues,i=>i.Code=="BEHAVIOR"&&!i.IsError);
        var service=new OfflineWorldCreatorService(new OfflineCatalogLoader(Root()),null!,null!);
        await Assert.ThrowsAsync<ArgumentException>(()=>service.Select(["es-2026-principal"]));
        var candidates = await service.SelectForPlanning(["es-2026-principal"]);
        Assert.Equal(172, candidates.Count);
        Assert.Equal(172, candidates.Select(c => c.Key).Distinct().Count());
        Assert.Equal(3905, candidates.Sum(c => c.OfflineSource!.Definition.Players!.Count));
        Assert.All(candidates, c =>
        {
            Assert.Equal("ES", c.CountryCode);
            Assert.Equal("2026", c.Season);
            Assert.NotEmpty(c.OfflineSource!.Definition.Players!);
            Assert.Empty(c.Players);
        });
        await Assert.ThrowsAsync<ArgumentException>(() => service.Preview(Guid.NewGuid(),
            new OfflineWorldRequest("Espanha 2026", ["es-2026-principal"], 42)));
    }

    [Fact]
    public async Task ConsolidatedSpanishCandidatesCanBePreviewedInMemoryWithExactly128Clubs()
    {
        var report = await new OfflineCatalogLoader(Root()).LoadAsync();
        var catalog = Assert.Single(report.Catalogs, c => c.Manifest?.CatalogId == "es-2026-principal");
        var available = catalog.Clubs.Select(c => OfflineWorldFactory.Eligible(c, catalog.Manifest!.Gender)).ToList();
        var request = new OfflineWorldRequest("Espanha · teste em memória", ["es-2026-principal"], 42,
            ApplyV16RosterPolicy: true);
        Assert.Contains(OfflineWorldFactory.Preview(request, available).Errors, e => e.Contains("exatamente 128"));
        request = request with { ClubKeys = available.Take(128).Select(c => c.Key).ToList() };
        var preview = OfflineWorldFactory.Preview(request, available);
        Assert.Empty(preview.Errors);
        var service = new OfflineWorldCreatorService(new OfflineCatalogLoader(Root()), null!, null!);
        var planning = await service.PreviewForPlanning(request);
        Assert.Equal(Guid.Empty, planning.DraftId);
        Assert.Contains("Prévia de rascunho", Assert.Single(planning.Errors));
        Assert.Equal(preview.DataHash, planning.DataHash);
        Assert.Throws<ArgumentException>(() => OfflineWorldFactory.Create(request, planning));
        Assert.Equal(new[] { 8, 8, 8, 8, 96 }, preview.Clubs.GroupBy(c => c.Division).Select(group => group.Count()));
        foreach (var entry in preview.Clubs)
        {
            Assert.All(entry.Club.Players, player =>
                Assert.Equal(WorldPlayerOrigin.REAL_HISTORICAL_CATALOG, player.PlayerOrigin));
            var (minimum, maximum) = entry.Division switch
            {
                1 => (20, 21), 2 or 3 => (19, 20), 4 => (17, 19), _ => (13, 23)
            };
            int size = entry.Club.Players.Count;
            int keepers = entry.Club.Players.Count(p => p.Position == "G");
            Assert.InRange(size, minimum, maximum);
            Assert.InRange(keepers, entry.Division == 5 ? 1 : 2, entry.Division == 5 && size == 23 ? 3 : 2);
            Assert.True(size - keepers >= 10);
        }
        Assert.Equal(preview.DataHash, OfflineWorldFactory.Preview(request, available).DataHash);
        Assert.Throws<ArgumentException>(() => OfflineWorldFactory.Create(request, preview));
    }
    [Theory]
    [InlineData("penya-deportiva")]
    [InlineData("real-madrid-castilla")]
    public async Task DistrictSelectionUsesOnlyRealCandidatesFromTheSpanishDraft(string clubId)
    {
        var report = await new OfflineCatalogLoader(Root()).LoadAsync();
        var catalog = Assert.Single(report.Catalogs, c => c.Manifest?.CatalogId == "es-2026-principal");
        var source = Assert.Single(catalog.Clubs, c => c.Definition.CatalogClubId == clubId);
        Assert.Equal("DRAFT", source.Definition.Status);
        // Exercise the selector independently of the READY gate, using the exact catalog IDs and positions.
        var club = OfflineWorldFactory.Eligible(source, catalog.Manifest!.Gender);
        var candidates = source.Definition.Players!.Select(p => new CatalogAthlete(p.Id!, clubId, club.Provider,
            club.Gender, club.Season, p.Name!, p.Position!, 1, "Test", DateTime.UnixEpoch)).ToList();
        Assert.Equal(22, candidates.Count);
        Assert.Equal(3, candidates.Count(p => p.Position == "G"));
        var entry = new WorldPreviewClub(5, club with { Players = candidates });
        var sizes = new HashSet<int>();
        for (int seed = 0; seed < 100; seed++)
        {
            var result = OfflineRosterV16.Select(entry, seed).Club;
            sizes.Add(result.Players.Count);
            Assert.Equal(2, result.Players.Count(p => p.Position == "G"));
            Assert.All(result.Players, p => Assert.Contains(p, candidates));
            Assert.Equal(22, result.ReferencePlayers!.Count);
        }
        Assert.Equal(Enumerable.Range(13, 9), sizes.Order());
    }

    [Theory]
    [InlineData("identidades/real-madrid/identidade.json")]
    [InlineData("espanha/2026/clubes/real-madrid.json")]
    [InlineData("espanha/2026/manifest.json")]
    public void DraftFilesAreUtf8WithoutBomAndValidJson(string relative)
    {
        byte[] bytes=File.ReadAllBytes(Path.Combine(Root(),relative));
        Assert.False(bytes.Take(3).SequenceEqual(new byte[]{239,187,191}));
        using var document=JsonDocument.Parse(bytes);Assert.Equal(JsonValueKind.Object,document.RootElement.ValueKind);
    }
}
