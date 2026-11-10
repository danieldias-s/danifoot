using Elifoot.Contracts;
namespace Elifoot.Domain;

public partial class GameWorld
{
    // Additive snapshot field; old saves default to an empty receipt collection.
    public Dictionary<string, LineupSelectionReceipt> LineupSelectionReceipts { get; set; } = new();
}
public record LineupSelectionReceipt(int Season, int Round, string Payload, LineupSelectionDto Result);
