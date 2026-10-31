using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Domain;
using Elifoot.Infrastructure.Catalog;
namespace Elifoot.Testing;

internal static class Final014BWorlds
{
    private sealed record FrozenInput(WorldCreatorRequest Request, WorldCreatorPreview Preview);
    public static GameWorld Create(FootballGender gender)
    {
        string directory=Environment.GetEnvironmentVariable("ELIFOOT_014B_INPUTS_DIRECTORY")
            ?? throw new InvalidOperationException("Prepare os snapshots próprios da 014B antes dos testes.");
        string path=Path.Combine(directory,gender+".input.json");
        var input=JsonSerializer.Deserialize<FrozenInput>(File.ReadAllText(path),CatalogImportCodec.Json)!;
        if(input.Request.DataMode!=WorldDataMode.REAL_CLUBS_SIMULATED||!input.Request.AllowFictionalPlayers)
            throw new InvalidOperationException("O ensaio exige consentimento explícito e clubes reais verificados.");
        var world=WorldCreatorEngine.Create(input.Request,input.Preview);
        if(world.Clubs.Count!=128||world.Players.Any(p=>p.CatalogIdentity==null))throw new InvalidOperationException("Snapshot inicial inválido.");
        return world;
    }
}
