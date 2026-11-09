namespace Elifoot.Contracts;

public static class LocalHistoricalCatalogSource
{
    public const string Provider = "local-historical";

    public static string ClubKey(string countryCode, string year, string clubId, FootballGender category) =>
        $"{countryCode}:{year}:{clubId}:{category}";

    public static string PlayerKey(string clubKey, string playerId) => $"{clubKey}:{playerId}";
}
