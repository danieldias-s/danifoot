using Elifoot.Contracts;
using Elifoot.Domain;

namespace Elifoot.Domain.Tests;

public sealed class InitialSquadStrength017BTests
{
    private static readonly int[][] Expected =
    [
        [50, 50, 50, 46, 44, 42, 40, 39],
        [38, 37, 36, 34, 32, 30, 28, 27],
        [25, 24, 23, 21, 19, 17, 15, 14],
        [12, 11, 10, 8, 6, 4, 2, 1]
    ];
    private static readonly (int Min, int Max)[] Bounds = [(39, 50), (26, 38), (13, 25), (1, 12)];

    [Theory]
    [InlineData(FootballGender.Male, 199801, "BR", "PT")]
    [InlineData(FootballGender.Female, 199802, "DE", "JP")]
    public void CatalogGenerationPersistsAll32ReferenceSlotsAndReplays(FootballGender gender, int seed, string first, string second)
    {
        var request = new WorldCreatorRequest("017B", gender, "2026", [first, second], WorldAssemblyMode.Automatic,
            DistrictFillMode.Ranking, seed, [], AllowFictionalTeams: true, AllowFictionalPlayers: true,
            DataMode: WorldDataMode.FREE_WORLD, UseWorldCatalogRules: true, FictionalOnly: true);
        var selection = new CatalogSelection(gender, request.Season, request.Countries, [], [], []);
        var preview = WorldCreatorEngine.Preview(request, selection);
        Assert.Empty(preview.Errors);
        var world = WorldCreatorEngine.Create(request, preview);
        Assert.Equal(gender, world.Gender);
        Assert.Equal(preview.DataHash, world.CatalogOrigin!.DataHash);
        Assert.Equal(preview.Clubs.SelectMany(c => c.Club.Players).Select(p => p.Strength), world.Players.Select(p => p.Strength));
        Verify(world);
        var league = world.Players.Where(p => world.Clubs.Take(32).Any(c => c.Id == p.ClubId)).ToList();
        Assert.All(league, p => Assert.Equal(WorldPlayerOrigin.FICTIONAL_GENERATED, p.CatalogIdentity!.Origin));
        Assert.Contains(17, preview.Clubs.Select(c => c.Club.Players.Count));
        Assert.Contains(23, preview.Clubs.Select(c => c.Club.Players.Count));
        var replay = WorldCreatorEngine.Preview(request, selection);
        Assert.Equal(preview.DataHash, replay.DataHash);
        Assert.Equal(preview.Clubs.SelectMany(c => c.Club.Players).Select(p => p.Strength),
            replay.Clubs.SelectMany(c => c.Club.Players).Select(p => p.Strength));
        var other = WorldCreatorEngine.Preview(request with { Seed = seed + 1 }, selection);
        Assert.False(preview.Clubs.Take(32).SelectMany(c => c.Club.Players).Select(p => p.Strength)
            .SequenceEqual(other.Clubs.Take(32).SelectMany(c => c.Club.Players).Select(p => p.Strength)));
    }

    [Fact]
    public void QuickCreationUsesSameRulesAndKeepsHistoricFactoryProfileAvailable()
    {
        Verify(WorldSeedFactory.CreateSeededWorld(seed: -15, applyInitialSquadStrength: true));
        Assert.All(WorldSeedFactory.CreateSeededWorld().Clubs, c => Assert.Null(c.InitialSquadReference));
    }

    private static void Verify(GameWorld world)
    {
        Assert.Equal(new[] { 8, 8, 8, 8, 96 }, world.Clubs.GroupBy(c => c.DivisionTier).OrderBy(g => g.Key).Select(g => g.Count()));
        var squads = world.Players.ToLookup(p => p.ClubId);
        for (int division = 1; division <= 4; division++)
        {
            var teams = world.Clubs.Where(c => (int)c.DivisionTier == division).ToList();
            var (divisionMin, divisionMax) = Bounds[division - 1];
            var firstTeamStrengths = squads[teams[0].Id].Select(p => p.Strength).ToArray();
            for (int i = 0; i < teams.Count; i++)
            {
                var team = teams[i];
                int expected = Expected[division - 1][i];
                bool elite = division == 1 && i < 3;
                int minimum = elite ? 47 : Math.Max(divisionMin, expected - 1);
                int maximum = Math.Min(divisionMax, expected + 1);
                Assert.Equal(new InitialSquadReference017B(i + 1, expected, minimum, maximum), team.InitialSquadReference);
                var squad = squads[team.Id].ToList();
                Assert.InRange(squad.Count, 17, 23);
                Assert.Contains(squad, p => p.Position == PlayerPosition.G);
                Assert.True(squad.Count(p => p.Position != PlayerPosition.G) >= 10);
                Assert.Equal(11, team.PreferredStarterIds.Count);
                Assert.All(squad, p => Assert.InRange(p.Strength, minimum, maximum));
                if (elite) Assert.Equal(new[] { 47, 48, 49, 50 }, squad.Select(p => p.Strength).Distinct().Order());
                if (i is 1 or 2 && elite) Assert.False(firstTeamStrengths.SequenceEqual(squad.Select(p => p.Strength)));
            }
        }
    }
}
