using Elifoot.Contracts;

namespace Elifoot.Domain;

public record CatalogHistoricalResult(string HomeClubId, string AwayClubId, int HomeGoals, int AwayGoals, DateTime PlayedAt);
/// <summary>Optional local ranking from authorized results. Not the Opta formula or official ranking.</summary>
public static class DerivedClubRanking
{
    public static List<CatalogRank> Calculate(IEnumerable<string> teamIds, IEnumerable<CatalogHistoricalResult> matches,
        string clubProvider, FootballGender gender, string season, DateTime referenceDate)
    {
        var scores = teamIds.Distinct().ToDictionary(id => id, _ => 1500.0, StringComparer.Ordinal);
        var games = scores.Keys.ToDictionary(id => id, _ => 0, StringComparer.Ordinal);
        foreach (var match in matches.OrderBy(m => m.PlayedAt).ThenBy(m => m.HomeClubId, StringComparer.Ordinal).ThenBy(m => m.AwayClubId, StringComparer.Ordinal))
        {
            if (!scores.ContainsKey(match.HomeClubId) || !scores.ContainsKey(match.AwayClubId) || match.HomeClubId == match.AwayClubId ||
                match.HomeGoals < 0 || match.AwayGoals < 0 || match.PlayedAt > referenceDate) throw new ArgumentException("Resultado incompatível com o snapshot de ranking.");
            double expected = 1 / (1 + Math.Pow(10, (scores[match.AwayClubId] - scores[match.HomeClubId]) / 400));
            double actual = match.HomeGoals == match.AwayGoals ? .5 : match.HomeGoals > match.AwayGoals ? 1 : 0;
            double change = 20 * (actual - expected);
            scores[match.HomeClubId] += change; scores[match.AwayClubId] -= change; games[match.HomeClubId]++; games[match.AwayClubId]++;
        }
        return scores.OrderByDescending(s => s.Value).ThenBy(s => s.Key, StringComparer.Ordinal).Select((s, i) => new CatalogRank(s.Key,
            clubProvider, gender, season, i + 1, "elifoot-derived-elo", referenceDate, s.Value, "derived-elo-v1", games[s.Key] / (games[s.Key] + 20.0),
            $"derived-elo:{gender}:{season}", CatalogDataOrigin.Estimated)).ToList();
    }

    public static List<CatalogRank> CalculateOpenEvidence(IEnumerable<string> teamIds, IEnumerable<CatalogHistoricalResult> matches,
        string clubProvider, FootballGender gender, string season, DateTime referenceDate)
    {
        var verified = matches.ToList();
        var active = verified.SelectMany(m => new[] { m.HomeClubId, m.AwayClubId }).ToHashSet();
        var rankings = Calculate(teamIds, verified, clubProvider, gender, season, referenceDate);
        return rankings.Where(r => active.Contains(r.ClubExternalId)).Select((r, i) => r with {
            Rank = i + 1,
            Source = "openfootball-derived-elo", Method = "derived-elo-open-results-v2-disconnected", Confidence = Math.Min(.35, r.Confidence ?? 0),
            ComparableGroup = $"open-evidence-provisional:{gender}:{season}" }).ToList();
    }
}
