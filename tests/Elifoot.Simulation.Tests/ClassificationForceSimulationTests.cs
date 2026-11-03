using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Domain;

namespace Elifoot.Simulation.Tests;

public partial class MultiSeasonAndBenchmarkTests
{
    [Fact]
    public void ClassificationForce_100SeasonsOn017BWorld()
    {
        const int seed = 199801;
        var directory = Path.Combine(DocsDir, "classification-force");
        Directory.CreateDirectory(directory);
        var seasons = new List<object>();
        var ownership = new Dictionary<Guid, (Guid ClubId, DivisionTier Division)>();
        var movements = new Dictionary<(DivisionTier From, DivisionTier To), (int Count, int Strength, int At40)>();
        var phaseTotals = new Dictionary<CompetitionType, (int Rounds, int Appearances, int Starters, int BenchEntries, int StarterLosses, int BenchLosses, int ReserveGains, int InjuryPoints)>();
        int lastEvent = 0;
        var result = RunMultiSeasonExperiment("Classification force comparison", seed, 100, ScenarioMode.ScenarioA_AllAi,
            worldFactory: () => WorldSeedFactory.CreateSeededWorld(seed: seed,
                forceEvolutionMode: ForceEvolutionMode.ClassicAdapted, applyInitialSquadStrength: true),
            observeForce012: (w, phase) =>
            {
                var clubs = w.Clubs.ToDictionary(c => c.Id);
                if (phase == "round")
                {
                    var playedMatches = w.Fixtures.Where(m => m.SeasonNumber == w.SeasonNumber
                        && m.RoundNumber == w.RoundNumber && m.IsFinished && m.CurrentMinute > 0).ToList();
                    var starters = playedMatches.SelectMany(m => m.StatisticsInitialLineup)
                        .Where(p => p.IsStarter).Select(p => p.PlayerId).ToHashSet();
                    var benchEntries = playedMatches.SelectMany(m => m.Events)
                        .Where(e => e.EventType == MatchEventType.Substitution).Select(e => e.PrimaryPlayerId)
                        .OfType<Guid>().Where(id => !starters.Contains(id)).ToHashSet();
                    var changes = w.PlayerStrengthHistory.Skip(lastEvent).ToList();
                    var roundType = CompetitionAndSeasonEngine.GetCompetitionTypeForRound(w, w.RoundNumber);
                    var totals = phaseTotals.GetValueOrDefault(roundType);
                    totals.Rounds++;
                    totals.Appearances += starters.Count + benchEntries.Count;
                    totals.Starters += starters.Count;
                    totals.BenchEntries += benchEntries.Count;
                    totals.ReserveGains += changes.Sum(e => e.ReserveGrowth);
                    totals.InjuryPoints += changes.Sum(e => e.DailyInjuryLoss);
                    var participationLosses = changes.Where(e => e.ChangeReason == "PLAYED_MATCH_DECLINE").ToList();
                    if (roundType != CompetitionType.League)
                    {
                        Assert.Empty(participationLosses);
                        Assert.All(changes, e => Assert.Equal(0,e.ReserveGrowth));
                    }
                    totals.StarterLosses += participationLosses.Count(e => starters.Contains(e.PlayerId));
                    totals.BenchLosses += participationLosses.Count(e => benchEntries.Contains(e.PlayerId));
                    Assert.All(participationLosses, e => Assert.True(starters.Contains(e.PlayerId) || benchEntries.Contains(e.PlayerId)));
                    Assert.All(changes.Where(e => e.ReserveGrowth > 0), e =>
                    {
                        Assert.Equal(1, e.ReserveGrowth);
                        Assert.True(e.StrengthAfter <= e.GrowthCeiling);
                        Assert.DoesNotContain(e.PlayerId, starters);
                    });
                    Assert.All(changes.Where(e => e.ChangeReason is "PLAYED_MATCH_DECLINE" or "RESERVE_PROGRESS")
                        .GroupBy(e => e.PlayerId), g => Assert.Single(g));
                    phaseTotals[roundType] = totals;
                }
                lastEvent = w.PlayerStrengthHistory.Count;

                var current = w.Players.Where(p => p.ClubId != null).ToDictionary(p => p.Id,
                    p => (ClubId: p.ClubId!.Value, Division: clubs[p.ClubId!.Value].DivisionTier));
                if (phase == "round")
                    foreach (var player in w.Players)
                        if (ownership.TryGetValue(player.Id, out var from) && current.TryGetValue(player.Id, out var to) && from.ClubId != to.ClubId)
                        {
                            var stats = movements.GetValueOrDefault((from.Division, to.Division));
                            movements[(from.Division, to.Division)] = (stats.Count + 1, stats.Strength + player.Strength,
                                stats.At40 + (player.Strength >= 40 ? 1 : 0));
                        }
                ownership = current;
                Assert.All(w.Players, p => Assert.InRange(p.Strength, 1, 50));

                if (phase == "initial" || (phase == "round" && w.RoundNumber == w.TotalRoundsInSeason))
                {
                    var groups = w.Players.Where(p => p.ClubId != null)
                        .GroupBy(p => clubs[p.ClubId!.Value].DivisionTier).OrderBy(g => g.Key);
                    seasons.Add(new
                    {
                        Season = phase == "initial" ? 0 : w.SeasonNumber,
                        OverallMean = w.Players.Average(p => p.Strength),
                        At1 = w.Players.Count(p => p.Strength == 1),
                        At50 = w.Players.Count(p => p.Strength == 50),
                        Divisions = groups.Select(g =>
                        {
                            var teamMeans = g.GroupBy(p => p.ClubId).Select(t => t.Average(p => p.Strength)).ToList();
                            return new { Division = (int)g.Key, Count = g.Count(), Mean = g.Average(p => p.Strength),
                                WeakestTeamMean = teamMeans.Min(), StrongestTeamMean = teamMeans.Max(),
                                At1 = g.Count(p => p.Strength == 1), At50 = g.Count(p => p.Strength == 50) };
                        }).ToList()
                    });
                    File.WriteAllText(Path.Combine(directory, "progress.txt"), (seasons.Count - 1).ToString());
                }
            });

        Assert.Equal(35_100, result.TotalMatchesCompleted);
        Assert.Equal(0, result.InvalidLineupSelectionViolations);
        Assert.Equal(0, result.InvalidSquadLimitViolations);
        Assert.Equal(0, result.DoubleSettledMatchFinancialViolations);
        Assert.Equal(101, seasons.Count);
        File.WriteAllText(Path.Combine(directory, "100-seasons.json"), JsonSerializer.Serialize(new
        {
            Seed = seed, result.SeasonsCompleted, result.TotalMatchesCompleted, result.RealMinutesSimulated,
            result.TotalPurchases, result.AuctionsWon,
            result.InvalidLineupSelectionViolations, result.InvalidSquadLimitViolations, result.DoubleSettledMatchFinancialViolations,
            Seasons = seasons,
            Rounds = phaseTotals.OrderBy(e => e.Key).Select(e => new { Competition = e.Key.ToString(),
                e.Value.Rounds, e.Value.Appearances, e.Value.Starters, e.Value.BenchEntries,
                e.Value.StarterLosses, e.Value.BenchLosses, e.Value.ReserveGains, e.Value.InjuryPoints }),
            ClubChanges = movements.OrderBy(e => e.Key.From).ThenBy(e => e.Key.To).Select(e => new
            {
                From = (int)e.Key.From, To = (int)e.Key.To, e.Value.Count,
                MeanStrengthAtObservation = (double)e.Value.Strength / e.Value.Count, e.Value.At40
            })
        }, new JsonSerializerOptions { WriteIndented = true }));
    }
}
