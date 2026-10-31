using Elifoot.Contracts;
namespace Elifoot.Domain;

/// <summary>Commercial cash-flow terms; bank and sporting rules are independent.</summary>
public sealed class OperatingEconomyRules
{
    public string Name { get; set; } = "LEGACY_012D";
    public bool AnnualSponsorship { get; set; }
    public double GateQualityExponent { get; set; }
    public bool ReserveBorrowedCapitalForAi { get; set; }
    public bool AiCreditRequiresOperatingSurplus { get; set; }
}
public record SponsorSeasonBudget(Guid ClubId,int Season,DivisionTier Division,decimal AnnualBudget,int Installments,int FirstPaymentRound);

public static class OperatingEconomyEngine
{
    public const string CurrentPolicy="CLASSIC_OPERATING_BALANCE_012D_FIX_V2";
    public static OperatingEconomyRules CurrentRules()=>new(){Name=CurrentPolicy,AnnualSponsorship=true,GateQualityExponent=2.2,
        ReserveBorrowedCapitalForAi=true,AiCreditRequiresOperatingSurplus=true};
    public static void EnsureScheduledRules(GameWorld world)
    {
        if(world.Economy.OperatingRules.Name is not ("LEGACY_012D" or "CLASSIC_OPERATING_BALANCE_012D_FIX_V1"))return;
        world.Economy.OperatingUpgradeSeason??=world.SeasonNumber+1;
        if(world.SeasonNumber<world.Economy.OperatingUpgradeSeason)return;
        world.Economy.OperatingRules=CurrentRules();world.Economy.OperatingUpgradeSeason=null;
    }
    public static bool AiCanServiceNewLoan(GameWorld world,Club club)
    {
        if(!world.Economy.OperatingRules.AiCreditRequiresOperatingSurplus)return true;
        var active=world.BankLoans.Where(l=>l.ClubId==club.Id&&!l.IsPaidOff).ToList();
        if(active.Any(l=>l.UnpaidInterest>0))return false;
        var matches=world.Fixtures.Where(m=>ClassicEconomyEngine.Played(m)&&(m.HomeClubId==club.Id||m.AwayClubId==club.Id))
            .OrderByDescending(m=>m.SeasonNumber).ThenByDescending(m=>m.RoundNumber).Take(20).ToList();
        if(matches.Count<4)return false; // No loan-financed rescue without observed operating capacity.
        var first=matches.Last();
        decimal net=world.Transactions.Where(t=>t.ClubId==club.Id&&(t.SeasonNumber>first.SeasonNumber||t.SeasonNumber==first.SeasonNumber&&t.RoundNumber>=first.RoundNumber)
            &&t.Category is FinancialTransactionCategory.TicketRevenue or FinancialTransactionCategory.Sponsorship or FinancialTransactionCategory.PlayerSalary
                or FinancialTransactionCategory.StadiumFixedCost or FinancialTransactionCategory.StadiumVariableCost or FinancialTransactionCategory.StadiumMaintenance
                or FinancialTransactionCategory.ContractRenewal).Sum(t=>t.Amount);
        decimal projectedOperatingNet=net/matches.Count*20;
        decimal interest=active.Sum(l=>l.Mode==BankLoanMode.ClassicFixed?l.InterestPerMatch:
            Math.Round(l.RemainingBalance*l.InterestRatePerRound,2))+world.Economy.Rules.InterestPerMatch;
        return projectedOperatingNet>=active.Sum(l=>l.RemainingBalance)+world.Economy.Rules.LoanPrincipal+interest*20;
    }
    public static decimal AiDebtReserve(GameWorld world,Club club)
        =>world.Economy.OperatingRules.ReserveBorrowedCapitalForAi&&club.HumanCoachId==null
            ?world.BankLoans.Where(l=>l.ClubId==club.Id&&!l.IsPaidOff).Sum(l=>l.RemainingBalance):0;
    public static void ApplyAiCapitalBudget(GameWorld world,Club club)
    {
        decimal debt=AiDebtReserve(world,club);
        if(debt==0||club.LatestSquadPlan is not {} plan)return;
        plan.EmergencyCashReserve+=debt;
        plan.MaxSingleSigningBudget=Math.Min(plan.MaxSingleSigningBudget,Math.Max(0,ClassicEconomyEngine.AvailableCash(world,club)-plan.EmergencyCashReserve));
    }
    public static decimal CommercialBase(DivisionTier division)=>division switch
    {
        DivisionTier.PrimeiraDivisao=>185000m,DivisionTier.SegundaDivisao=>125000m,
        DivisionTier.TerceiraDivisao=>92000m,DivisionTier.QuartaDivisao=>72000m,_=>20000m
    };
    public static (decimal Amount,string Description) SponsorshipForRound(GameWorld world,Club club)
    {
        if(!world.Economy.OperatingRules.AnnualSponsorship)
            return(CommercialBase(club.DivisionTier),"Patrocínio e direitos de transmissão da rodada");
        string key=$"S{world.SeasonNumber}:{club.Id}";
        if(!world.Economy.SponsorshipBudgets.TryGetValue(key,out var terms))
        {
            terms=new(club.Id,world.SeasonNumber,club.DivisionTier,CommercialBase(club.DivisionTier),Math.Max(1,world.TotalRoundsInSeason),world.RoundNumber);
            world.Economy.SponsorshipBudgets[key]=terms;
        }
        int round=world.RoundNumber;
        if(round<1||round>terms.Installments)return(0,"Patrocínio anual: nenhuma parcela adicional fora do calendário");
        decimal amount=Math.Round(terms.AnnualBudget*round/terms.Installments,2)-Math.Round(terms.AnnualBudget*(round-1)/terms.Installments,2);
        return(amount,$"Patrocínio anual R$ {terms.AnnualBudget:N2}: parcela {round}/{terms.Installments}");
    }
    public static double GateDemandFactor(GameWorld world,Club home,Club away)
    {
        double exponent=world.Economy.OperatingRules.GateQualityExponent;
        if(exponent==0)return 1;
        if(!double.IsFinite(exponent)||exponent<0||exponent>3)throw new InvalidOperationException("Invalid gate quality sensitivity.");
        static double Benchmark(DivisionTier division)=>division switch
        {
            DivisionTier.PrimeiraDivisao=>41,DivisionTier.SegundaDivisao=>33,
            DivisionTier.TerceiraDivisao=>26.5,DivisionTier.QuartaDivisao=>21,_=>15.5
        };
        double homeMean=world.Players.Where(p=>p.ClubId==home.Id).Select(p=>(double)p.Strength).DefaultIfEmpty(Benchmark(home.DivisionTier)).Average();
        double awayMean=world.Players.Where(p=>p.ClubId==away.Id).Select(p=>(double)p.Strength).DefaultIfEmpty(Benchmark(away.DivisionTier)).Average();
        double relativeQuality=(homeMean+awayMean)/(Benchmark(home.DivisionTier)+Benchmark(away.DivisionTier));
        return Math.Pow(relativeQuality,exponent);
    }
}
