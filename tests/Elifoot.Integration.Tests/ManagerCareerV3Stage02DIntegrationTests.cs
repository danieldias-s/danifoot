using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Domain;
using Elifoot.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace Elifoot.Integration.Tests;

/// <summary>
/// V3 — Etapa 02D: Integration tests for Coach Career, AccountId Identity, Invitations,
/// Club Switching, Dismissals, Concurrency, Idempotency, and SQLite Restart Recovery.
/// </summary>
public class ManagerCareerV3Stage02DIntegrationTests
{
    private static WebApplicationFactory<Program> CreateLocalDevFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("Elifoot:AuthMode", "LocalDev");
            b.UseSetting("Elifoot:DatabaseProvider", "InMemory");
            b.UseSetting("Elifoot:AutoSeedDefaultWorld", "false");
            b.UseSetting("Elifoot:DisableBackgroundClock", "true");
        });

    private static WebApplicationFactory<Program> CreateAccountTestFactory(string sqlitePath) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("Elifoot:AuthMode", "AccountTest");
            b.UseSetting("Elifoot:DatabaseProvider", "Sqlite");
            b.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={sqlitePath}");
            b.UseSetting("Elifoot:AutoSeedDefaultWorld", "false");
            b.UseSetting("Elifoot:DisableBackgroundClock", "true");
            b.UseSetting("Accounts:MigrationAdminToken", "admin-migration-token-02d");
        });

    private static async Task<HttpClient> CreateCsrfAuthenticatedClientAsync(
        WebApplicationFactory<Program> factory,
        string username,
        string displayName,
        string password,
        bool register = true)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://127.0.0.1:5242"),
            HandleCookies = true
        });
        var csrfRes = await client.GetAsync("/api/auth/csrf");
        csrfRes.EnsureSuccessStatusCode();
        using var csrfDoc = JsonDocument.Parse(await csrfRes.Content.ReadAsStringAsync());
        string csrfToken = csrfDoc.RootElement.GetProperty("csrfToken").GetString()!;
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrfToken);

        var authRes = register
            ? await client.PostAsJsonAsync("/api/auth/register", new { username, displayName, password })
            : await client.PostAsJsonAsync("/api/auth/login", new { username, password });
        authRes.EnsureSuccessStatusCode();

        // Refresh CSRF token after authentication cookie is issued
        var csrfAfter = await client.GetAsync("/api/auth/csrf");
        csrfAfter.EnsureSuccessStatusCode();
        using var csrfAfterDoc = JsonDocument.Parse(await csrfAfter.Content.ReadAsStringAsync());
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", csrfAfterDoc.RootElement.GetProperty("csrfToken").GetString()!);
        return client;
    }

    [Fact]
    public async Task AccountIdIdentity_PreservedAcrossDevicesClubSwitchAndDismissal_AndPreventsDuplicateCoachInSameWorld()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "elifoot98-v3-stage02d-tests");
        Directory.CreateDirectory(tempDir);
        string dbPath = Path.Combine(tempDir, $"account-career-{Guid.NewGuid():N}.db");

        await using var factory = CreateAccountTestFactory(dbPath);
        using var deviceAClient = await CreateCsrfAuthenticatedClientAsync(
            factory, "treinador02d", "Tecnico 02D", "SenhaForte#1998", register: true);

        using var sessionDocA = JsonDocument.Parse(await deviceAClient.GetStringAsync("/api/auth/session"));
        Guid accountId = sessionDocA.RootElement.GetProperty("accountId").GetGuid();
        Guid primaryCoachId = sessionDocA.RootElement.GetProperty("coachId").GetGuid();

        var repo = factory.Services.GetRequiredService<IWorldRepository>();
        var world = WorldSeedFactory.CreateSeededWorld("Mundo Conta 02D", 199801, inviteCode: "ACC02D");
        world.RoundNumber = 3;
        var initialClub = world.Clubs.First(c => c.DivisionTier == DivisionTier.QuartaDivisao);
        var nextClub = world.Clubs.First(c => c.DivisionTier == DivisionTier.SegundaDivisao);
        await repo.SaveWorldAsync(world);

        // Join world from Device A
        var joinRes = await deviceAClient.PostAsync(
            $"/api/worlds/{world.Id}/join?coachId={primaryCoachId}&nickname=Tecnico02D&deviceId=spoofed-device-A&preferredClubId={initialClub.Id}", null);
        joinRes.EnsureSuccessStatusCode();

        // Login from Device B (separate HTTP client + cookie container) using same account
        using var deviceBClient = await CreateCsrfAuthenticatedClientAsync(
            factory, "treinador02d", "Tecnico 02D", "SenhaForte#1998", register: false);

        using var sessionDocB = JsonDocument.Parse(await deviceBClient.GetStringAsync("/api/auth/session"));
        Assert.Equal(accountId, sessionDocB.RootElement.GetProperty("accountId").GetGuid());
        Assert.Equal(primaryCoachId, sessionDocB.RootElement.GetProperty("coachId").GetGuid());

        // Reconnect to world from Device B -> must NOT create a second HumanCoach
        var reconnectRes = await deviceBClient.PostAsync(
            $"/api/worlds/{world.Id}/join?coachId={primaryCoachId}&nickname=OutroNome&deviceId=device-B", null);
        reconnectRes.EnsureSuccessStatusCode();

        var reloadedWorld = (await repo.GetWorldAsync(world.Id))!;
        Assert.Single(reloadedWorld.HumanCoaches);
        var human = reloadedWorld.HumanCoaches.Single();
        Assert.Equal(primaryCoachId, human.Id);
        Assert.Equal(initialClub.Id, human.CurrentClubId);

        // Grant experience and open vacancy at nextClub; accept invitation from Device B
        human.LeagueTitles = 1;
        human.CoachPoints = 70m;
        human.Career.Morale = 85;
        foreach (var ai in reloadedWorld.AiManagers) ai.Career.Morale = 20;
        CoachCareerEngine.OpenVacancy(reloadedWorld, nextClub);
        CoachCareerEngine.ProcessVacancies(reloadedWorld);
        await repo.SaveWorldAsync(reloadedWorld);

        var careerDto = await deviceBClient.GetFromJsonAsync<CoachCareerSnapshotDto>($"/api/worlds/{world.Id}/invitations/{primaryCoachId}");
        Assert.NotNull(careerDto);
        var invite = Assert.Single(careerDto!.Invitations, i => i.ClubId == nextClub.Id);

        var acceptRes = await deviceBClient.PostAsync(
            $"/api/worlds/{world.Id}/invitations/{invite.InvitationId}/accept?coachId={primaryCoachId}&idempotencyKey=acc-switch-01", null);
        acceptRes.EnsureSuccessStatusCode();

        // Verify AccountId & CoachId remain identical after club switch
        using var sessionAfterSwitch = JsonDocument.Parse(await deviceBClient.GetStringAsync("/api/auth/session"));
        Assert.Equal(accountId, sessionAfterSwitch.RootElement.GetProperty("accountId").GetGuid());
        Assert.Equal(primaryCoachId, sessionAfterSwitch.RootElement.GetProperty("coachId").GetGuid());

        // Verify old club finances endpoint is now forbidden (403) to this account, while new club finances is allowed (200)
        var oldClubFinances = await deviceBClient.GetAsync($"/api/worlds/{world.Id}/clubs/{initialClub.Id}/finances");
        Assert.Equal(HttpStatusCode.Forbidden, oldClubFinances.StatusCode);

        var newClubFinances = await deviceBClient.GetAsync($"/api/worlds/{world.Id}/clubs/{nextClub.Id}/finances");
        Assert.Equal(HttpStatusCode.OK, newClubFinances.StatusCode);

        // Dismiss coach and verify AccountId & CoachId remain identical and newClub finances is now forbidden (403)
        reloadedWorld = (await repo.GetWorldAsync(world.Id))!;
        Assert.True(CoachCareerEngine.Dismiss(reloadedWorld, primaryCoachId, reloadedWorld.Clubs.Single(c => c.Id == nextClub.Id), "RED_MORALE_LOSS"));
        await repo.SaveWorldAsync(reloadedWorld);

        using var sessionAfterDismiss = JsonDocument.Parse(await deviceBClient.GetStringAsync("/api/auth/session"));
        Assert.Equal(accountId, sessionAfterDismiss.RootElement.GetProperty("accountId").GetGuid());
        Assert.Equal(primaryCoachId, sessionAfterDismiss.RootElement.GetProperty("coachId").GetGuid());

        var dismissedClubFinances = await deviceBClient.GetAsync($"/api/worlds/{world.Id}/clubs/{nextClub.Id}/finances");
        Assert.Equal(HttpStatusCode.Forbidden, dismissedClubFinances.StatusCode);
    }

    [Fact]
    public async Task HttpInvitationIdempotencyAndSqliteRestart_PreservesClubSwitchAndEmploymentHistory()
    {
        string tempDir = Path.Combine(Path.GetTempPath(), "elifoot98-v3-stage02d-tests");
        Directory.CreateDirectory(tempDir);
        string dbPath = Path.Combine(tempDir, $"restart-career-{Guid.NewGuid():N}.db");

        await using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b =>
        {
            b.UseSetting("Elifoot:AuthMode", "LocalDev");
            b.UseSetting("Elifoot:DatabaseProvider", "Sqlite");
            b.UseSetting("ConnectionStrings:DefaultConnection", $"Data Source={dbPath}");
            b.UseSetting("Elifoot:AutoSeedDefaultWorld", "false");
            b.UseSetting("Elifoot:DisableBackgroundClock", "true");
        });

        using var client = factory.CreateClient();
        var repo = factory.Services.GetRequiredService<IWorldRepository>();
        var world = WorldSeedFactory.CreateSeededWorld("Restart 02D", 199801);
        world.RoundNumber = 3;
        var clubA = world.Clubs.First(c => c.DivisionTier == DivisionTier.QuartaDivisao);
        var clubB = world.Clubs.First(c => c.DivisionTier == DivisionTier.TerceiraDivisao);
        clubB.Reputation = 85;
        clubB.CashBalance = 12_000_000m;
        clubB.Stadium.BuiltCapacity = 45_000;
        await repo.SaveWorldAsync(world);

        Guid coachId = CoachCareerEngine.StableId("http-idem-restart-coach");
        var joinRes = await client.PostAsync(
            $"/api/worlds/{world.Id}/join?coachId={coachId}&nickname=IdemCoach&deviceId=dev-idem&preferredClubId={clubA.Id}", null);
        joinRes.EnsureSuccessStatusCode();

        var w = (await repo.GetWorldAsync(world.Id))!;
        var h = w.HumanCoaches.Single(c => c.Id == coachId);
        h.Career.Morale = 80;
        foreach (var ai in w.AiManagers) ai.Career.Morale = 20;
        CoachCareerEngine.OpenVacancy(w, w.Clubs.Single(c => c.Id == clubB.Id));
        CoachCareerEngine.ProcessVacancies(w);
        await repo.SaveWorldAsync(w);

        var careerBefore = await client.GetFromJsonAsync<CoachCareerSnapshotDto>($"/api/worlds/{world.Id}/invitations/{coachId}");
        Assert.NotNull(careerBefore);
        var invite = Assert.Single(careerBefore!.Invitations);

        // First accept with idempotencyKey
        var accept1 = await client.PostAsync(
            $"/api/worlds/{world.Id}/invitations/{invite.InvitationId}/accept?coachId={coachId}&idempotencyKey=idem-accept-02d", null);
        accept1.EnsureSuccessStatusCode();

        // Second accept with the SAME idempotencyKey (network retry) -> 200 OK idempotent!
        var accept2 = await client.PostAsync(
            $"/api/worlds/{world.Id}/invitations/{invite.InvitationId}/accept?coachId={coachId}&idempotencyKey=idem-accept-02d", null);
        accept2.EnsureSuccessStatusCode();

        // Simulate server restart by clearing EfWorldRepository cache and reloading from SQLite
        EfWorldRepository.ClearCacheForTesting();

        var careerAfterRestart = await client.GetFromJsonAsync<CoachCareerSnapshotDto>($"/api/worlds/{world.Id}/invitations/{coachId}");
        Assert.NotNull(careerAfterRestart);
        Assert.Equal(clubB.Id, careerAfterRestart!.CurrentClubId);
        Assert.Equal(2, careerAfterRestart.EmploymentHistory.Count);
        Assert.Equal(clubA.Id, careerAfterRestart.EmploymentHistory[0].ClubId);
        Assert.Equal(clubB.Id, careerAfterRestart.EmploymentHistory[1].ClubId);
        Assert.Null(careerAfterRestart.EmploymentHistory[1].EndSeason);
    }
}
