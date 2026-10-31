using System.Text.Json.Serialization;
namespace Elifoot.Contracts;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WorldDataMode { REAL_COMPLETE, REAL_CLUBS_SIMULATED, FREE_WORLD }
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum WorldPlayerOrigin { REAL_VERIFIED_CURRENT, REAL_HISTORICAL_REFERENCE, FICTIONAL_GENERATED }

public record WorldPlayerProvenance(WorldPlayerOrigin Origin, string Provider, string ExternalPlayerId,
    string CanonicalIdentity, string InitialClubKey, string CountryCode, FootballGender Gender, string Season,
    DateTime UpdatedAt, DateOnly AsOf, List<CatalogReference> References,
    CatalogMembershipEvidence? MembershipEvidence = null, int? GeneratedSeed = null,
    PlayerStrengthEstimate? StrengthEstimate = null);
public record WorldDataSummary(int VerifiedRealClubs, int CurrentSeasonConfirmedClubs, int CompleteRealSquads,
    int IncompleteRealSquads, int MissingClubSlots, int SelectedRealClubs, int SelectedFictionalClubs,
    int RealCurrentPlayers, int GeneratedPlayers, int InitiallyIncompleteSelectedClubs, int SelectedReferenceClubs = 0);