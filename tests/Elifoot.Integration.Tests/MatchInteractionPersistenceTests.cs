using System.Net.Http.Json;
using System.Text.Json;
using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Domain;
using Elifoot.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
namespace Elifoot.Integration.Tests;

[Collection("Player rankings 009 SQLite")]
public class MatchInteractionPersistenceTests
{
    private static WebApplicationFactory<Program> Factory(string connection, bool postgres) => new WebApplicationFactory<Program>().WithWebHostBuilder(b => {
        b.UseSetting("Elifoot:AuthMode", "LocalDev"); b.UseSetting("Elifoot:DatabaseProvider", postgres ? "PostgreSQL" : "Sqlite");
        b.UseSetting("ConnectionStrings:DefaultConnection", connection);
        b.UseSetting("Elifoot:AutoSeedDefaultWorld", "false");
        b.UseSetting("Elifoot:DisableBackgroundClock", "true");
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
