using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Domain;

namespace Elifoot.Domain.Tests;

public class MarketValidationV3Stage02ATests
{
    private static (GameWorld W, Club Buyer, Club Seller, Player Player) World(bool ai = false)
    {
        var w = new GameWorld { TotalRoundsInSeason = 10 };
        var buyer = new Club { GameWorldId = w.Id, CashBalance = 10_000_000 };
        var seller = new Club { GameWorldId = w.Id, CashBalance = 100_000 };
        w.Clubs.AddRange([buyer, seller]);
        foreach (var club in w.Clubs)
        {
            var manager = new AiManager { GameWorldId = w.Id, CurrentClubId = club.Id };
            w.AiManagers.Add(manager); club.AiManagerId = manager.Id;
            for (int i = 0; i < 14; i++) w.Players.Add(new Player {
                GameWorldId = w.Id, ClubId = club.Id, Name = "Player " + i,
                Position = i < 2 ? PlayerPosition.G : PlayerPosition.A,
                Strength = 30, ContractRoundsRemaining = 10, SalaryPerRound = 1_000 });
        }
        if (!ai)
        {
            var coach = new HumanCoach { GameWorldId = w.Id, CurrentClubId = buyer.Id };
            buyer.HumanCoachId = coach.Id; buyer.AiManagerId = null; w.HumanCoaches.Add(coach);
        }
        return (w, buyer, seller, w.Players.Last());
    }

    private static void Resize(GameWorld w, Club club, int size)
    {
        while (w.Players.Count(p => p.ClubId == club.Id) > size)
            w.Players.Remove(w.Players.Last(p => p.ClubId == club.Id));
        while (w.Players.Count(p => p.ClubId == club.Id) < size)
            w.Players.Add(new Player { GameWorldId = w.Id, ClubId = club.Id, Position = PlayerPosition.D,
                ContractRoundsRemaining = 10, Strength = 20 });
    }

    private static AuctionItem FreeAuction(GameWorld w, Club? bidder = null, int end = 2)
    {
        var p = new Player { GameWorldId = w.Id, ClubId = null, Position = PlayerPosition.A, Strength = 40 };
        w.Players.Add(p);
        var a = new AuctionItem { GameWorldId = w.Id, PlayerId = p.Id, Reason = AuctionReason.ContractExpiration,
            CreatedAtSeason = w.SeasonNumber, CreatedAtRound = w.RoundNumber, ExpiresAtRound = end,
            MinimumBid = 100_000, HighestBidderClubId = bidder?.Id, CurrentBid = bidder == null ? 0 : 100_000 };
        w.Auctions.Add(a); return a;
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void PurchaseReconcilesOwnershipAndMoneyOnceForHumanAndAi(bool ai)
    {
        var (w, b, s, p) = World(ai); decimal money = b.CashBalance + s.CashBalance;
        Assert.True(MarketAndEconomyEngine.ExecuteTransfer(w,b.Id,s.Id,p.Id,123456.78m,"valid","purchase").Success);
        Assert.Equal(b.Id,p.ClubId); Assert.Equal(money,b.CashBalance+s.CashBalance);
        Assert.Equal(2,w.Transactions.Count); Assert.Equal(0,w.Transactions.Sum(t=>t.Amount));
        Assert.False(MarketAndEconomyEngine.ExecuteTransfer(w,b.Id,s.Id,p.Id,123456.78m,"duplicate","purchase").Success);
        Assert.Equal(2,w.Transactions.Count); Assert.Single(w.Economy.ContractHistory);
    }

    [Theory]
    [InlineData(false, "funds")] [InlineData(true, "funds")]
    [InlineData(false, "full")] [InlineData(true, "full")]
    [InlineData(false, "reserved")] [InlineData(true, "reserved")]
    [InlineData(false, "renewed")] [InlineData(true, "renewed")]
    [InlineData(false, "auction")] [InlineData(true, "auction")]
    [InlineData(false, "expired")] [InlineData(true, "expired")]
    [InlineData(false, "minimum")] [InlineData(true, "minimum")]
    public void PurchaseConstraintsAreIdenticalAndRejectionHasNoFinancialEffect(bool ai, string restriction)
    {
        var (w,b,s,p)=World(ai);
        switch(restriction)
        {
            case "funds": b.CashBalance=1; break;
            case "full": Resize(w,b,24); break;
            case "reserved": Resize(w,b,23); FreeAuction(w,b); break;
            case "renewed": p.RenewedForSeason=w.SeasonNumber; break;
            case "auction": w.Auctions.Add(new AuctionItem { PlayerId=p.Id, FormerClubId=s.Id, Reason=AuctionReason.Voluntary }); break;
            case "expired": w.Economy.Rules.SeasonContractsEnabled=false; p.ContractRoundsRemaining=0; break;
            case "minimum":
                foreach(var extra in w.Players.Where(x=>x.ClubId==s.Id&&x.Id!=p.Id).Skip(10).ToList()) w.Players.Remove(extra);
                break;
        }
        decimal buyerCash=b.CashBalance,sellerCash=s.CashBalance; var owner=p.ClubId;
        var result=MarketAndEconomyEngine.ExecuteTransfer(w,b.Id,s.Id,p.Id,100_000,"invalid","reject");
        Assert.False(result.Success); Assert.NotEmpty(result.ErrorMessage!);
        Assert.Equal(buyerCash,b.CashBalance); Assert.Equal(sellerCash,s.CashBalance); Assert.Equal(owner,p.ClubId);
        Assert.Empty(w.Transactions); Assert.Empty(w.Economy.ContractHistory);
    }

    [Fact]
    public void ExistingAuctionReservationCannotBeSpentByAnUnrelatedPurchase()
    {
        var(w,b,s,p)=World(); b.CashBalance=150_000; FreeAuction(w,b);
        Assert.False(MarketAndEconomyEngine.ExecuteTransfer(w,b.Id,s.Id,p.Id,100_000,"reserved cash","buy").Success);
        Assert.Empty(w.Transactions);
    }

    [Fact]
    public void OtherReservedDeparturesMustStillLeaveALegalSellerSquad()
    {
        var(w,b,s,p)=World(); Resize(w,s,12); p=w.Players.Last(x=>x.ClubId==s.Id);
        w.Players.First(x=>x.ClubId==s.Id&&x.Position==PlayerPosition.G).Position=PlayerPosition.D;
        var another=w.Players.First(x=>x.ClubId==s.Id&&x.Position==PlayerPosition.A&&x.Id!=p.Id);
        Assert.True(VoluntaryAuctionEngine.Create(w,s,another.Id,"reserve",0,2).Success);
        Assert.False(VoluntaryAuctionEngine.Create(w,s,p.Id,"too-many-departures",0,2).Success);
        var auction=Assert.Single(w.Auctions);
        Assert.True(ClassicEconomyEngine.PlaceBid(w,b,auction.Id,0,"bid").Success);
        Assert.True(MarketAndEconomyEngine.ExecuteTransfer(w,b.Id,s.Id,p.Id,100_000,"registered squad stays legal","buy").Success);
        w.RoundNumber=auction.ExpiresAtRound;w.RoundState=RoundState.RESULTS;
        w.SettledFinancialRounds.Add($"round_settled:S{w.SeasonNumber}:R{w.RoundNumber}");
        MarketAndEconomyEngine.SettleAuction(w,auction);
        Assert.True(auction.IsClosed);Assert.Equal("AUCTION_UNSOLD",auction.ClosureReason);
        Assert.Equal(s.Id,another.ClubId);Assert.Equal(11,w.Players.Count(x=>x.ClubId==s.Id));
        Assert.Equal(2,w.Transactions.Count);Assert.Equal(0,w.Transactions.Sum(t=>t.Amount));
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public void BidsShareCapacityFundsMinimumDeadlineAndPlayerChecks(bool ai)
    {
        var(w,b,_,_)=World(ai); var a=FreeAuction(w);
        Assert.False(ClassicEconomyEngine.PlaceBid(w,b,a.Id,99_999,"low").Success);
        b.CashBalance=1; Assert.False(ClassicEconomyEngine.PlaceBid(w,b,a.Id,100_000,"funds").Success);
        b.CashBalance=10_000_000; Resize(w,b,24);
        Assert.False(ClassicEconomyEngine.PlaceBid(w,b,a.Id,100_000,"full").Success);
        Resize(w,b,23); var first=FreeAuction(w,b);
        Assert.False(ClassicEconomyEngine.PlaceBid(w,b,a.Id,100_000,"reserved").Success);
        first.IsClosed=true;
        Assert.True(ClassicEconomyEngine.PlaceBid(w,b,a.Id,100_000,"valid").Success);
        Assert.True(ClassicEconomyEngine.PlaceBid(w,b,a.Id,100_000,"valid").Success);
        Assert.Equal(b.Id,a.HighestBidderClubId); Assert.Empty(w.Transactions);
        var second=FreeAuction(w); w.RoundNumber=3;
        Assert.False(ClassicEconomyEngine.PlaceBid(w,b,second.Id,100_000,"expired").Success);
        a.IsClosed=true; Assert.False(ClassicEconomyEngine.PlaceBid(w,b,a.Id,105_000,"closed").Success);
        w.RoundNumber=1; w.Players.RemoveAll(p=>p.Id==second.PlayerId);
        Assert.False(ClassicEconomyEngine.PlaceBid(w,b,second.Id,100_000,"missing").Success);
    }

    [Fact]
    public void SettlementWaitsForDeadlineAndExcludesItsOwnReservationAfterReload()
    {
        var(w,b,_,_)=World(); Resize(w,b,23); var a=FreeAuction(w);
        Assert.True(ClassicEconomyEngine.PlaceBid(w,b,a.Id,100_000,"bid").Success);
        MarketAndEconomyEngine.SettleAuction(w,a); Assert.False(a.IsClosed); Assert.Empty(w.Transactions);
        var restored=JsonSerializer.Deserialize<GameWorld>(JsonSerializer.Serialize(w))!;
        var restoredAuction=restored.Auctions.Single(); restored.RoundNumber=2;
        MarketAndEconomyEngine.SettleAuction(restored,restoredAuction);
        MarketAndEconomyEngine.SettleAuction(restored,restoredAuction);
        Assert.True(restoredAuction.IsClosed); Assert.Equal(24,restored.Players.Count(p=>p.ClubId==b.Id));
        Assert.Single(restored.Transactions); Assert.Equal(9_900_000,restored.Clubs.Single(c=>c.Id==b.Id).CashBalance);
        Assert.Equal(14,w.Players.Count(p=>p.ClubId==w.Clubs[1].Id));
    }

    [Fact]
    public void TransferFailureAfterPostingBothLegsRollsBackAndCanRetryTheSameKey()
    {
        var(w,b,s,p)=World(true); decimal buyerCash=b.CashBalance,sellerCash=s.CashBalance;
        s.PreferredStarterIds.Add(p.Id); p.IsListedForTransfer=true; p.AskingPrice=100_000;
        b.LatestSquadPlan=new ManagerSquadPlan { TargetStarters=null! }; // Corrupt legacy planning metadata fails during transfer stamping.
        var failed=MarketAndEconomyEngine.ExecuteTransfer(w,b.Id,s.Id,p.Id,100_000,"fault","same-key");
        Assert.False(failed.Success); Assert.StartsWith("MARKET_TRANSFER_FAILED",failed.ErrorMessage);
        Assert.Equal(buyerCash,b.CashBalance); Assert.Equal(sellerCash,s.CashBalance); Assert.Equal(s.Id,p.ClubId);
        Assert.Empty(w.Transactions); Assert.Empty(w.Economy.ContractHistory);
        Assert.Contains(p.Id,s.PreferredStarterIds); Assert.True(p.IsListedForTransfer); Assert.Equal(100_000,p.AskingPrice);
        b.LatestSquadPlan=null;
        Assert.True(MarketAndEconomyEngine.ExecuteTransfer(w,b.Id,s.Id,p.Id,100_000,"retry","same-key").Success);
        Assert.Equal(2,w.Transactions.Count); Assert.Single(w.Economy.ContractHistory);
    }

    [Fact]
    public void InvalidIdentitySelfPurchasePrecisionAndFinancialOverflowNeverMutateMoney()
    {
        var(w,b,s,p)=World();
        Assert.False(MarketAndEconomyEngine.ExecuteTransfer(w,b.Id,b.Id,p.Id,1,"self","self").Success);
        Assert.False(MarketAndEconomyEngine.ExecuteTransfer(w,Guid.NewGuid(),s.Id,p.Id,1,"missing","club").Success);
        Assert.False(MarketAndEconomyEngine.ExecuteTransfer(w,b.Id,s.Id,Guid.NewGuid(),1,"missing","player").Success);
        Assert.False(MarketAndEconomyEngine.ExecuteTransfer(w,b.Id,s.Id,p.Id,1.001m,"precision","precision").Success);
        s.CashBalance=decimal.MaxValue;
        Assert.False(MarketAndEconomyEngine.ExecuteTransfer(w,b.Id,s.Id,p.Id,100_000,"overflow","overflow").Success);
        Assert.Empty(w.Transactions); Assert.Equal(s.Id,p.ClubId); Assert.Equal(10_000_000,b.CashBalance);
    }

    [Fact]
    public void TemporaryInjuryAndSuspensionDoNotCreateANewSaleBan()
    {
        var(w,b,s,p)=World(); p.InjuryRoundsRemaining=2; p.SuspensionRoundsRemaining=2;
        Assert.True(MarketAndEconomyEngine.ExecuteTransfer(w,b.Id,s.Id,p.Id,100_000,"eligible for sale","injured").Success);
        Assert.Equal(2,p.InjuryRoundsRemaining); Assert.Equal(2,p.SuspensionRoundsRemaining);
    }

    [Fact]
    public void RealAiBatchCannotCommitTwoAuctionVacanciesOrActForAnotherCoach()
    {
        var w=WorldSeedFactory.CreateSeededWorld(totalClubs:64); w.Auctions.Clear();
        var b=w.Clubs[0];
        foreach(var c in w.Clubs.Where(c=>c.Id!=b.Id)) c.HumanCoachId=Guid.NewGuid();
        foreach(var p in w.Players) { p.Strength=1; p.RenewedForSeason=w.SeasonNumber; }
        Resize(w,b,23); b.CashBalance=100_000_000;
        var first=FreeAuction(w); var second=FreeAuction(w);
        foreach(var a in w.Auctions) w.Players.Single(p=>p.Id==a.PlayerId).Strength=50;
        MarketAndEconomyEngine.ProcessWeeklyAiMarketBatch(w);
        Assert.Single(w.Auctions,a=>a.HighestBidderClubId==b.Id);
        Assert.Single(w.AuditLogs,a=>a.ActionType=="AiAuctionBid");
        var remaining=w.Auctions.Single(a=>a.HighestBidderClubId==null);
        var manager=w.AiManagers.Single(m=>m.Id==b.AiManagerId); manager.CurrentClubId=null;
        MarketAndEconomyEngine.ProcessWeeklyAiMarketBatch(w);
        Assert.Null(remaining.HighestBidderClubId);
    }
}
