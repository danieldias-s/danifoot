using Elifoot.Contracts;
namespace Elifoot.Domain;

/// <summary>Selection priority only: never changes nominal strength, tactics or match physics.</summary>
public static class LineupSelectionEngine
{
    public readonly record struct Performance(int Goals, int Assists);

    public static Dictionary<Guid, Performance> CurrentPerformance(GameWorld? world)
        => world == null ? new() : world.PlayerStatistics.Where(s => s.Season == world.SeasonNumber)
            .GroupBy(s => s.PlayerId).ToDictionary(g => g.Key,
                g => new Performance(g.Sum(s => s.GoalsScored), g.Sum(s => s.Assists)));

    public static double Score(Player player, LineupSelectionMode mode, Performance performance = default)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentException("Modo de escalação inválido.");
        if (mode == LineupSelectionMode.Best) return player.Strength;
        // SeasonGoals already includes cup goals. Legacy counters and buckets overlap; never add them.
        int goals = Math.Max(Math.Max(0, player.SeasonGoals), Math.Max(player.CupGoals, performance.Goals));
        int assists = Math.Max(0, performance.Assists);
        double merit = player.Position switch
        {
            PlayerPosition.A => goals + .5 * assists,
            PlayerPosition.M => .5 * goals + assists,
            PlayerPosition.D => .5 * goals + .5 * assists,
            _ => 0
        };
        return player.Strength + Math.Min(10, merit);
    }

    public static (List<Guid> Starters, List<Guid> Bench) Select(
        IReadOnlyCollection<Player> squad, FormationSpec formation,
        LineupSelectionMode mode = LineupSelectionMode.Automatic,
        IEnumerable<Guid>? currentStarters = null, GameWorld? world = null)
    {
        if (!Enum.IsDefined(mode)) throw new ArgumentException("Modo de escalação inválido.");
        var performance = CurrentPerformance(world);
        var incumbents = currentStarters?.ToHashSet() ?? new HashSet<Guid>();
        var available = squad.Where(p => p.IsAvailableForMatch)
            .OrderByDescending(p => Score(p, mode, performance.GetValueOrDefault(p.Id)))
            .ThenByDescending(p => incumbents.Contains(p.Id))
            .ThenBy(p => p.Id).ToList();
        var remaining = new List<Player>(available);
        var starters = new List<Player>();
        void Pick(PlayerPosition position, int count)
        {
            foreach (var player in remaining.Where(p => p.Position == position).Take(count).ToList())
            { starters.Add(player); remaining.Remove(player); }
        }
        Pick(PlayerPosition.G, 1);
        // Keep the existing emergency-goalkeeper/short-handed policy, without inventing a new tactical rule.
        if (starters.Count == 0 && remaining.Count > 0)
        { starters.Add(remaining[0]); remaining.RemoveAt(0); }
        Pick(PlayerPosition.D, formation.Defenders);
        Pick(PlayerPosition.M, formation.Midfielders);
        Pick(PlayerPosition.A, formation.Attackers);
        foreach (var player in remaining.Where(p => p.Position != PlayerPosition.G).ToList())
        {
            if (starters.Count >= SquadValidator.RequiredStarters) break;
            starters.Add(player); remaining.Remove(player);
        }
        remaining = remaining.OrderByDescending(p => Score(p, mode, performance.GetValueOrDefault(p.Id)))
            .ThenBy(p => p.Id).ToList();
        var bench = new List<Player>();
        var keeper = remaining.FirstOrDefault(p => p.Position == PlayerPosition.G);
        if (keeper != null) { bench.Add(keeper); remaining.Remove(keeper); }
        bench.AddRange(remaining.Take(SquadValidator.MaxBenchSize - bench.Count));
        // Incumbency decides membership; canonical ordering makes repeated selections identical.
        return (starters.OrderBy(p => p.Position)
            .ThenByDescending(p => Score(p, mode, performance.GetValueOrDefault(p.Id)))
            .ThenBy(p => p.Id).Select(p => p.Id).ToList(), bench.Select(p => p.Id).ToList());
    }
}
