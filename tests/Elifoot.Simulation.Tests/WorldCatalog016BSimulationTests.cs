using Elifoot.Contracts;
using Elifoot.Domain;
namespace Elifoot.Simulation.Tests;
public partial class MultiSeasonAndBenchmarkTests
{
    [Theory][InlineData(FootballGender.Male)][InlineData(FootballGender.Female)]
    public void WorldCatalog016B_VariableInitialSquads100RealEngineSeasons(FootballGender gender)
    {
        GameWorld Create() {
            var request=new WorldCreatorRequest("016B authorized fictional simulation",gender,"2026",["ES","GB"],
                WorldAssemblyMode.Automatic,DistrictFillMode.Ranking,199801,[],AllowFictionalTeams:true,AllowFictionalPlayers:true,
                DataMode:WorldDataMode.FREE_WORLD,UseWorldCatalogRules:true,FictionalOnly:true);
            var preview=WorldCreatorEngine.Preview(request,new(gender,"2026",request.Countries,[],[],[]));
            Assert.Empty(preview.Errors);
            Assert.Contains(preview.Clubs,c=>c.Club.Players.Count==17);Assert.Contains(preview.Clubs,c=>c.Club.Players.Count==23);
            return WorldCreatorEngine.Create(request,preview);
        }
        RunEconomyBalanceCase("final",199801,100,Create,Path.Combine(DocsDir,"world-catalog-016b",gender.ToString()));
    }
}
