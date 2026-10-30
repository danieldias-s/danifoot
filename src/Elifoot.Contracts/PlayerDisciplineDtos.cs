namespace Elifoot.Contracts;

public record PlayerCardHistoryDto(string Key, Guid WorldId, Guid MatchId, int Season, int Round,
    int Minute, Guid ClubId, CompetitionType Competition, MatchEventType Card, int SuspensionMatches,
    bool RecoveredFromLegacy = false);

public class PlayerSuspensionHistoryDto
{
    public string Key { get; set; } = "";
    public Guid WorldId { get; set; }
    public Guid MatchId { get; set; }
    public Guid ClubId { get; set; }
    public int Season { get; set; }
    public int Round { get; set; }
    public int Minute { get; set; }
    public string Action { get; set; } = "";
    public string Reason { get; set; } = "";
    public int Matches { get; set; }
    public int BalanceBefore { get; set; }
    public int BalanceAfter { get; set; }
}
