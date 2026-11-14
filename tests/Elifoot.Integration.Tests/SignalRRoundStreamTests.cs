using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Channels;
using Elifoot.Contracts;
using Elifoot.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;

namespace Elifoot.Integration.Tests;

public sealed class SignalRRoundStreamTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RestartEstablishesNewEpochAndRestoresLastDurableVersion(bool compress)
    {
        string file = Path.Combine(Path.GetTempPath(), $"elifoot-v2-{Guid.NewGuid():N}.db");
        WebApplicationFactory<Program> Start() => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Elifoot:AuthMode", "LocalDev");
            builder.UseSetting("Elifoot:DatabaseProvider", "Sqlite");
            builder.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={file};Pooling=False");
            builder.UseSetting("Elifoot:AutoSeedDefaultWorld", "false");
            builder.UseSetting("Elifoot:DisableBackgroundClock", "true");
            builder.UseSetting("Elifoot:CompressNewSqliteSnapshots", compress.ToString());
        });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var ct = timeout.Token;
        try
        {
            LiveRoundSnapshotDto running;
            var coach = Guid.NewGuid();
            string path;
            await using (var before = Start())
            {
                using var http = before.CreateClient();
                using var created = await http.PostAsJsonAsync("/api/worlds", new CreateWorldRequest("Restart", 1, 64), ct);
                created.EnsureSuccessStatusCode();
                var world = (await created.Content.ReadFromJsonAsync<LiveRoundSnapshotDto>(ct))!;
                path = $"/api/worlds/{world.GameWorldId}";
                using var joined = await http.PostAsync(path + $"/join?coachId={coach}&nickname=Coach&joinUnemployed=true", null, ct);
                joined.EnsureSuccessStatusCode();
                using var ready = await http.PostAsJsonAsync(path + "/ready-unemployed", new UnemployedReadyRequest(world.GameWorldId, coach, "ready", 1), ct);
                ready.EnsureSuccessStatusCode();
                using var ticks = await http.PostAsync(path + "/tick?ticks=3", null, ct);
                ticks.EnsureSuccessStatusCode();
                running = (await http.GetFromJsonAsync<LiveRoundSnapshotDto>(path + "/snapshot", ct))!;
                Assert.Equal(3, running.CurrentMinute);
                Assert.True(running.PersistedRoundVersion < running.RoundVersion);
            }
            EfWorldRepository.ClearCacheForTesting();
            await using var after = Start();
            using var client = after.CreateClient();
            var restored = (await client.GetFromJsonAsync<LiveRoundSnapshotDto>(
                path + $"/snapshot?epoch={running.RealtimeEpoch}&sequence={running.RealtimeSequence}&version={running.RoundVersion}", ct))!;
            Assert.NotEqual(running.RealtimeEpoch, restored.RealtimeEpoch);
            Assert.Equal(running.PersistedRoundVersion, restored.RoundVersion);
            Assert.Equal(0, restored.CurrentMinute);
            using var replay = await client.PostAsJsonAsync(path + "/ready-unemployed",
                new UnemployedReadyRequest(restored.GameWorldId, coach, "ready", 1), ct);
            replay.EnsureSuccessStatusCode();
            var afterReplay = (await client.GetFromJsonAsync<LiveRoundSnapshotDto>(path + "/snapshot", ct))!;
            Assert.Equal(restored.RoundVersion, afterReplay.RoundVersion);
        }
        finally
        {
            File.Delete(file);
        }
    }

    [Theory]
    [InlineData(2)]
    [InlineData(8)]
    public async Task IncrementalAndLegacyClientsReceiveOnlyTheirRoomAndRecoverAfterDisconnect(int coachCount)
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Elifoot:AuthMode", "LocalDev");
            builder.UseSetting("Elifoot:DatabaseProvider", "InMemory");
            builder.UseSetting("Elifoot:AutoSeedDefaultWorld", "false");
            builder.UseSetting("Elifoot:DisableBackgroundClock", "true");
        });
        using var http = factory.CreateClient();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(90));
        var ct = timeout.Token;
        async Task<LiveRoundSnapshotDto> Create(int seed)
        {
            using var response = await http.PostAsJsonAsync("/api/worlds", new CreateWorldRequest($"Room {seed}", coachCount, 64, seed), ct);
            response.EnsureSuccessStatusCode();
            return (await response.Content.ReadFromJsonAsync<LiveRoundSnapshotDto>(ct))!;
        }

        async Task AddSeat(Guid world, Guid coach)
        {
            using var response = await http.PostAsync($"/api/worlds/{world}/join?coachId={coach}&nickname=Coach&joinUnemployed=true", null, ct);
            response.EnsureSuccessStatusCode();
        }

        HubConnection Connect() => new HubConnectionBuilder().WithUrl("http://localhost/hubs/gameroom",
            options => options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler()).Build();

        var room = await Create(199801);
        var other = await Create(199802);
        var otherCoach = Guid.NewGuid();
        await AddSeat(other.GameWorldId, otherCoach);
        var path = $"/api/worlds/{room.GameWorldId}";
        var coaches = Enumerable.Range(0, coachCount).Select(_ => Guid.NewGuid()).ToArray();
        var hubs = coaches.Select(_ => Connect()).ToArray();
        var first = Channel.CreateUnbounded<JsonElement>();
        var second = Channel.CreateUnbounded<JsonElement>();
        var legacy = new ConcurrentQueue<LiveRoundSnapshotDto>();
        var leaked = new ConcurrentQueue<JsonElement>();
        await using var isolated = Connect();
        foreach (var target in new[] { "RoundSyncV2", "RoundPatchV2" })
        {
            hubs[0].On<JsonElement>(target, msg => first.Writer.TryWrite(msg));
            hubs[1].On<JsonElement>(target, msg => second.Writer.TryWrite(msg));
            isolated.On<JsonElement>(target, leaked.Enqueue);
        }
        await using var oldClient = Connect();
        oldClient.On<LiveRoundSnapshotDto>("ClockTick", legacy.Enqueue);
        try
        {
            for (int i = 0; i < coaches.Length; i++)
            {
                await AddSeat(room.GameWorldId, coaches[i]);
                await hubs[i].StartAsync(ct);
                Assert.NotNull(await hubs[i].InvokeAsync<LiveRoundSnapshotDto>("JoinRoomV2", room.GameWorldId, coaches[i], ct));
            }
            await isolated.StartAsync(ct);
            Assert.NotNull(await isolated.InvokeAsync<LiveRoundSnapshotDto>("JoinRoomV2", other.GameWorldId, otherCoach, ct));
            Assert.Null(await isolated.InvokeAsync<LiveRoundSnapshotDto?>("JoinRoomV2", room.GameWorldId, otherCoach, ct));
            await oldClient.StartAsync(ct);
            Assert.NotNull(await oldClient.InvokeAsync<LiveRoundSnapshotDto>("JoinRoom", room.GameWorldId, coaches[0], ct));

            async Task Ready(Guid coach, int number)
            {
                using var response = await http.PostAsJsonAsync(path + "/ready-unemployed", new UnemployedReadyRequest(room.GameWorldId, coach, $"ready-{number}", 1), ct);
                response.EnsureSuccessStatusCode();
            }

            await Ready(coaches[0], 0);
            var waiting = await http.GetFromJsonAsync<LiveRoundSnapshotDto>(path + "/snapshot", ct);
            Assert.Equal(RoundState.PREPARING, waiting!.RoundState);
            await Task.WhenAll(coaches.Skip(1).Select((id, index) => Ready(id, index + 1)));
            var start = await first.Reader.ReadAsync(ct);
            Assert.Equal("RUNNING_FIRST_HALF", start.GetProperty("roundState").GetString());
            Assert.Equal(coachCount, start.GetProperty("readyCounter").GetProperty("readyCount").GetInt32());
            Assert.Equal(start.GetProperty("realtimeSequence").GetInt64(),
                (await second.Reader.ReadAsync(ct)).GetProperty("realtimeSequence").GetInt64());
            await hubs[1].StopAsync(ct);

            using var tick = await http.PostAsync(path + "/tick?ticks=3", null, ct);
            tick.EnsureSuccessStatusCode();
            var authoritative = (await tick.Content.ReadFromJsonAsync<LiveRoundSnapshotDto>(ct))!;
            var firstTick = await first.Reader.ReadAsync(ct);
            var lost = await first.Reader.ReadAsync(ct);
            var later = await first.Reader.ReadAsync(ct);
            Assert.Equal(lost.GetProperty("realtimeSequence").GetInt64(), later.GetProperty("previousSequence").GetInt64());
            Assert.NotEqual(firstTick.GetProperty("realtimeSequence").GetInt64(), later.GetProperty("previousSequence").GetInt64());
            Assert.Equal(authoritative.RoundVersion, later.GetProperty("roundVersion").GetInt64());
            var recovered = (await http.GetFromJsonAsync<LiveRoundSnapshotDto>(path + "/snapshot", ct))!;
            Assert.Equal(authoritative.RoundVersion, recovered.RoundVersion);
            Assert.Equal(later.GetProperty("realtimeSequence").GetInt64(), recovered.RealtimeSequence);
            using var unchanged = await http.GetAsync(path + $"/snapshot?epoch={recovered.RealtimeEpoch}&sequence={recovered.RealtimeSequence}&version={recovered.RoundVersion}", ct);
            Assert.Equal(HttpStatusCode.NoContent, unchanged.StatusCode);
            await hubs[1].StartAsync(ct);
            var rejoined = await hubs[1].InvokeAsync<LiveRoundSnapshotDto>("JoinRoomV2", room.GameWorldId, coaches[1], ct);
            Assert.Equal(recovered.RoundVersion, rejoined.RoundVersion);
            Assert.Equal(3, rejoined.CurrentMinute);
            Assert.Empty(leaked);
            Assert.Contains(legacy, entry => entry.GameWorldId == room.GameWorldId);
        }
        finally
        {
            foreach (var hub in hubs) await hub.DisposeAsync();
        }
    }
}
