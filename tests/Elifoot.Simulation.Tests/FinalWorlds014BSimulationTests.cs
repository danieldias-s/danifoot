using Elifoot.Contracts;
using Elifoot.Domain;
namespace Elifoot.Simulation.Tests;

public partial class MultiSeasonAndBenchmarkTests
{
    [Theory][InlineData(FootballGender.Male)][InlineData(FootballGender.Female)]
    public void FinalWorlds014B_RealCatalogWithAuthorizedGeneratedSquads100RealSeasons(FootballGender gender)
    {
        RunEconomyBalanceCase("final",199801,100,()=>Elifoot.Testing.Final014BWorlds.Create(gender),
            Path.Combine(DocsDir,"integration-014b",gender.ToString()));
    }
}
