using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Domain;

namespace Elifoot.Domain.Tests;

public class SalaryRulesCorrectionTests
{
    private static (GameWorld W,Club C,Player P) Setup(bool ai=false)
    {
        var w=WorldSeedFactory.CreateSeededWorld(totalClubs:32);w.Auctions.Clear();
        w.Transactions.Clear();w.AppliedTransactionKeys.Clear();
        var c=w.Clubs[0];if(!ai)c.HumanCoachId=Guid.NewGuid();
        var p=w.Players.Last(p=>p.ClubId==c.Id&&p.Position==PlayerPosition.A);
        p.IsListedForTransfer=false;p.ContractRoundsRemaining=0;p.SalaryPerRound=100;p.Strength=30;
        return(w,c,p);
    }
    private static SalaryRaiseRequest Request(GameWorld w,Club c,Player p,int? season=null)
    {
        var r=new SalaryRaiseRequest{Id=Guid.NewGuid(),WorldId=w.Id,ClubId=c.Id,PlayerId=p.Id,
            Season=season??w.SeasonNumber,Round=w.RoundNumber,CurrentSalary=p.SalaryPerRound,
            RequestedSalary=p.SalaryPerRound+10,RenewalMatches=20};
        w.Economy.SalaryRequests.Add(r);return r;
    }

    [Fact] public void NewWorldAndNewSeasonKeepOwnershipWagesAndHistoryWithoutActiveRenewals()
    {
        var(w,c,p)=Setup();Assert.All(w.Players,x=>Assert.False(ClassicEconomyEngine.IsSeasonRenewed(w,x)));
        var quote=ClassicEconomyEngine.PreviewRenewal(w,c,p);
        Assert.True(ClassicEconomyEngine.RenewContract(w,c,p,0,"renew",termsToken:quote.TermsToken).Success);
        var ownership=w.Players.ToDictionary(x=>x.Id,x=>x.ClubId);
        var wages=w.Players.ToDictionary(x=>x.Id,x=>x.SalaryPerRound);
        int history=w.Economy.ContractHistory.Count;
        w.RoundNumber=w.TotalRoundsInSeason;w.RoundState=RoundState.RESULTS;
        var result=CompetitionAndSeasonEngine.FinalizeSeasonAndAdvance(w,1);
        Assert.Same(result,CompetitionAndSeasonEngine.FinalizeSeasonAndAdvance(w,1));
        Assert.Equal(2,w.SeasonNumber);Assert.Equal(history,w.Economy.ContractHistory.Count);
        Assert.All(w.Players,x=>{Assert.Equal(ownership[x.Id],x.ClubId);Assert.Equal(wages[x.Id],x.SalaryPerRound);
            Assert.False(ClassicEconomyEngine.IsSeasonRenewed(w,x));Assert.Null(x.RenewedForSeason);});
        Assert.True(MarketValidationEngine.ValidateSale(w,c.Id,p.Id).Success);
    }

    [Fact] public void AbandoningVoluntaryQuoteIsReadOnlyAndNeverCreatesRequestAuctionOrSaleBlock()
    {
        var(w,c,p)=Setup();string before=JsonSerializer.Serialize(w);
        var quote=ClassicEconomyEngine.PreviewRenewal(w,c,p);
        Assert.True(quote.RequiresRaise);Assert.Equal(110,quote.RequiredSalary);
        Assert.Equal(before,JsonSerializer.Serialize(w));Assert.Equal(100,p.SalaryPerRound);
        Assert.Equal(c.Id,p.ClubId);Assert.Null(p.RenewedForSeason);
        Assert.Empty(w.Economy.SalaryRequests);Assert.Empty(w.Auctions);Assert.Empty(w.Transactions);
        Assert.True(MarketValidationEngine.ValidateSale(w,c.Id,p.Id).Success);
    }

    [Theory][InlineData(false)][InlineData(true)]
    public void DemandNeedsExplicitConfirmationAndOneFeeForHumansAndAi(bool ai)
    {
        var(w,c,p)=Setup(ai);decimal cash=c.CashBalance;
        var quote=ClassicEconomyEngine.PreviewRenewal(w,c,p);
        Assert.Contains("RENEWAL_CONFIRMATION_REQUIRED",ClassicEconomyEngine.RenewContract(w,c,p,0,"not-confirmed").Error);
        Assert.Equal(100,p.SalaryPerRound);Assert.Empty(w.Transactions);
        Assert.True(ClassicEconomyEngine.RenewContract(w,c,p,0,"yes",termsToken:quote.TermsToken).Success);
        Assert.True(ClassicEconomyEngine.RenewContract(w,c,p,0,"yes",termsToken:quote.TermsToken).Success);
        Assert.Equal(quote.RequiredSalary,p.SalaryPerRound);Assert.Equal(cash-quote.SigningFee,c.CashBalance);
        Assert.Single(w.Transactions);Assert.Single(w.Economy.ContractHistory);Assert.Empty(w.Auctions);
        Assert.True(ClassicEconomyEngine.IsSeasonRenewed(w,p));Assert.False(MarketValidationEngine.ValidateSale(w,c.Id,p.Id).Success);
    }

    [Fact] public void SameSalaryCanBeRenewedAndSeveralPlayersCanRenewInOneRound()
    {
        var(w,c,p)=Setup();var players=w.Players.Where(x=>x.ClubId==c.Id).Take(3).ToList();
        int round=w.RoundNumber;
        foreach(var x in players)
        {
            x.SalaryPerRound=100_000;x.IsListedForTransfer=false;
            var q=ClassicEconomyEngine.PreviewRenewal(w,c,x);Assert.False(q.RequiresRaise);
            Assert.True(ClassicEconomyEngine.RenewContract(w,c,x,0,"renew:"+x.Id).Success);
            Assert.Equal(100_000,x.SalaryPerRound);Assert.True(ClassicEconomyEngine.IsSeasonRenewed(w,x));
        }
        Assert.Equal(round,w.RoundNumber);Assert.Equal(3,w.Economy.ContractHistory.Count);
        Assert.Equal(3,w.Transactions.Count);Assert.Empty(w.Economy.SalaryRequests);Assert.Empty(w.Economy.SalaryRequestRounds);
    }

    [Fact] public void ChangedVoluntaryTermsRejectConfirmationWithoutConsequences()
    {
        var(w,c,p)=Setup();var quote=ClassicEconomyEngine.PreviewRenewal(w,c,p);p.SalaryPerRound=101;
        string before=JsonSerializer.Serialize(w);
        Assert.Contains("RENEWAL_TERMS_CHANGED",ClassicEconomyEngine.RenewContract(w,c,p,0,"stale",termsToken:quote.TermsToken).Error);
        Assert.Equal(before,JsonSerializer.Serialize(w));Assert.True(MarketValidationEngine.ValidateSale(w,c.Id,p.Id).Success);
    }

    [Fact] public void VoluntaryRenewalCannotResolveOrConvertAnExistingSpontaneousRequest()
    {
        var(w,c,p)=Setup();var r=Request(w,c,p);var quote=ClassicEconomyEngine.PreviewRenewal(w,c,p);
        Assert.Contains("SALARY_PENDING_DECISION",ClassicEconomyEngine.RenewContract(w,c,p,0,"not-a-response",termsToken:quote.TermsToken).Error);
        Assert.Equal(SalaryRequestStatus.PENDING,r.Status);Assert.Equal(100,p.SalaryPerRound);
        Assert.Empty(w.Auctions);Assert.Empty(w.Economy.ContractHistory);
        Assert.False(MarketValidationEngine.ValidateSale(w,c.Id,p.Id).Success);
    }

    [Theory][InlineData(true,false)][InlineData(false,false)][InlineData(true,true)][InlineData(false,true)]
    public void OldPendingRequestsAreRecoveredOnlyByAnExplicitAuthorizedDecision(bool accept,bool ai)
    {
        var(w,c,p)=Setup(ai);w.SeasonNumber=3;var r=Request(w,c,p,1);
        string before=JsonSerializer.Serialize(w);Assert.NotNull(SalarySeasonTransitionEngine.BlockingReason(w));
        Assert.Equal(before,JsonSerializer.Serialize(w));Assert.Equal(SalaryRequestStatus.PENDING,r.Status);
        string actor=ai?"AI":"HUMAN";
        Assert.True(ClassicEconomyEngine.RespondSalary(w,c,r.Id,accept,"decision",actor).Success);
        Assert.True(ClassicEconomyEngine.RespondSalary(w,c,r.Id,accept,"decision",actor).Success);
        Assert.Equal(1,r.Season);Assert.Equal(1,r.DecisionVersion);Assert.Null(SalarySeasonTransitionEngine.BlockingReason(w));
        Assert.Equal(accept?110:100,p.SalaryPerRound);Assert.Equal(accept?c.Id:(Guid?)null,p.ClubId);
        Assert.Equal(accept?0:1,w.Auctions.Count);Assert.False(ClassicEconomyEngine.IsSeasonRenewed(w,p));
    }

    [Theory][InlineData(RoundState.PREPARING,false)][InlineData(RoundState.RESULTS,false)]
    [InlineData(RoundState.RESULTS,true)][InlineData(RoundState.FINISHED,true)]
    public void SpontaneousRefusalHasExactlyOneOfficialRoundAndZeroMinimum(RoundState phase,bool lastRound)
    {
        var(w,c,p)=Setup();w.RoundState=phase;if(lastRound)w.RoundNumber=w.TotalRoundsInSeason;
        var r=Request(w,c,p);int expectedSeason=w.SeasonNumber;
        int expectedRound=w.RoundNumber+(phase==RoundState.PREPARING?0:1);
        if(expectedRound>w.TotalRoundsInSeason){expectedRound=1;expectedSeason++;}
        Assert.True(ClassicEconomyEngine.RespondSalary(w,c,r.Id,false,"no").Success);
        var a=Assert.Single(w.Auctions);Assert.Equal(1,a.DurationRounds);Assert.Equal(0,a.MinimumBid);
        Assert.Equal(expectedSeason,a.ExpiresAtSeason);Assert.Equal(expectedRound,a.ExpiresAtRound);
        Assert.Equal(100,p.SalaryPerRound);Assert.False(ClassicEconomyEngine.IsSpecialAuctionDue(w,a));
        w.SeasonNumber=expectedSeason;w.RoundNumber=expectedRound;w.RoundState=RoundState.RESULTS;
        w.SettledFinancialRounds.Add($"round_settled:S{expectedSeason}:R{expectedRound}");
        Assert.True(ClassicEconomyEngine.IsSpecialAuctionDue(w,a));
    }

    [Theory][InlineData(1)][InlineData(2)][InlineData(3)]
    public void VoluntaryAuctionsKeepTheirIndependentPriceAndDuration(int duration)
    {
        var(w,c,p)=Setup();var created=VoluntaryAuctionEngine.Create(w,c,p.Id,"voluntary",durationRounds:duration);
        Assert.True(created.Success,created.Error);var a=Assert.Single(w.Auctions);
        Assert.Equal(AuctionReason.Voluntary,a.Reason);Assert.Equal(500_000,a.MinimumBid);Assert.Equal(duration,a.DurationRounds);
        Assert.Equal(c.Id,p.ClubId);Assert.Empty(w.Economy.SalaryRequests);
        Assert.Equal(2,VoluntaryAuctionDefaults.DurationRounds);
    }

    [Fact] public void InsolvencyCannotSilentlyDecideAHumanSpontaneousRequest()
    {
        var(w,c,p)=Setup();foreach(var x in w.Players.Where(x=>x.ClubId==c.Id&&x.Id!=p.Id))x.RenewedForSeason=w.SeasonNumber;
        var r=Request(w,c,p);c.CashBalance=-1;
        ClassicEconomyEngine.EvaluateInsolvency(w);
        Assert.Equal(SalaryRequestStatus.PENDING,r.Status);Assert.Equal(c.Id,p.ClubId);Assert.Equal(100,p.SalaryPerRound);
        Assert.DoesNotContain(w.Auctions,a=>a.PlayerId==p.Id);
    }
}
