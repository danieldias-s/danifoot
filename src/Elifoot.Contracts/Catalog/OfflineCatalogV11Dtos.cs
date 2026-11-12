using System.Text.Json.Serialization;

namespace Elifoot.Contracts;

public record OfflineIdentityDefinition(string? SchemaVersion, string? CatalogClubId, string? Name,
    string? ShortName, string? Abbreviation, List<string>? Colors, string? CrestFile,
    [property: JsonPropertyName("$schema")] string? Schema = null);
public record OfflineHistoricalRanking(double? Rating, string? Source, DateOnly? ReferenceDate);
// A separate wire contract rejects annual copies of identity fields and individual strength.
public record OfflinePlayerV11(string? Id, string? Name, string? Position, string? Nationality,
    int? Behavior, bool? IsStar);
public record OfflineClubV11(string? SchemaVersion, string? CatalogClubId, string? Country, int? Year,
    int? TeamStrength, OfflineHistoricalRanking? HistoricalRanking, int? TieBreakPriority,
    OfflineStadium? Stadium, OfflineCoach? Coach, List<OfflinePlayerV11>? Players, string? Status,
    [property: JsonPropertyName("$schema")] string? Schema = null);
public record OfflineV11Metadata(OfflineIdentityDefinition Identity, int? TeamStrength,
    OfflineHistoricalRanking? HistoricalRanking, int? TieBreakPriority);
