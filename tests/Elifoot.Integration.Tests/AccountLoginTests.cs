using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Elifoot.Api;
using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.AspNetCore.Http.Connections;

namespace Elifoot.Integration.Tests;

public sealed class AccountLoginTests
{
    [Fact]
    public async Task SalaryDecisionIsOwnedPersistentAndConcurrentTabsProduceOnlyOneAuction()
    {
        await using var fixture=new Fixture();using var first=fixture.Server.CreateClient();using var second=fixture.Server.CreateClient();
        var coach=(await Register(first,"salary-owner","Owner")).GetProperty("coachId").GetGuid();
        await Register(second,"salary-other","Other");
        var created=await Send(first,"/api/worlds",new CreateWorldRequest("Salary QA"));created.EnsureSuccessStatusCode();
        var id=(await created.Content.ReadFromJsonAsync<LiveRoundSnapshotDto>())!.GameWorldId;
        (await Send(first,$"/api/worlds/{id}/join")).EnsureSuccessStatusCode();
        var repo=fixture.Server.Services.GetRequiredService<IWorldRepository>();var w=(await repo.GetWorldAsync(id))!;
        var c=w.Clubs.First(c=>c.HumanCoachId==coach);var p=w.Players.Last(p=>p.ClubId==c.Id);p.ContractRoundsRemaining=0;
        w.Auctions.Clear();var r=new Elifoot.Domain.SalaryRaiseRequest{Id=Guid.NewGuid(),WorldId=id,ClubId=c.Id,PlayerId=p.Id,Season=w.SeasonNumber,Round=w.RoundNumber,CurrentSalary=p.SalaryPerRound,RequestedSalary=p.SalaryPerRound+10};
        w.Economy.SalaryRequests.Add(r);await repo.SaveWorldAsync(w);
        var url=$"/api/worlds/{id}/clubs/{c.Id}/salary-requests/{r.Id}/respond";
        Assert.Equal(HttpStatusCode.Forbidden,(await Send(second,url,new SalaryDecisionCommandDto(coach,false,"unauthorized"))).StatusCode);
        var responses=await Task.WhenAll(Enumerable.Range(0,2).Select(i=>Send(first,url,new SalaryDecisionCommandDto(coach,false,"tab"+i))));
        Assert.All(responses,x=>Assert.True(x.IsSuccessStatusCode));Assert.Single(w.Auctions,a=>a.Id==r.AuctionId);
        Assert.NotNull(r.ResolvedByAccountId);Assert.Equal(1,r.DecisionVersion);Assert.Null(p.ClubId);
        await fixture.Restart();EfWorldRepository.ClearCacheForTesting();
        var restored=(await fixture.Server.Services.GetRequiredService<IWorldRepository>().GetWorldAsync(id))!;
        var saved=Assert.Single(restored.Economy.SalaryRequests);Assert.Equal(SalaryRequestStatus.REFUSED,saved.Status);
        Assert.Single(restored.Auctions,a=>a.Id==saved.AuctionId);Assert.InRange(restored.Auctions.Single(a=>a.Id==saved.AuctionId).DurationRounds!.Value,1,3);
    }
    private const string Password = "Teste-local-98!";

    [Fact]
    public async Task PersonalObservedPlayersPersistAfterRestartAndCannotBeReadOrChangedByAnotherAccount()
    {
        await using var fixture = new Fixture();
        using var first = fixture.Server.CreateClient();
        using var second = fixture.Server.CreateClient();
        var a = (await Register(first, "scout-a", "Scout A")).GetProperty("coachId").GetGuid();
        var b = (await Register(second, "scout-b", "Scout B")).GetProperty("coachId").GetGuid();
        var create = await Send(first, "/api/worlds", new CreateWorldRequest("Scouting", MaxHumans: 2));
        create.EnsureSuccessStatusCode();
        var world = (await create.Content.ReadFromJsonAsync<LiveRoundSnapshotDto>())!.GameWorldId;
        (await Send(first, $"/api/worlds/{world}/join?joinUnemployed=true")).EnsureSuccessStatusCode();
        (await Send(second, $"/api/worlds/{world}/join?joinUnemployed=true")).EnsureSuccessStatusCode();
        var repository = fixture.Server.Services.GetRequiredService<IWorldRepository>();
        var w = (await repository.GetWorldAsync(world))!;
        var player = w.Players[0].Id;
        w.Players[0].Name = "Daniel Dias";
        await repository.SaveWorldAsync(w);
        var query = $"/api/worlds/{world}/players/scouting?coachId={a}";
        Assert.Empty((await first.GetFromJsonAsync<PlayerScoutingDto>(query + "&name=Da"))!.Players);
        Assert.Contains((await first.GetFromJsonAsync<PlayerScoutingDto>(query + "&name=Dan"))!.Players, p => p.PlayerId == player);
        var observe = $"/api/worlds/{world}/players/{player}/observation";
        var request = new PlayerObservationRequest(a, true);
        (await Send(first, observe, request, HttpMethod.Put)).EnsureSuccessStatusCode();
        (await Send(first, observe, request, HttpMethod.Put)).EnsureSuccessStatusCode();
        Assert.Single((await first.GetFromJsonAsync<PlayerScoutingDto>(query + "&observedOnly=true"))!.Players);
        Assert.Equal(HttpStatusCode.Forbidden, (await second.GetAsync(query + "&observedOnly=true")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(second, observe, request, HttpMethod.Put)).StatusCode);
        Assert.Empty((await second.GetFromJsonAsync<PlayerScoutingDto>($"/api/worlds/{world}/players/scouting?coachId={b}&observedOnly=true"))!.Players);

        await fixture.Restart();
        EfWorldRepository.ClearCacheForTesting();
        using var reconnected = fixture.Server.CreateClient();
        await Login(reconnected, "scout-a");
        Assert.Equal(player, Assert.Single((await reconnected.GetFromJsonAsync<PlayerScoutingDto>(query + "&observedOnly=true"))!.Players).PlayerId);
        (await Send(reconnected, observe, request with { IsObserved = false }, HttpMethod.Put)).EnsureSuccessStatusCode();
        Assert.Empty((await reconnected.GetFromJsonAsync<PlayerScoutingDto>(query + "&observedOnly=true"))!.Players);
    }

    [Fact]
    public async Task RegistrationHashesPasswordsAssignsServerIdsAndSurvivesRestart()
    {
        await using var fixture = new Fixture();
        Guid id, coach;
        using (var client = fixture.Server.CreateClient())
        {
            var response = await Send(client, "/api/auth/register", new { username = "Daniel@example.test", displayName = "Daniel", password = Password, accountId = Guid.Empty });
            response.EnsureSuccessStatusCode();
            var json = await response.Content.ReadAsStringAsync(); var account = JsonDocument.Parse(json).RootElement;
            id = account.GetProperty("accountId").GetGuid(); coach = account.GetProperty("coachId").GetGuid();
            Assert.NotEqual(Guid.Empty, id); Assert.NotEqual(Guid.Empty, coach);
            Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("token", json, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(response.Headers.GetValues("Set-Cookie"), x => x.StartsWith("lc98.account=") && x.Contains("httponly", StringComparison.OrdinalIgnoreCase) && x.Contains("samesite=strict", StringComparison.OrdinalIgnoreCase));
            using var scope = fixture.Server.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<ElifootDbContext>();
            var stored = await db.Accounts.SingleAsync();
            Assert.NotEqual(Password, stored.PasswordHash);
            Assert.Equal(PasswordVerificationResult.Success, new PasswordHasher<AccountRecord>().VerifyHashedPassword(stored, stored.PasswordHash, Password));
            var duplicate = await Send(client, "/api/auth/register", new { username = "daniel@EXAMPLE.test", displayName = "Outro", password = Password });
            Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
            using var second = fixture.Server.CreateClient(); await Register(second, "outro", "Outro");
            var hashes = await db.Accounts.AsNoTracking().Select(x => x.PasswordHash).ToListAsync();
            Assert.Equal(2, hashes.Distinct().Count());
        }
        await fixture.Restart();
        using var reconnected = fixture.Server.CreateClient();
        var restored = await Login(reconnected, "daniel@example.test");
        Assert.Equal(id, restored.GetProperty("accountId").GetGuid()); Assert.Equal(coach, restored.GetProperty("coachId").GetGuid());
    }

    [Fact]
    public async Task AnonymousForgedIdentityCsrfWrongPasswordAndLogoutCannotBypassAuthentication()
    {
        await using var fixture = new Fixture(); using var client = fixture.Server.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/worlds?coachId=" + Guid.NewGuid())).StatusCode);
        client.DefaultRequestHeaders.Add("Authorization", "Bearer localdev-" + Guid.NewGuid().ToString("N"));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/profile/appearance")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/auth/local-login", new { nickname = "Daniel", deviceId = "old" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/register", new { username = "new", displayName = "Nome", password = Password })).StatusCode);
        var account = await Register(client, "conta", "Treinador Conta");
        // The existing Flutter creator sends Content-Type on GET with no body/length.
        using (var noBodyGet = new HttpRequestMessage(HttpMethod.Get, "/api/worlds") { Content = new UnknownLengthJson() })
            Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(noBodyGet)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        using var wrong = fixture.Server.CreateClient();
        var invalid = await Send(wrong, "/api/auth/login", new { username = "conta", password = "errada" });
        var missing = await Send(wrong, "/api/auth/login", new { username = "inexistente", password = "errada" });
        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
        Assert.Equal(await invalid.Content.ReadAsStringAsync(), await missing.Content.ReadAsStringAsync());
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        using var foreignOrigin = new HttpRequestMessage(HttpMethod.Post, "/api/auth/logout");
        foreignOrigin.Headers.Add("Origin", "https://untrusted.example"); foreignOrigin.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("csrfToken").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(foreignOrigin)).StatusCode);
        (await Send(client, "/api/auth/logout")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/session")).StatusCode);
        using var scope = fixture.Server.Services.CreateScope();
        Assert.NotNull((await scope.ServiceProvider.GetRequiredService<ElifootDbContext>().AccountSessions.SingleAsync(x => x.AccountId == account.GetProperty("accountId").GetGuid())).RevokedAtUtc);
    }

    [Fact]
    public async Task SameAccountOnTwoDevicesResumesFullRoomWithoutDuplicatingSeatsAndRejectsAnotherAccount()
    {
        await using var fixture = new Fixture(); using var first = fixture.Server.CreateClient(); using var second = fixture.Server.CreateClient(); using var stranger = fixture.Server.CreateClient();
        var owner = await Register(first, "proprietario", "Proprietário");
        var other = await Register(stranger, "visitante", "Visitante");
        var ownerId = owner.GetProperty("coachId").GetGuid(); var otherId = other.GetProperty("coachId").GetGuid();
        var created = await Send(first, "/api/worlds", new CreateWorldRequest("Sala conta", 1, 128, 199801)); created.EnsureSuccessStatusCode();
        var world = (await created.Content.ReadFromJsonAsync<LiveRoundSnapshotDto>())!.GameWorldId;
        (await Send(first, $"/api/worlds/{world}/join?coachId={ownerId}&nickname=Forged&deviceId=arbitrary")).EnsureSuccessStatusCode();
        var same = await Login(second, "PROPRIETARIO"); Assert.Equal(ownerId, same.GetProperty("coachId").GetGuid());
        (await Send(second, $"/api/worlds/{world}/join?coachId={ownerId}&nickname=OtherDevice&deviceId=another")).EnsureSuccessStatusCode();
        var rooms = await second.GetFromJsonAsync<JsonElement>("/api/worlds"); var room = rooms.EnumerateArray().Single();
        Assert.True(room.GetProperty("accountOwnsSeat").GetBoolean()); Assert.Equal(ownerId, room.GetProperty("reconnectCoachId").GetGuid());
        Assert.Equal(1, room.GetProperty("currentHumans").GetInt32()); Assert.Equal(0, room.GetProperty("availableSeats").GetInt32());
        Assert.Equal(0, room.GetProperty("connectedHumans").GetInt32()); Assert.Equal(0, room.GetProperty("readyCounter").GetProperty("readyCount").GetInt32());
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(stranger, $"/api/worlds/{world}/join?coachId={ownerId}&nickname=Proprietário")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(stranger, $"/api/worlds/{world}/tactics", new { coachId = ownerId })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await stranger.GetAsync($"/api/worlds?accountId={owner.GetProperty("accountId").GetGuid()}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(stranger, "/api/auth/profile", new { coachId = ownerId })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(stranger, $"/api/worlds/{world}/join?coachId={otherId}&nickname=Visitante")).StatusCode);
        var snapshot = await second.GetFromJsonAsync<LiveRoundSnapshotDto>($"/api/worlds/{world}/snapshot"); Assert.Single(snapshot!.ReadyCounter.Participants);
        Assert.Equal("Proprietário", snapshot.ReadyCounter.Participants.Single().Nickname);
        (await Send(first, "/api/profile/appearance", new { mode = "CLASSIC_98" }, HttpMethod.Put)).EnsureSuccessStatusCode();
        Assert.Equal("CLASSIC_98", (await second.GetFromJsonAsync<JsonElement>("/api/profile/appearance")).GetProperty("mode").GetString());
        Assert.Equal("CURRENT", (await stranger.GetFromJsonAsync<JsonElement>("/api/profile/appearance")).GetProperty("mode").GetString());
    }

    [Fact]
    public async Task LegacyCoachIsNeverClaimedByNameOrDeviceAndOnlyAnAuthorizedOneTimeGrantCanLinkIt()
    {
        await using var fixture = new Fixture();
        Guid world, legacyCoach = Guid.NewGuid(); string legacySave, legacyDevice = "preserve-this-legacy-device";
        using (var scope = fixture.Server.Services.CreateScope())
        {
            var game = scope.ServiceProvider.GetRequiredService<MultiplayerRoundOrchestrator>();
            world = (await game.CreateWorldAsync(new CreateWorldRequest("Legado", 1, 128, 199801))).Id;
            var joined = await game.JoinWorldAndAssignClubAsync(world, legacyCoach, "Mesmo Nome", legacyDevice);
            Assert.True(joined.Success);
            legacySave = await scope.ServiceProvider.GetRequiredService<ElifootDbContext>().Worlds.Where(x => x.Id == world).Select(x => x.SnapshotJson).SingleAsync();
        }
        using var owner = fixture.Server.CreateClient(); using var stranger = fixture.Server.CreateClient();
        await Register(owner, "destinatario", "Mesmo Nome"); await Register(stranger, "terceiro", "Mesmo Nome");
        var before = await owner.GetFromJsonAsync<JsonElement>($"/api/worlds?deviceId={legacyDevice}"); Assert.False(before.EnumerateArray().Single().GetProperty("accountOwnsSeat").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(owner, $"/api/worlds/{world}/join?coachId={legacyCoach}&deviceId={legacyDevice}&nickname=Mesmo Nome")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(owner, "/api/accounts/legacy-link-grants", new { username = "destinatario", coachId = legacyCoach, reason = "not approved" })).StatusCode);
        owner.DefaultRequestHeaders.Add("X-Account-Migration-Token", fixture.AdminToken);
        var grant = await Send(owner, "/api/accounts/legacy-link-grants", new { username = "destinatario", coachId = legacyCoach, reason = "Autorização explícita no teste isolado" }); grant.EnsureSuccessStatusCode();
        var code = (await grant.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString();
        Assert.Equal(HttpStatusCode.Conflict, (await Send(stranger, "/api/accounts/legacy-link", new { code })).StatusCode);
        (await Send(owner, "/api/accounts/legacy-link", new { code })).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Conflict, (await Send(owner, "/api/accounts/legacy-link", new { code })).StatusCode);
        using (var scope = fixture.Server.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ElifootDbContext>();
            Assert.Equal(legacySave, await db.Worlds.Where(x => x.Id == world).Select(x => x.SnapshotJson).SingleAsync());
            Assert.True(await db.AccountLinkAudit.AnyAsync(x => x.Outcome == "Rejected"));
        }
        (await Send(owner, "/api/auth/profile", new { coachId = legacyCoach })).EnsureSuccessStatusCode();
        (await Send(owner, $"/api/worlds/{world}/join?coachId={legacyCoach}")).EnsureSuccessStatusCode();
        using (var scope = fixture.Server.Services.CreateScope())
        {
            var saved = await scope.ServiceProvider.GetRequiredService<IWorldRepository>().GetWorldAsync(world);
            Assert.Single(saved!.HumanCoaches); Assert.Equal(legacyDevice, saved.HumanCoaches.Single().DeviceId);
        }
        Assert.Equal(HttpStatusCode.Conflict, (await Send(owner, "/api/accounts/legacy-link-grants", new { username = "terceiro", coachId = legacyCoach, reason = "Conflict" })).StatusCode);
    }

    [Fact]
    public async Task SignalRChecksAccountOwnershipSharesOneSeatAndLogoutRevokesTheLiveConnection()
    {
        await using var fixture = new Fixture();
        var jar = new CookieContainer(); using var first = fixture.Server.CreateDefaultClient(new CaptureCookies(jar));
        var secondJar = new CookieContainer(); using var second = fixture.Server.CreateDefaultClient(new CaptureCookies(secondJar));
        using var other = fixture.Server.CreateClient();
        var owner = await Register(first, "hub-owner", "Hub owner"); var foreign = await Register(other, "hub-other", "Hub other");
        await Login(second, "hub-owner");
        var created = await Send(first, "/api/worlds", new CreateWorldRequest("Hub conta", 1, 128, 199801)); created.EnsureSuccessStatusCode();
        var world = (await created.Content.ReadFromJsonAsync<LiveRoundSnapshotDto>())!.GameWorldId;
        var coach = owner.GetProperty("coachId").GetGuid();
        (await Send(first, $"/api/worlds/{world}/join?coachId={coach}")).EnsureSuccessStatusCode();
        async Task<HubConnection> Connect(HttpClient client, CookieContainer cookies)
        {
            var csrf = (await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf")).GetProperty("csrfToken").GetString()!;
            var hub = new HubConnectionBuilder().WithUrl("http://localhost/hubs/gameroom", options =>
            {
                options.Transports = HttpTransportType.LongPolling;
                options.HttpMessageHandlerFactory = _ => fixture.Server.Server.CreateHandler();
                options.Headers["Cookie"] = cookies.GetCookieHeader(new Uri("http://localhost"));
                options.Headers["X-CSRF-TOKEN"] = csrf;
            }).Build();
            await hub.StartAsync(); return hub;
        }
        await using var one = await Connect(first, jar); await using var two = await Connect(second, secondJar);
        Assert.NotNull(await one.InvokeAsync<LiveRoundSnapshotDto?>("JoinRoom", world, coach));
        Assert.NotNull(await two.InvokeAsync<LiveRoundSnapshotDto?>("JoinRoom", world, coach));
        await Assert.ThrowsAsync<Microsoft.AspNetCore.SignalR.HubException>(() => one.InvokeAsync<LiveRoundSnapshotDto?>("JoinRoom", world, foreign.GetProperty("coachId").GetGuid()));
        var shared = await first.GetFromJsonAsync<LiveRoundSnapshotDto>($"/api/worlds/{world}/snapshot");
        Assert.Single(shared!.ReadyCounter.Participants); Assert.True(shared.ReadyCounter.Participants.Single().IsConnected);
        var closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); one.Closed += _ => { closed.TrySetResult(); return Task.CompletedTask; };
        (await Send(first, "/api/auth/logout")).EnsureSuccessStatusCode();
        await closed.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var stillConnected = await second.GetFromJsonAsync<LiveRoundSnapshotDto>($"/api/worlds/{world}/snapshot");
        Assert.True(stillConnected!.ReadyCounter.Participants.Single().IsConnected);
        await two.StopAsync();
        var after = await second.GetFromJsonAsync<LiveRoundSnapshotDto>($"/api/worlds/{world}/snapshot");
        Assert.Single(after!.ReadyCounter.Participants); Assert.False(after.ReadyCounter.Participants.Single().IsConnected);
    }

    [Fact]
    public async Task FailedLoginAttemptsAreLimitedAndSecureCookiesAreUsedOnHttps()
    {
        await using var fixture = new Fixture(); using var setup = fixture.Server.CreateClient();
        await Register(setup, "limitada", "Limitada");
        using var wrong = fixture.Server.CreateClient();
        for (var i = 0; i < 5; i++) Assert.Equal(HttpStatusCode.Unauthorized, (await Send(wrong, "/api/auth/login", new { username = "limitada", password = "wrong" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(wrong, "/api/auth/login", new { username = "limitada", password = Password })).StatusCode);
        for (var i = 0; i < 15; i++) await Send(wrong, "/api/auth/login", new { username = "unknown", password = "wrong" });
        Assert.Equal(HttpStatusCode.TooManyRequests, (await Send(wrong, "/api/auth/login", new { username = "unknown", password = "wrong" })).StatusCode);
        await using var httpsFixture = new Fixture(); using var https = httpsFixture.Server.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost") });
        var registered = await Send(https, "/api/auth/register", new { username = "https", displayName = "HTTPS", password = Password }); registered.EnsureSuccessStatusCode();
        Assert.Contains(registered.Headers.GetValues("Set-Cookie"), c => c.StartsWith("lc98.account=") && c.Contains("secure", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<JsonElement> Register(HttpClient client, string username, string displayName)
    {
        var response = await Send(client, "/api/auth/register", new { username, displayName, password = Password }); response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
    private static async Task<JsonElement> Login(HttpClient client, string username)
    {
        var response = await Send(client, "/api/auth/login", new { username, password = Password }); response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }
    internal static async Task<HttpResponseMessage> Send(HttpClient client, string path, object? body = null, HttpMethod? method = null)
    {
        var token = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        using var request = new HttpRequestMessage(method ?? HttpMethod.Post, path);
        request.Headers.Add("X-CSRF-TOKEN", token.GetProperty("csrfToken").GetString());
        if (body != null) request.Content = JsonContent.Create(body);
        return await client.SendAsync(request);
    }
    private sealed class CaptureCookies(CookieContainer cookies) : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            var cookie = cookies.GetCookieHeader(request.RequestUri!); if (cookie.Length > 0) request.Headers.TryAddWithoutValidation("Cookie", cookie);
            var response = await base.SendAsync(request, ct);
            if (response.Headers.TryGetValues("Set-Cookie", out var values)) foreach (var value in values) cookies.SetCookies(request.RequestUri!, value);
            return response;
        }
    }
    private sealed class UnknownLengthJson : HttpContent
    {
        public UnknownLengthJson() => Headers.ContentType = new("application/json");
        protected override Task SerializeToStreamAsync(Stream stream, TransportContext? context) => Task.CompletedTask;
        protected override bool TryComputeLength(out long length) { length = 0; return false; }
    }
    private sealed class Fixture : IAsyncDisposable
    {
        private readonly string _file = Path.Combine(Path.GetTempPath(), $"elifoot-account-{Guid.NewGuid():N}.db");
        public string AdminToken { get; } = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
        public WebApplicationFactory<Program> Server { get; private set; }
        public Fixture() => Server = Start();
        private WebApplicationFactory<Program> Start() => new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseEnvironment("Development"); b.UseSetting("Elifoot:AuthMode", "AccountTest"); b.UseSetting("Elifoot:DatabaseProvider", "Sqlite");
            b.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={_file};Pooling=False"); b.UseSetting("Elifoot:AutoSeedDefaultWorld", "false");
            b.UseSetting("Elifoot:DisableBackgroundClock", "true"); b.UseSetting("Catalog013A:Enabled", "false"); b.UseSetting("Catalog016A:Enabled", "false"); b.UseSetting("Catalog016B:Enabled", "false");
            b.UseSetting("Accounts:MigrationAdminToken", AdminToken);
        });
        public async Task Restart() { await Server.DisposeAsync(); Server = Start(); }
        public async ValueTask DisposeAsync() { await Server.DisposeAsync(); File.Delete(_file); }
    }
}
