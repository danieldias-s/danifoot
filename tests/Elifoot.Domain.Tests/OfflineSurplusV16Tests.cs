using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Domain;

namespace Elifoot.Domain.Tests;

public sealed class OfflineSurplusV16Tests
{
    private static List<EligibleCatalogClub> Clubs() => Enumerable.Range(0,128).Select(i=>OfflineRosterV16Tests.Club(i)).ToList();
    private static OfflineWorldRequest Request(List<EligibleCatalogClub> clubs,bool surplus=true) => new("Synthetic surplus",["synthetic"],42,
        ClubKeys:clubs.Take(surplus?127:128).Select(c=>c.Key).ToList(),ApplyV16RosterPolicy:true,IncludeSurplusClub:surplus);

    [Fact]
    public void ToggleCreates127PlusOneWith23UniqueActualCutsAndFreezesSave()
    {
        var clubs=Clubs();var request=Request(clubs);
        var preview=OfflineWorldFactory.Preview(request,clubs);
        Assert.Empty(preview.Errors);Assert.Equal(128,preview.Clubs.Count);
        var special=Assert.Single(preview.Clubs,c=>c.Club.IsFictional);
        Assert.Same(special,preview.Clubs[127]);Assert.Equal(5,special.Division);
        Assert.Equal(1,special.Club.OfflineSource!.V11!.TeamStrength);Assert.Null(special.Club.OfflineSource.V11.HistoricalRanking);
        Assert.Equal(23,special.Club.Players.Count);Assert.Equal(3,special.Club.Players.Count(p=>p.Position=="G"));
        var selected=preview.Clubs.SelectMany(c=>c.Club.Players).ToList();
        Assert.Equal(selected.Count,selected.Select(p=>p.PlayerExternalId).Distinct().Count());
        Assert.All(special.Club.Players,p=>Assert.Contains(clubs,c=>c.Players.Any(original=>original.CanonicalPlayerId==p.CanonicalPlayerId)));
        Assert.Equal(preview.DataHash,OfflineWorldFactory.Preview(request,clubs.AsEnumerable().Reverse().ToList()).DataHash);
        var world=OfflineWorldFactory.Create(request,preview);
        var restored=JsonSerializer.Deserialize<GameWorld>(JsonSerializer.Serialize(world))!;
        var specialClub=restored.Clubs.Single(c=>c.OfflineIdentity!.CatalogClubId==OfflineSurplusV16.ClubId);
        var athletes=restored.Players.Where(p=>p.ClubId==specialClub.Id).ToList();Assert.Equal(23,athletes.Count);
        Assert.All(athletes,p=>Assert.Equal("synthetic",p.OfflineIdentity!.CatalogId));
        Assert.Equal(preview.DataHash,restored.OfflineCatalogOrigin!.DataHash);
        var off=OfflineWorldFactory.Preview(Request(clubs,false),clubs);
        Assert.Empty(off.Errors);Assert.DoesNotContain(off.Clubs,c=>c.Club.IsFictional);
        foreach(var real in preview.Clubs.Where(c=>!c.Club.IsFictional))
            Assert.Equal(real.Club.Players,off.Clubs.Single(c=>c.Club.Key==real.Club.Key).Club.Players);
    }
    [Theory]
    [InlineData("G")] [InlineData("D")]
    public void InsufficientActualCutsBlockWithoutInventingPlayers(string keepPosition)
    {
        var entries=Clubs().Take(127).Select(c=>new WorldPreviewClub(5,c with {
            ReferencePlayers=c.Players,Players=c.Players.Where(p=>p.Position!=keepPosition).ToList()
        })).ToList();
        var error=Assert.Throws<ArgumentException>(()=>OfflineSurplusV16.Build(entries,42));
        Assert.Contains("insuficientes",error.Message);
    }
    [Fact]
    public void SurplusRequiresExplicitRosterOption()
    {
        var clubs=Clubs();Assert.Throws<ArgumentException>(()=>OfflineWorldFactory.Preview(Request(clubs) with {ApplyV16RosterPolicy=false},clubs));
    }
}
