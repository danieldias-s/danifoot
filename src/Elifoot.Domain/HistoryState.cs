using Elifoot.Contracts;
namespace Elifoot.Domain;

public partial class GameWorld { public HistoryState History { get; set; } = new(); }
public sealed class HistoryState
{
    public int Version { get; set; }
    public int StartSeason { get; set; }
    public int StartRound { get; set; }
    public Dictionary<string,ClubSeasonArchive> ClubSeasons { get; set; } = new();
    public Dictionary<Guid,HistoryMatchArchive> Matches { get; set; } = new();
    public Dictionary<Guid,HistoryTransferDto> Transfers { get; set; } = new();
    public Dictionary<string,PlayerStrengthArchive> Strengths { get; set; } = new();
    public List<HistoryMembershipDto> Memberships { get; set; } = new();
    public List<HistoryTitleDto> Titles { get; set; } = new();
    public List<HistoryRecognitionDto> Scorers { get; set; } = new();
    public HashSet<int> ClosedSeasons { get; set; } = new();
}
public sealed class ClubSeasonArchive
{
    public Guid ClubId { get; set; }
    public int Season { get; set; }
    public DivisionTier? Division { get; set; }
    public int? FinalPosition { get; set; }
    public bool Closed { get; set; }
    public bool Partial { get; set; } = true;
    public bool Promoted { get; set; }
    public bool Relegated { get; set; }
    public decimal? OpeningCash { get; set; }
    public decimal? ClosingCash { get; set; }
    public decimal Income { get; set; }
    public decimal Expenses { get; set; }
}
public sealed class PlayerStrengthArchive
{
    public Guid PlayerId { get; set; }
    public int Season { get; set; }
    public int? Initial { get; set; }
    public int? Final { get; set; }
    public bool Partial { get; set; }
}
public record HistoryMatchArchive(HistoryMatchDto Match,Guid? HomeCoachId,Guid? AwayCoachId);
