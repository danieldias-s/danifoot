using Elifoot.Contracts;

namespace Elifoot.Domain;

/// <summary>Initial placement only. Never calibrates player strength or changes match rules.</summary>
public static class OfflineCatalogRanking
{
    public static bool IsV11(EligibleCatalogClub club) => club.OfflineSource?.Definition.SchemaVersion == "1.1";
    private static bool Comparable(IEnumerable<EligibleCatalogClub> clubs)
    {
        return clubs.Select(c => c.OfflineSource!.V11!.HistoricalRanking)
            .Where(ranking => ranking?.Rating != null)
            .Select(ranking => (Family(ranking!.Source), ranking.ReferenceDate)).Distinct().Count() <= 1;
    }
    private static string? Family(string? source) => source switch
    {
        "OPTA" or "OPTA_INTERPOLATED" => "OPTA",
        "TRANSFERMARKT_SNAPSHOT_VALOR_MERCADO_TOTAL_EUR_MILHOES_DIV_100" or
        "TRANSFERMARKT_SQUAD_TOTAL_EUR_MILLIONS_DIV_100" => "TRANSFERMARKT_EUR_MILLIONS_DIV_100",
        _ => source
    };
    public static List<EligibleCatalogClub> Order(IEnumerable<EligibleCatalogClub> input)
    {
        var clubs = input.ToList();
        if (!clubs.Any(IsV11)) return clubs.OrderBy(c => c.Key, StringComparer.Ordinal).ToList();
        if (clubs.Any(c => !IsV11(c))) throw new ArgumentException("Selecione uma versão por montagem: 1.0 ou 1.1. Não há equivalência de força entre os formatos.");
        return clubs.GroupBy(c => c.OfflineSource!.V11!.TeamStrength)
            .OrderByDescending(g => g.Key).SelectMany(group =>
            {
                bool comparable = Comparable(group);
                return group.OrderByDescending(c => comparable ? c.OfflineSource!.V11!.HistoricalRanking?.Rating : null)
                    .ThenBy(c => c.OfflineSource!.V11!.TieBreakPriority ?? int.MaxValue)
                    .ThenBy(c => c.Key, StringComparer.Ordinal);
            }).ToList();
    }
    public static List<string> Warnings(List<EligibleCatalogClub> input) => input.Where(IsV11)
        .GroupBy(c => c.OfflineSource!.V11!.TeamStrength).Where(g => !Comparable(g))
        .Select(g => $"Força {g.Key}: ranking ausente ou fontes/datas incompatíveis; somente prioridade manual decide. Chaves estáveis apenas apresentam empates.")
        .ToList();

    public static List<string> BoundaryErrors(List<EligibleCatalogClub> ordered, List<WorldPreviewClub> assigned,
        List<ClubAssignment> manual)
    {
        var errors = new List<string>();
        foreach (var group in ordered.Where(IsV11).GroupBy(c => c.OfflineSource!.V11!.TeamStrength))
        {
            bool comparable = Comparable(group);
            foreach (var tied in group.GroupBy(c => (Rating: comparable ? c.OfflineSource!.V11!.HistoricalRanking?.Rating : null,
                         Priority: c.OfflineSource!.V11!.TieBreakPriority)))
            {
                var keys = tied.Select(c => c.Key).ToHashSet(StringComparer.Ordinal);
                if (assigned.Where(a => keys.Contains(a.Club.Key)).Select(a => a.Division).Distinct().Count() > 1 &&
                    !keys.All(k => manual.Any(a => a.ClubKey == k)))
                    errors.Add("Empate atravessa vagas entre divisões: defina tieBreakPriority ou atribua explicitamente a divisão de todos os clubes empatados: " + string.Join(", ", keys));
            }
        }
        return errors;
    }
}
