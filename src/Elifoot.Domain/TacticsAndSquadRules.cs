using Elifoot.Contracts;

namespace Elifoot.Domain;

public record FormationSpec(
    string Code,
    int Defenders,
    int Midfielders,
    int Attackers,
    bool IsClassic505 = false)
{
    public int TotalOutfield => Defenders + Midfielders + Attackers;
}

public static class TacticalFormations
{
    public static readonly IReadOnlyDictionary<string, FormationSpec> All = new Dictionary<string, FormationSpec>(StringComparer.OrdinalIgnoreCase)
    {
        ["4-4-2"] = new("4-4-2", 4, 4, 2),
        ["4-3-3"] = new("4-3-3", 4, 3, 3),
        ["3-4-3"] = new("3-4-3", 3, 4, 3),
        ["5-3-2"] = new("5-3-2", 5, 3, 2),
        ["5-4-1"] = new("5-4-1", 5, 4, 1),
        ["4-5-1"] = new("4-5-1", 4, 5, 1),
        ["3-5-2"] = new("3-5-2", 3, 5, 2),
        ["4-2-4"] = new("4-2-4", 4, 2, 4),
        ["3-6-1"] = new("3-6-1", 3, 6, 1),
        ["5-0-5"] = new("5-0-5", 5, 0, 5, IsClassic505: true)
    };

    public static bool TryGet(string code, bool allowClassic505, out FormationSpec spec)
    {
        if (All.TryGetValue(code?.Trim() ?? string.Empty, out var found))
        {
            if (found.IsClassic505 && !allowClassic505)
            {
                spec = default!;
                return false;
            }
            spec = found;
            return true;
        }
        var parts = (code ?? string.Empty).Split('-');
        if (parts.Length == 3 && parts.All(p => p.Length is >= 1 and <= 2 && p.All(char.IsAsciiDigit))
            && int.TryParse(parts[0], out int defenders) && int.TryParse(parts[1], out int midfielders)
            && int.TryParse(parts[2], out int attackers)
            && defenders <= 10 && midfielders <= 10 && attackers <= 10
            && defenders + midfielders + attackers == 10
            && code == $"{defenders}-{midfielders}-{attackers}"
            && (allowClassic505 || code != "5-0-5"))
        {
            spec = new(code, defenders, midfielders, attackers);
            return true;
        }
        spec = default!;
        return false;
    }

    public static FormationSpec Resolve(string? code, bool allowClassic505 = true)
        => TryGet(code ?? string.Empty, allowClassic505, out var spec) ? spec : All["4-4-2"];

    public static IReadOnlyList<FormationSpec> GetAvailable(bool allowClassic505)
        => All.Values.Where(f => allowClassic505 || !f.IsClassic505).ToList();
}

public static class SquadValidator
{
    public const int MinSquadSize = 11;
    public const int MaxSquadSize = 24;
    public const int RequiredStarters = 11;
    public const int MaxBenchSize = 9;

    public static (bool IsValid, string? ErrorMessage) ValidateClubSquadLimits(IReadOnlyCollection<Player> squad)
    {
        if (squad.Count < MinSquadSize)
            return (false, $"O elenco possui {squad.Count} jogadores; o mínimo permitido é {MinSquadSize}.");
        if (squad.Count > MaxSquadSize)
            return (false, $"O elenco possui {squad.Count} jogadores; o máximo permitido é {MaxSquadSize}.");

        int goalkeepers = squad.Count(p => p.Position == PlayerPosition.G);
        int outfield = squad.Count(p => p.Position != PlayerPosition.G);

        if (goalkeepers < 1)
            return (false, "O elenco precisa ter pelo menos 1 goleiro (G).");
        if (outfield < 10)
            return (false, $"O elenco precisa ter pelo menos 10 jogadores de linha (atual: {outfield}).");

        return (true, null);
    }

    public static (bool CanRemove, string? ErrorMessage) CanRemovePlayerFromSquad(IReadOnlyCollection<Player> currentSquad, Guid playerToRemoveId)
    {
        var remaining = currentSquad.Where(p => p.Id != playerToRemoveId).ToList();
        return ValidateClubSquadLimits(remaining);
    }

    public static (bool CanAdd, string? ErrorMessage) CanAddPlayerToSquad(IReadOnlyCollection<Player> currentSquad)
    {
        if (currentSquad.Count >= MaxSquadSize)
            return (false, $"O elenco já atingiu o limite máximo de {MaxSquadSize} jogadores.");
        return (true, null);
    }

    public static int RequiredStartersForSquad(IReadOnlyCollection<Player> squad)
        => Math.Min(11,squad.Count(p=>p.IsAvailableForMatch&&p.Position!=PlayerPosition.G)
            +(squad.Any(p=>p.IsAvailableForMatch&&p.Position==PlayerPosition.G)?1:0));

    public static (bool IsValid, string? ErrorMessage) ValidatePreMatchLineup(
        string formationCode,
        IReadOnlyList<Guid> starterIds,
        IReadOnlyList<Guid> benchIds,
        IReadOnlyCollection<Player> clubSquad,
        bool allowClassic505)
    {
        if (!TacticalFormations.TryGet(formationCode, allowClassic505, out var spec))
            return (false, $"Formação tática inválida ou não permitida: '{formationCode}'.");

        int required=RequiredStartersForSquad(clubSquad);
        if(required<1)return(false,"O clube precisa de ao menos um jogador apto para disputar a partida.");
        if (starterIds.Count != required)
            return (false, $"A escalação titular deve ter exatamente {required} jogadores aptos (enviado: {starterIds.Count}).");

        if (benchIds.Count > MaxBenchSize)
            return (false, $"O banco de reservas comporta no máximo {MaxBenchSize} jogadores (enviado: {benchIds.Count}).");

        if (starterIds.Distinct().Count() != starterIds.Count)
            return (false, "Existem jogadores duplicados na escalação titular.");

        if (benchIds.Distinct().Count() != benchIds.Count)
            return (false, "Existem jogadores duplicados no banco de reservas.");

        if (starterIds.Intersect(benchIds).Any())
            return (false, "Um jogador não pode ser titular e reserva ao mesmo tempo.");

        var squadById = clubSquad.ToDictionary(p => p.Id);
        int availableGkInSquad = clubSquad.Count(p => p.Position == PlayerPosition.G && p.IsAvailableForMatch);
        int availableOutfieldInSquad = clubSquad.Count(p => p.Position != PlayerPosition.G && p.IsAvailableForMatch);

        var starters = new List<Player>(starterIds.Count);
        foreach (var id in starterIds)
        {
            if (!squadById.TryGetValue(id, out var p))
                return (false, $"Jogador titular {id} não pertence ao elenco do clube.");
            if (!p.IsAvailableForMatch)
            {
                return (false, $"O jogador {p.DisplayName} está lesionado ou suspenso e não pode ser escalado.");
            }
            starters.Add(p);
        }

        foreach (var id in benchIds)
        {
            if (!squadById.TryGetValue(id, out var p))
                return (false, $"Jogador reserva {id} não pertence ao elenco do clube.");
            if (!p.IsAvailableForMatch)
                return (false, $"O jogador reserva {p.DisplayName} está lesionado ou suspenso.");
        }

        int gkCount = starters.Count(p => p.Position == PlayerPosition.G);
        if (gkCount != (availableGkInSquad>0?1:0))
            return (false, $"A escalação titular deve conter exatamente 1 goleiro (G). Encontrado: {gkCount}.");

        if (spec.TotalOutfield != 10)
            return (false, "A soma dos setores de linha da tática deve ser 10.");

        return (true, null);
    }

    public static bool IsFreeIntervalState(RoundState state) =>
        state is RoundState.PREPARING
            or RoundState.HALFTIME_PAUSED
            or RoundState.EXTRA_TIME_BREAK
            or RoundState.EXTRA_TIME_HALFTIME;
}
