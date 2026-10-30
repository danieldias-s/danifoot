using System.Diagnostics;
using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Domain;

namespace Elifoot.Simulation.Tests;

public partial class MultiSeasonAndBenchmarkTests
{
    [Fact]
    public void PlayerRankings009_RealFullSeason_ReconcilesEveryRoundAndPaysExactAwards()
    {
        var w=WorldSeedFactory.CreateSeededWorld("Rankings 009 — temporada real",199801,inviteCode:"RANK009");
        var checks=new List<object>();long minutes=0;int games=0;
        for(int round=1;round<=w.TotalRoundsInSeason;round++)
        {
            w.RoundNumber=round;w.RoundState=RoundState.PREPARING;
            CompetitionAndSeasonEngine.EnsureCupFixturesForRound(w,round);
            var fixtures=w.Fixtures.Where(f=>f.SeasonNumber==1&&f.RoundNumber==round).OrderBy(f=>f.DeterministicMatchIndex).ToList();
            var experiment=new ExperimentResult();SimulateRoundWithRealMatchEngine(w,fixtures,experiment);
            games+=fixtures.Count;minutes+=experiment.RealMinutesSimulated;w.RoundState=RoundState.RESULTS;
            MarketAndEconomyEngine.SettleMatchAndRoundFinancials(w,fixtures);
            CompetitionAndSeasonEngine.ApplyFinishedRoundResultsToStandingsAndCareers(w,fixtures);
            var completed=w.Fixtures.Where(f=>f.SeasonNumber==1&&f.IsFinished&&f.CurrentMinute>0).ToList();
            int officialGoals=completed.Sum(m=>m.Events.Count(e=>e.EventType==MatchEventType.Goal&&!e.IsOwnGoal));
            Assert.Equal(officialGoals,w.PlayerStatistics.Sum(s=>s.GoalsScored));
            Assert.Equal(completed.Sum(m=>m.HomeGoals+m.AwayGoals),w.PlayerStatistics.Sum(s=>s.GoalsConceded));
            Assert.Equal(completed.Sum(m=>m.Events.Count(e=>e.EventType is MatchEventType.YellowCard or MatchEventType.SecondYellowRedCard)),w.PlayerStatistics.Sum(s=>s.YellowCards));
            Assert.Equal(completed.Sum(m=>m.Events.Count(e=>e.EventType is MatchEventType.DirectRedCard or MatchEventType.SecondYellowRedCard)),w.PlayerStatistics.Sum(s=>s.RedCards));
            Assert.Empty(w.PlayerStatisticsCoverageWarnings);
            string before=JsonSerializer.Serialize(w.PlayerStatistics);
            PlayerStatisticsEngine.ApplyFinishedMatches(w,fixtures);
            Assert.Equal(before,JsonSerializer.Serialize(w.PlayerStatistics));
            var scopes=new[]{"general","division1","division2","division3","division4","cup"};
            foreach(var scope in scopes)
            {
                var scorer=PlayerStatisticsEngine.GetRankings(w,"scorers",scope,"current");
                var keeper=PlayerStatisticsEngine.GetRankings(w,"goalkeepers",scope,"current");
                var defender=PlayerStatisticsEngine.GetRankings(w,"defenders",scope,"current");
                Assert.All(keeper.Rows,r=>Assert.True(r.MatchesPlayed>=20));
                Assert.All(defender.Rows,r=>Assert.True(r.MatchesPlayed>=20));
                Assert.True(scorer.Rows.Zip(scorer.Rows.Skip(1)).All(pair=>pair.First.GoalsScored>=pair.Second.GoalsScored));
            }
            checks.Add(new {Round=round,Matches=completed.Count,OfficialGoals=officialGoals,Applied=w.AppliedPlayerStatisticsMatches.Count});
        }
        var seasonRanking=PlayerStatisticsEngine.GetRankings(w,"scorers","general","current");
        int maximum=seasonRanking.Rows.Max(r=>r.GoalsScored);
        var winners=seasonRanking.Rows.Where(r=>r.GoalsScored==maximum).ToList();
        var cash=w.Clubs.ToDictionary(c=>c.Id,c=>c.CashBalance);int ledgerBefore=w.Transactions.Count;
        var history=CompetitionAndSeasonEngine.FinalizeSeasonAndAdvance(w);
        var awards=PlayerStatisticsEngine.GetSeasonAwards(w,1);
        Assert.Equal(winners.Select(r=>r.PlayerId).Order(),awards.Awards.Select(a=>a.PlayerId).Order());
        Assert.All(awards.Awards,a=>Assert.Equal(3_000_000m,a.AwardAmount));
        Assert.Equal(maximum,history.TopScorerGoals);
        var awardLedger=w.Transactions.Where(t=>t.IdempotencyKey.StartsWith("season_top_scorer:")).ToList();
        Assert.Equal(awards.Awards.Count,awardLedger.Count);Assert.Equal(winners.Count*3_000_000m,awardLedger.Sum(t=>t.Amount));
        foreach(var club in w.Clubs)Assert.Equal(club.CashBalance-cash[club.Id],w.Transactions.Skip(ledgerBefore).Where(t=>t.ClubId==club.Id).Sum(t=>t.Amount));
        Assert.Empty(PlayerStatisticsEngine.GetRankings(w,"scorers","general","current").Rows);
        static object HistoryFacts(PlayerRankingRowDto r)=>new {r.Rank,r.PlayerId,r.MatchesPlayed,r.GoalsScored,r.GoalsConceded,r.YellowCards,r.RedCards,r.MinutesPlayed,r.Average};
        Assert.Equal(seasonRanking.Rows.Select(HistoryFacts),PlayerStatisticsEngine.GetRankings(w,"scorers","general","season",1).Rows.Select(HistoryFacts));
        var reload=JsonSerializer.Deserialize<GameWorld>(JsonSerializer.Serialize(w))!;
        var balances=reload.Clubs.Select(c=>c.CashBalance).ToArray();
        PlayerStatisticsEngine.AwardSeason(reload,1);
        Assert.Equal(balances,reload.Clubs.Select(c=>c.CashBalance));
        Assert.Equal(awards.Awards,PlayerStatisticsEngine.GetSeasonAwards(reload,1).Awards);
        string output=Environment.GetEnvironmentVariable("ELIFOOT_AUDIT_OUTPUT") ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../docs/audit-009"));Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output,"real-season-world.json"),JsonSerializer.Serialize(w));
        File.WriteAllText(Path.Combine(output,"REAL_SEASON_009.json"),JsonSerializer.Serialize(new {Seed=w.Seed,Clubs=128,Season=1,Matches=games,Minutes=minutes,RoundChecks=checks,
            Goals=seasonRanking.Rows.Sum(r=>r.GoalsScored),MaximumGoals=maximum,Awards=awards,Ledger=awardLedger,
            Rankings= new[]{"general","division1","division2","division3","division4","cup"}.Select(scope=>new {Scope=scope,
                Scorers=PlayerStatisticsEngine.GetRankings(w,"scorers",scope,"season",1),Goalkeepers=PlayerStatisticsEngine.GetRankings(w,"goalkeepers",scope,"season",1),Defenders=PlayerStatisticsEngine.GetRankings(w,"defenders",scope,"season",1)}),
            ReloadIdempotent=true,FinancialReconciliation=true},new JsonSerializerOptions{WriteIndented=true}));
    }

    [Theory]
    [InlineData(128)]
    [InlineData(1024)]
    public void PlayerRankings009_QueryLoad_128And1024Clubs(int clubs)
    {
        var w=WorldSeedFactory.CreateSeededWorld(totalClubs:clubs);
        foreach(var p in w.Players.Where(p=>p.ClubId!=null))
            for(int season=1;season<=2;season++)
                w.PlayerStatistics.Add(new() {PlayerId=p.Id,ClubId=p.ClubId!.Value,Season=season,CompetitionType=CompetitionType.League,Division=w.Clubs.First(c=>c.Id==p.ClubId).DivisionTier,
                    Position=p.Position,MatchesPlayed=20,GoalsScored=p.Position==PlayerPosition.A?p.Strength%12:0,GoalsConceded=p.Position==PlayerPosition.G?p.Strength%20:0,
                    YellowCards=p.Strength%5,RedCards=p.Strength%2,MinutesPlayed=1800});
        w.SeasonNumber=2;
        var watch=Stopwatch.StartNew();int rows=0;
        foreach(var category in new[]{"scorers","goalkeepers","defenders"})
            foreach(var scope in new[]{"general","division1","division2","division3","division4","cup"})
                rows+=PlayerStatisticsEngine.GetRankings(w,category,scope,"career").Rows.Count;
        watch.Stop();Assert.True(watch.Elapsed<TimeSpan.FromSeconds(10),$"18 consultas: {watch.Elapsed}");Assert.True(rows>0);
        string output=Environment.GetEnvironmentVariable("ELIFOOT_AUDIT_OUTPUT") ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../docs/audit-009"));Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output,$"QUERY_LOAD_{clubs}_009.json"),JsonSerializer.Serialize(new {Clubs=clubs,Players=w.Players.Count,Aggregates=w.PlayerStatistics.Count,Queries=18,Rows=rows,ElapsedMs=watch.Elapsed.TotalMilliseconds,BudgetMs=10000,Fixture="Synthetic deterministic load records; real128-club season validated separately."},new JsonSerializerOptions{WriteIndented=true}));
    }
}
