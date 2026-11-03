using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Domain;
using Elifoot.Infrastructure;

namespace Elifoot.Application.Tests;

public class CoachCareer008ReadinessTests
{
    [Fact]
    public async Task ReadyCoachWhoseStarterWasSoldMustResubmitBeforeTheRoomCanStart()
    {
        var repo=new InMemoryWorldRepository();var o=new MultiplayerRoundOrchestrator(repo,NullMatchRoomNotificationPublisher.Instance);
        var w=await o.CreateWorldAsync(new("Escalação preservada",MaxHumans:2));w.RoundNumber=1;
        var a=(await o.JoinWorldAndAssignClubAsync(w.Id,Guid.NewGuid(),"A","a",w.Clubs[0].Id)).Coach!;
        var b=(await o.JoinWorldAndAssignClubAsync(w.Id,Guid.NewGuid(),"B","b",w.Clubs[1].Id)).Coach!;
        var ca=w.Clubs[0];var cb=w.Clubs[1];
        var first=await o.SubmitTacticsAsync(new(w.Id,a.Id,ca.PreferredFormation,ca.PreferredStarterIds.ToList(),ca.PreferredBenchIds.ToList(),"008-first-ready",w.RoundVersion));
        Assert.True(first.Success);Assert.False(first.RoundStarted);
        var player=w.Players.First(p=>a.SubmittedStarterIds.Contains(p.Id)&&p.Position==PlayerPosition.A);
        Assert.True(MarketAndEconomyEngine.ExecuteTransfer(w,cb.Id,ca.Id,player.Id,100000,"Compra entre preparações","008-ready-sale").Success);
        var (s,bench)=AiManagerBrain.BuildStartersAndBench(w.Players.Where(p=>p.ClubId==cb.Id).ToList(),TacticalFormations.All["4-4-2"]);
        var second=await o.SubmitTacticsAsync(new(w.Id,b.Id,"4-4-2",s,bench,"008-second-ready",w.RoundVersion));
        Assert.True(second.Success);Assert.False(second.RoundStarted);
        Assert.Equal(RoundState.PREPARING,w.RoundState);Assert.False(a.IsReadyForRound);Assert.Equal(1,second.Counter!.ReadyCount);
    }
}
