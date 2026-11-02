using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Infrastructure;
using Elifoot.Infrastructure.Catalog;
using Elifoot.Infrastructure.Catalog.Import016A;
using Elifoot.Infrastructure.Catalog.World016B;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace Elifoot.Integration.Tests;

public class WorldCatalogSync016BTests
{
    private sealed class Protocol:HttpMessageHandler {
        public List<string> Paths=[];public string? FailPath;public HttpStatusCode Failure=HttpStatusCode.ServiceUnavailable;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage req,CancellationToken ct) {
            string path=req.RequestUri!.PathAndQuery;lock(Paths)Paths.Add(path);
            if(FailPath!=null&&req.RequestUri.AbsolutePath==FailPath)return Task.FromResult(new HttpResponseMessage(Failure){Content=JsonContent.Create(new{error="DO_NOT_LOG_PROVIDER_BODY"})});
            var q=Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(req.RequestUri.Query);
            string country=q.TryGetValue("country",out var v)?v.ToString():"Spain";
            int league=q.TryGetValue("league",out v)?int.Parse(v!):101;
            int team=q.TryGetValue("team",out v)?int.Parse(v!):league*10;
            int page=q.TryGetValue("page",out v)?int.Parse(v!):1;object data;
            if(req.RequestUri.AbsolutePath=="/countries")data=new{response=new[]{new{name="Spain",code="ES"},new{name="England",code="GB"}},errors=new{}};
            else if(req.RequestUri.AbsolutePath=="/leagues")data=new{errors=new{},response=new[]{new{league=new{id=country=="England"?201:101,name="FIXTURE league",type="League"},seasons=new[]{new{year=2026,current=true,coverage=new{standings=true,players=true}}}}}};
            else if(req.RequestUri.AbsolutePath=="/teams")data=new{errors=new{},response=Enumerable.Range(0,2).Select(i=>new{team=new{id=league*10+i,name="FIXTURE club "+i,code="FX",country=league>=200?"England":"Spain",national=false}})};
            else if(req.RequestUri.AbsolutePath=="/standings")data=new{errors=new{},response=new[]{new{league=new{standings=new[]{Enumerable.Range(0,2).Select(i=>new{team=new{id=league*10+i},rank=i+1,points=6-i,all=new{played=3}})}}}}};
            else if(req.RequestUri.AbsolutePath=="/players/squads")data=new{errors=new{},response=new[]{new{team=new{id=team},players=Enumerable.Range(1,26).Select(i=>new{id=team*1000+i,name="FIXTURE athlete "+i,position=i<=2?"Goalkeeper":i<=10?"Defender":i<=19?"Midfielder":"Attacker"})}}};
            else if(req.RequestUri.AbsolutePath=="/players")data=new{errors=new{},paging=new{current=page,total=2},response=Enumerable.Range((page-1)*13+1,13).Select(i=>new{player=new{id=team*1000+i},statistics=new[]{new{team=new{id=team},league=new{id=team/10,season=2026},games=new{rating="7.0",minutes=500+i,appearences=5},goals=new{total=0,assists=1}}}})};
            else throw new InvalidOperationException(path);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=JsonContent.Create(data)});
        }
    }
    private sealed class Harness:IAsyncDisposable {
        public readonly ServiceProvider Services;public WorldCatalogService016B Service=>Services.GetRequiredService<WorldCatalogService016B>();
        public Import016AOptions Options {get;}public readonly string File;
        public Harness(Protocol protocol,string? file=null,bool key=true) {
            File=file??Path.Combine(Path.GetTempPath(),"elifoot-world-catalog-016b-"+Guid.NewGuid()+".db");
            var services=new ServiceCollection();services.AddDbContext<CatalogDbContext>(o=>o.UseSqlite("Data Source="+File+";Default Timeout=30;Pooling=False"));
            services.AddSingleton<IWorldRepository,InMemoryWorldRepository>();services.AddSingleton(new CatalogSyncOptions());services.AddSingleton<CatalogSyncService>();services.AddSingleton<WorldCreatorService>();
            Options=new(){FixtureData=true,ApiFootballKey=key?"synthetic-protocol-token-not-a-real-key":null,Retries=0,MinimumIntervalMilliseconds=0,
                ApiFootballRights=new("TEST-ONLY","Synthetic protocol, not an external licence",true,true,true,true,true,true,true)};
            services.AddSingleton(Options);services.AddSingleton(new WorldCatalogOptions016B());
            services.AddSingleton(sp=>new ImportHttp016A(sp.GetRequiredService<IServiceScopeFactory>(),_=>new HttpClient(protocol,false){BaseAddress=new("https://fixture.invalid/")},Options));
            services.AddSingleton<WorldCatalogService016B>();Services=services.BuildServiceProvider();
        }
        public async Task Init(){using var s=Services.CreateScope();var db=s.ServiceProvider.GetRequiredService<CatalogDbContext>();await db.Database.EnsureCreatedAsync();await Import016ASchema.Ensure(db);await WorldCatalogSchema016B.Ensure(db);}
        public async Task<T> Read<T>(Func<CatalogDbContext,Task<T>> f){using var s=Services.CreateScope();return await f(s.ServiceProvider.GetRequiredService<CatalogDbContext>());}
        public async Task Drain(int max=40){for(int i=0;i<max&&await Service.RunOneStep();i++){} }
        public async Task Reviewed(string country="ES") {
            await Service.Queue(country,FootballGender.Male,"2026");await Service.RunOneStep();
            Assert.Equal("REVIEW_REQUIRED",(await Service.Status(FootballGender.Male,"2026")).Single(x=>x.CountryCode==country).Status);
            await Service.ReviewLeague(new(country,country=="ES"?"101":"201","2026",FootballGender.Male,"FIXTURE reviewed category evidence"));
        }
        public ValueTask DisposeAsync()=>Services.DisposeAsync();
    }
    [Fact] public async Task WeeklyQueueUsesPersistentBatchesAndDoesNotLoseCatalogueAfterFailure() {
        var p=new Protocol();string file;
        await using(var h=new Harness(p)) {
            file=h.File;await h.Init();await h.Reviewed();await h.Drain();
            var status=(await h.Service.Status(FootballGender.Male,"2026")).Single(x=>x.CountryCode=="ES");Assert.Equal("SUCCEEDED",status.Status);
            Assert.Equal(52,await h.Read(db=>db.Set<CatalogPlayer>().CountAsync()));
            var packages=await h.Read(db=>db.Snapshots.Select(s=>s.PayloadJson).ToListAsync());
            Assert.All(packages,j=>Assert.True(CatalogImportCodec.ReadJson(j).IsFixture));
            var squad=packages.Select(CatalogImportCodec.ReadJson).Single(x=>x.Scope.DataType==CatalogDataType.Squads);Assert.All(squad.Players,x=>Assert.NotNull(x.Performance));
            int calls=p.Paths.Count;Assert.Equal("CACHED",(await h.Service.Queue("ES",FootballGender.Male,"2026")).Status);Assert.Equal(calls,p.Paths.Count);
        }
        await using(var restart=new Harness(p,file)) {
            await restart.Init();int calls=p.Paths.Count;Assert.Equal("CACHED",(await restart.Service.Queue("ES",FootballGender.Male,"2026")).Status);Assert.Equal(calls,p.Paths.Count);
            var prior=await restart.Read(db=>db.Snapshots.Select(s=>s.Id).ToListAsync());
            await restart.Read(async db=>{var row=await db.Set<CountryCatalog016B>().SingleAsync();row.LastSuccessfulUpdateAt=DateTime.UtcNow.AddDays(-8);row.NextEligibleUpdateAt=DateTime.UtcNow.AddDays(-1);await db.Set<Import016ACache>().ExecuteUpdateAsync(s=>s.SetProperty(x=>x.ExpiresAt,DateTime.UnixEpoch));await db.SaveChangesAsync();return true;});
            p.FailPath="/countries";await restart.Service.Queue("ES",FootballGender.Male,"2026");await restart.Service.RunOneStep();
            var status=(await restart.Service.Status(FootballGender.Male,"2026")).Single(x=>x.CountryCode=="ES");Assert.Equal("FAILED",status.Status);Assert.True(status.UsingSavedData);
            Assert.Equal(prior.Order(),(await restart.Read(db=>db.Snapshots.Select(s=>s.Id).ToListAsync())).Order());
            Assert.DoesNotContain("DO_NOT_LOG",JsonSerializer.Serialize(status));
        }
    }
    [Theory][InlineData(1,false)][InlineData(6,false)][InlineData(7,true)][InlineData(8,true)]
    public async Task SevenDayBoundaryIsExact(int days,bool due) {
        var p=new Protocol();await using var h=new Harness(p);await h.Init();await h.Service.Queue("ES",FootballGender.Male,"2026");
        await h.Read(async db=>{var row=await db.Set<CountryCatalog016B>().SingleAsync();row.LastSuccessfulUpdateAt=DateTime.UtcNow.AddDays(-days);row.NextEligibleUpdateAt=row.LastSuccessfulUpdateAt.Value.AddDays(7);row.Status="SUCCEEDED";await db.SaveChangesAsync();return true;});
        Assert.Equal(due?"QUEUED":"CACHED",(await h.Service.Queue("ES",FootballGender.Male,"2026")).Status);Assert.Empty(p.Paths);
    }
    [Fact] public async Task CompletedCountryRefreshesAutomaticallyAfterSevenDaysWithoutSelection() {
        var p=new Protocol();await using var h=new Harness(p);await h.Init();await h.Reviewed();await h.Drain();
        Assert.False(await h.Service.RunOneStep());
        int calls=p.Paths.Count;
        await h.Read(async db=>{var row=await db.Set<CountryCatalog016B>().SingleAsync();row.LastSuccessfulUpdateAt=DateTime.UtcNow.AddDays(-8);row.NextEligibleUpdateAt=DateTime.UtcNow.AddDays(-1);row.NextAttemptAt=row.NextEligibleUpdateAt;await db.Set<Import016ACache>().ExecuteUpdateAsync(s=>s.SetProperty(x=>x.ExpiresAt,DateTime.UnixEpoch));await db.SaveChangesAsync();return true;});
        Assert.True(await h.Service.RunOneStep());
        Assert.True(p.Paths.Count>calls); // No Queue call: the background worker discovers the due country itself.
        var status=(await h.Service.Status(FootballGender.Male,"2026")).Single(x=>x.CountryCode=="ES");
        Assert.NotEqual("DONE",status.Phase);Assert.True(status.UsingSavedData);await h.Drain();
        Assert.Equal("SUCCEEDED",(await h.Service.Status(FootballGender.Male,"2026")).Single(x=>x.CountryCode=="ES").Status);
    }
    [Fact] public async Task GlobalDirectoryCanExceed64CountriesWhileWorldLimitRemainsEight() {
        var protocol=new Protocol();await using var h=new Harness(protocol);await h.Init();
        await h.Read(async db=>{for(int n=0;n<80;n++)db.Countries.Add(new(){Code=$"X{(char)('A'+n/26)}{(char)('A'+n%26)}",Name="Fixture country "+n});await db.SaveChangesAsync();return true;});
        var creator=h.Services.GetRequiredService<WorldCreatorService>();Assert.Equal(80,(await creator.Countries("2026")).Count);
        var countries=await h.Read(db=>db.Countries.Select(c=>c.Code).Take(9).ToListAsync());
        await Assert.ThrowsAsync<ArgumentException>(()=>creator.Preview(Guid.NewGuid(),new("Fixture",FootballGender.Male,"2026",countries,WorldAssemblyMode.Automatic,DistrictFillMode.Ranking,1,[],UseWorldCatalogRules:true)));
        Assert.Empty(protocol.Paths);
    }
    [Fact] public async Task ManualPowerKeepsEvidenceAndIsNotReassignedToFitPairs() {
        var protocol=new Protocol();await using var h=new Harness(protocol);await h.Init();await h.Reviewed();await h.Drain();
        var creator=h.Services.GetRequiredService<WorldCreatorService>();var source=await creator.Select(["ES"],FootballGender.Male,"2026",true);
        var ranked=await h.Service.Project(source.Clubs);
        foreach(var club in ranked)await h.Service.ManualPower(new(club.Key,"2026",20,"Fixture explicit manual allocation"));
        var replay=await h.Service.Project(source.Clubs);
        Assert.All(replay,c=>{Assert.Equal(20,c.InitialProfile!.ClubInitialPower);Assert.True(c.InitialProfile.ManualPower);Assert.Equal("Fixture explicit manual allocation",c.InitialProfile.ManualPowerEvidence);});
        await Assert.ThrowsAsync<ArgumentException>(()=>h.Service.ManualPower(new(ranked[0].Key,"2026",21,"Invalid fixture")));
    }
    [Fact] public async Task MissingKeyHasNoCallsAndDoesNotClaimThereIsSavedData() {
        var p=new Protocol();await using var h=new Harness(p,key:false);await h.Init();
        var view=await h.Service.Queue("ES",FootballGender.Male,"2026");Assert.Equal("NOT_CONNECTED",view.Status);Assert.False(view.UsingSavedData);Assert.False(await h.Service.RunOneStep());Assert.Empty(p.Paths);
    }
    [Fact] public async Task ConcurrentSelectionsShareOneCountryJobAndOneDiscovery() {
        var p=new Protocol();await using var h=new Harness(p);await h.Init();
        await Task.WhenAll(Enumerable.Range(0,12).Select(_=>h.Service.Queue("ES",FootballGender.Male,"2026")));
        await Task.WhenAll(h.Service.RunOneStep(),h.Service.RunOneStep());
        Assert.Equal(1,await h.Read(db=>db.Set<CountryCatalog016B>().CountAsync()));Assert.Equal(2,p.Paths.Count);
        Assert.Equal(0,await h.Read(db=>db.Scopes.CountAsync())); // No guessed category or clubs before review.
    }
    [Fact] public async Task QuotaResumeRetainsCursorAndCountriesRemainIndependent() {
        var p=new Protocol();string file;int calls;
        await using(var h=new Harness(p)) {
            file=h.File;await h.Init();await h.Reviewed();p.FailPath="/players/squads";p.Failure=HttpStatusCode.TooManyRequests;await h.Drain();
            var state=(await h.Service.Status(FootballGender.Male,"2026")).Single(x=>x.CountryCode=="ES");Assert.Equal("PARTIAL",state.Status);Assert.Equal("SQUADS",state.Phase);Assert.True(state.UsingSavedData);
            calls=p.Paths.Count;await h.Service.Queue("ES",FootballGender.Male,"2026");Assert.False(await h.Service.RunOneStep());Assert.Equal(calls,p.Paths.Count);
        }
        await using(var restart=new Harness(p,file)) {
            await restart.Init();Assert.Equal("PARTIAL",(await restart.Service.Status(FootballGender.Male,"2026")).Single(x=>x.CountryCode=="ES").Status);
            p.FailPath=null;await restart.Read(async db=> {await db.Set<CountryCatalog016B>().ExecuteUpdateAsync(s=>s.SetProperty(x=>x.NextAttemptAt,DateTime.UnixEpoch));await db.Set<Import016ABudget>().ExecuteUpdateAsync(s=>s.SetProperty(x=>x.BlockedUntil,DateTime.UnixEpoch));return true;});
            await restart.Drain();Assert.Equal("SUCCEEDED",(await restart.Service.Status(FootballGender.Male,"2026")).Single(x=>x.CountryCode=="ES").Status);
            Assert.Equal(1,p.Paths.Count(x=>x.StartsWith("/teams?")));Assert.Equal(1,p.Paths.Count(x=>x.StartsWith("/countries")));
            await restart.Service.Queue("GB",FootballGender.Female,"2026");Assert.Equal(2,await restart.Read(db=>db.Set<CountryCatalog016B>().CountAsync()));
            Assert.Equal("SUCCEEDED",(await restart.Service.Status(FootballGender.Male,"2026")).Single(x=>x.CountryCode=="ES").Status);
        }
    }
}
