using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Domain;

namespace Elifoot.Simulation.Tests;

public class ClassicForce012CInjuryTriggerTests
{
    [Fact]
    public void FirstInjuryTriggerKeepsLegacyOccurrenceStreamAndNominalForce()
    {
        var initial=WorldSeedFactory.CreateSeededWorld();
        string snapshot=JsonSerializer.Serialize(initial);
        bool found=false;
        for(int seed=1;seed<=50&&!found;seed++)
        {
            var legacy=JsonSerializer.Deserialize<GameWorld>(snapshot)!;
            var classic=JsonSerializer.Deserialize<GameWorld>(snapshot)!;
            var lm=Fixture(legacy,false);var cm=Fixture(classic,true);
            var lp=legacy.Players.ToDictionary(p=>p.Id);var cp=classic.Players.ToDictionary(p=>p.Id);
            for(int minute=1;minute<=90;minute++)
            {
                lm.CurrentMinute=cm.CurrentMinute=minute;
                var le=MatchSimulationEngine.SimulateSingleMinute(lm,legacy.Clubs[0],legacy.Clubs[1],lp,
                    ManagerTacticalIdentity.Equilibrado,ManagerTacticalIdentity.Equilibrado,seed);
                var ce=MatchSimulationEngine.SimulateSingleMinute(cm,classic.Clubs[0],classic.Clubs[1],cp,
                    ManagerTacticalIdentity.Equilibrado,ManagerTacticalIdentity.Equilibrado,seed);
                var injury=le.FirstOrDefault(e=>e.EventType==MatchEventType.Injury);
                if(injury==null)
                {
                    Assert.Equal(le.Select(e=>(e.EventType,e.PrimaryPlayerId,e.SecondaryPlayerId)),ce.Select(e=>(e.EventType,e.PrimaryPlayerId,e.SecondaryPlayerId)));
                    continue;
                }
                var adapted=Assert.Single(ce,e=>e.EventType==MatchEventType.Injury&&e.PrimaryPlayerId==injury.PrimaryPlayerId);
                Assert.Equal(injury.Minute,adapted.Minute);Assert.Equal(injury.ClubId,adapted.ClubId);
                var id=injury.PrimaryPlayerId!.Value;
                Assert.Equal(lp[id].Strength,cp[id].Strength);
                Assert.InRange(lp[id].InjuryRoundsRemaining,1,3);
                Assert.InRange(cp[id].InjuryDaysRemaining,2,15);
                for(int next=minute+1;next<=90;next++)
                {
                    cm.CurrentMinute=next;
                    var events=MatchSimulationEngine.SimulateSingleMinute(cm,classic.Clubs[0],classic.Clubs[1],cp,
                        ManagerTacticalIdentity.Equilibrado,ManagerTacticalIdentity.Equilibrado,seed);
                    Assert.DoesNotContain(events,e=>e.PrimaryPlayerId==id||e.SecondaryPlayerId==id);
                }
                Assert.Equal(minute,ClassicForceEvolutionEngine.ParticipationMinutes(cm)[id]);
                found=true;break;
            }
        }
        Assert.True(found);
    }

    private static MatchFixture Fixture(GameWorld w,bool classic)
    {
        var (hs,hb)=AiManagerBrain.BuildStartersAndBench(w.Players.Where(p=>p.ClubId==w.Clubs[0].Id).ToList(),TacticalFormations.All["4-4-2"]);
        var (aws,awb)=AiManagerBrain.BuildStartersAndBench(w.Players.Where(p=>p.ClubId==w.Clubs[1].Id).ToList(),TacticalFormations.All["4-4-2"]);
        return new MatchFixture {GameWorldId=w.Id,SeasonNumber=1,RoundNumber=3,DeterministicMatchIndex=1,
            HomeClubId=w.Clubs[0].Id,AwayClubId=w.Clubs[1].Id,HomeFormation="4-4-2",AwayFormation="4-4-2",
            HomeActiveStarterIds=hs,HomeBenchIds=hb,AwayActiveStarterIds=aws,AwayBenchIds=awb,
            ForceRulesVersion=classic?1:0,GameDate=classic?new DateOnly(1998,8,1):null};
    }
}
