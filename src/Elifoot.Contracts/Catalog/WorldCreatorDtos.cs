using System.Text.Json.Serialization;

namespace Elifoot.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum FootballGender { Male, Female }
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CatalogDataType { Clubs, Squads, Rankings, Countries, Leagues }
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WorldAssemblyMode { Automatic, Custom }
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DistrictFillMode { Ranking, Random, Manual }

public record CatalogScopeKey(string CountryCode, FootballGender Gender, string League, string Season,
    CatalogDataType DataType, string Provider)
{
    public string Key => $"{CountryCode}|{Gender}|{League}|{Season}|{DataType}|{Provider}";
}
public record CatalogCountryEntry(string Code, string Name);
public record CatalogLeagueEntry(string ExternalId, string Name, FootballGender Gender, string Season);
public record CatalogTeam(string ExternalId, string Name, string ShortName, string LeagueExternalId,
    string LeagueName, string PrimaryColor = "#006400", string SecondaryColor = "#FFFFFF", CatalogClubVerification? Verification = null, ClubColorPalette? ColorPalette = null);
public record CatalogAthlete(string PlayerExternalId, string ClubExternalId, string ClubProvider,
    FootballGender Gender, string Season, string PlayerName, string Position, int Strength, string Source,
    DateTime UpdatedAt, string? CanonicalPlayerId = null, CatalogDataOrigin? IdentityOrigin = null,
    PlayerStrengthEstimate? StrengthEstimate = null, CatalogPlayerPerformance? Performance = null,
    DateOnly? MembershipStart = null, DateOnly? MembershipEnd = null, string? OriginalPosition = null, CatalogMembershipEvidence? MembershipEvidence = null, WorldPlayerOrigin? PlayerOrigin = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Number = null);
public record CatalogRank(string ClubExternalId, string ClubProvider, FootballGender Gender, string Season,
    int Rank, string Source, DateTime UpdatedAt, double? Score = null, string? Method = null,
    double? Confidence = null, string? ComparableGroup = null, CatalogDataOrigin? Origin = null);
public record CatalogImportPackage(CatalogScopeKey Scope, string CountryName, string Version,
    DateTime UpdatedAt, bool Complete, int ExpectedPages, int ReceivedPages, int ExpectedItems,
    bool StorageAuthorized, bool CommercialUseAuthorized, string LicenseReference,
    bool IsFixture, List<CatalogTeam> Clubs, List<CatalogAthlete> Players, List<CatalogRank> Rankings,
    bool AllowCountDecrease = false, List<CatalogCountryEntry>? CountryData = null, List<CatalogLeagueEntry>? LeagueData = null, CatalogUsageRights? Rights = null,
    bool AllowPartialSquads = false, List<CatalogRosterReport>? RosterReports = null, DateOnly? RosterAsOf = null);
public record CatalogReference(Guid SnapshotId, CatalogScopeKey Scope, string Version, DateTime UpdatedAt,
    DateTime ValidatedAt, bool IsFixture, string Status, string? Warning, CatalogUsageRights? Rights = null);
public record EligibleCatalogClub(string Key, string ExternalId, string Provider, string Name, string ShortName,
    string CountryCode, string CountryName, FootballGender Gender, string League, string Season,
    string PrimaryColor, string SecondaryColor, int? Rank, List<CatalogAthlete> Players,
    List<CatalogReference> References, bool IsFictional = false, Guid? InternalCatalogClubId = null,
    CatalogRank? RankingInfo = null, List<CatalogClubAlias>? Aliases = null, List<string>? CoverageWarnings = null,
    int? RawRosterCount = null, List<Guid>? IdentityConflicts = null, CatalogClubVerification? Verification = null, int? DocumentedPlayers = null, int? UncertainPlayers = null,
    List<CatalogAthlete>? ReferencePlayers = null, DateOnly? RosterAsOf = null, CatalogRank? SourceRanking = null, ClubColorPalette? ColorPalette = null, WorldClubProfile016B? InitialProfile = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] OfflineClubSource? OfflineSource = null);
public record CatalogCountryOption(string Code, string Name, int MaleClubs, int FemaleClubs);
public record CatalogSelection(FootballGender Gender, string Season, List<string> Countries,
    List<EligibleCatalogClub> Clubs, List<CatalogReference> References, List<string> Warnings, WorldDataSummary? DataSummary = null);
// In a custom assembly, the order of assignments within each division defines its slots.
public record ClubAssignment(string ClubKey, int Division);
public record WorldCreatorReassignRequest(List<ClubAssignment> Assignments, string? Name = null, int? MaxHumans = null,
    DistrictFillMode? DistrictMode = null);
public record WorldCreatorRequest(string Name, FootballGender Gender, string Season, List<string> Countries,
    WorldAssemblyMode Mode, DistrictFillMode DistrictMode, int Seed, List<ClubAssignment> Assignments,
    bool FillRemainingDivisions = true, bool AllowFictionalTeams = false, bool UseSavedData = false,
    int MaxHumans = 8, bool AllowFictionalPlayers = false, bool IncludeHistoricalClubs = false, bool IncludeUnverifiedClubs = false, WorldDataMode? DataMode = null, bool AllowProviderReportedPlayers = false, bool UseWorldCatalogRules = false, bool FictionalOnly = false, int EliteFullSquadRankCutoff = 20, bool RequireHostStart = false, string GameModeId = "CLASSIC_SYNC",
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool PreserveRankedSquadStrengths = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] bool GenerateAllRosterPlayers = false,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] List<string>? CatalogYears = null);
public record InitialSquadReference017B(int Position, int Strength, int Minimum, int Maximum);
public record WorldPreviewClub(int Division, EligibleCatalogClub Club, InitialSquadReference017B? InitialSquadReference = null);
public record WorldCreatorPreview(Guid DraftId, string Name, FootballGender Gender, int Seed,
    List<WorldPreviewClub> Clubs, int PendingSlots, List<string> Errors, List<string> Warnings,
    List<CatalogReference> References, DateTime CreatedAt, string DataHash,
    WorldDataMode? DataMode = null, DateOnly? DataAsOf = null, WorldDataSummary? DataSummary = null);
public record WorldCreationOrigin(FootballGender Gender, string Season, List<string> Countries, int Seed,
    DateTime CreatedAt, string DataHash, List<WorldPreviewClub> Clubs, List<CatalogReference> References,
    WorldDataMode? DataMode = null, int? GeneratedPlayerSeed = null, DateOnly? DataAsOf = null,
    WorldDataSummary? DataSummary = null, List<CatalogRank>? RankingSnapshot = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] List<string>? CatalogYears = null);
