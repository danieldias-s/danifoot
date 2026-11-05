using System.Net.Http.Json;
using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Domain;
using Elifoot.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
namespace Elifoot.Integration.Tests;

[Collection("Player rankings 009 SQLite")]
public class AssistAndStadiumPersistenceTests
{
    private static WebApplicationFactory<Program> Factory(string path) => new WebApplicationFactory<Program>().WithWebHostBuilder(b => {
        b.UseSetting("Elifoot:AuthMode","LocalDev");b.UseSetting("Elifoot:DatabaseProvider","Sqlite");
        b.UseSetting("ConnectionStrings:DefaultConnection",$"Data Source={path};Pooling=False");
        b.UseSetting("Elifoot:AutoSeedDefaultWorld","false");b.UseSetting("Elifoot:DisableBackgroundClock","true");
    });

    [Fact]
    public async Task AssistDivisionHistoryAndOneRoundWorksSurviveDatabaseRestart()
    {
        var path=Path.Combine(Path.GetTempPath(),$"elifoot-assists-{Guid.NewGuid():N}.db");
        var w=WorldSeedFactory.CreateSeededWorld();w.SeasonNumber=2;w.PlayerStatisticsVersion=9;
        var c=w.Clubs[0];c.CashBalance=10_000_000;int capacity=c.Stadium.BuiltCapacity;
        Assert.True(MarketAndEconomyEngine.StartStadiumExpansion(w,c,5000).Success);
        foreach(var position in Enum.GetValues<PlayerPosition>()) {
            var p=w.Players.First(p=>p.Position==position);
            w.PlayerStatistics.Add(new(){PlayerId=p.Id,Position=position,Season=1,CompetitionType=CompetitionType.League,
                Division=DivisionTier.QuartaDivisao,MatchesPlayed=2,Assists=4,GoalsScored=3,HasIncompleteAssists=false});
        }
        try {
            await using(var initial=Factory(path)) {
                using var client=initial.CreateClient();await initial.Services.GetRequiredService<IWorldRepository>().SaveWorldAsync(w);
                var dto=await client.GetFromJsonAsync<PlayerRankingsDto>($"/api/worlds/{w.Id}/player-rankings?category=assists&scope=division4&period=career");
                Assert.Equal(4,dto!.Rows.Count);Assert.All(dto.Rows,r=>Assert.Equal(4,r.Assists));Assert.Equal(0,dto.MinimumMatches);
            }
            EfWorldRepository.ClearCacheForTesting();
            await using(var restarted=Factory(path)) {
                using var client=restarted.CreateClient();var repo=restarted.Services.GetRequiredService<IWorldRepository>();
                var restored=(await repo.GetWorldAsync(w.Id))!;var club=restored.Clubs.Single(x=>x.Id==c.Id);
                Assert.Equal(capacity,club.Stadium.BuiltCapacity);Assert.Equal(1,club.Stadium.ExpansionRoundsRemaining);
                MarketAndEconomyEngine.SettleMatchAndRoundFinancials(restored,[]);await repo.SaveWorldAsync(restored);
                Assert.Equal(capacity+5000,club.Stadium.BuiltCapacity);Assert.Equal(0,club.Stadium.ExpansionRoundsRemaining);
                var historic=await client.GetFromJsonAsync<PlayerRankingsDto>($"/api/worlds/{w.Id}/player-rankings?category=assists&scope=division4&period=season&season=1");
                Assert.Equal(4,historic!.Rows.Count);Assert.All(historic.Rows,r=>Assert.Equal(4,r.Assists));
                var current=await client.GetFromJsonAsync<PlayerRankingsDto>($"/api/worlds/{w.Id}/player-rankings?category=assists&scope=division4&period=current");
                Assert.Empty(current!.Rows);
                var scorers=await client.GetFromJsonAsync<PlayerRankingsDto>($"/api/worlds/{w.Id}/player-rankings?category=scorers&scope=division4&period=career");
                Assert.Equal(4,scorers!.Rows.Count);Assert.All(scorers.Rows,r=>Assert.Equal(3,r.GoalsScored));
            }
        } finally {EfWorldRepository.ClearCacheForTesting();if(File.Exists(path))File.Delete(path);}
    }
}
