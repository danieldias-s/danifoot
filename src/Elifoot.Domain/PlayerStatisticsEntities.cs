using Elifoot.Contracts;

namespace Elifoot.Domain;

public record PlayerLineupSnapshot(Guid PlayerId, Guid ClubId, PlayerPosition Position, int Strength, bool IsStarter);

public class PlayerCompetitionStatistics
{
    public Guid PlayerId { get; set; }
    public int Season { get; set; }
    public CompetitionType CompetitionType { get; set; }
    public DivisionTier? Division { get; set; }
    public Guid ClubId { get; set; }
    public string ClubName { get; set; } = string.Empty;
    public PlayerPosition Position { get; set; }
    public int MatchesPlayed { get; set; }
    public int GoalsScored { get; set; }
    public int Assists { get; set; }
    public bool HasIncompleteAssists { get; set; } = true;
    public int GoalsConceded { get; set; }
    public int YellowCards { get; set; }
    public int RedCards { get; set; }
    public int MinutesPlayed { get; set; }
    public bool HasIncompleteAppearances { get; set; }
    public bool HasIncompleteGoals { get; set; }
    // (MatchId, PlayerId) is the logical idempotency key; each bucket retains its constituent MatchIds.
    public HashSet<Guid> AppliedMatchIds { get; set; } = new();
}

public record SeasonTopScorerAward(Guid AwardId, Guid PlayerId, string DisplayName, int Season,
    int TotalGoals, int TotalMatches, Guid? ClubId, string ClubNameAtClose, decimal AwardAmount,
    DateTime AwardGrantedAt, string IdempotencyKey);
