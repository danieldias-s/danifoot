using System.Text.RegularExpressions;
using Elifoot.Contracts;

namespace Elifoot.Domain;

public static class OfflineCatalogRules
{
    public const string Version = "1.0";
    public const int MaximumPlayers = 23; // Server limit remains 24.
    private static readonly HashSet<string> Countries = ("AD AE AF AG AI AL AM AO AQ AR AS AT AU AW AX AZ BA BB BD BE BF BG BH BI BJ BL BM BN BO BQ BR BS BT BV BW BY BZ CA CC CD CF CG CH CI CK CL CM CN CO CR CU CV CW CX CY CZ DE DJ DK DM DO DZ EC EE EG EH ER ES ET FI FJ FK FM FO FR GA GB GD GE GF GG GH GI GL GM GN GP GQ GR GS GT GU GW GY HK HM HN HR HT HU ID IE IL IM IN IO IQ IR IS IT JE JM JO JP KE KG KH KI KM KN KP KR KW KY KZ LA LB LC LI LK LR LS LT LU LV LY MA MC MD ME MF MG MH MK ML MM MN MO MP MQ MR MS MT MU MV MW MX MY MZ NA NC NE NF NG NI NL NO NP NR NU NZ OM PA PE PF PG PH PK PL PM PN PR PS PT PW PY QA RE RO RS RU RW SA SB SC SD SE SG SH SI SJ SK SL SM SN SO SR SS ST SV SX SY SZ TC TD TF TG TH TJ TK TL TM TN TO TR TT TV TW TZ UA UG UM US UY UZ VA VC VE VG VI VN VU WF WS YE YT ZA ZM ZW").Split(' ').ToHashSet(StringComparer.Ordinal);
    public static bool Country(string? code) => code != null && Countries.Contains(code);
    public static bool Id(string? id) => id != null && Regex.IsMatch(id, @"\A[A-Za-z0-9][A-Za-z0-9_-]{0,79}\z");
    public static bool Text(string? value, int maximum = 160) => !string.IsNullOrWhiteSpace(value) && value.Length <= maximum;

    public static List<OfflineCatalogIssue> ValidateClub(OfflineClub c, string path)
    {
        var issues = new List<OfflineCatalogIssue>();
        bool ready = c.Status == "READY";
        void Check(bool valid, string code, string field, string message, bool structural = false)
        { if (!valid) issues.Add(new(code, path + ":" + field, message, structural || ready)); }
        Check(c.SchemaVersion == Version, "SCHEMA_VERSION", "schemaVersion", "Versão suportada: 1.0.", true);
        Check(c.Status is "DRAFT" or "READY", "STATUS", "status", "Use DRAFT ou READY.", true);
        Check(Id(c.CatalogClubId), "CLUB_ID", "catalogClubId", "Identificador estável obrigatório.", true);
        Check(Country(c.Country), "COUNTRY", "country", "Código ISO de país inválido.", true);
        Check(c.Year is >= 1 and <= 9999, "YEAR", "year", "Ano entre 1 e 9999.", true);
        Check(Text(c.Name) && Text(c.ShortName, 80) && Text(c.Abbreviation, 12), "CLUB_NAMES", "name", "Preencha nome, nome curto e sigla independentes.");
        Check(c.Colors is { Count: 2 } && c.Colors.All(x => x != null && Regex.IsMatch(x, @"\A#[0-9a-fA-F]{6}\z")), "COLORS", "colors", "Informe duas cores #RRGGBB.");
        Check(c.Stadium != null && Text(c.Stadium.Name) && c.Stadium.Capacity is > 0 and <= 1_000_000, "STADIUM", "stadium", "Estádio e capacidade 1..1000000 obrigatórios.");
        Check(c.InitialDivision == null || c.InitialDivision is >= 1 and <= 5, "DIVISION", "initialDivision", "Divisão opcional: 1..5.", true);
        var coach = c.Coach;
        Check(coach != null && Id(coach.Id) && Text(coach.Name) && Country(coach.Nationality), "COACH", "coach", "Treinador exige ID, nome e nacionalidade ISO.");
        foreach (var (context, list) in new[] { ("home", coach?.PreferredTactics?.Home), ("away", coach?.PreferredTactics?.Away) })
            Check(list is { Count: 2 } && list.Distinct(StringComparer.Ordinal).Count() == 2 && list.All(t => t != null && TacticalFormations.TryGet(t, true, out var spec) && spec.Code == t),
                "TACTICS", "coach.preferredTactics." + context, "Exige exatamente duas formações distintas e válidas por contexto.");
        var players = c.Players ?? [];
        Check(players.Count is >= 11 and <= MaximumPlayers && players.Any(p => p?.Position == "G") && players.Count(p => p?.Position is "D" or "M" or "A") >= 10,
            "ROSTER", "players", "Elenco exige 11..23 atletas, pelo menos 1 G e 10 de linha; nenhum atleta será inventado.");
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (int n = 0; n < players.Count; n++)
        {
            var p = players[n]; string field = $"players[{n}]";
            if (p == null) { Check(false, "PLAYER", field, "Jogador nulo.", true); continue; }
            Check(Id(p.Id), "PLAYER_ID", field, "ID estável obrigatório.");
            Check(p.Id == null || ids.Add(p.Id), "DUPLICATE_PLAYER", field, "ID de jogador duplicado.", true);
            Check(Text(p.Name), "PLAYER_NAME", field, "Nome obrigatório.");
            Check(p.Position is "G" or "D" or "M" or "A", "POSITION", field, "Posição: G, D, M ou A.");
            Check(p.Strength is >= 1 and <= 50, "STRENGTH", field, "Força obrigatória: 1..50.");
            Check(Country(p.Nationality), "NATIONALITY", field, "Nacionalidade ISO obrigatória.");
            Check(p.Behavior is >= 0 and <= 5, "BEHAVIOR", field, "Comportamento obrigatório: 0..5.");
            Check(p.IsStar.HasValue, "STAR", field, "isStar deve ser true ou false.");
        }
        return issues;
    }
}
