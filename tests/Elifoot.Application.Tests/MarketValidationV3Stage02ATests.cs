using System.Text.Json;
using Elifoot.Application;
using Elifoot.Contracts;
using Elifoot.Domain;
using Elifoot.Infrastructure;

namespace Elifoot.Application.Tests;

public class MarketValidationV3Stage02ATests
{
    private static (GameWorld W, HumanCoach Buyer, HumanCoach Seller, Player Player) Setup()
    {
        var w=WorldSeedFactory.CreateSeededWorld(totalClubs:64); w.Auctions.Clear();
        foreach(var club in w.Clubs) club.HumanCoachId=Guid.NewGuid();
        var coaches=w.Clubs.Take(2).Select(club=>new HumanCoach { GameWorldId=w.Id,CurrentClubId=club.Id }).ToArray();
        foreach(var c in coaches) { w.HumanCoaches.Add(c); w.Clubs.Single(x=>x.Id==c.CurrentClubId).HumanCoachId=c.Id; }
        var player=w.Players.Last(p=>p.ClubId==coaches[1].CurrentClubId&&p.Position==PlayerPosition.A);
        player.IsListedForTransfer=true; player.AskingPrice=100_000;
        return(w,coaches[0],coaches[1],player);
    }

    private static void FillTo23(GameWorld w,HumanCoach buyer)
    {
        while(w.Players.Count(p=>p.ClubId==buyer.CurrentClubId)<23)
            w.Players.Add(new Player { GameWorldId=w.Id,ClubId=buyer.CurrentClubId,Position=PlayerPosition.D });
    }

    private static AuctionItem Auction(GameWorld w)
    {
        var player=new Player { GameWorldId=w.Id,Strength=40 }; w.Players.Add(player);
        var a=new AuctionItem { GameWorldId=w.Id,PlayerId=player.Id,Reason=AuctionReason.SalaryRenewalRefused,
            MinimumBid=0,CreatedAtSeason=w.SeasonNumber,CreatedAtRound=w.RoundNumber,ExpiresAtRound=w.RoundNumber+2 };
        w.Auctions.Add(a); return a;
    }

    [Fact]
    public async Task BilateralAcceptanceAndDirectPurchaseRespectTheSameReservedVacancy()
    {
        var(w,b,s,p)=Setup();var repo=new InMemoryWorldRepository();await repo.SaveWorldAsync(w);
        var service=new ClubAndMarketApplicationService(repo,NullMatchRoomNotificationPublisher.Instance);
        var offer=await service.CreateTransferOfferAsync(w.Id,b.Id,p.Id,90_000,"offer");
        Assert.True(offer.Success,offer.Error); FillTo23(w,b);
        var a=Auction(w); var buyer=w.Clubs.Single(c=>c.Id==b.CurrentClubId);
        Assert.True(ClassicEconomyEngine.PlaceBid(w,buyer,a.Id,0,"reserve").Success);
        var accepted=await service.RespondToTransferOfferAsync(w.Id,s.Id,offer.Offer!.OfferId,true,"accept");
        var direct=await service.BuyListedPlayerAsync(w.Id,b.Id,p.Id,"direct");
        Assert.False(accepted.Success);Assert.Contains("MARKET_SQUAD_FULL",accepted.Error);
        Assert.False(direct.Success);Assert.Contains("MARKET_SQUAD_FULL",direct.Error);
        Assert.Equal(s.CurrentClubId,p.ClubId);Assert.Equal(TransferOfferStatus.Pending,w.TransferOffers.Single().Status);
        Assert.DoesNotContain(w.Transactions,t=>t.PlayerId==p.Id);
    }

    [Fact]
    public async Task ConcurrentBidAndPurchaseCannotPromiseTheSameLastVacancy()
    {
        var(w,b,s,p)=Setup();FillTo23(w,b);var a=Auction(w);
        var repo=new InMemoryWorldRepository();await repo.SaveWorldAsync(w);
        var service=new ClubAndMarketApplicationService(repo,NullMatchRoomNotificationPublisher.Instance);
        var results=await Task.WhenAll(service.PlaceAuctionBidAsync(w.Id,b.Id,a.Id,0,idempotencyKey:"bid"),
            service.BuyListedPlayerAsync(w.Id,b.Id,p.Id,"buy"));
        Assert.Single(results,r=>r.Success);
        Assert.Equal(24,w.Players.Count(x=>x.ClubId==b.CurrentClubId)+w.Auctions.Count(x=>!x.IsClosed&&x.HighestBidderClubId==b.CurrentClubId));
        Assert.Equal(p.ClubId==b.CurrentClubId?2:0,w.Transactions.Count(t=>t.PlayerId==p.Id));
        Assert.True(p.ClubId==b.CurrentClubId||p.ClubId==s.CurrentClubId);
    }

    [Fact]
    public async Task FailedPersistenceDoesNotConfirmAPartialPurchaseAndSameReceiptRetriesDurability()
    {
        var(w,b,s,p)=Setup();var repo=new FailsOnce(w);
        var service=new ClubAndMarketApplicationService(repo,NullMatchRoomNotificationPublisher.Instance);
        var initial=JsonSerializer.Deserialize<GameWorld>(repo.DurableSnapshot)!;
        decimal buyerCash=w.Clubs.Single(c=>c.Id==b.CurrentClubId).CashBalance,sellerCash=w.Clubs.Single(c=>c.Id==s.CurrentClubId).CashBalance;
        long version=w.RoundVersion;
        await Assert.ThrowsAsync<IOException>(()=>service.BuyListedPlayerAsync(w.Id,b.Id,p.Id,"same",expectedRoundVersion:version));
        var durableBeforeRetry=JsonSerializer.Deserialize<GameWorld>(repo.DurableSnapshot)!;
        Assert.Equal(s.CurrentClubId,durableBeforeRetry.Players.Single(x=>x.Id==p.Id).ClubId);
        Assert.DoesNotContain(durableBeforeRetry.Transactions,t=>t.PlayerId==p.Id);
        Assert.Equal(initial.RoundVersion,durableBeforeRetry.RoundVersion);
        Assert.Equal(2,w.Transactions.Count(t=>t.PlayerId==p.Id)); // Complete in-memory receipt, never half of a transfer.
        Assert.Equal(buyerCash-100_000,w.Clubs.Single(c=>c.Id==b.CurrentClubId).CashBalance);
        Assert.Equal(sellerCash+100_000,w.Clubs.Single(c=>c.Id==s.CurrentClubId).CashBalance);
        Assert.True((await service.BuyListedPlayerAsync(w.Id,b.Id,p.Id,"same",expectedRoundVersion:version)).Success);
        var restored=JsonSerializer.Deserialize<GameWorld>(repo.DurableSnapshot)!;
        var restart=new InMemoryWorldRepository();await restart.SaveWorldAsync(restored);
        var restartedService=new ClubAndMarketApplicationService(restart,NullMatchRoomNotificationPublisher.Instance);
        Assert.True((await restartedService.BuyListedPlayerAsync(w.Id,b.Id,p.Id,"same",expectedRoundVersion:version)).Success);
        Assert.Equal(2,restored.Transactions.Count(t=>t.PlayerId==p.Id));
        Assert.Single(restored.Economy.ContractHistory,h=>h.PlayerId==p.Id);
        Assert.Equal(b.CurrentClubId,restored.Players.Single(x=>x.Id==p.Id).ClubId);
        Assert.Equal(version+1,restored.RoundVersion);
    }

    private sealed class FailsOnce(GameWorld world):IWorldRepository
    {
        public string DurableSnapshot { get; private set; }=JsonSerializer.Serialize(world);
        private int calls;
        public Task<GameWorld?> GetWorldAsync(Guid id,CancellationToken ct=default)=>Task.FromResult<GameWorld?>(world);
        public Task<GameWorld?> GetWorldByInviteCodeAsync(string code,CancellationToken ct=default)=>Task.FromResult<GameWorld?>(world);
        public Task<List<GameWorld>> ListWorldsAsync(CancellationToken ct=default)=>Task.FromResult(new List<GameWorld>{world});
        public Task SaveWorldAsync(GameWorld value,CancellationToken ct=default)
        {
            if(++calls==1)throw new IOException("Controlled market persistence failure.");
            DurableSnapshot=JsonSerializer.Serialize(value);return Task.CompletedTask;
        }
    }
}
