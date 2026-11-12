using Elifoot.Contracts;
using System.Text.Json.Serialization;
namespace Elifoot.Domain;

public class ClassicEconomyState
{
    public int Version { get; set; }
    public ClassicEconomyRules Rules { get; set; } = new();
    public ClassicPlayerEconomyRules PlayerEconomyRules { get; set; } = new();
    public double InflationIndex { get; set; } = ClassicPlayerEconomyEngine.InitialInflationIndex;
    [JsonIgnore] public int InflationDisplayIndex => ClassicPlayerEconomyEngine.CalculateInflationDisplay(InflationIndex);
    public int EconomyYearCounter { get; set; } = ClassicPlayerEconomyEngine.InitialEconomyYearCounter;
    public List<InflationHistoryEntry> InflationHistory { get; set; } = new();
    public Dictionary<Guid,double> ClubFactorsR { get; set; } = new();
    public OperatingEconomyRules OperatingRules { get; set; } = new();
    public int? OperatingUpgradeSeason { get; set; }
    public Dictionary<string,SponsorSeasonBudget> SponsorshipBudgets { get; set; } = new();
    public Dictionary<string,FinancialCommandReceipt> Commands { get; set; } = new();
    public List<SalaryRaiseRequest> SalaryRequests { get; set; } = new();
    public Dictionary<Guid,SalaryEligibility> SalaryEligibility { get; set; } = new();
    public List<PlayerContractRecord> ContractHistory { get; set; } = new();
    public Dictionary<Guid,CreditAssessmentDto> CreditAssessments { get; set; } = new();
    public Dictionary<Guid,ClubFinancialCrisis> Crises { get; set; } = new();
    public List<FinancialCrisisRecord> CrisisHistory { get; set; } = new();
    public List<SpecialAuctionResult> AuctionHistory { get; set; } = new();
    public HashSet<string> SalaryRequestRounds { get; set; } = new();
    public HashSet<string> SalaryRequestCheckedRounds { get; set; } = new();
    public HashSet<string> AiFinancialRounds { get; set; } = new();
    public int StrengthHistoryCursor { get; set; }
    public bool SeasonalDebtRulesInitialized { get; set; }
    public HashSet<string> OverdraftInterestRounds { get; set; } = new();
    public List<string> CoverageWarnings { get; set; } = new();
    [JsonIgnore] public HashSet<string> ContractKeys { get; } = new();
    [JsonIgnore] public int PendingCacheCount { get; set; } = -1;
    [JsonIgnore] public bool SalaryRecoveryNeedsSave { get; set; }
    [JsonIgnore] public Dictionary<Guid,List<SalaryRaiseRequest>> PendingByPlayer { get; set; } = new();
}
public class ClassicEconomyRules
{
    public bool SeasonContractsEnabled { get; set; } = true;
    public decimal NegativeCashInterestPerRound { get; set; } = .02m;
    public decimal LoanPrincipal { get; set; } = 500_000m;
    public decimal InterestPerMatch { get; set; } = 25_000m;
    public decimal StadiumAssetPerUsableSeat { get; set; } = 50m;
    public decimal MaximumSalaryRaiseFraction { get; set; } = .10m;
    public int RenewalMatches { get; set; } = 20;
    public int AuctionDurationRounds { get; set; } = 2;
    public int InsolvencyAuctionCooldownRounds { get; set; } = 2;
    public Dictionary<DivisionTier,List<decimal>> ExtraCreditThresholds { get; set; } = new()
    {
        [DivisionTier.QuartaDivisao]=[400_000m], [DivisionTier.TerceiraDivisao]=[500_000m,750_000m],
        [DivisionTier.SegundaDivisao]=[1_000_000m,1_250_000m], [DivisionTier.PrimeiraDivisao]=[1_600_000m,1_900_000m,2_200_000m],
        [DivisionTier.Distrital]=[]
    };
}
public record FinancialCommandReceipt(string Operation,string Payload,Guid? EntityId,string? Message=null,string? ResultJson=null);
public class SalaryRaiseRequest
{
    public decimal DifferencePerMatch => RequestedSalary-CurrentSalary;
    public decimal TotalSalaryCommitment => RequestedSalary*RenewalMatches;
    public Guid Id { get; set; }
    public Guid WorldId { get; set; }
    public Guid ClubId { get; set; }
    public Guid PlayerId { get; set; }
    public int Season { get; set; }
    public int Round { get; set; }
    public decimal CurrentSalary { get; set; }
    public decimal RequestedSalary { get; set; }
    public decimal ReferenceSalary { get; set; }
    public int RenewalMatches { get; set; }
    public string Reason { get; set; } = "";
    public string TriggerKey { get; set; } = "";
    public SalaryRequestStatus Status { get; set; }
    [JsonIgnore] public bool IsUnresolved => Status is SalaryRequestStatus.PENDING or SalaryRequestStatus.QUEUED;
    public string? Resolution { get; set; }
    public Guid? AuctionId { get; set; }
    public bool Mandatory { get; set; }
    public DateTime? CreatedAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }
    public string? ResolvedBy { get; set; }
    public Guid? ResolvedByAccountId { get; set; }
    public int DecisionVersion { get; set; }
}
public record SalaryEligibility(string Key,string Reason,int StrengthBefore,int StrengthAfter,Guid ClubId,int FromSeason=0);
public record PlayerContractRecord(string Key,Guid PlayerId,Guid? ClubId,int Season,int Round,
    decimal PreviousSalary,decimal Salary,int Matches,string Reason,decimal SigningFee,decimal Commitment);
public class ClubFinancialCrisis
{
    public bool IsInsolvent { get; set; }
    public int LastAuctionOrdinal { get; set; } = -100;
    public int FirstCrisisSeason { get; set; }
}
public record FinancialCrisisRecord(string Key,Guid ClubId,int Season,int Round,string Kind,
    decimal Cash,decimal UnpaidInterest,decimal BankDebt,Guid? PlayerId=null,Guid? AuctionId=null);
public record BankLoanEntry(string Key,int Season,int Round,Guid? MatchId,string Kind,
    decimal InterestCharged,decimal InterestPaid,decimal PrincipalPaid,decimal CashChange,decimal DebtBefore,decimal DebtAfter);
public record SpecialAuctionResult(Guid AuctionId,Guid PlayerId,AuctionReason Reason,Guid? FormerClubId,
    Guid? BuyerClubId,int Season,int Round,decimal Price,string Outcome);

public class ClassicPlayerEconomyRules
{
    public string Policy { get; set; } = ClassicPlayerEconomyEngine.CalibratedMultiplayerPolicy;
    public int CurrencyFactorC { get; set; } = ClassicPlayerEconomyEngine.DefaultCurrencyFactorC;
    public bool UseClassicalSalaryFormula { get; set; }
    public bool UseClassicalMarketValuation { get; set; }
    public bool UseClassicalNationalityPricing { get; set; }
    public bool UseClassicalAiHiringInterest { get; set; }
    public bool UseSeasonalInflationEvolution { get; set; } = true;
}
