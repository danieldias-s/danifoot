using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Collections.Concurrent;
using System.IO.Compression;
using Microsoft.AspNetCore.SignalR.Client;
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
    private static WebApplicationFactory<Program> Factory(string file, bool compress = false) => new WebApplicationFactory<Program>().WithWebHostBuilder(b => {
        b.UseEnvironment("Development"); b.UseSetting("Elifoot:AuthMode","LocalDev"); b.UseSetting("Elifoot:DatabaseProvider","Sqlite");
        b.UseSetting("ConnectionStrings:DefaultConnection",$"Data Source={file};Pooling=False");
        b.UseSetting("Elifoot:AutoSeedDefaultWorld","false"); b.UseSetting("Elifoot:DisableBackgroundClock","true");
        b.UseSetting("Elifoot:CompressNewSqliteSnapshots", compress.ToString());
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SqliteRollsBackSnapshotAndBothLedgerLegsWhenCreditInsertFailsAndReceiptCanRetry(bool compress)
    {
        string file=Path.Combine(Path.GetTempPath(),$"elifoot-v3-02a-failure-{Guid.NewGuid():N}.db");
        try
        {
            var(w,c,p)=Setup();string url=$"/api/worlds/{w.Id}/market/buy-listed?coachId={c[0].Id}&playerId={p.Id}&idempotencyKey=retry&expectedRoundVersion={w.RoundVersion}&salaryTermsToken={MarketContractTermsEngine.SalaryTermsToken(w,p)}";
            await using(var factory=Factory(file, compress))
            {
                using var client=factory.CreateClient();await factory.Services.GetRequiredService<IWorldRepository>().SaveWorldAsync(w);
                var (other, otherCoaches, _) = Setup();
                await factory.Services.GetRequiredService<IWorldRepository>().SaveWorldAsync(other);
                HubConnection Connect() => new HubConnectionBuilder().WithUrl("http://localhost/hubs/gameroom",
                    options => options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler()).Build();
                await using var buyer = Connect();
                await using var seller = Connect();
                await using var elsewhere = Connect();
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(45));
                var ct = timeout.Token;
                var buyerEvents = new ConcurrentQueue<WorldDataChange>();
                var sellerEvents = new ConcurrentQueue<WorldDataChange>();
                var outsideEvents = new ConcurrentQueue<WorldDataChange>();
                var both = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var sellerReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                buyer.On<WorldDataChange>("WorldDataChanged", change => { buyerEvents.Enqueue(change); both.TrySetResult(); });
                seller.On<WorldDataChange>("WorldDataChanged", change => { sellerEvents.Enqueue(change); sellerReady.TrySetResult(); });
                elsewhere.On<WorldDataChange>("WorldDataChanged", change => outsideEvents.Enqueue(change));
                await Task.WhenAll(buyer.StartAsync(ct), seller.StartAsync(ct), elsewhere.StartAsync(ct));
                await buyer.InvokeAsync<LiveRoundSnapshotDto>("JoinRoomV2", w.Id, c[0].Id, ct);
                await seller.InvokeAsync<LiveRoundSnapshotDto>("JoinRoomV2", w.Id, c[1].Id, ct);
                await elsewhere.InvokeAsync<LiveRoundSnapshotDto>("JoinRoomV2", other.Id, otherCoaches[0].Id, ct);
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
                Assert.Equal(HttpStatusCode.ServiceUnavailable,
                    (await client.GetAsync($"/api/worlds/{w.Id}/snapshot", ct)).StatusCode);
                Assert.Empty(buyerEvents);
                Assert.Empty(sellerEvents);
                using(var scope=factory.Services.CreateScope())
                {
                    var db=scope.ServiceProvider.GetRequiredService<ElifootDbContext>();
                    var snapshot=await db.Worlds.AsNoTracking().Where(x=>x.Id==w.Id).Select(x=>x.SnapshotJson).SingleAsync();
                    var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    const string prefix = "ELIFOOT:GZIP:1:";
                    Assert.Equal(compress, snapshot.StartsWith(prefix, StringComparison.Ordinal));
                    using var decoded = compress
                        ? new GZipStream(new MemoryStream(Convert.FromBase64String(snapshot[prefix.Length..])), CompressionMode.Decompress)
                        : null;
                    var durable = (decoded == null ? JsonSerializer.Deserialize<GameWorld>(snapshot, options)
                        : await JsonSerializer.DeserializeAsync<GameWorld>(decoded, options, ct))!;
                    Assert.Equal(c[1].CurrentClubId,durable.Players.Single(x=>x.Id==p.Id).ClubId);
                    Assert.Equal(0,await db.FinancialLedger.CountAsync(t=>t.GameWorldId==w.Id&&t.IdempotencyKey.StartsWith("retry")));
                    await db.Database.ExecuteSqlRawAsync("DROP TRIGGER stage02a_fail_credit");
                }
                Assert.True((await client.PostAsync(url,null)).IsSuccessStatusCode);
                await Task.WhenAll(both.Task, sellerReady.Task).WaitAsync(ct);
                Assert.True((await client.PostAsync(url,null)).IsSuccessStatusCode);
                // The join responses also drain previously queued messages on each connection.
                await buyer.InvokeAsync<LiveRoundSnapshotDto>("JoinRoomV2", w.Id, c[0].Id, ct);
                await seller.InvokeAsync<LiveRoundSnapshotDto>("JoinRoomV2", w.Id, c[1].Id, ct);
                await elsewhere.InvokeAsync<LiveRoundSnapshotDto>("JoinRoomV2", other.Id, otherCoaches[0].Id, ct);
                var change = Assert.Single(buyerEvents);
                Assert.Equal(change.Version, Assert.Single(sellerEvents).Version);
                Assert.Equal(w.Id, change.WorldId);
                Assert.Equal(w.RoundVersion, change.Version);
                Assert.Contains("market", change.Areas);
                Assert.Contains("squad", change.Areas);
                Assert.Empty(outsideEvents);
                Assert.Equal(c[0].CurrentClubId,p.ClubId);
                using(var scope=factory.Services.CreateScope())
                {
                    var db=scope.ServiceProvider.GetRequiredService<ElifootDbContext>();
                    var ledger=await db.FinancialLedger.Where(t=>t.GameWorldId==w.Id&&t.IdempotencyKey.StartsWith("retry")).ToListAsync();
                    Assert.Equal(2,ledger.Count);Assert.Equal(0,ledger.Sum(t=>t.Amount));
                }
            }
            EfWorldRepository.ClearCacheForTesting();
            await using(var restart=Factory(file, !compress))
            {
                using var client=restart.CreateClient();Assert.True((await client.PostAsync(url,null)).IsSuccessStatusCode);
                var restored=(await restart.Services.GetRequiredService<IWorldRepository>().GetWorldAsync(w.Id))!;
                Assert.Equal(c[0].CurrentClubId,restored.Players.Single(x=>x.Id==p.Id).ClubId);
                Assert.Equal(2,restored.Transactions.Count(t=>t.PlayerId==p.Id));
                using var scope = restart.Services.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<ElifootDbContext>();
                await restart.Services.GetRequiredService<IWorldRepository>().SaveWorldAsync(restored);
                var stored = await db.Worlds.Where(x => x.Id == w.Id).Select(x => x.SnapshotJson).SingleAsync();
                Assert.Equal(compress, stored.StartsWith("ELIFOOT:GZIP:1:", StringComparison.Ordinal));
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
