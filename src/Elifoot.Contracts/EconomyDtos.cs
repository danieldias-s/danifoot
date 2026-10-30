using System.Text.Json.Serialization;
namespace Elifoot.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum BankLoanMode { LegacyInstallment = 0, ClassicFixed = 1 }
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SalaryRequestStatus { PENDING = 0, ACCEPTED = 1, REFUSED = 2 }
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AuctionReason { ContractExpiration = 0, SalaryRenewalRefused = 1, FinancialInsolvency = 2 }

public record FinancialCommandDto(Guid CoachId,string IdempotencyKey,long? ExpectedRoundVersion=null);
public record SalaryDecisionCommandDto(Guid CoachId,bool Accept,string IdempotencyKey,long? ExpectedRoundVersion=null);
public record CreditAssessmentDto(Guid ClubId,DivisionTier Division,decimal AssetIndicator,int LoanLimit,
    int ActiveLoans,int AvailableLoans,decimal OutstandingDebt,decimal InterestPerMatch,string Rule);
