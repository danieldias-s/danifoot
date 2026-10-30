namespace Elifoot.Contracts;

public record PlayerRankingRowDto(int Rank, Guid PlayerId, string DisplayName, PlayerPosition Position,
    int Strength, string BehaviorDisplay, bool IsStar, Guid? CurrentClubId, string CurrentClubName,
    int MatchesPlayed, int GoalsScored, int GoalsConceded, int YellowCards, int RedCards, int MinutesPlayed,
    decimal? Average, decimal RedRate, decimal YellowRate, int MatchesRemaining, int SeasonTopScorerTitles);
public record PlayerRankingsDto(string Category, string Scope, string Period, int? Season, int CurrentSeason,
    int MinimumMatches, List<PlayerRankingRowDto> Rows, List<PlayerRankingRowDto> NotEligible,
    List<int> Seasons, List<string> CoverageWarnings);
public record PlayerStatisticsRowDto(int Season, CompetitionType CompetitionType, DivisionTier? Division,
    Guid ClubId, int MatchesPlayed, int GoalsScored, int GoalsConceded, int YellowCards, int RedCards,
    int MinutesPlayed, bool HasIncompleteAppearances, bool HasIncompleteGoals = false);
public record PlayerAwardDto(Guid AwardId, Guid PlayerId, string DisplayName, int Season, int TotalGoals,
    int TotalMatches, Guid? ClubId, string ClubNameAtClose, decimal AwardAmount, DateTime AwardGrantedAt,
    string IdempotencyKey);
public record PlayerHistoryDto(PlayerRankingRowDto Player, PlayerRankingRowDto Career,
    List<PlayerStatisticsRowDto> Statistics, List<PlayerAwardDto> Awards, List<string> CoverageWarnings);
public record AwardClubTotalDto(Guid ClubId, string ClubName, decimal Amount);
public record SeasonTopScorerAwardsDto(int CurrentSeason, int? Season, List<PlayerAwardDto> Awards,
    List<AwardClubTotalDto> ClubTotals, List<string> CoverageWarnings);
