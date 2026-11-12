using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Domain;

namespace Elifoot.Domain.Tests;

public sealed class OfflineTacticsV16Tests
{
    private static OfflineClub Club(List<string> home, List<string> away, string version = "1.1") => new(
        version, "synthetic", "BR", 2026, "Clube sintético", "Sintético", "SYN", ["#123456", "#FFFFFF"], new("Sintético", 10000),
        new("coach", "Técnico sintético", "BR", new(home, away)),
        Enumerable.Range(0,11).Select(i => new OfflinePlayer($"p-{i}", $"Sintético {i}", i==0?"G":i<5?"D":i<9?"M":"A",
            version=="1.0"?25:null,"BR",2,false)).ToList(), "READY");

    [Theory]
    [InlineData(1,0)] [InlineData(0,1)] [InlineData(2,0)] [InlineData(0,2)] [InlineData(1,2)] [InlineData(2,2)]
    public void ReadyAcceptsOneToFourVerifiedTactics(int home, int away)
    {
        var c=Club(new[]{"4-3-3","4-4-2"}.Take(home).ToList(),new[]{"5-3-2","4-5-1"}.Take(away).ToList());
        Assert.Empty(OfflineCatalogRules.ValidateClub(c,"test"));
        Assert.Equal(home,c.Coach!.PreferredTactics!.Home!.Count); Assert.Equal(away,c.Coach.PreferredTactics.Away!.Count);
    }
    [Theory]
    [InlineData("empty")] [InlineData("too-many-home")] [InlineData("too-many-away")]
    [InlineData("duplicate-home")] [InlineData("duplicate-away")] [InlineData("invalid")]
    public void InvalidOrAbsentTacticsCannotBecomeReady(string kind)
    {
        List<string> home=["4-4-2"],away=[];
        if(kind=="empty")home=[];
        if(kind=="too-many-home")home=["4-4-2","4-3-3","5-3-2"];
        if(kind=="too-many-away")away=["4-4-2","4-3-3","5-3-2"];
        if(kind=="duplicate-home")home=["4-4-2","4-4-2"];
        if(kind=="duplicate-away")away=["4-4-2","4-4-2"];
        if(kind=="invalid")home=["4-4-3"];
        Assert.Contains(OfflineCatalogRules.ValidateClub(Club(home,away),"test"),i=>i.IsError && i.Code.StartsWith("TACTICS"));
    }
    [Fact]
    public void Legacy10ContractStillRequiresTwoPerContext()
    {
        Assert.Contains(OfflineCatalogRules.ValidateClub(Club(["4-4-2"],[],"1.0"),"test"),i=>i.Code=="TACTICS");
        Assert.Empty(OfflineCatalogRules.ValidateClub(Club(["4-4-2","4-3-3"],["5-3-2","4-5-1"],"1.0"),"test"));
    }
    [Theory]
    [InlineData(true)] [InlineData(false)]
    public void RealPlanningUsesOtherVerifiedContextWhenCurrentContextIsEmpty(bool home)
    {
        var w=WorldSeedFactory.CreateSeededWorld();var c=w.Clubs[0];var manager=w.AiManagers.Single(m=>m.Id==c.AiManagerId);
        manager.OfflineIdentity=new("synthetic","coach","BR",2026,"BR",home?[]:["4-3-3"],home?["4-3-3"]:[]);
        var before=JsonSerializer.Serialize(manager.OfflineIdentity);
        var result=AiManagerBrain.PlanAndSelectLineup(manager,c,w.Players.Where(p=>p.ClubId==c.Id).ToList(),null,null,home,23,1,1);
        Assert.Equal(3,result.Plan.TargetStarters[PlayerPosition.M]);Assert.Equal(3,result.Plan.TargetStarters[PlayerPosition.A]);
        Assert.NotEmpty(result.Starters);Assert.True(TacticalFormations.TryGet(result.Formation,true,out _));
        Assert.Equal(before,JsonSerializer.Serialize(manager.OfflineIdentity));
    }
    [Fact]
    public void OldIncompleteProfileHasUsableRuntimeFallbackWithoutRewritingSavePreferences()
    {
        var w=WorldSeedFactory.CreateSeededWorld();var c=w.Clubs[0];var manager=w.AiManagers.Single(m=>m.Id==c.AiManagerId);
        manager.OfflineIdentity=new("synthetic","coach","BR",2026,"BR",[],[]);
        var result=AiManagerBrain.PlanAndSelectLineup(manager,c,w.Players.Where(p=>p.ClubId==c.Id).ToList(),null,null,true,23,1,1);
        Assert.Equal(4,result.Plan.TargetStarters[PlayerPosition.M]);Assert.NotEmpty(result.Starters);
        Assert.Empty(manager.OfflineIdentity.OriginalHome);Assert.Empty(manager.OfflineIdentity.OriginalAway);
    }
}
