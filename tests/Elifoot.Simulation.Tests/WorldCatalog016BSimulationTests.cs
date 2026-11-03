using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Domain;
namespace Elifoot.Simulation.Tests;
public partial class MultiSeasonAndBenchmarkTests
{
    [Theory][InlineData(FootballGender.Male)][InlineData(FootballGender.Female)]
    public void WorldCatalog016B_VariableInitialSquads100RealEngineSeasons(FootballGender gender)
    {
        RunEconomyBalanceCase("final",199801,100,()=>CreateFictionalCatalogWorld(gender,199801,["ES","GB"]),
            Path.Combine(DocsDir,"world-catalog-016b",gender.ToString()));
    }

    [Theory]
    [InlineData(FootballGender.Male, 199801, "PT", "BR")]
    [InlineData(FootballGender.Female, 199802, "DE", "JP")]
    public void FictionalInitialStrength_FirstSeasonCompletesWithTheRealEngine(
        FootballGender gender, int seed, string firstCountry, string secondCountry)
    {
        var result = RunMultiSeasonExperiment("Fictional initial strength", seed, 1, ScenarioMode.ScenarioA_AllAi,
            worldFactory: () => CreateFictionalCatalogWorld(gender, seed, [firstCountry, secondCountry]));
        Assert.Equal(351, result.TotalMatchesCompleted);
        Assert.True(result.RealMinutesSimulated >= 351 * 90);
        Assert.Equal(0, result.InvalidLineupSelectionViolations);
        Assert.Equal(0, result.InvalidSquadLimitViolations);
        Assert.Equal(0, result.DoubleSettledMatchFinancialViolations);
        Directory.CreateDirectory(DocsDir);
        File.WriteAllText(Path.Combine(DocsDir, $"initial-strength-season-{seed}.json"), JsonSerializer.Serialize(new
        {
            Gender = gender.ToString(), Seed = seed, Countries = new[] { firstCountry, secondCountry },
            result.TotalMatchesCompleted, result.RealMinutesSimulated, result.InvalidLineupSelectionViolations,
            result.InvalidSquadLimitViolations, result.DoubleSettledMatchFinancialViolations
        }));
    }

    private static GameWorld CreateFictionalCatalogWorld(FootballGender gender, int seed, List<string> countries)
    {
        var request = new WorldCreatorRequest("016B authorized fictional simulation", gender, "2026", countries,
            WorldAssemblyMode.Automatic, DistrictFillMode.Ranking, seed, [], AllowFictionalTeams: true, AllowFictionalPlayers: true,
            DataMode: WorldDataMode.FREE_WORLD, UseWorldCatalogRules: true, FictionalOnly: true);
        var preview = WorldCreatorEngine.Preview(request, new(gender, request.Season, countries, [], [], []));
        Assert.Empty(preview.Errors);
        Assert.Contains(preview.Clubs, c => c.Club.Players.Count == 17);
        Assert.Contains(preview.Clubs, c => c.Club.Players.Count == 23);
        return WorldCreatorEngine.Create(request, preview);
    }
}
