using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Domain;
namespace Elifoot.Domain.Tests;

public class ClassicEconomy012DTests
{
    [Fact] public void DifferentClubsMayReuseACommandKeyWithoutLosingEitherContractHistory()
    {
        var(w,_)=World();
        for(int i=0;i<2;i++)
        {
            var buyer=w.Clubs[i];var seller=w.Clubs[i+2];var p=w.Players.First(p=>p.ClubId==seller.Id);
            var result=MarketAndEconomyEngine.ExecuteTransfer(w,buyer.Id,seller.Id,p.Id,1000,"separate coaches","shared-client-key");
            Assert.True(result.Success,result.ErrorMessage);
        }
        Assert.Equal(2,w.Economy.ContractHistory.Count);Assert.Equal(2,w.Economy.ContractHistory.Select(h=>h.Key).Distinct().Count());
    }
    [Fact] public void ReferenceSalaryTracksEditedOrImportedStrengthWithoutChangingNegotiatedWage()
    {
        var p=new Player{Strength=1,Position=PlayerPosition.M,SalaryPerRound=1234};Assert.Equal(222,p.ReferenceSalaryPerRound);
        p.Strength=50;Assert.Equal(5861,p.ReferenceSalaryPerRound);Assert.Equal(1234,p.SalaryPerRound);
        var imported=JsonSerializer.Deserialize<Player>("{\"Strength\":50,\"Position\":\"M\",\"SalaryPerRound\":1234,\"ReferenceSalaryPerRound\":0}")!;
        Assert.Equal(5861,imported.ReferenceSalaryPerRound);Assert.Equal(1234,imported.SalaryPerRound);
    }
    private static (GameWorld W,Club C) World()
    {
        var w=WorldSeedFactory.CreateSeededWorld();w.RoundState=RoundState.PREPARING;w.Auctions.Clear();
        return(w,w.Clubs.First(c=>c.DivisionTier==DivisionTier.QuartaDivisao));
    }
    private static MatchFixture Match(GameWorld w,Club c,int round=1)=>new(){GameWorldId=w.Id,SeasonNumber=w.SeasonNumber,RoundNumber=round,
        HomeClubId=c.Id,AwayClubId=w.Clubs.First(x=>x.Id!=c.Id).Id,IsFinished=true,CurrentMinute=90,CompetitionType=CompetitionType.League};
    private static SalaryRaiseRequest Request(GameWorld w,Club c,Player p)
    {p.ContractRoundsRemaining=0;var r=new SalaryRaiseRequest{Id=Guid.NewGuid(),ClubId=c.Id,PlayerId=p.Id,CurrentSalary=p.SalaryPerRound,RequestedSalary=p.SalaryPerRound+100,RenewalMatches=20};w.Economy.SalaryRequests.Add(r);return r;}
    [Fact] public void LegacyTermCannotEraseResidualDebt()
    {
        var(w,c)=World();var l=new BankLoan{ClubId=c.Id,PrincipalAmount=500000,RemainingBalance=500000,RemainingRounds=20,InstallmentPerRound=28750};w.BankLoans.Add(l);
        ClassicEconomyEngine.SettleLoans(w,Enumerable.Range(1,20).Select(r=>Match(w,c,r)));
        Assert.Equal(0,l.RemainingRounds);Assert.False(l.IsPaidOff);Assert.InRange(l.RemainingBalance,8000,10000);Assert.Equal("OVERDUE",l.Status);
        decimal before=l.RemainingBalance;ClassicEconomyEngine.SettleLoans(w,[Match(w,c,21)]);Assert.True(l.RemainingBalance<before);
        Assert.Equal(BankLoanMode.LegacyInstallment,l.Mode);Assert.Equal(.015m,l.InterestRatePerRound);
    }
    [Theory][InlineData(CompetitionType.League)][InlineData(CompetitionType.CupPreliminary1)][InlineData(CompetitionType.CupFinal)]
    public void ClassicChargesExactlyOneInterestPerActuallyPlayedMatch(CompetitionType type)
    {
        var(w,c)=World();decimal cash=c.CashBalance;Assert.True(ClassicEconomyEngine.TakeClassicLoan(w,c,"loan").Success);
        var l=Assert.Single(w.BankLoans);var m=Match(w,c);m.CompetitionType=type;
        ClassicEconomyEngine.SettleLoans(w,[m]);ClassicEconomyEngine.SettleLoans(w,[m]);
        Assert.Equal(cash+475000,c.CashBalance);Assert.Equal(500000,l.RemainingBalance);Assert.Equal(500000,l.OutstandingPrincipal);
        Assert.Equal(25000,l.InterestCharged);Assert.Equal(25000,l.InterestPaid);Assert.Equal(0,l.PrincipalPaid);Assert.Single(l.ChargedMatches);
        Assert.True(ClassicEconomyEngine.RepayLoan(w,c,l.Id,"repay").Success);Assert.True(l.IsPaidOff);Assert.Equal(cash-25000,c.CashBalance);
        Assert.True(ClassicEconomyEngine.RepayLoan(w,c,l.Id,"repay").Success);Assert.Equal(cash-25000,c.CashBalance);
    }
    [Theory][InlineData("unfinished")][InlineData("cancelled")][InlineData("postponed")][InlineData("zero-minute")]
    public void NoInterestForUndisputedGames(string kind)
    {
        var(w,c)=World();ClassicEconomyEngine.TakeClassicLoan(w,c,"loan");var m=Match(w,c);
        if(kind=="unfinished")m.IsFinished=false;if(kind=="cancelled")m.IsCancelled=true;if(kind=="postponed")m.IsPostponed=true;if(kind=="zero-minute")m.CurrentMinute=0;
        ClassicEconomyEngine.SettleLoans(w,[m]);Assert.Equal(0,w.BankLoans[0].InterestCharged);
    }
    [Fact] public void LoanAfterGameHasNoRetroactiveInterestAndSurvivesSerialization()
    {
        var(w,c)=World();var played=Match(w,c);w.Fixtures.Add(played);w.RoundState=RoundState.RESULTS;
        ClassicEconomyEngine.TakeClassicLoan(w,c,"loan");ClassicEconomyEngine.SettleLoans(w,[played]);Assert.Equal(0,w.BankLoans[0].InterestCharged);
        var restored=JsonSerializer.Deserialize<GameWorld>(JsonSerializer.Serialize(w))!;
        ClassicEconomyEngine.SettleLoans(restored,[played]);Assert.Equal(0,restored.BankLoans[0].InterestCharged);
        ClassicEconomyEngine.SettleLoans(restored,[Match(w,c,2)]);Assert.Equal(25000,restored.BankLoans[0].InterestCharged);
    }
    [Fact] public void UnpaidInterestIsRealDebtWithoutCompoundInterestOrGiftedCash()
    {
        var(w,c)=World();ClassicEconomyEngine.TakeClassicLoan(w,c,"loan");c.CashBalance=1000;
        ClassicEconomyEngine.SettleLoans(w,[Match(w,c)]);var l=w.BankLoans[0];Assert.Equal(0,c.CashBalance);Assert.Equal(524000,l.RemainingBalance);Assert.Equal(24000,l.UnpaidInterest);
        ClassicEconomyEngine.SettleLoans(w,[Match(w,c,2)]);Assert.Equal(549000,l.RemainingBalance);Assert.Equal(49000,l.UnpaidInterest);Assert.Equal(500000,l.OutstandingPrincipal);
        Assert.False(ClassicEconomyEngine.RepayLoan(w,c,l.Id,"no-cash").Success);Assert.False(l.IsPaidOff);
    }
    [Fact] public void FourthDivisionHasIndividualOneAndTwoLoanLimitsWithoutCashFeedback()
    {
        var(w,c)=World();c.Stadium.UsableCapacity=6000;Assert.Equal(1,ClassicEconomyEngine.AssessCredit(w,c).LoanLimit);
        var other=w.Clubs.Last(x=>x.DivisionTier==DivisionTier.QuartaDivisao);other.Stadium.UsableCapacity=9000;
        Assert.Equal(2,ClassicEconomyEngine.AssessCredit(w,other).LoanLimit);ClassicEconomyEngine.TakeClassicLoan(w,c,"loan");Assert.False(ClassicEconomyEngine.TakeClassicLoan(w,c,"second").Success);
        c.DivisionTier=DivisionTier.Distrital;Assert.Equal(500000,ClassicEconomyEngine.AssessCredit(w,c).OutstandingDebt);
    }
    [Fact] public void RequestsArePersistentLimitedToOneAndDoNotOverrideNegotiatedTerms()
    {
        var(w,c)=World();var players=w.Players.Where(p=>p.ClubId==c.Id).Take(3).ToList();
        foreach(var p in players){p.Strength=20;p.SalaryPerRound=100;p.ContractRoundsRemaining=0;p.IsListedForTransfer=false;w.PlayerStrengthHistory.Add(new(Guid.NewGuid().ToString(),p.Id,1,1,c.Id,c.Name,c.DivisionTier,19,20,"RESERVE_PROGRESS",null,"test","test"));}
        var protectedPlayer=players[0];protectedPlayer.ContractRoundsRemaining=20;ClassicEconomyEngine.RecordContract(w,protectedPlayer,100,"NEGOTIATED","protect");
        var m=Match(w,c);ClassicEconomyEngine.GenerateSalaryRequests(w,[m]);ClassicEconomyEngine.GenerateSalaryRequests(w,[m]);
        var r=Assert.Single(w.Economy.SalaryRequests);Assert.NotEqual(protectedPlayer.Id,r.PlayerId);Assert.Equal(SalaryRequestStatus.PENDING,r.Status);
        Assert.True(ClassicEconomyEngine.RespondSalary(w,c,r.Id,true,"accept").Success);Assert.Equal(SalaryRequestStatus.ACCEPTED,r.Status);
        ClassicEconomyEngine.GenerateSalaryRequests(w,[m]);Assert.Single(w.Economy.SalaryRequests);
        var restored=JsonSerializer.Deserialize<GameWorld>(JsonSerializer.Serialize(w))!;Assert.Equal(SalaryRequestStatus.ACCEPTED,restored.Economy.SalaryRequests[0].Status);
    }
    [Fact] public void PromotionCanTriggerRequestButOnlyInTheNewSeason()
    {
        var(w,c)=World();foreach(var p in w.Players.Where(p=>p.ClubId==c.Id)){p.SalaryPerRound=10;p.NegotiatedContractProtected=false;p.ContractRoundsRemaining=0;p.IsListedForTransfer=false;}
        c.DivisionTier=DivisionTier.TerceiraDivisao;ClassicEconomyEngine.RegisterPromotions(w,[c.Id]);
        ClassicEconomyEngine.GenerateSalaryRequests(w,[Match(w,c)]);Assert.Empty(w.Economy.SalaryRequests);
        w.SeasonNumber++;ClassicEconomyEngine.GenerateSalaryRequests(w,[Match(w,c)]);Assert.Equal("DIVISION_PROMOTION",Assert.Single(w.Economy.SalaryRequests).Reason);
    }
    [Fact] public void RefusalCreatesZeroAuctionForFreeAgentWithoutFormerClubRevenue()
    {
        var(w,c)=World();var p=w.Players.First(p=>p.ClubId==c.Id&&p.Position==PlayerPosition.A);var r=Request(w,c,p);decimal oldCash=c.CashBalance;
        Assert.True(ClassicEconomyEngine.RespondSalary(w,c,r.Id,false,"refuse").Success);Assert.Null(p.ClubId);
        var a=Assert.Single(w.Auctions);Assert.Equal(0,a.MinimumBid);Assert.Equal(AuctionReason.SalaryRenewalRefused,a.Reason);
        var buyer=w.Clubs.First(x=>x.Id!=c.Id);Assert.True(ClassicEconomyEngine.PlaceBid(w,buyer,a.Id,0,"bid").Success);
        w.RoundNumber=a.ExpiresAtRound;w.SeasonNumber=a.ExpiresAtSeason!.Value;w.RoundState=RoundState.RESULTS;w.SettledFinancialRounds.Add($"round_settled:S{w.SeasonNumber}:R{w.RoundNumber}");MarketAndEconomyEngine.SettleAuction(w,a);Assert.Equal(buyer.Id,p.ClubId);Assert.Equal(oldCash,c.CashBalance);Assert.Equal("SOLD",a.ClosureReason);
        Assert.DoesNotContain(w.Transactions,t=>t.Category==FinancialTransactionCategory.AuctionSale&&t.PlayerId==p.Id);
    }
    [Fact] public void MinimumSquadMakesRefusalAMandatoryRenewalEvenWithoutCash()
    {
        var(w,c)=World();var squad=w.Players.Where(p=>p.ClubId==c.Id).ToList();var keep=squad.Where(p=>p.Position==PlayerPosition.G).Take(1).Concat(squad.Where(p=>p.Position!=PlayerPosition.G).Take(10)).ToList();
        foreach(var p in squad.Except(keep))p.ClubId=null;c.CashBalance=-100;var player=keep[0];var r=Request(w,c,player);
        var result=ClassicEconomyEngine.RespondSalary(w,c,r.Id,false,"refuse",actor:"AI");Assert.True(result.Success);Assert.Contains("OBRIGATÓRIA",result.Error);
        Assert.Equal(c.Id,player.ClubId);Assert.Empty(w.Auctions);Assert.Equal(-100,c.CashBalance);Assert.Equal(SalaryRequestStatus.ACCEPTED,r.Status);
    }
    [Fact] public void DebtAuctionPrioritizesForceThenSalaryThenStableIdAndReconcilesActualSale()
    {
        var(w,c)=World();c.CashBalance=-50000;var squad=w.Players.Where(p=>p.ClubId==c.Id).ToList();foreach(var p in squad){p.Strength=10;p.SalaryPerRound=100;}
        var strong=squad.Last();strong.Strength=50;strong.NegotiatedContractProtected=true;
        ClassicEconomyEngine.EvaluateInsolvency(w);var a=Assert.Single(w.Auctions);Assert.Equal(strong.Id,a.PlayerId);Assert.Equal(c.Id,strong.ClubId);Assert.Equal(0,a.MinimumBid);
        var buyer=w.Clubs.First(x=>x.Id!=c.Id);decimal before=buyer.CashBalance;
        Assert.True(ClassicEconomyEngine.PlaceBid(w,buyer,a.Id,60000,"bid").Success);
        w.RoundState=RoundState.RESULTS;w.SettledFinancialRounds.Add($"round_settled:S{w.SeasonNumber}:R{w.RoundNumber}");
        MarketAndEconomyEngine.SettleAuction(w,a);MarketAndEconomyEngine.SettleAuction(w,a);
        Assert.Equal(10000,c.CashBalance);Assert.Equal(before-60000,buyer.CashBalance);Assert.Equal(buyer.Id,strong.ClubId);
        var tx=w.Transactions.Where(t=>t.PlayerId==strong.Id&&t.TransactionType=="FinancialInsolvency").ToList();Assert.Equal(2,tx.Count);Assert.Equal(0,tx.Sum(t=>t.Amount));
        ClassicEconomyEngine.EvaluateInsolvency(w);Assert.False(w.Economy.Crises[c.Id].IsInsolvent);
    }
    [Fact] public void UnsoldDebtAuctionKeepsOwnershipAndDebtAndDoesNotLoop()
    {
        var(w,c)=World();c.CashBalance=-50000;ClassicEconomyEngine.EvaluateInsolvency(w);var a=Assert.Single(w.Auctions);var p=w.Players.First(p=>p.Id==a.PlayerId);
        w.RoundState=RoundState.RESULTS;w.SettledFinancialRounds.Add($"round_settled:S{w.SeasonNumber}:R{w.RoundNumber}");
        MarketAndEconomyEngine.SettleAuction(w,a);Assert.Equal(c.Id,p.ClubId);Assert.Equal("AUCTION_UNSOLD",a.ClosureReason);
        for(int i=0;i<10;i++)ClassicEconomyEngine.EvaluateInsolvency(w);Assert.Single(w.Auctions);Assert.Equal(-50000,c.CashBalance);
        w.RoundNumber+=2;ClassicEconomyEngine.EvaluateInsolvency(w);Assert.Equal(2,w.Auctions.Count);
    }
    [Fact] public void InsolvencyWithNoEligiblePlayerRetainsDebtAndReturns()
    {
        var(w,c)=World();var squad=w.Players.Where(p=>p.ClubId==c.Id).ToList();var keep=squad.Where(p=>p.Position==PlayerPosition.G).Take(1).Concat(squad.Where(p=>p.Position!=PlayerPosition.G).Take(10));
        foreach(var p in squad.Except(keep).ToList())p.ClubId=null;c.CashBalance=-1;
        for(int i=0;i<10;i++)ClassicEconomyEngine.EvaluateInsolvency(w);Assert.Empty(w.Auctions);Assert.Equal(-1,c.CashBalance);
        Assert.Single(w.Economy.CrisisHistory,h=>h.Kind=="NO_ELIGIBLE_PLAYER"&&h.ClubId==c.Id);
    }
    [Fact] public void BidsReserveCashAndRepaymentCannotSpendItTwice()
    {
        var(w,c)=World();ClassicEconomyEngine.TakeClassicLoan(w,c,"loan");c.CashBalance=500000;
        var p=w.Players.First(p=>p.ClubId!=c.Id);p.ClubId=null;var a=new AuctionItem{PlayerId=p.Id,MinimumBid=0,Reason=AuctionReason.SalaryRenewalRefused};w.Auctions.Add(a);
        Assert.True(ClassicEconomyEngine.PlaceBid(w,c,a.Id,10000,"bid").Success);Assert.True(ClassicEconomyEngine.PlaceBid(w,c,a.Id,10000,"bid").Success);
        Assert.False(ClassicEconomyEngine.RepayLoan(w,c,w.BankLoans[0].Id,"repay").Success);Assert.Equal(490000,ClassicEconomyEngine.AvailableCash(w,c));
        Assert.False(ClassicEconomyEngine.PlaceBid(w,c,a.Id,10000,"newbid").Success);
    }
    [Fact] public void OldSaveMigrationPreservesOriginalLoanAndProtectsExistingNegotiatedSalary()
    {
        var(w,c)=World();w.Economy=new();var p=w.Players.First(p=>p.ClubId==c.Id);p.SalaryPerRound=9876;
        var loan=new BankLoan{ClubId=c.Id,RemainingBalance=8654,RemainingRounds=0,InterestRatePerRound=.015m,InstallmentPerRound=28750};w.BankLoans.Add(loan);
        ClassicEconomyEngine.EnsureMigrated(w);Assert.Equal(9876,p.SalaryPerRound);Assert.True(p.NegotiatedContractProtected);Assert.False(loan.IsPaidOff);
        Assert.Equal(BankLoanMode.LegacyInstallment,loan.Mode);Assert.Equal(8654,loan.LegacyOpeningBalance);Assert.NotEmpty(w.Economy.CoverageWarnings);
    }
    [Fact] public void TwoLoansChargeFiftyThousandAndReborrowingRequiresActualRepayment()
    {
        var(w,c)=World();c.Stadium.UsableCapacity=9000;
        ClassicEconomyEngine.TakeClassicLoan(w,c,"one");ClassicEconomyEngine.TakeClassicLoan(w,c,"two");decimal before=c.CashBalance;
        ClassicEconomyEngine.SettleLoans(w,[Match(w,c)]);Assert.Equal(before-50000,c.CashBalance);Assert.Equal(1000000,w.BankLoans.Sum(l=>l.RemainingBalance));
        Assert.False(ClassicEconomyEngine.TakeClassicLoan(w,c,"three").Success);Assert.True(ClassicEconomyEngine.RepayLoan(w,c,w.BankLoans[0].Id,"repay").Success);
        Assert.True(ClassicEconomyEngine.TakeClassicLoan(w,c,"three").Success);Assert.Equal(2,ClassicEconomyEngine.AssessCredit(w,c).ActiveLoans);
        c.DivisionTier=DivisionTier.Distrital;Assert.Equal(0,ClassicEconomyEngine.AssessCredit(w,c).AvailableLoans);Assert.False(ClassicEconomyEngine.TakeClassicLoan(w,c,"four").Success);
    }
    [Theory][InlineData(11)][InlineData(12)][InlineData(15)][InlineData(20)]
    public void InsolvencyProtectsTheOnlyGoalkeeperAndResolvesPriorityTies(int squadSize)
    {
        var(w,c)=World();var squad=w.Players.Where(p=>p.ClubId==c.Id).ToList();
        var keep=squad.Where(p=>p.Position==PlayerPosition.G).Take(1).Concat(squad.Where(p=>p.Position!=PlayerPosition.G).Take(squadSize-1)).ToList();
        foreach(var p in squad.Except(keep))p.ClubId=null;
        while(keep.Count<squadSize){var extra=new Player{ClubId=c.Id,Position=PlayerPosition.D};w.Players.Add(extra);keep.Add(extra);}
        var keeper=keep.Single(p=>p.Position==PlayerPosition.G);keeper.Strength=50;keeper.SalaryPerRound=999999;
        foreach(var p in keep.Where(p=>p.Position!=PlayerPosition.G)){p.Strength=20;p.SalaryPerRound=100;p.NegotiatedContractProtected=true;}
        var tied=keep.Where(p=>p.Position!=PlayerPosition.G).OrderBy(p=>p.Id).Take(2).ToList();foreach(var p in tied)p.SalaryPerRound=200;
        c.CashBalance=-1000000;ClassicEconomyEngine.EvaluateInsolvency(w);
        if(squadSize==11)Assert.Empty(w.Auctions);
        else {var a=Assert.Single(w.Auctions);Assert.Equal(tied[0].Id,a.PlayerId);Assert.NotEqual(keeper.Id,a.PlayerId);
            var attempts=MarketAndEconomyEngine.ExecuteTransfer(w,w.Clubs.First(x=>x.Id!=c.Id).Id,c.Id,a.PlayerId,1000,"parallel","double");Assert.False(attempts.Success);}
        Assert.Equal(c.Id,keeper.ClubId);
    }
    [Fact] public void ExpiringContractCannotReleaseAPlayerReservedForDebtAuction()
    {
        var(w,c)=World();c.CashBalance=-9999999;var p=w.Players.Last(p=>p.ClubId==c.Id);p.Strength=50;p.ContractRoundsRemaining=1;
        w.Economy.Rules.SeasonContractsEnabled=false;
        ClassicEconomyEngine.EvaluateInsolvency(w);var a=Assert.Single(w.Auctions);Assert.Equal(p.Id,a.PlayerId);
        MarketAndEconomyEngine.SettleMatchAndRoundFinancials(w,[Match(w,c)]);Assert.Equal(c.Id,p.ClubId);Assert.True(p.ContractRoundsRemaining>0);Assert.Single(w.Auctions);
    }
    [Fact] public void EconomicReplaySurvivesSerializationWithoutDuplicateSalaryOrInterest()
    {
        var(w,c)=World();var m=Match(w,c);w.Fixtures=[m];ClassicEconomyEngine.TakeClassicLoan(w,c,"loan");
        // This loan was created after the completed fixture, so only the next official game charges it.
        m=Match(w,c,2);w.Fixtures=[m];w.RoundNumber=2;
        MarketAndEconomyEngine.SettleMatchAndRoundFinancials(w,[m]);
        var restored=JsonSerializer.Deserialize<GameWorld>(JsonSerializer.Serialize(w))!;var balances=restored.Clubs.Select(c=>c.CashBalance).ToList();int transactions=restored.Transactions.Count;
        MarketAndEconomyEngine.SettleMatchAndRoundFinancials(restored,restored.Fixtures);
        Assert.Equal(balances,restored.Clubs.Select(c=>c.CashBalance));Assert.Equal(transactions,restored.Transactions.Count);
        Assert.Equal(25000,restored.BankLoans[0].InterestCharged);
        Assert.Single(restored.Transactions,t=>t.ClubId==c.Id&&t.Category==FinancialTransactionCategory.PlayerSalary);
    }
    [Theory][InlineData(true)][InlineData(false)]
    public void AiAcceptsAffordableRaisesAndRefusesUnaffordableOnesWithoutUnlimitedCredit(bool affordable)
    {
        var(w,c)=World();c.Stadium.UsableCapacity=6000;
        ClassicEconomyEngine.TakeClassicLoan(w,c,"existing");c.CashBalance=affordable?10000000:0;
        var p=w.Players.Last(p=>p.ClubId==c.Id&&p.Position!=PlayerPosition.G);var r=Request(w,c,p);
        ClassicEconomyEngine.ProcessAiFinances(w);ClassicEconomyEngine.ProcessAiFinances(w);
        Assert.Equal(affordable?SalaryRequestStatus.ACCEPTED:SalaryRequestStatus.REFUSED,r.Status);
        if(affordable){Assert.Equal(c.Id,p.ClubId);Assert.True(p.NegotiatedContractProtected);}
        else {Assert.Null(p.ClubId);Assert.Single(w.Auctions,a=>a.PlayerId==p.Id&&a.Reason==AuctionReason.SalaryRenewalRefused);Assert.Single(w.BankLoans,l=>l.ClubId==c.Id);}
    }
    [Fact] public void AnInsolventMinimumSquadCanRemainInCrisisAcrossSeasonsWithoutDebtErasure()
    {
        var(w,c)=World();var squad=w.Players.Where(p=>p.ClubId==c.Id).ToList();var keep=squad.Where(p=>p.Position==PlayerPosition.G).Take(1).Concat(squad.Where(p=>p.Position!=PlayerPosition.G).Take(10)).ToHashSet();
        foreach(var p in squad.Where(p=>!keep.Contains(p)))p.ClubId=null;
        c.CashBalance=-10000000;w.BankLoans.Add(new(){ClubId=c.Id,RemainingBalance=8654,RemainingRounds=0});
        for(int season=1;season<=5;season++)for(int round=1;round<=22;round++)
        {w.SeasonNumber=season;w.RoundNumber=round;ClassicEconomyEngine.EvaluateInsolvency(w);Assert.Empty(w.Auctions);Assert.Equal(8654,w.BankLoans[0].RemainingBalance);Assert.Equal(11,w.Players.Count(p=>p.ClubId==c.Id));}
        Assert.False(w.BankLoans[0].IsPaidOff);Assert.True(w.Economy.Crises[c.Id].IsInsolvent);
    }
    [Theory][InlineData(1)][InlineData(2)][InlineData(3)]
    public void EveryActivePrincipalHasItsOwnFixedMatchInterest(int count)
    {
        var(w,c)=World();c.DivisionTier=DivisionTier.PrimeiraDivisao;c.Stadium.UsableCapacity=40000;
        for(int i=0;i<count;i++)Assert.True(ClassicEconomyEngine.TakeClassicLoan(w,c,"loan-"+i).Success);
        decimal before=c.CashBalance;ClassicEconomyEngine.SettleLoans(w,[Match(w,c)]);
        Assert.Equal(25000m*count,before-c.CashBalance);Assert.Equal(500000m*count,w.BankLoans.Sum(l=>l.RemainingBalance));
    }
}
