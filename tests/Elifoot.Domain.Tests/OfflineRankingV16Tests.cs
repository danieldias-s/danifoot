using Elifoot.Contracts;
using Elifoot.Domain;

namespace Elifoot.Domain.Tests;

public sealed class OfflineRankingV16Tests
{
    private static EligibleCatalogClub Club(string key, string source, double rating, DateOnly? date = null)
    {
        var definition = new OfflineClub("1.1",key,"BR",2026,key,key,"SYN",["#123456","#FFFFFF"],null,null,[],"READY");
        var origin = new OfflineClubSource("synthetic","BR","Brasil",2026,"hash",definition,V11:new(
            new("1.1",key,key,key,"SYN",["#123456","#FFFFFF"],null),20,new(rating,source,date??new(2026,7,1)),null));
        return OfflineWorldFactory.Eligible(origin,FootballGender.Male);
    }
    [Fact]
    public void OptaAndInterpolatedUseSameScaleAndKeepOriginalProvenance()
    {
        var a=Club("a","OPTA",90);var b=Club("b","OPTA_INTERPOLATED",95);
        Assert.Equal([b.Key,a.Key],OfflineCatalogRanking.Order([a,b]).Select(c=>c.Key));
        Assert.Empty(OfflineCatalogRanking.Warnings([a,b]));
        Assert.Equal("OPTA_INTERPOLATED",b.OfflineSource!.V11!.HistoricalRanking!.Source);
    }
    [Theory]
    [InlineData("Club Elo",false)] [InlineData("IFFHS",false)] [InlineData("OPTA",true)]
    public void OtherScalesOrDifferentDatesAreNeverCompared(string source,bool differentDate)
    {
        var a=Club("a","OPTA",90);var b=Club("b",source,100000,differentDate?new DateOnly(2026,7,2):null);
        Assert.NotEmpty(OfflineCatalogRanking.Warnings([a,b]));
        Assert.Equal([a.Key,b.Key],OfflineCatalogRanking.Order([b,a]).Select(c=>c.Key));
    }
    [Fact]
    public void EqualRatingUsesManualPriorityThenStablePresentationAndBlocksBoundary()
    {
        var clubs=Enumerable.Range(0,9).Select(i=>Club($"c-{i}",i%2==0?"OPTA":"OPTA_INTERPOLATED",90)).ToList();
        var order=OfflineCatalogRanking.Order(clubs.AsEnumerable().Reverse());
        Assert.Equal(clubs.Select(c=>c.Key),order.Select(c=>c.Key));
        var assigned=order.Select((c,i)=>new WorldPreviewClub(i<8?1:2,c)).ToList();
        Assert.Single(OfflineCatalogRanking.BoundaryErrors(order,assigned,[]));
        order=OfflineCatalogRanking.Order(clubs.Select((c,i)=>c with {OfflineSource=c.OfflineSource! with {V11=c.OfflineSource!.V11! with {TieBreakPriority=i}}}));
        Assert.Empty(OfflineCatalogRanking.BoundaryErrors(order,assigned,[]));
    }
}
