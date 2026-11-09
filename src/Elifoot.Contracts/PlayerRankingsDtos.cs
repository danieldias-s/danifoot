namespace Elifoot.Contracts;

public record PlayerRankingRowDto(int Rank, Guid PlayerId, string DisplayName, PlayerPosition Position,
    int Strength, string BehaviorDisplay, bool IsStar, Guid? CurrentClubId, string CurrentClubName,
    int MatchesPlayed, int GoalsScored, int GoalsConceded, int YellowCards, int RedCards, int MinutesPlayed,
    decimal? Average, decimal RedRate, decimal YellowRate, int MatchesRemaining, int SeasonTopScorerTitles)
{
    public int Assists { get; init; }
    public bool HasIncompleteAssists { get; init; }
}
public record PlayerRankingsDto(string Category, string Scope, string Period, int? Season, int CurrentSeason,
    int MinimumMatches, List<PlayerRankingRowDto> Rows, List<PlayerRankingRowDto> NotEligible,
    List<int> Seasons, List<string> CoverageWarnings);
public record PlayerStatisticsRowDto(int Season, CompetitionType CompetitionType, DivisionTier? Division,
    Guid ClubId, int MatchesPlayed, int GoalsScored, int GoalsConceded, int YellowCards, int RedCards,
    int MinutesPlayed, bool HasIncompleteAppearances, bool HasIncompleteGoals = false, string ClubName = "")
{
    public int Assists { get; init; }
    public bool HasIncompleteAssists { get; init; }
    public int Injuries { get; init; }
}
public record PlayerAwardDto(Guid AwardId, Guid PlayerId, string DisplayName, int Season, int TotalGoals,
    int TotalMatches, Guid? ClubId, string ClubNameAtClose, decimal AwardAmount, DateTime AwardGrantedAt,
    string IdempotencyKey);
public record PlayerHistoryDto(PlayerRankingRowDto Player, PlayerRankingRowDto Career,
    List<PlayerStatisticsRowDto> Statistics, List<PlayerAwardDto> Awards, List<string> CoverageWarnings)
{
    public List<PlayerStrengthChangeDto> StrengthHistory { get; init; } = new();
    public int? StrengthHistoryStartSeason { get; init; }
    public int? StrengthHistoryStartRound { get; init; }
    public List<PlayerCardHistoryDto> CardHistory { get; init; } = new();
    public List<PlayerSuspensionHistoryDto> SuspensionHistory { get; init; } = new();
    public List<HistoryMembershipDto> ClubCareer { get; init; } = new();
    public List<HistoryTransferDto> Transfers { get; init; } = new();
    public List<HistoryTitleDto> ClubTitlesWithParticipation { get; init; } = new();
    public List<HistoryRecognitionDto> ScoringRecognitions { get; init; } = new();
    public List<PlayerSeasonStrengthDto> SeasonStrengths { get; init; } = new();
    public int HighestRecordedStrength { get; init; }
    public int MostGoalsInSeason { get; init; }
    public int MostGamesInSeason { get; init; }
    public List<HistoryPlayerSeasonTotalsDto> SeasonTotals { get; init; } = new();
    public int CurrentSeason { get; init; }
    public DivisionTier? CurrentDivisionTier { get; init; }
    public string? CurrentDivisionDisplay { get; init; }
    public int CareerMatches { get; init; }
    public int SeasonGoals { get; init; }
    public int SeasonAssists { get; init; }
    public int CurrentDivisionGoals { get; init; }
    public int CurrentDivisionAssists { get; init; }
    public Dictionary<string, int> DivisionGoals { get; init; } = new();
    public Dictionary<string, int> DivisionAssists { get; init; } = new();
    public int CareerGoals { get; init; }
    public int CareerAssists { get; init; }
    public int CareerRedCards { get; init; }
    public int CareerInjuries { get; init; }
}
public record AwardClubTotalDto(Guid ClubId, string ClubName, decimal Amount);
public record SeasonTopScorerAwardsDto(int CurrentSeason, int? Season, List<PlayerAwardDto> Awards,
    List<AwardClubTotalDto> ClubTotals, List<string> CoverageWarnings);
