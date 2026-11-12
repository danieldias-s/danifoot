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
        var keepers = ordered.Where(p => p.Position == "G").Take(keepG).ToList();
        // Search feasible rosters rather than sacrificing a protected athlete to the first formation.
        // This does not change the coach's actual preferences or the world's tactical rules.
        var tactics = club.OfflineSource.Definition.Coach!.PreferredTactics!;
        var codes = tactics.Home!.Concat(tactics.Away!).Concat(TacticalFormations.GetAvailable(false).Select(f => f.Code)).Distinct();
        List<CatalogAthlete>? selected = null;
        List<long>? bestScore = null;
        List<long> Preservation(List<CatalogAthlete> proposed)
        {
            var ids = proposed.Select(p => p.PlayerExternalId).ToHashSet();
            return new List<long> {
                proposed.Count(p => source[p.PlayerExternalId].IsStar == true),
                proposed.Count(p => byId.GetValueOrDefault(p.PlayerExternalId)?.IsProdigy == true),
                proposed.Count(p => byId.GetValueOrDefault(p.PlayerExternalId)?.ImportantStarter == true),
                proposed.Sum(p => (long)(byId.GetValueOrDefault(p.PlayerExternalId)?.Minutes ?? 0))
            }.Concat(ordered.Select(p => ids.Contains(p.PlayerExternalId) ? 1L : 0L)).ToList();
        }
        foreach (var code in codes)
        {
            if (!TacticalFormations.TryGet(code, false, out var formation)) continue;
            var trial = keepers.ToList();
            foreach (var (position, count) in new[] { ("D",formation.Defenders), ("M",formation.Midfielders), ("A",formation.Attackers) })
                trial.AddRange(ordered.Where(p => p.Position == position).Take(count));
            if (trial.Count != keepG+10) continue;
            var used = trial.Select(p => p.PlayerExternalId).ToHashSet();
            trial.AddRange(ordered.Where(p => p.Position != "G" && !used.Contains(p.PlayerExternalId)).Take(target-trial.Count));
            var score = Preservation(trial);
            bool better = bestScore == null;
            if (bestScore != null)
                for (int i=0;i<score.Count;i++)
                    if (score[i] != bestScore[i]) { better=score[i]>bestScore[i]; break; }
            if (better) { selected=trial; bestScore=score; }
        }
        if (selected == null) throw new ArgumentException($"{club.Name}: posições insuficientes para uma formação nativa habilitada.");
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
