using Elifoot.Contracts;

namespace Elifoot.Domain;

public partial class GameWorld
{
    public ForceEvolutionMode ForceEvolutionMode { get; set; }
    public ClassicForceState ClassicForce { get; set; } = new();
    public ClassicEconomyState Economy { get; set; } = new();
    public int DisciplineRulesVersion { get; set; }
    public HashSet<Guid> SettledDisciplineMatches { get; set; } = new();
    public List<string> DisciplineCoverageWarnings { get; set; } = new();
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "Campeonato Brasileiro Clássico";
    public string InviteCode { get; set; } = "ELIF98";
    public int Seed { get; set; } = 199801;
    public int MaxHumans { get; set; } = 8;
    public int TotalClubs { get; set; } = 128;
    public int SeasonNumber { get; set; } = 1;
    public int RoundNumber { get; set; } = 1;
    public int TotalRoundsInSeason { get; set; } = 22;
    public bool LeagueFirstSchedule { get; set; }
    public RoundState RoundState { get; set; } = RoundState.PREPARING;
    public long RoundVersion { get; set; } = 1;
    public int CurrentMatchMinute { get; set; } = 0;
    public int TickIntervalMs { get; set; } = 200;
    public int PauseTimeoutSeconds { get; set; } = 60;
    public bool AllowClassic505 { get; set; } = true;
    public Guid? ActivePauseWindowId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;

    public List<Club> Clubs { get; set; } = new();
    public List<Player> Players { get; set; } = new();
    public List<AiManager> AiManagers { get; set; } = new();
    public List<HumanCoach> HumanCoaches { get; set; } = new();
    public List<MatchFixture> Fixtures { get; set; } = new();
    public List<TransferOffer> TransferOffers { get; set; } = new();
    public List<AuctionItem> Auctions { get; set; } = new();
    public List<FinancialTransaction> Transactions { get; set; } = new();
    public List<JobInvitation> JobInvitations { get; set; } = new();
    public List<PauseWindow> PauseWindows { get; set; } = new();
    public List<SeasonHistoryEntry> SeasonHistories { get; set; } = new();
    public List<AuditLogEntry> AuditLogs { get; set; } = new();
    public List<BankLoan> BankLoans { get; set; } = new();
    public int CareerRulesVersion { get; set; }
    public CoachMoraleRules CoachMoraleRules { get; set; } = new();
    public List<CoachTitleRecord> CoachTitles { get; set; } = new();
    public int PlayerStatisticsVersion { get; set; }
    public List<PlayerCompetitionStatistics> PlayerStatistics { get; set; } = new();
    public HashSet<Guid> AppliedPlayerStatisticsMatches { get; set; } = new();
    public List<string> PlayerStatisticsCoverageWarnings { get; set; } = new();
    public List<SeasonTopScorerAward> SeasonTopScorerAwards { get; set; } = new();
    public HashSet<int> ClosedPlayerAwardSeasons { get; set; } = new();
    public List<PlayerStrengthChangeDto> PlayerStrengthHistory { get; set; } = new();
    public HashSet<int> EvolvedPlayerStrengthSeasons { get; set; } = new();
    public int? PlayerStrengthHistoryStartSeason { get; set; }
    public int? PlayerStrengthHistoryStartRound { get; set; }
    [System.Text.Json.Serialization.JsonIgnore]
    public Dictionary<string, PlayerCompetitionStatistics> PlayerStatisticsIndex { get; } = new();

    // The round guard must survive snapshot reloads. The transaction-key cache below is rebuilt from the ledger.
    public HashSet<string> SettledFinancialRounds { get; set; } = new();

    [System.Text.Json.Serialization.JsonIgnore]
    public HashSet<string> AppliedTransactionKeys { get; } = new();
}

public partial class Club
{
    public int? ClubInitialPower { get; set; }
    public InitialSquadReference017B? InitialSquadReference { get; set; }
    public int? InitialSquadSize { get; set; }
    public ClubColorPalette? ColorPalette { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameWorldId { get; set; }
    public int DeterministicIndex { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public string Country { get; set; } = "Brasil";
    public string PrimaryColorHex { get; set; } = "#006400";
    public string SecondaryColorHex { get; set; } = "#FFFFFF";
    public DivisionTier DivisionTier { get; set; } = DivisionTier.PrimeiraDivisao;
    public decimal CashBalance { get; set; } = 2_500_000m;
    public int Reputation { get; set; } = 50;

    public Guid? HumanCoachId { get; set; }
    public Guid? AiManagerId { get; set; }

    public string PreferredFormation { get; set; } = "4-4-2";
    public LineupSelectionMode LineupSelectionMode { get; set; } = LineupSelectionMode.Automatic;
    public List<Guid> PreferredStarterIds { get; set; } = new();
    public List<Guid> PreferredBenchIds { get; set; } = new();

    // League table tracking for current season
    public int LeaguePlayed { get; set; }
    public int LeagueWins { get; set; }
    public int LeagueDraws { get; set; }
    public int LeagueLosses { get; set; }
    public int LeagueGoalsFor { get; set; }
    public int LeagueGoalsAgainst { get; set; }
    public int LeaguePoints => LeagueWins * 3 + LeagueDraws;
    public int LeagueGoalDifference => LeagueGoalsFor - LeagueGoalsAgainst;
    public int ConsecutiveWinlessRounds { get; set; }

    public Stadium Stadium { get; set; } = new();
    public ManagerSquadPlan? LatestSquadPlan { get; set; }
    public Guid? CareerVacancyId { get; set; }
    public bool HasProvisionalManager { get; set; }
    public int LastVacancySearchOrdinal { get; set; } = -1;
    public int CareerVacancySequence { get; set; }
}

public class Stadium
{
    public string Name { get; set; } = "Estádio Municipal";
    public int BuiltCapacity { get; set; } = 20_000;
    public int UsableCapacity { get; set; } = 20_000;
    public double ConditionPercent { get; set; } = 95.0;
    public decimal MaintenanceReservePerSeason { get; set; } = 120_000m;
    public decimal FixedMatchCostRate { get; set; } = 0.70m;
    public decimal VariableAttendeeCostRate { get; set; } = 1.10m;
    public decimal LastTicketPrice { get; set; } = 20m;
    public int LastAttendance { get; set; } = 10_000;
    public int ExpansionTargetSeats { get; set; } = 0;
    public int ExpansionRoundsRemaining { get; set; } = 0;
    public int RenovationRoundsRemaining { get; set; } = 0;
    public bool ExpansionProgressesEachRound { get; set; }
    public bool RenovationProgressesEachRound { get; set; }
}

public partial class Player
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameWorldId { get; set; }
    public Guid? ClubId { get; set; }
    public string Name { get; set; } = string.Empty;
    public PlayerPosition Position { get; set; } = PlayerPosition.M;
    public int Strength { get; set; } = 25; // 1..50
    public PlayerBehavior Behavior { get; set; } = PlayerBehavior.Cavalheiro;
    public bool IsStar { get; set; } // Literal '*' in compact tables
    public int ContractRoundsRemaining { get; set; } = 20;
    public decimal SalaryPerRound { get; set; } = 8_000m;
    public decimal ReferenceSalaryPerRound => MarketAndEconomyEngine.CalculateFairSalaryPerRound(this);
    public bool NegotiatedContractProtected { get; set; }
    public int? RenewedForSeason { get; set; }
    public decimal MarketPrice { get; set; } = 250_000m;
    public bool IsListedForTransfer { get; set; }
    public decimal? AskingPrice { get; set; }
    public int ListedAtRound { get; set; }
    public long TransferListingVersion { get; set; }

    public int InjuryRoundsRemaining { get; set; }
    public int InjuryDaysRemaining { get; set; }
    public List<ForceInjury> ForceInjuries { get; set; } = new();
    public int SuspensionRoundsRemaining { get; set; }
    public int YellowCardsAccumulated { get; set; }
    public int DisciplineVersion { get; set; }
    public int TotalYellowCards { get; set; }
    public int YellowCardsInCurrentCycle { get => YellowCardsAccumulated; set => YellowCardsAccumulated=value; }
    public int TotalDirectRedCards { get; set; }
    public int TotalSecondYellowDismissals { get; set; }
    public int UnclassifiedHistoricalRedCards { get; set; }
    public int SuspensionMatchesRemaining { get => SuspensionRoundsRemaining; set => SuspensionRoundsRemaining=value; }
    public string? SuspensionReason { get; set; }
    public List<PlayerCardHistoryDto> CardHistory { get; set; } = new();
    public List<PlayerSuspensionHistoryDto> SuspensionHistory { get; set; } = new();

    public int SeasonGoals { get; set; }
    public int CupGoals { get; set; }
    public int SeasonMatches { get; set; }
    public int SeasonYellows { get; set; }
    public int SeasonReds { get; set; }
    public int CareerGoals { get; set; }
    public int CareerMatches { get; set; }
    public int SeasonTopScorerTitles { get; set; }

    public string? PhotoUrl { get; set; }
    public string? PhotoLicense { get; set; }
    public string? PhotoCredit { get; set; }

    public string DisplayName => IsStar ? $"{Name}*" : Name;
    public bool IsAvailableForMatch => InjuryRoundsRemaining <= 0 && InjuryDaysRemaining <= 0 && SuspensionRoundsRemaining <= 0;
}

public partial class AiManager
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameWorldId { get; set; }
    public Guid? CurrentClubId { get; set; }
    public string Name { get; set; } = string.Empty;
    public ManagerTacticalIdentity Identity { get; set; } = ManagerTacticalIdentity.Equilibrado;
    public int Pragmatism { get; set; } = 50; // 0..100
    public int Adaptability { get; set; } = 50; // 0..100
    public int FinancialAggressiveness { get; set; } = 50; // 0..100
    public int YouthPatience { get; set; } = 50; // 0..100
    public List<FormationPreferenceWeightDto> PreferredFormations { get; set; } = new();

    // Career stats
    public int LeagueTitles { get; set; }
    public int CupTitles { get; set; }
    public decimal CoachPoints { get; set; }
    public int TotalWins { get; set; }
    public int TotalDraws { get; set; }
    public int TotalLosses { get; set; }
    public int SeasonsManaged { get; set; }
    public int Dismissals { get; set; }
    public string LastDecisionLog { get; set; } = string.Empty;
    public CoachCareerState Career { get; set; } = new();
    public List<CoachEmploymentRecord> EmploymentHistory { get; set; } = new();
}

public class HumanCoach
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameWorldId { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public string DeviceId { get; set; } = string.Empty;
    public string SessionToken { get; set; } = string.Empty;
    public bool IsHost { get; set; }
    public bool IsConnected { get; set; } = true;
    public bool IsAutoMode { get; set; }
    public Guid? CurrentClubId { get; set; }
    public bool IsUnemployed => CurrentClubId == null;
    public List<Guid> ObservedPlayerIds { get; set; } = new();

    // Ready state for current round preparation
    public bool IsReadyForRound { get; set; }
    public string? SubmittedFormation { get; set; }
    public List<Guid> SubmittedStarterIds { get; set; } = new();
    public List<Guid> SubmittedBenchIds { get; set; } = new();
    public string? LastIdempotencyKey { get; set; }

    // Career stats & classic ranking
    public int LeagueTitles { get; set; }
    public int CupTitles { get; set; }
    public decimal CoachPoints { get; set; }
    public int TotalWins { get; set; }
    public int TotalDraws { get; set; }
    public int TotalLosses { get; set; }
    public int SeasonsManaged { get; set; }
    public int Dismissals { get; set; }
    public List<CoachEmploymentRecord> EmploymentHistory { get; set; } = new();
    public CoachCareerState Career { get; set; } = new();
}

public class CoachEmploymentRecord
{
    public Guid ClubId { get; set; }
    public string ClubName { get; set; } = string.Empty;
    public int StartSeason { get; set; }
    public int StartRound { get; set; }
    public int? EndSeason { get; set; }
    public int? EndRound { get; set; }
    public string? ReasonForLeaving { get; set; }
}

public class ManagerSquadPlan
{
    public Guid ManagerId { get; set; }
    public string ManagerName { get; set; } = string.Empty;
    public ManagerTacticalIdentity Identity { get; set; }
    public string TargetPrimaryFormation { get; set; } = "4-4-2";
    public string SelectedMatchFormation { get; set; } = "4-4-2";
    public string SelectionReasoning { get; set; } = string.Empty;
    public Dictionary<PlayerPosition, int> TargetStarters { get; set; } = new();
    public Dictionary<PlayerPosition, int> TargetReserves { get; set; } = new();
    public List<PlayerPosition> PriorityGapPositions { get; set; } = new();
    public List<string> PriorityGapDescriptions { get; set; } = new();
    public decimal MaxSingleSigningBudget { get; set; }
    public decimal EmergencyCashReserve { get; set; }
    public List<Guid> SurplusPlayerIds { get; set; } = new();
    public int EvaluatedAtRound { get; set; }
}

public class MatchFixture
{
    public int ForceRulesVersion { get; set; }
    public DateOnly? GameDate { get; set; }
    public List<ForceRosterPlayer> ForceRoster { get; set; } = new();
    public bool StatisticsLineupCaptured { get; set; }
    public List<PlayerLineupSnapshot> StatisticsInitialLineup { get; set; } = new();
    public bool CareerLeadershipCaptured { get; set; }
    public Guid? HomeCoachAtKickoffId { get; set; }
    public Guid? AwayCoachAtKickoffId { get; set; }
    public int HomeMoraleAtKickoff { get; set; }
    public int AwayMoraleAtKickoff { get; set; }
    public bool CareerResultsApplied { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameWorldId { get; set; }
    public int SeasonNumber { get; set; }
    public int RoundNumber { get; set; }
    public int DeterministicMatchIndex { get; set; }
    public CompetitionType CompetitionType { get; set; } = CompetitionType.League;
    public DivisionTier? DivisionTier { get; set; }
    public Guid HomeClubId { get; set; }
    public Guid AwayClubId { get; set; }

    public int HomeGoals { get; set; }
    public int AwayGoals { get; set; }
    public int HomeFirstHalfGoals { get; set; }
    public int AwayFirstHalfGoals { get; set; }
    public bool HomeLedDuringMatch { get; set; }
    public bool AwayLedDuringMatch { get; set; }
    public int? HomePenaltyGoals { get; set; }
    public int? AwayPenaltyGoals { get; set; }
    public int HomeShootoutAttempts { get; set; }
    public int AwayShootoutAttempts { get; set; }
    public int HomeFouls { get; set; }
    public int AwayFouls { get; set; }
    public int CurrentMinute { get; set; }
    public bool IsFinished { get; set; }
    public bool IsCancelled { get; set; }
    public bool IsPostponed { get; set; }
    public int LastSimulatedMinute { get; set; }
    public List<PendingPenaltyKick> PendingPenalties { get; set; } = new();
    public MatchDisciplineState Discipline { get; set; } = new();
    public HashSet<Guid> HomeInjuryVacancies { get; set; } = new();
    public HashSet<Guid> AwayInjuryVacancies { get; set; } = new();
    public bool RequiresKnockoutResolution { get; set; }
    public bool EnteredExtraTime { get; set; }
    public Guid? WinnerClubId { get; set; }

    // Live match state
    public string HomeFormation { get; set; } = "4-4-2";
    public string AwayFormation { get; set; } = "4-4-2";
    public List<Guid> HomeActiveStarterIds { get; set; } = new();
    public List<Guid> HomeBenchIds { get; set; } = new();
    public List<Guid> HomeSubstitutedOutIds { get; set; } = new();
    public List<Guid> HomeSubstitutedInIds { get; set; } = new();
    public List<Guid> HomeSentOffIds { get; set; } = new();
    public List<Guid> HomeInjuredInMatchIds { get; set; } = new();
    public List<Guid> HomeSuspendedByYellowInMatchIds { get; set; } = new();
    public Dictionary<Guid, int> HomeYellowCardsInMatch { get; set; } = new();

    public List<Guid> AwayActiveStarterIds { get; set; } = new();
    public List<Guid> AwayBenchIds { get; set; } = new();
    public List<Guid> AwaySubstitutedOutIds { get; set; } = new();
    public List<Guid> AwaySubstitutedInIds { get; set; } = new();
    public List<Guid> AwaySentOffIds { get; set; } = new();
    public List<Guid> AwayInjuredInMatchIds { get; set; } = new();
    public List<Guid> AwaySuspendedByYellowInMatchIds { get; set; } = new();
    public Dictionary<Guid, int> AwayYellowCardsInMatch { get; set; } = new();

    // Quotas: 5 subs / 3 windows in 90 min; +1 sub (6) / +1 window (4) in Extra Time
    public int HomeUsedSubstitutions { get; set; }
    public int HomeMaxSubstitutions => EnteredExtraTime ? 6 : 5;
    public int HomeUsedPauseWindows { get; set; }
    public int HomeMaxPauseWindows => EnteredExtraTime ? 4 : 3;

    public int AwayUsedSubstitutions { get; set; }
    public int AwayMaxSubstitutions => EnteredExtraTime ? 6 : 5;
    public int AwayUsedPauseWindows { get; set; }
    public int AwayMaxPauseWindows => EnteredExtraTime ? 4 : 3;

    // Track which PauseWindowIds each side has already spent an opportunity on
    public HashSet<Guid> HomeConsumedPauseWindowIds { get; set; } = new();
    public HashSet<Guid> AwayConsumedPauseWindowIds { get; set; } = new();

    // Stadium gate for this match
    public int Attendance { get; set; }
    public decimal TicketPrice { get; set; }
    public decimal TicketRevenue { get; set; }
    public decimal StadiumOperatingCost { get; set; }
    public bool FinancialsSettled { get; set; }

    public List<MatchEventRecord> Events { get; set; } = new();
}

public class MatchEventRecord
{
    public int? DisciplinarySuspensionMatches { get; set; }
    public bool IsOwnGoal { get; set; }
    public int Minute { get; set; }
    public MatchEventType EventType { get; set; }
    public Guid ClubId { get; set; }
    public string ClubName { get; set; } = string.Empty;
    public Guid? PrimaryPlayerId { get; set; }
    public string? PrimaryPlayerDisplayName { get; set; }
    public Guid? SecondaryPlayerId { get; set; }
    public string? SecondaryPlayerDisplayName { get; set; }
    public Guid? AssistPlayerId { get; set; }
    public string? AssistPlayerDisplayName { get; set; }
    public bool HasAssistCoverage { get; set; }
    public string Description { get; set; } = string.Empty;
}

public class PauseWindow
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameWorldId { get; set; }
    public int SeasonNumber { get; set; }
    public int RoundNumber { get; set; }
    public int Minute { get; set; }
    public RoundState PauseType { get; set; } = RoundState.GLOBAL_PAUSED;
    public Guid? RequestedByCoachId { get; set; }
    public string? RequestedByCoachName { get; set; }
    public DateTime OpenedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAtUtc { get; set; } = DateTime.UtcNow.AddSeconds(60);
    public bool IsClosed { get; set; }
    public HashSet<Guid> ConfirmedCoachIds { get; set; } = new();
    public HashSet<Guid> EditedCoachIds { get; set; } = new();
}

public class TransferOffer
{
    public string? SalaryTermsToken { get; set; }
    public Guid? SubmittedByManagerId { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameWorldId { get; set; }
    public Guid PlayerId { get; set; }
    public Guid BuyerClubId { get; set; }
    public Guid SellerClubId { get; set; }
    public decimal OfferedAmount { get; set; }
    public decimal? CounterAmount { get; set; }
    public TransferOfferStatus Status { get; set; } = TransferOfferStatus.Pending;
    public string Justification { get; set; } = string.Empty;
    public int CreatedAtSeason { get; set; }
    public int CreatedAtRound { get; set; }
    public string IdempotencyKey { get; set; } = string.Empty;
}

public class AuctionItem
{
    public AuctionReason Reason { get; set; }
    public string? ClosureReason { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameWorldId { get; set; }
    public Guid PlayerId { get; set; }
    public Guid? FormerClubId { get; set; }
    public decimal MinimumBid { get; set; }
    public decimal CurrentBid { get; set; }
    public Guid? HighestBidderClubId { get; set; }
    public int CreatedAtSeason { get; set; }
    public int CreatedAtRound { get; set; }
    public int ExpiresAtRound { get; set; }
    // Optional: old automatic auctions retain their original deadline semantics.
    public int? DurationRounds { get; set; }
    public int? ExpiresAtSeason { get; set; }
    public bool IsClosed { get; set; }
    public Dictionary<Guid, decimal> PrivateAiMaxBids { get; set; } = new();
}

public class FinancialTransaction
{
    // Transfer metadata is captured at execution, never inferred from the club's current manager.
    public Guid? TransferTransactionId { get; set; }
    public Guid? PlayerId { get; set; }
    public Guid? BuyerClubId { get; set; }
    public Guid? SellerClubId { get; set; }
    public Guid? ManagerId { get; set; }
    public string? TransactionType { get; set; }
    public AiSigningDecision? SigningDecision { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameWorldId { get; set; }
    public Guid ClubId { get; set; }
    public int SeasonNumber { get; set; }
    public int RoundNumber { get; set; }
    public FinancialTransactionCategory Category { get; set; }
    public decimal Amount { get; set; } // Positive = credit, Negative = debit
    public decimal BalanceAfter { get; set; }
    public string Description { get; set; } = string.Empty;
    public string IdempotencyKey { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class BankLoan
{
    public BankLoanMode Mode { get; set; }
    public decimal InterestPerMatch { get; set; }
    public decimal UnpaidInterest { get; set; }
    public decimal InterestCharged { get; set; }
    public decimal InterestPaid { get; set; }
    public decimal PrincipalPaid { get; set; }
    public int CreatedSeason { get; set; }
    public int CreatedRound { get; set; }
    public DateOnly? CreatedVirtualDate { get; set; }
    public int FirstEligibleOrdinal { get; set; }
    public HashSet<Guid> ExcludedCompletedMatches { get; set; } = new();
    public HashSet<Guid> ChargedMatches { get; set; } = new();
    public List<BankLoanEntry> History { get; set; } = new();
    public decimal? LegacyOpeningBalance { get; set; }
    public decimal? OutstandingPrincipal => Mode==BankLoanMode.ClassicFixed?Math.Max(0m,RemainingBalance-UnpaidInterest):null;
    public string Status => IsPaidOff?"PAID":Mode==BankLoanMode.LegacyInstallment&&RemainingRounds<=0?"OVERDUE":"ACTIVE";
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameWorldId { get; set; }
    public Guid ClubId { get; set; }
    public decimal PrincipalAmount { get; set; }
    public decimal RemainingBalance { get; set; }
    public decimal InterestRatePerRound { get; set; } = 0.015m;
    public decimal InstallmentPerRound { get; set; }
    public int RemainingRounds { get; set; }
    public bool IsPaidOff => RemainingBalance <= 0m;
}

public class JobInvitation
{
    public bool IsRejected { get; set; }
    public bool IsAiCandidate { get; set; }
    public Guid VacancyId { get; set; }
    public string Explanation { get; set; } = string.Empty;
    public decimal CandidateScore { get; set; }
    public decimal ProjectImprovement { get; set; }
    public int ExpiresAtOrdinal { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameWorldId { get; set; }
    public Guid CoachId { get; set; }
    public Guid ClubId { get; set; }
    public int CreatedAtSeason { get; set; }
    public int CreatedAtRound { get; set; }
    public bool IsAccepted { get; set; }
    public bool IsExpired { get; set; }
}

public class CoachMoraleRules
{
    public int GreenMinimum { get; set; } = 60;
    public int YellowMinimum { get; set; } = 30;
    public int UnemployedRecoveryPerRound { get; set; } = 6;
    public string Band(int value) => value >= GreenMinimum ? "VERDE" : value >= YellowMinimum ? "AMARELA" : "VERMELHA";
}

public class CoachCareerState
{
    public int Morale { get; set; } = 70;
    public int WinlessMatches { get; set; }
    public int UnemployedSinceOrdinal { get; set; } = -1;
    public int LastRecoveryOrdinal { get; set; } = -1;
    public int LastMoveOrdinal { get; set; } = -100;
    public List<int> RecentResults { get; set; } = new();
    public List<CoachMoraleEntry> MoraleHistory { get; set; } = new();
}

public record CoachMoraleEntry(int Season, int Round, Guid? MatchId, int Before, int After, string Reason);
public record CoachTitleRecord(int Season, DivisionTier? Division, Guid ClubId, Guid CoachId, string TitleType);

public class SeasonHistoryEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameWorldId { get; set; }
    public int SeasonNumber { get; set; }
    public Guid FirstDivisionChampionClubId { get; set; }
    public string FirstDivisionChampionName { get; set; } = string.Empty;
    public Guid CupChampionClubId { get; set; }
    public string CupChampionName { get; set; } = string.Empty;
    public Guid TopScorerPlayerId { get; set; }
    public string TopScorerPlayerName { get; set; } = string.Empty;
    public int TopScorerGoals { get; set; }
    public List<Guid> PromotedClubIds { get; set; } = new();
    public List<Guid> RelegatedClubIds { get; set; } = new();
}

public class AuditLogEntry
{
    public Guid? ClubId { get; set; }
    public Guid? PlayerId { get; set; }
    public Guid? RelatedEntityId { get; set; }
    public decimal? Amount { get; set; }
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid GameWorldId { get; set; }
    public int SeasonNumber { get; set; }
    public int RoundNumber { get; set; }
    public string ActionType { get; set; } = string.Empty;
    public Guid? ActorCoachId { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}

public record AiSigningDecision(string Reason, string Formation, PlayerPosition Position,
    int Strength, int? StarterStrength, int? ReserveStrength, decimal Amount, decimal MarketValue,
    decimal SalaryPerRound, PlayerBehavior Behavior, bool IsStar, decimal CashBefore, decimal CashAfter);
