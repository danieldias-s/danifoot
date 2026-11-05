using System.Text.Json;
using Elifoot.Domain;
namespace Elifoot.Simulation.Tests;

public partial class MultiSeasonAndBenchmarkTests
{
    [Fact] public void History017C_OneTenAndHundredSeasonsFromRealMatchesRemainConsistentAfterPruning()
    {
        var evidence=new List<object>();GameWorld? final=null;
        var output=Environment.GetEnvironmentVariable("ELIFOOT_HISTORY_OUTPUT")??Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../artifacts/history-017c"));
        Directory.CreateDirectory(output);
        var run=RunMultiSeasonExperiment("HISTORY_017C_REAL_100",199817,100,ScenarioMode.ScenarioA_AllAi,
            worldFactory:()=>WorldSeedFactory.CreateSeededWorld(worldName:"Histórico 017C",seed:199817,totalClubs:64),
            observeForce012:(w,stage)=> {
                if(stage!="seasonFinalized")return;
                int closed=w.SeasonNumber-1;final=w;
                Assert.Equal(closed,w.History.ClosedSeasons.Count);
                Assert.Equal(w.History.Matches.Count,w.History.Matches.Keys.Distinct().Count());
                Assert.Equal(w.History.Titles.Count,w.History.Titles.DistinctBy(t=>(t.Season,t.Competition,t.Division,t.ClubId,t.RunnerUp)).Count());
                if(closed is 1 or 10 or 100) {
                    var club=HistoryEngine.GetClubHistory(w,w.Clubs[0].Id)!;
                    Assert.Equal(closed,club.Seasons.Count(s=>s.Closed));Assert.Equal(club.Total.Games,club.Seasons.Sum(s=>s.Total.Games));
                    Assert.Equal(club.Total.GoalsFor,club.Seasons.Sum(s=>s.Total.GoalsFor));
                    var player=PlayerStatisticsEngine.GetHistory(w,w.Players[0].Id)!;
                    Assert.Equal(player.Career.MatchesPlayed,player.SeasonTotals.Sum(s=>s.Games));Assert.Equal(player.Career.GoalsScored,player.SeasonTotals.Sum(s=>s.Goals));
                    Assert.Equal(player.Career.Assists,player.SeasonTotals.Sum(s=>s.Assists));
                    evidence.Add(new{seasons=closed,matches=w.History.Matches.Count,clubSeasons=w.History.ClubSeasons.Count,transfers=w.History.Transfers.Count,titles=w.History.Titles.Count,playerGoals=player.Career.GoalsScored});
                    File.WriteAllText(Path.Combine(output,"progress.json"),JsonSerializer.Serialize(evidence,new JsonSerializerOptions{WriteIndented=true}));
                }
            });
        Assert.Equal(100,run.SeasonsCompleted);Assert.NotNull(final);
        Assert.Equal(run.TotalMatchesCompleted,final.History.Matches.Count);
        Assert.True(final.Fixtures.Count<final.History.Matches.Count);
        var restored=JsonSerializer.Deserialize<GameWorld>(JsonSerializer.Serialize(final))!;HistoryEngine.Observe(restored);
        File.WriteAllText(Path.Combine(output,"snapshot.json"),JsonSerializer.Serialize(restored));
        Assert.Equal(final.History.Matches.Count,restored.History.Matches.Count);Assert.Equal(final.History.Transfers.Count,restored.History.Transfers.Count);
        File.WriteAllText(Path.Combine(output,"final.json"),JsonSerializer.Serialize(new{run.SeasonsCompleted,run.TotalMatchesCompleted,run.RealMinutesSimulated,historyMatches=restored.History.Matches.Count,retainedFixtures=restored.Fixtures.Count,snapshots=evidence},new JsonSerializerOptions{WriteIndented=true}));
    }
}
