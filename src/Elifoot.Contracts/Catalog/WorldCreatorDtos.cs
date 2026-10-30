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
    string LeagueName, string PrimaryColor = "#006400", string SecondaryColor = "#FFFFFF");
public record CatalogAthlete(string PlayerExternalId, string ClubExternalId, string ClubProvider,
    FootballGender Gender, string Season, string PlayerName, string Position, int Strength, string Source,
    DateTime UpdatedAt);
public record CatalogRank(string ClubExternalId, string ClubProvider, FootballGender Gender, string Season,
    int Rank, string Source, DateTime UpdatedAt);
public record CatalogImportPackage(CatalogScopeKey Scope, string CountryName, string Version,
    DateTime UpdatedAt, bool Complete, int ExpectedPages, int ReceivedPages, int ExpectedItems,
    bool StorageAuthorized, bool CommercialUseAuthorized, string LicenseReference,
    bool IsFixture, List<CatalogTeam> Clubs, List<CatalogAthlete> Players, List<CatalogRank> Rankings,
    bool AllowCountDecrease = false, List<CatalogCountryEntry>? CountryData = null, List<CatalogLeagueEntry>? LeagueData = null);
public record CatalogReference(Guid SnapshotId, CatalogScopeKey Scope, string Version, DateTime UpdatedAt,
    DateTime ValidatedAt, bool IsFixture, string Status, string? Warning);
public record EligibleCatalogClub(string Key, string ExternalId, string Provider, string Name, string ShortName,
    string CountryCode, string CountryName, FootballGender Gender, string League, string Season,
    string PrimaryColor, string SecondaryColor, int? Rank, List<CatalogAthlete> Players,
    List<CatalogReference> References, bool IsFictional = false);
public record CatalogCountryOption(string Code, string Name, int MaleClubs, int FemaleClubs);
public record CatalogSelection(FootballGender Gender, string Season, List<string> Countries,
    List<EligibleCatalogClub> Clubs, List<CatalogReference> References, List<string> Warnings);
public record ClubAssignment(string ClubKey, int Division);
public record WorldCreatorRequest(string Name, FootballGender Gender, string Season, List<string> Countries,
    WorldAssemblyMode Mode, DistrictFillMode DistrictMode, int Seed, List<ClubAssignment> Assignments,
    bool FillRemainingDivisions = true, bool AllowFictionalTeams = false, bool UseSavedData = false,
    int MaxHumans = 8);
public record WorldPreviewClub(int Division, EligibleCatalogClub Club);
public record WorldCreatorPreview(Guid DraftId, string Name, FootballGender Gender, int Seed,
    List<WorldPreviewClub> Clubs, int PendingSlots, List<string> Errors, List<string> Warnings,
    List<CatalogReference> References, DateTime CreatedAt, string DataHash);
public record WorldCreationOrigin(FootballGender Gender, string Season, List<string> Countries, int Seed,
    DateTime CreatedAt, string DataHash, List<WorldPreviewClub> Clubs, List<CatalogReference> References);
