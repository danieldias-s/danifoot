using System.Text.Json;
using Elifoot.Contracts;
using Elifoot.Domain;
namespace Elifoot.Domain.Tests;

public class OperatingEconomy012DFixTests
{
    [Fact] public void ExistingSaveKeepsItsSeasonTermsAndBalancesUntilTheScheduledUpgrade()
    {
        var w=WorldSeedFactory.CreateSeededWorld();w.Economy.OperatingRules=new();w.SeasonNumber=7;w.RoundNumber=13;
        var c=w.Clubs[0];decimal cash=c.CashBalance;var p=w.Players[0];decimal salary=p.SalaryPerRound;
        OperatingEconomyEngine.EnsureScheduledRules(w);Assert.Equal(8,w.Economy.OperatingUpgradeSeason);
        Assert.False(w.Economy.OperatingRules.AnnualSponsorship);
        var restored=JsonSerializer.Deserialize<GameWorld>(JsonSerializer.Serialize(w))!;restored.SeasonNumber=8;restored.RoundNumber=1;
        OperatingEconomyEngine.EnsureScheduledRules(restored);Assert.Equal(OperatingEconomyEngine.CurrentPolicy,restored.Economy.OperatingRules.Name);
        Assert.Null(restored.Economy.OperatingUpgradeSeason);Assert.Equal(cash,restored.Clubs[0].CashBalance);Assert.Equal(salary,restored.Players[0].SalaryPerRound);
    }
    private static GameWorld AnnualWorld()
    {
        var w=WorldSeedFactory.CreateSeededWorld();w.Economy.OperatingRules=new(){Name="annual-test",AnnualSponsorship=true};return w;
    }
    [Theory][InlineData(DivisionTier.PrimeiraDivisao,185000)][InlineData(DivisionTier.SegundaDivisao,125000)]
    [InlineData(DivisionTier.TerceiraDivisao,92000)][InlineData(DivisionTier.QuartaDivisao,72000)][InlineData(DivisionTier.Distrital,20000)]
    public void SeasonWithoutGamesPaysOneAnnualCommercialBudgetAndNoUncontractedMatchWages(DivisionTier division,int annual)
    {
        var w=AnnualWorld();var c=w.Clubs.First(c=>c.DivisionTier==division);decimal before=c.CashBalance;
        var p=w.Players.First(p=>p.ClubId==c.Id);int term=p.ContractRoundsRemaining;
        for(int round=1;round<=22;round++)
        {w.RoundNumber=round;MarketAndEconomyEngine.SettleMatchAndRoundFinancials(w,[]);MarketAndEconomyEngine.SettleMatchAndRoundFinancials(w,[]);}
        Assert.Equal(before+annual,c.CashBalance);Assert.Equal(term,p.ContractRoundsRemaining);
        Assert.Equal(annual,w.Transactions.Where(t=>t.ClubId==c.Id&&t.Category==FinancialTransactionCategory.Sponsorship).Sum(t=>t.Amount));
        Assert.DoesNotContain(w.Transactions,t=>t.ClubId==c.Id&&t.Category==FinancialTransactionCategory.PlayerSalary);
    }
    [Fact] public void SponsorshipTermsSurviveReloadAndDivisionChangeWithoutBackpayOrOverpayment()
    {
        var w=AnnualWorld();var c=w.Clubs.First(c=>c.DivisionTier==DivisionTier.QuartaDivisao);decimal before=c.CashBalance;
        for(int r=1;r<=11;r++){w.RoundNumber=r;MarketAndEconomyEngine.SettleMatchAndRoundFinancials(w,[]);}
        var restored=JsonSerializer.Deserialize<GameWorld>(JsonSerializer.Serialize(w))!;var club=restored.Clubs.Single(x=>x.Id==c.Id);club.DivisionTier=DivisionTier.PrimeiraDivisao;
        MarketAndEconomyEngine.SettleMatchAndRoundFinancials(restored,[]);
        for(int r=12;r<=22;r++){restored.RoundNumber=r;MarketAndEconomyEngine.SettleMatchAndRoundFinancials(restored,[]);}
        Assert.Equal(before+72000,club.CashBalance);
        restored.RoundNumber=23;MarketAndEconomyEngine.SettleMatchAndRoundFinancials(restored,[]);Assert.Equal(before+72000,club.CashBalance);
        restored.SeasonNumber=2;restored.RoundNumber=1;MarketAndEconomyEngine.SettleMatchAndRoundFinancials(restored,[]);
        Assert.Equal(before+72000+8409.09m,club.CashBalance);Assert.Equal(185000,restored.Economy.SponsorshipBudgets[$"S2:{club.Id}"].AnnualBudget);
    }
    [Fact] public void OffMatchCommercialInstallmentDoesNotGenerateBankInterest()
    {
        var w=AnnualWorld();var c=w.Clubs.First(c=>c.DivisionTier==DivisionTier.QuartaDivisao);c.Stadium.UsableCapacity=9000;
        ClassicEconomyEngine.TakeClassicLoan(w,c,"one");ClassicEconomyEngine.TakeClassicLoan(w,c,"two");
        MarketAndEconomyEngine.SettleMatchAndRoundFinancials(w,[]);Assert.All(w.BankLoans,l=>Assert.Equal(0,l.InterestCharged));
        w.RoundNumber=2;var m=new MatchFixture{GameWorldId=w.Id,SeasonNumber=1,RoundNumber=2,HomeClubId=c.Id,AwayClubId=w.Clubs[0].Id,IsFinished=true,CurrentMinute=90};
        MarketAndEconomyEngine.SettleMatchAndRoundFinancials(w,[m]);MarketAndEconomyEngine.SettleMatchAndRoundFinancials(w,[m]);
        Assert.Equal(50000,w.BankLoans.Sum(l=>l.InterestCharged));Assert.Equal(1000000,w.BankLoans.Sum(l=>l.RemainingBalance));
    }
    [Fact] public void LowerGateDemandChangesOnlyRealAttendanceRevenueAndVariableCost()
    {
        var w=AnnualWorld();var h=w.Clubs[0];var a=w.Clubs[1];
        var normal=MarketAndEconomyEngine.CalculateMatchGateEconomics(h,a,CompetitionType.League,199801,1,1,1);
        var reduced=MarketAndEconomyEngine.CalculateMatchGateEconomics(h,a,CompetitionType.League,199801,1,1,1,.25);
        Assert.True(reduced.Attendance<normal.Attendance);Assert.Equal(normal.TicketPrice,reduced.TicketPrice);Assert.Equal(normal.FixedCost,reduced.FixedCost);
        Assert.Equal(reduced.Attendance*reduced.TicketPrice,reduced.Revenue);Assert.Equal(reduced.Attendance*h.Stadium.VariableAttendeeCostRate,reduced.VariableCost);
    }
    [Fact] public void CommercialQualityUsesNominalRegisteredPlayersAndCannotOverwriteWages()
    {
        var w=AnnualWorld();w.Economy.OperatingRules.GateQualityExponent=1;var home=w.Clubs[0];var away=w.Clubs[1];
        foreach(var p in w.Players.Where(p=>p.ClubId==home.Id||p.ClubId==away.Id)){p.Strength=41;p.SalaryPerRound=1234;p.InjuryDaysRemaining=10;p.SuspensionMatchesRemaining=4;}
        Assert.Equal(1,OperatingEconomyEngine.GateDemandFactor(w,home,away));
        foreach(var p in w.Players.Where(p=>p.ClubId==home.Id||p.ClubId==away.Id))p.Strength=20;
        Assert.Equal(20d/41,OperatingEconomyEngine.GateDemandFactor(w,home,away),10);
        Assert.All(w.Players.Where(p=>p.ClubId==home.Id||p.ClubId==away.Id),p=>Assert.Equal(1234,p.SalaryPerRound));
    }
    [Fact] public void AiDebtReserveProtectsPrincipalWithoutChangingCashDebtOrHumanAuthority()
    {
        var w=AnnualWorld();w.Economy.OperatingRules.ReserveBorrowedCapitalForAi=true;var buyer=w.Clubs[0];var seller=w.Clubs[1];
        ClassicEconomyEngine.TakeClassicLoan(w,buyer,"working-capital");buyer.CashBalance=510000;
        var player=w.Players.Last(p=>p.ClubId==seller.Id);decimal debt=w.BankLoans[0].RemainingBalance;
        var refused=MarketAndEconomyEngine.ExecuteTransfer(w,buyer.Id,seller.Id,player.Id,20000,"optional reinforcement","reserve");
        Assert.False(refused.Success);Assert.Equal(510000,buyer.CashBalance);Assert.Equal(debt,w.BankLoans[0].RemainingBalance);
        buyer.HumanCoachId=Guid.NewGuid();
        Assert.True(MarketAndEconomyEngine.ExecuteTransfer(w,buyer.Id,seller.Id,player.Id,20000,"human risk decision","human-reserve").Success);
        Assert.Equal(490000,buyer.CashBalance);Assert.Equal(debt,w.BankLoans[0].RemainingBalance);
    }
    [Fact] public void AiRepaidPrincipalReleasesItsBudgetWithoutFutureInterest()
    {
        var w=AnnualWorld();w.Economy.OperatingRules.ReserveBorrowedCapitalForAi=true;var c=w.Clubs[0];
        ClassicEconomyEngine.TakeClassicLoan(w,c,"credit");decimal before=c.CashBalance;
        Assert.Equal(500000,OperatingEconomyEngine.AiDebtReserve(w,c));
        Assert.True(ClassicEconomyEngine.RepayLoan(w,c,w.BankLoans[0].Id,"pay").Success);
        Assert.Equal(before-500000,c.CashBalance);Assert.Equal(0,OperatingEconomyEngine.AiDebtReserve(w,c));
        var match=new MatchFixture{SeasonNumber=1,RoundNumber=2,HomeClubId=c.Id,AwayClubId=w.Clubs[1].Id,CurrentMinute=90,IsFinished=true};
        ClassicEconomyEngine.SettleLoans(w,[match]);Assert.Equal(0,w.BankLoans[0].InterestCharged);
    }
    [Theory][InlineData(true)][InlineData(false)]
    public void AiBorrowingRequiresObservedOperatingProfitRatherThanSalesOrPrizeWindfalls(bool profitable)
    {
        var w=AnnualWorld();w.Economy.OperatingRules.AiCreditRequiresOperatingSurplus=true;var c=w.Clubs[0];w.Fixtures.Clear();
        for(int round=1;round<=4;round++)
        {
            w.RoundNumber=round;w.Fixtures.Add(new(){SeasonNumber=1,RoundNumber=round,HomeClubId=c.Id,AwayClubId=w.Clubs[1].Id,CurrentMinute=90,IsFinished=true});
            MarketAndEconomyEngine.TryRecordTransaction(w,c,FinancialTransactionCategory.TicketRevenue,profitable?100000:10000,"Gate","gate"+round);
            MarketAndEconomyEngine.TryRecordTransaction(w,c,FinancialTransactionCategory.PlayerSalary,-20000,"Wages","wages"+round);
        }
        MarketAndEconomyEngine.TryRecordTransaction(w,c,FinancialTransactionCategory.PrizeMoney,10000000,"Extraordinary prize","windfall");
        Assert.Equal(profitable,OperatingEconomyEngine.AiCanServiceNewLoan(w,c));
        if(profitable)
        {w.BankLoans.Add(new(){ClubId=c.Id,RemainingBalance=500001,UnpaidInterest=1,Mode=BankLoanMode.ClassicFixed});Assert.False(OperatingEconomyEngine.AiCanServiceNewLoan(w,c));}
    }
    [Fact] public void AiCreditProjectionIncludesLegacyInterestWithoutConvertingTheExistingLoan()
    {
        var w=AnnualWorld();w.Economy.OperatingRules.AiCreditRequiresOperatingSurplus=true;var c=w.Clubs[0];w.Fixtures.Clear();
        for(int round=1;round<=4;round++)
        {
            w.RoundNumber=round;w.Fixtures.Add(new(){SeasonNumber=1,RoundNumber=round,HomeClubId=c.Id,AwayClubId=w.Clubs[1].Id,CurrentMinute=90,IsFinished=true});
            MarketAndEconomyEngine.TryRecordTransaction(w,c,FinancialTransactionCategory.TicketRevenue,80000,"Net operating income","legacy-gate"+round);
        }
        var legacy=new BankLoan{ClubId=c.Id,RemainingBalance=500000,InterestRatePerRound=.015m,RemainingRounds=20,InstallmentPerRound=28750};
        w.BankLoans.Add(legacy);Assert.False(OperatingEconomyEngine.AiCanServiceNewLoan(w,c));
        Assert.Equal(BankLoanMode.LegacyInstallment,legacy.Mode);Assert.Equal(500000,legacy.RemainingBalance);Assert.Empty(legacy.History);
    }
}
