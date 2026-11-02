using Elifoot.Contracts;
namespace Elifoot.Domain;

public partial class GameWorld
{
    // Interaction decisions must be durable even between periodic live-clock checkpoints.
    [System.Text.Json.Serialization.JsonIgnore]
    public bool RequiresDurableMatchInteractionSave { get; set; }
    public Dictionary<string, MatchInteractionReceipt> MatchInteractionReceipts { get; set; } = new();
}
public sealed class MatchInteractionReceipt
{
    public string Fingerprint { get; set; } = "";
    public Guid? PauseWindowId { get; set; }
}
public sealed class PendingPenaltyKick
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid CoachId { get; set; }
    public Guid ClubId { get; set; }
    public int Minute { get; set; }
    public double ConversionRoll { get; set; }
    public double GoalkeeperPower { get; set; }
    public Guid? GoalkeeperId { get; set; }
    public List<Guid> EligiblePlayerIds { get; set; } = new();
}
