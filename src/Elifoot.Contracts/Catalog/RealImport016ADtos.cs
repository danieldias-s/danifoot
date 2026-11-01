using System.Text.Json.Serialization;
namespace Elifoot.Contracts;

public record PilotClub016A(string Key, string Name, string CountryCode, string CountryName,
    FootballGender Gender, string WikidataId, string CountryEntityId, List<string> Aliases);
public static class PilotClubs016A
{
    public static readonly List<PilotClub016A> All = [
        new("flamengo", "Flamengo", "BR", "Brasil", FootballGender.Male, "Q17479", "Q155", ["Flamengo", "Flamengo RJ", "Clube de Regatas do Flamengo"]),
        new("corinthians", "Corinthians", "BR", "Brasil", FootballGender.Male, "Q35933", "Q155", ["Corinthians", "Corinthians SP", "Sport Club Corinthians Paulista", "S.C. Corinthians Paulista"]),
        new("real-madrid", "Real Madrid", "ES", "Espanha", FootballGender.Male, "Q8682", "Q29", ["Real Madrid", "Real Madrid CF", "Real Madrid Club de Fútbol"]),
        new("barcelona-male", "Barcelona", "ES", "Espanha", FootballGender.Male, "Q7156", "Q29", ["Barcelona", "FC Barcelona", "Fútbol Club Barcelona"]),
        new("porto", "FC Porto", "PT", "Portugal", FootballGender.Male, "Q128446", "Q45", ["FC Porto", "Porto", "Futebol Clube do Porto"]),
        new("barcelona-female", "FC Barcelona Feminino", "ES", "Espanha", FootballGender.Female, "Q522899", "Q29", ["Barcelona W", "Barcelona Women", "FC Barcelona Femení", "Barcelona Feminino", "FC Barcelona Feminino"])
    ];
}
public record RealImport016ARequest(List<string> ClubKeys, string Provider = "wikidata", bool Refresh = false, bool IncludeSquads = true);
public record ConfirmClub016ARequest(string ClubKey, string Provider, string ExternalTeamId, string ExternalLeagueId,
    string LeagueName, FootballGender Gender, string CountryCode, string Evidence);
public record ImportCandidate016A(string ExternalId, string Name, string Country, string? LeagueId, string? LeagueName, bool RequiresReview);
public record ImportPlayer016A(string ExternalId, string Name, string? Position, string? OriginalPosition,
    WorldPlayerOrigin Origin, string ClubExternalId, string Season, DateTime ObservedAt, string Evidence, List<string> Issues);
public record ImportClubReport016A(string ClubKey, string Name, string CountryCode, FootballGender Gender,
    string Provider, string? ExternalId, string WikidataId, string Season, DateTime ObservedAt, string Status,
    int ReceivedPlayers, int UsablePlayers, Dictionary<string,int> Positions, int Duplicates,
    int CurrentReported, int CurrentVerified, int Historical, List<string> Issues, List<ImportCandidate016A> Candidates,
    List<ImportPlayer016A> Players, Guid? InternalCatalogClubId = null);
public record RealImportRun016A(Guid Id, string Mode, string Provider, string Status, DateTime CreatedAt,
    List<ImportClubReport016A> Clubs, List<string> Issues, bool IsFixture, bool Applied = false,
    List<Guid>? SnapshotIds = null);
public record ImportProviderStatus016A(string Provider, bool Configured, string Status, int DailyBudget,
    int RequestsUsed, int? ProviderRemaining, DateTime? BlockedUntil, int CacheEntries);
public record RealImportCoverage016A(List<ImportProviderStatus016A> Providers, List<ImportClubReport016A> Clubs,
    DateTime? LastUpdate, int RealPlayers, int CurrentReported, int CurrentVerified, int IncompleteRecords);
