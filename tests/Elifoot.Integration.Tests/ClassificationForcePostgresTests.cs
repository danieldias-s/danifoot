using System.Net.Http.Json;
using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Domain;
using Elifoot.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace Elifoot.Integration.Tests;

[Collection("Player rankings 009 SQLite")]
public sealed class ClassificationForcePostgresTests
{
    [Fact]
    public async Task CupRoundPersistsOnceAcrossRefreshReconnectAndPostgresApiRestart()
    {
        var connection = new NpgsqlConnectionStringBuilder(
            Environment.GetEnvironmentVariable("ELIFOOT_013A_LEGACY_PG")
            ?? throw new InvalidOperationException("Configure o PostgreSQL isolado para a regressão."));
        Assert.Equal("127.0.0.1", connection.Host);
        Assert.InRange(connection.Port, 55433, 55442);
        connection.Database = $"elifoot_classification_{Guid.NewGuid():N}";

        WebApplicationFactory<Program> Api() => new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Elifoot:DatabaseProvider", "PostgreSQL");
            builder.UseSetting("ConnectionStrings:DefaultConnection", connection.ConnectionString);
            builder.UseSetting("Elifoot:AutoSeedDefaultWorld", "false");
            builder.UseSetting("Elifoot:DisableBackgroundClock", "true");
        });
        static HubConnection Hub(WebApplicationFactory<Program> api) =>
            new HubConnectionBuilder().WithUrl("http://localhost/hubs/gameroom",
                options => options.HttpMessageHandlerFactory = _ => api.Server.CreateHandler()).Build();

        Guid worldId, playerId, byeClubId;
        Guid coachId = Guid.NewGuid();
        (Guid Id, int Strength)[] strengths;
        string[] changeKeys;
        decimal contractedSalary;
        await using (var api = Api())
        {
            using var client = api.CreateClient();
            var created = await client.PostAsJsonAsync("/api/worlds", new CreateWorldRequest("Classificação PG", MaxHumans: 1));
            created.EnsureSuccessStatusCode();
            worldId = (await created.Content.ReadFromJsonAsync<LiveRoundSnapshotDto>())!.GameWorldId;
            var repository = api.Services.GetRequiredService<IWorldRepository>();
            var world = (await repository.GetWorldAsync(worldId))!;
            var cup = MultiplayerRoundOrchestrator.GetCurrentRoundFixtures(world);
            var bye = world.Clubs.First(c => c.DivisionTier == DivisionTier.PrimeiraDivisao);
            Assert.DoesNotContain(cup, m => m.HomeClubId == bye.Id || m.AwayClubId == bye.Id);
            byeClubId = bye.Id;
            var reserve = world.Players.First(p => p.ClubId == bye.Id);
            reserve.Strength = 1;
            playerId = reserve.Id;
            contractedSalary = reserve.SalaryPerRound;
            await repository.SaveWorldAsync(world);

            string join = $"/api/worlds/{worldId}/join?coachId={coachId}&nickname=ForcePG&deviceId=force-pg&preferredClubId={bye.Id}";
            (await client.PostAsync(join, null)).EnsureSuccessStatusCode();
            await using var hub = Hub(api);
            await hub.StartAsync();
            await hub.InvokeAsync<LiveRoundSnapshotDto>("JoinRoom", worldId, coachId);
            var ready = await client.PostAsJsonAsync($"/api/worlds/{worldId}/tactics",
                new SubmitTacticsRequest(worldId, coachId, bye.PreferredFormation, bye.PreferredStarterIds,
                    bye.PreferredBenchIds, "classification-ready", world.RoundVersion));
            ready.EnsureSuccessStatusCode();

            for (int step = 0; step < 20; step++)
            {
                var current = (await client.GetFromJsonAsync<LiveRoundSnapshotDto>($"/api/worlds/{worldId}/snapshot"))!;
                if (current.RoundState == RoundState.RESULTS) break;
                if (current.ActivePauseWindow != null)
                    (await client.PostAsJsonAsync($"/api/worlds/{worldId}/continue",
                        new ConfirmContinueMatchCommand(worldId, coachId, $"classification-continue-{step}"))).EnsureSuccessStatusCode();
                else
                    (await ApiAndSignalREndToEndTests.TickWithAutomaticPenaltyChoices(client, worldId, 125)).EnsureSuccessStatusCode();
            }
            Assert.Equal(RoundState.RESULTS, world.RoundState);
            Assert.All(cup, m => Assert.True(m.IsFinished));
            Assert.Equal(2, reserve.Strength);
            Assert.Equal(contractedSalary, reserve.SalaryPerRound);
            Assert.Single(world.PlayerStrengthHistory, e => e.PlayerId == playerId && e.ReserveGrowth == 1);
            strengths = world.Players.Select(p => (p.Id, p.Strength)).OrderBy(p => p.Id).ToArray();
            changeKeys = world.PlayerStrengthHistory.Select(e => e.IdempotencyKey).ToArray();
        }

        EfWorldRepository.ClearCacheForTesting();
        await using var restarted = Api();
        using var refreshed = restarted.CreateClient();
        for (int refresh = 0; refresh < 2; refresh++)
        {
            var detail = (await refreshed.GetFromJsonAsync<ClubDetailDto>($"/api/worlds/{worldId}/clubs/{byeClubId}"))!;
            Assert.Equal(2, Assert.Single(detail.Squad, p => p.Id == playerId).Strength);
        }
        await using var reconnected = Hub(restarted);
        await reconnected.StartAsync();
        var snapshot = await reconnected.InvokeAsync<LiveRoundSnapshotDto>("JoinRoom", worldId, coachId);
        Assert.Equal(RoundState.RESULTS, snapshot.RoundState);
        var restored = (await restarted.Services.GetRequiredService<IWorldRepository>().GetWorldAsync(worldId))!;
        Assert.Equal(strengths, restored.Players.Select(p => (p.Id, p.Strength)).OrderBy(p => p.Id));
        Assert.Equal(changeKeys, restored.PlayerStrengthHistory.Select(e => e.IdempotencyKey));
        Assert.Equal(contractedSalary, restored.Players.Single(p => p.Id == playerId).SalaryPerRound);
    }
}
