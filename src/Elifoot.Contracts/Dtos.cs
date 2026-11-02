namespace Elifoot.Contracts;

public record LocalAuthRequest(string Nickname, string? DeviceId);

public record LocalAuthResponse(
    Guid CoachId,
    string Nickname,
    string DeviceId,
    string SessionToken,
    string AuthModeWarning);

public record CreateWorldRequest(
    string WorldName,
    int MaxHumans = 8,
    int TotalClubs = 128,
    int Seed = 199801,
    int TickIntervalMs = 250,
    int PauseTimeoutSeconds = 60,
    bool AllowClassic505 = true,
    string? InviteCode = null,
    ForceEvolutionMode ForceEvolutionMode = ForceEvolutionMode.ClassicAdapted);

public record JoinWorldRequest(
    string InviteCodeOrWorldId,
    Guid? PreferredClubId = null);

public record SubmitTacticsRequest(
    Guid GameWorldId,
    Guid CoachId,
    string Formation,
    List<Guid> StarterPlayerIds,
    List<Guid> BenchPlayerIds,
    string IdempotencyKey,
    long ExpectedRoundVersion,
    bool SaveAsPreferred = true);

public record UnemployedReadyRequest(
    Guid GameWorldId,
    Guid CoachId,
    string IdempotencyKey,
    long ExpectedRoundVersion);

public record WithdrawReadyRequest(
    Guid GameWorldId,
    Guid CoachId,
    string IdempotencyKey,
    long ExpectedRoundVersion);

public record RequestPauseCommand(
    Guid GameWorldId,
    Guid CoachId,
    string IdempotencyKey);

public record SubmitInMatchAdjustmentCommand(
    Guid GameWorldId,
    Guid CoachId,
    Guid MatchId,
    string? NewFormation,
    List<InMatchSubstitutionDto> Substitutions,
    string IdempotencyKey);

public record InMatchSubstitutionDto(Guid PlayerOutId, Guid PlayerInId);

public record ChoosePenaltyTakerCommand(Guid GameWorldId, Guid CoachId, Guid DecisionId, Guid PlayerId, string IdempotencyKey);
public record PenaltyTakerChoiceDto(Guid PlayerId, string DisplayName, PlayerPosition Position, int Strength, bool IsStar);
public record PendingPenaltyDecisionDto(Guid DecisionId, Guid MatchId, Guid CoachId, string CoachName,
    Guid ClubId, string ClubName, int Minute, List<PenaltyTakerChoiceDto> EligiblePlayers);

public record ConfirmContinueMatchCommand(
    Guid GameWorldId,
    Guid CoachId,
    string IdempotencyKey);

public record ReadyCounterDto(
    Guid GameWorldId,
    int SeasonNumber,
    int RoundNumber,
    RoundState RoundState,
    long RoundVersion,
    int ReadyCount,
    int TotalHumans,
    string CounterLabel,
    List<ParticipantReadyItemDto> Participants);

public record ParticipantReadyItemDto(
    Guid CoachId,
    string Nickname,
    Guid? ClubId,
    string? ClubName,
    bool IsUnemployed,
    bool IsReady,
    bool IsConnected,
    bool IsAutoMode,
    int UsedSubstitutions,
    int MaxSubstitutions,
    int UsedPauseWindows,
    int MaxPauseWindows,
    int Morale = 70,
    string MoraleBand = "VERDE");

public record PauseWindowStateDto(
    Guid PauseWindowId,
    Guid GameWorldId,
    int Minute,
    RoundState PauseType,
    Guid? RequestedByCoachId,
    string? RequestedByCoachName,
    int ConfirmedCount,
    int TotalHumans,
    int RemainingSeconds,
    List<Guid> ConfirmedCoachIds,
    List<Guid> EditedCoachIds);

public record LiveRoundSnapshotDto(
    Guid GameWorldId,
    string WorldName,
    int SeasonNumber,
    int RoundNumber,
    CompetitionType CompetitionType,
    string CompetitionLabel,
    RoundState RoundState,
    long RoundVersion,
    int CurrentMinute,
    ReadyCounterDto ReadyCounter,
    PauseWindowStateDto? ActivePauseWindow,
    List<LiveMatchSummaryDto> Matches)
{
    public bool HasInitialLobby { get; init; }
    public bool LobbyStarted { get; init; }
    public string GameModeId { get; init; } = "CLASSIC_SYNC";
    public PendingPenaltyDecisionDto? PendingPenalty { get; init; }
    public int TotalRoundsInSeason { get; init; }
    public ForceEvolutionMode ForceEvolutionMode { get; init; }
    public int ForceRulesVersion { get; init; }
    public int DisciplineRulesVersion { get; init; }
    public DateOnly? GameDate { get; init; }
}

public record LiveMatchSummaryDto(
    Guid MatchId,
    CompetitionType CompetitionType,
    DivisionTier? DivisionTier,
    Guid HomeClubId,
    string HomeClubName,
    string HomePrimaryColor,
    string HomeSecondaryColor,
    Guid AwayClubId,
    string AwayClubName,
    string AwayPrimaryColor,
    string AwaySecondaryColor,
    int HomeGoals,
    int AwayGoals,
    int? HomePenaltyGoals,
    int? AwayPenaltyGoals,
    int Minute,
    bool IsFinished,
    bool IsExtraTime,
    string HomeFormation,
    string AwayFormation,
    int HomeSubCount,
    int HomeMaxSubs,
    int HomePauseWindowsUsed,
    int HomeMaxPauseWindows,
    int AwaySubCount,
    int AwayMaxSubs,
    int AwayPauseWindowsUsed,
    int AwayMaxPauseWindows,
    int Attendance,
    decimal TicketPrice,
    decimal TicketRevenue,
    List<MatchEventDto> Events,
    List<MatchLineupPlayerDto> HomeStarters,
    List<MatchLineupPlayerDto> HomeBench,
    List<MatchLineupPlayerDto> AwayStarters,
    List<MatchLineupPlayerDto> AwayBench)
{
    public ClubColorPalette? HomeColorPalette { get; init; }
    public ClubColorPalette? AwayColorPalette { get; init; }
    public int HomePlayersOnPitch { get; init; }
    public int AwayPlayersOnPitch { get; init; }
}

public record MatchLineupPlayerDto(
    Guid PlayerId,
    string DisplayName,
    PlayerPosition Position,
    int Strength,
    string BehaviorDisplay,
    bool IsStar,
    int YellowCardsInMatch,
    bool IsSentOff,
    bool IsInjuredInMatch,
    bool WasSubstitutedOut,
    bool WasSubstitutedIn)
{
    public bool IsOnPitch { get; init; }
}

public record MatchEventDto(
    int Minute,
    MatchEventType EventType,
    Guid ClubId,
    string ClubName,
    Guid? PrimaryPlayerId,
    string? PrimaryPlayerDisplayName,
    Guid? SecondaryPlayerId,
    string? SecondaryPlayerDisplayName,
    string Description);

public record PlayerDto(
    Guid Id,
    Guid? ClubId,
    string? ClubName,
    string Name,
    string DisplayName,
    PlayerPosition Position,
    int Strength,
    PlayerBehavior Behavior,
    string BehaviorDisplay,
    bool IsStar,
    int ContractRoundsRemaining,
    decimal SalaryPerRound,
    decimal MarketPrice,
    decimal FairValue,
    bool IsListedForTransfer,
    decimal? AskingPrice,
    int InjuryRoundsRemaining,
    int SuspensionRoundsRemaining,
    int YellowCardsAccumulated,
    int SeasonGoals,
    int SeasonMatches,
    int CareerGoals,
    int CareerMatches,
    string? PhotoUrl,
    string? PhotoLicense,
    string? PhotoCredit)
{
    public WorldPlayerProvenance? CatalogIdentity { get; init; }
    public decimal ReferenceSalaryPerRound { get; init; }
    public bool NegotiatedContractProtected { get; init; }
    public int InjuryDaysRemaining { get; init; }
    public int TotalYellowCards { get; init; }
    public int YellowCardsInCurrentCycle { get; init; }
    public int TotalDirectRedCards { get; init; }
    public int TotalSecondYellowDismissals { get; init; }
    public int SuspensionMatchesRemaining { get; init; }
    public string? SuspensionReason { get; init; }
    public DateOnly? InjuryRecoveryDate { get; init; }
}

public record ClubDetailDto(
    Guid Id,
    string Name,
    string ShortName,
    string Country,
    string PrimaryColorHex,
    string SecondaryColorHex,
    DivisionTier DivisionTier,
    string DivisionDisplay,
    decimal CashBalance,
    int Reputation,
    Guid? HumanCoachId,
    string? HumanCoachName,
    Guid? AiManagerId,
    string? AiManagerName,
    string? AiManagerIdentity,
    string PreferredFormation,
    List<Guid> PreferredStarterIds,
    List<Guid> PreferredBenchIds,
    StadiumDto Stadium,
    List<PlayerDto> Squad,
    AiSquadPlanDto? AiSquadPlan)
{
    public ClubColorPalette? ColorPalette { get; init; }
    public int? GrowthCeiling { get; init; }
    public int? GrowthLeaguePosition { get; init; }
    public bool GrowthPositionIsProvisional { get; init; }
    public int RequiredStarterCount { get; init; } = 11;
}

public record StadiumDto(
    Guid ClubId,
    string StadiumName,
    int BuiltCapacity,
    int UsableCapacity,
    double ConditionPercent,
    decimal MaintenanceReservePerSeason,
    decimal FixedCostPerMatch,
    decimal VariableCostPerAttendee,
    decimal LastCalculatedTicketPrice,
    int LastAttendance,
    int ExpansionTargetSeats,
    int ExpansionRoundsRemaining,
    int RenovationRoundsRemaining);

public record AiSquadPlanDto(
    Guid ManagerId,
    string ManagerName,
    string TacticalIdentity,
    List<FormationPreferenceWeightDto> PreferredFormations,
    string SelectedFormationForNextMatch,
    string SelectionReasoning,
    Dictionary<string, int> TargetStartersByPosition,
    Dictionary<string, int> TargetReservesByPosition,
    List<string> PriorityGaps,
    decimal MaxSingleSigningBudget,
    decimal EmergencyCashReserve,
    List<Guid> SurplusPlayerIds);

public record FormationPreferenceWeightDto(string Formation, int WeightPercent);

public record DivisionStandingRowDto(
    int Position,
    Guid ClubId,
    string ClubName,
    string PrimaryColorHex,
    string SecondaryColorHex,
    bool IsHumanControlled,
    string CoachName,
    int Played,
    int Wins,
    int Draws,
    int Losses,
    int GoalsFor,
    int GoalsAgainst,
    int GoalDifference,
    int Points,
    string ZoneStatus);

public record TopScorerDto(
    int Rank,
    Guid PlayerId,
    string DisplayName,
    PlayerPosition Position,
    int Strength,
    bool IsStar,
    Guid ClubId,
    string ClubName,
    DivisionTier Division,
    int SeasonGoals,
    int CupGoals);

public record CoachRankingDto(
    int Rank,
    Guid CoachId,
    string Name,
    bool IsHuman,
    Guid? CurrentClubId,
    string? CurrentClubName,
    DivisionTier? CurrentDivision,
    int LeagueTitles,
    int CupTitles,
    decimal CoachPoints,
    int TotalWins,
    int TotalDraws,
    int TotalLosses,
    int SeasonsManaged,
    int Dismissals);

public record TransferListingDto(
    Guid PlayerId,
    string DisplayName,
    PlayerPosition Position,
    int Strength,
    string BehaviorDisplay,
    bool IsStar,
    Guid? SellerClubId,
    string SellerClubName,
    decimal AskingPrice,
    decimal FairValue,
    decimal SalaryPerRound,
    int ContractRoundsRemaining,
    int ListedAtRound)
{
    public long ListingVersion { get; init; }
}

public record AuctionItemDto(
    Guid AuctionId,
    Guid PlayerId,
    string DisplayName,
    PlayerPosition Position,
    int Strength,
    string BehaviorDisplay,
    bool IsStar,
    decimal MinimumBid,
    decimal CurrentBid,
    Guid? HighestBidderClubId,
    string? HighestBidderClubName,
    int ExpiresAtRound,
    bool IsClosed)
{
    public Guid? SellerClubId { get; init; }
    public string? SellerClubName { get; init; }
    public int RoundsRemaining { get; init; }
    public int? DurationRounds { get; init; }
    public int? ExpiresAtSeason { get; init; }
    public AuctionReason Reason { get; init; }
    public string? ClosureReason { get; init; }
    public decimal SalaryPerRound { get; init; }
}

public record TransferOfferDto(
    Guid OfferId,
    Guid PlayerId,
    string PlayerDisplayName,
    PlayerPosition Position,
    int Strength,
    bool IsStar,
    Guid BuyerClubId,
    string BuyerClubName,
    Guid SellerClubId,
    string SellerClubName,
    decimal OfferedAmount,
    decimal? CounterAmount,
    TransferOfferStatus Status,
    string Justification,
    int CreatedAtRound);

public record JobInvitationDto(
    Guid InvitationId,
    Guid CoachId,
    Guid ClubId,
    string ClubName,
    DivisionTier DivisionTier,
    string DivisionDisplay,
    decimal ClubBudget,
    int SquadSize,
    int CreatedAtSeason,
    int CreatedAtRound,
    bool IsAccepted,
    bool IsRejected = false,
    string Explanation = "",
    int TablePosition = 0,
    double SquadStrength = 0,
    int StadiumCapacity = 0,
    string Objective = "",
    decimal ProjectImprovement = 0);

public record WorldPackageExportDto(
    string FormatVersion,
    string PackageName,
    int Seed,
    DateTime ExportedAtUtc,
    bool AllowClassic505,
    int DefaultTickIntervalMs,
    List<ClubExportDto> Clubs,
    List<PlayerExportDto> Players,
    List<AiManagerExportDto> AiManagers,
    FootballGender Gender = FootballGender.Male,
    WorldCreationOrigin? CatalogOrigin = null,
    ForceEvolutionMode? ForceEvolutionMode = null);

public record ClubExportDto(
    Guid Id,
    string Name,
    string ShortName,
    string Country,
    string PrimaryColorHex,
    string SecondaryColorHex,
    DivisionTier DivisionTier,
    decimal CashBalance,
    int Reputation,
    string StadiumName,
    int BuiltCapacity,
    int UsableCapacity,
    double ConditionPercent,
    decimal MaintenanceReserve,
    ClubColorPalette? ColorPalette = null);

public record PlayerExportDto(
    Guid Id,
    Guid? ClubId,
    string Name,
    PlayerPosition Position,
    int Strength,
    PlayerBehavior Behavior,
    bool IsStar,
    int ContractRoundsRemaining,
    decimal SalaryPerRound,
    decimal MarketPrice,
    string? PhotoUrl,
    string? PhotoLicense,
    string? PhotoCredit,
    bool? NegotiatedContractProtected = null,
    WorldPlayerProvenance? CatalogIdentity = null);

public record AiManagerExportDto(
    Guid Id,
    Guid? AssignedClubId,
    string Name,
    ManagerTacticalIdentity Identity,
    int Pragmatism,
    int Adaptability,
    int FinancialAggressiveness,
    int YouthPatience,
    List<FormationPreferenceWeightDto> PreferredFormations);

public record PackageValidationResultDto(
    bool IsValid,
    int ClubCount,
    int PlayerCount,
    int ManagerCount,
    List<string> Errors,
    List<string> Warnings,
    List<string> DiffPreview);
