using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Domain;
using Elifoot.Infrastructure;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Elifoot.Integration.Tests;

[Collection("Player rankings 009 SQLite")]
public class MarketValidationV3Stage02AIntegrationTests
{
    private static WebApplicationFactory<Program> Factory(string file) => new WebApplicationFactory<Program>().WithWebHostBuilder(b => {
        b.UseEnvironment("Development"); b.UseSetting("Elifoot:AuthMode","LocalDev"); b.UseSetting("Elifoot:DatabaseProvider","Sqlite");
        b.UseSetting("ConnectionStrings:DefaultConnection",$"Data Source={file};Pooling=False");
        b.UseSetting("Elifoot:AutoSeedDefaultWorld","false"); b.UseSetting("Elifoot:DisableBackgroundClock","true");
        b.ConfigureLogging(logging=>logging.ClearProviders());
    });

    private static (GameWorld W,HumanCoach[] Coaches,Player Player) Setup()
    {
        var w=WorldSeedFactory.CreateSeededWorld(totalClubs:64); w.Auctions.Clear();
        foreach(var club in w.Clubs) club.HumanCoachId=Guid.NewGuid();
        var coaches=w.Clubs.Take(3).Select(c=>new HumanCoach { GameWorldId=w.Id,CurrentClubId=c.Id }).ToArray();
        foreach(var coach in coaches) { w.HumanCoaches.Add(coach);w.Clubs.Single(c=>c.Id==coach.CurrentClubId).HumanCoachId=coach.Id; }
        var p=w.Players.Last(p=>p.ClubId==coaches[1].CurrentClubId&&p.Position==PlayerPosition.A);
        p.IsListedForTransfer=true;p.AskingPrice=100_000;
        return(w,coaches,p);
    }

    [Fact]
    public async Task SqliteRollsBackSnapshotAndBothLedgerLegsWhenCreditInsertFailsAndReceiptCanRetry()
    {
        string file=Path.Combine(Path.GetTempPath(),$"elifoot-v3-02a-failure-{Guid.NewGuid():N}.db");
        try
        {
            var(w,c,p)=Setup();string url=$"/api/worlds/{w.Id}/market/buy-listed?coachId={c[0].Id}&playerId={p.Id}&idempotencyKey=retry&expectedRoundVersion={w.RoundVersion}";
            await using(var factory=Factory(file))
            {
                using var client=factory.CreateClient();await factory.Services.GetRequiredService<IWorldRepository>().SaveWorldAsync(w);
                using(var scope=factory.Services.CreateScope())
                {
                    var db=scope.ServiceProvider.GetRequiredService<ElifootDbContext>();
                    await db.Database.ExecuteSqlRawAsync("""
                        CREATE TRIGGER stage02a_fail_credit BEFORE INSERT ON ef_financial_ledger
                        WHEN CAST(NEW."Amount" AS REAL) > 0
                        BEGIN SELECT RAISE(ABORT, 'STAGE02A_CONTROLLED_CREDIT_FAILURE'); END;
                        """);
                }
                var response=await client.PostAsync(url,null);
                Assert.Equal(HttpStatusCode.InternalServerError,response.StatusCode);
                Assert.Contains("MARKET_PERSISTENCE_FAILED",await response.Content.ReadAsStringAsync());
                using(var scope=factory.Services.CreateScope())
                {
                    var db=scope.ServiceProvider.GetRequiredService<ElifootDbContext>();
                    var snapshot=await db.Worlds.AsNoTracking().Where(x=>x.Id==w.Id).Select(x=>x.SnapshotJson).SingleAsync();
                    var durable=JsonSerializer.Deserialize<GameWorld>(snapshot,new JsonSerializerOptions { PropertyNameCaseInsensitive=true })!;
                    Assert.Equal(c[1].CurrentClubId,durable.Players.Single(x=>x.Id==p.Id).ClubId);
                    Assert.Equal(0,await db.FinancialLedger.CountAsync(t=>t.GameWorldId==w.Id&&t.IdempotencyKey.StartsWith("retry")));
                    await db.Database.ExecuteSqlRawAsync("DROP TRIGGER stage02a_fail_credit");
                }
                Assert.True((await client.PostAsync(url,null)).IsSuccessStatusCode);
                Assert.True((await client.PostAsync(url,null)).IsSuccessStatusCode);
                Assert.Equal(c[0].CurrentClubId,p.ClubId);
                using(var scope=factory.Services.CreateScope())
                {
                    var db=scope.ServiceProvider.GetRequiredService<ElifootDbContext>();
                    var ledger=await db.FinancialLedger.Where(t=>t.GameWorldId==w.Id&&t.IdempotencyKey.StartsWith("retry")).ToListAsync();
                    Assert.Equal(2,ledger.Count);Assert.Equal(0,ledger.Sum(t=>t.Amount));
                }
            }
            EfWorldRepository.ClearCacheForTesting();
            await using(var restart=Factory(file))
            {
                using var client=restart.CreateClient();Assert.True((await client.PostAsync(url,null)).IsSuccessStatusCode);
                var restored=(await restart.Services.GetRequiredService<IWorldRepository>().GetWorldAsync(w.Id))!;
                Assert.Equal(c[0].CurrentClubId,restored.Players.Single(x=>x.Id==p.Id).ClubId);
                Assert.Equal(2,restored.Transactions.Count(t=>t.PlayerId==p.Id));
            }
        }
        finally { EfWorldRepository.ClearCacheForTesting();if(File.Exists(file))File.Delete(file); }
    }

    [Fact]
    public async Task ConcurrentHttpBidsCannotReserveTwoPlayersForOneVacancyAndPersistTheWinningReceipt()
    {
        string file=Path.Combine(Path.GetTempPath(),$"elifoot-v3-02a-bids-{Guid.NewGuid():N}.db");
        try
        {
            var(w,c,_)=Setup();var buyer=w.Clubs.Single(x=>x.Id==c[0].CurrentClubId);
            while(w.Players.Count(p=>p.ClubId==buyer.Id)<23)w.Players.Add(new Player { GameWorldId=w.Id,ClubId=buyer.Id });
            var auctions=Enumerable.Range(0,2).Select(_=>{
                var p=new Player { GameWorldId=w.Id };w.Players.Add(p);
                var a=new AuctionItem { GameWorldId=w.Id,PlayerId=p.Id,Reason=AuctionReason.SalaryRenewalRefused,
                    MinimumBid=0,CreatedAtSeason=w.SeasonNumber,CreatedAtRound=w.RoundNumber,ExpiresAtRound=w.RoundNumber+2 };
                w.Auctions.Add(a);return a;
            }).ToArray();
            string Url(AuctionItem a)=>$"/api/worlds/{w.Id}/market/auction/{a.Id}/bid?coachId={c[0].Id}&bidAmount=0&idempotencyKey={a.Id}";
            string? winning=null;
            await using(var factory=Factory(file))
            {
                using var client=factory.CreateClient();await factory.Services.GetRequiredService<IWorldRepository>().SaveWorldAsync(w);
                var attempts=await Task.WhenAll(auctions.Select(a=>client.PostAsync(Url(a),null)));
                Assert.Single(attempts,x=>x.IsSuccessStatusCode);
                winning=Url(auctions[Array.FindIndex(attempts,x=>x.IsSuccessStatusCode)]);
                Assert.True((await client.PostAsync(winning,null)).IsSuccessStatusCode);
                Assert.Single(w.Auctions,a=>a.HighestBidderClubId==buyer.Id);
                Assert.DoesNotContain(w.Transactions,t=>t.Category==FinancialTransactionCategory.AuctionPurchase);
            }
            EfWorldRepository.ClearCacheForTesting();
            await using(var restart=Factory(file))
            {
                using var client=restart.CreateClient();Assert.True((await client.PostAsync(winning,null)).IsSuccessStatusCode);
                var restored=(await restart.Services.GetRequiredService<IWorldRepository>().GetWorldAsync(w.Id))!;
                Assert.Single(restored.Auctions,a=>a.HighestBidderClubId==buyer.Id);
                Assert.Equal(23,restored.Players.Count(p=>p.ClubId==buyer.Id));
            }
        }
        finally { EfWorldRepository.ClearCacheForTesting();if(File.Exists(file))File.Delete(file); }
    }
}
