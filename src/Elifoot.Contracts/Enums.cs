using System.Text.Json.Serialization;

namespace Elifoot.Contracts;

public enum ForceEvolutionMode { LegacySeasonal = 0, ClassicAdapted = 1 }

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PlayerPosition
{
    G = 0,
    D = 1,
    M = 2,
    A = 3
}

/// <summary>
/// Ordered strictly from lowest (FairPlay) to highest (Sarrafeiro) probability of fouls and cards.
/// FairPlay never commits intentional fouls and never receives a red card for aggression.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum PlayerBehavior
{
    FairPlay = 0,
    Cordeirinho = 1,
    Cavalheiro = 2,
    Caneleiro = 3,
    Caceteiro = 4,
    Sarrafeiro = 5
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum RoundState
{
    PREPARING = 0,
    ALL_READY = 1,
    RUNNING_FIRST_HALF = 2,
    HALFTIME_PAUSED = 3,
    RUNNING_SECOND_HALF = 4,
    GLOBAL_PAUSED = 5,
    EXTRA_TIME_BREAK = 6,
    RUNNING_EXTRA_TIME = 7,
    EXTRA_TIME_HALFTIME = 8,
    RESULTS = 9,
    FINISHED = 10
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ManagerTacticalIdentity
{
    Ofensivo = 0,
    Equilibrado = 1,
    Defensivo = 2
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DivisionTier
{
    PrimeiraDivisao = 1,
    SegundaDivisao = 2,
    TerceiraDivisao = 3,
    QuartaDivisao = 4,
    Distrital = 5
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CompetitionType
{
    League = 0,
    CupPreliminary1 = 1,
    CupPreliminary2 = 2,
    CupRoundOf64 = 3,
    CupRoundOf32 = 4,
    CupRoundOf16 = 5,
    CupQuarterFinal = 6,
    CupSemiFinal = 7,
    CupFinal = 8,
    CupRoundOf128 = 9,
    CupRoundOf256 = 10,
    CupRoundOf512 = 11,
    CupRoundOf1024 = 12
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MatchEventType
{
    Goal = 0,
    YellowCard = 1,
    SecondYellowRedCard = 2,
    DirectRedCard = 3,
    Injury = 4,
    Substitution = 5,
    TacticChange = 6,
    PenaltyMissed = 7,
    Offside = 8,
    PenaltyShootoutGoal = 9,
    PenaltyShootoutMiss = 10,
    GlobalPauseTriggered = 11,
    HalftimeReached = 12,
    ExtraTimeStarted = 13,
    MatchEnded = 14,
    PenaltyAwarded
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FinancialTransactionCategory
{
    InitialBalance = 0,
    TicketRevenue = 1,
    StadiumFixedCost = 2,
    StadiumVariableCost = 3,
    StadiumMaintenance = 4,
    StadiumRenovation = 5,
    StadiumExpansion = 6,
    PlayerSalary = 7,
    ContractRenewal = 8,
    TransferPurchase = 9,
    TransferSale = 10,
    AuctionPurchase = 11,
    AuctionSale = 12,
    PrizeMoney = 13,
    LoanDisbursement = 14,
    LoanRepayment = 15,
    Sponsorship = 16,
    FreeAgentSigning = 17,
    LoanInterestPayment = 18,
    LoanPrincipalRepayment = 19,
    NegativeCashInterest = 20
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum TransferOfferStatus
{
    Pending = 0,
    Accepted = 1,
    Rejected = 2,
    Countered = 3,
    Cancelled = 4,
    Expired = 5
}

public static class EnumExtensions
{
    public static string ToDisplayPt(this PlayerBehavior behavior) => behavior switch
    {
        PlayerBehavior.FairPlay => "Fair Play",
        PlayerBehavior.Cordeirinho => "Cordeirinho",
        PlayerBehavior.Cavalheiro => "Cavalheiro",
        PlayerBehavior.Caneleiro => "Caneleiro",
        PlayerBehavior.Caceteiro => "Caceteiro",
        PlayerBehavior.Sarrafeiro => "Sarrafeiro",
        _ => behavior.ToString()
    };

    public static string ToDisplayPt(this DivisionTier tier) => tier switch
    {
        DivisionTier.PrimeiraDivisao => "1ª Divisão",
        DivisionTier.SegundaDivisao => "2ª Divisão",
        DivisionTier.TerceiraDivisao => "3ª Divisão",
        DivisionTier.QuartaDivisao => "4ª Divisão",
        DivisionTier.Distrital => "Distrital",
        _ => tier.ToString()
    };

    public static string ToDisplayPt(this CompetitionType comp) => comp switch
    {
        CompetitionType.League => "Campeonato Nacional",
        CompetitionType.CupPreliminary1 => "Taça - Preliminar 1",
        CompetitionType.CupPreliminary2 => "Taça - Preliminar 2",
        CompetitionType.CupRoundOf128 => "Taça - 128 clubes",
        CompetitionType.CupRoundOf256 => "Taça - 256 clubes",
        CompetitionType.CupRoundOf512 => "Taça - 512 clubes",
        CompetitionType.CupRoundOf1024 => "Taça - 1024 clubes",
        CompetitionType.CupRoundOf64 => "Taça - 32-avos de Final (64)",
        CompetitionType.CupRoundOf32 => "Taça - 16-avos de Final (32)",
        CompetitionType.CupRoundOf16 => "Taça - Oitavas de Final",
        CompetitionType.CupQuarterFinal => "Taça - Quartas de Final",
        CompetitionType.CupSemiFinal => "Taça - Semifinal",
        CompetitionType.CupFinal => "Taça - Grande Final",
        _ => comp.ToString()
    };
}
