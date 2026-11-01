using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Domain;
using Elifoot.Infrastructure.Catalog;
using Elifoot.Infrastructure.Catalog.Import016A;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
namespace Elifoot.Integration.Tests;

[Collection("Player rankings 009 SQLite")]
public class RealImport016ATests
{
    private const string Admin="016a-test-admin-token-not-a-production-secret";
    private sealed class Loopback : IStartupFilter {
        public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next)=>app=>{app.Use(async(c,n)=>{c.Connection.RemoteIpAddress=c.Request.Headers.ContainsKey("Test-Remote")?IPAddress.Parse("203.0.113.1"):IPAddress.Loopback;await n();});next(app);};
    }
    private sealed class Protocol : HttpMessageHandler {
        public int Requests;public int SquadCount=26;public int? Remaining;public HttpStatusCode Status=HttpStatusCode.OK;public string? Error;public bool MissingPosition,Duplicate,WrongTeam,OldSeason;public bool Changed;
        public List<string> Paths=new();
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct) {
            Requests++;Paths.Add(request.RequestUri!.PathAndQuery);
            if(Status!=HttpStatusCode.OK)return Task.FromResult(new HttpResponseMessage(Status){Content=JsonContent.Create(new{message="DO NOT LOG PROVIDER SECRET"})});
            object body;
            if(Error!=null)body=new{errors=new{requests=Error},response=Array.Empty<object>()};
            else if(request.RequestUri.AbsolutePath=="/teams")body=new{errors=new{},response=new[]{new{team=new{id=127,name="Flamengo",country="Brazil",national=false}}}};
            else if(request.RequestUri.AbsolutePath=="/leagues")body=new{errors=new{},response=new[]{new{league=new{id=71,name="Serie A"},country=new{name="Brazil"},seasons=new[]{new{year=OldSeason?2020:DateTime.UtcNow.Year,current=true,start=OldSeason?"2020-01-01":$"{DateTime.UtcNow.Year}-01-01",end=OldSeason?"2020-12-31":$"{DateTime.UtcNow.Year}-12-31"}}}}};
            else if(request.RequestUri.AbsolutePath=="/players/squads") {
                var players=Enumerable.Range(1,SquadCount).Select(i=>new{id=16000+i,name=$"FIXTURE athlete {i}"+(Changed?" updated":""),position=MissingPosition&&i==3?null:i<=2?"Goalkeeper":i<10?"Defender":i<19?"Midfielder":"Attacker"}).ToList();if(Duplicate&&players.Count>0)players.Add(players[0]);
                body=new{errors=new{},response=new[]{new{team=new{id=WrongTeam?999:127,name="Flamengo"},players}}};
            } else body=new{response=new{subscription=new{plan="FIXTURE",active=true},requests=new{current=0,limit_day=100}}};
            var response=new HttpResponseMessage(HttpStatusCode.OK){Content=JsonContent.Create(body)};
            if(Remaining!=null)response.Headers.Add("x-ratelimit-requests-remaining",Remaining.ToString());
            return Task.FromResult(response);
        }
    }
    private sealed class WikiProtocol : HttpMessageHandler {
        public bool MissingPosition,WrongCategory,Maxlag;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct) {
            if(Maxlag)return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=JsonContent.Create(new{error=new{code="maxlag"}})});
            string path=request.RequestUri!.AbsolutePath;object data;
            if(path.Contains("Special:EntityData")) {
                string id=path.Split('/').Last().Split('.')[0];var club=PilotClubs016A.All.Single(c=>c.WikidataId==id);
                object Claim(string q)=>new{mainsnak=new{datavalue=new{value=new{id=q}}}};
                data=new{entities=new Dictionary<string,object>{[id]=new{id,lastrevid=123,labels=new{en=new{value=club.Name}},aliases=new{},claims=new Dictionary<string,object>{["P17"]=new[]{Claim(club.CountryEntityId)},["P31"]=new[]{Claim(club.Gender==FootballGender.Female&&!WrongCategory?"Q28140340":"Q476028")},["P641"]=new[]{Claim("Q2736")}}}}};
            } else {
                object Value(string text)=>new{value=text};
                var rows=new List<Dictionary<string,object>>();
                foreach(int i in new[]{1,2})rows.Add(new(){["player"]=Value("http://www.wikidata.org/entity/Q999"+i),["playerLabel"]=Value("FIXTURE Wikidata athlete "+i),["positionLabel"]=Value(MissingPosition?"not a position":i==1?"goalkeeper":"defender"),["membership"]=Value("FIXTURE statement "+i),["sex"]=Value("http://www.wikidata.org/entity/Q6581072"),["start"]=Value("2010-01-01T00:00:00Z"),["end"]=Value("2011-01-01T00:00:00Z")});
                data=new{results=new{bindings=rows}};
            }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=JsonContent.Create(data)});
        }
    }
    private static WebApplicationFactory<Program> Factory(string connection,HttpMessageHandler protocol,bool postgres=false,bool key=true,int budget=100) {
        return new WebApplicationFactory<Program>().WithWebHostBuilder(b=>{
            b.UseEnvironment("Development");b.UseSetting("Elifoot:DatabaseProvider","InMemory");b.UseSetting("Elifoot:AutoSeedDefaultWorld","false");b.UseSetting("Elifoot:DisableBackgroundClock","true");
            b.UseSetting("Catalog013A:Enabled","true");b.UseSetting("Catalog013B:Enabled","true");b.UseSetting("Catalog013A:DatabaseProvider",postgres?"PostgreSQL":"Sqlite");b.UseSetting("ConnectionStrings:Catalog013A",connection);b.UseSetting("Catalog013A:LoadFixtures","false");b.UseSetting("Catalog016A:Enabled","true");b.UseSetting("Catalog016A:AdminToken",Admin);
            b.ConfigureServices(services=>{
                var options=new Import016AOptions{Enabled=true,FixtureData=true,AdminToken=Admin,ApiFootballKey=key?"fixture-provider-key-never-returned":null,DailyRequestBudget=budget,MinimumIntervalMilliseconds=0,Retries=0,ApiFootballRights=new("TEST-ONLY","Synthetic protocol fixture; no real data",true,true,true,true,true,true,true)};
                services.RemoveAll<Import016AOptions>();services.AddSingleton(options);services.RemoveAll<ImportHttp016A>();
                services.AddSingleton(sp=>new ImportHttp016A(sp.GetRequiredService<IServiceScopeFactory>(),_=>new HttpClient(protocol,false){BaseAddress=new("https://fixture.invalid/")},options));
                services.AddSingleton<IStartupFilter,Loopback>();
            });
        });
    }
    private static HttpClient Client(WebApplicationFactory<Program> factory) {var client=factory.CreateClient();client.DefaultRequestHeaders.Add("X-Catalog-Admin-Token",Admin);return client;}
    private static async Task Mapping(HttpClient client) {
        var response=await client.PostAsJsonAsync("/api/catalog-016a/map-team",new ConfirmClub016ARequest("flamengo","api-football","127","71","Serie A",FootballGender.Male,"BR","FIXTURE explicit identity review for tests only"));response.EnsureSuccessStatusCode();
    }
    private static async Task<RealImportRun016A> Preview(HttpClient client,bool refresh=false) {
        var response=await client.PostAsJsonAsync("/api/catalog-016a/dry-run",new RealImport016ARequest(["flamengo"],"api-football",refresh));response.EnsureSuccessStatusCode();return(await response.Content.ReadFromJsonAsync<RealImportRun016A>())!;
    }
    private static string FileName()=>Path.Combine(Path.GetTempPath(),"elifoot-016a-"+Guid.NewGuid().ToString("N")+".db");
    [Theory][InlineData(false)][InlineData(true)]
    public async Task DryRunApplyCacheAndIdempotentRestartPreserveAll26CatalogAthletesAndOldSnapshots(bool postgres) {
        string file=FileName();string connection=postgres?Environment.GetEnvironmentVariable("ELIFOOT_016A_IMPORT_TEST_PG")??throw new InvalidOperationException("Use isolated 016A runner."):$"Data Source={file};Pooling=False";
        var protocol=new Protocol{Duplicate=true};Guid runId;Guid firstSquad;
        try {
            await using(var factory=Factory(connection,protocol,postgres)) {
                using var client=Client(factory);await Mapping(client);
                using var scope=factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<CatalogDbContext>();int before=await db.Snapshots.CountAsync();
                var run=await Preview(client);runId=run.Id;Assert.True(run.IsFixture);Assert.False(run.Applied);Assert.Equal(before,await db.Snapshots.CountAsync());
                var report=Assert.Single(run.Clubs);Assert.Equal(26,report.Players.Count);Assert.Equal(27,report.ReceivedPlayers);Assert.Equal(1,report.Duplicates);Assert.Equal(26,report.CurrentReported);Assert.Equal(0,report.CurrentVerified);
                var apply=await client.PostAsync($"/api/catalog-016a/apply/{runId}",null);Assert.True(apply.IsSuccessStatusCode,await apply.Content.ReadAsStringAsync());
                var payload=(await apply.Content.ReadFromJsonAsync<RealImportRun016A>())!;Assert.True(payload.Applied);Assert.Equal(2,payload.SnapshotIds!.Count);
                firstSquad=payload.SnapshotIds.Single(id=>JsonSerializer.Deserialize<CatalogImportPackage>(db.Snapshots.Single(s=>s.Id==id).PayloadJson,CatalogImportCodec.Json)!.Scope.DataType==CatalogDataType.Squads);
                Assert.Equal(26,await db.Set<CatalogPlayer>().CountAsync(p=>p.SnapshotId==firstSquad));Assert.Equal(26,await db.Set<CatalogSquad>().CountAsync(p=>p.SnapshotId==firstSquad));
                int requests=protocol.Requests;var cached=await Preview(client);Assert.Equal(requests,protocol.Requests);Assert.Equal(26,Assert.Single(cached.Clubs).Players.Count);
                var cachedApply=await client.PostAsync($"/api/catalog-016a/apply/{cached.Id}",null);Assert.True(cachedApply.IsSuccessStatusCode,await cachedApply.Content.ReadAsStringAsync());Assert.Equal(payload.SnapshotIds,(await cachedApply.Content.ReadFromJsonAsync<RealImportRun016A>())!.SnapshotIds);
                Assert.DoesNotContain("fixture-provider-key",await (await client.GetAsync("/api/catalog-016a/coverage")).Content.ReadAsStringAsync());
            }
            await using(var restart=Factory(connection,protocol,postgres)) {
                using var client=Client(restart);int calls=protocol.Requests;var cached=await Preview(client);Assert.Equal(calls,protocol.Requests);
                var freshPreviewApply=await client.PostAsync($"/api/catalog-016a/apply/{cached.Id}",null);Assert.True(freshPreviewApply.IsSuccessStatusCode,await freshPreviewApply.Content.ReadAsStringAsync());Assert.Contains(firstSquad,(await freshPreviewApply.Content.ReadFromJsonAsync<RealImportRun016A>())!.SnapshotIds!);
                Assert.True((await client.PostAsync($"/api/catalog-016a/apply/{runId}",null)).IsSuccessStatusCode);
                using var scope=restart.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<CatalogDbContext>();Assert.Equal(26,await db.Set<CatalogPlayer>().CountAsync(p=>p.SnapshotId==firstSquad));
                protocol.Changed=true;var changed=await Preview(client,true);Assert.True((await client.PostAsync($"/api/catalog-016a/apply/{changed.Id}",null)).IsSuccessStatusCode);
                Assert.True(await db.Snapshots.AnyAsync(p=>p.Id==firstSquad));Assert.Equal(26,await db.Set<CatalogPlayer>().CountAsync(p=>p.SnapshotId==firstSquad));
            }
        } finally {if(!postgres&&System.IO.File.Exists(file))System.IO.File.Delete(file);}
    }
    [Theory][InlineData("missing-key")][InlineData("unauthorized")][InlineData("unavailable")][InlineData("quota")][InlineData("payload-limit")][InlineData("empty")][InlineData("wrong-team")][InlineData("old-season")][InlineData("missing-position")]
    public async Task FailureAndPartialProtocolsNeverInventPlayersOrConsumeMoreCallsAfterQuota(string scenario) {
        string file=FileName();var protocol=new Protocol();
        if(scenario=="unauthorized")protocol.Status=HttpStatusCode.Unauthorized;
        if(scenario=="unavailable")protocol.Status=HttpStatusCode.ServiceUnavailable;
        if(scenario=="quota")protocol.Status=HttpStatusCode.TooManyRequests;
        if(scenario=="payload-limit")protocol.Error="Daily limit reached";
        if(scenario=="empty")protocol.SquadCount=0;
        if(scenario=="wrong-team")protocol.WrongTeam=true;
        if(scenario=="old-season")protocol.OldSeason=true;
        if(scenario=="missing-position")protocol.MissingPosition=true;
        try {await using var factory=Factory($"Data Source={file};Pooling=False",protocol,key:scenario!="missing-key");using var client=Client(factory);await Mapping(client);
            var run=await Preview(client);var club=Assert.Single(run.Clubs);
            if(scenario=="missing-position"){Assert.Equal(25,club.UsablePlayers);Assert.Contains(club.Players,p=>p.Position==null&&p.Issues.Contains("POSITION_MISSING_OR_AMBIGUOUS"));}
            else {Assert.Equal(0,club.UsablePlayers);Assert.NotEqual("CURRENT_PROVIDER_REPORTED",club.Status);Assert.False((await client.PostAsync($"/api/catalog-016a/apply/{run.Id}",null)).IsSuccessStatusCode);}
            if(scenario=="missing-key")Assert.Equal(0,protocol.Requests);
            if(scenario is "quota" or "payload-limit"){int calls=protocol.Requests;await Preview(client,true);Assert.Equal(calls,protocol.Requests);}
        } finally {if(System.IO.File.Exists(file))System.IO.File.Delete(file);}
    }
    [Fact] public async Task DailyBudgetIsPersistentAndCannotBeResetByRestartOrExplicitRefresh() {
        string file=FileName();var protocol=new Protocol();try {
            await using(var first=Factory($"Data Source={file};Pooling=False",protocol,budget:2)){using var client=Client(first);await Mapping(client);var report=await Preview(client,true);Assert.Contains(report.Clubs,c=>c.Status=="DAILY_BUDGET_EXHAUSTED");Assert.Equal(2,protocol.Requests);}
            await using(var restart=Factory($"Data Source={file};Pooling=False",protocol,budget:2)){using var client=Client(restart);var report=await Preview(client,true);Assert.Contains(report.Clubs,c=>c.Status=="DAILY_BUDGET_EXHAUSTED");Assert.Equal(2,protocol.Requests);}
        } finally {if(System.IO.File.Exists(file))System.IO.File.Delete(file);}
    }
    [Fact] public async Task ProviderRemainingQuotaStopsTheNextRequestAndAuditNeverIncludesCredentials() {
        string file=FileName();var protocol=new Protocol{Remaining=0};try{await using var factory=Factory($"Data Source={file};Pooling=False",protocol);using var client=Client(factory);await Mapping(client);
            var run=await Preview(client,true);Assert.Equal(1,protocol.Requests);Assert.Contains(run.Clubs,c=>c.Status=="PROVIDER_WAIT");
            var audit=await(await client.GetAsync("/api/catalog-016a/audit")).Content.ReadAsStringAsync();Assert.DoesNotContain("fixture-provider-key",audit);Assert.DoesNotContain("DO NOT LOG",audit);
        } finally {if(System.IO.File.Exists(file))System.IO.File.Delete(file);}
    }
    [Fact] public async Task IdentityDiscoveryNeedsReviewAndCannotMapMaleTeamToFemalePilot() {
        string file=FileName();var protocol=new Protocol();try{await using var factory=Factory($"Data Source={file};Pooling=False",protocol);using var client=Client(factory);
            var run=await Preview(client);var report=Assert.Single(run.Clubs);Assert.Equal("IDENTITY_REVIEW_REQUIRED",report.Status);Assert.NotEmpty(report.Candidates);Assert.Empty(report.Players);
            Assert.False((await client.PostAsJsonAsync("/api/catalog-016a/map-team",new ConfirmClub016ARequest("barcelona-female","api-football","127","71","Serie A",FootballGender.Male,"BR","Fixture rejected cross-category mapping"))).IsSuccessStatusCode);
            await Mapping(client);Assert.False((await client.PostAsJsonAsync("/api/catalog-016a/map-team",new ConfirmClub016ARequest("barcelona-female","api-football","127","71","Serie A",FootballGender.Female,"ES","Fixture rejected shared external team ID"))).IsSuccessStatusCode);
        } finally {if(System.IO.File.Exists(file))System.IO.File.Delete(file);}
    }
    [Theory][InlineData(false)][InlineData(true)] public async Task AdministratorTokenAndLoopbackAreBothMandatory(bool remote) {
        string file=FileName();var protocol=new Protocol();try{await using var factory=Factory($"Data Source={file};Pooling=False",protocol);using var client=remote?Client(factory):factory.CreateClient();if(remote)client.DefaultRequestHeaders.Add("Test-Remote","true");
            Assert.Equal(HttpStatusCode.NotFound,(await client.GetAsync("/api/catalog-016a/coverage")).StatusCode);Assert.Equal(0,protocol.Requests);
        } finally {if(System.IO.File.Exists(file))System.IO.File.Delete(file);}
    }
    [Theory][InlineData("historical")][InlineData("missing-position")][InlineData("wrong-category")][InlineData("maxlag")]
    public async Task WikidataFemaleIdentityAndHistoricalMembershipsNeverBecomeCurrentByNameOrOpenDates(string scenario) {
        string file=FileName();var protocol=new WikiProtocol{MissingPosition=scenario=="missing-position",WrongCategory=scenario=="wrong-category",Maxlag=scenario=="maxlag"};
        try{await using var factory=Factory($"Data Source={file};Pooling=False",protocol,key:false);using var client=Client(factory);
            var response=await client.PostAsJsonAsync("/api/catalog-016a/dry-run",new RealImport016ARequest(["barcelona-female"],"wikidata"));response.EnsureSuccessStatusCode();var report=(await response.Content.ReadFromJsonAsync<RealImportRun016A>())!;var club=Assert.Single(report.Clubs);
            Assert.Equal(0,club.CurrentVerified);Assert.Equal(0,club.CurrentReported);Assert.True(report.IsFixture);
            if(scenario=="historical"){Assert.Equal(2,club.Historical);Assert.Equal("Q522899",club.ExternalId);Assert.All(club.Players,p=>Assert.Equal(WorldPlayerOrigin.REAL_HISTORICAL_REFERENCE,p.Origin));}
            if(scenario=="missing-position")Assert.All(club.Players,p=>Assert.Null(p.Position));
            if(scenario=="wrong-category")Assert.Equal("CATEGORY_MISMATCH",club.Status);
            if(scenario=="maxlag")Assert.Equal("MAXLAG",club.Status);
        }finally{if(System.IO.File.Exists(file))System.IO.File.Delete(file);}
    }
    [Fact] public async Task EmptyResponseRetainsActiveCatalogAndNeverMutatesAnExistingWorld() {
        string file=FileName();var protocol=new Protocol();try{await using var factory=Factory($"Data Source={file};Pooling=False",protocol);using var client=Client(factory);await Mapping(client);var first=await Preview(client);(await client.PostAsync($"/api/catalog-016a/apply/{first.Id}",null)).EnsureSuccessStatusCode();
            using var scope=factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<CatalogDbContext>();var ids=await db.Scopes.Select(s=>s.ActiveSnapshotId).ToListAsync();
            var world=WorldSeedFactory.CreateSeededWorld();var repository=factory.Services.GetRequiredService<Elifoot.Application.IWorldRepository>();await repository.SaveWorldAsync(world);string frozen=JsonSerializer.Serialize(world);
            protocol.SquadCount=0;var empty=await Preview(client,true);Assert.Equal("EMPTY_SQUAD",Assert.Single(empty.Clubs).Status);Assert.False((await client.PostAsync($"/api/catalog-016a/apply/{empty.Id}",null)).IsSuccessStatusCode);
            Assert.Equal(ids,await db.Scopes.Select(s=>s.ActiveSnapshotId).ToListAsync());Assert.Equal(frozen,JsonSerializer.Serialize(await repository.GetWorldAsync(world.Id)));
        }finally{if(System.IO.File.Exists(file))System.IO.File.Delete(file);}
    }
    [Fact] public async Task ReviewedExternalAliasReusesExistingClubAndPreservesItsColorsInsteadOfCreatingAnotherFlamengo() {
        string file=FileName();var protocol=new Protocol();try{await using var factory=Factory($"Data Source={file};Pooling=False",protocol);using var client=Client(factory);
            var old=new CatalogImportPackage(new("BR",FootballGender.Male,"TEST",DateTime.UtcNow.Year.ToString(),CatalogDataType.Clubs,"old-provider"),"Brasil","TEST-old",DateTime.UtcNow.AddDays(-1),true,1,1,1,true,true,"Explicit synthetic identity fixture",true,[new("old-flamengo","Flamengo RJ","FLA","TEST","TEST", "#CC0000","#000000")],[],[]);
            await factory.Services.GetRequiredService<CatalogSyncService>().ImportAsync(old);
            using var scope=factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<CatalogDbContext>();var identity=await db.Set<CatalogClubIdentity>().SingleAsync();await Mapping(client);var run=await Preview(client);
            var denied=await client.PostAsync($"/api/catalog-016a/apply/{run.Id}",null);Assert.False(denied.IsSuccessStatusCode);Assert.Contains("IDENTITY_LINK_REQUIRED",await denied.Content.ReadAsStringAsync());
            var mapped=await client.PostAsJsonAsync("/api/catalog-016a/map-existing-club",new CatalogIdentityMappingRequest("BR",FootballGender.Male,"api-football","127",identity.Id,"TEST independently reviewed existing source and external IDs"));mapped.EnsureSuccessStatusCode();
            var result=await client.PostAsync($"/api/catalog-016a/apply/{run.Id}",null);Assert.True(result.IsSuccessStatusCode,await result.Content.ReadAsStringAsync());Assert.Equal(1,await db.Set<CatalogClubIdentity>().CountAsync());
            var applied=(await result.Content.ReadFromJsonAsync<RealImportRun016A>())!;
            var clubSnapshot=await db.Snapshots.SingleAsync(p=>p.Id==applied.SnapshotIds![0]);var team=JsonSerializer.Deserialize<CatalogImportPackage>(clubSnapshot.PayloadJson,CatalogImportCodec.Json)!.Clubs.Single();Assert.Equal("#CC0000",team.PrimaryColor);Assert.Equal("#000000",team.SecondaryColor);
        }finally{if(System.IO.File.Exists(file))System.IO.File.Delete(file);}
    }
    [Fact] public async Task IdentityOnlyPreviewDoesNotErasePreviouslyCollectedSquadCoverage() {
        string file=FileName();var protocol=new WikiProtocol();try{await using var factory=Factory($"Data Source={file};Pooling=False",protocol,key:false);using var client=Client(factory);
            using var scope=factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<CatalogDbContext>();
            var response=await client.PostAsJsonAsync("/api/catalog-016a/dry-run",new RealImport016ARequest(["barcelona-female"],"wikidata"));response.EnsureSuccessStatusCode();var collected=(await response.Content.ReadFromJsonAsync<RealImportRun016A>())!;
            // Persist real-run-shaped reports without claiming the synthetic protocol is a real source.
            var row=await db.Set<Import016ARun>().SingleAsync(r=>r.Id==collected.Id);row.ReportJson=JsonSerializer.Serialize(collected with{IsFixture=false},CatalogImportCodec.Json);await db.SaveChangesAsync();
            var identityResponse=await client.PostAsJsonAsync("/api/catalog-016a/dry-run",new RealImport016ARequest(["barcelona-female"],"wikidata",IncludeSquads:false));identityResponse.EnsureSuccessStatusCode();var identity=(await identityResponse.Content.ReadFromJsonAsync<RealImportRun016A>())!;
            var identityRow=await db.Set<Import016ARun>().SingleAsync(r=>r.Id==identity.Id);identityRow.ReportJson=JsonSerializer.Serialize(identity with{IsFixture=false},CatalogImportCodec.Json);await db.SaveChangesAsync();
            var coverage=(await client.GetFromJsonAsync<RealImportCoverage016A>("/api/catalog-016a/coverage"))!;Assert.Equal(2,coverage.RealPlayers);Assert.Equal(2,Assert.Single(coverage.Clubs).Historical);Assert.Equal(0,coverage.CurrentVerified);Assert.Equal(0,coverage.CurrentReported);
        }finally{if(System.IO.File.Exists(file))System.IO.File.Delete(file);}
    }
    [Fact] public async Task TransactionRollsBackClubAndSquadTogetherOnInvalidRoster() {
        string file=FileName();var protocol=new Protocol();try{await using var factory=Factory($"Data Source={file};Pooling=False",protocol);using var client=Client(factory);await Mapping(client);var report=await Preview(client);
            using var scope=factory.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<CatalogDbContext>();var row=await db.Set<Import016ARun>().SingleAsync(r=>r.Id==report.Id);
            var packages=JsonSerializer.Deserialize<List<CatalogImportPackage>>(row.PackagesJson,CatalogImportCodec.Json)!;
            var squad=packages.Single(p=>p.Scope.DataType==CatalogDataType.Squads);packages[packages.IndexOf(squad)]=squad with{Players=squad.Players.Select(p=>p with{ClubExternalId="wrong-club"}).ToList(),RosterReports=squad.RosterReports!.Select(r=>r with{ClubExternalId="wrong-club"}).ToList()};row.PackagesJson=JsonSerializer.Serialize(packages,CatalogImportCodec.Json);await db.SaveChangesAsync();
            Assert.False((await client.PostAsync($"/api/catalog-016a/apply/{report.Id}",null)).IsSuccessStatusCode);Assert.Equal(0,await db.Snapshots.CountAsync());Assert.Equal(0,await db.Set<CatalogClubIdentity>().CountAsync());
        } finally {if(System.IO.File.Exists(file))System.IO.File.Delete(file);}
    }
}
