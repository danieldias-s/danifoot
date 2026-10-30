using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Domain;

namespace Elifoot.Domain.Tests;

public sealed class Coverage013BDomainTests
{
    private static readonly DateOnly AsOf = new(2026, 10, 30);
    [Theory]
    [InlineData("2025-01-01", "2027-01-01", CatalogMembershipStatus.DatedActive)]
    [InlineData("2026-10-30", "2026-10-30", CatalogMembershipStatus.DatedActive)]
    [InlineData("2025-01-01", null, CatalogMembershipStatus.OpenEndedUnconfirmed)]
    [InlineData("2025-01-01", "2026-10-29", CatalogMembershipStatus.Historical)]
    [InlineData("2026-10-31", null, CatalogMembershipStatus.Historical)]
    [InlineData(null, "2027-01-01", CatalogMembershipStatus.Unknown)]
    [InlineData(null, null, CatalogMembershipStatus.Unknown)]
    public void MembershipRequiresExplicitStartAndEndToEstablishCurrentPeriod(string? start, string? end, CatalogMembershipStatus expected)
        => Assert.Equal(expected, CatalogMembershipPolicy.Classify(start == null ? null : DateOnly.Parse(start), end == null ? null : DateOnly.Parse(end), AsOf));

    [Fact]
    public void OpenEndedEvidenceCannotBeMadeCurrentByOnlyChangingItsLabel()
    {
        var athlete = new CatalogAthlete("synthetic-p", "synthetic-team", "fixture", FootballGender.Female, "2026", "Atleta sintética", "G", 25, "fixture", DateTime.UnixEpoch,
            MembershipStart: AsOf.AddDays(-100), MembershipEvidence: new(CatalogMembershipStatus.DatedActive, "statement", "synthetic test", AsOf));
        Assert.False(CatalogMembershipPolicy.Usable(athlete, AsOf));
        Assert.True(CatalogMembershipPolicy.Usable(athlete with { MembershipEnd = AsOf.AddDays(100) }, AsOf));
        Assert.False(CatalogMembershipPolicy.Usable(athlete with { MembershipEnd = AsOf.AddDays(100), MembershipEvidence = athlete.MembershipEvidence! with { Status = CatalogMembershipStatus.Unknown } }, AsOf));
    }

    [Theory] [InlineData(FootballGender.Male)] [InlineData(FootballGender.Female)]
    public void HistoricalAndUnverifiedTeamsNeedExplicitChoiceAndRosterCompletionNeedsSeparateConsent(FootballGender gender)
    {
        var clubs = Enumerable.Range(0, 128).Select(i => new EligibleCatalogClub("synthetic:" + i, "synthetic:" + i, "fixture", "Clube sintético " + i, "FIX" + i,
            "BR", "Brasil", gender, "Liga sintética", "2026", "#006400", "#FFFFFF", null, [], [], RawRosterCount: 0,
            Verification: new(i == 0 ? CatalogClubVerificationStatus.REAL_CLUB_VERIFIED : i == 1 ? CatalogClubVerificationStatus.REAL_CLUB_UNVERIFIED : CatalogClubVerificationStatus.REAL_CLUB_HISTORICAL, "Synthetic verification fixture only", DateTime.UnixEpoch))).ToList();
        var selection = new CatalogSelection(gender, "2026", ["BR"], clubs, [], []);
        var request = new WorldCreatorRequest("Teste sintético", gender, "2026", ["BR"], WorldAssemblyMode.Automatic, DistrictFillMode.Random, 17, []);
        var strict = WorldCreatorEngine.Preview(request, selection);
        Assert.Single(strict.Clubs); Assert.NotEmpty(strict.Errors); Assert.Empty(strict.Clubs.SelectMany(c => c.Club.Players));
        var chosen = request with { IncludeHistoricalClubs = true, IncludeUnverifiedClubs = true };
        Assert.NotEmpty(WorldCreatorEngine.Preview(chosen, selection).Errors);
        var completed = WorldCreatorEngine.Preview(chosen with { AllowFictionalPlayers = true }, selection);
        Assert.Empty(completed.Errors); Assert.Equal(128, completed.Clubs.Count); Assert.False(completed.Clubs.Any(c => c.Club.IsFictional));
        Assert.All(completed.Clubs.SelectMany(c => c.Club.Players), p => { Assert.Equal(gender, p.Gender); Assert.Equal(CatalogDataOrigin.Generated, p.IdentityOrigin); });
        var world = WorldCreatorEngine.Create(chosen, completed);
        var json = JsonSerializer.Serialize(world); Assert.Equal(completed.DataHash, JsonSerializer.Deserialize<GameWorld>(json)!.CatalogOrigin!.DataHash);
    }

    [Fact]
    public void OpenResultEloExcludesClubsWithoutGamesAndLimitsConfidenceOfDisconnectedLeagues()
    {
        var date = new DateTime(2026, 10, 30, 0, 0, 0, DateTimeKind.Utc);
        var matches = new[] { new CatalogHistoricalResult("a", "b", 2, 0, date), new CatalogHistoricalResult("c", "d", 1, 1, date) };
        var ranking = DerivedClubRanking.CalculateOpenEvidence(["a", "b", "c", "d", "no-games"], matches, "openfootball", FootballGender.Female, "2026", date);
        Assert.Equal(4, ranking.Count); Assert.DoesNotContain(ranking, r => r.ClubExternalId == "no-games");
        Assert.All(ranking, r => { Assert.Contains("disconnected", r.Method!); Assert.Equal(CatalogDataOrigin.Estimated, r.Origin); Assert.InRange(r.Confidence!.Value, 0, .35); Assert.Equal(FootballGender.Female, r.Gender); });
        Assert.Equal(ranking, DerivedClubRanking.CalculateOpenEvidence(["d", "c", "b", "a"], matches.Reverse(), "openfootball", FootballGender.Female, "2026", date));
    }
}
