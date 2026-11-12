using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Domain;

namespace Elifoot.Domain.Tests;

public class SalaryContractsV3Stage02BTests
{
    private static (GameWorld W,Club C,Player P,SalaryRaiseRequest R) Setup(bool ai=false)
    {
        var w=WorldSeedFactory.CreateSeededWorld(totalClubs:32);w.Auctions.Clear();w.Transactions.Clear();w.AppliedTransactionKeys.Clear();
        var c=w.Clubs[0];if(!ai)c.HumanCoachId=Guid.NewGuid();
        foreach(var p in w.Players.Where(p=>p.ClubId==c.Id)){p.IsListedForTransfer=false;p.ContractRoundsRemaining=0;}
        var player=w.Players.Last(p=>p.ClubId==c.Id&&p.Position==PlayerPosition.A);
        player.SalaryPerRound=100;player.Strength=30;
        var r=new SalaryRaiseRequest{Id=Guid.NewGuid(),WorldId=w.Id,ClubId=c.Id,PlayerId=player.Id,
            Season=w.SeasonNumber,Round=w.RoundNumber,CurrentSalary=100,RequestedSalary=110,RenewalMatches=20};
        w.Economy.SalaryRequests.Add(r);return(w,c,player,r);
    }

    [Theory][InlineData(false)][InlineData(true)]
    public void AcceptIsExactAndReplaysNeverChargeOrDecideTwice(bool ai)
    {
        var(w,c,p,r)=Setup(ai);decimal cash=c.CashBalance;
        string actor=ai?"AI":"HUMAN";
        Assert.True(ClassicEconomyEngine.RespondSalary(w,c,r.Id,true,"accept",actor).Success);
        Assert.True(ClassicEconomyEngine.RespondSalary(w,c,r.Id,true,"accept",actor).Success);
        Assert.True(ClassicEconomyEngine.RespondSalary(w,c,r.Id,true,"other-tab",actor).Success);
        Assert.False(ClassicEconomyEngine.RespondSalary(w,c,r.Id,false,"opposite",actor).Success);
        Assert.Equal(110,p.SalaryPerRound);Assert.Equal(cash,c.CashBalance);Assert.Empty(w.Transactions);
        Assert.Equal(1,r.DecisionVersion);Assert.Single(w.Economy.ContractHistory);
        var restored=JsonSerializer.Deserialize<GameWorld>(JsonSerializer.Serialize(w))!;
        Assert.Equal(110,restored.Players.Single(x=>x.Id==p.Id).SalaryPerRound);
        Assert.Equal(SalaryRequestStatus.ACCEPTED,restored.Economy.SalaryRequests.Single().Status);
        Assert.Null(p.RenewedForSeason);Assert.Equal(0,p.ContractRoundsRemaining);
    }

    [Theory][InlineData(-1)][InlineData(0)][InlineData(99)][InlineData(100)][InlineData(100.001)]
    public void InvalidSalaryNeverChangesOwnershipBalanceOrDecision(double amount)
    {
        var(w,c,p,r)=Setup();r.RequestedSalary=(decimal)amount;decimal cash=c.CashBalance;
        var result=ClassicEconomyEngine.RespondSalary(w,c,r.Id,true,"invalid");
        Assert.False(result.Success);Assert.Contains("SALARY_VALUE_INVALID",result.Error);
        Assert.Equal(100,p.SalaryPerRound);Assert.Equal(cash,c.CashBalance);Assert.Equal(c.Id,p.ClubId);
        Assert.Equal(SalaryRequestStatus.PENDING,r.Status);Assert.Equal(0,r.DecisionVersion);
        Assert.Empty(w.Economy.ContractHistory);Assert.Empty(w.Transactions);
    }

    [Fact] public void OverflowAndChangedCurrentSalaryAreRejectedBeforeMutation()
    {
        var(w,c,p,r)=Setup();r.RequestedSalary=decimal.MaxValue;
        Assert.False(ClassicEconomyEngine.RespondSalary(w,c,r.Id,true,"overflow").Success);
        r.RequestedSalary=110;p.SalaryPerRound=101;
        Assert.Contains("SALARY_TERMS_CHANGED",ClassicEconomyEngine.RespondSalary(w,c,r.Id,true,"changed").Error);
        Assert.Equal(SalaryRequestStatus.PENDING,r.Status);Assert.Empty(w.Economy.ContractHistory);
    }

    [Fact] public void MissingPlayerWrongWorldAndFakeAuthorityCannotResolveRequest()
    {
        var(w,c,p,r)=Setup();
        Assert.False(ClassicEconomyEngine.RespondSalary(w,new Club{Id=c.Id},r.Id,true,"fake").Success);
        Assert.False(ClassicEconomyEngine.RespondSalary(w,c,r.Id,true,"fake-ai","AI").Success);
        r.WorldId=Guid.NewGuid();Assert.False(ClassicEconomyEngine.RespondSalary(w,c,r.Id,true,"world").Success);
        r.WorldId=w.Id;w.Players.Remove(p);Assert.False(ClassicEconomyEngine.RespondSalary(w,c,r.Id,true,"missing").Success);
        Assert.Equal(SalaryRequestStatus.PENDING,r.Status);Assert.Empty(w.Auctions);
    }

    [Theory][InlineData(false)][InlineData(true)]
    public void RefusalKeepsApprovedFreeAgentAuctionAndOneConsequence(bool ai)
    {
        var(w,c,p,r)=Setup(ai);decimal cash=c.CashBalance;string actor=ai?"AI":"HUMAN";
        Assert.True(ClassicEconomyEngine.RespondSalary(w,c,r.Id,false,"refuse",actor).Success);
        Assert.True(ClassicEconomyEngine.RespondSalary(w,c,r.Id,false,"retry",actor).Success);
        Assert.False(ClassicEconomyEngine.RespondSalary(w,c,r.Id,true,"contradiction",actor).Success);
        var auction=Assert.Single(w.Auctions);Assert.Equal(auction.Id,r.AuctionId);Assert.Null(p.ClubId);
        Assert.Equal(AuctionReason.SalaryRenewalRefused,auction.Reason);Assert.Equal(0,auction.MinimumBid);
        Assert.Equal(1,auction.DurationRounds);Assert.Equal(cash,c.CashBalance);
        Assert.Empty(w.Transactions);Assert.Equal(1,r.DecisionVersion);
    }

    [Fact] public void AuctionCreationConflictRollsBackAndSameKeyCanRecover()
    {
        var(w,c,p,r)=Setup();var conflict=new AuctionItem{
            Id=CoachCareerEngine.StableId($"auction:{w.Id}:salary:{r.Id}:refused"),PlayerId=Guid.NewGuid(),
            FormerClubId=c.Id,Reason=AuctionReason.Voluntary};w.Auctions.Add(conflict);
        var result=ClassicEconomyEngine.RespondSalary(w,c,r.Id,false,"retry");
        Assert.False(result.Success);Assert.Contains("SALARY_OPERATION_FAILED",result.Error);
        Assert.Equal(c.Id,p.ClubId);Assert.Equal(SalaryRequestStatus.PENDING,r.Status);
        Assert.Null(r.ResolvedAtUtc);Assert.Null(r.AuctionId);Assert.Equal(0,r.DecisionVersion);
        Assert.Single(w.Auctions);Assert.Empty(w.Economy.Commands);
        w.Auctions.Remove(conflict);
        Assert.True(ClassicEconomyEngine.RespondSalary(w,c,r.Id,false,"retry").Success);
        Assert.Single(w.Auctions);Assert.Equal(1,r.DecisionVersion);
    }

    [Fact] public void ExistingPlayerAuctionNeverCreatesAnotherOrLosesThePlayer()
    {
        var(w,c,p,r)=Setup();w.Auctions.Add(new AuctionItem{PlayerId=p.Id,FormerClubId=c.Id,Reason=AuctionReason.Voluntary});
        Assert.False(ClassicEconomyEngine.RespondSalary(w,c,r.Id,false,"refuse").Success);
        Assert.Equal(c.Id,p.ClubId);Assert.Single(w.Auctions);Assert.Equal(SalaryRequestStatus.PENDING,r.Status);
    }

    [Fact] public void RoundLimitIsPerClubAndPendingRequestBlocksAnotherRound()
    {
        var(w,c,p,r)=Setup();w.Economy.SalaryRequests.Clear();
        var other=w.Clubs[1];other.HumanCoachId=Guid.NewGuid();
        foreach(var club in new[]{c,other})foreach(var x in w.Players.Where(x=>x.ClubId==club.Id))
        {x.ContractRoundsRemaining=0;x.IsListedForTransfer=false;x.SalaryPerRound=10;x.NegotiatedContractProtected=false;
         w.Economy.SalaryEligibility[x.Id]=new("force:"+x.Id,"FORCE_PROGRESS",10,20,club.Id,1);}
        var match=new MatchFixture{HomeClubId=c.Id,AwayClubId=other.Id,IsFinished=true,CurrentMinute=90};
        ClassicEconomyEngine.GenerateSalaryRequests(w,[match]);ClassicEconomyEngine.GenerateSalaryRequests(w,[match]);
        Assert.Equal(2,w.Economy.SalaryRequests.Count);Assert.All(w.Economy.SalaryRequests.GroupBy(x=>x.ClubId),g=>Assert.Single(g));
        var first = w.Economy.SalaryRequests.Single(x => x.ClubId == c.Id);
        var stillPending = w.Economy.SalaryRequests.Single(x => x.ClubId == other.Id);
        Assert.True(ClassicEconomyEngine.RespondSalary(w, c, first.Id, true, "first").Success);
        w.Economy.SalaryRequestCheckedRounds.Clear();
        w.Economy.SalaryRequestRounds.Clear();
        ClassicEconomyEngine.GenerateSalaryRequests(w, [match]);
        Assert.Equal(2, w.Economy.SalaryRequests.Count);

        w.RoundNumber++;
        ClassicEconomyEngine.GenerateSalaryRequests(w, [match]);
        var newRequest = Assert.Single(w.Economy.SalaryRequests, x => x.Round == w.RoundNumber);
        Assert.Equal(c.Id, newRequest.ClubId);
        Assert.Equal(SalaryRequestStatus.PENDING, stillPending.Status);
        Assert.All(w.Economy.SalaryRequests.Where(x => x.Status == SalaryRequestStatus.PENDING).GroupBy(x => x.ClubId),
            requests => Assert.Single(requests));
    }

    [Theory][InlineData(false)][InlineData(true)]
    public void RenewedPlayerCannotLeaveUntilNextSeasonAndFeeIsOnce(bool ai)
    {
        var(w,c,p,r)=Setup(ai);w.Economy.SalaryRequests.Clear();decimal cash=c.CashBalance;
        var quote=ClassicEconomyEngine.PreviewRenewal(w,c,p);
        var renewal=ClassicEconomyEngine.RenewContract(w,c,p,200,"renew",termsToken:quote.TermsToken);Assert.True(renewal.Success,renewal.Error);
        Assert.True(ClassicEconomyEngine.RenewContract(w,c,p,200,"renew",termsToken:quote.TermsToken).Success);
        Assert.False(ClassicEconomyEngine.RenewContract(w,c,p,200,"again").Success);
        Assert.Equal(cash-ClassicEconomyEngine.SeasonRenewalFee(w,p),c.CashBalance);
        Assert.Single(w.Transactions);Assert.Single(w.Economy.ContractHistory);
        Assert.False(MarketValidationEngine.ValidateSale(w,c.Id,p.Id).Success);
        Assert.False(VoluntaryAuctionEngine.Create(w,c,p.Id,"auction").Success);
        var restored=JsonSerializer.Deserialize<GameWorld>(JsonSerializer.Serialize(w))!;
        var rp=restored.Players.Single(x=>x.Id==p.Id);Assert.True(ClassicEconomyEngine.IsSeasonRenewed(restored,rp));
        restored.SeasonNumber++;rp.ContractRoundsRemaining=0;
        Assert.True(MarketValidationEngine.ValidateSale(restored,c.Id,p.Id).Success);
        Assert.Equal(quote.RequiredSalary,rp.SalaryPerRound);Assert.Equal(c.Id,rp.ClubId);
    }

    [Fact] public void RenewalWithInvalidMoneyOrIncompleteFeeCannotApplyContract()
    {
        var(w,c,p,r)=Setup();w.Economy.SalaryRequests.Clear();
        Assert.False(ClassicEconomyEngine.RenewContract(w,c,p,1,"negative",-1).Success);
        Assert.False(ClassicEconomyEngine.RenewContract(w,c,p,1,"precision",1.001m).Success);
        Assert.False(ClassicEconomyEngine.RenewContract(w,c,p,1,"overflow",decimal.MaxValue).Success);
        MarketAndEconomyEngine.TryRecordTransaction(w,c,FinancialTransactionCategory.ContractRenewal,-1,"interrupted fee",$"contract:{p.Id}:retry:fee");
        MarketAndEconomyEngine.TryRecordTransaction(w,c,FinancialTransactionCategory.InitialBalance,0,"other existing key","other-key");
        decimal before=c.CashBalance;
        var quote=ClassicEconomyEngine.PreviewRenewal(w,c,p);
        Assert.False(ClassicEconomyEngine.RenewContract(w,c,p,1,"retry",termsToken:quote.TermsToken).Success);
        Assert.Contains($"{c.Id}:contract:{p.Id}:retry:fee",w.AppliedTransactionKeys);
        Assert.False(ClassicEconomyEngine.RenewContract(w,c,p,1,"retry",termsToken:quote.TermsToken).Success);
        Assert.Single(w.Transactions,t=>t.IdempotencyKey==$"contract:{p.Id}:retry:fee");
        Assert.Equal(before,c.CashBalance);Assert.Null(p.RenewedForSeason);Assert.Equal(100,p.SalaryPerRound);
        Assert.Empty(w.Economy.ContractHistory);Assert.Equal(SalaryRequestStatus.PENDING,r.Status);
    }

    [Fact] public void SeasonPreflightPreservesPendingStateThenResolutionAllowsOneTransition()
    {
        var(w,c,p,r)=Setup();w.RoundNumber=w.TotalRoundsInSeason;w.RoundState=RoundState.RESULTS;
        string before=JsonSerializer.Serialize(w);
        Assert.Throws<SalarySeasonTransitionException>(()=>CompetitionAndSeasonEngine.FinalizeSeasonAndAdvance(w,1));
        Assert.Equal(before,JsonSerializer.Serialize(w));
        Assert.True(ClassicEconomyEngine.RespondSalary(w,c,r.Id,true,"resolve").Success);
        var history=CompetitionAndSeasonEngine.FinalizeSeasonAndAdvance(w,1);
        Assert.Same(history,CompetitionAndSeasonEngine.FinalizeSeasonAndAdvance(w,1));Assert.Equal(2,w.SeasonNumber);
        Assert.Single(w.SeasonHistories);Assert.Equal(SalaryRequestStatus.ACCEPTED,r.Status);
        Assert.False(ClassicEconomyEngine.IsSeasonRenewed(w,p));Assert.Equal(110,p.SalaryPerRound);
        Assert.True(MarketValidationEngine.ValidateSale(w,c.Id,p.Id).Success);
    }

    [Fact] public void AlreadyCarriedLegacyRequestIsPreservedUntilExplicitDecision()
    {
        var(w,c,p,r)=Setup();w.SeasonNumber=2;r.Season=1;
        Assert.Contains("SALARY_PENDING_TRANSITION",SalarySeasonTransitionEngine.BlockingReason(w));
        Assert.Equal(SalaryRequestStatus.PENDING,r.Status);Assert.Equal(100,p.SalaryPerRound);
        Assert.Equal(c.Id,p.ClubId);Assert.Empty(w.Auctions);
        Assert.True(ClassicEconomyEngine.RespondSalary(w,c,r.Id,true,"explicit-human-decision").Success);
        Assert.Equal(SalaryRequestStatus.ACCEPTED,r.Status);Assert.Equal(110,p.SalaryPerRound);
        Assert.Null(SalarySeasonTransitionEngine.BlockingReason(w));Assert.Equal(1,r.Season);
    }

    [Fact] public void LegacyRequestFromRenewedPlayerLeavesTransitionWithoutFinancialEffects()
    {
        var (world, club, player, request) = Setup();
        player.RenewedForSeason = world.SeasonNumber;

        Assert.True(ClassicEconomyEngine.ReconcileSalaryRequests(world));
        Assert.False(ClassicEconomyEngine.ReconcileSalaryRequests(world));
        Assert.Null(SalarySeasonTransitionEngine.BlockingReason(world));
        Assert.Equal(SalaryRequestStatus.CANCELLED, request.Status);
        Assert.Equal("CONTRACT_IN_FORCE", request.Resolution);
        Assert.Equal(0, request.DecisionVersion);
        Assert.Equal(100, player.SalaryPerRound);
        Assert.Equal(club.Id, player.ClubId);
        Assert.True(ClassicEconomyEngine.IsSeasonRenewed(world, player));
        Assert.Empty(world.Auctions);
        Assert.Empty(world.Economy.ContractHistory);
    }

    [Fact] public void ActiveSalaryAuctionKeepsItsIdentityAndDeadlineAcrossSeasonBoundary()
    {
        var(w,c,p,r)=Setup();w.RoundNumber=w.TotalRoundsInSeason;w.RoundState=RoundState.RESULTS;
        Assert.True(ClassicEconomyEngine.RespondSalary(w,c,r.Id,false,"refuse").Success);
        var a=Assert.Single(w.Auctions);var end=(a.ExpiresAtSeason,a.ExpiresAtRound);
        CompetitionAndSeasonEngine.FinalizeSeasonAndAdvance(w,1);
        Assert.Contains(a,w.Auctions);Assert.Equal(end,(a.ExpiresAtSeason,a.ExpiresAtRound));
        Assert.False(a.IsClosed);Assert.Null(p.ClubId);Assert.Equal(2,w.SeasonNumber);
    }

    [Theory][InlineData(1)][InlineData(3000)][InlineData(50000)]
    public void PreviewAndSigningUseOneFormulaWithoutChangingSellerWage(double sellerWage)
    {
        var(w,c,p,r)=Setup();w.Economy.SalaryRequests.Clear();p.SalaryPerRound=(decimal)sellerWage;
        var buyer=w.Clubs[1];decimal expected=MarketContractTermsEngine.SigningSalary(p);
        string token=MarketContractTermsEngine.SalaryTermsToken(w,p);
        Assert.Equal((decimal)sellerWage,p.SalaryPerRound);Assert.Null(MarketContractTermsEngine.ValidateConfirmation(w,p,token));
        var sale=MarketAndEconomyEngine.ExecuteTransfer(w,buyer.Id,c.Id,p.Id,100,"same salary calculation","buy");
        Assert.True(sale.Success,sale.ErrorMessage);Assert.Equal(expected,p.SalaryPerRound);Assert.Equal(buyer.Id,p.ClubId);
        Assert.Equal(0,w.Transactions.Where(t=>t.PlayerId==p.Id).Sum(t=>t.Amount));
    }

    [Fact] public void AiCanResolveANewOrRetriedRequestAfterItsRoundFinancialPassWithoutRepeatingLoans()
    {
        var(w,c,p,r)=Setup(true);w.Economy.AiFinancialRounds.Add($"{c.Id}:S{w.SeasonNumber}:R{w.RoundNumber}");
        ClassicEconomyEngine.ProcessAiFinances(w);ClassicEconomyEngine.ProcessAiFinances(w);
        Assert.Equal(SalaryRequestStatus.ACCEPTED,r.Status);Assert.Equal(110,p.SalaryPerRound);
        Assert.Equal(1,r.DecisionVersion);Assert.Empty(w.BankLoans);
    }

    [Fact] public void EditedSalaryTermsRequireFreshConfirmation()
    {
        var(w,c,p,r)=Setup();string token=MarketContractTermsEngine.SalaryTermsToken(w,p);
        Assert.Contains("PREVIEW_REQUIRED",MarketContractTermsEngine.ValidateConfirmation(w,p,null));
        p.Strength=50;Assert.Contains("TERMS_CHANGED",MarketContractTermsEngine.ValidateConfirmation(w,p,token));
        Assert.Null(MarketContractTermsEngine.ValidateConfirmation(w,p,MarketContractTermsEngine.SalaryTermsToken(w,p)));
    }
}
