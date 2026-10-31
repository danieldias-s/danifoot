using System.Text.Json;
using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Domain;
using Elifoot.Infrastructure;
using Elifoot.Infrastructure.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace Elifoot.Integration.Tests;

public sealed class ClubColors014CTests
{
    private sealed class Source:IClubColorSource {
        public int Calls; public ClubColorPalette? Palette; public bool Fail;
        public Task<ClubColorPalette?> FindAsync(EligibleCatalogClub club,CancellationToken ct=default){Calls++;if(Fail)throw new HttpRequestException("offline");return Task.FromResult(Palette);}
    }
    private sealed class Harness:IAsyncDisposable {
        private readonly string file=Path.Combine(Path.GetTempPath(),$"elifoot-014c-colors-{Guid.NewGuid():N}.db");
        public readonly InMemoryWorldRepository Worlds=new();public readonly Source Source=new();public ServiceProvider Provider{get;}
        public ClubColorService Colors=>Provider.GetRequiredService<ClubColorService>();
        public Harness(){var services=new ServiceCollection();services.AddDbContext<CatalogDbContext>(o=>o.UseSqlite($"Data Source={file};Pooling=False"));services.AddSingleton<IWorldRepository>(Worlds);services.AddSingleton<IClubColorSource>(Source);services.AddSingleton<ClubColorService>();Provider=services.BuildServiceProvider();}
        public async Task Init(){using var scope=Provider.CreateScope();var db=scope.ServiceProvider.GetRequiredService<CatalogDbContext>();await db.Database.EnsureCreatedAsync();await ClubColorService.EnsureSchema(db);await ClubColorService.EnsureSchema(db);}
        public async ValueTask DisposeAsync(){await Provider.DisposeAsync();if(File.Exists(file))File.Delete(file);}
    }
    private static ClubColorPalette Palette(FootballGender gender=FootballGender.Male,string country="BR",string id="Q17479")=>new("#C3281E","#000000","#000000","Own club museum",true,DateTime.UtcNow,.95,"v1",country,gender,"wikidata",id,"https://museuflamengo.com.br/simbolos/escudos");
    private static EligibleCatalogClub Club(Guid id,FootballGender gender=FootballGender.Male,string country="BR",string external="Q17479")=>new($"{country}:{gender}:{external}",external,"wikidata","Mesmo nome","MES",country,"Brasil",gender,"Liga","2026","#006400","#FFFFFF",1,[],[],InternalCatalogClubId:id);

    private sealed class ColorHttp(Func<HttpRequestMessage,string> response):HttpMessageHandler {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)=>Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK){Content=new StringContent(response(request))});
    }
    [Theory][InlineData("P6364")][InlineData("P462")]
    public async Task WikidataUsesColorItemsThenP465WithExactReviewedQid(string property){
        object Claim(object value)=>new{mainsnak=new{datavalue=new{value}}};
        var clubEntity=new{lastrevid=1400,claims=new Dictionary<string,object>{["P17"]=new[]{Claim(new{id="Q155"})},[property]=new[]{Claim(new{id="Q3142"}),Claim(new{id="Q23445"})}}};
        var colorEntities=new Dictionary<string,object>{["Q3142"]=new{claims=new{P465=new[]{Claim("FF0000")}}},["Q23445"]=new{claims=new{P465=new[]{Claim("000000")}}}};
        int calls=0;using var client=new HttpClient(new ColorHttp(r=>{calls++;return JsonSerializer.Serialize(new{entities=r.RequestUri!.Query.Contains("ids=Q17479&")?new Dictionary<string,object>{["Q17479"]=clubEntity}:colorEntities});})){BaseAddress=new Uri("https://www.wikidata.org/")};
        var source=new Elifoot.Infrastructure.Catalog.Providers.WikidataClubColorSource(new(client,Elifoot.Infrastructure.Catalog.Providers.FootballProviderKind.Wikidata,null,0));
        var club=Club(Guid.NewGuid()) with{Aliases=[new("wikidata","Q17479")]};
        var palette=await source.FindAsync(club);Assert.NotNull(palette);Assert.Equal("#FF0000",palette.PrimaryColor);Assert.Equal("#000000",palette.SecondaryColor);Assert.Contains(property,palette.Evidence);Assert.Equal(2,calls);
        await Assert.ThrowsAsync<ArgumentException>(()=>source.FindAsync(club with{CountryCode="PT"}));
    }
    [Fact]
    public async Task FailedOnlineSourceFallsBackToAuthorizedImport(){
        await using var h=new Harness();await h.Init();h.Source.Fail=true;var fallback=new Source{Palette=Palette()};
        var service=new ClubColorService(h.Provider.GetRequiredService<IServiceScopeFactory>(),[h.Source,fallback],h.Worlds);
        var report=await service.Refresh([Club(Guid.NewGuid())]);Assert.Equal("#C3281E",report[0].Palette!.PrimaryColor);Assert.Null(report[0].LastIssue);Assert.Equal(1,fallback.Calls);
    }
    [Fact]
    public async Task LegacyWorldWithoutColorsAllowsPrivateAppearanceAndPreservesCompleteSnapshot(){
        await using var h=new Harness();await h.Init();var w=WorldSeedFactory.CreateSeededWorld(totalClubs:128);var owner=Guid.NewGuid();var club=w.Clubs[0];club.HumanCoachId=owner;w.HumanCoaches.Add(new(){Id=owner,CurrentClubId=club.Id});await h.Worlds.SaveWorldAsync(w);
        var before=JsonSerializer.Serialize(w,CatalogImportCodec.Json);Assert.Null(club.ColorPalette);
        var result=await h.Colors.Appearance(owner,w.Id,club.Id,new(new("Custom","#000000","#0000FF","#FFFFFF","#FFFFFF","#FFFF00"),0));
        Assert.Null(result.ClubColors);Assert.Equal("Custom",result.Preferences.Mode);Assert.Equal(before,JsonSerializer.Serialize(w,CatalogImportCodec.Json));
    }
    [Fact]
    public async Task ConcurrentCacheRefreshCoalescesSourceRequests(){
        await using var h=new Harness();await h.Init();h.Source.Palette=Palette();var club=Club(Guid.NewGuid());
        await Task.WhenAll(Enumerable.Range(0,5).Select(_=>h.Colors.Refresh([club])));Assert.Equal(1,h.Source.Calls);
    }
    [Fact]
    public async Task CacheHonorsLastValidResultCooldownAndSourceFailure(){
        await using var h=new Harness();await h.Init();var club=Club(Guid.NewGuid());h.Source.Palette=Palette();
        var first=await h.Colors.Refresh([club]);Assert.Equal("#C3281E",first[0].Palette!.PrimaryColor);Assert.Equal(1,h.Source.Calls);
        await h.Colors.Refresh([club]);Assert.Equal(1,h.Source.Calls);
        h.Source.Fail=true;var offline=await h.Colors.Refresh([club],true);Assert.Equal(first[0].Palette,offline[0].Palette);Assert.NotNull(offline[0].LastIssue);
        h.Source.Fail=false;h.Source.Palette=null;var empty=await h.Colors.Refresh([club],true);Assert.Equal(first[0].Palette,empty[0].Palette);
        var enriched=await h.Colors.Enrich([club]);Assert.Equal("#C3281E",enriched[0].PrimaryColor);Assert.Equal("#000000",enriched[0].SecondaryColor);
    }
    [Fact]
    public async Task ExternalIdentityCountryAndGenderAreNotMergedByName(){
        await using var h=new Harness();await h.Init();var male=Club(Guid.NewGuid());var female=Club(Guid.NewGuid(),FootballGender.Female);var wrongCountry=Club(Guid.NewGuid(),country:"PT");
        h.Source.Palette=Palette();await h.Colors.Refresh([male]);var f=await h.Colors.Refresh([female]);var p=await h.Colors.Refresh([wrongCountry]);
        Assert.Null(f[0].Palette);Assert.Null(p[0].Palette);Assert.NotNull((await h.Colors.Enrich([male]))[0].ColorPalette);
    }
    [Theory][InlineData(FootballGender.Male)][InlineData(FootballGender.Female)]
    public async Task AppearanceIsPrivateDurablePerClubAndDoesNotWriteGameplaySnapshot(FootballGender gender){
        await using var h=new Harness();await h.Init();var w=WorldSeedFactory.CreateSeededWorld("014C",totalClubs:128);var one=w.Clubs[0];var two=w.Clubs[1];var coach=Guid.NewGuid();var other=Guid.NewGuid();
        one.ColorPalette=Palette(gender);one.HumanCoachId=coach;two.HumanCoachId=other;
        w.HumanCoaches.Add(new(){Id=coach,CurrentClubId=one.Id});w.HumanCoaches.Add(new(){Id=other,CurrentClubId=two.Id});await h.Worlds.SaveWorldAsync(w);
        string snapshot=JsonSerializer.Serialize(w,CatalogImportCodec.Json);
        var state=await h.Colors.Appearance(coach,w.Id,one.Id);Assert.Equal("Club",state.Preferences.Mode);Assert.Equal(0,state.Preferences.Version);
        var custom=new AppearancePreferencesDto("Custom","#000000","#C3281E","#000000","#FFFFFF","#FF0000");
        state=await h.Colors.Appearance(coach,w.Id,one.Id,new(custom,0));Assert.Equal(1,state.Preferences.Version);
        Assert.Equal(custom with{Version=1},(await h.Colors.Appearance(coach,w.Id,one.Id)).Preferences);
        Assert.Equal("Club",(await h.Colors.Appearance(other,w.Id,one.Id)).Preferences.Mode);
        await Assert.ThrowsAsync<ArgumentException>(()=>h.Colors.Appearance(other,w.Id,one.Id,new(custom,0)));
        await Assert.ThrowsAsync<KeyNotFoundException>(()=>h.Colors.Appearance(Guid.NewGuid(),w.Id,one.Id));
        await Assert.ThrowsAsync<AssemblyConflictException>(()=>h.Colors.Appearance(coach,w.Id,one.Id,new(custom,0)));
        one.HumanCoachId=null;two.HumanCoachId=coach;
        Assert.Equal("Club",(await h.Colors.Appearance(coach,w.Id,two.Id)).Preferences.Mode);
        Assert.Equal(custom with{Version=1},(await h.Colors.Appearance(coach,w.Id,one.Id)).Preferences);
        one.HumanCoachId=coach;two.HumanCoachId=other;
        Assert.Equal(snapshot,JsonSerializer.Serialize(w,CatalogImportCodec.Json));
        var reset=await h.Colors.Appearance(coach,w.Id,one.Id,new(new("Club"),1));Assert.Equal("Club",reset.Preferences.Mode);Assert.Equal(2,reset.Preferences.Version);
        Assert.Equal(snapshot,JsonSerializer.Serialize(w,CatalogImportCodec.Json));
    }
    [Theory][InlineData("#C3281E",true)][InlineData("#abcdef",true)][InlineData("#FFF",false)][InlineData("#00000000",false)][InlineData("red",false)][InlineData("#12xx34",false)]
    public void RgbHexValidationIsStrict(string hex,bool valid)=>Assert.Equal(valid,ClubColorPolicy.Hex(hex));
    [Fact]
    public void PersonalizedPackageKeepsColorsAndUsesIndependentCopies(){
        var world=WorldSeedFactory.CreateSeededWorld(totalClubs:128);var package=WorldEditorAndPackageService.ExportWorldToPackage(world);var club=package.Clubs[0];
        var colors=ClubColorPolicy.Personalized("#0000FF","#FFFFFF","BR",FootballGender.Male,club.Id);package=package with{Clubs=package.Clubs.Select(c=>c.Id==club.Id?c with{PrimaryColorHex=colors.PrimaryColor,SecondaryColorHex=colors.SecondaryColor!,ColorPalette=colors}:c).ToList()};
        var prepared=CustomAssemblyPolicy.Prepare(package,package,Guid.NewGuid(),[new("BR","Brasil")],false,true);Assert.Equal("BR",prepared.Clubs[0].ColorPalette!.CountryCode);
        var a=CustomAssemblyPolicy.CreateIndependent(package,Guid.NewGuid(),1,["BR"],"One",2);var b=CustomAssemblyPolicy.CreateIndependent(package,Guid.NewGuid(),1,["BR"],"Two",2);
        Assert.Equal(colors,a.Clubs[0].ColorPalette);Assert.Equal(colors,b.Clubs[0].ColorPalette);Assert.NotEqual(a.Clubs[0].Id,b.Clubs[0].Id);
        var back=WorldEditorAndPackageService.ExportWorldToPackage(a);Assert.Equal(colors,back.Clubs[0].ColorPalette);
    }
}
