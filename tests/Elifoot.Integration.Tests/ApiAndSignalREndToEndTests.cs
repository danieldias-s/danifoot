using System.Net.Http.Json;
using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Domain;
using Elifoot.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elifoot.Integration.Tests;

public class ApiAndSignalREndToEndTests
{
    // Lifecycle test driver explicitly supplies the new human decision; production never auto-chooses it.
    internal static async Task<HttpResponseMessage> TickWithAutomaticPenaltyChoices(HttpClient client, Guid worldId, int ticks)
    {
        var snapshot=(await client.GetFromJsonAsync<LiveRoundSnapshotDto>($"/api/worlds/{worldId}/snapshot"))!;
        int target=snapshot.CurrentMinute+ticks;
        for(int guard=0;guard<128;guard++) {
            if(snapshot.PendingPenalty is {} pending) {
                var player=pending.EligiblePlayers.OrderByDescending(p=>p.IsStar).ThenByDescending(p=>p.Position==PlayerPosition.A).ThenByDescending(p=>p.Strength).First();
                var chosen=await client.PostAsJsonAsync($"/api/worlds/{worldId}/penalty-taker",new ChoosePenaltyTakerCommand(worldId,pending.CoachId,pending.DecisionId,player.PlayerId,$"lifecycle-{pending.DecisionId}"));
                chosen.EnsureSuccessStatusCode(); snapshot=(await chosen.Content.ReadFromJsonAsync<LiveRoundSnapshotDto>())!;
                continue;
            }
            if(snapshot.CurrentMinute>=target || snapshot.RoundState is not (RoundState.RUNNING_FIRST_HALF or RoundState.RUNNING_SECOND_HALF or RoundState.RUNNING_EXTRA_TIME))
                return new HttpResponseMessage(System.Net.HttpStatusCode.OK){Content=JsonContent.Create(snapshot)};
            var response=await client.PostAsync($"/api/worlds/{worldId}/tick?ticks={target-snapshot.CurrentMinute}",null);
            response.EnsureSuccessStatusCode(); snapshot=(await response.Content.ReadFromJsonAsync<LiveRoundSnapshotDto>())!;
        }
        throw new InvalidOperationException("Lifecycle driver could not reach its requested minute.");
    }

    [Fact]
    public async Task EndToEnd_ApiAndSignalR_6Humans_PauseAtMinute79_Reconnect_Market_Stadium_AndEditor()
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Elifoot:DatabaseProvider", "InMemory");
            builder.UseSetting("Elifoot:AutoSeedDefaultWorld", "false");
            builder.UseSetting("Elifoot:DisableBackgroundClock", "true");
        });

        using var client = factory.CreateClient();

        // 1. Health & LocalDev Auth
        var health = await client.GetAsync("/api/health");
        health.EnsureSuccessStatusCode();

        var authRes = await client.PostAsJsonAsync("/api/auth/local-login", new LocalAuthRequest("DanielHost", "dev-host-1"));
        authRes.EnsureSuccessStatusCode();
        var authDto = await authRes.Content.ReadFromJsonAsync<LocalAuthResponse>();
        Assert.NotNull(authDto);
        Assert.Contains("LOCAL_DEV", authDto!.AuthModeWarning);

        // 2. Create 128-club world
        var createRes = await client.PostAsJsonAsync("/api/worlds", new CreateWorldRequest(
            WorldName: "Sala E2E 6 Técnicos",
            MaxHumans: 8,
            TotalClubs: 128,
            Seed: 199801,
            InviteCode: "E2E98"));
        createRes.EnsureSuccessStatusCode();
        var initialSnap = await createRes.Content.ReadFromJsonAsync<LiveRoundSnapshotDto>();
        Assert.NotNull(initialSnap);
        Guid worldId = initialSnap!.GameWorldId;

        // The first round is a League round with all division clubs playing.
        var repo = factory.Services.GetRequiredService<IWorldRepository>();
        var worldEntity = await repo.GetWorldAsync(worldId);
        worldEntity!.RoundNumber = 1;
        await repo.SaveWorldAsync(worldEntity);

        // 3. Connect SignalR Hub client to observe real-time ReadyCounterUpdated & GlobalPause events
        var receivedCounters = new List<ReadyCounterDto>();
        bool globalPauseReceived = false;

        var hubConnection = new HubConnectionBuilder()
            .WithUrl("http://localhost/hubs/gameroom", options =>
            {
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
            })
            .Build();

        hubConnection.On<ReadyCounterDto>("ReadyCounterUpdated", c => receivedCounters.Add(c));
        hubConnection.On<object>("GlobalPause", _ => globalPauseReceived = true);

        await hubConnection.StartAsync();

        // 4. Join 6 humans via REST and verify 0/6 -> 6/6 via SignalR
        var round3Matches = MultiplayerRoundOrchestrator.GetCurrentRoundFixtures(worldEntity);
        var targetClubIds = round3Matches.Take(3).SelectMany(m => new[] { m.HomeClubId, m.AwayClubId }).ToList();
        var coachIds = new List<Guid>();

        for (int i = 0; i < 6; i++)
        {
            Guid cid = Guid.NewGuid();
            coachIds.Add(cid);
            var joinRes = await client.PostAsync(
                $"/api/worlds/{worldId}/join?coachId={cid}&nickname=Coach{i + 1}&deviceId=d{i + 1}&preferredClubId={targetClubIds[i]}",
                null);
            joinRes.EnsureSuccessStatusCode();
        }

        var hubSnap = await hubConnection.InvokeAsync<LiveRoundSnapshotDto>("JoinRoom", worldId, coachIds[0]);
        Assert.NotNull(hubSnap);
        Assert.Equal("TÁTICAS ENVIADAS: 0/6", hubSnap!.ReadyCounter.CounterLabel);

        // Submit tactics for all 6 coaches
        for (int i = 0; i < 6; i++)
        {
            var clubDto = await client.GetFromJsonAsync<ClubDetailDto>($"/api/worlds/{worldId}/clubs/{targetClubIds[i]}");
            Assert.NotNull(clubDto);

            var tacRes = await client.PostAsJsonAsync($"/api/worlds/{worldId}/tactics", new SubmitTacticsRequest(
                worldId,
                coachIds[i],
                clubDto!.PreferredFormation,
                clubDto.PreferredStarterIds,
                clubDto.PreferredBenchIds,
                $"e2e-tac-{i}",
                1));
            tacRes.EnsureSuccessStatusCode();
        }

        // Verify 6/6 started the round
        var runningSnap = await client.GetFromJsonAsync<LiveRoundSnapshotDto>($"/api/worlds/{worldId}/snapshot");
        Assert.Equal(RoundState.RUNNING_FIRST_HALF, runningSnap!.RoundState);
        Assert.NotEmpty(receivedCounters);

        // 5. Tick to Halftime (45'), confirm halftime, tick to Minute 79', and Pause at Minute 79'!
        var tick45Res = await TickWithAutomaticPenaltyChoices(client, worldId, 45);
        var snap45 = await tick45Res.Content.ReadFromJsonAsync<LiveRoundSnapshotDto>();
        Assert.Equal(45, snap45!.CurrentMinute);
        Assert.Equal(RoundState.HALFTIME_PAUSED, snap45.RoundState);

        for (int i = 0; i < 6; i++)
        {
            await client.PostAsJsonAsync($"/api/worlds/{worldId}/continue", new ConfirmContinueMatchCommand(worldId, coachIds[i], $"ht-{i}"));
        }

        var tick79Res = await TickWithAutomaticPenaltyChoices(client, worldId, 34); // 45 + 34 = 79'
        var snap79 = await tick79Res.Content.ReadFromJsonAsync<LiveRoundSnapshotDto>();
        Assert.Equal(79, snap79!.CurrentMinute);

        var pauseRes = await client.PostAsJsonAsync($"/api/worlds/{worldId}/pause", new RequestPauseCommand(worldId, coachIds[0], "pause-79"));
        pauseRes.EnsureSuccessStatusCode();
        await Task.Delay(50);
        Assert.True(globalPauseReceived);

        // Simulate disconnect & reconnect at minute 79 during GLOBAL_PAUSED -> state is reconstructed from snapshot
        await hubConnection.InvokeAsync("LeaveRoom", worldId, coachIds[0]);
        var reconnectedSnap = await hubConnection.InvokeAsync<LiveRoundSnapshotDto>("JoinRoom", worldId, coachIds[0]);
        Assert.Equal(79, reconnectedSnap!.CurrentMinute);
        Assert.Equal(RoundState.GLOBAL_PAUSED, reconnectedSnap.RoundState);
        Assert.NotNull(reconnectedSnap.ActivePauseWindow);

        // Confirm all 6 to resume from 79' and finish the round at 90'
        for (int i = 0; i < 6; i++)
        {
            await client.PostAsJsonAsync($"/api/worlds/{worldId}/continue", new ConfirmContinueMatchCommand(worldId, coachIds[i], $"c79-{i}"));
        }

        var tick90Res = await TickWithAutomaticPenaltyChoices(client, worldId, 15);
        var snap90 = await tick90Res.Content.ReadFromJsonAsync<LiveRoundSnapshotDto>();
        Assert.Equal(RoundState.RESULTS, snap90!.RoundState);

        // 6. Advance to next round preparation and verify Market, Stadium, Ranking, and Editor endpoints
        var nextRes = await client.PostAsync($"/api/worlds/{worldId}/next-round", null);
        nextRes.EnsureSuccessStatusCode();

        var standings = await client.GetFromJsonAsync<List<DivisionStandingRowDto>>($"/api/worlds/{worldId}/standings/{DivisionTier.PrimeiraDivisao}");
        Assert.Equal(8, standings!.Count);

        var exportPkg = await client.GetFromJsonAsync<WorldPackageExportDto>($"/api/worlds/{worldId}/editor/export-json");
        Assert.NotNull(exportPkg);
        Assert.Equal(128, exportPkg!.Clubs.Count);

        var valRes = await client.PostAsJsonAsync($"/api/worlds/{worldId}/editor/validate-package", exportPkg);
        var valDto = await valRes.Content.ReadFromJsonAsync<PackageValidationResultDto>();
        Assert.True(valDto!.IsValid);

        await hubConnection.DisposeAsync();
    }

    [Fact]
    public async Task PostgreSQL_Persistence_RoundtripAndCrashRecovery_WorksAgainstLivePostgres18()
    {
        string testDbName = $"elifoot_test_{Guid.NewGuid():N}"[..28];
        string testConn = Environment.GetEnvironmentVariable("ELIFOOT_013A_LEGACY_PG") is { Length: > 0 } isolated
            ? new Npgsql.NpgsqlConnectionStringBuilder(isolated) { Database = testDbName }.ConnectionString
            : throw new InvalidOperationException("Configure ELIFOOT_013A_LEGACY_PG via o runner isolado 013A/013B; o teste não usará o PostgreSQL principal.");

        var services = new ServiceCollection();
        services.AddDbContext<ElifootDbContext>(opt => opt.UseNpgsql(testConn));
        services.AddSingleton<IWorldRepository, EfWorldRepository>();
        using var provider = services.BuildServiceProvider();

        try
        {
            using (var scope = provider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ElifootDbContext>();
                await db.Database.EnsureCreatedAsync();
            }

            var repo = provider.GetRequiredService<IWorldRepository>();
            var orchestrator = new MultiplayerRoundOrchestrator(repo, NullMatchRoomNotificationPublisher.Instance);

            var created = await orchestrator.CreateWorldAsync(new CreateWorldRequest("Mundo Postgres 18", MaxHumans: 4, TotalClubs: 128, Seed: 199801));
            await orchestrator.JoinWorldAndAssignClubAsync(created.Id, Guid.NewGuid(), "TecnicoPG", "pg-dev-1");

            // Clear in-memory cache to simulate server process restart and reload from PostgreSQL
            EfWorldRepository.ClearCacheForTesting();

            var recovered = await repo.GetWorldAsync(created.Id);
            Assert.NotNull(recovered);
            Assert.Equal(128, recovered!.Clubs.Count);
            Assert.Single(recovered.HumanCoaches);
            Assert.Equal("TecnicoPG", recovered.HumanCoaches[0].Nickname);
        }
        finally
        {
            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ElifootDbContext>();
            await db.Database.EnsureDeletedAsync();
        }
    }

    [Theory]
    [InlineData(2, false)] [InlineData(4, false)] [InlineData(6, false)] [InlineData(8, false)]
    [InlineData(2, true)] [InlineData(4, true)] [InlineData(6, true)] [InlineData(8, true)]
    public async Task MultiplayerRooms_2_4_6_And_8_IndependentClients_Full15StepLifecycle_SignalR_Pause_Substitutions_DisconnectAndReconnect(int coachCount, bool importedWorld)
        => await RunRoomLifecycle(coachCount,importedWorld);

    [Theory][InlineData(2)][InlineData(4)][InlineData(6)][InlineData(8)]
    public async Task Final014B_RealClubsAndAuthorizedSimulatedSquadsFullMultiplayerLifecycle(int coachCount)
        => await RunRoomLifecycle(coachCount,true,true);

    private async Task RunRoomLifecycle(int coachCount,bool importedWorld,bool finalWorld=false)
    {
        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("Elifoot:DatabaseProvider", "InMemory");
            builder.UseSetting("Elifoot:AutoSeedDefaultWorld", "false");
            builder.UseSetting("Elifoot:DisableBackgroundClock", "true");
        });

        // Create N independent HTTP clients and N independent SignalR HubConnection clients (simulating N independent browser tabs/devices)
        var httpClients = new List<HttpClient>(coachCount);
        var hubClients = new List<HubConnection>(coachCount);
        var lastSnapshotByClient = new LiveRoundSnapshotDto?[coachCount];
        var lastCounterByClient = new ReadyCounterDto?[coachCount];

        try
        {
            for (int i = 0; i < coachCount; i++)
            {
                int clientIdx = i;
                httpClients.Add(factory.CreateClient());
                var hub = new HubConnectionBuilder()
                    .WithUrl("http://localhost/hubs/gameroom", options =>
                    {
                        options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                    })
                    .Build();
                hub.On<LiveRoundSnapshotDto>("RoundStateChanged", snap => lastSnapshotByClient[clientIdx] = snap);
                hub.On<ReadyCounterDto>("ReadyCounterUpdated", cnt => lastCounterByClient[clientIdx] = cnt);
                await hub.StartAsync();
                hubClients.Add(hub);
            }

            // Step 1: Create room with client[0] (Host)
            var createRes = await httpClients[0].PostAsJsonAsync("/api/worlds", new CreateWorldRequest(
                WorldName: $"Sala Audit 006 ({coachCount} Treinadores)",
                MaxHumans: 8,
                TotalClubs: 128,
                Seed: 199801 + coachCount,
                InviteCode: $"RM0{coachCount}"));
            createRes.EnsureSuccessStatusCode();
            var createdSnap = (await createRes.Content.ReadFromJsonAsync<LiveRoundSnapshotDto>())!;
            Guid worldId = createdSnap.GameWorldId;

            // Advance world to Round 3 (League Round 1 where all 32 division clubs play)
            var repo = factory.Services.GetRequiredService<IWorldRepository>();
            var worldEntity = (await repo.GetWorldAsync(worldId))!;
            if (importedWorld)
            {
                var gender=coachCount is 2 or 6 ? FootballGender.Female : FootballGender.Male;
                worldEntity = finalWorld ? Elifoot.Testing.Final014BWorlds.Create(gender)
                    : Elifoot.Testing.Imported014AWorlds.Create(gender,199801+coachCount);
                if(finalWorld)
                {
                    Assert.Equal(WorldDataMode.REAL_CLUBS_SIMULATED,worldEntity.CatalogOrigin!.DataMode);
                    Assert.All(worldEntity.Players,p=>Assert.NotNull(p.CatalogIdentity));
                }
                worldId = worldEntity.Id;
                await repo.SaveWorldAsync(worldEntity);
                Assert.NotNull(worldEntity.CatalogOrigin);
                Assert.Equal(OperatingEconomyEngine.CurrentPolicy, worldEntity.Economy.OperatingRules.Name);
            }
            worldEntity.RoundNumber = 1;
            await repo.SaveWorldAsync(worldEntity);

            var round3Fixtures = MultiplayerRoundOrchestrator.GetCurrentRoundFixtures(worldEntity);
            var clubPool = round3Fixtures.Take(4).SelectMany(m => new[] { m.HomeClubId, m.AwayClubId }).Take(coachCount).ToList();

            // Steps 2 & 3: Join with N independent coaches and choose clubs
            var coachIds = new List<Guid>(coachCount);
            for (int i = 0; i < coachCount; i++)
            {
                var authRes = await httpClients[i].PostAsJsonAsync("/api/auth/local-login",
                    new LocalAuthRequest($"Coach_{coachCount}_{i + 1}", $"dev-{coachCount}-{i + 1}"));
                authRes.EnsureSuccessStatusCode();
                var auth = (await authRes.Content.ReadFromJsonAsync<LocalAuthResponse>())!;
                coachIds.Add(auth.CoachId);

                var joinRes = await httpClients[i].PostAsync(
                    $"/api/worlds/{worldId}/join?coachId={auth.CoachId}&nickname={auth.Nickname}&deviceId={auth.DeviceId}&preferredClubId={clubPool[i]}",
                    null);
                joinRes.EnsureSuccessStatusCode();

                var hubJoinSnap = await hubClients[i].InvokeAsync<LiveRoundSnapshotDto>("JoinRoom", worldId, auth.CoachId);
                Assert.NotNull(hubJoinSnap);
            }

            // Step 7 (Initial): Verify 0/N counter on all N clients
            var prepSnap = (await httpClients[0].GetFromJsonAsync<LiveRoundSnapshotDto>($"/api/worlds/{worldId}/snapshot"))!;
            Assert.Equal($"TÁTICAS ENVIADAS: 0/{coachCount}", prepSnap.ReadyCounter.CounterLabel);
            Assert.Equal(0, prepSnap.ReadyCounter.ReadyCount);
            Assert.Equal(coachCount, prepSnap.ReadyCounter.TotalHumans);

            if (importedWorld)
            {
                var club = worldEntity.Clubs.Single(c => c.Id == clubPool[0]);
                decimal before = club.CashBalance;
                string bank = $"/api/worlds/{worldId}/clubs/{club.Id}/loans?coachId={coachIds[0]}&amount=500000&rounds=0&idempotencyKey=014a-reconnect";
                var loans = await Task.WhenAll(Enumerable.Range(0, coachCount).Select(_ => httpClients[0].PostAsync(bank, null)));
                Assert.All(loans, response => Assert.True(response.IsSuccessStatusCode));
                await hubClients[0].StopAsync(); await hubClients[0].StartAsync();
                await hubClients[0].InvokeAsync<LiveRoundSnapshotDto>("JoinRoom", worldId, coachIds[0]);
                Assert.True((await httpClients[0].PostAsync(bank, null)).IsSuccessStatusCode);
                Assert.Equal(before + 500000, club.CashBalance); Assert.Single(worldEntity.BankLoans);
                string foreignBank = bank.Replace($"clubs/{club.Id}", $"clubs/{clubPool[1]}").Replace("014a-reconnect", "014a-forbidden");
                Assert.False((await httpClients[0].PostAsync(foreignBank, null)).IsSuccessStatusCode);
                Assert.Single(worldEntity.BankLoans);
            }

            // Steps 4, 5, 6, 7 & 8: Prepare lineup, pick tactic, submit tactic for each of the N clients, verify counter progression and automatic start at N/N
            string[] formationChoices = ["4-4-2", "4-3-3", "3-5-2", "5-3-2", "3-4-3", "4-5-1", "5-4-1", "4-2-4"];
            for (int i = 0; i < coachCount; i++)
            {
                var clubDto = (await httpClients[i].GetFromJsonAsync<ClubDetailDto>($"/api/worlds/{worldId}/clubs/{clubPool[i]}"))!;
                string chosenForm = formationChoices[i % formationChoices.Length];

                var tacRes = await httpClients[i].PostAsJsonAsync($"/api/worlds/{worldId}/tactics", new SubmitTacticsRequest(
                    worldId,
                    coachIds[i],
                    chosenForm,
                    clubDto.PreferredStarterIds,
                    clubDto.PreferredBenchIds,
                    $"tac-{coachCount}-{i}",
                    prepSnap.RoundVersion));
                tacRes.EnsureSuccessStatusCode();

                var afterSubmitSnap = (await httpClients[i].GetFromJsonAsync<LiveRoundSnapshotDto>($"/api/worlds/{worldId}/snapshot"))!;
                if (i < coachCount - 1)
                {
                    Assert.Equal(RoundState.PREPARING, afterSubmitSnap.RoundState);
                    Assert.Equal(i + 1, afterSubmitSnap.ReadyCounter.ReadyCount);
                    Assert.Equal(coachCount, afterSubmitSnap.ReadyCounter.TotalHumans);
                }
                else
                {
                    // N/N reached -> Round starts automatically!
                    Assert.Equal(RoundState.RUNNING_FIRST_HALF, afterSubmitSnap.RoundState);
                    Assert.Equal(coachCount, afterSubmitSnap.ReadyCounter.ReadyCount);
                }
            }

            // Step 9: Follow matches up to Minute 34'
            var tick34Res = await TickWithAutomaticPenaltyChoices(httpClients[0], worldId, 34);
            tick34Res.EnsureSuccessStatusCode();
            var snap34 = (await tick34Res.Content.ReadFromJsonAsync<LiveRoundSnapshotDto>())!;
            Assert.Equal(34, snap34.CurrentMinute);
            Assert.Equal(RoundState.RUNNING_FIRST_HALF, snap34.RoundState);

            // Step 10: Request Global Pause from Client 0
            var pauseRes = await httpClients[0].PostAsJsonAsync($"/api/worlds/{worldId}/pause",
                new RequestPauseCommand(worldId, coachIds[0], $"pause-{coachCount}-34"));
            pauseRes.EnsureSuccessStatusCode();
            var pausedSnap = (await httpClients[0].GetFromJsonAsync<LiveRoundSnapshotDto>($"/api/worlds/{worldId}/snapshot"))!;
            Assert.Equal(RoundState.GLOBAL_PAUSED, pausedSnap.RoundState);
            Assert.Equal(34, pausedSnap.CurrentMinute);

            // Step 11: Make in-match substitution and formation adjustment during pause
            var match0 = pausedSnap.Matches.First(m => m.HomeClubId == clubPool[0] || m.AwayClubId == clubPool[0]);
            bool isHome0 = match0.HomeClubId == clubPool[0];
            var starters0 = isHome0 ? match0.HomeStarters : match0.AwayStarters;
            var bench0 = isHome0 ? match0.HomeBench : match0.AwayBench;
            var outP = starters0.First(p => p.Position != PlayerPosition.G && !p.IsSentOff);
            var inP = bench0.First(p => p.Position != PlayerPosition.G);

            var adjRes = await httpClients[0].PostAsJsonAsync($"/api/worlds/{worldId}/in-match-adjust",
                new SubmitInMatchAdjustmentCommand(
                    worldId,
                    coachIds[0],
                    match0.MatchId,
                    "4-3-3",
                    new List<InMatchSubstitutionDto> { new(outP.PlayerId, inP.PlayerId) },
                    $"adj-{coachCount}-34"));
            adjRes.EnsureSuccessStatusCode();

            // Simulate Browser Close / Disconnection of last client (coachCount - 1) and Reconnection
            int discIdx = coachCount - 1;
            await hubClients[discIdx].InvokeAsync("LeaveRoom", worldId, coachIds[discIdx]);
            var reconnectedSnap = await hubClients[discIdx].InvokeAsync<LiveRoundSnapshotDto>("JoinRoom", worldId, coachIds[discIdx]);
            Assert.NotNull(reconnectedSnap);
            Assert.Equal(34, reconnectedSnap!.CurrentMinute);
            Assert.Equal(RoundState.GLOBAL_PAUSED, reconnectedSnap.RoundState);

            // Step 12: All N coaches confirm continue to resume match
            for (int i = 0; i < coachCount; i++)
            {
                var contRes = await httpClients[i].PostAsJsonAsync($"/api/worlds/{worldId}/continue",
                    new ConfirmContinueMatchCommand(worldId, coachIds[i], $"cont34-{coachCount}-{i}"));
                contRes.EnsureSuccessStatusCode();
            }

            // Advance to Halftime (45'), confirm halftime, and finish at 90' (Step 13)
            var tick45Res = await TickWithAutomaticPenaltyChoices(httpClients[0], worldId, 11);
            var snap45 = (await tick45Res.Content.ReadFromJsonAsync<LiveRoundSnapshotDto>())!;
            Assert.Equal(45, snap45.CurrentMinute);
            Assert.Equal(RoundState.HALFTIME_PAUSED, snap45.RoundState);

            for (int i = 0; i < coachCount; i++)
            {
                await httpClients[i].PostAsJsonAsync($"/api/worlds/{worldId}/continue",
                    new ConfirmContinueMatchCommand(worldId, coachIds[i], $"cont45-{coachCount}-{i}"));
            }

            var tick90Res = await TickWithAutomaticPenaltyChoices(httpClients[0], worldId, 45);
            var snap90 = (await tick90Res.Content.ReadFromJsonAsync<LiveRoundSnapshotDto>())!;
            Assert.Equal(RoundState.RESULTS, snap90.RoundState);
            Assert.All(snap90.Matches, m => Assert.True(m.IsFinished));

            // Step 14: Verify all N independent clients consult identical results and RoundVersion
            for (int i = 0; i < coachCount; i++)
            {
                var clientView = (await httpClients[i].GetFromJsonAsync<LiveRoundSnapshotDto>($"/api/worlds/{worldId}/snapshot"))!;
                Assert.Equal(snap90.RoundVersion, clientView.RoundVersion);
                Assert.Equal(RoundState.RESULTS, clientView.RoundState);
                Assert.Equal(snap90.Matches.Count, clientView.Matches.Count);
            }

            // Step 15: Advance to next round and verify counter resets to 0/N
            var nextRoundRes = await httpClients[0].PostAsync($"/api/worlds/{worldId}/next-round", null);
            nextRoundRes.EnsureSuccessStatusCode();
            var nextSnap = (await nextRoundRes.Content.ReadFromJsonAsync<LiveRoundSnapshotDto>())!;
            Assert.Equal(2, nextSnap.RoundNumber);
            Assert.Equal(RoundState.PREPARING, nextSnap.RoundState);
            Assert.Equal(0, nextSnap.ReadyCounter.ReadyCount);
            Assert.Equal(coachCount, nextSnap.ReadyCounter.TotalHumans);
        }
        finally
        {
            foreach (var h in hubClients) await h.DisposeAsync();
            foreach (var c in httpClients) c.Dispose();
        }
    }
}
