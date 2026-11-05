using System.Net.Http.Json;
using System.Text.Json;
using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Domain;
using Elifoot.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
namespace Elifoot.Integration.Tests;

[Collection("Player rankings 009 SQLite")]
public class History017CIntegrationTests
{
    [Fact] public async Task SavingAndListingWarmRoomsDoNotReadSnapshotPayloadAndColdLoadsAreShared()
    {
        await using var connection=new SqliteConnection("Data Source=:memory:");await connection.OpenAsync();var sql=new List<string>();
        var services=new ServiceCollection();services.AddDbContext<ElifootDbContext>(o=>o.UseSqlite(connection).LogTo(sql.Add,LogLevel.Information));
        await using var provider=services.BuildServiceProvider();
        using(var scope=provider.CreateScope())await scope.ServiceProvider.GetRequiredService<ElifootDbContext>().Database.EnsureCreatedAsync();
        var repo=new EfWorldRepository(provider.GetRequiredService<IServiceScopeFactory>());var w=WorldSeedFactory.CreateSeededWorld(totalClubs:64);
        await repo.SaveWorldAsync(w);sql.Clear();w.RoundState=RoundState.RESULTS;w.RoundVersion++;w.RoundNumber=2;
        await repo.SaveWorldAsync(w);await repo.ListWorldsAsync();
        Assert.DoesNotContain(sql,s=>s.Contains("SELECT")&&s.Contains("FROM \"ef_worlds\"")&&s.Contains("\"SnapshotJson\""));
        EfWorldRepository.ClearCacheForTesting();sql.Clear();
        var loaded=await Task.WhenAll(Enumerable.Range(0,4).Select(i=>i==0?repo.GetWorldByInviteCodeAsync(w.InviteCode):repo.GetWorldAsync(w.Id)));
        Assert.All(loaded,x=>Assert.Same(loaded[0],x));Assert.Equal(RoundState.RESULTS,loaded[0]!.RoundState);Assert.Equal(w.RoundVersion,loaded[0]!.RoundVersion);
        Assert.Single(sql,s=>s.Contains("SELECT")&&s.Contains("FROM \"ef_worlds\"")&&s.Contains("\"SnapshotJson\""));
        using var readScope=provider.CreateScope();var head=await readScope.ServiceProvider.GetRequiredService<ElifootDbContext>().Worlds.AsNoTracking().SingleAsync(x=>x.Id==w.Id);
        Assert.Equal((int)w.RoundState,head.RoundState);Assert.Equal(w.RoundNumber,head.RoundNumber);Assert.Equal(w.Seed,head.Seed);Assert.Equal(w.TotalClubs,head.TotalClubs);
        EfWorldRepository.ClearCacheForTesting();
    }
    private static WebApplicationFactory<Program> Factory(string provider,string connection)=>new WebApplicationFactory<Program>().WithWebHostBuilder(b=>{
        b.UseSetting("Elifoot:AuthMode","LocalDev");b.UseSetting("Elifoot:DatabaseProvider",provider);b.UseSetting("ConnectionStrings:DefaultConnection",connection);
        b.UseSetting("Elifoot:AutoSeedDefaultWorld","false");b.UseSetting("Elifoot:DisableBackgroundClock","true");
    });
    [Fact] public async Task RealMatchesHistoryAndClubSeasonQueriesSurviveApiAndDatabaseRestart()
    {
        var pg=Environment.GetEnvironmentVariable("ELIFOOT_HISTORY_POSTGRES");
        var path=Path.Combine(Path.GetTempPath(),$"history017c-{Guid.NewGuid():N}.db");
        var provider=pg==null?"Sqlite":"PostgreSQL";var connection=pg??$"Data Source={path};Pooling=False";
        var snapshotPath=Environment.GetEnvironmentVariable("ELIFOOT_HISTORY_SNAPSHOT");
        var w=snapshotPath==null?WorldSeedFactory.CreateSeededWorld(worldName:"QA History 017C",totalClubs:64):JsonSerializer.Deserialize<GameWorld>(File.ReadAllText(snapshotPath))!;
        var m=w.Fixtures.First();var home=w.Clubs.Single(c=>c.Id==m.HomeClubId);var away=w.Clubs.Single(c=>c.Id==m.AwayClubId);
        if(snapshotPath==null) {
        CoachCareerEngine.CaptureLeadership(w,m);
        MatchSimulationEngine.SimulateFullMatchReal(m,home,away,w.AiManagers.Single(a=>a.Id==home.AiManagerId),w.AiManagers.Single(a=>a.Id==away.AiManagerId),
            w.Players.Where(p=>p.ClubId==home.Id).ToList(),w.Players.Where(p=>p.ClubId==away.Id).ToList(),w.Players.ToDictionary(p=>p.Id),w.Seed);
        PlayerStatisticsEngine.ApplyFinishedMatches(w,[m]);HistoryEngine.Observe(w);
        } else Assert.Equal(100,w.History.ClosedSeasons.Count);
        int expectedMatches=w.History.Matches.Count;
        var expected=HistoryEngine.GetClubHistory(w,home.Id)!;
        try {
            await using(var first=Factory(provider,connection)) {
                using var client=first.CreateClient();await first.Services.GetRequiredService<IWorldRepository>().SaveWorldAsync(w);
                var dto=await client.GetFromJsonAsync<ClubHistoryDto>($"/api/worlds/{w.Id}/clubs/{home.Id}/history");
                Assert.Equal(expected.Total,dto!.Total);Assert.True(dto.Total.Games>0);
                Assert.Equal(System.Net.HttpStatusCode.BadRequest,(await client.GetAsync($"/api/worlds/{w.Id}/clubs/{home.Id}/history?season=999")).StatusCode);
            }
            EfWorldRepository.ClearCacheForTesting();
            await using(var restarted=Factory(provider,connection)) {
                using var client=restarted.CreateClient();var repo=restarted.Services.GetRequiredService<IWorldRepository>();var restored=(await repo.GetWorldAsync(w.Id))!;
                var dto=await client.GetFromJsonAsync<ClubHistoryDto>($"/api/worlds/{w.Id}/clubs/{home.Id}/history?season=1");
                Assert.Equal(HistoryEngine.GetClubHistory(w,home.Id,1)!.Total,dto!.Total);Assert.Equal(expectedMatches,restored.History.Matches.Count);
                Assert.Equal(w.History.ClosedSeasons.Count,restored.History.ClosedSeasons.Count);
                var p=w.PlayerStatistics.First().PlayerId;
                var player=await client.GetFromJsonAsync<PlayerHistoryDto>($"/api/worlds/{w.Id}/players/{p}/history");
                Assert.Equal(player!.Career.MatchesPlayed,player.SeasonTotals.Sum(s=>s.Games));Assert.Equal(player.Career.GoalsScored,player.SeasonTotals.Sum(s=>s.Goals));
                var before=restored.History.Matches.Count;HistoryEngine.Observe(restored);await repo.SaveWorldAsync(restored);Assert.Equal(before,restored.History.Matches.Count);
                if(pg!=null) {
                    var dir=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../../../../../artifacts/history-017c"));Directory.CreateDirectory(dir);
                    File.WriteAllText(Path.Combine(dir,"postgres.json"),JsonSerializer.Serialize(new{provider,worldId=w.Id,clubId=home.Id,playerId=p,restarted=true,gameCount=dto.Total.Games,goals=dto.Total.GoalsFor,historyMatches=restored.History.Matches.Count,closedSeasons=restored.History.ClosedSeasons.Count}));
                }
            }
        } finally {EfWorldRepository.ClearCacheForTesting();if(pg==null&&File.Exists(path))File.Delete(path);}
    }
}
