"""Reconcile MatchEngine evidence and generate the two audit CSVs and report."""

import argparse
import csv
import json
from collections import Counter
from hashlib import sha256
from pathlib import Path
from zipfile import ZIP_DEFLATED, ZipFile


PREFIX = "DISTRICT_VS_DIV1_DIV2"
DIVISIONS = {"PrimeiraDivisao": "1ª Divisão", "SegundaDivisao": "2ª Divisão"}
PHASES = {
    "CupRoundOf128": "Fase de 128 clubes",
    "CupRoundOf64": "Fase de 64 clubes",
    "CupRoundOf32": "Fase de 32 clubes",
    "CupRoundOf16": "Oitavas de final",
    "CupQuarterFinal": "Quartas de final",
    "CupSemiFinal": "Semifinal",
    "CupFinal": "Final",
}
POSITIONS = {"G": "Goleiro", "D": "Defensor", "M": "Meia", "A": "Atacante"}


def json_lines(path):
    with path.open(encoding="utf-8") as stream:
        yield from (json.loads(line) for line in stream)


def matchup(source):
    home, away = source["homeDivision"], source["awayDivision"]
    if home == "Distrital" and away in DIVISIONS:
        return "home", "away", DIVISIONS[away]
    if away == "Distrital" and home in DIVISIONS:
        return "away", "home", DIVISIONS[home]
    return None


def team(detail, side):
    kickoff = detail["kickoff"][side]
    roster = {p["id"]: p for p in kickoff["squad"]}
    assert len(roster) == len(kickoff["squad"])
    xi = [roster[player_id] for player_id in kickoff["starters"]]
    assert 1 <= len(xi) <= 11 and len({p["id"] for p in xi}) == len(xi)
    assert all(1 <= p["strength"] <= 50 and p["position"] in POSITIONS for p in roster.values())
    fit = [p for p in roster.values() if not (p["injuryDays"] or p["injuryRounds"] or p["suspensionMatches"])]
    # SquadValidator.RequiredStartersForSquad counts at most one available goalkeeper.
    required = min(11, sum(p["position"] != "G" for p in fit) + any(p["position"] == "G" for p in fit))
    assert len(xi) == required and all(p in fit for p in xi)
    values = [p["strength"] for p in xi]
    return {
        **kickoff, "roster": roster, "xi": xi, "avg": sum(values) / len(xi),
        "min": min(values), "max": max(values), "finalFormation": detail[f"{side}FinalFormation"],
        "squadAvg": sum(p["strength"] for p in roster.values()) / len(roster),
    }


def make_row(detail):
    source = detail["result"]
    match = source["match"]
    district_side, elite_side, division = matchup(source)
    district, elite = team(detail, district_side), team(detail, elite_side)
    home_goals, away_goals = match["homeGoals"], match["awayGoals"]
    dg, eg = match[f"{district_side}Goals"], match[f"{elite_side}Goals"]
    home_pen, away_pen = source["homePenaltyGoals"], source["awayPenaltyGoals"]
    shootout = home_pen is not None
    assert shootout == (away_pen is not None)
    assert source["minutes"] in (90, 120)
    assert source["minutes"] == 120 or not shootout
    winner = match["winnerClubId"]
    assert winner in (match["homeClubId"], match["awayClubId"])
    if shootout:
        assert home_goals == away_goals and home_pen != away_pen
        winner_side = "home" if home_pen > away_pen else "away"
    else:
        assert home_goals != away_goals
        winner_side = "home" if home_goals > away_goals else "away"
    assert winner == match[f"{winner_side}ClubId"]
    for event in detail["events"]:
        assert 1 <= event["minute"] <= source["minutes"]
        assert event["clubId"] in (match["homeClubId"], match["awayClubId"])
        if event["eventType"] == "Substitution":
            side_team = district if event["clubId"] == match[f"{district_side}ClubId"] else elite
            assert event["primaryPlayerId"] in side_team["roster"]
            assert event["secondaryPlayerId"] in side_team["roster"]
    return {
        "Season": match["season"], "Round": match["round"], "Competition": "Taça",
        "Phase": PHASES[match["competition"]], "CompetitionCode": match["competition"],
        "MatchId": match["matchId"],
        "DistrictClub": match[f"{district_side}Club"], "OpponentClub": match[f"{elite_side}Club"],
        "OpponentDivision": division, "DistrictGoals": dg, "OpponentGoals": eg,
        "Result": "V" if dg > eg else "D" if dg < eg else "E",
        "PenaltyWinner": match[f"{winner_side}Club"] if shootout else "",
        "DistrictAverageStrength": round(district["avg"], 6), "OpponentAverageStrength": round(elite["avg"], 6),
        "HomeClub": match["homeClub"], "AwayClub": match["awayClub"],
        "DistrictClubId": match[f"{district_side}ClubId"], "OpponentClubId": match[f"{elite_side}ClubId"],
        "DistrictIsHome": district_side == "home", "KnockoutWinner": match[f"{winner_side}Club"],
        "DistrictAdvanced": winner_side == district_side,
        "DistrictPenaltyGoals": source[f"{district_side}PenaltyGoals"],
        "OpponentPenaltyGoals": source[f"{elite_side}PenaltyGoals"], "Minutes": source["minutes"],
        "EliteMinusDistrictStrength": round(elite["avg"] - district["avg"], 6),
        "DistrictStarterCount": len(district["xi"]), "OpponentStarterCount": len(elite["xi"]),
        "DistrictMinStrength": district["min"], "DistrictMaxStrength": district["max"],
        "OpponentMinStrength": elite["min"], "OpponentMaxStrength": elite["max"],
        "DistrictSquadAverageStrength": round(district["squadAvg"], 6),
        "OpponentSquadAverageStrength": round(elite["squadAvg"], 6),
        "DistrictFormation": district["formation"], "OpponentFormation": elite["formation"],
        "DistrictMoraleAtKickoff": district["morale"], "OpponentMoraleAtKickoff": elite["morale"],
        "DistrictHomeMultiplier": district["homeMultiplier"], "OpponentHomeMultiplier": elite["homeMultiplier"],
    }, district, elite


def pct(count, total):
    return 100 * count / total if total else None


def dec(value):
    return f"{value:.2f}".replace(".", ",")


def percent(value):
    return f"{dec(value)}%" if value is not None else "N/A"


def stats(rows):
    results = Counter(r["Result"] for r in rows)
    n = len(rows)
    v, e, d = (results[k] for k in "VED")
    assert v + e + d == n
    return {
        "Games": n, "Wins": v, "Draws": e, "Losses": d,
        "WinPct": pct(v, n), "UnbeatenPct": pct(v + e, n),
        "PenaltyAdvances": sum(bool(r["PenaltyWinner"]) and r["DistrictAdvanced"] for r in rows),
        "PenaltyEliminations": sum(bool(r["PenaltyWinner"]) and not r["DistrictAdvanced"] for r in rows),
        "TotalAdvances": sum(r["DistrictAdvanced"] for r in rows),
    }


def write_csv(path, rows):
    with path.open("w", newline="", encoding="utf-8-sig") as stream:
        writer = csv.DictWriter(stream, fieldnames=list(rows[0]))
        writer.writeheader()
        writer.writerows(rows)
    with path.open(newline="", encoding="utf-8-sig") as stream:
        assert list(csv.DictReader(stream)) == [
            {key: "" if value is None else str(value) for key, value in row.items()} for row in rows
        ]


def result_line(label, info):
    return (
        f"| {label} | {info['Games']} | {info['Wins']} | {info['Draws']} | {info['Losses']} | "
        f"{percent(info['WinPct'])} | {percent(info['UnbeatenPct'])} |"
    )


def score(row):
    return f"{row['DistrictClub']} {row['DistrictGoals']} × {row['OpponentGoals']} {row['OpponentClub']}"


def describe_game(row):
    return f"T{row['Season']} R{row['Round']} · {row['Phase']} · {score(row)} · {row['OpponentDivision']}"


def describe_team(label, name, team):
    xi = team["xi"]
    squad = team["roster"]
    sectors = team["sectors"]
    unavailable = [p for p in squad.values() if p["injuryDays"] or p["injuryRounds"] or p["suspensionMatches"]]
    lines = [
        f"#### {label} · {name}", "",
        f"Formação {team['formation']} → {team['finalFormation']}, perfil {team['tacticalIdentity']}, "
        f"moral do treinador {team['morale']}/100 e fator de mando ×{dec(team['homeMultiplier'])}.",
        f"Titulares ({len(xi)}) com média **{dec(team['avg'])}**, mínimo {team['min']} e máximo {team['max']}. "
        f"Elenco completo ({len(squad)}) com média **{dec(team['squadAvg'])}**.",
        f"Setores no início pelo cálculo do motor: ataque efetivo {dec(sectors['effectiveAttackTotal'])}, "
        f"defesa efetiva {dec(sectors['effectiveDefenseTotal'])} e goleiro {dec(sectors['goalkeeperPower'])}.",
        "",
        "| # | Jogador | Posição | Força inicial |",
        "| ---: | --- | --- | ---: |",
    ]
    lines.extend(f"| {i} | {p['name']} | {POSITIONS[p['position']]} | {p['strength']} |" for i, p in enumerate(xi, 1))
    if unavailable:
        lines.extend(["", "Indisponíveis no início: " + ", ".join(
            f"{p['name']} ({p['injuryDays']} d, {p['injuryRounds']} rodadas de lesão, "
            f"{p['suspensionMatches']} jogos de suspensão)" for p in unavailable
        ) + "."])
    return lines


def victory_section(number, row, district, elite, events):
    lines = [
        f"### Vitória {number} · Temporada {row['Season']} · Rodada {row['Round']} · Taça",
        "", f"**{score(row)}** ({row['Minutes']} min). Vencedor: **{row['DistrictClub']}**.", "",
        f"{row['Phase']}. Adversário da {row['OpponentDivision']}. Mandante {row['HomeClub']}. "
        f"Visitante {row['AwayClub']}. ID `{row['MatchId']}`.", "",
        f"Diferença média dos titulares (elite − distrital) **{dec(row['EliteMinusDistrictStrength'])} pontos**.", "",
    ]
    lines.extend(describe_team("Distrital", row["DistrictClub"], district))
    lines.append("")
    lines.extend(describe_team(row["OpponentDivision"], row["OpponentClub"], elite))
    lines.extend(["", "**Substituições registradas**", ""])
    subs = [e for e in events if e["eventType"] == "Substitution"]
    for e in subs:
        roster = district["roster"] if e["clubId"] == row["DistrictClubId"] else elite["roster"]
        incoming, outgoing = (roster[e[f"{key}PlayerId"]] for key in ("primary", "secondary"))
        lines.append(
            f"- {e['minute']}′ · {e['clubName']} · saiu {outgoing['name']} "
            f"({POSITIONS[outgoing['position']]}), entrou {incoming['name']} "
            f"({POSITIONS[incoming['position']]}, força {incoming['strength']})."
        )
    if not subs:
        lines.append("Nenhuma registrada.")
    lines.extend(["", "**Expulsões, lesões e mudanças de formação**", ""])
    other = [e for e in events if e["eventType"] != "Substitution"]
    if other:
        lines.extend(f"- {e['clubName']} · {e['description']}" for e in other)
    else:
        lines.append("Nenhuma registrada.")
    return lines + [""]


def report(meta, all_rows, details, annual, fingerprints):
    summary = {label: stats([r for r in all_rows if r["OpponentDivision"] == label]) for label in DIVISIONS.values()}
    total = stats(all_rows)
    wins = [r for r in all_rows if r["Result"] == "V"]
    lines = [
        "# Distrital contra 1ª e 2ª Divisões em 100 temporadas", "",
        f"Motor do checkpoint `{meta['checkpoint']}`, seed `{meta['seed']}`. "
        f"Um mundo novo de {meta['clubCount']} clubes com 96 distritais e 8 clubes em cada divisão nacional. "
        f"Todos os clubes foram controlados pela IA. Evolução `{meta['forceMode']}`, "
        f"versão de força {meta['forceRulesVersion']} e disciplina {meta['disciplineRulesVersion']}.", "",
        f"Foram concluídas {meta['seasons']} temporadas, com {meta['league']} partidas de Liga, "
        f"{meta['cup']} de Taça e {meta['playedMinutes']} minutos reais do motor. "
        f"A classificação dos clubes é a do início do confronto. Os {total['Games']} confrontos válidos vieram da Taça.", "",
        "| Confronto | Jogos | V | E | D | %V | % sem derrota |",
        "| --- | ---: | ---: | ---: | ---: | ---: | ---: |",
        *(result_line(f"Distrital × {label}", info) for label, info in summary.items()),
        result_line("**Total**", total), "",
        "V/E/D se refere ao placar de campo, incluindo a prorrogação. "
        "Pênaltis de desempate ficam em colunas próprias e os empates continuam como E.", "",
        "| Adversário | Classificações totais | Classificações nos pênaltis | Eliminações nos pênaltis | Temporadas com ao menos uma vitória |",
        "| --- | ---: | ---: | ---: | ---: |",
    ]
    for label, info in summary.items():
        winning_seasons = {r["Season"] for r in wins if r["OpponentDivision"] == label}
        lines.append(f"| {label} | {info['TotalAdvances']} | {info['PenaltyAdvances']} | {info['PenaltyEliminations']} | {len(winning_seasons)} |")
    lines.extend([
        "", "## Força e equilíbrio", "",
        "A média do XI usa a força individual registrada no primeiro minuto. "
        "O elenco completo inclui todos os atletas vinculados ao clube, inclusive os indisponíveis. "
        "A moral é a registrada para o treinador no início. Ela dá contexto à carreira, sem representar um multiplicador direto de gols no MatchEngine. "
        "O mando ×1,06 atua nos setores ofensivo e defensivo da equipe mandante. "
        "Os valores setoriais abaixo também refletem formação, craques, improvisação e perfil tático.", "",
    ])
    if all(info["Games"] for info in summary.values()):
        difference = summary[DIVISIONS["SegundaDivisao"]]["WinPct"] - summary[DIVISIONS["PrimeiraDivisao"]]["WinPct"]
        lines.extend([f"A taxa de vitória contra a 2ª, menos a taxa contra a 1ª, foi de **{dec(difference)} pontos percentuais**.", ""])
    lines.extend([
        "| Grupo | Jogos | V | E | D | %V | % sem derrota |",
        "| --- | ---: | ---: | ---: | ---: | ---: | ---: |",
    ])
    buckets = [
        ("Distrital ≥3 pontos mais forte", lambda g: g <= -3),
        ("Equilíbrio de força (diferença absoluta <3)", lambda g: -3 < g < 3),
        ("Elite 3 a <10 pontos mais forte", lambda g: 3 <= g < 10),
        ("Elite 10 a <20 pontos mais forte", lambda g: 10 <= g < 20),
        ("Elite ≥20 pontos mais forte", lambda g: g >= 20),
    ]
    for label, accepts in buckets:
        lines.append(result_line(label, stats([r for r in all_rows if accepts(r["EliteMinusDistrictStrength"])])))
    for home, label in [(True, "Distrital mandante"), (False, "Distrital visitante")]:
        lines.append(result_line(label, stats([r for r in all_rows if r["DistrictIsHome"] == home])))
    lines.extend(["", "| Temporadas | Jogos | V | E | D | %V | % sem derrota |", "| --- | ---: | ---: | ---: | ---: | ---: | ---: |"])
    for start in range(1, 101, 10):
        lines.append(result_line(f"{start}–{start + 9}", stats([r for r in all_rows if start <= r["Season"] < start + 10])))
    early = [r for r in all_rows if r["Season"] <= 50]
    late = [r for r in all_rows if r["Season"] > 50]
    early_stats, late_stats = stats(early), stats(late)
    def average_gap(rows):
        return sum(r["EliteMinusDistrictStrength"] for r in rows) / len(rows)

    lines.extend([
        "", f"Os distritais perderam {percent(pct(total['Losses'], total['Games']))} dos confrontos. "
        f"A taxa de vitória caiu de {early_stats['Wins']}/{early_stats['Games']} ({percent(early_stats['WinPct'])}) "
        f"na primeira metade para {late_stats['Wins']}/{late_stats['Games']} ({percent(late_stats['WinPct'])}) na segunda. "
        f"A vantagem média da elite subiu de {dec(average_gap(early))} para {dec(average_gap(late))} pontos. "
        "A queda ao longo das temporadas é um achado de equilíbrio desta execução. "
        "O agregado não identifica o efeito isolado da evolução, dos elencos ou da seleção de confrontos.", "",
        "Diferença abaixo de 3 pontos em valor absoluto caracteriza médias próximas. "
        "As vitórias nesse grupo são compatíveis com jogos equilibrados. "
        "Uma vitória com 10 pontos ou mais de desvantagem foi rara na amostra. "
        "Nenhum resultado isolado fornece a probabilidade individual da partida ou demonstra defeito. "
        "O mesmo mundo, as temporadas encadeadas e a seleção de adversários na Taça limitam a generalização.", "",
    ])
    if wins:
        extreme = max(wins, key=lambda r: r["EliteMinusDistrictStrength"])
        district, elite, events = details[extreme["MatchId"]]
        reds = [e for e in events if e["eventType"] in ("DirectRedCard", "SecondYellowRedCard")]
        red_context = " ".join(e["description"] for e in reds) if reds else "Sem expulsões registradas."
        injuries = sum(e["eventType"] == "Injury" for e in events)
        lines.extend([
            f"**Maior surpresa pela diferença de força** — {describe_game(extreme)}. "
            f"XI do distrital {dec(district['avg'])} contra {dec(elite['avg'])}, "
            f"diferença de {dec(extreme['EliteMinusDistrictStrength'])} pontos. "
            f"Distrital {'em casa' if extreme['DistrictIsHome'] else 'fora'}, "
            f"formações iniciais {district['formation']} e {elite['formation']}. "
            f"{red_context} Lesões registradas: {injuries}. "
            "O motor penaliza setores de uma equipe com menos jogadores em campo. "
            "A expulsão dá contexto ao resultado, sem provar que determinou a vitória.", "",
        ])
    else:
        lines.extend(["Nenhuma vitória distrital foi registrada.", ""])
    lines.extend(["### Placares e maiores margens", ""])
    for label in DIVISIONS.values():
        group = [r for r in all_rows if r["OpponentDivision"] == label]
        distribution = Counter((r["DistrictGoals"], r["OpponentGoals"]) for r in group)
        lines.append(f"- {label}, na ordem distrital × elite: " + (
            ", ".join(f"{g}–{a} ({n})" for (g, a), n in sorted(distribution.items(), key=lambda item: (-item[1], item[0])))
            or "sem confrontos"
        ) + ".")
        if group:
            lines.append(f"- Maior margem contra a {label}: {describe_game(max(group, key=lambda r: abs(r['DistrictGoals'] - r['OpponentGoals'])))}.")
    if wins:
        widest = max(wins, key=lambda r: r["DistrictGoals"] - r["OpponentGoals"])
        lines.append(
            f"- Maior margem distrital: {describe_game(widest)}. "
            f"Médias iniciais de {dec(widest['DistrictAverageStrength'])} e {dec(widest['OpponentAverageStrength'])}."
        )
    heavy = [r for r in all_rows if r["OpponentGoals"] - r["DistrictGoals"] >= 10]
    if heavy:
        lines.extend(["", f"A elite venceu {len(heavy)} confrontos por 10 gols ou mais "
                      f"({percent(pct(len(heavy), len(all_rows)))} da amostra). "
                      "Essas goleadas são um ponto de equilíbrio a examinar. A auditoria confirma os placares, "
                      "mas não estabelece um limite aceitável de gols por partida."])
    lines.extend([
        "", "## Controle por temporada", "",
        "| Temporada | × 1ª jogos | V | E | D | × 2ª jogos | V | E | D |",
        "| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |",
    ])
    for i in range(0, len(annual), 2):
        first, second = annual[i:i + 2]
        counts = [part[key] for part in (first, second) for key in ("Games", "Wins", "Draws", "Losses")]
        lines.append(f"| {first['Season']} | " + " | ".join(map(str, counts)) + " |")
    short = [r for r in all_rows if r["DistrictStarterCount"] != 11 or r["OpponentStarterCount"] != 11]
    short_wins = sum(r["Result"] == "V" for r in short)
    lines.extend([
        "", "## Validação e limites", "",
        f"- {meta['matches']} IDs únicos de partidas jogadas no arquivo histórico e {total['Games']} confrontos correspondentes nos CSVs. "
        "Os placares, vencedores, pênaltis e divisões foram reconciliados pelos IDs. As tabelas anuais têm 200 linhas, incluindo categorias sem jogos.",
        f"- {len(short)} confrontos com menos de 11 titulares em algum lado ({short_wins} vitórias distritais). "
        "Em todos eles, a contagem bateu com o limite de titulares aptos da regra vigente. "
        "A seção mostra os atletas que de fato começaram.",
        "- A força dos titulares e dos reservas foi confrontada com o snapshot do próprio motor. "
        "A força de entrada nas substituições é a mesma do apito inicial, verificada também antes do processamento da rodada. "
        "As lesões podem alterar a força nos dias seguintes.",
        "- O teste usou `SimulateFullMatchReal`, as decisões da IA, o calendário, o fechamento e o histórico do projeto, "
        "em uma execução local sem servidor multiplayer ou saves do proprietário. As partidas foram processadas sequencialmente por confronto, minuto a minuto.",
        "- Os CSVs usam UTF-8 com BOM, vírgula como separador e ponto nos decimais. V = vitória, E = empate, D = derrota. "
        "Campo vazio de pênaltis indica que não houve disputa. O ID permite consultar a evidência bruta.",
        "",
        f"[Confrontos]({PREFIX}_MATCHES.csv) · [Temporadas]({PREFIX}_SEASONS.csv) · "
        f"[Registros brutos]({PREFIX}_RAW.zip)", "",
        "Para reproduzir a execução em um ambiente local, rode o teste e gere o relatório a partir da saída dele.", "",
        "```powershell",
        "dotnet test tests/Elifoot.Simulation.Tests -c Release --filter FullyQualifiedName~DistrictVsElite_100RealSeasons",
        "python scripts/report-district-vs-elite.py artifacts/district-vs-elite docs/evidence",
        "```", "",
        "| Evidência de origem | SHA-256 |", "| --- | --- |",
        *(f"| `{name}` | `{digest}` |" for name, digest in fingerprints.items()),
        "", f"## Todas as {len(wins)} vitórias distritais de campo", "",
    ])
    for index, row in enumerate(wins, 1):
        lines.extend(victory_section(index, row, *details[row["MatchId"]]))
    return "\n".join(lines).rstrip() + "\n", summary


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("evidence", type=Path)
    args = parser.parse_args()
    meta = json.loads((args.source / "run.json").read_text(encoding="utf-8"))
    assert meta["seasons"] == 100 and meta["clubCount"] == 128
    persisted = {}
    calendar = Counter()
    minutes = 0
    for fixture in json_lines(args.source / "persisted-fixtures.jsonl"):
        match = fixture["match"]
        assert match["matchId"] not in persisted
        persisted[match["matchId"]] = fixture
        calendar[match["season"], match["competition"]] += 1
        minutes += fixture["minutes"]
    assert len(persisted) == meta["matches"] == 35100 and minutes == meta["playedMinutes"]
    assert {season for season, _ in calendar} == set(range(1, 101))
    for season in range(1, 101):
        assert calendar[season, "League"] == 224
        assert [calendar[season, phase] for phase in PHASES] == [64, 32, 16, 8, 4, 2, 1]
    expected = {key for key, fixture in persisted.items() if matchup(fixture)}
    rows, details = [], {}
    for detail in json_lines(args.source / "confrontations.jsonl"):
        key = detail["result"]["match"]["matchId"]
        assert key not in details and key in expected and detail["result"] == persisted[key]
        row, district, elite = make_row(detail)
        rows.append(row)
        details[key] = (district, elite, detail["events"])
    assert len(expected) == len(rows) == meta["confrontations"] and set(details) == expected
    rows.sort(key=lambda r: (r["Season"], r["Round"], r["DistrictClub"], r["OpponentClub"]))
    annual = [
        {"Season": season, "OpponentDivision": label, **stats([
            r for r in rows if r["Season"] == season and r["OpponentDivision"] == label
        ])}
        for season in range(1, 101) for label in DIVISIONS.values()
    ]
    assert len(annual) == 200
    for key in ("Games", "Wins", "Draws", "Losses", "PenaltyAdvances", "PenaltyEliminations", "TotalAdvances"):
        assert sum(part[key] for part in annual) == stats(rows)[key]
    fingerprints = {
        name: sha256((args.source / name).read_bytes()).hexdigest()
        for name in ("run.json", "persisted-fixtures.jsonl", "confrontations.jsonl")
    }
    markdown, summary = report(meta, rows, details, annual, fingerprints)
    args.evidence.mkdir(parents=True, exist_ok=True)
    write_csv(args.evidence / f"{PREFIX}_MATCHES.csv", rows)
    write_csv(args.evidence / f"{PREFIX}_SEASONS.csv", annual)
    (args.evidence / f"{PREFIX}_100_SEASONS.md").write_text(markdown, encoding="utf-8")
    with ZipFile(args.evidence / f"{PREFIX}_RAW.zip", "w", compression=ZIP_DEFLATED) as archive:
        for name in fingerprints:
            archive.write(args.source / name, name)
        for name in ("district-100.trx", "match-regression.trx"):
            if (args.source / name).is_file():
                archive.write(args.source / name, name)
    strongest_upset = max((r for r in rows if r["Result"] == "V"),
                           key=lambda r: r["EliteMinusDistrictStrength"], default=None)
    print(json.dumps({"run": meta, "byDivision": summary, "total": stats(rows), "largestUpset": strongest_upset},
                     ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
