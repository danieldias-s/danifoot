using Elifoot.Contracts;

namespace Elifoot.Domain;

/// <summary>The roster in a validated local file belongs to that club version and year.</summary>
public static class HistoricalCatalogPolicy
{
    public static bool MatchesScope(WorldCreatorRequest request, EligibleCatalogClub club) =>
        club.Gender == request.Gender && request.Countries.Contains(club.CountryCode) &&
        (request.CatalogYears?.Contains(club.Season) ?? club.Season == request.Season);

    public static bool IsRosterPlayer(CatalogAthlete player, EligibleCatalogClub club) =>
        IsLocalClub(club) && player.ClubProvider == club.Provider && player.ClubExternalId == club.ExternalId &&
        player.IdentityOrigin == CatalogDataOrigin.Real && player.Gender == club.Gender && player.Season == club.Season &&
        player.Position is "G" or "D" or "M" or "A" && !string.IsNullOrWhiteSpace(player.PlayerName);

    /// <summary>Classify after filtering the chosen countries and years, so the editor and preview share one order.</summary>
    public static List<EligibleCatalogClub> Classify(IEnumerable<EligibleCatalogClub> clubs) => clubs
        .OrderBy(club => SourceRank(club)?.Rank ?? club.Rank ?? int.MaxValue)
        .ThenByDescending(club => SourceRank(club)?.Score ?? double.MinValue)
        .ThenBy(club => club.Key, StringComparer.Ordinal)
        .Select((club, index) =>
        {
            var sourceRank = SourceRank(club);
            int rank = index + 1;
            return club with
            {
                Rank = rank,
                SourceRanking = sourceRank,
                RankingInfo = sourceRank,
                InitialProfile = new(WorldCatalogRules016B.PowerAt(index), rank, sourceRank?.Score,
                    sourceRank?.Source ?? LocalHistoricalCatalogSource.Provider,
                    sourceRank?.UpdatedAt ?? DateTime.UnixEpoch, sourceRank?.Confidence ?? 0)
            };
        }).ToList();

    public static string DisplayName(string name, string year, IReadOnlyCollection<string>? selectedYears)
    {
        if (selectedYears is not { Count: > 1 } || string.IsNullOrWhiteSpace(name)) return name;
        string suffix = $" ({year})";
        return name.EndsWith(suffix, StringComparison.Ordinal) ? name : name + suffix;
    }

    internal static string BaseName(string name, string year, IReadOnlyCollection<string>? selectedYears)
    {
        if (selectedYears is not { Count: > 1 } || string.IsNullOrWhiteSpace(name)) return name;
        string suffix = $" ({year})";
        return name.EndsWith(suffix, StringComparison.Ordinal) ? name[..^suffix.Length] : name;
    }

    internal static bool IsLocalClub(EligibleCatalogClub club) =>
        club.Provider == LocalHistoricalCatalogSource.Provider && PlayableWorldPolicy.Verified(club);

    internal static List<CatalogAthlete> Roster(EligibleCatalogClub club) =>
        club.Players.Where(player => IsRosterPlayer(player, club))
            .Select(player => player with
            {
                CanonicalPlayerId = LocalHistoricalCatalogSource.PlayerKey(club.Key, player.PlayerExternalId),
                PlayerOrigin = WorldPlayerOrigin.REAL_HISTORICAL_CATALOG,
                StrengthEstimate = new(InitialPlayerStrength.Version, CatalogDataOrigin.Estimated, 0, "", .5, null)
            }).ToList();

    internal static EligibleCatalogClub PrepareRoster(EligibleCatalogClub club, int seed)
    {
        // Local files supply the complete roster, subject to the existing 24-player game limit.
        var selected = InitialRosterPolicy.Prepare(club, seed, allowGenerated: false).Players;
        return club with
        {
            Players = selected,
            InitialProfile = club.InitialProfile! with
            {
                TargetInitialSquadSize = selected.Count,
                SquadSelectionMethod = "local-historical-file"
            }
        };
    }

    private static CatalogRank? SourceRank(EligibleCatalogClub club) => club.SourceRanking ?? club.RankingInfo;
}
