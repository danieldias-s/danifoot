using System.Net.Http.Json;
using System.Text.Json;
using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Domain;
using Elifoot.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Elifoot.Integration.Tests;

public class CoachCareer008IntegrationTests
{
    private static WebApplicationFactory<Program> Factory()=>new WebApplicationFactory<Program>().WithWebHostBuilder(b=>{
        b.UseSetting("Elifoot:AuthMode", "LocalDev"); b.UseSetting("Elifoot:DatabaseProvider","InMemory");b.UseSetting("Elifoot:AutoSeedDefaultWorld","false");b.UseSetting("Elifoot:DisableBackgroundClock","true");
    });

    private static async Task<(GameWorld W,HumanCoach H)> Prepare(WebApplicationFactory<Program> f,HttpClient client)
    {
        var auth=await client.PostAsJsonAsync("/api/auth/local-login",new LocalAuthRequest("Carreira 008","qa008-web"));
        var identity=(await auth.Content.ReadFromJsonAsync<LocalAuthResponse>())!;
        var w=WorldSeedFactory.CreateSeededWorld("Carreira 008 — validação privada",199801,tickIntervalMs:100,pauseTimeoutSeconds:60,inviteCode:"CAREER008");w.RoundNumber=1;
        var repo=f.Services.GetRequiredService<IWorldRepository>();await repo.SaveWorldAsync(w);
        var o=new MultiplayerRoundOrchestrator(repo,NullMatchRoomNotificationPublisher.Instance);
        var join=await o.JoinWorldAndAssignClubAsync(w.Id,identity.CoachId,identity.Nickname,identity.DeviceId,joinUnemployed:true);
        var h=join.Coach!;h.CoachPoints=12.5m;h.LeagueTitles=1;h.CupTitles=1;
        foreach(var a in w.AiManagers)a.Career.Morale=20;
        CoachCareerEngine.OpenVacancy(w,w.Clubs[24]);CoachCareerEngine.OpenVacancy(w,w.Clubs[25]);CoachCareerEngine.ProcessVacancies(w);
        await repo.SaveWorldAsync(w);return(w,h);
    }

    [Theory] [InlineData(true)] [InlineData(false)]
    public async Task HumanCanAcceptOrRejectThroughHttpAndReceivesUpdatedOwnership(bool accept)
    {
        await using var f=Factory();using var client=f.CreateClient();var(w,h)=await Prepare(f,client);
        var invite=w.JobInvitations.First(i=>i.CoachId==h.Id);
        var next=new HumanCoach { Id=CoachCareerEngine.StableId("008-next-human"),GameWorldId=w.Id,Nickname="Próximo candidato",CoachPoints=100 };
        if(!accept)w.HumanCoaches.Add(next);
        var response=await client.PostAsync($"/api/worlds/{w.Id}/invitations/{invite.Id}/{(accept?"accept":"reject")}?coachId={h.Id}",null);
        response.EnsureSuccessStatusCode();
        Assert.Equal(accept,invite.IsAccepted);Assert.Equal(!accept,invite.IsRejected);
        if(accept){var club=w.Clubs.Single(c=>c.Id==invite.ClubId);Assert.Null(club.AiManagerId);Assert.Equal(h.Id,club.HumanCoachId);Assert.Equal(club.Id,h.CurrentClubId);}
        else
        {
            Assert.True(h.IsUnemployed);
            Assert.Single(w.JobInvitations,i=>i.VacancyId==invite.VacancyId&&i.CoachId==next.Id&&!i.IsAccepted&&!i.IsRejected&&!i.IsExpired);
            Assert.NotNull(w.Clubs.Single(c=>c.Id==invite.ClubId).AiManagerId);
        }
        var again=await client.PostAsync($"/api/worlds/{w.Id}/invitations/{invite.Id}/accept?coachId={h.Id}",null);Assert.False(again.IsSuccessStatusCode);
        var career=JsonDocument.Parse(await client.GetStringAsync($"/api/worlds/{w.Id}/invitations/{h.Id}"));
        Assert.Equal("VERDE",career.RootElement.GetProperty("moraleBand").GetString());
    }

    [Fact]
    public async Task ConcurrentAcceptancesCannotDuplicateClubOrCoach_AndInFlightChangesAreRejected()
    {
        await using var f=Factory();using var client=f.CreateClient();var(w,h)=await Prepare(f,client);
        var other=new HumanCoach{Id=CoachCareerEngine.StableId("008-second"),Nickname="Segundo",GameWorldId=w.Id,CoachPoints=100};w.HumanCoaches.Add(other);
        var first=w.JobInvitations.First(i=>i.CoachId==h.Id);
        // Emulates an old/concurrent stale issuance; both requests must still serialize against the clock lock.
        var second=new JobInvitation{CoachId=other.Id,ClubId=first.ClubId,VacancyId=first.VacancyId,ExpiresAtOrdinal=first.ExpiresAtOrdinal};w.JobInvitations.Add(second);
        var replies=await Task.WhenAll(client.PostAsync($"/api/worlds/{w.Id}/invitations/{first.Id}/accept?coachId={h.Id}",null),
            client.PostAsync($"/api/worlds/{w.Id}/invitations/{second.Id}/accept?coachId={other.Id}",null));
        Assert.Single(replies,r=>r.IsSuccessStatusCode);
        var club=w.Clubs.Single(c=>c.Id==first.ClubId);Assert.True(club.HumanCoachId==h.Id||club.HumanCoachId==other.Id);Assert.Null(club.AiManagerId);
        Assert.Equal(1,w.HumanCoaches.Count(c=>c.CurrentClubId==club.Id));
        w.RoundState=RoundState.RUNNING_FIRST_HALF;
        var blocked=await client.PostAsync($"/api/worlds/{w.Id}/invitations/{first.Id}/accept?coachId={first.CoachId}",null);
        Assert.False(blocked.IsSuccessStatusCode);
        using var error=JsonDocument.Parse(await blocked.Content.ReadAsStringAsync());
        Assert.Equal("Trocas somente entre partidas.",error.RootElement.GetProperty("error").GetString());
    }

    [Theory] [InlineData(20)] [InlineData(50)]
    public async Task PendingOfferCannotBeAcceptedAfterMoraleFallsBelowGreen(int morale)
    {
        await using var f=Factory();using var client=f.CreateClient();var(w,h)=await Prepare(f,client);
        var offer=w.JobInvitations.First(i=>i.CoachId==h.Id);h.Career.Morale=morale;
        var response=await client.PostAsync($"/api/worlds/{w.Id}/invitations/{offer.Id}/accept?coachId={h.Id}",null);
        Assert.False(response.IsSuccessStatusCode);Assert.True(h.IsUnemployed);
    }

    [Fact]
    public async Task HostCanConfigureMoraleBandsWithValidationAndUnemployedCountsInReadyCounter()
    {
        await using var f=Factory();using var client=f.CreateClient();var(w,h)=await Prepare(f,client);
        var update=await client.PutAsJsonAsync($"/api/worlds/{w.Id}/career/morale-rules?coachId={h.Id}",new CoachMoraleRules{GreenMinimum=70,YellowMinimum=40,UnemployedRecoveryPerRound=5});update.EnsureSuccessStatusCode();
        Assert.Equal(70,w.CoachMoraleRules.GreenMinimum);
        var invalid=await client.PutAsJsonAsync($"/api/worlds/{w.Id}/career/morale-rules?coachId={h.Id}",new CoachMoraleRules{GreenMinimum=20,YellowMinimum=40});Assert.False(invalid.IsSuccessStatusCode);
        var o=new MultiplayerRoundOrchestrator(f.Services.GetRequiredService<IWorldRepository>(),NullMatchRoomNotificationPublisher.Instance);
        var ready=await o.SubmitUnemployedReadyAsync(new(w.Id,h.Id,"008-ready",w.RoundVersion));
        Assert.True(ready.Success,ready.Error);Assert.Equal(1,ready.Counter!.TotalHumans);Assert.Equal(1,ready.Counter.ReadyCount);
    }

    [Fact]
    public async Task SqliteRestartPreservesRankingPendingOffersAndCareer_AndWritesPrivateBrowserFixture()
    {
        await using var f=Factory();using var client=f.CreateClient();var(w,h)=await Prepare(f,client);
        string root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../"));
        string artifacts=Path.Combine(root,"artifacts","coach-career-008");Directory.CreateDirectory(artifacts);
        string database=Path.Combine(artifacts,$"browser-{Guid.NewGuid():N}.db");
        var services=new ServiceCollection();services.AddDbContext<ElifootDbContext>(o=>o.UseSqlite($"Data Source={database}"));
        await using var provider=services.BuildServiceProvider();
        using(var scope=provider.CreateScope())await scope.ServiceProvider.GetRequiredService<ElifootDbContext>().Database.EnsureCreatedAsync();
        var repository=new EfWorldRepository(provider.GetRequiredService<IServiceScopeFactory>());await repository.SaveWorldAsync(w);
        var ranking=CompetitionAndSeasonEngine.GetClassicCoachRanking(w);
        var ids=w.JobInvitations.Where(i=>i.CoachId==h.Id).Select(i=>i.Id).ToArray();Assert.Equal(2,ids.Length);
        EfWorldRepository.ClearCacheForTesting();var loaded=(await repository.GetWorldAsync(w.Id))!;
        Assert.Equal(ids,loaded.JobInvitations.Where(i=>i.CoachId==h.Id).Select(i=>i.Id).ToArray());
        Assert.Equal(ranking,CompetitionAndSeasonEngine.GetClassicCoachRanking(loaded));Assert.Equal(12.5m,loaded.HumanCoaches.Single().CoachPoints);
        Assert.Equal(70,loaded.HumanCoaches.Single().Career.Morale);
        var output=Environment.GetEnvironmentVariable("ELIFOOT_AUDIT_OUTPUT") ?? Path.Combine(root,"docs","audit-008");Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(output,"browser-fixture.json"),JsonSerializer.Serialize(new{
            DatabasePath=database,WorldId=w.Id,CoachId=h.Id,Nickname=h.Nickname,DeviceId=h.DeviceId,
            InvitationIds=ids,Label="Private QA fixture with pre-existing career aggregates and two real pending invitations; not evidence that seeded titles were won in the browser."
        },new JsonSerializerOptions{WriteIndented=true}));
    }
}
