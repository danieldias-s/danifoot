using Elifoot.Contracts;
using Elifoot.Domain;

namespace Elifoot.Domain.Tests;

public class InitialFictionalStrengthTests
{
    [Theory]
    [InlineData(199801, "PT", "BR", FootballGender.Male, true, WorldDataMode.FREE_WORLD)]
    [InlineData(199802, "JP", "ES", FootballGender.Female, true, WorldDataMode.FREE_WORLD)]
    [InlineData(199803, "DE", "FR", FootballGender.Male, false, WorldDataMode.FREE_WORLD)]
    [InlineData(199807, "BR", "GB", FootballGender.Female, false, null)]
    public void FreshFictionalClubsAreTieredInTheActualWorld(int seed, string firstCountry, string secondCountry,
        FootballGender gender, bool catalogRules, WorldDataMode? dataMode)
    {
        var request = new WorldCreatorRequest("Initial strengths", gender, "2026", [firstCountry, secondCountry],
            WorldAssemblyMode.Automatic, DistrictFillMode.Ranking, seed, [], AllowFictionalTeams: true,
            AllowFictionalPlayers: true, DataMode: dataMode, UseWorldCatalogRules: catalogRules, FictionalOnly: catalogRules);
        var selection = new CatalogSelection(gender, request.Season, request.Countries, [], [], []);
        var preview = WorldCreatorEngine.Preview(request, selection);
        var replay = WorldCreatorEngine.Preview(request, selection);
        Assert.Empty(preview.Errors);
        Assert.Equal(preview.DataHash, replay.DataHash);

        var world = WorldCreatorEngine.Create(request, preview);
        var tiers = world.Clubs.GroupBy(c => c.DivisionTier).OrderBy(g => g.Key).ToList();
        Assert.Equal(new[] { 8, 8, 8, 8, 96 }, tiers.Select(g => g.Count()));
        var playersByClub = world.Players.ToLookup(p => p.ClubId);
        double previousSquad = 51, previousXI = 51;
        foreach (var tier in tiers.Take(4))
        {
            var clubMeans = new List<double>();
            var lineupMeans = new List<double>();
            foreach (var club in tier)
            {
                var squad = playersByClub[club.Id].ToList();
                var starters = squad.Where(p => club.PreferredStarterIds.Contains(p.Id)).ToList();
                Assert.Equal(11, starters.Count);
                Assert.InRange(squad.Count - starters.Count, 6, 12);
                Assert.True(squad.Select(p => p.Strength).Distinct().Count() > 3);
                Assert.All(squad, p => Assert.InRange(p.Strength, 1, 50));
                if (dataMode != null)
                    Assert.All(squad, p => Assert.Equal("generated-classic-seed-v1", p.CatalogIdentity?.StrengthEstimate?.Method));
                clubMeans.Add(squad.Average(p => p.Strength));
                lineupMeans.Add(starters.Average(p => p.Strength));
            }
            Assert.True(previousSquad > clubMeans.Max(), $"Elencos da {tier.Key} sem separação no seed {seed}.");
            Assert.True(previousXI > lineupMeans.Max(), $"Titulares da {tier.Key} sem separação no seed {seed}.");
            previousSquad = clubMeans.Min();
            previousXI = lineupMeans.Min();
        }
    }
}
