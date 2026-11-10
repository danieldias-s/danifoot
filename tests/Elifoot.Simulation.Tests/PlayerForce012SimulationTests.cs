using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Domain;

namespace Elifoot.Simulation.Tests;

public partial class MultiSeasonAndBenchmarkTests
{
    [Fact]
    public void Force012_100Seasons_DistributionAndRealEngineRegression()
    {
        string mode = Environment.GetEnvironmentVariable("ELIFOOT_FORCE012_MODE") ?? "candidate";
        string folder = Path.Combine(DocsDir, "force-012");
        Directory.CreateDirectory(folder);
        var distributions = new List<object>();
        int observations = 0;
        var result = RunMultiSeasonExperiment("FORCE_012_199801", 199801, 100,
            ScenarioMode.ScenarioA_AllAi, observeForce012: (world, phase) =>
            {
                Assert.All(world.Players, p => Assert.InRange(p.Strength, 1, 50));
                if (phase == "round") { observations++; return; }
                var clubs = world.Clubs.ToDictionary(c => c.Id);
                var groups = world.Players.GroupBy(p => (
                    Division: p.ClubId is Guid id && clubs.TryGetValue(id, out var c) ? c.DivisionTier.ToString() : "FreeAgent",
                    Position: p.Position.ToString()));
                foreach (var group in groups.OrderBy(g => g.Key.Division).ThenBy(g => g.Key.Position))
                    distributions.Add(new
                    {
                        Season = world.SeasonNumber, Phase = phase, group.Key.Division, group.Key.Position,
                        Count = group.Count(), Minimum = group.Min(p => p.Strength), Maximum = group.Max(p => p.Strength),
                        Mean = Math.Round(group.Average(p => p.Strength), 6),
                        Histogram = group.GroupBy(p => p.Strength).OrderBy(g => g.Key)
                            .ToDictionary(g => g.Key.ToString(), g => g.Count())
                    });
                Assert.True(world.Players.Count(p => p.Strength == 50) < world.Players.Count / 2,
                    "A majority of the population unexpectedly reached strength 50.");
            });
        var report = new
        {
            Mode = mode, Seed = 199801, result.SeasonsCompleted, result.TotalMatchesCompleted,
            result.RealMinutesSimulated, RoundObservations = observations, result.FinalStateSignature,
            result.InvalidLineupSelectionViolations, result.DoubleSettledMatchFinancialViolations,
            result.TotalGoals, result.Injuries, Distributions = distributions
        };
        var json = new JsonSerializerOptions { WriteIndented = true };
        string path = Path.Combine(folder, $"{mode}-100-seasons.json");
        File.WriteAllText(path, JsonSerializer.Serialize(report, json));
        Assert.Equal(100, result.SeasonsCompleted);
        Assert.Equal(35_100, result.TotalMatchesCompleted);
        Assert.Equal(100 * CompetitionAndSeasonEngine.BuildSeasonRoundSchedule(128).Count, observations);
        Assert.Equal(0, result.InvalidLineupSelectionViolations);
        Assert.Equal(0, result.DoubleSettledMatchFinancialViolations);
        string baselinePath = Path.Combine(folder, "baseline-100-seasons.json");
        if (mode == "candidate" && File.Exists(baselinePath))
        {
            using var baseline = JsonDocument.Parse(File.ReadAllText(baselinePath));
            Assert.Equal(baseline.RootElement.GetProperty("FinalStateSignature").GetString(), result.FinalStateSignature);
            Assert.True(JsonElement.DeepEquals(baseline.RootElement.GetProperty("Distributions"),
                JsonSerializer.SerializeToElement(distributions, json)), "Strength distributions differ from the 011 baseline.");
        }
    }
}
