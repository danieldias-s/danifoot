using System.Text.Json;
using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Domain;
using Elifoot.Infrastructure;
using Elifoot.Infrastructure.Catalog;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
namespace Elifoot.Integration.Tests;

/// <summary>
/// ELIFOOT_VISUAL_THEME_REBALANCE_014C: the global theme is the fixed classic green; preferences gained optional
/// theme variant, brightness and match-colors fields. Old saved preferences must keep loading unchanged.
/// </summary>
public sealed class VisualThemeRebalance014CTests
{
    private sealed class NoSource:IClubColorSource {
        public Task<ClubColorPalette?> FindAsync(EligibleCatalogClub club,CancellationToken ct=default)=>Task.FromResult<ClubColorPalette?>(null);
    }
    private sealed class Harness:IAsyncDisposable {
        private readonly string file=Path.Combine(Path.GetTempPath(),$"elifoot-014c-rebalance-{Guid.NewGuid():N}.db");
        public readonly InMemoryWorldRepository Worlds=new();public ServiceProvider Provider{get;}
        public ClubColorService Colors=>Provider.GetRequiredService<ClubColorService>();
        public Harness(){var s=new ServiceCollection();s.AddDbContext<CatalogDbContext>(o=>o.UseSqlite($"Data Source={file};Pooling=False"));s.AddSingleton<IWorldRepository>(Worlds);s.AddSingleton<IClubColorSource>(new NoSource());s.AddSingleton<ClubColorService>();Provider=s.BuildServiceProvider();}
        public async Task Init(){using var scope=Provider.CreateScope();var db=scope.ServiceProvider.GetRequiredService<CatalogDbContext>();await db.Database.EnsureCreatedAsync();await ClubColorService.EnsureSchema(db);}
        public async ValueTask DisposeAsync(){await Provider.DisposeAsync();if(File.Exists(file))File.Delete(file);}
    }

    [Fact]
    public void PreferencesSavedBeforeTheRebalanceStillDeserialize()
    {
        var legacy=JsonSerializer.Deserialize<AppearancePreferencesDto>("{\"mode\":\"Club\",\"version\":3}",CatalogImportCodec.Json)!;
        Assert.Equal("Club",legacy.Mode);Assert.Equal(3,legacy.Version);
        Assert.Null(legacy.ThemeVariant);Assert.Null(legacy.Brightness);Assert.Null(legacy.MatchColors);
        var custom=JsonSerializer.Deserialize<AppearancePreferencesDto>("{\"mode\":\"Custom\",\"background\":\"#000000\",\"primary\":\"#C3281E\",\"secondary\":\"#000000\",\"text\":\"#FFFFFF\",\"accent\":\"#FF0000\",\"version\":1}",CatalogImportCodec.Json)!;
        ClubColorPolicy.Validate(custom);
        ClubColorPolicy.Validate(legacy);
    }

    [Theory]
    [InlineData("Classic",null,null,null,true)]
    [InlineData("Classic","Escuro",-2,"Neutral",true)]
    [InlineData("Classic","AltoContraste",2,"Strong",true)]
    [InlineData("ClubGlobal",null,null,null,true)]
    [InlineData("Neutral","Classico",0,null,true)]
    [InlineData("Rainbow",null,null,null,false)]
    [InlineData("Classic","Roxo",null,null,false)]
    [InlineData("Classic",null,3,null,false)]
    [InlineData("Classic",null,-3,null,false)]
    [InlineData("Classic",null,null,"Vivid",false)]
    public void RebalancedPreferenceValidation(string mode,string? variant,int? brightness,string? matchColors,bool valid)
    {
        var p=new AppearancePreferencesDto(mode,ThemeVariant:variant,Brightness:brightness,MatchColors:matchColors);
        if(valid)ClubColorPolicy.Validate(p);else Assert.Throws<ArgumentException>(()=>ClubColorPolicy.Validate(p));
    }

    [Fact]
    public async Task ClassicGreenPreferencesPersistPrivatelyWithoutTouchingTheWorld()
    {
        await using var h=new Harness();await h.Init();
        var w=WorldSeedFactory.CreateSeededWorld("014C-rebalance",totalClubs:128);var club=w.Clubs[0];var coach=Guid.NewGuid();
        club.HumanCoachId=coach;w.HumanCoaches.Add(new(){Id=coach,CurrentClubId=club.Id});await h.Worlds.SaveWorldAsync(w);
        var snapshot=JsonSerializer.Serialize(w,CatalogImportCodec.Json);
        Assert.Equal("Club",(await h.Colors.Appearance(coach,w.Id,club.Id)).Preferences.Mode);
        var classic=new AppearancePreferencesDto("Classic",ThemeVariant:"Escuro",Brightness:1,MatchColors:"Neutral");
        var saved=await h.Colors.Appearance(coach,w.Id,club.Id,new(classic,0));
        Assert.Equal(classic with{Version=1},saved.Preferences);
        Assert.Equal(classic with{Version=1},(await h.Colors.Appearance(coach,w.Id,club.Id)).Preferences);
        Assert.Equal(snapshot,JsonSerializer.Serialize(w,CatalogImportCodec.Json));
    }
}
