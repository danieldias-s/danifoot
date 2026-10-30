using System.Text.Json.Serialization;

namespace Elifoot.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CatalogDataOrigin { Real, Estimated, Generated, Fixture, Unspecified }
public record CatalogPlayerPerformance(double? Rating = null, int? Minutes = null, int? Appearances = null,
    int? Goals = null, int? Assists = null, string? RatingScale = null);
public record PlayerStrengthEstimate(string Method, CatalogDataOrigin Origin, double Confidence,
    string InputHash, double IndividualPercentile, double? ClubPercentile);
public record CatalogUsageRights(string License, string Evidence, bool AccessAllowed, bool StorageAllowed,
    bool DisplayAllowed, bool CommercialUseAllowed, bool RedistributionAllowed, bool OfflineWorldsAllowed,
    bool WorldExportAllowed, string? Attribution = null, DateTime? UseUntil = null, DateTime? RetainUntil = null,
    bool LogosAllowed = false, bool PhotosAllowed = false);
public record CatalogRosterReport(string ClubExternalId, string ClubProvider, int ReceivedPlayers,
    int UsablePlayers, bool Complete, List<string> Issues);
public record CatalogClubAlias(string Provider, string ExternalId);
public record CatalogIdentityMappingRequest(string CountryCode, FootballGender Gender, string Provider,
    string ExternalId, Guid InternalClubId, string Evidence);
public record CatalogCoverageReport(string CountryCode, string CountryName, FootballGender Gender,
    string Season, List<string> Leagues, int Clubs, int Players, int CompleteSquads, int PartialSquads,
    int RealPlayers, int EstimatedStrengths, int GeneratedPlayers, List<CatalogReference> References,
    List<string> Restrictions, List<string> Warnings, int DocumentedPlayers = 0, int UncertainPlayers = 0, int VerifiedClubs = 0, int HistoricalClubs = 0, int UnverifiedClubs = 0);

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CatalogClubVerificationStatus { REAL_CLUB_VERIFIED, REAL_CLUB_HISTORICAL, REAL_CLUB_UNVERIFIED, FICTIONAL_CLUB }
public record CatalogClubVerification(CatalogClubVerificationStatus Status, string Evidence, DateTime ObservedAt,
    string? SourceSeason = null, bool CurrentSeasonConfirmed = false, DateOnly? DissolvedAt = null, string? WikidataId = null);
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum CatalogMembershipStatus { DatedActive, OpenEndedUnconfirmed, Historical, Unknown }
public record CatalogMembershipEvidence(CatalogMembershipStatus Status, string StatementId, string Evidence,
    DateOnly AsOf, string? Note = null);
