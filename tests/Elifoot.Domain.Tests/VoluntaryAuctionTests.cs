using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Domain;
namespace Elifoot.Domain.Tests;

public class VoluntaryAuctionTests
{
    private static (GameWorld W,Club Seller,Club Buyer,Player Player) World()
    {
        var w=WorldSeedFactory.CreateSeededWorld(totalClubs:128);w.Auctions.Clear();w.RoundNumber=7;w.RoundState=RoundState.PREPARING;
        foreach(var c in w.Clubs)c.HumanCoachId=Guid.NewGuid();
        var seller=w.Clubs[0];var buyer=w.Clubs[1];buyer.CashBalance=20_000_000;
        return(w,seller,buyer,w.Players.Last(p=>p.ClubId==seller.Id));
    }
    private static void Finished(GameWorld w,int season,int round)
    {
        w.SeasonNumber=season;w.RoundNumber=round;w.RoundState=RoundState.RESULTS;
        w.SettledFinancialRounds.Add($"round_settled:S{season}:R{round}");
    }
    [Fact]
    public void DefaultIsFiveHundredThousandForTwoGlobalRoundsAndR7EndsAfterR8()
    {
        var(w,s,b,p)=World();Assert.True(VoluntaryAuctionEngine.Create(w,s,p.Id,"default").Success);
        var a=Assert.Single(w.Auctions);Assert.Equal(500000,a.MinimumBid);Assert.Equal(2,a.DurationRounds);Assert.Equal(8,a.ExpiresAtRound);Assert.Equal(1,a.ExpiresAtSeason);
        Assert.Equal(s.Id,p.ClubId);Assert.False(VoluntaryAuctionEngine.IsDue(w,a));
        // Finishing individual fixtures and repeated market reads/processing cannot age the auction.
        foreach(var m in w.Fixtures.Where(m=>m.RoundNumber==7)){m.IsFinished=true;m.CurrentMinute=90;}
        for(int i=0;i<3;i++)MarketAndEconomyEngine.ProcessWeeklyAiMarketBatch(w);
        Assert.False(a.IsClosed);Finished(w,1,7);MarketAndEconomyEngine.ProcessWeeklyAiMarketBatch(w);Assert.False(a.IsClosed);
        // Advancing to preparation R8 is not the completion of R8.
        w.RoundNumber=8;w.RoundState=RoundState.PREPARING;MarketAndEconomyEngine.ProcessWeeklyAiMarketBatch(w);Assert.False(a.IsClosed);
        w.RoundState=RoundState.RESULTS;MarketAndEconomyEngine.SettleAuction(w,a);Assert.False(a.IsClosed);
        Finished(w,1,8);MarketAndEconomyEngine.ProcessWeeklyAiMarketBatch(w);Assert.True(a.IsClosed);Assert.Equal("AUCTION_UNSOLD",a.ClosureReason);Assert.Equal(s.Id,p.ClubId);
    }
    [Theory][InlineData(1,7)][InlineData(2,8)][InlineData(3,9)]
    public void AllAllowedDurationsEndOnlyOnTheirOfficialCompletedGlobalRound(int duration,int end)
    {
        var(w,s,b,p)=World();Assert.True(VoluntaryAuctionEngine.Create(w,s,p.Id,"duration",0,duration).Success);var a=w.Auctions.Single();
        for(int round=7;round<=end;round++){Finished(w,1,round);MarketAndEconomyEngine.SettleAuction(w,a);Assert.Equal(round==end,a.IsClosed);}
    }
    [Theory][InlineData(0)][InlineData(1)][InlineData(500000)][InlineData(10000000)]
    public void NonnegativeValidMinimumsOverrideDefault(int amount)
    {
        var(w,s,b,p)=World();Assert.True(VoluntaryAuctionEngine.Create(w,s,p.Id,"amount",amount,3).Success);Assert.Equal(amount,w.Auctions.Single().MinimumBid);
    }
    [Fact]
    public void CentsRemainExactInMinimumBidAndReconciledSale()
    {
        var(w,s,b,p)=World();Assert.True(VoluntaryAuctionEngine.Create(w,s,p.Id,"cents",123.45m,1).Success);var a=w.Auctions.Single();
        Assert.True(ClassicEconomyEngine.PlaceBid(w,b,a.Id,123.45m,"bid").Success);decimal seller=s.CashBalance,buyer=b.CashBalance;
        Finished(w,1,7);MarketAndEconomyEngine.SettleAuction(w,a);Assert.Equal(seller+123.45m,s.CashBalance);Assert.Equal(buyer-123.45m,b.CashBalance);Assert.Equal(b.Id,p.ClubId);
    }
    [Theory][InlineData(-1,2)][InlineData(1,0)][InlineData(1,4)][InlineData(1,-1)]
    public void InvalidMinimumOrDurationDoesNotCreateOrReleaseAnything(int minimum,int duration)
    {
        var(w,s,b,p)=World();Assert.False(VoluntaryAuctionEngine.Create(w,s,p.Id,"bad",minimum,duration).Success);Assert.Empty(w.Auctions);Assert.Equal(s.Id,p.ClubId);
        Assert.False(VoluntaryAuctionEngine.Create(w,s,p.Id,"fraction",.001m,2).Success);
    }
    [Fact]
    public void ResultsCreationStartsAtNextRoundAndCrossesSeasonWithoutEarlyClosure()
    {
        var(w,s,b,p)=World();Finished(w,1,7);VoluntaryAuctionEngine.Create(w,s,p.Id,"post",0,2);var a=w.Auctions.Single();Assert.Equal(9,a.ExpiresAtRound);
        Finished(w,1,8);Assert.False(VoluntaryAuctionEngine.IsDue(w,a));Finished(w,1,9);Assert.True(VoluntaryAuctionEngine.IsDue(w,a));
        var q=w.Players.First(p=>p.ClubId==s.Id&&p.Id!=a.PlayerId);w.RoundNumber=w.TotalRoundsInSeason;w.RoundState=RoundState.PREPARING;
        VoluntaryAuctionEngine.Create(w,s,q.Id,"season",0,3);var next=w.Auctions.Last();Assert.Equal(2,next.ExpiresAtSeason);Assert.Equal(2,next.ExpiresAtRound);
        Finished(w,1,w.TotalRoundsInSeason);Assert.False(VoluntaryAuctionEngine.IsDue(w,next));Finished(w,2,1);Assert.False(VoluntaryAuctionEngine.IsDue(w,next));Finished(w,2,2);Assert.True(VoluntaryAuctionEngine.IsDue(w,next));
    }
    [Theory][InlineData(0)][InlineData(500000)]
    public void WinningSaleCreditsTheSellerExactlyOnceIncludingAZeroBid(int minimum)
    {
        var(w,s,b,p)=World();var created=VoluntaryAuctionEngine.Create(w,s,p.Id,"sale",minimum,2);var a=w.Auctions.Single();decimal seller=s.CashBalance,buyer=b.CashBalance;
        Assert.True(ClassicEconomyEngine.PlaceBid(w,b,a.Id,minimum,"bid").Success);Finished(w,1,8);
        MarketAndEconomyEngine.SettleAuction(w,a);MarketAndEconomyEngine.SettleAuction(w,a);
        Assert.Equal(b.Id,p.ClubId);Assert.Equal(seller+minimum,s.CashBalance);Assert.Equal(buyer-minimum,b.CashBalance);Assert.Equal("SOLD",a.ClosureReason);
        var tx=w.Transactions.Where(t=>t.PlayerId==p.Id&&t.TransactionType=="VoluntaryAuction").ToList();Assert.Equal(2,tx.Count);Assert.Equal(0,tx.Sum(t=>t.Amount));Assert.Contains(tx,t=>t.Category==FinancialTransactionCategory.AuctionSale&&t.ClubId==s.Id);
        Assert.Single(w.Economy.AuctionHistory,h=>h.AuctionId==a.Id);
        Assert.Equal(created.AuctionId,VoluntaryAuctionEngine.Create(w,s,p.Id,"sale",minimum,2).AuctionId);
        Assert.False(VoluntaryAuctionEngine.Create(w,s,p.Id,"sale",minimum+1,2).Success);
    }
    [Fact]
    public void ReserveAndSettlementBothProtectElevenOneGoalkeeperAndTenOutfield()
    {
        var(w,s,b,p)=World();var squad=w.Players.Where(p=>p.ClubId==s.Id).ToList();var keep=squad.Where(p=>p.Position==PlayerPosition.G).Take(1).Concat(squad.Where(p=>p.Position!=PlayerPosition.G).Take(11)).ToList();
        foreach(var removed in squad.Except(keep))removed.ClubId=null;
        var onlyKeeper=keep.Single(p=>p.Position==PlayerPosition.G);Assert.False(VoluntaryAuctionEngine.Create(w,s,onlyKeeper.Id,"keeper").Success);
        var outgoing=keep.Last();Assert.True(VoluntaryAuctionEngine.Create(w,s,outgoing.Id,"last-surplus").Success);
        Assert.False(VoluntaryAuctionEngine.Create(w,s,keep[^2].Id,"over-reserve").Success);
        var a=w.Auctions.Single();Assert.True(ClassicEconomyEngine.PlaceBid(w,b,a.Id,500000,"bid").Success);
        // Another legal change leaves exactly 11 before settlement: the auction cannot remove the 11th.
        keep[^2].ClubId=null;Finished(w,1,8);decimal cash=s.CashBalance;MarketAndEconomyEngine.SettleAuction(w,a);
        Assert.Equal("AUCTION_UNSOLD",a.ClosureReason);Assert.Equal(s.Id,outgoing.ClubId);Assert.Equal(cash,s.CashBalance);Assert.True(SquadValidator.ValidateClubSquadLimits(w.Players.Where(p=>p.ClubId==s.Id).ToList()).IsValid);
    }
    [Fact]
    public void SerializationDoesNotChangeDeadlineOrCountReconnetionsAndAutomaticZeroRemainsZero()
    {
        var(w,s,b,p)=World();VoluntaryAuctionEngine.Create(w,s,p.Id,"persist");var id=w.Auctions.Single().Id;
        for(int i=0;i<4;i++)w=JsonSerializer.Deserialize<GameWorld>(JsonSerializer.Serialize(w))!;
        var a=w.Auctions.Single(x=>x.Id==id);Assert.Equal(8,a.ExpiresAtRound);Assert.False(VoluntaryAuctionEngine.IsDue(w,a));
        var debtClub=w.Clubs[2];debtClub.CashBalance=-1;ClassicEconomyEngine.EvaluateInsolvency(w);Assert.All(w.Auctions.Where(x=>x.Reason==AuctionReason.FinancialInsolvency),x=>Assert.Equal(0,x.MinimumBid));
        Assert.Equal(500000,a.MinimumBid);
    }
}
