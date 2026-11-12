using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Nodes;
using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Domain;
using Elifoot.Infrastructure.Catalog;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Elifoot.Integration.Tests;

public sealed class OfflineCatalogTests
{
    private sealed class Files : IDisposable
    {
        private readonly DirectoryInfo folder = Directory.CreateTempSubdirectory("elifoot-offline-test-");
        public string Root => folder.FullName;
        public string YearRoot => Path.Combine(Root, "brasil", "1998");
        public string ClubFile(int i = 0) => Path.Combine(YearRoot, "clubes", $"c-{i:000}.json");
        public string ManifestFile => Path.Combine(YearRoot, "manifest.json");
        public OfflineCatalogLoader Loader => new(Root);
        public async Task Write(int count = 1, int players = 23)
        {
            Directory.CreateDirectory(Path.Combine(YearRoot, "clubes"));
            for (int i = 0; i < count; i++) await Save(ClubFile(i), Club(i, players));
            await Save(ManifestFile, new OfflineManifest("1.0", "br-1998", new("BR", "Brasil", "brasil"), 1998,
                "Catálogo sintético de teste", "READY", Enumerable.Range(0, count).Select(i => $"clubes/c-{i:000}.json").ToList()));
        }
        public async Task Edit(string file, Action<JsonObject> edit)
        {
            var json = JsonNode.Parse(await File.ReadAllTextAsync(file))!.AsObject(); edit(json);
            await File.WriteAllTextAsync(file, json.ToJsonString());
        }
        public static Task Save<T>(string path, T item) => File.WriteAllTextAsync(path, JsonSerializer.Serialize(item, OfflineCatalogLoader.Json));
        public void Dispose()
        {
            var expected = Path.GetFullPath(Path.GetTempPath());
            Assert.StartsWith(expected, folder.FullName, StringComparison.OrdinalIgnoreCase);
            Assert.StartsWith("elifoot-offline-test-", folder.Name);
            folder.Delete(recursive: true);
        }
    }
    private static OfflineClub Club(int index = 0, int count = 23)
    {
        string[] positions = ["G", "D", "D", "D", "D", "M", "M", "M", "M", "A", "A", "G", "D", "D", "D", "D", "M", "M", "M", "M", "A", "A", "A", "A"];
        return new("1.0", $"club-{index:000}", "BR", 1998, $"Clube sintético {index}", $"Curto {index}", $"C{index}",
            ["#123456", "#FFFFFF"], new($"Estádio sintético {index}", 17000),
            new($"coach-{index}", $"Treinador sintético {index}", "BR", new(["4-4-2", "4-3-3"], ["5-3-2", "4-5-1"])),
            Enumerable.Range(0, count).Select(i => new OfflinePlayer($"p-{index}-{i}", $"Atleta sintético {index}-{i}",
                positions[i], i == 0 ? 50 : i == 1 ? 1 : 35, "BR", i % 6, i == 10)).ToList(), "READY");
    }
    private static OfflineWorldRequest Request => new("Mundo sintético offline", ["br-1998"], 217, 2);
    private static List<EligibleCatalogClub> Eligible(OfflineCatalogReport report) => report.Catalogs
        .SelectMany(c => c.Clubs.Select(s => OfflineWorldFactory.Eligible(s, c.Manifest!.Gender))).ToList();

    [Fact]
    public async Task ValidFilesReadOfflineWithoutDatabasePreserveOptionalFieldsAndLiteralValues()
    {
        using var files = new Files(); await files.Write();
        var report = await files.Loader.LoadAsync();
        Assert.True(report.Valid); Assert.True(Assert.Single(report.Catalogs).Ready);
        var club = Assert.Single(report.Catalogs[0].Clubs).Definition;
        Assert.Null(club.CrestFile); Assert.Null(club.InitialDivision);
        Assert.Equal(50, club.Players![0].Strength); Assert.Equal(1, club.Players[1].Strength);
        Assert.True(club.Players[10].IsStar); Assert.False(club.Players[0].IsStar);
        Assert.Equal(Enumerable.Range(0, 6), club.Players.Take(6).Select(p => p.Behavior!.Value));
        Assert.Equal(["4-4-2", "4-3-3"], club.Coach!.PreferredTactics!.Home);
        Assert.Equal(["5-3-2", "4-5-1"], club.Coach.PreferredTactics.Away);
    }

    [Fact]
    public async Task IncompleteDraftIsEditableButNeverReadyAndNeverSilentlyFilled()
    {
        using var files = new Files(); await files.Write(players: 11);
        await files.Edit(files.ClubFile(), c => { c["status"] = "DRAFT"; c["players"] = new JsonArray(); c["coach"] = null; });
        await files.Edit(files.ManifestFile, c => c["status"] = "DRAFT");
        var report = await files.Loader.LoadAsync();
        Assert.True(report.Valid); Assert.False(report.Catalogs[0].Ready);
        Assert.Empty(report.Catalogs[0].Clubs[0].Definition.Players!);
        Assert.Contains(report.Catalogs[0].Issues, i => i.Code == "ROSTER" && !i.IsError);
    }

    [Theory]
    [InlineData("strength", "0", "STRENGTH")]
    [InlineData("strength", "51", "STRENGTH")]
    [InlineData("strength", "null", "STRENGTH")]
    [InlineData("strength", "\"25\"", "CLUB_FILE")]
    [InlineData("strength", "25.5", "CLUB_FILE")]
    [InlineData("behavior", "\"2\"", "CLUB_FILE")]
    [InlineData("isStar", "\"true\"", "CLUB_FILE")]
    [InlineData("position", "\"GK\"", "POSITION")]
    [InlineData("behavior", "-1", "BEHAVIOR")]
    [InlineData("behavior", "6", "BEHAVIOR")]
    [InlineData("nationality", "\"XX\"", "NATIONALITY")]
    [InlineData("nationality", "\"br\"", "NATIONALITY")]
    [InlineData("isStar", "null", "STAR")]
    [InlineData("id", "\"../id\"", "PLAYER_ID")]
    public async Task ReadyRejectsInvalidPlayerValues(string field, string value, string code)
    {
        using var files = new Files(); await files.Write();
        await files.Edit(files.ClubFile(), c => c["players"]![0]![field] = JsonNode.Parse(value));
        var catalog = Assert.Single((await files.Loader.LoadAsync()).Catalogs);
        Assert.False(catalog.Valid); Assert.False(catalog.Ready); Assert.Contains(catalog.Issues, i => i.Code == code);
    }

    [Theory]
    [InlineData("country", "\"XX\"", "COUNTRY")]
    [InlineData("year", "0", "YEAR")]
    [InlineData("year", "1999", "SCOPE")]
    [InlineData("schemaVersion", "\"2.0\"", "SCHEMA_VERSION")]
    [InlineData("coach", "null", "COACH")]
    [InlineData("initialDivision", "6", "DIVISION")]
    [InlineData("colors", "[\"red\",\"blue\"]", "COLORS")]
    public async Task InvalidClubAndScopeAreRejected(string field, string value, string code)
    {
        using var files = new Files(); await files.Write();
        await files.Edit(files.ClubFile(), c => c[field] = JsonNode.Parse(value));
        Assert.Contains((await files.Loader.LoadAsync()).Catalogs[0].Issues, i => i.Code == code && i.IsError);
    }

    [Theory]
    [InlineData("year", "\"1998\"")]
    [InlineData("gender", "0")]
    [InlineData("gender", "\"male\"")]
    public async Task ManifestTypesAndCategoryMustMatchTheVersionedSchema(string field, string value)
    {
        using var files = new Files(); await files.Write();
        await files.Edit(files.ManifestFile, c => c[field] = JsonNode.Parse(value));
        Assert.Contains((await files.Loader.LoadAsync()).Catalogs[0].Issues, i => i.Code == "MANIFEST_FILE" && i.IsError);
    }

    [Theory]
    [InlineData("home", "[\"F2\",\"F5\"]")]
    [InlineData("away", "[\"4-4-2\"]")]
    [InlineData("home", "[\"4-4-2\",\"4-4-2\"]")]
    [InlineData("away", "[\"4-4-2\",\"4-3-3\",\"5-3-2\"]")]
    [InlineData("home", "[\"9-9-9\",\"4-3-3\"]")]
    public async Task ExactlyTwoValidTacticsPerContext(string context, string list)
    {
        using var files = new Files(); await files.Write();
        await files.Edit(files.ClubFile(), c => c["coach"]!["preferredTactics"]![context] = JsonNode.Parse(list));
        Assert.Contains((await files.Loader.LoadAsync()).Catalogs[0].Issues, i => i.Code == "TACTICS" && i.IsError);
    }

    [Theory]
    [InlineData("id")][InlineData("name")][InlineData("nationality")]
    public async Task CoachMandatoryFieldsAreValidated(string field)
    {
        using var files = new Files(); await files.Write();
        await files.Edit(files.ClubFile(), c => c["coach"]![field] = null);
        Assert.Contains((await files.Loader.LoadAsync()).Catalogs[0].Issues, i => i.Code == "COACH" && i.IsError);
    }

    [Theory]
    [InlineData(10)][InlineData(24)]
    public async Task CatalogLimitIs11To23WithoutChangingServer24(int count)
    {
        using var files = new Files(); await files.Write(players: count);
        Assert.Contains((await files.Loader.LoadAsync()).Catalogs[0].Issues, i => i.Code == "ROSTER");
        Assert.Equal(24, SquadValidator.MaxSquadSize);
    }

    [Fact]
    public async Task DuplicateClubPlayerCoachAndReferenceIdsAreDetected()
    {
        using var files = new Files(); await files.Write(2);
        await files.Edit(files.ClubFile(1), c => { c["catalogClubId"] = "club-000"; c["players"]![0]!["id"] = "p-0-0"; c["coach"]!["id"] = "coach-0"; });
        var report = await files.Loader.LoadAsync();
        foreach (string code in new[] { "DUPLICATE_CLUB", "DUPLICATE_PLAYER", "DUPLICATE_COACH" })
            Assert.Contains(report.Catalogs[0].Issues, i => i.Code == code);
        await files.Edit(files.ManifestFile, c => c["clubs"]!.AsArray().Add("clubes/c-000.json"));
        Assert.Contains((await files.Loader.LoadAsync()).Catalogs[0].Issues, i => i.Code == "DUPLICATE_REFERENCE");
    }

    [Theory]
    [InlineData("../outside.json")][InlineData("/outside.json")][InlineData("C:/outside.json")]
    [InlineData("clubes/../../outside.json")][InlineData("clubes\\outside.json")]
    public async Task TraversalAndAbsolutePathsAreRejected(string path)
    {
        using var files = new Files(); await files.Write();
        await files.Edit(files.ManifestFile, c => c["clubs"] = new JsonArray(path));
        Assert.Contains((await files.Loader.LoadAsync()).Catalogs[0].Issues, i => i.Code == "CLUB_FILE");
        await files.Write(); await files.Edit(files.ClubFile(), c => c["crestFile"] = path);
        Assert.False((await files.Loader.LoadAsync()).Catalogs[0].Valid);
    }

    [Theory]
    [InlineData("{bad")]
    [InlineData("{\"schemaVersion\":\"1.0\",\"schemaVersion\":\"1.0\"}")]
    [InlineData("{\"unexpected\":1}")]
    [InlineData("null")]
    public async Task MalformedDuplicateJsonPropertiesAndUnknownFieldsAreRejected(string json)
    {
        using var files = new Files(); await files.Write();
        await File.WriteAllTextAsync(files.ClubFile(), json);
        Assert.Contains((await files.Loader.LoadAsync()).Catalogs[0].Issues, i => i.Code == "CLUB_FILE");
    }

    [Fact]
    public async Task MissingCountryYearManifestAndBrokenReferencesNeverBecomePlayable()
    {
        using var files = new Files(); await files.Write();
        await files.Edit(files.ManifestFile, c => { c["country"]!["code"] = "XX"; c["year"] = 2026; });
        var catalog = (await files.Loader.LoadAsync()).Catalogs[0];
        Assert.Contains(catalog.Issues, i => i.Code == "COUNTRY"); Assert.Contains(catalog.Issues, i => i.Code == "YEAR");
        await files.Write(); File.Delete(files.ClubFile());
        Assert.Contains((await files.Loader.LoadAsync()).Catalogs[0].Issues, i => i.Code == "CLUB_FILE");
        File.Delete(files.ManifestFile);
        Assert.Contains((await files.Loader.LoadAsync()).Catalogs[0].Issues, i => i.Code == "MANIFEST_FILE");
    }

    [Fact]
    public async Task AnyIsoCountryAndValidYearWorkWithoutPortugalSpainWhitelist()
    {
        using var files = new Files(); await files.Write();
        string japan = Path.Combine(files.Root, "japao", "2026"); Directory.CreateDirectory(Path.GetDirectoryName(japan)!);
        Directory.Move(files.YearRoot, japan);
        await files.Edit(Path.Combine(japan, "manifest.json"), c => { c["country"]!["code"] = "JP"; c["country"]!["name"] = "Japão"; c["country"]!["slug"] = "japao"; c["year"] = 2026; });
        await files.Edit(Path.Combine(japan, "clubes", "c-000.json"), c => { c["country"] = "JP"; c["year"] = 2026; });
        Assert.True((await files.Loader.LoadAsync()).Catalogs.Single().Ready);
    }

    [Fact]
    public async Task NewWorldCopiesExactRosterWithNewIdsAndKeepsSourceAndOldSnapshotsIndependent()
    {
        using var files = new Files(); await files.Write(128);
        var selection = Eligible(await files.Loader.LoadAsync());
        var preview = OfflineWorldFactory.Preview(Request, selection); Assert.Empty(preview.Errors);
        var sourceBefore = await File.ReadAllBytesAsync(files.ClubFile());
        var world = OfflineWorldFactory.Create(Request, preview);
        Assert.Equal(128, world.Clubs.Count); Assert.Equal(128 * 23, world.Players.Count); Assert.Empty(world.Auctions);
        Assert.Equal([8, 8, 8, 8, 96], Enumerable.Range(1, 5).Select(d => world.Clubs.Count(c => (int)c.DivisionTier == d)));
        Assert.Equal(21, world.TotalRoundsInSeason);
        var club = world.Clubs.Single(c => c.OfflineIdentity!.CatalogClubId == "club-000");
        var player = world.Players.Single(p => p.ClubId == club.Id && p.OfflineIdentity!.PlayerId == "p-0-0");
        Assert.Equal(50, player.Strength); Assert.Equal("BR", player.Nationality); Assert.Equal(PlayerBehavior.FairPlay, player.Behavior);
        Assert.False(player.IsStar); Assert.Equal("Curto 0", club.ShortName); Assert.Equal("C0", club.OfflineIdentity!.Abbreviation);
        Assert.Equal(17000, club.Stadium.BuiltCapacity); Assert.Equal("Estádio sintético 0", club.Stadium.Name);
        var second = OfflineWorldFactory.Create(Request, preview);
        Assert.Empty(second.Clubs.Select(c => c.Id).Intersect(world.Clubs.Select(c => c.Id)));
        Assert.Empty(second.Players.Select(p => p.Id).Intersect(world.Players.Select(p => p.Id)));
        Assert.Empty(second.AiManagers.Select(c => c.Id).Intersect(world.AiManagers.Select(c => c.Id)));
        await files.Edit(files.ClubFile(), c => { c["name"] = "Outra versão"; c["players"]![0]!["strength"] = 2; });
        Assert.Equal(50, player.Strength); Assert.Equal("Clube sintético 0", club.Name);
        selection[0].OfflineSource!.Definition.Players![0] = selection[0].OfflineSource!.Definition.Players![0] with { Name = "Mutação em memória" };
        Assert.Equal("Atleta sintético 0-0", world.OfflineCatalogOrigin!.Clubs[0].Club.OfflineSource!.Definition.Players![0].Name);
        string snapshot = JsonSerializer.Serialize(world, CatalogImportCodec.Json);
        var restored = JsonSerializer.Deserialize<GameWorld>(snapshot, CatalogImportCodec.Json)!;
        Assert.Equal(snapshot, JsonSerializer.Serialize(restored, CatalogImportCodec.Json));
        Assert.Equal(JsonSerializer.Serialize(world.AiManagers[0].OfflineIdentity), JsonSerializer.Serialize(restored.AiManagers[0].OfflineIdentity));
        Assert.NotEqual(sourceBefore, await File.ReadAllBytesAsync(files.ClubFile()));
    }

    [Fact]
    public async Task InitialDivisionsAndManualOverridesDoNotChangeProvidedStrengths()
    {
        using var files = new Files(); await files.Write(128);
        await files.Edit(files.ClubFile(127), c => c["initialDivision"] = 1);
        var selection = Eligible(await files.Loader.LoadAsync());
        var preview = OfflineWorldFactory.Preview(Request, selection);
        Assert.Empty(preview.Errors); Assert.Equal(1, preview.Clubs.Single(c => c.Club.ExternalId == "club-127").Division);
        var req = Request with { Assignments = [new(selection.Single(c => c.ExternalId == "club-127").Key, 5)] };
        var changed = OfflineWorldFactory.Preview(req, selection);
        Assert.Empty(changed.Errors); Assert.Equal(5, changed.Clubs.Single(c => c.Club.ExternalId == "club-127").Division);
        Assert.All(changed.Clubs, c => { Assert.Null(c.InitialSquadReference); Assert.Equal(50, c.Club.Players[0].Strength); });
        var bad = Request with { Assignments = selection.Take(9).Select(c => new ClubAssignment(c.Key, 1)).ToList() };
        Assert.NotEmpty(OfflineWorldFactory.Preview(bad, selection).Errors);
    }

    [Fact]
    public void ExistingSnapshotsHaveNoNewPropertiesAndKeepOriginalBytes()
    {
        var old = WorldSeedFactory.CreateSeededWorld("Save anterior", 1998, 64);
        string snapshot = JsonSerializer.Serialize(old, CatalogImportCodec.Json);
        Assert.DoesNotContain("offlineIdentity", snapshot); Assert.DoesNotContain("offlineCatalogOrigin", snapshot);
        var read = JsonSerializer.Deserialize<GameWorld>(snapshot, CatalogImportCodec.Json)!;
        Assert.Equal(snapshot, JsonSerializer.Serialize(read, CatalogImportCodec.Json));
        Assert.All(read.AiManagers, m => Assert.Null(m.OfflineIdentity));
    }

    [Fact]
    public void CatalogPreferencesRemainSoftAndFutureLearningFieldsHaveNoEffect()
    {
        var club = new Club { CashBalance = 10_000_000 };
        var manager = new AiManager
        {
            OfflineIdentity = new("demo", "coach", "BR", 1998, "BR", ["5-0-5", "4-2-4"], ["4-4-2", "4-3-3"])
        };
        var squad = Enumerable.Range(0, 16).Select(i => new Player
        {
            ClubId = club.Id, Strength = 35, Position = i == 0 ? PlayerPosition.G : i < 8 ? PlayerPosition.D : PlayerPosition.M
        }).ToList();
        var first = AiManagerBrain.PlanAndSelectLineup(manager, club, squad, null, null, true, 1998, 1, 1);
        Assert.DoesNotContain(first.Formation, manager.OfflineIdentity.OriginalHome);
        Assert.True(TacticalFormations.TryGet(first.Formation, true, out _));
        manager.OfflineIdentity = manager.OfflineIdentity with { LearnedHome = "5-0-5", LearnedAway = "3-4-3" };
        var second = AiManagerBrain.PlanAndSelectLineup(manager, club, squad, null, null, true, 1998, 1, 1);
        Assert.Equal(first.Formation, second.Formation);
        Assert.Equal(["5-0-5", "4-2-4"], manager.OfflineIdentity.OriginalHome);
        var restored = JsonSerializer.Deserialize<AiManager>(JsonSerializer.Serialize(manager))!;
        Assert.Equal("5-0-5", restored.OfflineIdentity!.LearnedHome);
        Assert.Equal(["4-4-2", "4-3-3"], restored.OfflineIdentity.OriginalAway);
    }

    [Theory]
    [InlineData(false)][InlineData(true)]
    public async Task CatalogPlayersStillTransferUnderLegacyAndClassicalEconomy(bool classical)
    {
        using var files = new Files(); await files.Write(128, 18);
        var preview = OfflineWorldFactory.Preview(Request, Eligible(await files.Loader.LoadAsync()));
        var world = OfflineWorldFactory.Create(Request, preview);
        Assert.False(ClassicPlayerEconomyEngine.IsClassicalSalaryEnabled(world));
        if (classical) ClassicPlayerEconomyEngine.EnableClassicalEconomy(world, recalculateInitialPlayerValues: true);
        var seller = world.Clubs[0]; var buyer = world.Clubs[1]; buyer.CashBalance = 100_000_000;
        var player = world.Players.Last(p => p.ClubId == seller.Id);
        string identity = JsonSerializer.Serialize(player.OfflineIdentity); int force = player.Strength;
        decimal before = seller.CashBalance + buyer.CashBalance;
        var result = MarketAndEconomyEngine.ExecuteTransfer(world, buyer.Id, seller.Id, player.Id, 100_000m, "offline test", "offline-transfer");
        Assert.True(result.Success, result.ErrorMessage); Assert.Equal(buyer.Id, player.ClubId);
        Assert.Equal(before, seller.CashBalance + buyer.CashBalance);
        Assert.Equal(identity, JsonSerializer.Serialize(player.OfflineIdentity)); Assert.Equal(force, player.Strength);
        Assert.Equal(ClassicalNationalityClass.SameCountry, ClassicPlayerEconomyEngine.ClassifyNationality(player.Nationality, buyer.Country));
    }

    [Fact]
    public async Task CoachKeepsIdentityCareerAndOriginalPreferencesWhenHiredByAnotherClub()
    {
        using var files = new Files(); await files.Write(128);
        var preview = OfflineWorldFactory.Preview(Request, Eligible(await files.Loader.LoadAsync()));
        var world = OfflineWorldFactory.Create(Request, preview);
        foreach (var a in world.AiManagers) a.Career.Morale = 0;
        var source = world.Clubs[31]; var target = world.Clubs[24];
        var coach = world.AiManagers.Single(a => a.Id == source.AiManagerId);
        coach.Career.Morale = 80; coach.CoachPoints = 12.5m; coach.LeagueTitles = 2;
        target.CashBalance = 30_000_000; target.Reputation = 98; target.Stadium.BuiltCapacity = 80000;
        source.CashBalance = 100_000; source.Reputation = 18; source.Stadium.BuiltCapacity = 5000;
        string identity = JsonSerializer.Serialize(coach.OfflineIdentity); var id = coach.Id;
        CoachCareerEngine.OpenVacancy(world, target); CoachCareerEngine.ProcessVacancies(world);
        Assert.Equal(target.Id, coach.CurrentClubId); Assert.Equal(id, target.AiManagerId);
        Assert.NotEqual(id, source.AiManagerId); Assert.Equal(12.5m, coach.CoachPoints); Assert.Equal(2, coach.LeagueTitles);
        Assert.Equal(identity, JsonSerializer.Serialize(coach.OfflineIdentity)); Assert.Equal(2, coach.EmploymentHistory.Count);
        var restored = JsonSerializer.Deserialize<GameWorld>(JsonSerializer.Serialize(world, CatalogImportCodec.Json), CatalogImportCodec.Json)!;
        Assert.Equal(identity, JsonSerializer.Serialize(restored.AiManagers.Single(a => a.Id == id).OfflineIdentity));
    }

    [Fact]
    public async Task CrestIsValidatedAndFrozenWithoutExternalOrLiveFileReferences()
    {
        using var files = new Files(); await files.Write();
        string path = Path.Combine(files.YearRoot, "escudo.png");
        await File.WriteAllBytesAsync(path, Convert.FromBase64String("iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+/lU8AAAAASUVORK5CYII="));
        await files.Edit(files.ClubFile(), c => c["crestFile"] = "escudo.png");
        var catalog = (await files.Loader.LoadAsync()).Catalogs[0]; Assert.True(catalog.Ready);
        string frozen = Assert.Single(catalog.Clubs).CrestDataUrl!; File.Delete(path);
        Assert.StartsWith("data:image/png;base64,", frozen);
        Assert.False((await files.Loader.LoadAsync()).Catalogs[0].Ready);
        Assert.Equal(frozen, catalog.Clubs[0].CrestDataUrl);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NewCatalogApiIsAuthenticatedFrozenIdempotentAndRejectsWrongConfirmationPaths(bool postgres)
    {
        using var files = new Files(); await files.Write(128);
        string? catalogConnection = null, worldConnection = null;
        if (postgres)
        {
            var settings = new Npgsql.NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("ELIFOOT_LOCAL_CATALOG_TEST_PG")
                ?? throw new InvalidOperationException("Configure o PostgreSQL local isolado para a integração offline."));
            Assert.Equal("127.0.0.1", settings.Host); Assert.InRange(settings.Port, 55433, 55442);
            string suffix = Guid.NewGuid().ToString("N");
            settings.Database = "elifoot_offline_013a_catalog_" + suffix; catalogConnection = settings.ConnectionString;
            settings.Database = "elifoot_offline_013a_world_" + suffix; worldConnection = settings.ConnectionString;
        }
        await using var app = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("Elifoot:AuthMode", "LocalDev"); b.UseSetting("Elifoot:DatabaseProvider", postgres ? "PostgreSQL" : "InMemory");
            if (worldConnection != null) b.UseSetting("ConnectionStrings:DefaultConnection", worldConnection);
            b.UseSetting("Elifoot:AutoSeedDefaultWorld", "false"); b.UseSetting("Elifoot:DisableBackgroundClock", "true");
            b.UseSetting("Catalog013A:Enabled", "true"); b.UseSetting("Catalog013A:DatabaseProvider", postgres ? "PostgreSQL" : "Sqlite");
            b.UseSetting("Catalog016B:Enabled", "true");
            b.UseSetting("ConnectionStrings:Catalog013A", catalogConnection ?? $"Data Source={Path.Combine(files.Root, "test.db")};Pooling=False");
            b.UseSetting("CatalogOffline:Root", files.Root);
        });
        using var client = app.CreateClient(); const string route = "/api/world-creator/offline";
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync(route + "/catalogs")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", $"localdev-{Guid.NewGuid():N}");
        var report = (await client.GetFromJsonAsync<OfflineCatalogReport>(route + "/catalogs"))!;
        Assert.True(report.Catalogs.Single().Ready);
        var invalid = await client.PostAsJsonAsync(route + "/clubs", Request with { CatalogIds = ["missing-country-year"] });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var response = await client.PostAsJsonAsync(route + "/preview", Request); response.EnsureSuccessStatusCode();
        var preview = (await response.Content.ReadFromJsonAsync<WorldCreatorPreview>())!; Assert.Empty(preview.Errors);
        await files.Edit(files.ClubFile(), c => { c["players"]![0]!["strength"] = 2; c["coach"]!["name"] = "Mudou depois da prévia"; });
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync($"/api/world-creator/confirm/{preview.DraftId}", null)).StatusCode);
        using var stranger = app.CreateClient(); stranger.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", $"localdev-{Guid.NewGuid():N}");
        Assert.Equal(HttpStatusCode.NotFound, (await stranger.PostAsync($"{route}/confirm/{preview.DraftId}", null)).StatusCode);
        var confirms = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ => client.PostAsync($"{route}/confirm/{preview.DraftId}", null)));
        foreach (var result in confirms) result.EnsureSuccessStatusCode();
        var ids = await Task.WhenAll(confirms.Select(async r => JsonNode.Parse(await r.Content.ReadAsStringAsync())!["id"]!.GetValue<Guid>()));
        Assert.Equal(ids[0], ids[1]);
        var world = (await app.Services.GetRequiredService<IWorldRepository>().GetWorldAsync(ids[0]))!;
        Assert.Equal(50, world.Players.Single(p => p.OfflineIdentity!.PlayerId == "p-0-0").Strength);
        Assert.Equal("Treinador sintético 0", world.AiManagers.Single(c => c.OfflineIdentity!.CoachId == "coach-0").Name);
        Assert.NotNull(world.InitialLobby);
        (await client.GetAsync($"/api/world-lobby-016b/{world.Id}")).EnsureSuccessStatusCode();
        await files.Edit(files.ManifestFile, c => c["status"] = "DRAFT");
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync(route + "/preview", Request)).StatusCode);
    }
}
