using Elifoot.Contracts;
namespace Elifoot.Domain;

/// <summary>Metadata only. Never changes generation, RNG, strength, contracts or financial rules.</summary>
public static class RuntimePlayerProvenance
{
    public static void EnsureGeneratedPlayers(GameWorld world)
    {
        if (world.CatalogOrigin?.DataMode == null) return; // Historical worlds keep their original representation.
        foreach (var player in world.Players.Where(p => p.CatalogIdentity == null))
        {
            int index = world.Clubs.FindIndex(c => c.Id == player.ClubId);
            var initialClub = index >= 0 && index < world.CatalogOrigin.Clubs.Count ? world.CatalogOrigin.Clubs[index].Club : null;
            string id = player.Id.ToString("N");
            player.CatalogIdentity = new(WorldPlayerOrigin.FICTIONAL_GENERATED, "elifoot-game-generated", id,
                "game-generated:" + id, initialClub?.Key ?? "GAME_FREE_AGENT", initialClub?.CountryCode ?? "XX",
                world.Gender, initialClub?.Season ?? world.CatalogOrigin.Season, world.UpdatedAtUtc, world.ClassicForce.GameDate, [],
                GeneratedSeed: world.Seed, CreatedInGameSeason: world.SeasonNumber);
        }
    }
}
