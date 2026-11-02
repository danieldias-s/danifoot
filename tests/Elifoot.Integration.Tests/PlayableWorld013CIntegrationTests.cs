using System.Net.Http.Json;
using System.Text.Json;
using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Domain;
using Elifoot.Infrastructure;
using Elifoot.Infrastructure.Catalog;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace Elifoot.Integration.Tests;

[Collection("Player rankings 009 SQLite")]
public sealed class PlayableWorld013CIntegrationTests
{
    private static readonly CatalogUsageRights TestRights=new("Synthetic repository fixture","Invented integration-test data; no external provider rights claimed",true,true,true,true,true,true,true);
    private static WebApplicationFactory<Program> Factory(string catalog,string worlds) => new WebApplicationFactory<Program>().WithWebHostBuilder(b=>{
        b.UseSetting("Elifoot:DatabaseProvider","PostgreSQL"); b.UseSetting("ConnectionStrings:DefaultConnection",worlds);
        b.UseSetting("ConnectionStrings:Catalog013A",catalog); b.UseSetting("Catalog013A:Enabled","true");
        b.UseSetting("Catalog013B:Enabled","true"); b.UseSetting("Catalog013A:LoadFixtures","false");
        b.UseSetting("Elifoot:AutoSeedDefaultWorld","false");b.UseSetting("Elifoot:DisableBackgroundClock","true");b.UseSetting("Elifoot:AuthMode","LocalDev");
    });
    private static async Task Populate(CatalogSyncService sync,FootballGender gender)
    {
        // Invented fixtures exercise real-data contracts; never counted as actual football coverage.
        foreach(var original in CatalogFixtureFactory.Packages().Where(p=>p.Scope.Gender==gender))
        {
            var package=original with {IsFixture=false,Rights=TestRights,LicenseReference="Synthetic 013C integration test data owned by repository"};
            if(package.Scope.DataType==CatalogDataType.Clubs)
                package=package with {Clubs=package.Clubs.Select(c=>c with {Verification=new(CatalogClubVerificationStatus.REAL_CLUB_VERIFIED,"Synthetic test verification",package.UpdatedAt,"2026",true)}).ToList()};
            if(package.Scope.DataType==CatalogDataType.Squads)
            {
                var athletes=package.Scope.CountryCode=="BR" ? package.Players.Take(1).Select(p=>p with {IdentityOrigin=CatalogDataOrigin.Real,
                    StrengthEstimate=new(InitialPlayerStrength.Version+":pending",CatalogDataOrigin.Estimated,.2,"synthetic-test-input",.5,null),
                    MembershipStart=new(2020,1,1),MembershipEnd=new(2030,1,1),MembershipEvidence=new(CatalogMembershipStatus.DatedActive,"synthetic-test-claim","Synthetic test evidence",new(2026,10,30))}).ToList() : [];
                var reports=original.Players.GroupBy(p=>p.ClubExternalId).Select(g=>new CatalogRosterReport(g.Key,g.First().ClubProvider,
                    athletes.Count(p=>p.ClubExternalId==g.Key),athletes.Count(p=>p.ClubExternalId==g.Key),false,["Synthetic partial roster; not actual football coverage"])).ToList();
                package=package with {Players=athletes,ExpectedItems=athletes.Count,AllowPartialSquads=true,RosterAsOf=new(2026,10,30),RosterReports=reports};
            }
            if(package.Scope.DataType==CatalogDataType.Rankings)
                package=package with {Rankings=package.Rankings.Select(r=>r with {Method="synthetic-relative-ranking",Origin=CatalogDataOrigin.Estimated,Confidence=.8,ComparableGroup="synthetic-test-cohort"}).ToList()};
            await sync.ImportAsync(package);
        }
    }
    private static async Task<LiveRoundSnapshotDto> Snapshot(HttpClient client,Guid world) => (await client.GetFromJsonAsync<LiveRoundSnapshotDto>($"/api/worlds/{world}/snapshot"))!;
    private static async Task<WorldCreatorPreview> Preview(HttpClient client,WorldCreatorRequest request)
    {var response=await client.PostAsJsonAsync("/api/world-creator/preview",request);Assert.True(response.IsSuccessStatusCode,await response.Content.ReadAsStringAsync());return (await response.Content.ReadFromJsonAsync<WorldCreatorPreview>())!;}

    [Theory][InlineData(FootballGender.Male)][InlineData(FootballGender.Female)]
    public async Task ModesConsentActualMatchesPostgresRestartAndCatalogIndependence(FootballGender gender)
    {
        string baseConnection=Environment.GetEnvironmentVariable("ELIFOOT_013C_REAL_CATALOG_PG")??throw new InvalidOperationException("Use PostgreSQL 013C runner.");
        var parsed=new Npgsql.NpgsqlConnectionStringBuilder(baseConnection);Assert.True(parsed.Port==55442&&parsed.Database!.StartsWith("elifoot_016b_",StringComparison.Ordinal)||parsed.Port==55441&&parsed.Database!.StartsWith("elifoot_016a_",StringComparison.Ordinal)||parsed.Port==55440&&parsed.Database!.StartsWith("elifoot_015b_",StringComparison.Ordinal)||parsed.Port==55439&&parsed.Database!.StartsWith("elifoot_015a_",StringComparison.Ordinal)||parsed.Port==55438&&parsed.Database!.StartsWith("elifoot_014c_",StringComparison.Ordinal)||parsed.Port==55436&&parsed.Database!.StartsWith("elifoot_013c_",StringComparison.Ordinal)||parsed.Port==55437&&parsed.Database!.StartsWith("elifoot_014b_",StringComparison.Ordinal));Assert.Equal("127.0.0.1",parsed.Host);
        string prefix=parsed.Port==55442?"elifoot_016b_013b_":parsed.Port==55441?"elifoot_016a_013b_":parsed.Port==55440?"elifoot_015b_013b_":parsed.Port==55439?"elifoot_015a_013b_":parsed.Port==55438?"elifoot_014c_013b_":parsed.Port==55437?"elifoot_014b_013b_":"elifoot_013c_013b_";
        string suffix=Guid.NewGuid().ToString("N")[..10];
        string catalog=new Npgsql.NpgsqlConnectionStringBuilder(baseConnection){Database=prefix+"catalog_test_"+suffix}.ConnectionString;
        string worlds=new Npgsql.NpgsqlConnectionStringBuilder(baseConnection){Database=prefix+"worlds_test_"+suffix}.ConnectionString;
        await using var factory=Factory(catalog,worlds);using var client=factory.CreateClient();
        var owner=Guid.NewGuid();client.DefaultRequestHeaders.Authorization=new("Bearer","localdev-"+owner.ToString("N"));
        await Populate(factory.Services.GetRequiredService<CatalogSyncService>(),gender);
        var request=new WorldCreatorRequest("Synthetic playable 013C",gender,"2026",WorldCreatorService.PriorityCountries.Select(c=>c.Code).ToList(),
            WorldAssemblyMode.Automatic,DistrictFillMode.Random,130013,[],UseSavedData:true,DataMode:WorldDataMode.REAL_COMPLETE);
        var strict=await Preview(client,request);Assert.NotEmpty(strict.Errors);Assert.Equal(0,strict.DataSummary!.CompleteRealSquads);
        Assert.False((await client.PostAsJsonAsync($"/api/world-creator/confirm/{strict.DraftId}",new{})).IsSuccessStatusCode);
        var refused=await Preview(client,request with {DataMode=WorldDataMode.REAL_CLUBS_SIMULATED});
        Assert.NotEmpty(refused.Errors);Assert.Equal(0,refused.DataSummary!.GeneratedPlayers);
        request=request with {DataMode=WorldDataMode.REAL_CLUBS_SIMULATED,AllowFictionalPlayers=true};
        if(gender==FootballGender.Female)
        {
            var selected=(await (await client.PostAsJsonAsync("/api/world-creator/catalog",request)).Content.ReadFromJsonAsync<CatalogSelection>())!;
            var confirmedPlayerClub=selected.Clubs.Single(c=>c.Players.Count>0);
            request=request with {Mode=WorldAssemblyMode.Custom,Assignments=[new(confirmedPlayerClub.Key,1)]};
        }
        var preview=await Preview(client,request);Assert.Empty(preview.Errors);Assert.Equal(128,preview.Clubs.Count);
        var confirmation=await client.PostAsJsonAsync($"/api/world-creator/confirm/{preview.DraftId}",new{});Assert.True(confirmation.IsSuccessStatusCode,await confirmation.Content.ReadAsStringAsync());
        var created=(await confirmation.Content.ReadFromJsonAsync<JsonElement>());Guid worldId=created.GetProperty("id").GetGuid();
        var repo=factory.Services.GetRequiredService<IWorldRepository>();var world=(await repo.GetWorldAsync(worldId))!;
        Assert.Equal(request.DataMode,world.CatalogOrigin!.DataMode);Assert.All(world.Players,p=>Assert.NotNull(p.CatalogIdentity));
        Assert.All(world.Players,p=>Assert.True(p.CatalogIdentity!.Origin is WorldPlayerOrigin.FICTIONAL_GENERATED or WorldPlayerOrigin.REAL_VERIFIED_CURRENT));
        Assert.Equal(128,world.Clubs.Count);Assert.Equal(2304,world.Players.Count);Assert.Equal(128,world.Economy.CreditAssessments.Count);
        var clubs=world.Clubs.Take(2).ToList();var coaches=new[]{owner,Guid.NewGuid()};
        for(int i=0;i<2;i++)Assert.True((await client.PostAsync($"/api/worlds/{worldId}/join?coachId={coaches[i]}&nickname=Test{i}&deviceId=013c-{coaches[i]}&preferredClubId={clubs[i].Id}",null)).IsSuccessStatusCode);
        await using var hub=new HubConnectionBuilder().WithUrl("http://localhost/hubs/gameroom",o=>o.HttpMessageHandlerFactory=_=>factory.Server.CreateHandler()).Build();
        await hub.StartAsync();await hub.InvokeAsync<LiveRoundSnapshotDto>("JoinRoom",worldId,owner);
        string loan=$"/api/worlds/{worldId}/clubs/{clubs[0].Id}/loans?coachId={owner}&amount=500000&rounds=0&idempotencyKey=013c-unique";
        for(int i=0;i<2;i++)Assert.True((await client.PostAsync(loan,null)).IsSuccessStatusCode);
        Assert.Single(world.BankLoans);
        for(int round=1;round<=3;round++)
        {
            for(int i=0;i<2;i++)
            {
                var club=(await client.GetFromJsonAsync<ClubDetailDto>($"/api/worlds/{worldId}/clubs/{clubs[i].Id}"))!;
                Assert.All(club.Squad,p=>Assert.NotNull(p.CatalogIdentity));var snap=await Snapshot(client,worldId);
                var submitted=await client.PostAsJsonAsync($"/api/worlds/{worldId}/tactics",new SubmitTacticsRequest(worldId,coaches[i],club.PreferredFormation,club.PreferredStarterIds,club.PreferredBenchIds,$"013c-ready-{round}-{i}",snap.RoundVersion));
                Assert.True(submitted.IsSuccessStatusCode,await submitted.Content.ReadAsStringAsync());
            }
            for(int step=0;step<16;step++)
            {
                var snap=await Snapshot(client,worldId);if(snap.RoundState==RoundState.RESULTS)break;
                if(snap.ActivePauseWindow!=null)
                    foreach(var coach in coaches)Assert.True((await client.PostAsJsonAsync($"/api/worlds/{worldId}/continue",new ConfirmContinueMatchCommand(worldId,coach,$"continue-{round}-{step}-{coach}"))).IsSuccessStatusCode);
                else Assert.True((await client.PostAsync($"/api/worlds/{worldId}/tick?ticks=125",null)).IsSuccessStatusCode);
            }
            Assert.Equal(RoundState.RESULTS,(await Snapshot(client,worldId)).RoundState);
            Assert.True((await client.PostAsync($"/api/worlds/{worldId}/next-round",null)).IsSuccessStatusCode);
        }
        Assert.Contains(world.Fixtures,m=>m.CurrentMinute>=90&&m.IsFinished&&(m.HomeClubId==clubs[0].Id||m.AwayClubId==clubs[0].Id));
        var debt=world.BankLoans.Single();Assert.Equal(25000m*debt.ChargedMatches.Count,debt.InterestCharged);Assert.True(debt.InterestCharged>0);
        Assert.Equal(world.Transactions.Sum(t=>t.Amount),world.Clubs.Sum(c=>c.CashBalance));
        Assert.All(world.Clubs,c=>Assert.True(SquadValidator.ValidateClubSquadLimits(world.Players.Where(p=>p.ClubId==c.Id).ToList()).IsValid));
        await hub.StopAsync();string frozen=JsonSerializer.Serialize(world);var ids=world.Players.Select(p=>p.CatalogIdentity!.CanonicalIdentity).ToList();
        var source=CatalogFixtureFactory.Packages().First(p=>p.Scope.Gender==gender&&p.Scope.DataType==CatalogDataType.Clubs);
        await factory.Services.GetRequiredService<CatalogSyncService>().ImportAsync(source with {IsFixture=false,Rights=TestRights,Version="test-update013c",UpdatedAt=source.UpdatedAt.AddDays(1),
            Clubs=source.Clubs.Select(c=>c with {Name=c.Name+" changed",Verification=new(CatalogClubVerificationStatus.REAL_CLUB_VERIFIED,"Synthetic updated evidence",source.UpdatedAt,"2026",true)}).ToList()});
        Assert.Equal(frozen,JsonSerializer.Serialize(world));await repo.SaveWorldAsync(world);EfWorldRepository.ClearCacheForTesting();
        await using var restarted=Factory(catalog,worlds);using var fresh=restarted.CreateClient();fresh.DefaultRequestHeaders.Authorization=client.DefaultRequestHeaders.Authorization;
        var restored=(await restarted.Services.GetRequiredService<IWorldRepository>().GetWorldAsync(worldId))!;
        Assert.Equal(ids,restored.Players.Select(p=>p.CatalogIdentity!.CanonicalIdentity));Assert.Equal(preview.DataHash,restored.CatalogOrigin!.DataHash);
        Assert.Equal(world.Clubs.Sum(c=>c.CashBalance),restored.Clubs.Sum(c=>c.CashBalance));Assert.Equal(debt.RemainingBalance,restored.BankLoans.Single().RemainingBalance);
        Assert.True((await fresh.PostAsync(loan,null)).IsSuccessStatusCode);Assert.Single(restored.BankLoans);
        var again=await fresh.PostAsJsonAsync($"/api/world-creator/confirm/{preview.DraftId}",new{});Assert.Equal(worldId,(await again.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetGuid());
        Assert.Empty(restarted.Services.GetServices<ICatalogSource>()); // No external provider is needed to play or reload.
    }
}