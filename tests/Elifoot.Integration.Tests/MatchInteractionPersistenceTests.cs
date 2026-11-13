using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Domain;
using Elifoot.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.EventLog;
namespace Elifoot.Integration.Tests;

[Collection("Player rankings 009 SQLite")]
public class MatchInteractionPersistenceTests
{
    private static WebApplicationFactory<Program> Factory(string connection, bool postgres, SaveChangesInterceptor? fault = null) => new WebApplicationFactory<Program>().WithWebHostBuilder(b => {
        b.UseSetting("Elifoot:AuthMode", "LocalDev"); b.UseSetting("Elifoot:DatabaseProvider", postgres ? "PostgreSQL" : "Sqlite");
        b.UseSetting("ConnectionStrings:DefaultConnection", connection);
        b.UseSetting("Elifoot:AutoSeedDefaultWorld", "false");
        b.UseSetting("Elifoot:DisableBackgroundClock", "true");
        if (fault != null)
        {
            // Keep injected SQLite failures observable on hosts without Event Log write access.
            b.ConfigureLogging(logging =>
            {
                if (OperatingSystem.IsWindows())
                    logging.AddFilter<EventLogLoggerProvider>(null, LogLevel.None);
            });
            b.ConfigureServices(services =>
                services.ConfigureDbContext<ElifootDbContext>(options => options.AddInterceptors(fault)));
        }
    });
    private static async Task<(GameWorld W, MatchFixture M, HumanCoach A, HumanCoach B)> Room()
    {
        var repository = new InMemoryWorldRepository();
        var service = new MultiplayerRoundOrchestrator(repository, NullMatchRoomNotificationPublisher.Instance);
        var world = await service.CreateWorldAsync(new("Match interaction HTTP QA", TotalClubs:128));
        world.RoundNumber = 1;
        var match = MultiplayerRoundOrchestrator.GetCurrentRoundFixtures(world)[0];
        var a = (await service.JoinWorldAndAssignClubAsync(world.Id, Guid.NewGuid(), "QA Home", "qa-home", match.HomeClubId)).Coach!;
        var b = (await service.JoinWorldAndAssignClubAsync(world.Id, Guid.NewGuid(), "QA Away", "qa-away", match.AwayClubId)).Coach!;
        foreach (var coach in new[] { a, b }) {
            var club = world.Clubs.First(c => c.Id == coach.CurrentClubId);
            Assert.True((await service.SubmitTacticsAsync(new(world.Id, coach.Id, club.PreferredFormation, club.PreferredStarterIds, club.PreferredBenchIds, $"start-{coach.Id}", world.RoundVersion))).Success);
        }
        var players = world.Players.ToDictionary(p => p.Id);
        world.CurrentMatchMinute = 20;
        world.RequiresDurableMatchInteractionSave = true;
        foreach (var fixture in MultiplayerRoundOrchestrator.GetCurrentRoundFixtures(world)) {
            PlayerStatisticsEngine.CaptureLineup(fixture, players);
            fixture.CurrentMinute = fixture.LastSimulatedMinute = 20;
        }
        return (world, match, a, b);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task ContinueRecoversFromSqliteIoFailure(bool afterCommit, bool lastConfirmation)
    {
        string file = Path.Combine(Path.GetTempPath(), $"elifoot-continue-io-{Guid.NewGuid():N}.db");
        string connection = $"Data Source={file};Pooling=False";
        var (world, match, a, b) = await Room();
        var window = new PauseWindow
        {
            GameWorldId = world.Id, SeasonNumber = world.SeasonNumber, RoundNumber = world.RoundNumber,
            Minute = 20, ExpiresAtUtc = DateTime.UtcNow.AddHours(1)
        };
        if (lastConfirmation) window.ConfirmedCoachIds.Add(a.Id);
        world.PauseWindows.Add(window);
        world.ActivePauseWindowId = window.Id;
        world.RoundState = RoundState.GLOBAL_PAUSED;
        match.HomeGoals = 1;
        MatchEventType[] originalEvents = [MatchEventType.Goal, MatchEventType.YellowCard, MatchEventType.Injury];
        match.Events = originalEvents.Select(type => new MatchEventRecord { EventType = type, Minute = 19, ClubId = match.HomeClubId }).ToList();
        var balances = world.Clubs.Select(club => club.CashBalance).ToArray();
        var ledgerIds = world.Transactions.Select(entry => entry.Id).ToArray();
        long version = world.RoundVersion;
        var coach = lastConfirmation ? b : a;
        var command = new ConfirmContinueMatchCommand(world.Id, coach.Id, "resume-io") { PauseWindowId = window.Id };
        var route = $"/api/worlds/{world.Id}";
        var fault = new ContinueSaveFault(afterCommit);

        try
        {
            await using (var initial = Factory(connection, false, fault))
            {
                using var first = initial.CreateClient();
                using var second = initial.CreateClient();
                await initial.Services.GetRequiredService<IWorldRepository>().SaveWorldAsync(world);
                first.DefaultRequestHeaders.Add("Cookie", "session=private-test-cookie");
                first.DefaultRequestHeaders.Add("Authorization", "Bearer private-test-token");
                fault.Armed = true;

                using var failed = await first.PostAsJsonAsync(route + "/continue", command);
                Assert.Equal(HttpStatusCode.ServiceUnavailable, failed.StatusCode);
                var body = await failed.Content.ReadFromJsonAsync<JsonElement>();
                Assert.Equal("MATCH_PERSISTENCE_FAILED", body.GetProperty("code").GetString());
                Assert.Equal(2, body.EnumerateObject().Count());
                Assert.DoesNotContain("private-test", body.GetRawText());
                Assert.DoesNotContain(nameof(SqliteException), body.GetRawText());
                Assert.Equal(1, fault.Failures);

                var left = (await first.GetFromJsonAsync<LiveRoundSnapshotDto>(route + "/snapshot"))!;
                var right = (await second.GetFromJsonAsync<LiveRoundSnapshotDto>(route + "/snapshot"))!;
                Assert.Equal(afterCommit ? version + 1 : version, left.RoundVersion);
                Assert.Equal(left.RoundVersion, right.RoundVersion);
                Assert.Equal(afterCommit ? RoundState.RUNNING_FIRST_HALF : RoundState.GLOBAL_PAUSED, left.RoundState);
                Assert.Equal(left.RoundState, right.RoundState);
                if (!afterCommit)
                {
                    Assert.DoesNotContain(coach.Id, left.ActivePauseWindow!.ConfirmedCoachIds);
                    using var tick = await second.PostAsync(route + "/tick", null);
                    tick.EnsureSuccessStatusCode();
                    Assert.Equal(20, (await tick.Content.ReadFromJsonAsync<LiveRoundSnapshotDto>())!.CurrentMinute);
                }

                var retries = await Task.WhenAll(first.PostAsJsonAsync(route + "/continue", command),
                    second.PostAsJsonAsync(route + "/continue", command));
                foreach (var retry in retries)
                {
                    using (retry)
                    {
                        retry.EnsureSuccessStatusCode();
                        var result = await retry.Content.ReadFromJsonAsync<JsonElement>();
                        Assert.Equal(lastConfirmation, result.GetProperty("resumed").GetBoolean());
                        Assert.Equal(version + 1, result.GetProperty("snapshot").GetProperty("roundVersion").GetInt64());
                    }
                }
            }

            EfWorldRepository.ClearCacheForTesting();
            await using var restarted = Factory(connection, false);
            using var client = restarted.CreateClient();
            using var replay = await client.PostAsJsonAsync(route + "/continue", command);
            replay.EnsureSuccessStatusCode();
            var wrongWindow = command with { PauseWindowId = Guid.NewGuid(), IdempotencyKey = "stale-window" };
            using var stale = await client.PostAsJsonAsync(route + "/continue", wrongWindow);
            Assert.Equal(HttpStatusCode.BadRequest, stale.StatusCode);
            var restored = (await restarted.Services.GetRequiredService<IWorldRepository>().GetWorldAsync(world.Id))!;
            var restoredMatch = restored.Fixtures.Single(f => f.Id == match.Id);
            Assert.Equal(version + 1, restored.RoundVersion);
            Assert.Equal(world.RoundNumber, restored.RoundNumber);
            Assert.Equal(20, restored.CurrentMatchMinute);
            Assert.Equal(1, restoredMatch.HomeGoals);
            Assert.Equal(originalEvents, restoredMatch.Events.Select(e => e.EventType));
            Assert.Equal(balances, restored.Clubs.Select(club => club.CashBalance));
            Assert.Equal(ledgerIds, restored.Transactions.Select(entry => entry.Id));
        }
        finally
        {
            EfWorldRepository.ClearCacheForTesting();
            if (File.Exists(file)) File.Delete(file);
        }
    }

    private sealed class ContinueSaveFault(bool afterCommit) : SaveChangesInterceptor
    {
        public bool Armed { get; set; }
        public int Failures { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
            InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (!afterCommit) FailIfArmed();
            return ValueTask.FromResult(result);
        }

        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default)
        {
            if (afterCommit) FailIfArmed();
            return ValueTask.FromResult(result);
        }

        private void FailIfArmed()
        {
            if (!Armed) return;
            Armed = false;
            Failures++;
            throw new SqliteException("disk I/O error", 10, 778);
        }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task HalftimeWaitsOnlyForPlayingHumansAcrossRestart(bool playingHuman)
    {
        string file = Path.Combine(Path.GetTempPath(), $"elifoot-interval-participation-{Guid.NewGuid():N}.db");
        string connection = $"Data Source={file};Pooling=False";
        var (world, _, a, b) = await Room();
        foreach (var observer in playingHuman ? new[] { b } : new[] { a, b })
        {
            world.Clubs.First(c => c.Id == observer.CurrentClubId).HumanCoachId = null;
            observer.CurrentClubId = null;
        }
        world.CurrentMatchMinute = 44;
        foreach (var fixture in MultiplayerRoundOrchestrator.GetCurrentRoundFixtures(world))
            fixture.CurrentMinute = fixture.LastSimulatedMinute = 44;
        try
        {
            await using (var initial = Factory(connection, false))
            {
                using var client = initial.CreateClient();
                await initial.Services.GetRequiredService<IWorldRepository>().SaveWorldAsync(world);
                var reached = await ApiAndSignalREndToEndTests.TickWithAutomaticPenaltyChoices(client, world.Id, 1);
                reached.EnsureSuccessStatusCode();
                var snapshot = (await reached.Content.ReadFromJsonAsync<LiveRoundSnapshotDto>())!;
                Assert.Equal(45, snapshot.CurrentMinute);
                Assert.Equal(playingHuman ? RoundState.HALFTIME_PAUSED : RoundState.RUNNING_SECOND_HALF, snapshot.RoundState);
            }
            EfWorldRepository.ClearCacheForTesting();
            await using (var restarted = Factory(connection, false))
            {
                using var client = restarted.CreateClient();
                var snapshot = (await client.GetFromJsonAsync<LiveRoundSnapshotDto>($"/api/worlds/{world.Id}/snapshot"))!;
                Assert.Equal(45, snapshot.CurrentMinute);
                if (playingHuman)
                {
                    Assert.Contains(b.Id, snapshot.ActivePauseWindow!.ConfirmedCoachIds);
                    Assert.DoesNotContain(a.Id, snapshot.ActivePauseWindow.ConfirmedCoachIds);
                    // Only the playing coach confirms; the spectator never sends a command.
                    (await client.PostAsJsonAsync($"/api/worlds/{world.Id}/continue",
                        new ConfirmContinueMatchCommand(world.Id, a.Id, "continue-player"))).EnsureSuccessStatusCode();
                }
                else
                {
                    Assert.Null(snapshot.ActivePauseWindow);
                    Assert.Equal(RoundState.RUNNING_SECOND_HALF, snapshot.RoundState);
                }
                var advanced = await ApiAndSignalREndToEndTests.TickWithAutomaticPenaltyChoices(client, world.Id, 1);
                advanced.EnsureSuccessStatusCode();
                var secondHalf = (await advanced.Content.ReadFromJsonAsync<LiveRoundSnapshotDto>())!;
                Assert.Equal(46, secondHalf.CurrentMinute);
                Assert.Equal(RoundState.RUNNING_SECOND_HALF, secondHalf.RoundState);
                Assert.Null(secondHalf.ActivePauseWindow);
            }
        }
        finally
        {
            EfWorldRepository.ClearCacheForTesting();
            if (File.Exists(file)) File.Delete(file);
        }
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task PendingHumanPenaltyFreezesRoomAcrossDatabaseRestartAndConcurrentHttpChoicesSettleExactlyOnce(bool postgres)
    {
        string file = Path.Combine(Path.GetTempPath(), $"elifoot-match-decisions-{Guid.NewGuid():N}.db");
        string connection = postgres ? Environment.GetEnvironmentVariable("ELIFOOT_015A_MARKET_PG") ?? throw new InvalidOperationException("Use the isolated 015B test runner.") : $"Data Source={file};Pooling=False";
        var (w, m, a, b) = await Room();
        var (other, _, _, _) = await Room();
        var pending = new PendingPenaltyKick { CoachId=a.Id, ClubId=m.HomeClubId, Minute=20, ConversionRoll=0, GoalkeeperPower=25, EligiblePlayerIds=m.HomeActiveStarterIds.ToList() };
        m.PendingPenalties.Add(pending);
        long version = w.RoundVersion;
        ChoosePenaltyTakerCommand? winning = null;
        try {
            await using (var initial = Factory(connection, postgres)) {
                using var client = initial.CreateClient();
                var repo = initial.Services.GetRequiredService<IWorldRepository>();
                await repo.SaveWorldAsync(w); await repo.SaveWorldAsync(other);
            }
            EfWorldRepository.ClearCacheForTesting();
            await using (var restarted = Factory(connection, postgres)) {
                using var ownerClient = restarted.CreateClient(); using var observerClient = restarted.CreateClient();
                var repo = restarted.Services.GetRequiredService<IWorldRepository>();
                var restored = (await repo.GetWorldAsync(w.Id))!;
                var restoredMatch = restored.Fixtures.Single(f => f.Id == m.Id);
                Assert.Equal(pending.Id, Assert.Single(restoredMatch.PendingPenalties).Id);
                Assert.Equal(0, restoredMatch.PendingPenalties[0].ConversionRoll);
                for (int i=0; i<3; i++) {
                    var response = await observerClient.PostAsync($"/api/worlds/{w.Id}/tick?ticks=125", null); response.EnsureSuccessStatusCode();
                    var json = await response.Content.ReadAsStringAsync();
                    var snapshot = JsonSerializer.Deserialize<JsonElement>(json);
                    Assert.Equal(20, snapshot.GetProperty("currentMinute").GetInt32());
                    Assert.Equal(version, snapshot.GetProperty("roundVersion").GetInt64());
                    Assert.Equal(pending.Id, snapshot.GetProperty("pendingPenalty").GetProperty("decisionId").GetGuid());
                    Assert.DoesNotContain("conversionRoll", json, StringComparison.OrdinalIgnoreCase);
                    Assert.DoesNotContain("goalkeeperPower", json, StringComparison.OrdinalIgnoreCase);
                    Assert.All(MultiplayerRoundOrchestrator.GetCurrentRoundFixtures(restored), f => Assert.Equal(20, f.LastSimulatedMinute));
                }
                Assert.Equal(21, (await (await observerClient.PostAsync($"/api/worlds/{other.Id}/tick", null)).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("currentMinute").GetInt32());
                var wrongCoach = new ChoosePenaltyTakerCommand(w.Id,b.Id,pending.Id,pending.EligiblePlayerIds[0],"wrong-coach");
                Assert.False((await observerClient.PostAsJsonAsync($"/api/worlds/{w.Id}/penalty-taker", wrongCoach)).IsSuccessStatusCode);
                var bench = wrongCoach with { CoachId=a.Id, PlayerId=m.HomeBenchIds[0], IdempotencyKey="bench" };
                Assert.False((await ownerClient.PostAsJsonAsync($"/api/worlds/{w.Id}/penalty-taker", bench)).IsSuccessStatusCode);
                var commands = pending.EligiblePlayerIds.Take(2).Select((id,i) => new ChoosePenaltyTakerCommand(w.Id,a.Id,pending.Id,id,$"race-{i}")).ToArray();
                var results = await Task.WhenAll(commands.Select(cmd => ownerClient.PostAsJsonAsync($"/api/worlds/{w.Id}/penalty-taker",cmd)));
                Assert.Single(results, result => result.IsSuccessStatusCode);
                winning = commands[Array.FindIndex(results,r=>r.IsSuccessStatusCode)];
                Assert.Empty(restoredMatch.PendingPenalties); Assert.Equal(1,restoredMatch.HomeGoals);
                Assert.Single(restoredMatch.Events, e=>e.EventType==MatchEventType.Goal);
                Assert.Equal(winning.PlayerId, restoredMatch.Events.Single(e=>e.EventType==MatchEventType.Goal).PrimaryPlayerId);
                Assert.Equal(0,restoredMatch.HomeUsedPauseWindows); Assert.Equal(0,restoredMatch.HomeUsedSubstitutions);
            }
            EfWorldRepository.ClearCacheForTesting();
            await using (var final = Factory(connection, postgres)) {
                using var client=final.CreateClient(); var restored=(await final.Services.GetRequiredService<IWorldRepository>().GetWorldAsync(w.Id))!;
                Assert.True((await client.PostAsJsonAsync($"/api/worlds/{w.Id}/penalty-taker", winning)).IsSuccessStatusCode);
                Assert.True((await client.PostAsJsonAsync($"/api/worlds/{w.Id}/penalty-taker", winning! with { IdempotencyKey="retry-after-restart" })).IsSuccessStatusCode);
                var fixture=restored.Fixtures.Single(f=>f.Id==m.Id); Assert.Equal(1,fixture.HomeGoals); Assert.Single(fixture.Events,e=>e.EventType==MatchEventType.Goal);
                Assert.Null((await client.GetFromJsonAsync<JsonElement>($"/api/worlds/{w.Id}/snapshot")).GetProperty("pendingPenalty").Deserialize<PendingPenaltyDecisionDto>());
                Assert.Equal(21,(await (await client.PostAsync($"/api/worlds/{w.Id}/tick",null)).Content.ReadFromJsonAsync<JsonElement>()).GetProperty("currentMinute").GetInt32());
            }
        } finally { EfWorldRepository.ClearCacheForTesting(); if(!postgres && File.Exists(file)) File.Delete(file); }
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task OpeningAndActualSubstitutionReceiptsSurviveRestartWithoutReopeningOrChargingTwice(bool postgres)
    {
        string file=Path.Combine(Path.GetTempPath(),$"elifoot-pause-decisions-{Guid.NewGuid():N}.db");
        string connection=postgres?Environment.GetEnvironmentVariable("ELIFOOT_015A_MARKET_PG")??throw new InvalidOperationException("Use the isolated 015B runner."):$"Data Source={file};Pooling=False";
        var(w,m,a,b)=await Room();
        var open=new RequestPauseCommand(w.Id,a.Id,"open");
        var outgoing=m.HomeActiveStarterIds.First(id=>w.Players.First(p=>p.Id==id).Position!=PlayerPosition.G);
        var incoming=m.HomeBenchIds.First(id=>w.Players.First(p=>p.Id==id).Position!=PlayerPosition.G);
        var edit=new SubmitInMatchAdjustmentCommand(w.Id,a.Id,m.Id,null,new(){new(outgoing,incoming)},"edit");
        try {
            await using(var initial=Factory(connection,postgres)) {
                using var client=initial.CreateClient(); await initial.Services.GetRequiredService<IWorldRepository>().SaveWorldAsync(w);
                (await client.PostAsJsonAsync($"/api/worlds/{w.Id}/pause",open)).EnsureSuccessStatusCode(); Assert.Equal(0,m.HomeUsedPauseWindows);
                (await client.PostAsJsonAsync($"/api/worlds/{w.Id}/in-match-adjust",edit)).EnsureSuccessStatusCode(); Assert.Equal(1,m.HomeUsedPauseWindows); Assert.Equal(1,m.HomeUsedSubstitutions); Assert.Equal(0,m.AwayUsedPauseWindows);
                foreach(var h in new[]{a,b}) (await client.PostAsJsonAsync($"/api/worlds/{w.Id}/continue",new ConfirmContinueMatchCommand(w.Id,h.Id,$"continue-{h.Id}"))).EnsureSuccessStatusCode();
                Assert.Equal(RoundState.RUNNING_FIRST_HALF,w.RoundState);
            }
            EfWorldRepository.ClearCacheForTesting();
            await using(var final=Factory(connection,postgres)) {
                using var client=final.CreateClient(); var restored=(await final.Services.GetRequiredService<IWorldRepository>().GetWorldAsync(w.Id))!;
                (await client.PostAsJsonAsync($"/api/worlds/{w.Id}/pause",open)).EnsureSuccessStatusCode();
                (await client.PostAsJsonAsync($"/api/worlds/{w.Id}/in-match-adjust",edit)).EnsureSuccessStatusCode();
                Assert.Null(restored.ActivePauseWindowId); Assert.Equal(RoundState.RUNNING_FIRST_HALF,restored.RoundState);
                var fixture=restored.Fixtures.Single(f=>f.Id==m.Id); Assert.Equal(1,fixture.HomeUsedPauseWindows); Assert.Equal(1,fixture.HomeUsedSubstitutions);
                Assert.DoesNotContain(outgoing,fixture.HomeActiveStarterIds); Assert.Contains(incoming,fixture.HomeActiveStarterIds);
            }
        } finally { EfWorldRepository.ClearCacheForTesting(); if(!postgres && File.Exists(file))File.Delete(file); }
    }
}
