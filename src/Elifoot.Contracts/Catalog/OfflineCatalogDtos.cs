using System.Text.Json.Serialization;

namespace Elifoot.Contracts;

// Nullable fields distinguish an unfinished draft from an explicit zero/false value.
public record OfflineCountry(string? Code, string? Name, string? Slug);
public record OfflineManifest(string? SchemaVersion, string? CatalogId, OfflineCountry? Country,
    int? Year, string? Name, string? Status, List<string>? Clubs, FootballGender Gender = FootballGender.Male,
    [property: JsonPropertyName("$schema")] string? Schema = null);
public record OfflineStadium(string? Name, int? Capacity);
public record OfflineTactics(List<string>? Home, List<string>? Away);
public record OfflineCoach(string? Id, string? Name, string? Nationality, OfflineTactics? PreferredTactics);
public record OfflinePlayer(string? Id, string? Name, string? Position, int? Strength,
    string? Nationality, int? Behavior, bool? IsStar);
public record OfflineClub(string? SchemaVersion, string? CatalogClubId, string? Country, int? Year,
    string? Name, string? ShortName, string? Abbreviation, List<string>? Colors, OfflineStadium? Stadium,
    OfflineCoach? Coach, List<OfflinePlayer>? Players, string? Status, int? InitialDivision = null,
    string? CrestFile = null, [property: JsonPropertyName("$schema")] string? Schema = null);
public record OfflineCatalogIssue(string Code, string Path, string Message, bool IsError = true);
public record OfflineClubSource(string CatalogId, string CountryCode, string CountryName, int Year,
    string SourceHash, OfflineClub Definition, string? CrestDataUrl = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] OfflineV11Metadata? V11 = null);
public record OfflineLoadedCatalog(string Path, OfflineManifest? Manifest, List<OfflineClubSource> Clubs,
    List<OfflineCatalogIssue> Issues, string SourceHash)
{
    public bool Valid => Issues.All(i => !i.IsError);
    public bool Ready => Valid && Manifest?.Status == "READY" && Clubs.Count > 0 && Issues.Count == 0;
}
public record OfflineCatalogReport(List<OfflineLoadedCatalog> Catalogs, List<OfflineCatalogIssue> Issues)
{
    public bool Valid => Issues.All(i => !i.IsError) && Catalogs.All(c => c.Valid);
}
public record OfflineWorldRequest(string Name, List<string> CatalogIds, int Seed, int MaxHumans = 1,
    List<string>? ClubKeys = null, List<ClubAssignment>? Assignments = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool ApplyV16RosterPolicy = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] List<OfflineRetentionEvidence>? RetentionEvidence = null);
// New-creation input only; no change to the save contract. Missing research never implies prodigy/starter status.
public record OfflineRetentionEvidence(string ClubKey, string PlayerId, string Evidence,
    bool IsProdigy = false, bool ImportantStarter = false, int? Minutes = null);
public record OfflineWorldOrigin(string SchemaVersion, string DataHash, DateTime ImportedAt,
    List<WorldPreviewClub> Clubs);
public record OfflineClubIdentity(string CatalogId, string CatalogClubId, string CountryCode, int Year,
    string ShortName, string Abbreviation, string SourceHash, string? CrestDataUrl = null);
public record OfflinePlayerIdentity(string CatalogId, string PlayerId, string CountryCode, int Year);
public record OfflineCoachIdentity(string CatalogId, string CoachId, string CountryCode, int Year,
    string Nationality, List<string> OriginalHome, List<string> OriginalAway,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? LearnedHome = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? LearnedAway = null);
