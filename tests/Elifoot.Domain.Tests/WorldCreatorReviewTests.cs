using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Domain;

namespace Elifoot.Domain.Tests;

public sealed class WorldCreatorReviewTests
{
    private static (WorldCreatorRequest Request, WorldCreatorPreview Preview) Generated()
    {
        var request = new WorldCreatorRequest("Mundo de teste", FootballGender.Male, "2026", ["ES"],
            WorldAssemblyMode.Automatic, DistrictFillMode.Ranking, 1857, [], AllowFictionalTeams: true,
            AllowFictionalPlayers: true, DataMode: WorldDataMode.FREE_WORLD, UseWorldCatalogRules: true,
            FictionalOnly: true, GenerateAllRosterPlayers: true);
        var preview = WorldCreatorEngine.Preview(request, new(FootballGender.Male, "2026", ["ES"], [], [], []));
        Assert.Empty(preview.Errors);
        return (request, preview);
    }

    [Theory]
    [InlineData(1, 2)] [InlineData(1, 3)] [InlineData(1, 4)] [InlineData(1, 5)]
    [InlineData(2, 4)] [InlineData(3, 5)] [InlineData(1, 1)] [InlineData(5, 5)]
    public void ReciprocalSwapsPreserveTheFrozenGeneratedUniverseAndStrengths(int firstDivision, int secondDivision)
    {
        var (request, preview) = Generated();
        var assignments = preview.Clubs.Select(entry => new ClubAssignment(entry.Club.Key, entry.Division)).ToList();
        int first = assignments.FindIndex(entry => entry.Division == firstDivision);
        int second = assignments.FindLastIndex(entry => entry.Division == secondDivision);
        (assignments[first], assignments[second]) =
            (assignments[second] with { Division = firstDivision }, assignments[first] with { Division = secondDivision });
        var originalJson = JsonSerializer.Serialize(preview);

        var updated = WorldCreatorEngine.Reassign(request, preview,
            new(assignments, "Mundo revisado", MaxHumans: 3));

        Assert.NotEqual(preview.DraftId, updated.Preview.DraftId);
        Assert.Equal(originalJson, JsonSerializer.Serialize(preview));
        Assert.Equal("Mundo revisado", updated.Request.Name);
        Assert.Equal(3, updated.Request.MaxHumans);
        Assert.Equal(DistrictFillMode.Manual, updated.Request.DistrictMode);
        for (int division = 1; division <= 5; division++)
        {
            Assert.Equal(division == 5 ? 96 : 8, updated.Preview.Clubs.Count(entry => entry.Division == division));
            Assert.Equal(assignments.Where(entry => entry.Division == division).Select(entry => entry.ClubKey),
                updated.Preview.Clubs.Where(entry => entry.Division == division).Select(entry => entry.Club.Key));
        }
        foreach (var entry in updated.Preview.Clubs)
        {
            var original = preview.Clubs.Single(source => source.Club.Key == entry.Club.Key);
            Assert.Equal(JsonSerializer.Serialize(original.Club), JsonSerializer.Serialize(entry.Club));
            Assert.Equal(original.InitialSquadReference, entry.InitialSquadReference);
        }

        var world = WorldCreatorEngine.Create(updated.Request, updated.Preview);
        Assert.Equal(128, world.Clubs.Count);
        foreach (var entry in updated.Preview.Clubs)
        {
            var club = world.Clubs.Single(club => club.Name == entry.Club.Name);
            Assert.Equal((DivisionTier)entry.Division, club.DivisionTier);
            Assert.Equal(entry.Club.Players.Select(player => player.Strength),
                world.Players.Where(player => player.ClubId == club.Id).Select(player => player.Strength));
        }
    }

    [Theory]
    [InlineData("duplicate")] [InlineData("missing")] [InlineData("outsider")]
    [InlineData("capacity")] [InlineData("invalid-division")] [InlineData("name")] [InlineData("humans")]
    public void ReassignRejectsInvalidStructuresBeforeCreatingAnotherPreview(string kind)
    {
        var (request, preview) = Generated();
        var assignments = preview.Clubs.Select(entry => new ClubAssignment(entry.Club.Key, entry.Division)).ToList();
        switch (kind)
        {
            case "duplicate": assignments[1] = assignments[0]; break;
            case "missing": assignments.RemoveAt(0); break;
            case "outsider": assignments[0] = assignments[0] with { ClubKey = "another-year:another-club" }; break;
            case "capacity": assignments[0] = assignments[0] with { Division = 5 }; break;
            case "invalid-division": assignments[0] = assignments[0] with { Division = 6 }; break;
        }
        var command = new WorldCreatorReassignRequest(assignments,
            Name: kind == "name" ? " " : null, MaxHumans: kind == "humans" ? 9 : null);
        Assert.Throws<ArgumentException>(() => WorldCreatorEngine.Reassign(request, preview, command));
    }

    [Fact]
    public void ConfirmationRevalidatesCountsDuplicatesScopeAndRosterEvenWhenStoredErrorsAreEmpty()
    {
        var (request, preview) = Generated();
        var changed = preview.Clubs.ToList();
        changed[0] = changed[0] with { Division = 5 };
        Assert.Throws<ArgumentException>(() => WorldCreatorEngine.Create(request, preview with { Clubs = changed }));
        changed = preview.Clubs.ToList();
        changed[0] = changed[0] with { Club = changed[1].Club };
        Assert.Throws<ArgumentException>(() => WorldCreatorEngine.Create(request, preview with { Clubs = changed }));
        changed = preview.Clubs.ToList();
        changed[0] = changed[0] with { Club = changed[0].Club with { CountryCode = "XX" } };
        Assert.Throws<ArgumentException>(() => WorldCreatorEngine.Create(request, preview with { Clubs = changed }));
        changed = preview.Clubs.ToList();
        changed[0] = changed[0] with { Club = changed[0].Club with { Season = "1901" } };
        Assert.Throws<ArgumentException>(() => WorldCreatorEngine.Create(request, preview with { Clubs = changed }));
        changed = preview.Clubs.ToList();
        changed[0] = changed[0] with { Club = changed[0].Club with { Players = [] } };
        Assert.Throws<ArgumentException>(() => WorldCreatorEngine.Create(request, preview with { Clubs = changed }));
        Assert.Throws<ArgumentException>(() => WorldCreatorEngine.Create(request with { MaxHumans = 0 }, preview));
    }
}
