using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Domain;

namespace Elifoot.Domain.Tests;

public class HistoricalCatalogWorldTests
{
    [Fact]
    public void TwoYearsOfTheSameClubKeepIndependentRostersAndNamesInTheSave()
    {
        var clubs = MixedCatalog();
        var request = Request(["1985", "2025"], ["PT", "ES"]);
        var preview = WorldCreatorEngine.Preview(request, Selection(clubs, request));

        Assert.Empty(preview.Errors);
        Assert.Equal(128, preview.Clubs.Count);
        Assert.Equal(96, preview.Clubs.Count(entry => entry.Division == 5));
        Assert.Equal(128, preview.DataSummary!.CompleteRealSquads);
        Assert.Equal(128 * 18, preview.DataSummary.RealCurrentPlayers);
        Assert.Equal(clubs.Select(club => club.Key), preview.Clubs.Select(entry => entry.Club.Key));

        var world = WorldCreatorEngine.Create(request, preview);
        var early = world.Clubs.Single(club => club.Name == "Clube de teste (1985)");
        var late = world.Clubs.Single(club => club.Name == "Clube de teste (2025)");
        Assert.NotEqual(early.Id, late.Id);
        Assert.Equal("TST (1985)", early.ShortName);
        Assert.Equal("TST (2025)", late.ShortName);
        Assert.All(world.Clubs, club => Assert.Matches(@" \((1985|2025)\)$", club.Name));

        var earlyPlayers = world.Players.Where(player => player.ClubId == early.Id).ToList();
        var latePlayers = world.Players.Where(player => player.ClubId == late.Id).ToList();
        Assert.Empty(earlyPlayers.Select(player => player.Id).Intersect(latePlayers.Select(player => player.Id)));
        Assert.Empty(earlyPlayers.Select(player => player.CatalogIdentity!.CanonicalIdentity)
            .Intersect(latePlayers.Select(player => player.CatalogIdentity!.CanonicalIdentity)));
        Assert.All(earlyPlayers, player =>
        {
            Assert.StartsWith("Atleta de teste 1985", player.Name);
            Assert.Equal("1985", player.CatalogIdentity!.Season);
            Assert.Equal(WorldPlayerOrigin.REAL_HISTORICAL_CATALOG, player.CatalogIdentity.Origin);
            Assert.Null(player.CatalogIdentity.MembershipEvidence);
            Assert.InRange(player.CatalogIdentity.Number!.Value, 1, 18);
        });
        Assert.All(latePlayers, player => Assert.StartsWith("Atleta de teste 2025", player.Name));
        Assert.Equal(world.Players.Count, world.History.Memberships.Count);
        Assert.All(world.History.Memberships.Where(entry => entry.ClubId == early.Id),
            entry => Assert.Equal("Clube de teste (1985)", entry.ClubName));
        Assert.All(world.History.Memberships.Where(entry => entry.ClubId == late.Id),
            entry => Assert.Equal("Clube de teste (2025)", entry.ClubName));
        var lateBalance = late.CashBalance;
        early.CashBalance -= 10m;
        Assert.Equal(lateBalance, late.CashBalance);

        // The save owns its own copy, and the year suffix is never written into the source club name.
        clubs[0].Players.Clear();
        var restored = JsonSerializer.Deserialize<GameWorld>(JsonSerializer.Serialize(world))!;
        Assert.Equal(new[] { "1985", "2025" }, restored.CatalogOrigin!.CatalogYears);
        Assert.Equal("Clube de teste", restored.CatalogOrigin.Clubs[0].Club.Name);
        Assert.Equal(18, restored.CatalogOrigin.Clubs[0].Club.Players.Count);
        Assert.Equal("Clube de teste (1985)", restored.Clubs[0].Name);
        Assert.Equal(early.CashBalance, restored.Clubs[0].CashBalance);
    }

    [Fact]
    public void APrivateAssemblyRetainsCatalogYearsAndEachVariantWhenItCreatesANewWorld()
    {
        var request = Request(["1985", "2025"], ["PT", "ES"]);
        var preview = WorldCreatorEngine.Preview(request, Selection(MixedCatalog(), request));
        var world = WorldCreatorEngine.Create(request, preview);
        var source = WorldEditorAndPackageService.ExportWorldToPackage(world);
        var assemblyId = Guid.NewGuid();
        var countries = new List<CatalogCountryEntry> { new("PT", "Portugal"), new("ES", "Espanha") };

        var edited = source with
        {
            Clubs = source.Clubs.Select((club, index) => index switch
            {
                0 => club with { DivisionTier = DivisionTier.SegundaDivisao },
                8 => club with { DivisionTier = DivisionTier.PrimeiraDivisao },
                _ => club
            }).ToList()
        };
        var saved = CustomAssemblyPolicy.Prepare(edited, source, assemblyId, countries, trustedCatalog: true, personalized: false);
        Assert.True(CustomAssemblyPolicy.Validate(saved, countries, source, personalized: false).IsValid);
        Assert.Equal(request.CatalogYears, saved.CatalogOrigin!.CatalogYears);
        Assert.Equal(preview.Clubs.Select(entry => entry.Club.Key).Order(), saved.CatalogOrigin.Clubs.Select(entry => entry.Club.Key).Order());
        Assert.All(saved.CatalogOrigin.Clubs, entry => Assert.DoesNotContain($"({entry.Club.Season})", entry.Club.Name));

        var created = CustomAssemblyPolicy.CreateIndependent(saved, assemblyId, 1, request.Countries, "Cópia da montagem", 2);
        var early = created.Clubs.Single(club => club.Name == "Clube de teste (1985)");
        var late = created.Clubs.Single(club => club.Name == "Clube de teste (2025)");
        Assert.Equal(DivisionTier.SegundaDivisao, early.DivisionTier);
        Assert.Equal(DivisionTier.PrimeiraDivisao, late.DivisionTier);
        Assert.DoesNotContain(world.Clubs, club => club.Id == early.Id || club.Id == late.Id);
        Assert.Equal(request.CatalogYears, created.CatalogOrigin!.CatalogYears);
        Assert.All(created.Players.Where(player => player.ClubId == early.Id), player =>
        {
            Assert.Equal("1985", player.CatalogIdentity!.Season);
            Assert.NotNull(player.CatalogIdentity.Number);
        });
        Assert.All(created.Players.Where(player => player.ClubId == late.Id), player =>
        {
            Assert.Equal("2025", player.CatalogIdentity!.Season);
            Assert.NotNull(player.CatalogIdentity.Number);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AOneYearWorldUsesPlainNamesAndReadsBothLegacyAndNewSnapshots(bool local)
    {
        var clubs = Enumerable.Range(0, 129).Where(i => i != 8).Select(i => Club(i, "1985", "PT")).ToList();
        if (!local)
            clubs = clubs.Select(club => club with
            {
                Players = club.Players.Select(player => player with
                {
                    IdentityOrigin = null,
                    CanonicalPlayerId = LocalHistoricalCatalogSource.PlayerKey(club.Key, player.PlayerExternalId)
                }).ToList()
            }).ToList();
        var request = Request(["1985"], ["PT"]);
        if (!local) request = request with { CatalogYears = null, DataMode = null, UseWorldCatalogRules = false };
        var preview = WorldCreatorEngine.Preview(request, Selection(clubs, request));
        Assert.Empty(preview.Errors);
        var world = WorldCreatorEngine.Create(request, preview);
        Assert.Equal("Clube de teste", world.Clubs[0].Name);
        Assert.Equal("TST", world.Clubs[0].ShortName);
        Assert.Equal(request.CatalogYears, world.CatalogOrigin!.CatalogYears);
    }

    [Fact]
    public void SwappingTwoVersionsAndReplacingAnAvailableClubKeepsEverySlotUnique()
    {
        var clubs = MixedCatalog();
        var request = Request(["1985", "2025"], ["PT", "ES"]);
        var assignments = clubs.Take(32).Select((club, i) => new ClubAssignment(club.Key, i / 8 + 1)).ToList();
        assignments[0] = assignments[0] with { ClubKey = clubs[8].Key };
        assignments[8] = assignments[8] with { ClubKey = clubs[0].Key };
        assignments[1] = assignments[1] with { ClubKey = clubs[32].Key };
        request = request with { Mode = WorldAssemblyMode.Custom, FillRemainingDivisions = false, Assignments = assignments };

        var preview = WorldCreatorEngine.Preview(request, Selection(clubs, request));
        Assert.Empty(preview.Errors);
        Assert.Equal(clubs[8].Key, preview.Clubs.First(entry => entry.Division == 1).Club.Key);
        Assert.Equal(clubs[0].Key, preview.Clubs.First(entry => entry.Division == 2).Club.Key);
        Assert.Equal(clubs[32].Key, preview.Clubs[1].Club.Key);
        Assert.Contains(preview.Clubs, entry => entry.Division == 5 && entry.Club.Key == clubs[1].Key);
        Assert.Equal(128, preview.Clubs.Select(entry => entry.Club.Key).Distinct().Count());

        assignments[1] = assignments[1] with { ClubKey = clubs[8].Key };
        var duplicate = WorldCreatorEngine.Preview(request, Selection(clubs, request));
        Assert.Contains(duplicate.Errors, error => error.StartsWith("Clube repetido"));
        Assert.Throws<ArgumentException>(() => WorldCreatorEngine.Create(request, duplicate));
    }

    [Fact]
    public void IncompleteFileBlocksRealModeAndExplicitFictionalModeUsesTheSameClubs()
    {
        var clubs = MixedCatalog();
        clubs[0] = clubs[0] with { Players = [] };
        var request = Request(["1985", "2025"], ["PT", "ES"]);

        var real = WorldCreatorEngine.Preview(request, Selection(clubs, request));
        Assert.Equal(127, real.DataSummary!.CompleteRealSquads);
        Assert.Equal(1, real.PendingSlots);
        Assert.Throws<ArgumentException>(() => WorldCreatorEngine.Create(request, real));

        var fictional = request with
        {
            DataMode = WorldDataMode.REAL_CLUBS_SIMULATED,
            AllowFictionalPlayers = true,
            GenerateAllRosterPlayers = true
        };
        var generated = WorldCreatorEngine.Preview(fictional, Selection(clubs, fictional));
        Assert.Empty(generated.Errors);
        Assert.Equal(128, generated.Clubs.Count);
        Assert.Equal(0, generated.DataSummary!.RealCurrentPlayers);
        Assert.All(generated.Clubs.SelectMany(entry => entry.Club.Players),
            player => Assert.Equal(WorldPlayerOrigin.FICTIONAL_GENERATED, player.PlayerOrigin));
        Assert.Equal(128, WorldCreatorEngine.Create(fictional, generated).Clubs.Count);
    }

    [Fact]
    public void YearsWithoutLocalClubsAreRejectedEvenWithFictionalCompletion()
    {
        var clubs = MixedCatalog();
        var request = Request(["1985", "2025", "2000"], ["PT", "ES"]) with
        {
            DataMode = WorldDataMode.FREE_WORLD,
            AllowFictionalPlayers = true,
            AllowFictionalTeams = true
        };
        var preview = WorldCreatorEngine.Preview(request, Selection(clubs, request));
        Assert.Contains(preview.Errors, error => error.Contains("2000"));
    }

    private static WorldCreatorRequest Request(List<string> years, List<string> countries) =>
        new("Catálogo histórico", FootballGender.Male, years[0], countries, WorldAssemblyMode.Automatic,
            DistrictFillMode.Ranking, 791, [], DataMode: WorldDataMode.REAL_COMPLETE, UseWorldCatalogRules: true,
            PreserveRankedSquadStrengths: true, CatalogYears: years);

    [Fact]
    public void ReviewedSwapKeepsCountryYearIdentityRosterAndOriginalStrengths()
    {
        var request = Request(["1985", "2025"], ["PT", "ES"]);
        var preview = WorldCreatorEngine.Preview(request, Selection(MixedCatalog(), request));
        var assignments = preview.Clubs.Select(entry => new ClubAssignment(entry.Club.Key, entry.Division)).ToList();
        (assignments[0], assignments[40]) =
            (assignments[40] with { Division = 1 }, assignments[0] with { Division = 5 });
        var updated = WorldCreatorEngine.Reassign(request, preview, new(assignments));
        Assert.Equal(preview.Clubs[40].Club.Key, updated.Preview.Clubs[0].Club.Key);
        Assert.Equal(preview.Clubs[0].Club.Key, updated.Preview.Clubs[40].Club.Key);
        foreach (var original in preview.Clubs)
        {
            var moved = updated.Preview.Clubs.Single(entry => entry.Club.Key == original.Club.Key);
            Assert.Equal(JsonSerializer.Serialize(original.Club), JsonSerializer.Serialize(moved.Club));
        }
        var world = WorldCreatorEngine.Create(updated.Request, updated.Preview);
        Assert.Equal(["1985", "2025"], world.CatalogOrigin!.CatalogYears);
        Assert.All(world.Clubs, club => Assert.Matches(@" \((1985|2025)\)$", club.Name));
        Assert.Equal(128, world.CatalogOrigin.Clubs.Select(entry => entry.Club.Key).Distinct().Count());
    }

    [Theory]
    [InlineData(DistrictFillMode.Ranking)] [InlineData(DistrictFillMode.Random)]
    public void DistrictModeAfterReviewUsesTheExistingOrderingAndKeepsManualPrincipalDivisions(DistrictFillMode mode)
    {
        var request = Request(["1985", "2025"], ["PT", "ES"]);
        var catalog = MixedCatalog();
        var preview = WorldCreatorEngine.Preview(request, Selection(catalog, request));
        var assignments = preview.Clubs.Select(entry => new ClubAssignment(entry.Club.Key, entry.Division)).ToList();
        // A reciprocal manual swap between principal divisions must survive the district mode change.
        (assignments[0], assignments[8]) =
            (assignments[8] with { Division = 1 }, assignments[0] with { Division = 2 });
        var changed = WorldCreatorEngine.Reassign(request, preview, new(assignments));
        var reversedDistricts = changed.Preview.Clubs.Select(entry => new ClubAssignment(entry.Club.Key, entry.Division)).ToList();
        reversedDistricts.Reverse(32, 96);
        var updated = WorldCreatorEngine.Reassign(changed.Request, changed.Preview, new(reversedDistricts, DistrictMode: mode));
        var expected = WorldCreatorEngine.Preview(request with { DistrictMode = mode }, Selection(catalog, request));

        Assert.Equal(changed.Preview.Clubs.Where(entry => entry.Division < 5), updated.Preview.Clubs.Where(entry => entry.Division < 5));
        Assert.Equal(expected.Clubs.Where(entry => entry.Division == 5).Select(entry => entry.Club.Key),
            updated.Preview.Clubs.Where(entry => entry.Division == 5).Select(entry => entry.Club.Key));
        Assert.Equal(changed.Preview.Clubs.Select(entry => entry.Club.Key).Order(), updated.Preview.Clubs.Select(entry => entry.Club.Key).Order());
        foreach (var club in updated.Preview.Clubs)
            Assert.Equal(JsonSerializer.Serialize(changed.Preview.Clubs.Single(entry => entry.Club.Key == club.Club.Key).Club),
                JsonSerializer.Serialize(club.Club));
        var repeated = WorldCreatorEngine.Reassign(changed.Request, changed.Preview, new(reversedDistricts, DistrictMode: mode));
        Assert.Equal(updated.Preview.Clubs.Select(entry => entry.Club.Key), repeated.Preview.Clubs.Select(entry => entry.Club.Key));
    }

    private static CatalogSelection Selection(List<EligibleCatalogClub> clubs, WorldCreatorRequest request) =>
        new(request.Gender, request.Season, request.Countries, clubs, [], []);

    private static List<EligibleCatalogClub> MixedCatalog() => Enumerable.Range(0, 128)
        .Select(i => Club(i, i < 8 ? "1985" : "2025", i % 2 == 0 ? "PT" : "ES")).ToList();

    private static EligibleCatalogClub Club(int index, string year, string country)
    {
        bool shared = index is 0 or 8;
        string id = shared ? "test-club" : $"test-club-{index}";
        string name = shared ? "Clube de teste" : $"Clube de teste {index}";
        string provider = LocalHistoricalCatalogSource.Provider;
        var key = LocalHistoricalCatalogSource.ClubKey(country, year, id, FootballGender.Male);
        var players = Enumerable.Range(0, 18).Select(i => new CatalogAthlete(
            $"p{i}", id, provider, FootballGender.Male, year, $"Atleta de teste {year} {i}",
            i < 2 ? "G" : i < 8 ? "D" : i < 14 ? "M" : "A", 25, provider, DateTime.UnixEpoch,
            IdentityOrigin: CatalogDataOrigin.Real, Number: i + 1)).ToList();
        var rank = new CatalogRank(id, provider, FootballGender.Male, year, index + 1, provider, DateTime.UnixEpoch,
            Score: 1000 - index, Confidence: 1);
        return new(key, id, provider, name, "TST", country, country == "PT" ? "Portugal" : "Espanha",
            FootballGender.Male, "Catálogo local", year, "#123456", "#ABCDEF", rank.Rank, players, [],
            RankingInfo: rank, Verification: new(CatalogClubVerificationStatus.REAL_CLUB_VERIFIED,
                "Arquivo de teste", DateTime.UnixEpoch, SourceSeason: year));
    }
}
