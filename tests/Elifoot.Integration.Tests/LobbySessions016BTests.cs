using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Domain;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;

namespace Elifoot.Integration.Tests;

public class LobbySessions016BTests
{
    private static WebApplicationFactory<Program> Factory() => new WebApplicationFactory<Program>().WithWebHostBuilder(b => {
        b.UseSetting("Elifoot:AuthMode", "LocalDev"); b.UseSetting("Elifoot:DatabaseProvider", "InMemory");
        b.UseSetting("Elifoot:AutoSeedDefaultWorld", "false");
        b.UseSetting("Elifoot:DisableBackgroundClock", "true");
    });
    private static async Task<Guid> World(HttpClient client, int capacity) {
        var response = await client.PostAsJsonAsync("/api/worlds", new CreateWorldRequest("Lobby audit " + Guid.NewGuid(), MaxHumans: capacity));
        response.EnsureSuccessStatusCode(); return (await response.Content.ReadFromJsonAsync<LiveRoundSnapshotDto>())!.GameWorldId;
    }
    private static Task<HttpResponseMessage> Join(HttpClient client, Guid world, Guid coach, string device, string nickname = "Audit") =>
        client.PostAsync($"/api/worlds/{world}/join?coachId={coach}&nickname={Uri.EscapeDataString(nickname)}&deviceId={Uri.EscapeDataString(device)}", null);
    private static async Task<JsonElement> Room(HttpClient client, Guid world) =>
        (await client.GetFromJsonAsync<List<JsonElement>>("/api/worlds"))!.Single(w => w.GetProperty("id").GetGuid() == world);
    private static HubConnection Hub(WebApplicationFactory<Program> factory) => new HubConnectionBuilder()
        .WithUrl("http://localhost/hubs/gameroom", o => { o.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler(); o.Transports = HttpTransportType.LongPolling; }).Build();
    private static async Task Online(HttpClient client, Guid world, int count) {
        for (int i = 0; i < 100; i++) { if ((await Room(client, world)).GetProperty("connectedHumans").GetInt32() == count) return; await Task.Delay(50); }
        Assert.Equal(count, (await Room(client, world)).GetProperty("connectedHumans").GetInt32());
    }

    [Theory][InlineData(1)][InlineData(2)][InlineData(3)][InlineData(4)][InlineData(5)][InlineData(6)][InlineData(7)][InlineData(8)]
    public async Task Capacity_RealHttpAndSignalR_JoinFullReconnectDisconnectReturn(int capacity)
    {
        await using var factory = Factory(); using var client = factory.CreateClient();
        var world = await World(client, capacity); var coaches = new List<Guid>(); var devices = new List<string>(); var hubs = new List<HubConnection>();
        try {
            for (int i = 0; i < capacity; i++) {
                var coach = Guid.NewGuid(); var device = "016b-" + Guid.NewGuid(); coaches.Add(coach); devices.Add(device);
                using var response = await Join(client, world, coach, device); response.EnsureSuccessStatusCode();
                var room = await Room(client, world);
                Assert.Equal(i + 1, room.GetProperty("currentHumans").GetInt32());
                Assert.Equal(i, room.GetProperty("connectedHumans").GetInt32()); // HTTP membership is not live presence.
                var hub = Hub(factory); hubs.Add(hub); await hub.StartAsync();
                Assert.NotNull(await hub.InvokeAsync<LiveRoundSnapshotDto>("JoinRoom", world, coach));
                await Online(client, world, i + 1);
            }
            using var denied = await Join(client, world, Guid.NewGuid(), "new-device");
            Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
            var error = await denied.Content.ReadAsStringAsync(); Assert.Contains("ROOM_FULL", error); Assert.Contains("Sala lotada", error);
            // Exact reconnect at full capacity keeps the same participant and club.
            using var exact = await Join(client, world, coaches[0], devices[0]); exact.EnsureSuccessStatusCode();
            var original = await exact.Content.ReadFromJsonAsync<JsonElement>();
            await hubs[0].StopAsync(); await Online(client, world, capacity - 1);
            var offline = await Room(client, world);
            Assert.Equal(capacity, offline.GetProperty("currentHumans").GetInt32()); Assert.Equal(0, offline.GetProperty("availableSeats").GetInt32());
            Assert.Equal(capacity, offline.GetProperty("readyCounter").GetProperty("totalHumans").GetInt32());
            // A renamed LocalDev login with the SAME device restores the original identity before checking the limit.
            using var renamed = await Join(client, world, Guid.NewGuid(), devices[0], "Edited nickname"); renamed.EnsureSuccessStatusCode();
            var restored = await renamed.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(coaches[0], restored.GetProperty("coachId").GetGuid()); Assert.Equal(original.GetProperty("clubId").GetGuid(), restored.GetProperty("clubId").GetGuid());
            await hubs[0].StartAsync(); await hubs[0].InvokeAsync<LiveRoundSnapshotDto>("JoinRoom", world, coaches[0]); await Online(client, world, capacity);
            var final = await client.GetFromJsonAsync<LiveRoundSnapshotDto>($"/api/worlds/{world}/snapshot");
            Assert.Equal(capacity, final!.ReadyCounter.Participants.Select(p => p.CoachId).Distinct().Count());
            Assert.Equal(0, final.ReadyCounter.ReadyCount); Assert.Equal(1, final.RoundNumber); Assert.Equal(RoundState.PREPARING, final.RoundState);
            Assert.All(final.Matches, m => { Assert.Equal(0, m.HomeGoals); Assert.Equal(0, m.AwayGoals); });
        } finally { foreach (var hub in hubs) await hub.DisposeAsync(); }
        await Online(client, world, 0);
        Assert.Equal(capacity, (await Room(client, world)).GetProperty("currentHumans").GetInt32());
    }

    [Fact]
    public async Task MultipleTabs_OldDisconnectCannotRemoveActiveTab_AndRoomsAreIsolated()
    {
        await using var factory = Factory(); using var client = factory.CreateClient();
        var a = await World(client, 2); var b = await World(client, 2); var coach = Guid.NewGuid();
        (await Join(client, a, coach, "same-coach-device")).EnsureSuccessStatusCode();
        (await Join(client, b, coach, "same-coach-device")).EnsureSuccessStatusCode();
        await using var first = Hub(factory); await using var second = Hub(factory);
        await first.StartAsync(); await second.StartAsync();
        await first.InvokeAsync<LiveRoundSnapshotDto>("JoinRoom", a, coach); await second.InvokeAsync<LiveRoundSnapshotDto>("JoinRoom", a, coach);
        await Online(client, a, 1); await first.StopAsync(); await Online(client, a, 1);
        await second.InvokeAsync("LeaveRoom", b, coach); await Online(client, a, 1); // Wrong-room leave is inert.
        await second.InvokeAsync<LiveRoundSnapshotDto>("JoinRoom", b, coach);
        await Online(client, a, 0); await Online(client, b, 1);
        Assert.Equal(1, (await Room(client, a)).GetProperty("currentHumans").GetInt32());
        Assert.Equal(1, (await Room(client, b)).GetProperty("currentHumans").GetInt32());
        Assert.Null(await second.InvokeAsync<LiveRoundSnapshotDto>("JoinRoom", b, Guid.NewGuid()));
        await Online(client, b, 1); // An unregistered identity never joins a group or consumes presence.
    }

    [Fact]
    public async Task ConcurrentReconnects_SameDeviceNeverDuplicatesPersistentMembership()
    {
        await using var factory = Factory(); using var client = factory.CreateClient(); var world = await World(client, 8);
        var responses = await Task.WhenAll(Enumerable.Range(0, 24).Select(i => Join(client, world, Guid.NewGuid(), "same-device-" + world, "Alias " + i)));
        var identities = new List<Guid>();
        foreach (var response in responses) { response.EnsureSuccessStatusCode(); identities.Add((await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("coachId").GetGuid()); response.Dispose(); }
        Assert.Single(identities.Distinct()); var room = await Room(client, world);
        Assert.Equal(1, room.GetProperty("currentHumans").GetInt32()); Assert.Equal(0, room.GetProperty("connectedHumans").GetInt32()); Assert.Equal(7, room.GetProperty("availableSeats").GetInt32());
    }

    [Fact]
    public void RestartedServer_IgnoresStalePersistedPresence_AndDoesNotChangeReadiness()
    {
        var world = WorldSeedFactory.CreateSeededWorld(); var coach = new HumanCoach { GameWorldId = world.Id, IsConnected = true, IsReadyForRound = true }; world.HumanCoaches.Add(coach);
        var snapshot = new RoomConnectionRegistry().WithPresence(MultiplayerRoundOrchestrator.BuildLiveRoundSnapshotDto(world));
        Assert.False(snapshot.ReadyCounter.Participants.Single().IsConnected); Assert.Equal(1, snapshot.ReadyCounter.ReadyCount); Assert.Single(world.HumanCoaches); Assert.True(coach.IsReadyForRound);
    }
    [Fact]
    public async Task ConcurrentDistinctDevices_OnlyAvailableSeatsAreAdmitted()
    {
        await using var factory = Factory(); using var client = factory.CreateClient(); var world = await World(client, 2);
        var responses = await Task.WhenAll(Enumerable.Range(0, 16).Select(i => Join(client, world, Guid.NewGuid(), "distinct-" + i)));
        Assert.Equal(2, responses.Count(r => r.IsSuccessStatusCode)); Assert.Equal(14, responses.Count(r => r.StatusCode == HttpStatusCode.BadRequest));
        foreach (var response in responses) response.Dispose();
        var room = await Room(client, world); Assert.Equal(2, room.GetProperty("currentHumans").GetInt32()); Assert.Equal(0, room.GetProperty("availableSeats").GetInt32());
    }
    [Fact]
    public async Task SharedLegacyPlaceholder_DoesNotStealAnExistingIdentity()
    {
        await using var factory = Factory(); using var client = factory.CreateClient(); var world = await World(client, 2);
        (await Join(client, world, Guid.NewGuid(), "dev", "First")).EnsureSuccessStatusCode();
        (await Join(client, world, Guid.NewGuid(), "dev", "Second")).EnsureSuccessStatusCode();
        using var denied = await Join(client, world, Guid.NewGuid(), "dev", "Third"); Assert.Equal(HttpStatusCode.BadRequest, denied.StatusCode);
        Assert.Equal(2, (await Room(client, world)).GetProperty("currentHumans").GetInt32());
    }

    [Fact]
    public async Task LegacyIdentityIsResolvedIndependentlyPerRoom_WithoutExposingDevices()
    {
        await using var factory = Factory(); using var client = factory.CreateClient();
        var a = await World(client, 2); var b = await World(client, 2); var first = Guid.NewGuid(); var second = Guid.NewGuid();
        (await Join(client, a, first, "legacy-device", "Before")).EnsureSuccessStatusCode();
        (await Join(client, b, second, "legacy-device", "After")).EnsureSuccessStatusCode();
        var rows = await client.GetFromJsonAsync<List<JsonElement>>($"/api/worlds?coachId={Guid.NewGuid()}&deviceId=legacy-device");
        Assert.Equal(first, rows!.Single(w => w.GetProperty("id").GetGuid() == a).GetProperty("reconnectCoachId").GetGuid());
        Assert.Equal(second, rows!.Single(w => w.GetProperty("id").GetGuid() == b).GetProperty("reconnectCoachId").GetGuid());
        Assert.DoesNotContain("legacy-device", JsonSerializer.Serialize(rows));
        var anonymous = await Room(client, a); Assert.Equal(JsonValueKind.Null, anonymous.GetProperty("reconnectCoachId").ValueKind);
    }

}
