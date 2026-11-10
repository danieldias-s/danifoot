using System.Text.Json.Serialization;
namespace Elifoot.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum LineupSelectionMode { Automatic = 0, Best = 1 }

public record SelectLineupRequest(Guid CoachId, string Formation, LineupSelectionMode Mode,
    List<Guid>? CurrentStarterPlayerIds, string IdempotencyKey, long ExpectedRoundVersion);
public record LineupSelectionDto(Guid GameWorldId, Guid ClubId, string Formation, LineupSelectionMode Mode,
    List<Guid> StarterPlayerIds, List<Guid> BenchPlayerIds, long RoundVersion);
