using System.Text.Json;
using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Domain;
using Elifoot.Infrastructure;
namespace Elifoot.Application.Tests;

public class MatchInteractionDecisionTests
{
    private static async Task<(GameWorld W, InMemoryWorldRepository R, MultiplayerRoundOrchestrator S, MatchFixture M, HumanCoach A, HumanCoach B)> Room()
    {
        var r=new InMemoryWorldRepository();var s=new MultiplayerRoundOrchestrator(r,NullMatchRoomNotificationPublisher.Instance);
        var w=await s.CreateWorldAsync(new("Interaction QA",TotalClubs:128));w.RoundNumber=1;
        var m=MultiplayerRoundOrchestrator.GetCurrentRoundFixtures(w)[0];
        var a=(await s.JoinWorldAndAssignClubAsync(w.Id,Guid.NewGuid(),"Home","a",m.HomeClubId)).Coach!;
        var b=(await s.JoinWorldAndAssignClubAsync(w.Id,Guid.NewGuid(),"Away","b",m.AwayClubId)).Coach!;
        foreach(var h in new[]{a,b}) {var c=w.Clubs.First(c=>c.Id==h.CurrentClubId);Assert.True((await s.SubmitTacticsAsync(new(w.Id,h.Id,c.PreferredFormation,c.PreferredStarterIds,c.PreferredBenchIds,$"start-{h.Id}",w.RoundVersion))).Success);}
        var players=w.Players.ToDictionary(p=>p.Id);
        w.CurrentMatchMinute=20;foreach(var f in MultiplayerRoundOrchestrator.GetCurrentRoundFixtures(w)){PlayerStatisticsEngine.CaptureLineup(f,players);f.CurrentMinute=20;f.LastSimulatedMinute=20;}
        return(w,r,s,m,a,b);
    }
    private static PendingPenaltyKick Pending(GameWorld w,MatchFixture m,HumanCoach coach,int minute=20,double roll=0)
    {
        w.CurrentMatchMinute=minute;m.CurrentMinute=minute;m.LastSimulatedMinute=minute;
        var ids=m.HomeClubId==coach.CurrentClubId?m.HomeActiveStarterIds:m.AwayActiveStarterIds;
        var p=new PendingPenaltyKick{CoachId=coach.Id,ClubId=coach.CurrentClubId!.Value,Minute=minute,ConversionRoll=roll,GoalkeeperPower=25,EligiblePlayerIds=ids.ToList()};
        m.PendingPenalties.Add(p);return p;
    }
    [Fact] public async Task OpeningObservingNoOpAndInvalidEditsAreFreeOnlyActualEditorsSpendOneWindow()
    {
        var(w,r,s,m,a,b)=await Room();
        var request=new RequestPauseCommand(w.Id,a.Id,"open");var opened=await s.RequestGlobalPauseAsync(request);Assert.True(opened.Success,opened.Error);
        Assert.Equal(0,m.HomeUsedPauseWindows);Assert.Empty(w.PauseWindows.Last().EditedCoachIds);
        Assert.True((await s.RequestGlobalPauseAsync(new(w.Id,b.Id,"observe"))).Success);Assert.Equal(0,m.AwayUsedPauseWindows);
        Assert.True((await s.SubmitInMatchAdjustmentAsync(new(w.Id,a.Id,m.Id,m.HomeFormation,new(),"noop"))).Success);Assert.Equal(0,m.HomeUsedPauseWindows);
        Assert.False((await s.SubmitInMatchAdjustmentAsync(new(w.Id,a.Id,m.Id,"invalid",new(),"bad"))).Success);Assert.Equal(0,m.HomeUsedPauseWindows);
        var changed=m.HomeFormation=="4-3-3"?"4-4-2":"4-3-3";
        var edit=new SubmitInMatchAdjustmentCommand(w.Id,a.Id,m.Id,changed,new(),"edit");Assert.True((await s.SubmitInMatchAdjustmentAsync(edit)).Success);Assert.Equal(1,m.HomeUsedPauseWindows);
        Assert.True((await s.SubmitInMatchAdjustmentAsync(new(w.Id,a.Id,m.Id,"3-5-2",new(),"edit2"))).Success);Assert.Equal(1,m.HomeUsedPauseWindows);
        Assert.Equal(0,m.AwayUsedPauseWindows);Assert.Single(w.PauseWindows.Last().EditedCoachIds);
        Assert.True((await s.ConfirmContinueMatchAsync(new(w.Id,a.Id,"continue-a"))).Success);
        Assert.True((await s.ConfirmContinueMatchAsync(new(w.Id,b.Id,"continue-b"))).Resumed);
        w=JsonSerializer.Deserialize<GameWorld>(JsonSerializer.Serialize(w))!;await r.SaveWorldAsync(w);s=new(r,NullMatchRoomNotificationPublisher.Instance);
        Assert.True((await s.SubmitInMatchAdjustmentAsync(edit)).Success);Assert.True((await s.RequestGlobalPauseAsync(request)).Success);
        Assert.Null(w.ActivePauseWindowId);Assert.Equal(RoundState.RUNNING_FIRST_HALF,w.RoundState);
    }
    [Fact] public async Task FinishedClubCannotChangeItsLineupWhileOtherClubsUseASharedExtraTimeWindow()
    {
        var(w,r,s,m,a,b)=await Room();Assert.True((await s.RequestGlobalPauseAsync(new(w.Id,a.Id,"open"))).Success);
        m.IsFinished=true;w.RoundState=RoundState.EXTRA_TIME_BREAK;w.PauseWindows.Last().PauseType=RoundState.EXTRA_TIME_BREAK;
        var starters=m.HomeActiveStarterIds.ToList();int eventCount=m.Events.Count;
        var result=await s.SubmitInMatchAdjustmentAsync(new(w.Id,a.Id,m.Id,"3-5-2",new(),"finished-match"));
        Assert.False(result.Success);Assert.Equal(starters,m.HomeActiveStarterIds);Assert.Equal(eventCount,m.Events.Count);
        Assert.Equal(0,m.HomeUsedPauseWindows);Assert.Equal(0,m.HomeUsedSubstitutions);Assert.Empty(w.PauseWindows.Last().EditedCoachIds);
        Assert.True((await s.ConfirmContinueMatchAsync(new(w.Id,a.Id,"continue"))).Success);
    }
    [Fact] public async Task EverySubCountsIncludingHalfTimeButHalfTimeDoesNotSpendWindows()
    {
        var(w,r,s,m,a,b)=await Room();await s.RequestGlobalPauseAsync(new(w.Id,a.Id,"pause"));
        var outs=m.HomeActiveStarterIds.Where(id=>w.Players.First(p=>p.Id==id).Position!=PlayerPosition.G).Take(2).ToList();
        var ins=m.HomeBenchIds.Where(id=>w.Players.First(p=>p.Id==id).Position!=PlayerPosition.G).Take(3).ToList();
        var command=new SubmitInMatchAdjustmentCommand(w.Id,a.Id,m.Id,null,new(){new(outs[0],ins[0]),new(outs[1],ins[1])},"two");
        Assert.True((await s.SubmitInMatchAdjustmentAsync(command)).Success);Assert.Equal(2,m.HomeUsedSubstitutions);Assert.Equal(1,m.HomeUsedPauseWindows);
        Assert.True((await s.SubmitInMatchAdjustmentAsync(command)).Success);Assert.Equal(2,m.HomeUsedSubstitutions);
        w.RoundState=RoundState.HALFTIME_PAUSED;w.PauseWindows.Last().PauseType=RoundState.HALFTIME_PAUSED;
        var next=m.HomeActiveStarterIds.First(id=>w.Players.First(p=>p.Id==id).Position!=PlayerPosition.G);
        Assert.True((await s.SubmitInMatchAdjustmentAsync(new(w.Id,a.Id,m.Id,null,new(){new(next,ins[2])},"half"))).Success);
        Assert.Equal(3,m.HomeUsedSubstitutions);Assert.Equal(1,m.HomeUsedPauseWindows);
    }
    [Fact] public async Task PenaltyFreezesEntireRoomPersistsAcrossReloadAndOnlyItsCoachCanChooseAnActivePlayer()
    {
        var(w,r,s,m,a,b)=await Room();var kick=Pending(w,m,a);long version=w.RoundVersion;
        for(int i=0;i<3;i++){var snap=await s.AdvanceClockTickAsync(w.Id);Assert.Equal(20,snap!.CurrentMinute);Assert.Equal(version,snap.RoundVersion);Assert.NotNull(snap.PendingPenalty);}
        Assert.False(await s.ForceExpirePauseWindowIfTimedOutAsync(w.Id,true));
        Assert.False((await s.RequestGlobalPauseAsync(new(w.Id,a.Id,"not-during-penalty"))).Success);
        Assert.False((await s.ChoosePenaltyTakerAsync(new(w.Id,b.Id,kick.Id,kick.EligiblePlayerIds[0],"wrong-coach"))).Success);
        Assert.False((await s.ChoosePenaltyTakerAsync(new(w.Id,a.Id,kick.Id,m.HomeBenchIds[0],"bench"))).Success);
        w=JsonSerializer.Deserialize<GameWorld>(JsonSerializer.Serialize(w))!;await r.SaveWorldAsync(w);s=new(r,NullMatchRoomNotificationPublisher.Instance);
        m=w.Fixtures.First(f=>f.Id==m.Id);kick=m.PendingPenalties.Single();var request=new ChoosePenaltyTakerCommand(w.Id,a.Id,kick.Id,kick.EligiblePlayerIds[0],"kick");
        var result=await s.ChoosePenaltyTakerAsync(request);Assert.True(result.Success,result.Error);Assert.Null(result.Snapshot!.PendingPenalty);Assert.Equal(20,result.Snapshot.CurrentMinute);
        Assert.Single(m.Events,e=>e.EventType is MatchEventType.Goal or MatchEventType.PenaltyMissed);
        Assert.True((await s.ChoosePenaltyTakerAsync(request)).Success);Assert.True((await s.ChoosePenaltyTakerAsync(request with{IdempotencyKey="retry-new-key"})).Success);
        Assert.Single(m.Events,e=>e.EventType is MatchEventType.Goal or MatchEventType.PenaltyMissed);Assert.Equal(0,m.HomeUsedPauseWindows);Assert.Equal(0,m.HomeUsedSubstitutions);
        Assert.Equal(21,(await s.AdvanceClockTickAsync(w.Id))!.CurrentMinute);
    }
    [Fact] public async Task ConcurrentDifferentChoicesProduceOneKickAndDurableReceiptSurvivesRestart()
    {
        var(w,r,s,m,a,b)=await Room();var p=Pending(w,m,a);
        var results=await Task.WhenAll(p.EligiblePlayerIds.Take(2).Select((id,i)=>s.ChoosePenaltyTakerAsync(new(w.Id,a.Id,p.Id,id,$"choice-{i}"))));
        Assert.Single(results,x=>x.Success);Assert.Single(m.Events,e=>e.EventType is MatchEventType.Goal or MatchEventType.PenaltyMissed);
        w=JsonSerializer.Deserialize<GameWorld>(JsonSerializer.Serialize(w))!;await r.SaveWorldAsync(w);s=new(r,NullMatchRoomNotificationPublisher.Instance);
        int winner=Array.FindIndex(results,x=>x.Success);Assert.True((await s.ChoosePenaltyTakerAsync(new(w.Id,a.Id,p.Id,p.EligiblePlayerIds[winner],$"choice-{winner}"))).Success);
        Assert.Single(w.Fixtures.First(f=>f.Id==m.Id).Events,e=>e.EventType is MatchEventType.Goal or MatchEventType.PenaltyMissed);
    }
    [Theory][InlineData(45,RoundState.RUNNING_FIRST_HALF,RoundState.HALFTIME_PAUSED)][InlineData(90,RoundState.RUNNING_SECOND_HALF,RoundState.RESULTS)]
    public async Task HumanKickIsResolvedBeforeIntervalOrMatchClosure(int minute,RoundState state,RoundState expected)
    {
        var(w,r,s,m,a,b)=await Room();w.RoundState=state;
        foreach(var f in MultiplayerRoundOrchestrator.GetCurrentRoundFixtures(w)){f.CurrentMinute=minute;f.LastSimulatedMinute=minute;}
        var p=Pending(w,m,a,minute);Assert.Equal(state,(await s.AdvanceClockTickAsync(w.Id))!.RoundState);
        w=JsonSerializer.Deserialize<GameWorld>(JsonSerializer.Serialize(w))!;await r.SaveWorldAsync(w);s=new(r,NullMatchRoomNotificationPublisher.Instance);m=w.Fixtures.First(f=>f.Id==m.Id);
        var result=await s.ChoosePenaltyTakerAsync(new(w.Id,a.Id,p.Id,p.EligiblePlayerIds[0],"boundary"));Assert.True(result.Success,result.Error);
        Assert.Equal(expected,result.Snapshot!.RoundState);Assert.Equal(minute,result.Snapshot.CurrentMinute);
        if(minute==90){Assert.True(m.IsFinished);Assert.Equal(m.HomeClubId,m.WinnerClubId);Assert.True(m.FinancialsSettled);}
    }
    [Fact] public async Task SameMinutePenaltyQueueWaitsForEveryCoachAndNeverConsumesTacticalWindows()
    {
        var(w,r,s,m,a,b)=await Room();var first=Pending(w,m,a);var second=Pending(w,m,b);
        var result=await s.ChoosePenaltyTakerAsync(new(w.Id,a.Id,first.Id,first.EligiblePlayerIds[0],"first"));Assert.True(result.Success);Assert.Equal(second.Id,result.Snapshot!.PendingPenalty!.DecisionId);
        Assert.Equal(20,(await s.AdvanceClockTickAsync(w.Id))!.CurrentMinute);
        Assert.True((await s.ChoosePenaltyTakerAsync(new(w.Id,b.Id,second.Id,second.EligiblePlayerIds[0],"second"))).Success);
        Assert.Equal(0,m.HomeUsedPauseWindows);Assert.Equal(0,m.AwayUsedPauseWindows);Assert.Empty(m.PendingPenalties);
    }
    [Fact] public async Task ActualEngineAwardsHumanKickWithoutChangingTheAutomaticConversionOrTheOtherAttack()
    {
        var(w,r,s,m,a,b)=await Room();var home=w.Clubs.First(c=>c.Id==m.HomeClubId);var away=w.Clubs.First(c=>c.Id==m.AwayClubId);
        var original=w.Players.Where(p=>p.ClubId==home.Id||p.ClubId==away.Id).ToList();foreach(var p in original)p.Behavior=PlayerBehavior.Sarrafeiro;
        var template=JsonSerializer.Serialize(m);var saved=original.ToDictionary(p=>p.Id,p=>JsonSerializer.Serialize(p));
        Dictionary<Guid,Player> Players()=>saved.ToDictionary(k=>k.Key,k=>JsonSerializer.Deserialize<Player>(k.Value)!);
        bool found=false;
        for(int seed=1;seed<=4000;seed++) {
            var manual=JsonSerializer.Deserialize<MatchFixture>(template)!;manual.CurrentMinute=30;manual.LastSimulatedMinute=29;var players=Players();
            MatchSimulationEngine.SimulateSingleMinute(manual,home,away,players,ManagerTacticalIdentity.Equilibrado,ManagerTacticalIdentity.Equilibrado,seed,homeHumanPenaltyChoice:true);
            if(manual.PendingPenalties.Count==0)continue;
            found=true;var pending=manual.PendingPenalties.Single();Assert.Equal(0,manual.HomeGoals);Assert.Contains(manual.Events,e=>e.EventType==MatchEventType.PenaltyAwarded);
            var chosen=pending.EligiblePlayerIds.Select(id=>players[id]).OrderByDescending(p=>p.IsStar).ThenByDescending(p=>p.Position==PlayerPosition.A).ThenByDescending(p=>p.Strength).First();
            MatchSimulationEngine.ResolvePenaltyKick(manual,home,chosen,players.GetValueOrDefault(pending.GoalkeeperId??Guid.Empty),pending.GoalkeeperPower,true,30,pending.ConversionRoll,new());
            var automatic=JsonSerializer.Deserialize<MatchFixture>(template)!;automatic.CurrentMinute=30;automatic.LastSimulatedMinute=29;
            MatchSimulationEngine.SimulateSingleMinute(automatic,home,away,Players(),ManagerTacticalIdentity.Equilibrado,ManagerTacticalIdentity.Equilibrado,seed);
            Assert.Empty(automatic.PendingPenalties);Assert.Equal(automatic.HomeGoals,manual.HomeGoals);Assert.Equal(automatic.AwayGoals,manual.AwayGoals);
            break;
        }
        Assert.True(found,"A real deterministic seed must award a human penalty.");
    }
    [Fact] public async Task ShootoutAfterExtraTimeRemainsAutomaticAsRequested()
    {
        var(w,r,s,m,a,b)=await Room();w.RoundState=RoundState.RUNNING_EXTRA_TIME;m.EnteredExtraTime=true;m.RequiresKnockoutResolution=true;
        foreach(var f in MultiplayerRoundOrchestrator.GetCurrentRoundFixtures(w)){f.CurrentMinute=120;f.LastSimulatedMinute=120;}
        var penalty=Pending(w,m,a,120,1);
        var result=await s.ChoosePenaltyTakerAsync(new(w.Id,a.Id,penalty.Id,penalty.EligiblePlayerIds[0],"extra-time-penalty"));
        Assert.True(result.Success,result.Error);Assert.Equal(RoundState.RESULTS,result.Snapshot!.RoundState);Assert.Null(result.Snapshot.PendingPenalty);
        Assert.NotNull(m.HomePenaltyGoals);Assert.NotNull(m.AwayPenaltyGoals);Assert.True(m.IsFinished);Assert.NotNull(m.WinnerClubId);
    }
}
