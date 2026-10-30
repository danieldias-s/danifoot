using System.Diagnostics;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using Elifoot.Api;
using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Domain;
using Elifoot.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Elifoot.Integration.Tests;

[CollectionDefinition("Player rankings 009 SQLite", DisableParallelization = true)]
public sealed class PlayerRankings009Collection;

[Collection("Player rankings 009 SQLite")]
public sealed class PlayerRankings009IntegrationTests(ITestOutputHelper output)
{
    private static readonly string Root = FindRoot();
    private static readonly string Audit = Path.Combine(Root, "docs", "audit-009");
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task HttpFiltersCoverAllCategoriesScopesAndPeriodsUsingPersistedStatistics()
    {
        var world = FilterWorld();
        await using var host = await ApiHost.StartAsync(NewDatabase("filters"));
        await host.Repository.SaveWorldAsync(world);
        var scopes = new[] { "general", "division1", "division2", "division3", "division4", "cup" };
        foreach (var category in new[] { "scorers", "goalkeepers", "defenders" })
        foreach (var scope in scopes)
        foreach (var period in new[] { "career", "current", "season" })
        {
            string query = $"category={category}&scope={scope}&period={period}" + (period == "season" ? "&season=1" : "");
            var dto = await host.Client.GetFromJsonAsync<PlayerRankingsDto>($"/api/worlds/{world.Id}/player-rankings?{query}");
            Assert.NotNull(dto);
            Assert.Equal(category, dto.Category);
            Assert.Equal(scope, dto.Scope);
            Assert.Equal(period, dto.Period);
            Assert.Equal(period == "career" ? (int?)null : period == "current" ? 2 : 1, dto.Season);
            Assert.Equal(2, dto.CurrentSeason);
            var row = Assert.Single(dto.Rows);
            int[] buckets = scope == "general" ? [0, 1, 2, 3, 4, 5] : scope == "cup" ? [4, 5] : [int.Parse(scope[^1..]) - 1];
            int[] seasons = period == "career" ? [1, 2] : period == "current" ? [2] : [1];
            Assert.Equal(buckets.Length * seasons.Length * 20, row.MatchesPlayed);
            Assert.Equal(1, row.Rank);
            Assert.Equal(world.Clubs[1].Id, row.CurrentClubId);
            Assert.Empty(dto.NotEligible);
            if (category == "scorers")
                Assert.Equal(seasons.Sum(s => buckets.Sum(i => s * 10 + i)), row.GoalsScored);
            else
                Assert.Equal(20, dto.MinimumMatches);
        }
        var defaults = await host.Client.GetFromJsonAsync<PlayerRankingsDto>($"/api/worlds/{world.Id}/player-rankings");
        Assert.Equal(210, Assert.Single(defaults!.Rows).GoalsScored);
        var normalized = await host.Client.GetFromJsonAsync<PlayerRankingsDto>($"/api/worlds/{world.Id}/player-rankings?category=SCORERS&scope=%20CUP%20&period=CURRENT");
        Assert.Equal(49, Assert.Single(normalized!.Rows).GoalsScored);
    }

    [Fact]
    public async Task HttpRejectsInvalidOrAmbiguousQueriesAndReturns404ForMissingWorldOrPlayer()
    {
        await using var host = await ApiHost.StartAsync(NewDatabase("validation"));
        var world = FilterWorld();
        await host.Repository.SaveWorldAsync(world);
        foreach (var query in new[] {
            "category=coaches", "category=", "category=scorers&category=defenders",
            "scope=division0", "scope=division5", "scope=", "scope=general&scope=cup",
            "period=all", "period=", "period=current&period=season", "period=season",
            "period=season&season=0", "period=season&season=-1", "period=season&season=3",
            "period=season&season=abc", "period=season&season=1.5", "period=season&season=2147483648",
            "period=season&season=", "period=season&season=1&season=2", "period=current&season=1", "season=1" })
        {
            using var response = await host.Client.GetAsync($"/api/worlds/{world.Id}/player-rankings?{query}");
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            using var error = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.False(string.IsNullOrWhiteSpace(error.RootElement.GetProperty("error").GetString()));
        }
        foreach (var season in new[] { "0", "-1", "3", "abc", "", "1&season=2" })
        {
            using var response = await host.Client.GetAsync($"/api/worlds/{world.Id}/season-top-scorers?season={season}");
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        foreach (var path in new[] { "player-rankings", $"players/{Guid.NewGuid()}/history", "season-top-scorers" })
        {
            using var response = await host.Client.GetAsync($"/api/worlds/{Guid.NewGuid()}/{path}");
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        }
        using var missing = await host.Client.GetAsync($"/api/worlds/{world.Id}/players/{Guid.NewGuid()}/history");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task HttpHistoryPreservesHistoricalClubsAndGetRequestsDoNotPayAwardsOrWriteSnapshots()
    {
        var world = FilterWorld();
        await using var host = await ApiHost.StartAsync(NewDatabase("history"));
        await host.Repository.SaveWorldAsync(world);
        var playerId = world.PlayerStatistics.First(s => s.GoalsScored > 0).PlayerId;
        var before = await host.SnapshotAsync(world.Id);
        var history = await host.Client.GetFromJsonAsync<PlayerHistoryDto>($"/api/worlds/{world.Id}/players/{playerId}/history");
        Assert.NotNull(history);
        Assert.Equal(world.Clubs[1].Id, history.Player.CurrentClubId);
        Assert.Equal(210, history.Career.GoalsScored);
        Assert.Equal(12, history.Statistics.Count);
        Assert.All(history.Statistics, s => Assert.Equal(world.Clubs[0].Id, s.ClubId));
        Assert.Empty(history.Awards);
        for (int i = 0; i < 3; i++)
        {
            var awards = await host.Client.GetFromJsonAsync<SeasonTopScorerAwardsDto>($"/api/worlds/{world.Id}/season-top-scorers?season=2");
            Assert.Empty(awards!.Awards);
        }
        Assert.Empty(world.SeasonTopScorerAwards);
        Assert.Empty(world.Transactions);
        Assert.Equal(before, await host.SnapshotAsync(world.Id));
    }

    [Theory]
    [InlineData("same")]
    [InlineData("different")]
    [InlineData("unattached")]
    public async Task SqliteReloadPreservesExactAwardsTitlesLedgerAndMatchIdempotency(string receiver)
    {
        string database = NewDatabase($"awards-{receiver}");
        var (world, first, second, match) = AwardWorld();
        var originalClub = world.Clubs.Single(c => c.Id == first.ClubId);
        var otherClub = world.Clubs.First(c => c.Id != originalClub.Id);
        second.ClubId = receiver == "same" ? originalClub.Id : receiver == "different" ? otherClub.Id : null;
        decimal originalBalance = originalClub.CashBalance, otherBalance = otherClub.CashBalance;
        var coaches = JsonSerializer.Serialize(CompetitionAndSeasonEngine.GetClassicCoachRanking(world), Json);
        var awards = PlayerStatisticsEngine.AwardSeason(world, world.SeasonNumber);
        Assert.Equal(2, awards.Count);
        Assert.All(awards, a => { Assert.Equal(2, a.TotalGoals); Assert.Equal(1, a.TotalMatches); });
        Assert.Equal(originalBalance + (receiver == "same" ? 6_000_000m : 3_000_000m), originalClub.CashBalance);
        Assert.Equal(otherBalance + (receiver == "different" ? 3_000_000m : 0m), otherClub.CashBalance);
        Assert.Equal(receiver == "unattached" ? 0m : 3_000_000m, awards.Single(a => a.PlayerId == second.Id).AwardAmount);
        Assert.All(awards.Where(a => a.ClubId != null), a => Assert.Equal(3_000_000m, a.AwardAmount));
        var awardSignature = JsonSerializer.Serialize(awards.OrderBy(a => a.PlayerId), Json);
        int ledgerCount = receiver == "unattached" ? 1 : 2;
        Assert.Equal(ledgerCount, world.Transactions.Count);
        PlayerStatisticsEngine.AwardSeason(world, world.SeasonNumber);
        PlayerStatisticsEngine.ApplyFinishedMatches(world, [match, match]);
        Assert.Equal(ledgerCount, world.Transactions.Count);
        await using (var firstHost = await ApiHost.StartAsync(database))
        {
            await firstHost.Repository.SaveWorldAsync(world);
            await firstHost.AssertLedgerAsync(world.Id, ledgerCount, receiver == "unattached" ? 3_000_000m : 6_000_000m);
        }
        EfWorldRepository.ClearCacheForTesting();
        await using var restarted = await ApiHost.StartAsync(database);
        var loaded = (await restarted.Repository.GetWorldAsync(world.Id))!;
        Assert.NotSame(world, loaded);
        Assert.Contains(match.Id, loaded.AppliedPlayerStatisticsMatches);
        Assert.Contains(1, loaded.ClosedPlayerAwardSeasons);
        PlayerStatisticsEngine.ApplyFinishedMatches(loaded, loaded.Fixtures.Concat(loaded.Fixtures));
        PlayerStatisticsEngine.AwardSeason(loaded, 1);
        PlayerStatisticsEngine.EnsureMigrated(loaded);
        Assert.Equal(awardSignature, JsonSerializer.Serialize(loaded.SeasonTopScorerAwards.OrderBy(a => a.PlayerId), Json));
        Assert.Equal(coaches, JsonSerializer.Serialize(CompetitionAndSeasonEngine.GetClassicCoachRanking(loaded), Json));
        Assert.Equal(1, loaded.Players.Single(p => p.Id == first.Id).SeasonTopScorerTitles);
        Assert.Equal(1, loaded.Players.Single(p => p.Id == second.Id).SeasonTopScorerTitles);
        Assert.Equal(ledgerCount, loaded.Transactions.Count);
        Assert.All(loaded.Transactions, t => Assert.Equal(3_000_000m, t.Amount));
        var rankings = await restarted.Client.GetFromJsonAsync<PlayerRankingsDto>($"/api/worlds/{world.Id}/player-rankings");
        Assert.Equal(4, rankings!.Rows.Sum(r => r.GoalsScored));
        var response = await restarted.Client.GetFromJsonAsync<SeasonTopScorerAwardsDto>($"/api/worlds/{world.Id}/season-top-scorers?season=1");
        Assert.Equal(awardSignature, JsonSerializer.Serialize(response!.Awards.OrderBy(a => a.PlayerId), Json));
        Assert.Equal(receiver == "unattached" ? 3_000_000m : 6_000_000m, response.ClubTotals.Sum(c => c.Amount));
        var history = await restarted.Client.GetFromJsonAsync<PlayerHistoryDto>($"/api/worlds/{world.Id}/players/{second.Id}/history");
        Assert.Single(history!.Awards);
        Assert.Equal(1, history.Player.SeasonTopScorerTitles);
        await restarted.Repository.SaveWorldAsync(loaded);
        await restarted.Repository.SaveWorldAsync(loaded);
        await restarted.AssertLedgerAsync(world.Id, ledgerCount, receiver == "unattached" ? 3_000_000m : 6_000_000m);
        using var snapshot = JsonDocument.Parse(await restarted.SnapshotAsync(world.Id));
        Assert.False(snapshot.RootElement.TryGetProperty("playerStatisticsIndex", out _));
        Assert.Single(snapshot.RootElement.GetProperty("appliedPlayerStatisticsMatches").EnumerateArray());
        Assert.Equal(2, snapshot.RootElement.GetProperty("seasonTopScorerAwards").GetArrayLength());
    }

    [Fact]
    public async Task Existing008DatabaseMigratesFromConsistentReadOnlyBackupWithoutChangingCareerOrLedger()
    {
        using var fixture = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(Root, "docs", "audit-008", "browser-used-fixture.json")));
        string source = fixture.RootElement.GetProperty("DatabasePath").GetString()!;
        string database = NewDatabase("legacy008-copy");
        await using (var original = new SqliteConnection(new SqliteConnectionStringBuilder { DataSource = source, Mode = SqliteOpenMode.ReadOnly }.ToString()))
        await using (var copy = new SqliteConnection($"Data Source={database}"))
        {
            await original.OpenAsync();
            await copy.OpenAsync();
            original.BackupDatabase(copy);
        }
        string initial;
        Guid id;
        int ledgerCount;
        await using (var connection = new SqliteConnection($"Data Source={database}"))
        {
            await connection.OpenAsync();
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT SnapshotJson FROM ef_worlds LIMIT 1";
            initial = (string)(await command.ExecuteScalarAsync())!;
            var legacy = JsonSerializer.Deserialize<GameWorld>(initial, Json)!;
            id = legacy.Id;
            Assert.Equal(8, legacy.CareerRulesVersion);
            Assert.Equal(0, legacy.PlayerStatisticsVersion);
            command.CommandText = "SELECT COUNT(*) FROM ef_financial_ledger";
            ledgerCount = Convert.ToInt32(await command.ExecuteScalarAsync());
        }
        var expected = JsonSerializer.Deserialize<GameWorld>(initial, Json)!;
        var coaches = JsonSerializer.Serialize(CompetitionAndSeasonEngine.GetClassicCoachRanking(expected), Json);
        var financials = JsonSerializer.Serialize(expected.Transactions, Json);
        var cash = expected.Clubs.ToDictionary(c => c.Id, c => c.CashBalance);
        EfWorldRepository.ClearCacheForTesting();
        await using var host = await ApiHost.StartAsync(database);
        var loaded = (await host.Repository.GetWorldAsync(id))!;
        Assert.Equal(9, loaded.PlayerStatisticsVersion);
        Assert.Equal(8, loaded.CareerRulesVersion);
        Assert.Equal(coaches, JsonSerializer.Serialize(CompetitionAndSeasonEngine.GetClassicCoachRanking(loaded), Json));
        Assert.Equal(financials, JsonSerializer.Serialize(loaded.Transactions, Json));
        Assert.All(loaded.Clubs, c => Assert.Equal(cash[c.Id], c.CashBalance));
        var signature = JsonSerializer.Serialize(loaded.PlayerStatistics, Json);
        PlayerStatisticsEngine.EnsureMigrated(loaded);
        Assert.Equal(signature, JsonSerializer.Serialize(loaded.PlayerStatistics, Json));
        Assert.Empty(loaded.SeasonTopScorerAwards);
        Assert.NotEmpty(loaded.PlayerStatisticsCoverageWarnings);
        // Exercise every EF load path without writing to the original save.
        EfWorldRepository.ClearCacheForTesting();
        Assert.Equal(9, (await host.Repository.GetWorldByInviteCodeAsync(loaded.InviteCode))!.PlayerStatisticsVersion);
        EfWorldRepository.ClearCacheForTesting();
        Assert.All(await host.Repository.ListWorldsAsync(), w => Assert.Equal(9, w.PlayerStatisticsVersion));
        using var scope = host.App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ElifootDbContext>();
        Assert.Equal(ledgerCount, await db.FinancialLedger.CountAsync());
        Assert.Equal(initial, (await db.Worlds.SingleAsync(w => w.Id == id)).SnapshotJson);
    }

    [Fact]
    public async Task QueryServiceSharesTheClockLockAndReleasesItAfterCancellationOrFailure()
    {
        var world = FilterWorld();
        await using var host = await ApiHost.StartAsync(NewDatabase("lock"));
        await host.Repository.SaveWorldAsync(world);
        var service = host.App.Services.GetRequiredService<PlayerRankingsApplicationService>();
        // Reflection is confined to the test to inspect the existing internal clock lock.
        var method = typeof(MultiplayerRoundOrchestrator).GetMethod("GetLock", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!;
        var gate = (SemaphoreSlim)method.Invoke(null, [world.Id])!;
        await gate.WaitAsync();
        try
        {
            using var cancellation = new CancellationTokenSource();
            var waiting = service.ReadWorldAsync(world.Id, _ => "read", cancellation.Token);
            Assert.False(waiting.IsCompleted);
            cancellation.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting);
            Assert.Equal(0, gate.CurrentCount);
        }
        finally { gate.Release(); }
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.ReadWorldAsync<string>(world.Id, _ => throw new InvalidOperationException("test")));
        Assert.Equal("released", await service.ReadWorldAsync(world.Id, _ => "released"));
        Assert.Null(await service.ReadWorldAsync(Guid.NewGuid(), _ => "missing"));
    }

    [Theory]
    [InlineData(128)]
    [InlineData(1024)]
    public async Task HttpRankingPerformanceIsMeasuredAfterColdSqliteReload(int clubs)
    {
        string database = NewDatabase($"performance-{clubs}");
        var world = FilterWorld(clubs);
        // One aggregate for every player makes the 1024-club query representative of world size.
        var recorded = world.PlayerStatistics.Select(s => s.PlayerId).ToHashSet();
        var divisions = world.Clubs.ToDictionary(c => c.Id, c => c.DivisionTier);
        foreach (var p in world.Players.Where(p => !recorded.Contains(p.Id)))
            world.PlayerStatistics.Add(new() { PlayerId = p.Id, ClubId = p.ClubId!.Value, Position = p.Position,
                Season = 2, CompetitionType = CompetitionType.League, Division = divisions[p.ClubId.Value],
                MatchesPlayed = 20, GoalsScored = p.Position == PlayerPosition.A ? 1 : 0, MinutesPlayed = 1800 });
        await using (var writer = await ApiHost.StartAsync(database)) await writer.Repository.SaveWorldAsync(world);
        EfWorldRepository.ClearCacheForTesting();
        await using var host = await ApiHost.StartAsync(database);
        var timer = Stopwatch.StartNew();
        var cold = await host.Client.GetFromJsonAsync<PlayerRankingsDto>($"/api/worlds/{world.Id}/player-rankings");
        long coldMs = timer.ElapsedMilliseconds;
        Assert.NotEmpty(cold!.Rows);
        timer.Restart();
        foreach (var category in new[] { "scorers", "goalkeepers", "defenders" })
        {
            var dto = await host.Client.GetFromJsonAsync<PlayerRankingsDto>($"/api/worlds/{world.Id}/player-rankings?category={category}");
            Assert.NotEmpty(dto!.Rows);
        }
        long warmMs = timer.ElapsedMilliseconds;
        output.WriteLine($"{clubs} clubs, {world.Players.Count} players: cold={coldMs}ms, three warm categories={warmMs}ms");
        await File.WriteAllTextAsync(Path.Combine(Audit, $"api-performance-{clubs}.json"), JsonSerializer.Serialize(new {
            Clubs = clubs, Players = world.Players.Count, Statistics = world.PlayerStatistics.Count,
            ColdHttpMilliseconds = coldMs, ThreeWarmCategoriesMilliseconds = warmMs,
            Note = "Synthetic aggregates for HTTP sizing; not a real-season sporting result. No product performance SLA was specified." }, Json));
        Assert.True(coldMs < 30_000 && warmMs < 30_000, "HTTP query exceeded the 30-second integration smoke budget.");
    }

    [Fact]
    public async Task RealSeasonArtifactExportsQaSqliteAndProductionRoutesSurviveReload()
    {
        string sourcePath = Path.Combine(Audit, "real-season-world.json");
        string manifestPath = await CreateRealSeasonQaFixtureAsync(sourcePath);
        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(manifestPath));
        string database = manifest.RootElement.GetProperty("databasePath").GetString()!;
        var source = JsonSerializer.Deserialize<GameWorld>(await File.ReadAllBytesAsync(sourcePath), Json)!;
        EfWorldRepository.ClearCacheForTesting();
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder => {
            builder.UseSetting("Elifoot:DatabaseProvider", "Sqlite");
            builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={database}");
            builder.UseSetting("Elifoot:AutoSeedDefaultWorld", "false");
            builder.UseSetting("Elifoot:DisableBackgroundClock", "true");
        });
        using var client = factory.CreateClient();
        var awards = await client.GetFromJsonAsync<SeasonTopScorerAwardsDto>($"/api/worlds/{source.Id}/season-top-scorers?season=1");
        Assert.Equal(2, awards!.Awards.Count);
        Assert.All(awards.Awards, a => { Assert.Equal(10, a.TotalGoals); Assert.Equal(3_000_000m, a.AwardAmount); });
        Assert.Equal(6_000_000m, awards.ClubTotals.Sum(c => c.Amount));
        Assert.Empty(awards.CoverageWarnings);
        var career = await client.GetFromJsonAsync<PlayerRankingsDto>($"/api/worlds/{source.Id}/player-rankings");
        Assert.Equal(10, career!.Rows[0].GoalsScored);
        Assert.Equal(2, career.Rows.Count(r => r.GoalsScored == 10));
        var legacy = await client.GetFromJsonAsync<List<TopScorerDto>>($"/api/worlds/{source.Id}/top-scorers");
        Assert.Empty(legacy!); // The artifact already advanced to T2; no current-season goals exist yet.
        var repository = factory.Services.GetRequiredService<IWorldRepository>();
        var restored = (await repository.GetWorldAsync(source.Id))!;
        Assert.Equal(JsonSerializer.Serialize(source.SeasonTopScorerAwards.OrderBy(a => a.PlayerId), Json),
            JsonSerializer.Serialize(restored.SeasonTopScorerAwards.OrderBy(a => a.PlayerId), Json));
        var before = restored.Clubs.ToDictionary(c => c.Id, c => c.CashBalance);
        PlayerStatisticsEngine.AwardSeason(restored, 1);
        Assert.All(restored.Clubs, c => Assert.Equal(before[c.Id], c.CashBalance));
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ElifootDbContext>();
        var ledger = await db.FinancialLedger.Where(t => t.GameWorldId == source.Id && t.IdempotencyKey.StartsWith("season_top_scorer:")).ToListAsync();
        Assert.Equal(2, ledger.Count);
        Assert.All(ledger, t => Assert.Equal(3_000_000m, t.Amount));
        Assert.Equal(source.Transactions.Count, await db.FinancialLedger.CountAsync(t => t.GameWorldId == source.Id));
        output.WriteLine($"Real-season browser fixture: {manifestPath}; SQLite: {database}");
    }

    // Invoked separately only after the coordinator's complete real-season artifact exists.
    public static async Task<string> CreateRealSeasonQaFixtureAsync(string sourcePath)
    {
        byte[] source = await File.ReadAllBytesAsync(sourcePath);
        var world = JsonSerializer.Deserialize<GameWorld>(source, Json) ?? throw new InvalidDataException("World JSON is empty.");
        if (world.SeasonNumber < 2 || world.PlayerStatisticsVersion != 9 || world.PlayerStatistics.Count == 0 || world.SeasonTopScorerAwards.Count == 0)
            throw new InvalidDataException("Expected the real completed 009 season, advanced to season 2 with statistics and awards.");
        string directory = Path.Combine(Root, "artifacts", "player-rankings-009");
        Directory.CreateDirectory(directory);
        string database = Path.Combine(directory, $"browser-{Guid.NewGuid():N}.db");
        await using var host = await ApiHost.StartAsync(database);
        await host.Repository.SaveWorldAsync(world);
        var ranking = PlayerStatisticsEngine.GetRankings(world);
        var awards = PlayerStatisticsEngine.GetSeasonAwards(world);
        string manifest = Path.Combine(Audit, "browser-fixture.json");
        await File.WriteAllTextAsync(manifest, JsonSerializer.Serialize(new {
            SourcePath = Path.GetFullPath(sourcePath), SourceSha256 = Convert.ToHexString(SHA256.HashData(source)),
            DatabasePath = database, WorldId = world.Id, world.InviteCode, world.SeasonNumber,
            Clubs = world.Clubs.Count, Players = world.Players.Count, Statistics = world.PlayerStatistics.Count,
            Awards = awards, TopScorers = ranking.Rows.Take(10).ToList(),
            Nickname = "Rankings 009", DeviceId = "qa009-web",
            Label = "Imported real complete season. No sporting numbers or login identity were injected; join unemployed via HTTP afterwards." },
            new JsonSerializerOptions(Json) { WriteIndented = true }));
        return manifest;
    }

    private static GameWorld FilterWorld(int clubs = 128)
    {
        var world = WorldSeedFactory.CreateSeededWorld("009 HTTP synthetic filter fixture", totalClubs: clubs, inviteCode: $"QA009{Guid.NewGuid():N}");
        world.SeasonNumber = 2;
        world.PlayerStatisticsVersion = 9;
        world.PlayerStatistics.Clear();
        world.PlayerStatisticsIndex.Clear();
        world.Transactions.Clear();
        foreach (var position in new[] { PlayerPosition.A, PlayerPosition.G, PlayerPosition.D })
        {
            var p = world.Players.First(p => p.Position == position);
            p.ClubId = world.Clubs[1].Id; // Current club differs from every recorded historic club.
            for (int season = 1; season <= 2; season++)
            for (int bucket = 0; bucket < 6; bucket++)
                world.PlayerStatistics.Add(new() { PlayerId = p.Id, ClubId = world.Clubs[0].Id, Position = p.Position,
                    Season = season, CompetitionType = bucket < 4 ? CompetitionType.League : bucket == 4 ? CompetitionType.CupPreliminary1 : CompetitionType.CupFinal,
                    Division = bucket < 4 ? (DivisionTier)(bucket + 1) : null, MatchesPlayed = 20,
                    GoalsScored = position == PlayerPosition.A ? season * 10 + bucket : 0,
                    GoalsConceded = position == PlayerPosition.G ? bucket + season : 0,
                    YellowCards = position == PlayerPosition.D ? 4 : 0, RedCards = position == PlayerPosition.D ? 1 : 0, MinutesPlayed = 1800 });
        }
        return world;
    }

    private static (GameWorld, Player, Player, MatchFixture) AwardWorld()
    {
        var world = WorldSeedFactory.CreateSeededWorld("009 synthetic official-event award fixture", inviteCode: $"QA009{Guid.NewGuid():N}");
        world.PlayerStatisticsVersion = 9;
        world.RoundNumber = world.TotalRoundsInSeason;
        world.RoundState = RoundState.RESULTS;
        world.Fixtures.Clear();
        List<Guid> Starters(Guid club) => world.Players.Where(p => p.ClubId == club && p.Position == PlayerPosition.G).Take(1)
            .Concat(world.Players.Where(p => p.ClubId == club && p.Position != PlayerPosition.G).OrderByDescending(p => p.Position).Take(10)).Select(p => p.Id).ToList();
        var match = new MatchFixture { GameWorldId = world.Id, SeasonNumber = 1, RoundNumber = world.RoundNumber,
            HomeClubId = world.Clubs[0].Id, AwayClubId = world.Clubs[1].Id, CompetitionType = CompetitionType.League,
            DivisionTier = DivisionTier.PrimeiraDivisao, CurrentMinute = 90, IsFinished = true, HomeGoals = 4,
            HomeActiveStarterIds = Starters(world.Clubs[0].Id), AwayActiveStarterIds = Starters(world.Clubs[1].Id) };
        var first = world.Players.Single(p => p.Id == match.HomeActiveStarterIds[1]);
        var second = world.Players.Single(p => p.Id == match.HomeActiveStarterIds[2]);
        foreach (var (minute, player) in new[] { (10, first), (25, first), (40, second), (80, second) })
            match.Events.Add(new() { Minute = minute, ClubId = match.HomeClubId, EventType = MatchEventType.Goal, PrimaryPlayerId = player.Id });
        match.Events.Add(new() { Minute = 90, ClubId = match.HomeClubId, EventType = MatchEventType.PenaltyShootoutGoal, PrimaryPlayerId = first.Id });
        PlayerStatisticsEngine.CaptureLineup(match, world.Players.ToDictionary(p => p.Id));
        Assert.True(match.StatisticsLineupCaptured);
        world.Fixtures.Add(match);
        PlayerStatisticsEngine.ApplyFinishedMatches(world, [match]);
        Assert.All(world.PlayerStatistics, s => Assert.False(s.HasIncompleteAppearances));
        return (world, first, second, match);
    }

    private static string NewDatabase(string label)
    {
        string directory = Path.Combine(Audit, "sqlite");
        Directory.CreateDirectory(directory);
        return Path.Combine(directory, $"{label}-{Guid.NewGuid():N}.db");
    }

    private static string FindRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory != null; directory = directory.Parent)
            if (File.Exists(Path.Combine(directory.FullName, "Elifoot.slnx"))) return directory.FullName;
        throw new DirectoryNotFoundException("Elifoot.slnx was not found above the test output directory.");
    }

    private sealed class ApiHost(WebApplication app, HttpClient client) : IAsyncDisposable
    {
        public WebApplication App { get; } = app;
        public HttpClient Client { get; } = client;
        public IWorldRepository Repository => App.Services.GetRequiredService<IWorldRepository>();

        public static async Task<ApiHost> StartAsync(string database)
        {
            var builder = WebApplication.CreateBuilder(new WebApplicationOptions { EnvironmentName = "Testing", ContentRootPath = Root });
            builder.WebHost.UseTestServer();
            builder.Logging.ClearProviders();
            builder.Services.AddDbContext<ElifootDbContext>(options => options.UseSqlite($"Data Source={database}"));
            builder.Services.AddSingleton<IWorldRepository, EfWorldRepository>();
            builder.Services.AddScoped<PlayerRankingsApplicationService>();
            var app = builder.Build();
            using (var scope = app.Services.CreateScope())
                await scope.ServiceProvider.GetRequiredService<ElifootDbContext>().Database.EnsureCreatedAsync();
            app.MapPlayerRankings();
            await app.StartAsync();
            return new(app, app.GetTestClient());
        }

        public async Task<string> SnapshotAsync(Guid id)
        {
            using var scope = App.Services.CreateScope();
            return (await scope.ServiceProvider.GetRequiredService<ElifootDbContext>().Worlds.AsNoTracking().SingleAsync(w => w.Id == id)).SnapshotJson;
        }

        public async Task AssertLedgerAsync(Guid id, int count, decimal amount)
        {
            using var scope = App.Services.CreateScope();
            var rows = await scope.ServiceProvider.GetRequiredService<ElifootDbContext>().FinancialLedger.Where(t => t.GameWorldId == id).ToListAsync();
            Assert.Equal(count, rows.Count);
            Assert.Equal(amount, rows.Sum(t => t.Amount));
            Assert.Equal(count, rows.Select(t => t.IdempotencyKey).Distinct().Count());
            Assert.All(rows, t => { Assert.Equal(3_000_000m, t.Amount); Assert.Equal((int)FinancialTransactionCategory.PrizeMoney, t.Category); });
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await App.StopAsync();
            await App.DisposeAsync();
        }
    }
}
