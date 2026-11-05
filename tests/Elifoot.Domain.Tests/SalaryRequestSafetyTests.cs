using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Domain;
namespace Elifoot.Domain.Tests;

public class SalaryRequestSafetyTests
{
    private static (GameWorld w,Club c,Player p,MatchFixture m) Setup()
    {
        var w=WorldSeedFactory.CreateSeededWorld();w.Auctions.Clear();
        var c=w.Clubs[0];c.HumanCoachId=Guid.NewGuid();
        foreach(var x in w.Players.Where(p=>p.ClubId==c.Id)) { x.ContractRoundsRemaining=5;x.IsListedForTransfer=false; }
        var p=w.Players.Last(p=>p.ClubId==c.Id&&p.Position==PlayerPosition.A);
        p.ContractRoundsRemaining=0;p.SalaryPerRound=100;p.Strength=30;
        w.Economy.SalaryEligibility[p.Id]=new("test","FORCE_PROGRESS",29,30,c.Id,w.SeasonNumber);
        var m=new MatchFixture{HomeClubId=c.Id,AwayClubId=w.Clubs[1].Id,SeasonNumber=1,RoundNumber=1,IsFinished=true,CurrentMinute=90};
        return(w,c,p,m);
    }
    [Fact] public void OnlyUncontractedUnrenewedPlayersCanAskAndNeverDuringUnfinishedMatches()
    {
        var(w,c,p,m)=Setup();p.ContractRoundsRemaining=1;
        ClassicEconomyEngine.GenerateSalaryRequests(w,[m]);Assert.Empty(w.Economy.SalaryRequests);
        w.RoundNumber++;p.ContractRoundsRemaining=0;p.RenewedForSeason=w.SeasonNumber;
        ClassicEconomyEngine.GenerateSalaryRequests(w,[m]);Assert.Empty(w.Economy.SalaryRequests);
        w.SeasonNumber++;m.IsFinished=false;
        ClassicEconomyEngine.GenerateSalaryRequests(w,[m]);Assert.Empty(w.Economy.SalaryRequests);
        m.IsFinished=true;ClassicEconomyEngine.GenerateSalaryRequests(w,[m]);Assert.Single(w.Economy.SalaryRequests);
    }
    [Fact] public void AcceptIsExactSeasonalPersistentAndDoesNotCreateSecondEventOrExtendArbitraryTerm()
    {
        var(w,c,p,m)=Setup();ClassicEconomyEngine.GenerateSalaryRequests(w,[m]);
        var r=Assert.Single(w.Economy.SalaryRequests);decimal requested=r.RequestedSalary;
        Assert.Equal(100,p.SalaryPerRound);
        var restored=JsonSerializer.Deserialize<GameWorld>(JsonSerializer.Serialize(w))!;
        var club=restored.Clubs.Single(x=>x.Id==c.Id);var player=restored.Players.Single(x=>x.Id==p.Id);
        Assert.True(ClassicEconomyEngine.RespondSalary(restored,club,r.Id,true,"accept").Success);
        Assert.True(ClassicEconomyEngine.RespondSalary(restored,club,r.Id,true,"second-tab").Success);
        Assert.False(ClassicEconomyEngine.RespondSalary(restored,club,r.Id,false,"opposite").Success);
        Assert.Equal(requested,player.SalaryPerRound);Assert.Equal(restored.SeasonNumber,player.RenewedForSeason);
        Assert.Equal(ClassicEconomyEngine.SeasonContractRounds(restored),player.ContractRoundsRemaining);
        ClassicEconomyEngine.GenerateSalaryRequests(restored,[m]);Assert.Single(restored.Economy.SalaryRequests);
        Assert.Single(restored.Economy.ContractHistory);Assert.Equal(1,restored.Economy.SalaryRequests[0].DecisionVersion);
    }
    [Fact] public void RefusalUsesExistingFreeAgentAuctionPriceIsIdempotentAndWaitsUntilItsOfficialEnd()
    {
        var(w,c,p,m)=Setup();ClassicEconomyEngine.GenerateSalaryRequests(w,[m]);var r=Assert.Single(w.Economy.SalaryRequests);
        Assert.True(ClassicEconomyEngine.RespondSalary(w,c,r.Id,false,"no").Success);
        Assert.True(ClassicEconomyEngine.RespondSalary(w,c,r.Id,false,"other-tab").Success);
        var a=Assert.Single(w.Auctions);Assert.Equal(r.AuctionId,a.Id);Assert.InRange(a.DurationRounds!.Value,1,3);
        Assert.Equal(0,a.MinimumBid);Assert.Equal(AuctionReason.SalaryRenewalRefused,a.Reason);
        var buyer=w.Clubs[1];Assert.True(ClassicEconomyEngine.PlaceBid(w,buyer,a.Id,0,"bid").Success);
        MarketAndEconomyEngine.SettleAuction(w,a);Assert.False(a.IsClosed);Assert.Null(p.ClubId);
        w.RoundState=RoundState.RESULTS;w.SeasonNumber=a.ExpiresAtSeason!.Value;w.RoundNumber=a.ExpiresAtRound;
        w.SettledFinancialRounds.Add($"round_settled:S{w.SeasonNumber}:R{w.RoundNumber}");
        MarketAndEconomyEngine.SettleAuction(w,a);Assert.Equal(buyer.Id,p.ClubId);Assert.True(a.IsClosed);
    }
    [Fact] public void PendingAndInvalidRefusalRemainUnresolvedRatherThanSilentlyAccepting()
    {
        var(w,c,p,m)=Setup();ClassicEconomyEngine.GenerateSalaryRequests(w,[m]);var r=Assert.Single(w.Economy.SalaryRequests);
        w.RoundNumber++;ClassicEconomyEngine.GenerateSalaryRequests(w,[m]);Assert.Single(w.Economy.SalaryRequests);
        foreach(var x in w.Players.Where(x=>x.ClubId==c.Id&&x.Id!=p.Id).Skip(10))x.ClubId=null;
        Assert.False(ClassicEconomyEngine.RespondSalary(w,c,r.Id,false,"invalid").Success);
        Assert.Equal(SalaryRequestStatus.PENDING,r.Status);Assert.Empty(w.Auctions);Assert.Equal(c.Id,p.ClubId);
    }
}
