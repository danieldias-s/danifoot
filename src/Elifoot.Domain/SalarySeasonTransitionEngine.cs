using Elifoot.Contracts;

namespace Elifoot.Domain;

/// <summary>Preflight only: no expiration, retroactive decision or monetary mutation.</summary>
public static class SalarySeasonTransitionEngine
{
    public static string? BlockingReason(GameWorld world, Guid? clubId = null)
    {
        if (!world.Economy.SalaryRequests.Any(r => r.IsUnresolved && (clubId == null || r.ClubId == clubId)))
            return null;
        return clubId == null
            ? "SALARY_PENDING_TRANSITION: Aguardando decisões salariais para avançar a rodada."
            : "SALARY_PENDING_TRANSITION: Resolva o pedido salarial do seu clube. No elenco mínimo, aceite ou recomponha o elenco antes da recusa.";
    }

    public static void RequireResolved(GameWorld world)
    {
        if(BlockingReason(world) is string reason)throw new SalarySeasonTransitionException(reason);
    }
}

public sealed class SalarySeasonTransitionException(string message):InvalidOperationException(message);
