using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Elifoot.Contracts;

namespace Elifoot.Domain;

/// <summary>Creation-time selection only. Source athletes and force generators remain unchanged.</summary>
public static class OfflineRosterV16
{
    public const string Version = "offline-roster-1.6";
    internal static ulong Draw(int seed, string club, string token) => BinaryPrimitives.ReadUInt64LittleEndian(
        SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new { Version, seed, club, token })));

    public static WorldPreviewClub Select(WorldPreviewClub entry, int seed, List<OfflineRetentionEvidence>? research = null)
    {
        var club = entry.Club;
        if (!OfflineCatalogRanking.IsV11(club)) throw new ArgumentException("Corte V1.6 exige catálogo 1.1; 1.0 conserva valores literais.");
        var candidates = club.Players;
        var original = club.OfflineSource!.Definition.Players!;
        if (candidates.Select(p => p.PlayerExternalId).Distinct(StringComparer.OrdinalIgnoreCase).Count() != candidates.Count)
            throw new ArgumentException($"{club.Name}: IDs de candidatos duplicados.");
        var evidence = (research ?? []).Where(e => e.ClubKey == club.Key).ToList();
        if (evidence.Select(e => e.PlayerId).Distinct().Count() != evidence.Count || evidence.Any(e =>
            !OfflineCatalogRules.Text(e.Evidence, 2000) || e.Minutes < 0 || candidates.All(p => p.PlayerExternalId != e.PlayerId)))
            throw new ArgumentException($"{club.Name}: pesquisa de retenção inválida ou sem evidência.");
        var byId = evidence.ToDictionary(e => e.PlayerId);
        var source = original.ToDictionary(p => p.Id!);
        var (minimum, maximum) = entry.Division switch { 1 => (20,21), 2 or 3 => (19,20), 4 => (17,19), 5 => (13,23), _ => throw new ArgumentException("Divisão inválida.") };
        int target = Math.Min(candidates.Count, minimum + (int)(Draw(seed, club.Key, "size") % (uint)(maximum-minimum+1)));
        int availableG = candidates.Count(p => p.Position == "G");
        int keepG = entry.Division < 5 ? 2 : target == 23 && availableG >= 3 ? 3 : Math.Min(2, availableG);
        if (target < minimum || availableG < keepG || keepG == 0 || candidates.Count-availableG < target-keepG)
            throw new ArgumentException($"{club.Name}: candidatos insuficientes para divisão {entry.Division} ({minimum}..{maximum}, {keepG} G). Não serão inventados atletas.");

        // Lexicographic preservation, no unapproved numeric weights or inferred ages/minutes.
        var ordered = candidates.OrderByDescending(p => source[p.PlayerExternalId].IsStar == true)
            .ThenByDescending(p => byId.GetValueOrDefault(p.PlayerExternalId)?.IsProdigy == true)
            .ThenByDescending(p => byId.GetValueOrDefault(p.PlayerExternalId)?.ImportantStarter == true)
            .ThenByDescending(p => byId.GetValueOrDefault(p.PlayerExternalId)?.Minutes ?? -1)
            .ThenByDescending(p => p.Strength).ThenBy(p => Draw(seed, club.Key, p.PlayerExternalId))
            .ThenBy(p => p.PlayerExternalId, StringComparer.Ordinal).ToList();
        var selected = ordered.Where(p => p.Position == "G").Take(keepG).ToList();
        // Guarantee at least one playable native formation, favoring the verified coach's formations.
        var tactics = club.OfflineSource.Definition.Coach!.PreferredTactics!;
        var codes = tactics.Home!.Concat(tactics.Away!).Concat(new[] { "4-4-2", "4-3-3", "3-4-3", "5-3-2", "4-5-1", "3-5-2", "4-2-4", "3-6-1", "5-4-1", "5-0-5" }).Distinct();
        List<CatalogAthlete>? functional = null;
        foreach (var code in codes)
        {
            var parts = code.Split('-').Select(int.Parse).ToArray();
            var trial = new List<CatalogAthlete>();
            foreach (var (position, count) in new[] { ("D",parts[0]), ("M",parts[1]), ("A",parts[2]) })
                trial.AddRange(ordered.Where(p => p.Position == position).Take(count));
            if (trial.Count == 10) { functional = trial; break; }
        }
        if (functional == null) throw new ArgumentException($"{club.Name}: posições insuficientes para uma formação nativa.");
        // Required positions reserve slots; remaining places follow the preservation order.
        selected.AddRange(functional);
        selected.AddRange(ordered.Where(p => p.Position != "G" && selected.All(s => s.PlayerExternalId != p.PlayerExternalId)).Take(target-selected.Count));
        var keys = selected.Select(p => p.PlayerExternalId).ToHashSet();
        var retained = candidates.Where(p => keys.Contains(p.PlayerExternalId)).ToList();
        var excluded = candidates.Where(p => !keys.Contains(p.PlayerExternalId)).Select(p => p.PlayerExternalId);
        return entry with { Club = club with {
            Players = retained, ReferencePlayers = candidates.ToList(), RawRosterCount = candidates.Count,
            CoverageWarnings = (club.CoverageWarnings ?? []).Concat(new[] { $"{Version}: divisão {entry.Division}; selecionados {retained.Count}; cortados: {string.Join(",", excluded)}. Sem pesquisa, prodígio/titular não é presumido." }).ToList(),
            InitialProfile = club.InitialProfile == null ? null : club.InitialProfile with { TargetInitialSquadSize = retained.Count, SquadSelectionMethod = Version }
        }};
    }
}
