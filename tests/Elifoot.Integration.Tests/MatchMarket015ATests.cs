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

[Collection("Player rankings 009 SQLite")]
public class MatchMarket015ATests
{
    private static WebApplicationFactory<Program> Factory(string connection, bool postgres) => new WebApplicationFactory<Program>().WithWebHostBuilder(b => {
        b.UseSetting("Elifoot:AuthMode", "LocalDev"); b.UseSetting("Elifoot:DatabaseProvider",postgres?"PostgreSQL":"Sqlite");
        b.UseSetting("ConnectionStrings:DefaultConnection",connection);
        b.UseSetting("Elifoot:AutoSeedDefaultWorld","false");
        b.UseSetting("Elifoot:DisableBackgroundClock","true");
    });
    private static (GameWorld W, HumanCoach[] Coaches, Player Player) Seed(int humans=3)
    {
        var w=WorldSeedFactory.CreateSeededWorld(totalClubs:64);w.Auctions.Clear();
        foreach(var club in w.Clubs)club.HumanCoachId=Guid.NewGuid();
        var coaches=Enumerable.Range(0,humans).Select(i=>new HumanCoach{Nickname="015A Coach "+i,CurrentClubId=w.Clubs[i].Id,IsConnected=true}).ToArray();
        foreach(var coach in coaches){w.HumanCoaches.Add(coach);var c=w.Clubs.Single(c=>c.Id==coach.CurrentClubId);c.HumanCoachId=coach.Id;c.CashBalance=10_000_000;}
        return(w,coaches,w.Players.Last(p=>p.ClubId==coaches[0].CurrentClubId&&p.Position==PlayerPosition.A));
    }
    [Theory][InlineData(false)][InlineData(true)]
    public async Task FirstValidBuyerWinsAndDurableLedgerReceiptAndCancelledOffersSurviveRestart(bool postgres)
    {
        string file=Path.Combine(Path.GetTempPath(),$"elifoot-015a-{Guid.NewGuid():N}.db");
        string conn=postgres?Environment.GetEnvironmentVariable("ELIFOOT_015A_MARKET_PG")??throw new InvalidOperationException("Use o runner isolado 015A."):$"Data Source={file};Pooling=False";
        var(w,coaches,p)=Seed();Guid winner=Guid.Empty;string? winningUrl=null;decimal price=123456.78m;
        try {
            await using(var factory=Factory(conn,postgres)){
                using var client=factory.CreateClient();await factory.Services.GetRequiredService<IWorldRepository>().SaveWorldAsync(w);
                using var serviceScope=factory.Services.CreateScope();var svc=serviceScope.ServiceProvider.GetRequiredService<ClubAndMarketApplicationService>();
                Assert.True((await svc.ListPlayerForTransferAsync(w.Id,coaches[0].Id,p.Id,price,idempotencyKey:"list")).Success);
                Assert.True((await svc.CreateTransferOfferAsync(w.Id,coaches[1].Id,p.Id,100000,"pending")).Success);
                Assert.Equal(TransferOfferStatus.Pending,Assert.Single(w.TransferOffers).Status);
                var listing=(await client.GetFromJsonAsync<JsonElement>($"/api/worlds/{w.Id}/market")).GetProperty("listings").EnumerateArray().Single(x=>x.GetProperty("playerId").GetGuid()==p.Id);
                long version=w.RoundVersion;long listingVersion=listing.GetProperty("listingVersion").GetInt64();
                string Buy(HumanCoach c)=>$"/api/worlds/{w.Id}/market/buy-listed?coachId={c.Id}&playerId={p.Id}&listingVersion={listingVersion}&sellerClubId={coaches[0].CurrentClubId}&askingPrice=123456.78&idempotencyKey=buy-{c.Id}&expectedRoundVersion={version}";
                var urls=coaches.Skip(1).Select(Buy).ToArray();
                var attempts=await Task.WhenAll(urls.Select(u=>client.PostAsync(u,null)));
                Assert.Single(attempts,r=>r.IsSuccessStatusCode);
                Assert.Contains("JOGADOR JÁ VENDIDO",await attempts.Single(r=>!r.IsSuccessStatusCode).Content.ReadAsStringAsync());
                int index=Array.FindIndex(attempts,r=>r.IsSuccessStatusCode);winner=coaches[index+1].CurrentClubId!.Value;winningUrl=urls[index];
                Assert.Equal(winner,p.ClubId);Assert.False(p.IsListedForTransfer);Assert.Null(p.AskingPrice);
                Assert.Equal(TransferOfferStatus.Cancelled,Assert.Single(w.TransferOffers).Status);
                Assert.Equal(10_000_000+price,w.Clubs[0].CashBalance);
                Assert.Equal(10_000_000-price,w.Clubs.Single(c=>c.Id==winner).CashBalance);
                var ledger=w.Transactions.Where(t=>t.PlayerId==p.Id&&t.TransactionType=="DirectPurchase").ToList();
                Assert.Equal(2,ledger.Count);Assert.Equal(0,ledger.Sum(t=>t.Amount));
                Assert.True((await client.PostAsync(winningUrl,null)).IsSuccessStatusCode);Assert.Equal(2,w.Transactions.Count(t=>t.PlayerId==p.Id));
                Assert.False((await svc.RespondToTransferOfferAsync(w.Id,coaches[0].Id,w.TransferOffers.Single().Id,true,"stale-offer")).Success);
            }
            EfWorldRepository.ClearCacheForTesting();
            await using(var restart=Factory(conn,postgres)){
                using var client=restart.CreateClient();var restored=(await restart.Services.GetRequiredService<IWorldRepository>().GetWorldAsync(w.Id))!;
                Assert.Equal(winner,restored.Players.Single(x=>x.Id==p.Id).ClubId);
                Assert.Equal(10_000_000+price,restored.Clubs[0].CashBalance);
                Assert.Equal(TransferOfferStatus.Cancelled,restored.TransferOffers.Single().Status);
                Assert.True((await client.PostAsync(winningUrl,null)).IsSuccessStatusCode);Assert.Equal(2,restored.Transactions.Count(t=>t.PlayerId==p.Id));
                using var scope=restart.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<ElifootDbContext>();
                Assert.Equal(2,await db.FinancialLedger.CountAsync(t=>t.GameWorldId==w.Id&&t.IdempotencyKey.Contains("buy-")));
                Assert.Equal(0,await db.FinancialLedger.Where(t=>t.GameWorldId==w.Id&&t.IdempotencyKey.Contains("buy-")).SumAsync(t=>t.Amount));
            }
        } finally { EfWorldRepository.ClearCacheForTesting();if(!postgres&&File.Exists(file))File.Delete(file); }
    }
    [Fact]
    public async Task ListingRevisionCounterOfferAndIncompatibleModalitiesCannotBypassSellerConsent()
    {
        var(w,c,p)=Seed();var repo=new InMemoryWorldRepository();await repo.SaveWorldAsync(w);var svc=new ClubAndMarketApplicationService(repo,NullMatchRoomNotificationPublisher.Instance);
        Assert.True((await svc.ListPlayerForTransferAsync(w.Id,c[0].Id,p.Id,200000,idempotencyKey:"list")).Success);long old=p.TransferListingVersion;
        Assert.True((await svc.ListPlayerForTransferAsync(w.Id,c[0].Id,p.Id,250000,idempotencyKey:"reprice")).Success);
        Assert.False((await svc.BuyListedPlayerAsync(w.Id,c[1].Id,p.Id,"stale-price",listingVersion:old,sellerClubId:c[0].CurrentClubId,askingPrice:200000)).Success);
        var offer=await svc.CreateTransferOfferAsync(w.Id,c[1].Id,p.Id,170000,"offer");Assert.True(offer.Success);Assert.Equal(c[0].CurrentClubId,p.ClubId);Assert.Equal(250000,p.AskingPrice);
        Assert.False((await svc.CounterTransferOfferAsync(w.Id,c[1].Id,offer.Offer!.OfferId,190000,"not-seller")).Success);
        Assert.True((await svc.CounterTransferOfferAsync(w.Id,c[0].Id,offer.Offer!.OfferId,190000,"counter")).Success);Assert.Equal(250000,p.AskingPrice);
        Assert.False((await svc.RespondToTransferOfferAsync(w.Id,c[0].Id,offer.Offer.OfferId,true,"seller-cannot-accept-own-counter")).Success);
        Assert.True((await svc.RespondToTransferOfferAsync(w.Id,c[1].Id,offer.Offer.OfferId,true,"accept")).Success);
        Assert.Equal(c[1].CurrentClubId,p.ClubId);Assert.Equal(TransferOfferStatus.Accepted,w.TransferOffers.Single().Status);Assert.Equal(2,w.Transactions.Count(t=>t.PlayerId==p.Id));
    }
    [Fact]
    public async Task WithdrawalAndRepublishInvalidatePreviouslyConfirmedPriceAndListingVersion()
    {
        var(w,c,p)=Seed();var repo=new InMemoryWorldRepository();await repo.SaveWorldAsync(w);var svc=new ClubAndMarketApplicationService(repo,NullMatchRoomNotificationPublisher.Instance);
        Assert.True((await svc.ListPlayerForTransferAsync(w.Id,c[0].Id,p.Id,200000,idempotencyKey:"list")).Success);long old=p.TransferListingVersion;
        Assert.True((await svc.WithdrawTransferListingAsync(w.Id,c[0].Id,p.Id,"withdraw")).Success);Assert.False(p.IsListedForTransfer);
        Assert.True((await svc.ListPlayerForTransferAsync(w.Id,c[0].Id,p.Id,200000,idempotencyKey:"relist")).Success);
        Assert.False((await svc.BuyListedPlayerAsync(w.Id,c[1].Id,p.Id,"stale",listingVersion:old)).Success);Assert.DoesNotContain(w.Transactions,t=>t.Category is FinancialTransactionCategory.TransferPurchase or FinancialTransactionCategory.TransferSale);
        Assert.True((await svc.WithdrawTransferListingAsync(w.Id,c[0].Id,p.Id,"withdraw-again")).Success);
        Assert.True((await svc.CreateVoluntaryAuctionAsync(w.Id,c[0].Id,p.Id,idempotencyKey:"auction")).Success);
        Assert.False((await svc.ListPlayerForTransferAsync(w.Id,c[0].Id,p.Id,200000,idempotencyKey:"incompatible")).Success);
    }
    [Theory]
    [InlineData(RoundState.RUNNING_FIRST_HALF)][InlineData(RoundState.HALFTIME_PAUSED)][InlineData(RoundState.RUNNING_SECOND_HALF)]
    [InlineData(RoundState.GLOBAL_PAUSED)][InlineData(RoundState.EXTRA_TIME_BREAK)][InlineData(RoundState.RUNNING_EXTRA_TIME)][InlineData(RoundState.EXTRA_TIME_HALFTIME)]
    public async Task EveryLivePhaseRejectsFinancialMutationsEvenWithoutFlutter(RoundState stage)
    {
        var(w,c,p)=Seed();var repo=new InMemoryWorldRepository();await repo.SaveWorldAsync(w);var svc=new ClubAndMarketApplicationService(repo,NullMatchRoomNotificationPublisher.Instance);
        Assert.True((await svc.ListPlayerForTransferAsync(w.Id,c[0].Id,p.Id,200000,idempotencyKey:"list")).Success);w.RoundState=stage;
        Assert.False((await svc.BuyListedPlayerAsync(w.Id,c[1].Id,p.Id,"buy")).Success);
        Assert.False((await svc.WithdrawTransferListingAsync(w.Id,c[0].Id,p.Id,"withdraw")).Success);
        Assert.False((await svc.CreateTransferOfferAsync(w.Id,c[1].Id,p.Id,170000,"offer")).Success);
        Assert.False((await svc.TakeBankLoanAsync(w.Id,c[0].Id,500000,0,idempotencyKey:"loan")).Success);
        Assert.Equal(c[0].CurrentClubId,p.ClubId);Assert.DoesNotContain(w.Transactions,t=>t.Category is FinancialTransactionCategory.TransferPurchase or FinancialTransactionCategory.TransferSale);
        w.RoundState=RoundState.RESULTS;Assert.True((await svc.BuyListedPlayerAsync(w.Id,c[1].Id,p.Id,"buy-results")).Success);
    }
    [Fact]
    public async Task NoFundsExpiredContractLastGoalkeeperAndMaximumSquadCannotBeBought()
    {
        var(w,c,p)=Seed();var repo=new InMemoryWorldRepository();await repo.SaveWorldAsync(w);var svc=new ClubAndMarketApplicationService(repo,NullMatchRoomNotificationPublisher.Instance);
        w.Economy.Rules.SeasonContractsEnabled=false;
        Assert.True((await svc.ListPlayerForTransferAsync(w.Id,c[0].Id,p.Id,200000,idempotencyKey:"list")).Success);
        w.Clubs[1].CashBalance=0;Assert.False((await svc.BuyListedPlayerAsync(w.Id,c[1].Id,p.Id,"no-cash")).Success);w.Clubs[1].CashBalance=10_000_000;
        p.ContractRoundsRemaining=0;Assert.False((await svc.BuyListedPlayerAsync(w.Id,c[1].Id,p.Id,"expired")).Success);p.ContractRoundsRemaining=20;
        while(w.Players.Count(x=>x.ClubId==c[1].CurrentClubId)<24)w.Players.Add(new Player{GameWorldId=w.Id,ClubId=c[1].CurrentClubId});
        Assert.False((await svc.BuyListedPlayerAsync(w.Id,c[1].Id,p.Id,"full")).Success);
        var keeper=w.Players.First(x=>x.ClubId==c[0].CurrentClubId&&x.Position==PlayerPosition.G);
        w.Players.RemoveAll(x=>x.ClubId==c[0].CurrentClubId&&x.Position==PlayerPosition.G&&x.Id!=keeper.Id);
        Assert.False((await svc.ListPlayerForTransferAsync(w.Id,c[0].Id,keeper.Id,1,idempotencyKey:"last-keeper")).Success);
        Assert.DoesNotContain(w.Transactions,t=>t.Category is FinancialTransactionCategory.TransferPurchase or FinancialTransactionCategory.TransferSale);
    }

    [Fact]
    public async Task UnrenewedAthleteCanBeSoldAndRenewalClosesTheListing()
    {
        var (world, coaches, player) = Seed();
        var repo = new InMemoryWorldRepository();
        await repo.SaveWorldAsync(world);
        var service = new ClubAndMarketApplicationService(repo, NullMatchRoomNotificationPublisher.Instance);
        player.ContractRoundsRemaining = 0;
        Assert.True((await service.ListPlayerForTransferAsync(world.Id, coaches[0].Id, player.Id, 200_000, idempotencyKey: "list")).Success);
        Assert.True((await service.RenewPlayerContractAsync(world.Id, coaches[0].Id, player.Id, 0, idempotencyKey: "renew")).Success);
        Assert.False((await service.BuyListedPlayerAsync(world.Id, coaches[1].Id, player.Id, "stale-buy")).Success);
        Assert.False((await service.ListPlayerForTransferAsync(world.Id, coaches[0].Id, player.Id, 200_000, idempotencyKey: "protected")).Success);

        world.SeasonNumber++;
        world.RoundNumber = 1;
        player.ContractRoundsRemaining = 0;
        Assert.True((await service.ListPlayerForTransferAsync(world.Id, coaches[0].Id, player.Id, 200_000, idempotencyKey: "next-list")).Success);
        Assert.True((await service.BuyListedPlayerAsync(world.Id, coaches[1].Id, player.Id, "buy")).Success);
        Assert.Equal(coaches[1].CurrentClubId, player.ClubId);
    }
}
