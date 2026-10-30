using Elifoot.Contracts;

namespace Elifoot.Domain;

public class ClassicForceState
{
    public int Version { get; set; } = 1;
    public int CalendarSeason { get; set; }
    public DateOnly SeasonStartDate { get; set; } = new(1998, 8, 1);
    public DateOnly GameDate { get; set; } = new(1998, 8, 1);
    public HashSet<Guid> ProcessedMatches { get; set; } = new();
    public HashSet<string> ProcessedRoundPlayers { get; set; } = new();
    public int GuardSeason { get; set; }
    public int GuardRound { get; set; }
    public Dictionary<Guid, int> CompletedLeaguePositions { get; set; } = new();
    public Dictionary<Guid, int> SeasonStartingStrengths { get; set; } = new();
    public List<ForceTurnSnapshot> TurnSnapshots { get; set; } = new();
    public Dictionary<string, DateOnly> RoundDates { get; set; } = new();
    public int PreparedRound { get; set; }
    public List<ForceCalendarDelay> CalendarDelays { get; set; } = new();
}

public class ForceInjury
{
    public Guid Id { get; set; }
    public Guid MatchId { get; set; }
    public int Season { get; set; }
    public int Round { get; set; }
    public DateOnly StartDate { get; set; }
    public int Duration { get; set; }
    public int InitialForce { get; set; }
    public int TotalLoss { get; set; }
    public int ProcessedDays { get; set; }
    public int ActualLoss { get; set; }
    public DateOnly EndDate => StartDate.AddDays(Duration);
}

public record ForceRosterPlayer(Guid PlayerId, Guid ClubId);
public record ForceCalendarDelay(int Season, int Round, DateOnly ScheduledDate, DateOnly ActualDate, List<Guid> WaitingClubs);
public record ForceTurnPlayer(Guid PlayerId, int StartingStrength, int CurrentStrength,
    int ReserveGrowth, int PlayedMatchLoss, int InjuryLoss);
public record ForceTurnSnapshot(int Season, int Half, DateOnly GameDate, Guid ClubId, DivisionTier Division,
    int LeaguePosition, int GrowthCeiling, List<ForceTurnPlayer> Players, int ReserveGrowth, int PlayedMatchLoss, int InjuryLoss);
