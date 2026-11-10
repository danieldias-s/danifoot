using Elifoot.Contracts;

namespace Elifoot.Domain;

/// <summary>Preflight only: no expiration, retroactive decision or monetary mutation.</summary>
public static class SalarySeasonTransitionEngine
{
    public static string? BlockingReason(GameWorld world)
    {
        var pending=world.Economy.SalaryRequests.Where(r=>r.Status==SalaryRequestStatus.PENDING).ToList();
        if(pending.Count==0)return null;
        string clubs=string.Join(", ",pending.Select(r=>world.Clubs.FirstOrDefault(c=>c.Id==r.ClubId)?.Name??r.ClubId.ToString()).Distinct());
        return $"SALARY_PENDING_TRANSITION: Resolva os pedidos espontâneos pendentes, inclusive os antigos, antes de avançar a rodada ({clubs}). Aceite ou recuse pelo clube responsável; no elenco mínimo, aceite ou recomponha o elenco antes da recusa.";
    }

    public static void RequireResolved(GameWorld world)
    {
        if(BlockingReason(world) is string reason)throw new SalarySeasonTransitionException(reason);
    }
}

public sealed class SalarySeasonTransitionException(string message):InvalidOperationException(message);
