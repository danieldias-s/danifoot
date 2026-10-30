using Elifoot.Contracts;

namespace Elifoot.Domain;

public record PreparedInitialRoster(List<CatalogAthlete> Players, List<string> Issues);
public static class InitialRosterPolicy
{
    public static bool IsRealImport(EligibleCatalogClub club) => club.RawRosterCount != null || club.References.Any(r => !r.IsFixture) || club.Players.Any(p => p.IdentityOrigin != null);
    public static PreparedInitialRoster Prepare(EligibleCatalogClub club, int seed, bool allowGenerated)
    {
        var issues = new List<string>();
        var ordered = club.Players.OrderByDescending(p => p.Strength).ThenBy(p => p.CanonicalPlayerId ?? p.PlayerExternalId, StringComparer.Ordinal).ToList();
        var selected = new List<CatalogAthlete>();
        foreach (var (position, capacity) in new[] { ("G", 2), ("D", 7), ("M", 8), ("A", 7) })
            selected.AddRange(ordered.Where(p => p.Position == position).Take(capacity));
        foreach (var p in ordered) { if (selected.Count >= 24) break; if (!selected.Contains(p)) selected.Add(p); }
        if (allowGenerated)
        {
            int index = 0;
            void Add(string position)
            {
                if (selected.Count >= 24) return;
                string id = $"generated-{club.Key}-{++index}";
                selected.Add(new(id, club.ExternalId, club.Provider, club.Gender, club.Season,
                    $"Atleta fictício {club.ShortName}-{index:00}", position, 16 + (int)((uint)(seed + index * 17) % 16),
                    "generated013b", DateTime.UnixEpoch, $"generated:{club.Key}:{index}", CatalogDataOrigin.Generated,
                    new("generated-v1", CatalogDataOrigin.Generated, 0, id, .5, null)));
            }
            foreach (var (position, minimum) in new[] { ("G", 1), ("D", 3), ("M", 3), ("A", 1) })
                while (selected.Count(p => p.Position == position) < minimum && selected.Count < 24) Add(position);
            string[] cycle = ["D", "M", "A", "G", "D", "M", "A"];
            int cycleIndex = 0;
            while ((selected.Count < 11 || selected.Count(p => p.Position != "G") < 10) && selected.Count < 24)
                Add(cycle[cycleIndex++ % cycle.Length]);
        }
        if (selected.Count < 11) issues.Add($"{club.Name}: apenas {selected.Count} atletas utilizáveis; mínimo 11.");
        if (!selected.Any(p => p.Position == "G")) issues.Add($"{club.Name}: falta goleiro.");
        if (selected.Count(p => p.Position != "G") < 10) issues.Add($"{club.Name}: faltam jogadores de linha.");
        if (!selected.Any(p => p.Position == "D")) issues.Add($"{club.Name}: nenhum defensor utilizável.");
        return new(selected.OrderBy(p => p.Position == "G" ? 0 : p.Position == "D" ? 1 : p.Position == "M" ? 2 : 3)
            .ThenByDescending(p => p.Strength).ThenBy(p => p.PlayerExternalId, StringComparer.Ordinal).ToList(), issues);
    }
}
