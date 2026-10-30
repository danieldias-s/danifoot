using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Domain;
using Elifoot.Infrastructure;

namespace Elifoot.Simulation.Tests;

public partial class MultiSeasonAndBenchmarkTests
{
    [Fact]
    public async Task CoachCareer008_HumanMarketPrepRevalidatesAllPreviouslySelectedLineups()
    {
        var w=WorldSeedFactory.CreateSeededWorld();w.RoundNumber=4;
        var repo=new InMemoryWorldRepository();await repo.SaveWorldAsync(w);
        var o=new MultiplayerRoundOrchestrator(repo,NullMatchRoomNotificationPublisher.Instance);
        var a=(await o.JoinWorldAndAssignClubAsync(w.Id,CoachCareerEngine.StableId("prep-a"),"A","prep-a",w.Clubs[0].Id)).Coach!;
        var b=(await o.JoinWorldAndAssignClubAsync(w.Id,CoachCareerEngine.StableId("prep-b"),"B","prep-b",w.Clubs[1].Id)).Coach!;
        foreach(var p in w.Players){p.IsListedForTransfer=false;if(p.ClubId==a.CurrentClubId)p.Strength=30;if(p.ClubId==b.CurrentClubId)p.Strength=20;}
        var target=w.Players.First(p=>p.ClubId==a.CurrentClubId&&p.Position==PlayerPosition.A);
        target.Strength=50;target.IsListedForTransfer=true;target.AskingPrice=100000;w.Clubs[1].CashBalance=20000000;
        var policies=new Dictionary<Guid,(string PolicyName,string PrimaryFormation,string SecondaryFormation,decimal SpendCapRatio)>{
            [a.Id]=("A","4-4-2","4-4-2",.5m),[b.Id]=("B","4-4-2","4-4-2",.5m)};
        RunAutomatedHumanCoachAgentsRoundPrep(w,policies,new ExperimentResult());
        Assert.Equal(b.CurrentClubId,target.ClubId);
        foreach(var c in w.Clubs.Where(c=>c.HumanCoachId!=null))
        {
            var valid=SquadValidator.ValidatePreMatchLineup(c.PreferredFormation,c.PreferredStarterIds,c.PreferredBenchIds,w.Players.Where(p=>p.ClubId==c.Id).ToList(),true);
            Assert.True(valid.IsValid,valid.ErrorMessage);
        }
    }

    [Fact]
    public async Task CoachCareer008_FiftyRealSeasonsAndDeterministicReplay()
    {
        var replay1=await RunCareer(1);var replay2=await RunCareer(1);
        Assert.Equal(replay1.Signature,replay2.Signature);
        var full=await RunCareer(50);
        Assert.Equal(17550,full.Matches);Assert.Equal(50*5,full.World.CoachTitles.Count);
        Assert.Contains(full.World.AuditLogs,a=>a.ActionType=="COACH_DISMISSED");
        Assert.Contains(full.World.JobInvitations,i=>i.IsAccepted&&i.IsAiCandidate);
        Directory.CreateDirectory(DocsDir);
        File.WriteAllText(Path.Combine(DocsDir,"COACH_CAREER_50_SEASONS_008.json"),JsonSerializer.Serialize(new {
            Seed=199801,Seasons=50,Clubs=128,Humans=8,full.Matches,full.Minutes,full.Signature,
            ReplayOneSeasonSignature=replay1.Signature,ReplayIdentical=true,
            HumanAccepts=full.HumanAccepts,HumanRefusals=full.HumanRefusals,
            AiAccepts=full.World.JobInvitations.Count(i=>i.IsAiCandidate&&i.IsAccepted),
            AiRefusals=full.World.JobInvitations.Count(i=>i.IsAiCandidate&&i.IsRejected),
            Dismissals=full.World.AuditLogs.Where(a=>a.ActionType=="COACH_DISMISSED").ToList(),
            Decisions=full.World.JobInvitations,
            Titles=full.World.CoachTitles,
            Ranking=CompetitionAndSeasonEngine.GetClassicCoachRanking(full.World),
            SameResultsLegacyComparison=CoachCareerEngine.Coaches(full.World).Select(c=>new {
                c.Id,c.Name,NewPoints=c.Points,LegacyMandoPoints=full.LegacyPoints.GetValueOrDefault(c.Id),
                NewLeagueTitles=c.LeagueTitles,
                LegacyFirstDivisionTitles=full.World.CoachTitles.Count(t=>t.CoachId==c.Id&&t.TitleType=="LEAGUE"&&t.Division==DivisionTier.PrimeiraDivisao),
                c.CupTitles }),
            Notes="Legacy comparison applies old scoring to these same actual results/coach-at-kickoff records, not a separate old hiring trajectory. All ownership and replay invariants checked after each round."
        },new JsonSerializerOptions{WriteIndented=true}));
    }

    private sealed record CareerRun(GameWorld World,int Matches,long Minutes,string Signature,int HumanAccepts,int HumanRefusals,Dictionary<Guid,decimal> LegacyPoints);

    private static async Task<CareerRun> RunCareer(int seasons)
    {
        var w=WorldSeedFactory.CreateSeededWorld("Carreira008",199801);
        var repo=new InMemoryWorldRepository();await repo.SaveWorldAsync(w);
        var orchestrator=new MultiplayerRoundOrchestrator(repo,NullMatchRoomNotificationPublisher.Instance);
        for(int i=0;i<8;i++)
        {
            var h=await orchestrator.JoinWorldAndAssignClubAsync(w.Id,CoachCareerEngine.StableId($"008-human-{i}"),$"Carreira {i+1}",$"008-{i}",w.Clubs[i*4].Id);
            Assert.True(h.Success,h.Error);
        }
        int matches=0,accepted=0,refused=0;long minutes=0;
        var legacy=new Dictionary<Guid,decimal>();
        var signatures=new StringBuilder();
        for(int season=1;season<=seasons;season++)
        {
            for(int round=1;round<=w.TotalRoundsInSeason;round++)
            {
                w.RoundNumber=round;w.RoundState=RoundState.PREPARING;
                CoachCareerEngine.ProcessVacancies(w);
                foreach(var h in w.HumanCoaches)
                {
                    var offer=w.JobInvitations.FirstOrDefault(i=>i.CoachId==h.Id&&!i.IsAccepted&&!i.IsRejected&&!i.IsExpired);
                    if(offer==null)continue;
                    bool accept=(CoachCareerEngine.Ordinal(w)+w.HumanCoaches.IndexOf(h))%4!=0;
                    var result=CoachCareerEngine.Respond(w,h.Id,offer.Id,accept);
                    if(result.Success){if(accept)accepted++;else refused++;}
                }
                CompetitionAndSeasonEngine.EnsureCupFixturesForRound(w,round);
                var fixtures=w.Fixtures.Where(f=>f.SeasonNumber==season&&f.RoundNumber==round).OrderBy(f=>f.DeterministicMatchIndex).ToList();
                foreach(var club in w.Clubs.Where(c=>c.HumanCoachId!=null))
                {
                    var squad=w.Players.Where(p=>p.ClubId==club.Id).ToList();
                    var (s,b)=AiManagerBrain.BuildStartersAndBench(squad,TacticalFormations.All["4-4-2"]);
                    club.PreferredFormation="4-4-2";club.PreferredStarterIds=s;club.PreferredBenchIds=b;
                }
                var metrics=new ExperimentResult();
                SimulateRoundWithRealMatchEngine(w,fixtures,metrics);
                matches+=fixtures.Count;minutes+=metrics.RealMinutesSimulated;
                foreach(var f in fixtures)
                {
                    if(f.HomeCoachAtKickoffId is Guid home)legacy[home]=legacy.GetValueOrDefault(home)+(f.HomeGoals>f.AwayGoals?2:f.HomeGoals==f.AwayGoals?1:0);
                    if(f.AwayCoachAtKickoffId is Guid away)legacy[away]=legacy.GetValueOrDefault(away)+(f.AwayGoals>f.HomeGoals?3:f.AwayGoals==f.HomeGoals?2:0);
                    signatures.Append($"{season}:{round}:{f.DeterministicMatchIndex}:{f.HomeGoals}:{f.AwayGoals}:{f.HomePenaltyGoals}:{f.AwayPenaltyGoals};");
                    Assert.True(f.IsFinished);
                }
                w.RoundState=RoundState.RESULTS;
                MarketAndEconomyEngine.SettleMatchAndRoundFinancials(w,fixtures);
                CompetitionAndSeasonEngine.ApplyFinishedRoundResultsToStandingsAndCareers(w,fixtures);
                string careerBefore=CareerSignature(w);
                CompetitionAndSeasonEngine.ApplyFinishedRoundResultsToStandingsAndCareers(w,fixtures);
                Assert.Equal(careerBefore,CareerSignature(w));
                if(round%2==0)MarketAndEconomyEngine.ProcessWeeklyAiMarketBatch(w);
                CheckOwnership(w);
                Assert.All(CoachCareerEngine.Coaches(w),c=>Assert.InRange(c.State.Morale,0,100));
            }
            CompetitionAndSeasonEngine.FinalizeSeasonAndAdvance(w);CheckOwnership(w);
            Assert.Equal(season*5,w.CoachTitles.Count);
        }
        signatures.Append(CareerSignature(w));
        return new(w,matches,minutes,Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(signatures.ToString()))),accepted,refused,legacy);
    }

    private static string CareerSignature(GameWorld w) => string.Join(";",CoachCareerEngine.Coaches(w).OrderBy(c=>c.Id)
        .Select(c=>$"{c.Id}:{c.Points}:{c.LeagueTitles}:{c.CupTitles}:{c.State.Morale}:{c.ClubId}:{c.Human?.Dismissals??c.Ai!.Dismissals}"));
    private static void CheckOwnership(GameWorld w)
    {
        Assert.Equal(128,w.Clubs.Count);
        foreach(var club in w.Clubs)
        {
            Assert.True(club.HumanCoachId.HasValue^club.AiManagerId.HasValue,$"Two or zero coaches: {club.Name}");
            if(club.HumanCoachId is Guid human){Assert.NotEqual(DivisionTier.Distrital,club.DivisionTier);Assert.Equal(club.Id,w.HumanCoaches.Single(c=>c.Id==human).CurrentClubId);}
            else Assert.Equal(club.Id,w.AiManagers.Single(c=>c.Id==club.AiManagerId).CurrentClubId);
        }
        foreach(var c in CoachCareerEngine.Coaches(w).Where(c=>c.ClubId!=null))
            Assert.Single(w.Clubs,x=>(x.HumanCoachId??x.AiManagerId)==c.Id);
    }
}
