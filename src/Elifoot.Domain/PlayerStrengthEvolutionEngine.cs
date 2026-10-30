using Elifoot.Contracts;

namespace Elifoot.Domain;

/// <summary>
/// Preserves the project's 005 seasonal policy, with the verified 1..50 scale and an event journal.
/// This is not a reconstruction of FUN_17fb_4a41: its performance evaluator and injury loss are unknown.
/// See docs/ORIGINAL_PLAYER_FORCE_RESEARCH_012.md before changing the policy or adding a round caller.
/// </summary>
public static class PlayerStrengthEvolutionEngine
{
    public const string Rule = "LEGACY_SEASONAL_005_SCALE_FIX_012";

    public static void ApplySeasonEnd(GameWorld world)
    {
        if (ClassicForceEvolutionEngine.IsActive(world)) return;
        if (world.EvolvedPlayerStrengthSeasons.Contains(world.SeasonNumber)) return;
        world.PlayerStrengthHistoryStartSeason ??= world.SeasonNumber;
        world.PlayerStrengthHistoryStartRound ??= world.RoundNumber;
        var clubs = world.Clubs.ToDictionary(c => c.Id);

        for (int i = 0; i < world.Players.Count; i++)
        {
            var p = world.Players[i];
            Club? club = p.ClubId is Guid id ? clubs.GetValueOrDefault(id) : null;
            var rng = DeterministicRng.FromContext(world.Seed, world.SeasonNumber, 100, i + 1, p.Strength, 888);
            // Legacy balance targets, not historically verified division caps.
            int targetMean = club?.DivisionTier switch
            {
                DivisionTier.PrimeiraDivisao => 37,
                DivisionTier.SegundaDivisao => 30,
                DivisionTier.TerceiraDivisao => 25,
                DivisionTier.QuartaDivisao => 21,
                _ => 17
            };
            int totalGoals = p.SeasonGoals + p.CupGoals;
            int delta = 0;
            string detail = "";
            if (totalGoals >= 6 || (p.SeasonMatches >= 12 && p.Strength < targetMean + 2 && rng.NextBool(0.42)))
            {
                delta = 1;
                detail = totalGoals >= 6 ? "LEGACY_GOALS" : "LEGACY_APPEARANCES";
                if (totalGoals >= 11 && p.Strength < 46 && rng.NextBool(0.35)) delta = 2;
            }
            else if (p.Strength > targetMean + 6 && rng.NextBool(0.45))
            {
                delta = -1;
                detail = "LEGACY_ABOVE_DIVISION_TARGET";
            }
            else if (p.Strength < targetMean - 4 && rng.NextBool(0.55))
            {
                delta = 1;
                detail = "LEGACY_BELOW_DIVISION_TARGET";
            }
            else if (p.SeasonMatches <= 2 && p.Strength > 22 && rng.NextBool(0.25))
            {
                delta = -1;
                detail = "LEGACY_LOW_APPEARANCES";
            }

            int before = p.Strength;
            int after = Math.Clamp(before + delta, 1, 50);
            if (delta == 0) continue;
            p.Strength = after;
            p.MarketPrice = MarketAndEconomyEngine.CalculateFairValue(p);
            p.SalaryPerRound = MarketAndEconomyEngine.CalculateFairSalaryPerRound(p);
            if (after == before) continue;
            world.PlayerStrengthHistory.Add(new(
                $"player_strength:S{world.SeasonNumber}:P{p.Id}", p.Id, world.SeasonNumber, world.RoundNumber,
                p.ClubId, club?.Name ?? "Sem clube", club?.DivisionTier, before, after,
                after > before ? "NORMAL_PROGRESS" : "NORMAL_DECLINE", null, Rule, detail));
        }
        world.EvolvedPlayerStrengthSeasons.Add(world.SeasonNumber);
    }
}
