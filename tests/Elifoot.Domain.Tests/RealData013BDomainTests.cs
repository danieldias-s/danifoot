using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Domain;

namespace Elifoot.Domain.Tests;

public sealed class RealData013BDomainTests
{
    private static readonly List<string> Countries = ["BR", "AR", "GB", "ES", "DE", "IT", "FR", "PT"];
    private static CatalogSelection Selection(FootballGender gender)
    {
        var clubs = Enumerable.Range(0, 160).Select(index => {
            string country = Countries[index % 8], id = "fixture-013b-" + index;
            var scope = new CatalogScopeKey(country, gender, "fixture", "2026", CatalogDataType.Squads, "fixture013b");
            var reference = new CatalogReference(Guid.NewGuid(), scope, "fixture-v1", DateTime.UnixEpoch, DateTime.UnixEpoch, true, "Fixture", null);
            string[] positions = ["G", "G", "D", "D", "D", "D", "D", "D", "M", "M", "M", "M", "M", "M", "A", "A", "A", "A"];
            var players = positions.Select((position, j) => new CatalogAthlete(id + "-p" + j, id, "fixture013b", gender, "2026", $"Atleta fictício {index}-{j}", position,
                25, "fixture013b", DateTime.UnixEpoch, id + "-p" + j, CatalogDataOrigin.Fixture,
                new(InitialPlayerStrength.Version + ":pending", CatalogDataOrigin.Estimated, .2, "fixture", .5, null),
                new((index * 7 + j * 11) % 101 / 10.0, 100 + j * 40, 10, j % 3, j % 2, "fixture-relative"))).ToList();
            return new EligibleCatalogClub(WorldCreatorEngine.ClubKey(country, gender, "fixture013b", id), id, "fixture013b", "Clube fictício " + index, "FIX" + index,
                country, country, gender, "Liga fictícia", "2026", "#006400", "#FFFFFF", index + 1, players, [reference], RawRosterCount: 18);
        }).ToList();
        return new(gender, "2026", Countries, clubs, clubs.SelectMany(c => c.References).ToList(), []);
    }
    private static WorldCreatorRequest Request(FootballGender gender) => new("Mundo sintético 013B", gender, "2026", Countries,
        WorldAssemblyMode.Automatic, DistrictFillMode.Random, 130013, []);
    [Theory] [InlineData(FootballGender.Male)] [InlineData(FootballGender.Female)]
    public void EstimatedInitialForcesAreRelativeVariedReproducibleAndWorldSnapshotIsIndependent(FootballGender gender)
    {
        var selection = Selection(gender); var request = Request(gender);
        var a = WorldCreatorEngine.Preview(request, selection); var b = WorldCreatorEngine.Preview(request, selection);
        Assert.Empty(a.Errors); Assert.Equal(128, a.Clubs.Count); Assert.Equal(128, a.Clubs.Select(c => c.Club.Key).Distinct().Count());
        var athletes = a.Clubs.SelectMany(c => c.Club.Players).ToList();
        Assert.All(athletes, p => { Assert.InRange(p.Strength, 1, 50); Assert.Equal(CatalogDataOrigin.Estimated, p.StrengthEstimate!.Origin); Assert.Equal(gender, p.Gender); });
        Assert.True(athletes.Max(p => p.Strength) >= 45); Assert.True(athletes.Select(p => p.Strength).Distinct().Count() >= 20);
        Assert.NotEqual(Enumerable.Repeat(50, 18), a.Clubs[0].Club.Players.Select(p => p.Strength));
        Assert.Equal(athletes.Select(p => p.Strength), b.Clubs.SelectMany(c => c.Club.Players).Select(p => p.Strength));
        var world = WorldCreatorEngine.Create(request, a); Assert.Equal(ForceEvolutionMode.ClassicAdapted, world.ForceEvolutionMode); Assert.Equal(2304, world.Players.Count);
        var json = JsonSerializer.Serialize(world); var loaded = JsonSerializer.Deserialize<GameWorld>(json)!; Assert.Equal(a.DataHash, loaded.CatalogOrigin!.DataHash);
        selection.Clubs[0].Players.Clear(); Assert.Equal(json, JsonSerializer.Serialize(world));
    }
    [Fact]
    public void ManualPlacementDoesNotReduceForcesAndGendersUseIdenticalScale()
    {
        var male = Selection(FootballGender.Male); var female = Selection(FootballGender.Female);
        // Same identities/performance/seed isolate gender from all other inputs.
        var a = InitialPlayerStrength.Apply(male.Clubs, 3); var b = InitialPlayerStrength.Apply(female.Clubs, 3);
        Assert.Equal(a.SelectMany(c => c.Players).Select(p => p.Strength), b.SelectMany(c => c.Players).Select(p => p.Strength));
        var request = Request(FootballGender.Male);
        var automatic = WorldCreatorEngine.Preview(request, male);
        var assignments = automatic.Clubs.Select(c => new ClubAssignment(c.Club.Key, c.Division)).ToList();
        (assignments[0], assignments[24]) = (assignments[0] with { Division = 4 }, assignments[24] with { Division = 1 });
        var manual = WorldCreatorEngine.Preview(request with { Mode = WorldAssemblyMode.Custom, DistrictMode = DistrictFillMode.Manual,
            Assignments = assignments, FillRemainingDivisions = false }, male);
        Assert.Empty(manual.Errors);
        var moved = manual.Clubs.Single(c => c.Club.Key == automatic.Clubs[0].Club.Key); Assert.Equal(4, moved.Division);
        Assert.Equal(automatic.Clubs[0].Club.Players.Select(p => p.Strength), moved.Club.Players.Select(p => p.Strength));
    }
    [Fact]
    public void MissingIndividualDataIsAnExplicitEstimateAndNeverAnOfficialRating()
    {
        var s = Selection(FootballGender.Female);
        var without = s.Clubs.Select(c => c with { Players = c.Players.Select(p => p with { Performance = null }).ToList() }).ToList();
        var result = InitialPlayerStrength.Apply(without, 17);
        Assert.All(result.SelectMany(c => c.Players), p => { Assert.Equal("relative-v1:missing-individual", p.StrengthEstimate!.Method); Assert.Equal(.2, p.StrengthEstimate.Confidence); Assert.InRange(p.Strength, 1, 50); });
        Assert.True(result[0].Players.Select(p => p.Strength).Distinct().Count() > 1);
    }
    [Fact]
    public void LargeRostersPreservePositionsAndIncompleteRostersRequireSeparateConsent()
    {
        var club = Selection(FootballGender.Male).Clubs[0];
        var extra = Enumerable.Range(0, 30).Select(i => club.Players[14] with { PlayerExternalId = "extra" + i, CanonicalPlayerId = "extra" + i, Strength = 50 }).ToList();
        var large = club with { Players = [.. club.Players, .. extra], RawRosterCount = 48 };
        var selected = InitialRosterPolicy.Prepare(large, 1, false); Assert.Empty(selected.Issues); Assert.Equal(24, selected.Players.Count);
        Assert.Contains(selected.Players, p => p.Position == "G"); Assert.Contains(selected.Players, p => p.Position == "D");
        var shortSquad = club with { Players = club.Players.Where(p => p.Position == "M").Take(5).ToList() };
        Assert.NotEmpty(InitialRosterPolicy.Prepare(shortSquad, 1, false).Issues);
        var completed = InitialRosterPolicy.Prepare(shortSquad, 1, true); Assert.Empty(completed.Issues);
        Assert.Contains(completed.Players, p => p.IdentityOrigin == CatalogDataOrigin.Generated && p.PlayerName.StartsWith("Atleta fictício"));
        var s = Selection(FootballGender.Male); s.Clubs[0] = shortSquad;
        Assert.NotEmpty(WorldCreatorEngine.Preview(Request(FootballGender.Male), s).Errors);
        var authorized = WorldCreatorEngine.Preview(Request(FootballGender.Male) with { AllowFictionalPlayers = true }, s);
        Assert.Empty(authorized.Errors); Assert.Contains(authorized.Warnings, w => w.Contains("atletas fictícios"));
    }
    [Fact]
    public void CanonicalPlayerDuplicatesAcrossClubsAreRejected()
    {
        var s = Selection(FootballGender.Female); s.Clubs[1].Players[2] = s.Clubs[1].Players[2] with { CanonicalPlayerId = s.Clubs[0].Players[2].CanonicalPlayerId };
        var result = WorldCreatorEngine.Preview(Request(FootballGender.Female), s);
        Assert.Contains(result.Errors, e => e.Contains("Jogadores duplicados"));
    }
    [Fact]
    public void DerivedRankingIsExplicitReproducibleAndGenderSpecific()
    {
        var date = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);
        var matches = new[] { new CatalogHistoricalResult("a", "b", 2, 0, date.AddDays(-2)), new CatalogHistoricalResult("a", "c", 1, 0, date.AddDays(-1)) };
        var result = DerivedClubRanking.Calculate(["a", "b", "c"], matches, "authorized-fixture", FootballGender.Female, "2026", date);
        Assert.Equal("a", result[0].ClubExternalId); Assert.All(result, r => { Assert.Equal(CatalogDataOrigin.Estimated, r.Origin); Assert.Equal("derived-elo-v1", r.Method); Assert.Equal(FootballGender.Female, r.Gender); Assert.DoesNotContain("Opta", r.Source); });
        Assert.Equal(result, DerivedClubRanking.Calculate(["a", "b", "c"], matches.Reverse(), "authorized-fixture", FootballGender.Female, "2026", date));
    }
}
