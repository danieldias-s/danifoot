using System.Text.Json;
using Elifoot.Domain;

namespace Elifoot.Infrastructure.Catalog.Providers;

public record OpenFootballResults(List<CatalogHistoricalResult> Results, List<string> Teams, int FutureOrUnplayed, string Competition);
public static class OpenFootballResultParser
{
    public static OpenFootballResults Parse(string json, DateTime referenceDate, Func<string, string> resolveTeam)
    {
        using var document = JsonDocument.Parse(json); var root = document.RootElement;
        if (root.Child("matches").ValueKind != JsonValueKind.Array) throw new ArgumentException("OpenFootball sem matches[].");
        var teams = new HashSet<string>(); var result = new List<CatalogHistoricalResult>(); var used = new Dictionary<string, (int Home, int Away)>(); int ignored = 0;
        foreach (var match in root.Child("matches").Rows())
        {
            string home = resolveTeam(match.Required("team1")), away = resolveTeam(match.Required("team2"));
            if (home == away) throw new ArgumentException("A mesma identidade ocupa os dois lados da partida.");
            teams.Add(home); teams.Add(away);
            if (!DateTime.TryParse(match.Text("date"), System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.AssumeUniversal | System.Globalization.DateTimeStyles.AdjustToUniversal, out var date))
                throw new ArgumentException("Partida sem data válida.");
            var scores = match.Child("score").Child("ft").Rows();
            if (date > referenceDate || scores.Count == 0) { ignored++; continue; }
            if (scores.Count != 2 || scores[0].Integer() is not { } hg || scores[1].Integer() is not { } ag || hg < 0 || ag < 0)
                throw new ArgumentException("Placar inválido/incompleto.");
            string key = $"{date:yyyy-MM-dd}|{home}|{away}";
            if (used.TryGetValue(key, out var prior)) { if (prior != (hg, ag)) throw new ArgumentException("Resultados conflitantes para a mesma partida."); continue; }
            used.Add(key, (hg, ag)); result.Add(new(home, away, hg, ag, date));
        }
        return new(result, teams.OrderBy(t => t, StringComparer.Ordinal).ToList(), ignored, root.Required("name"));
    }
}
