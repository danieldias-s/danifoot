using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Domain;

namespace Elifoot.Domain.Tests;

public class Discipline012CTests
{
    [Fact]
    public void ThreeIsolatedYellowsShareOneCycleAcrossLeagueCupAndTransfer()
    {
        var (w,p)=World();
        Card(w,Fixture(w,3),p,MatchEventType.YellowCard);
        Assert.Equal(1,p.YellowCardsInCurrentCycle);Assert.Equal(0,p.SuspensionMatchesRemaining);
        Card(w,Fixture(w,5,CompetitionType.CupRoundOf64),p,MatchEventType.YellowCard);
        Assert.Equal(2,p.YellowCardsInCurrentCycle);
        var old=p.ClubId;var buyer=w.Clubs.First(c=>c.DivisionTier==DivisionTier.SegundaDivisao);
        Assert.True(MarketAndEconomyEngine.ExecuteTransfer(w,buyer.Id,old!.Value,p.Id,100_000,"discipline","disc-transfer").Success);
        Assert.Equal(2,p.YellowCardsInCurrentCycle);
        var third=Fixture(w,6,home:buyer.Id);Card(w,third,p,MatchEventType.YellowCard);
        Assert.Equal(0,p.YellowCardsInCurrentCycle);Assert.Equal(3,p.TotalYellowCards);
        Assert.Equal(1,p.SuspensionMatchesRemaining);Assert.Equal("THREE_YELLOWS",p.SuspensionReason);
        third.IsFinished=true;DisciplineEngine.SettleFinishedMatches(w,[third]);
        Assert.Equal(1,p.SuspensionMatchesRemaining); // The infraction match is never served.
        var next=Fixture(w,7,home:buyer.Id);Finish(w,next);
        Assert.Equal(0,p.SuspensionMatchesRemaining);Assert.Null(p.SuspensionReason);
        Assert.Equal(new[]{old,old,buyer.Id},p.CardHistory.Select(e=>(Guid?)e.ClubId));
    }

    [Theory]
    [InlineData(0)] [InlineData(1)] [InlineData(2)]
    public void TwoYellowsRestorePriorCycleAndAwardExactlyOneMatch(int prior)
    {
        var (w,p)=World();p.YellowCardsInCurrentCycle=prior;
        var m=Fixture(w,3);Card(w,m,p,MatchEventType.YellowCard,10);
        Card(w,m,p,MatchEventType.SecondYellowRedCard,30);
        Assert.Equal(prior,p.YellowCardsInCurrentCycle);Assert.Equal(2,p.TotalYellowCards);
        Assert.Equal(1,p.TotalSecondYellowDismissals);Assert.Equal(0,p.TotalDirectRedCards);
        Assert.Equal(1,p.SuspensionMatchesRemaining);Assert.Equal("SECOND_YELLOW",p.SuspensionReason);
        Assert.Single(p.SuspensionHistory);Assert.Equal(1,p.SuspensionHistory[0].Matches);
        Assert.Equal(2,p.CardHistory.Count);
        m.CurrentMinute=90;m.IsFinished=true;DisciplineEngine.SettleFinishedMatches(w,[m]);
        Assert.Equal(1,p.SuspensionMatchesRemaining);Assert.Equal(prior,p.YellowCardsInCurrentCycle);
        PlayerStatisticsEngine.ApplyFinishedMatches(w,[m]);
        var row=Assert.Single(w.PlayerStatistics,s=>s.PlayerId==p.Id);
        Assert.Equal(2,row.YellowCards);Assert.Equal(1,row.RedCards);
    }

    [Theory]
    [InlineData(0,1)] [InlineData(49,1)] [InlineData(50,2)] [InlineData(79,2)]
    [InlineData(80,3)] [InlineData(94,3)] [InlineData(95,4)] [InlineData(99,4)]
    public void DirectRedCumulativeBoundariesAndMaximumOfPenalties(int roll,int matches)
    {
        Assert.Equal(matches,DisciplineEngine.DirectRedDuration(roll));
        var (w,p)=World();p.YellowCardsInCurrentCycle=2;var m=Fixture(w,3);
        Card(w,m,p,MatchEventType.YellowCard,10);
        Card(w,m,p,MatchEventType.DirectRedCard,20,matches);
        Assert.Equal(matches,p.SuspensionMatchesRemaining);Assert.Equal(0,p.YellowCardsInCurrentCycle);
        Assert.Single(p.SuspensionHistory);Assert.Equal(matches,p.SuspensionHistory[0].Matches);
        Assert.Equal(1,p.TotalDirectRedCards);Assert.Equal(1,p.TotalYellowCards);
        Assert.Throws<ArgumentOutOfRangeException>(()=>DisciplineEngine.DirectRedDuration(-1));
        Assert.Throws<ArgumentOutOfRangeException>(()=>DisciplineEngine.DirectRedDuration(100));
    }

    [Theory]
    [InlineData(CompetitionType.League,CompetitionType.CupRoundOf64)]
    [InlineData(CompetitionType.CupRoundOf64,CompetitionType.League)]
    public void SuspensionIsServedAtCompletionAcrossCompetitionsAndSurvivesReload(CompetitionType first,CompetitionType second)
    {
        var (w,p)=World();var infraction=Fixture(w,3,first);Card(w,infraction,p,MatchEventType.DirectRedCard,60,2);
        Finish(w,infraction);Assert.Equal(2,p.SuspensionMatchesRemaining);
        var next=Fixture(w,4,second);
        Assert.DoesNotContain(p.Id,next.HomeActiveStarterIds);Assert.Equal(2,p.SuspensionMatchesRemaining);
        Finish(w,next);Assert.Equal(1,p.SuspensionMatchesRemaining);
        string save=JsonSerializer.Serialize(w);w=JsonSerializer.Deserialize<GameWorld>(save)!;p=w.Players.Single(x=>x.Id==p.Id);
        DisciplineEngine.SettleFinishedMatches(w,[next]);Assert.Equal(save,JsonSerializer.Serialize(w));
        Finish(w,Fixture(w,5,first));Assert.Equal(0,p.SuspensionMatchesRemaining);Assert.True(p.IsAvailableForMatch);
        Assert.Equal(2,p.SuspensionHistory.Count(h=>h.Action=="SERVE"));
    }

    [Fact]
    public void TransferAndSeasonChangeKeepCyclesBalancesAndCompleteHistories()
    {
        var (w,p)=World();p.YellowCardsInCurrentCycle=2;var m=Fixture(w,3);
        Card(w,m,p,MatchEventType.DirectRedCard,30,4);
        var buyer=w.Clubs.First(c=>c.DivisionTier==DivisionTier.QuartaDivisao);
        Assert.True(MarketAndEconomyEngine.ExecuteTransfer(w,buyer.Id,p.ClubId!.Value,p.Id,100_000,"discipline","ban-transfer").Success);
        Assert.Equal(4,p.SuspensionMatchesRemaining);Assert.Equal(2,p.YellowCardsInCurrentCycle);
        Finish(w,Fixture(w,4,home:buyer.Id));Assert.Equal(3,p.SuspensionMatchesRemaining);
        m.IsFinished=true;w.RoundNumber=w.TotalRoundsInSeason;
        CompetitionAndSeasonEngine.FinalizeSeasonAndAdvance(w);
        Assert.Equal(3,p.SuspensionMatchesRemaining);Assert.Equal(2,p.YellowCardsInCurrentCycle);
        Assert.Single(p.CardHistory);Assert.Equal(2,p.SuspensionHistory.Count);
        Finish(w,Fixture(w,3,home:buyer.Id));Assert.Equal(2,p.SuspensionMatchesRemaining);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void ByesPostponementCancellationAndRepeatedSettlementDoNotServe(bool district)
    {
        var (w,p)=World();
        if(district)p=w.Players.First(x=>x.ClubId==w.Clubs.First(c=>c.DivisionTier==DivisionTier.Distrital).Id);
        p.SuspensionMatchesRemaining=2;p.SuspensionReason="DIRECT_RED";
        var bye=Fixture(w,3,home:w.Clubs.First(c=>c.Id!=p.ClubId&&c.DivisionTier==DivisionTier.SegundaDivisao).Id);
        Finish(w,bye);Assert.Equal(2,p.SuspensionMatchesRemaining);
        var postponed=Fixture(w,4,home:p.ClubId);postponed.IsFinished=true;postponed.CurrentMinute=90;postponed.IsPostponed=true;
        DisciplineEngine.SettleFinishedMatches(w,[postponed]);Assert.Equal(2,p.SuspensionMatchesRemaining);
        postponed.IsPostponed=false;postponed.IsCancelled=true;
        DisciplineEngine.SettleFinishedMatches(w,[postponed]);Assert.Equal(2,p.SuspensionMatchesRemaining);
        postponed.IsCancelled=false;postponed.IsFinished=false;
        DisciplineEngine.SettleFinishedMatches(w,[postponed]);Assert.Equal(2,p.SuspensionMatchesRemaining);
        postponed.IsFinished=true;DisciplineEngine.SettleFinishedMatches(w,[postponed]);Assert.Equal(1,p.SuspensionMatchesRemaining);
        DisciplineEngine.SettleFinishedMatches(w,[postponed]);Assert.Equal(1,p.SuspensionMatchesRemaining);
    }

    [Fact]
    public void LegacyMigrationPreservesKnownTotalsWithoutInventingMissingEventsOrTypes()
    {
        var (w,p)=World();w.DisciplineRulesVersion=0;p.DisciplineVersion=0;
        p.SeasonYellows=7;p.SeasonReds=3;p.YellowCardsAccumulated=2;p.SuspensionRoundsRemaining=2;
        var m=Fixture(w,3);m.Events.Add(Event(m,p,MatchEventType.YellowCard,10));m.Events.Add(Event(m,p,MatchEventType.DirectRedCard,20));
        m.IsFinished=true;m.CurrentMinute=90;m.FinancialsSettled=true;w.Fixtures=[m];
        DisciplineEngine.EnsureMigrated(w);
        Assert.Equal(7,p.TotalYellowCards);Assert.Equal(1,p.TotalDirectRedCards);Assert.Equal(2,p.UnclassifiedHistoricalRedCards);
        Assert.Equal(2,p.CardHistory.Count);Assert.All(p.CardHistory,e=>Assert.True(e.RecoveredFromLegacy));
        Assert.Equal(2,p.YellowCardsInCurrentCycle);Assert.Equal(2,p.SuspensionMatchesRemaining);Assert.NotEmpty(w.DisciplineCoverageWarnings);
        string save=JsonSerializer.Serialize(w);DisciplineEngine.EnsureMigrated(w);DisciplineEngine.SettleFinishedMatches(w,[m]);
        Assert.Equal(save,JsonSerializer.Serialize(w));
        // Statistics alone do not prove that the old financial suspension countdown ran.
        var (pending,waiting)=World();pending.DisciplineRulesVersion=0;waiting.DisciplineVersion=0;waiting.SuspensionMatchesRemaining=2;
        var unfinishedSettlement=Fixture(pending,3);unfinishedSettlement.IsFinished=true;unfinishedSettlement.CurrentMinute=90;
        pending.Fixtures=[unfinishedSettlement];pending.AppliedPlayerStatisticsMatches.Add(unfinishedSettlement.Id);
        DisciplineEngine.EnsureMigrated(pending);DisciplineEngine.SettleFinishedMatches(pending,[unfinishedSettlement]);
        Assert.Equal(1,waiting.SuspensionMatchesRemaining);
    }

    [Fact]
    public void OldLiveThirdYellowMarkerRestoresTheTwoPriorYellowsIfSecondYellowFollows()
    {
        var (w,p)=World();var m=Fixture(w,3);w.Fixtures=[m];w.DisciplineRulesVersion=0;
        foreach(var player in w.Players)player.DisciplineVersion=0;
        p.YellowCardsAccumulated=0;p.SeasonYellows=1;p.SuspensionRoundsRemaining=1;
        m.CurrentMinute=10;m.HomeYellowCardsInMatch[p.Id]=1;m.HomeSuspendedByYellowInMatchIds.Add(p.Id);
        m.Events.Add(Event(m,p,MatchEventType.YellowCard,10));
        DisciplineEngine.EnsureMigrated(w);Assert.Equal(3,p.TotalYellowCards);
        Card(w,m,p,MatchEventType.SecondYellowRedCard,30);
        Assert.Equal(2,p.YellowCardsInCurrentCycle);Assert.Equal(1,p.SuspensionMatchesRemaining);Assert.Equal(4,p.TotalYellowCards);
        Assert.Equal(2,p.CardHistory.Count);Assert.Single(p.SuspensionHistory);
    }

    [Theory]
    [InlineData(11,11)] [InlineData(10,11)] [InlineData(9,11)] [InlineData(10,10)]
    public void ActualPlayerCompositionDeterminesManDownSectorStrength(int homeCount,int awayCount)
    {
        var (w,_)=World();var m=Fixture(w,3);var players=w.Players.ToDictionary(p=>p.Id);
        foreach(var p in w.Players){p.Strength=30;p.IsStar=false;}
        var full=MatchSimulationEngine.ComputeSectorBreakdown("4-4-2",m.HomeActiveStarterIds.Select(id=>players[id]).ToList(),false,ManagerTacticalIdentity.Equilibrado);
        m.HomeActiveStarterIds.RemoveRange(homeCount,11-homeCount);m.AwayActiveStarterIds.RemoveRange(awayCount,11-awayCount);
        var home=MatchSimulationEngine.ComputeSectorBreakdown("4-4-2",m.HomeActiveStarterIds.Select(id=>players[id]).ToList(),false,ManagerTacticalIdentity.Equilibrado);
        var away=MatchSimulationEngine.ComputeSectorBreakdown("4-4-2",m.AwayActiveStarterIds.Select(id=>players[id]).ToList(),false,ManagerTacticalIdentity.Equilibrado);
        Assert.Equal(homeCount,home.ActivePlayersCount);Assert.Equal(awayCount,away.ActivePlayersCount);
        if(homeCount<11){Assert.True(home.EffectiveAttackTotal<full.EffectiveAttackTotal);Assert.True(home.EffectiveDefenseTotal<=full.EffectiveDefenseTotal);}
        Assert.Equal((home.DefenseSectorPower+.45*home.MidfieldSectorPower)*Math.Pow(.93,11-homeCount),home.EffectiveDefenseTotal,6);
    }

    [Theory]
    [InlineData(PlayerPosition.G)] [InlineData(PlayerPosition.D)] [InlineData(PlayerPosition.M)] [InlineData(PlayerPosition.A)]
    public void RealRedRemovesEveryRoleAndPreventsSubstitutionReturnAndFutureParticipation(PlayerPosition role)
    {
        var (w,_)=World();var template=Fixture(w,3);var target=w.Players.First(p=>template.HomeActiveStarterIds.Contains(p.Id)&&p.Position==role);
        foreach(var p in w.Players)p.Strength=30;
        target.Strength=50; // Keep this controlled offender in the starting lineup despite behavior's selection penalty.
        target.Behavior=PlayerBehavior.Sarrafeiro;
        string snapshot=JsonSerializer.Serialize(w);bool found=false;
        for(int seed=1;seed<=1000&&!found;seed++)
        {
            var trial=JsonSerializer.Deserialize<GameWorld>(snapshot)!;var m=Fixture(trial,3);var p=trial.Players.Single(p=>p.Id==target.Id);
            m.HomeYellowCardsInMatch[p.Id]=1;
            var map=trial.Players.ToDictionary(p=>p.Id);var home=trial.Clubs.Single(c=>c.Id==m.HomeClubId);var away=trial.Clubs.Single(c=>c.Id==m.AwayClubId);
            var before=MatchSimulationEngine.ComputeSectorBreakdown("4-4-2",m.HomeActiveStarterIds.Select(id=>map[id]).ToList(),false,ManagerTacticalIdentity.Equilibrado);
            for(int minute=1;minute<=90&&!found;minute++)
            {
                m.CurrentMinute=minute;MatchSimulationEngine.SimulateSingleMinute(m,home,away,map,ManagerTacticalIdentity.Equilibrado,ManagerTacticalIdentity.Equilibrado,seed);
                if(!m.HomeSentOffIds.Contains(p.Id))continue;
                found=true;Assert.DoesNotContain(p.Id,m.HomeActiveStarterIds);
                int count=m.HomeActiveStarterIds.Count;
                var after=MatchSimulationEngine.ComputeSectorBreakdown("4-4-2",m.HomeActiveStarterIds.Select(id=>map[id]).ToList(),false,ManagerTacticalIdentity.Equilibrado);
                Assert.True(after.EffectiveDefenseTotal<before.EffectiveDefenseTotal);
                Assert.True(after.EffectiveAttackTotal<before.EffectiveAttackTotal);
                Assert.True(role switch {PlayerPosition.G=>after.GoalkeeperPower<before.GoalkeeperPower,
                    PlayerPosition.D=>after.DefenseSectorPower<before.DefenseSectorPower,
                    PlayerPosition.M=>after.MidfieldSectorPower<before.MidfieldSectorPower,_=>after.AttackSectorPower<before.AttackSectorPower});
                var substitute=m.HomeBenchIds.First(id=>map[id].IsAvailableForMatch);
                Assert.False(MatchSimulationEngine.ApplyInMatchAdjustments(m,home,true,RoundState.HALFTIME_PAUSED,null,null,[new(p.Id,substitute)],map).Success);
                if(role==PlayerPosition.G)
                {
                    var backup=m.HomeBenchIds.First(id=>map[id].IsAvailableForMatch&&map[id].Position==PlayerPosition.G);
                    var outfield=m.HomeActiveStarterIds.First(id=>map[id].Position!=PlayerPosition.G);
                    Assert.True(MatchSimulationEngine.ApplyInMatchAdjustments(m,home,true,RoundState.HALFTIME_PAUSED,null,null,[new(outfield,backup)],map).Success);
                    Assert.Equal(count,m.HomeActiveStarterIds.Count); // A legal exchange restores the position, never the expelled slot.
                }
                for(int next=minute+1;next<=90;next++)
                {
                    m.CurrentMinute=next;
                    Assert.DoesNotContain(MatchSimulationEngine.SimulateSingleMinute(m,home,away,map,ManagerTacticalIdentity.Equilibrado,ManagerTacticalIdentity.Equilibrado,seed),e=>e.PrimaryPlayerId==p.Id||e.SecondaryPlayerId==p.Id);
                }
                Assert.Equal(count,m.HomeActiveStarterIds.Count);
            }
        }
        Assert.True(found);
    }

    [Theory]
    [InlineData(PlayerPosition.G,false)] [InlineData(PlayerPosition.D,false)]
    [InlineData(PlayerPosition.M,true)] [InlineData(PlayerPosition.A,true)] [InlineData(PlayerPosition.G,true)]
    public void RealInjuryLeavesFieldImmediatelyAndOnlyLegalSubstitutionCanRestoreItsSlot(PlayerPosition role,bool legalSub)
    {
        var (w,_)=World();var m=Fixture(w,3);m.CurrentMinute=75;
        var map=w.Players.ToDictionary(p=>p.Id);var home=w.Clubs.Single(c=>c.Id==m.HomeClubId);var away=w.Clubs.Single(c=>c.Id==m.AwayClubId);
        var p=map[m.HomeActiveStarterIds.First(id=>map[id].Position==role)];
        var before=MatchSimulationEngine.ComputeSectorBreakdown("4-4-2",m.HomeActiveStarterIds.Select(id=>map[id]).ToList(),false,ManagerTacticalIdentity.Equilibrado);
        if(!legalSub){m.HomeUsedSubstitutions=5;m.HomeUsedPauseWindows=3;}
        int seed=InjurySeed(m,m.HomeActiveStarterIds.IndexOf(p.Id),11);
        MatchSimulationEngine.SimulateSingleMinute(m,home,away,map,ManagerTacticalIdentity.Equilibrado,ManagerTacticalIdentity.Equilibrado,seed);
        Assert.Contains(p.Id,m.HomeInjuredInMatchIds);Assert.DoesNotContain(p.Id,m.HomeActiveStarterIds);
        Assert.Contains(p.Id,m.HomeInjuryVacancies);Assert.InRange(p.InjuryDaysRemaining,2,15);Assert.Equal(50,p.Strength);
        var after=MatchSimulationEngine.ComputeSectorBreakdown("4-4-2",m.HomeActiveStarterIds.Select(id=>map[id]).ToList(),false,ManagerTacticalIdentity.Equilibrado);
        Assert.Equal(10,after.ActivePlayersCount);Assert.True(after.EffectiveAttackTotal<before.EffectiveAttackTotal);
        if(role==PlayerPosition.G)Assert.True(after.GoalkeeperPower<before.GoalkeeperPower);
        var incoming=m.HomeBenchIds.First(id=>map[id].Position==(role==PlayerPosition.G?PlayerPosition.G:map[id].Position)&&map[id].IsAvailableForMatch&& (role==PlayerPosition.G||map[id].Position!=PlayerPosition.G));
        var result=MatchSimulationEngine.ApplyInMatchAdjustments(m,home,true,RoundState.GLOBAL_PAUSED,Guid.NewGuid(),null,[new(p.Id,incoming)],map);
        Assert.Equal(legalSub,result.Success);Assert.Equal(legalSub?11:10,m.HomeActiveStarterIds.Count);
        if(legalSub){Assert.DoesNotContain(p.Id,m.HomeInjuryVacancies);Assert.Contains(incoming,m.HomeActiveStarterIds);}
        else
        {
            m.CurrentMinute=76;var second=map[m.HomeActiveStarterIds.First(id=>map[id].Position!=PlayerPosition.G)];
            seed=InjurySeed(m,m.HomeActiveStarterIds.IndexOf(second.Id),10);
            MatchSimulationEngine.SimulateSingleMinute(m,home,away,map,ManagerTacticalIdentity.Equilibrado,ManagerTacticalIdentity.Equilibrado,seed);
            Assert.Equal(9,m.HomeActiveStarterIds.Count);
        }
    }

    [Fact]
    public void SuspendedPlayersNeverStartEvenWhenSquadMustPlayShortHanded()
    {
        var (w,p)=World();var home=w.Clubs.Single(c=>c.Id==p.ClubId);
        var squad=w.Players.Where(p=>p.ClubId==home.Id).ToList();
        foreach(var banned in squad.Where(p=>p.Position!=PlayerPosition.G).Skip(8))banned.SuspensionMatchesRemaining=2;
        foreach(var g in squad.Where(p=>p.Position==PlayerPosition.G))g.SuspensionMatchesRemaining=2;
        var (starters,bench)=AiManagerBrain.BuildStartersAndBench(squad,TacticalFormations.All["4-4-2"]);
        Assert.Equal(8,starters.Count);Assert.All(starters,id=>Assert.True(squad.Single(p=>p.Id==id).IsAvailableForMatch));
        Assert.True(SquadValidator.ValidatePreMatchLineup("4-4-2",starters,bench,squad,true).IsValid);
        starters[0]=squad.First(p=>p.SuspensionMatchesRemaining>0).Id;
        Assert.False(SquadValidator.ValidatePreMatchLineup("4-4-2",starters,bench,squad,true).IsValid);
    }

    [Fact]
    public void ExtremeShortageUsesAllFiveEligibleAthletesWithoutErasingOrServingBansEarly()
    {
        var (w,p)=World();var squad=w.Players.Where(x=>x.ClubId==p.ClubId).ToList();
        foreach(var x in squad.Where(x=>x.Position!=PlayerPosition.G).Skip(4))x.SuspensionMatchesRemaining=2;
        var (s,b)=AiManagerBrain.BuildStartersAndBench(squad,TacticalFormations.All["4-4-2"]);
        Assert.Equal(5,s.Count);Assert.True(SquadValidator.ValidatePreMatchLineup("4-4-2",s,b,squad,true).IsValid);
        Assert.All(s,id=>Assert.True(squad.Single(x=>x.Id==id).IsAvailableForMatch));
        Assert.All(squad.Where(x=>x.SuspensionMatchesRemaining>0),x=>Assert.Equal(2,x.SuspensionMatchesRemaining));
    }

    [Fact]
    public void RepeatedMinuteAndRestoredFixtureDoNotRepeatEventsCardsOrInjuries()
    {
        var (w,_)=World();var m=Fixture(w,3);m.CurrentMinute=75;
        var map=w.Players.ToDictionary(p=>p.Id);int seed=InjurySeed(m,1,11);
        var h=w.Clubs.Single(c=>c.Id==m.HomeClubId);var a=w.Clubs.Single(c=>c.Id==m.AwayClubId);
        MatchSimulationEngine.SimulateSingleMinute(m,h,a,map,ManagerTacticalIdentity.Equilibrado,ManagerTacticalIdentity.Equilibrado,seed);
        string state=JsonSerializer.Serialize(m),players=JsonSerializer.Serialize(w.Players);
        Assert.Empty(MatchSimulationEngine.SimulateSingleMinute(m,h,a,map,ManagerTacticalIdentity.Equilibrado,ManagerTacticalIdentity.Equilibrado,seed));
        Assert.Equal(state,JsonSerializer.Serialize(m));Assert.Equal(players,JsonSerializer.Serialize(w.Players));
        var restored=JsonSerializer.Deserialize<MatchFixture>(state)!;
        Assert.Empty(MatchSimulationEngine.SimulateSingleMinute(restored,h,a,map,ManagerTacticalIdentity.Equilibrado,ManagerTacticalIdentity.Equilibrado,seed));
        Assert.Equal(state,JsonSerializer.Serialize(restored));
    }

    [Fact]
    public void FullFixtureReplayPreservesActualCardsSuspensionHistoryAndOneTimeSettlement()
    {
        var (w,_)=World();foreach(var p in w.Players)p.Behavior=PlayerBehavior.Sarrafeiro;
        var m=Fixture(w,3);w.Fixtures=[m];
        foreach(var c in w.Clubs)c.HumanCoachId=Guid.NewGuid(); // No unsolicited substitutions in this controlled match.
        string snapshot=JsonSerializer.Serialize(w);
        string Replay()
        {
            var world=JsonSerializer.Deserialize<GameWorld>(snapshot)!;var fixture=world.Fixtures[0];
            var home=world.Clubs.Single(c=>c.Id==fixture.HomeClubId);var away=world.Clubs.Single(c=>c.Id==fixture.AwayClubId);
            MatchSimulationEngine.SimulateFullMatchReal(fixture,home,away,null,null,world.Players.Where(p=>p.ClubId==home.Id).ToList(),
                world.Players.Where(p=>p.ClubId==away.Id).ToList(),world.Players.ToDictionary(p=>p.Id),199801);
            DisciplineEngine.SettleFinishedMatches(world,[fixture]);
            Assert.True(world.Players.Sum(p=>p.CardHistory.Count)>0);
            string before=JsonSerializer.Serialize(world);
            DisciplineEngine.SettleFinishedMatches(world,[fixture]);
            Assert.Equal(before,JsonSerializer.Serialize(world));
            return before;
        }
        Assert.Equal(Replay(),Replay());
    }

    [Fact]
    public void SuspensionAloneNeverLosesForceAndRemainsEligibleForLeagueReserveGrowth()
    {
        bool grew=false;
        for(int seed=1;seed<=30&&!grew;seed++)
        {
            var (w,p)=World();w.Seed=seed;p.Strength=5;p.SuspensionMatchesRemaining=1;
            var m=Fixture(w,3);m.CurrentMinute=90;m.IsFinished=true;
            DisciplineEngine.SettleFinishedMatches(w,[m]);Assert.Equal(5,p.Strength);
            ClassicForceEvolutionEngine.ApplyFinishedRound(w,[m]);
            Assert.DoesNotContain(w.PlayerStrengthHistory,e=>e.PlayerId==p.Id&&e.ChangeReason!="RESERVE_PROGRESS");
            grew=p.Strength==6;
        }
        Assert.True(grew);
    }

    internal static (GameWorld World,Player Player) World()
    {
        var w=WorldSeedFactory.CreateSeededWorld(forceEvolutionMode:ForceEvolutionMode.ClassicAdapted);
        foreach(var p in w.Players){p.Behavior=PlayerBehavior.FairPlay;p.Strength=50;p.IsStar=false;}
        var club=w.Clubs.First(c=>c.DivisionTier==DivisionTier.PrimeiraDivisao);
        return(w,w.Players.First(p=>p.ClubId==club.Id&&p.Position==PlayerPosition.D));
    }
    internal static MatchFixture Fixture(GameWorld w,int round,CompetitionType type=CompetitionType.League,Guid? home=null)
    {
        var h=w.Clubs.Single(c=>c.Id==(home??w.Clubs.First(c=>c.DivisionTier==DivisionTier.PrimeiraDivisao).Id));
        var a=w.Clubs.First(c=>c.Id!=h.Id&&c.DivisionTier==h.DivisionTier);
        var hs=w.Players.Where(p=>p.ClubId==h.Id).ToList();var aws=w.Players.Where(p=>p.ClubId==a.Id).ToList();
        var (s,b)=AiManagerBrain.BuildStartersAndBench(hs,TacticalFormations.All["4-4-2"]);
        var (t,u)=AiManagerBrain.BuildStartersAndBench(aws,TacticalFormations.All["4-4-2"]);
        w.RoundNumber=round;
        var m=new MatchFixture {GameWorldId=w.Id,SeasonNumber=w.SeasonNumber,RoundNumber=round,DeterministicMatchIndex=42,
            HomeClubId=h.Id,AwayClubId=a.Id,HomeFormation="4-4-2",AwayFormation="4-4-2",CompetitionType=type,
            HomeActiveStarterIds=s,HomeBenchIds=b,AwayActiveStarterIds=t,AwayBenchIds=u,ForceRulesVersion=1,GameDate=w.ClassicForce.GameDate};
        var map=w.Players.ToDictionary(p=>p.Id);DisciplineEngine.CaptureKickoff(m,map);PlayerStatisticsEngine.CaptureLineup(m,map);
        return m;
    }
    internal static MatchEventRecord Event(MatchFixture m,Player p,MatchEventType card,int minute,int? duration=null)
        =>new(){EventType=card,PrimaryPlayerId=p.Id,ClubId=p.ClubId!.Value,Minute=minute,DisciplinarySuspensionMatches=duration};
    internal static void Card(GameWorld w,MatchFixture m,Player p,MatchEventType type,int minute=10,int? duration=null)
    {
        m.CurrentMinute=minute;var e=Event(m,p,type,minute,duration);
        if(type!=MatchEventType.DirectRedCard)p.SeasonYellows++;if(type!=MatchEventType.YellowCard)p.SeasonReds++;
        DisciplineEngine.RecordCard(m,p,e,w.Seed);m.Events.Add(e);
    }
    internal static void Finish(GameWorld w,MatchFixture m){m.IsFinished=true;m.CurrentMinute=90;DisciplineEngine.SettleFinishedMatches(w,[m]);}
    internal static int InjurySeed(MatchFixture m,int index,int count)
    {
        for(int seed=1;seed<=200_000;seed++)
        {
            var rng=DeterministicRng.FromContext(seed,m.SeasonNumber,m.RoundNumber,m.DeterministicMatchIndex,m.CurrentMinute);
            if(rng.NextDouble()<.0018&&rng.NextInt(0,count)==index)return seed;
        }
        throw new InvalidOperationException("Controlled injury seed not found.");
    }
}
