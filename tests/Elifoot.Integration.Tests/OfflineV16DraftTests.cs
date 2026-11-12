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
    public async Task RealMadridDraftIsStructurallyValidButCannotCreateWorld()
    {
        var report=await new OfflineCatalogLoader(Root()).LoadAsync();
        Assert.True(report.Valid);
        var catalog=Assert.Single(report.Catalogs,c=>c.Manifest?.CatalogId=="es-2026-principal");
        Assert.False(catalog.Ready);Assert.Equal("DRAFT",catalog.Manifest!.Status);
        Assert.Single(catalog.Manifest.Clubs!,c=>c=="clubes/real-madrid.json");
        var club=Assert.Single(catalog.Clubs,c=>c.Definition.CatalogClubId=="real-madrid");
        Assert.Equal("DRAFT",club.Definition.Status);Assert.Null(club.V11!.TeamStrength);
        Assert.Null(club.V11.HistoricalRanking);Assert.Null(club.V11.TieBreakPriority);Assert.Null(club.Definition.Coach);
        Assert.Equal(23,club.Definition.Players!.Count);
        Assert.All(club.Definition.Players,p=>{Assert.Null(p.Strength);Assert.Null(p.Behavior);});
        Assert.Contains(catalog.Issues,i=>i.Code=="TEAM_STRENGTH"&&!i.IsError);
        Assert.Contains(catalog.Issues,i=>i.Code=="BEHAVIOR"&&!i.IsError);
        var service=new OfflineWorldCreatorService(new OfflineCatalogLoader(Root()),null!,null!);
        await Assert.ThrowsAsync<ArgumentException>(()=>service.Select(["es-2026-principal"]));
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
