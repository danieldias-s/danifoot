import 'package:flutter/material.dart';
import 'api_client.dart';
import 'auction_config.dart' show formatAuctionMoney;
import 'player_rankings.dart' show ClassicDialog;
import 'classic98_widgets.dart';
import 'club_theme.dart';
import 'presentation_version.dart';
import 'presentation_widgets.dart';
import 'visual_components.dart' show classicCompetition;

String historyValue(dynamic v) => v == null ? 'Não registrado' : '$v';
String historyMoment(dynamic season, dynamic round) =>
    round == null || round == 0
    ? 'T$season • rodada não registrada'
    : 'T$season R$round';
String historyDivision(dynamic v) => switch ('$v') {
  'PrimeiraDivisao' => '1ª Divisão',
  'SegundaDivisao' => '2ª Divisão',
  'TerceiraDivisao' => '3ª Divisão',
  'QuartaDivisao' => '4ª Divisão',
  'Distrital' => 'Distrital',
  _ => 'Não registrada',
};
String historyCompetition(Map t) =>
    t['competition'] == 'CUP' ? 'Taça' : historyDivision(t['division']);
String historyScope(dynamic v) => switch ('$v') {
  'general' => 'Geral',
  'cup' => 'Taça',
  'division1' => '1ª Divisão',
  'division2' => '2ª Divisão',
  'division3' => '3ª Divisão',
  'division4' => '4ª Divisão',
  _ => '$v',
};
String historyCupCampaign(dynamic value) {
  if (value == null) return 'Sem campanha registrada';
  final text = value.toString();
  final separator = text.indexOf(':');
  if (separator == -1) return text;
  final round = classicCompetition(text.substring(0, separator));
  return '${round.replaceFirst('Taça - ', '')}${text.substring(separator)}';
}

List<Map<String, dynamic>> historyRows(dynamic v) => (v as List? ?? [])
    .whereType<Map>()
    .map((x) => Map<String, dynamic>.from(x))
    .toList();

/// Same server facts, compact rows in the classic adapter and cards at phone widths.
class HistoryTable extends StatelessWidget {
  final List<String> columns;
  final List<List<String>> rows;
  final ValueChanged<int>? onSelect;
  const HistoryTable({
    super.key,
    required this.columns,
    required this.rows,
    this.onSelect,
  });
  @override
  Widget build(BuildContext context) => LayoutBuilder(
    builder: (context, c) {
      if (rows.isEmpty) {
        return const Padding(
          padding: EdgeInsets.all(10),
          child: Text('Nenhum registro disponível.'),
        );
      }
      final classic =
          PresentationScope.maybeOf(context)?.version ==
          PresentationVersion.classic;
      if (c.maxWidth < 650) {
        return Column(
          children: [
            for (var i = 0; i < rows.length; i++)
              Card(
                margin: const EdgeInsets.symmetric(vertical: 4),
                child: InkWell(
                  onTap: onSelect == null ? null : () => onSelect!(i),
                  child: Padding(
                    padding: const EdgeInsets.all(10),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        for (var n = 0; n < columns.length; n++)
                          Text('${columns[n]}: ${rows[i][n]}'),
                      ],
                    ),
                  ),
                ),
              ),
          ],
        );
      }
      if (classic) {
        return RetroTable(headers: columns, rows: rows, onSelect: onSelect);
      }
      return SingleChildScrollView(
        scrollDirection: Axis.horizontal,
        child: DataTable(
          headingRowHeight: 36,
          dataRowMinHeight: 40,
          dataRowMaxHeight: 72,
          columnSpacing: 18,
          columns: columns.map((c) => DataColumn(label: Text(c))).toList(),
          rows: [
            for (var i = 0; i < rows.length; i++)
              DataRow(
                cells: [
                  for (final v in rows[i])
                    DataCell(
                      Text(v),
                      onTap: onSelect == null ? null : () => onSelect!(i),
                    ),
                ],
              ),
          ],
        ),
      );
    },
  );
}

Widget historySection(String title, Widget child) => Builder(
  builder: (context) {
    final classic =
        PresentationScope.maybeOf(context)?.version ==
            PresentationVersion.classic ||
        ClassicTheme.of(context).classicPresentation;
    return Padding(
      padding: const EdgeInsets.only(top: 14),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Container(
            color: classic
                ? ClassicColors.titleBarBlue
                : const Color(0xFF003D2C),
            padding: const EdgeInsets.all(9),
            child: Text(
              title,
              style: TextStyle(
                color: classic ? Colors.white : const Color(0xFFFFD700),
                fontSize: 19,
                fontWeight: FontWeight.bold,
              ),
            ),
          ),
          child,
        ],
      ),
    );
  },
);

Widget historyTransfers(dynamic source) => HistoryTable(
  columns: const [
    'Temporada / rodada',
    'Jogador',
    'Origem → destino',
    'Operação',
    'Valor',
  ],
  rows: [
    for (final t in historyRows(source))
      [
        historyMoment(t['season'], t['round']),
        '${t['playerName']}',
        '${t['fromClub']} → ${t['toClub']}',
        '${t['operation']}',
        formatAuctionMoney(t['amount'] as num? ?? 0),
      ],
  ],
);

Future<void> showClubHistoryDialog({
  required BuildContext context,
  required ElifootApiClient api,
  required String worldId,
  required String clubId,
}) => showDialog<void>(
  context: context,
  builder: (_) => ClubHistoryDialog(api: api, worldId: worldId, clubId: clubId),
);

class ClubHistoryDialog extends StatefulWidget {
  final ElifootApiClient api;
  final String worldId, clubId;
  const ClubHistoryDialog({
    super.key,
    required this.api,
    required this.worldId,
    required this.clubId,
  });
  @override
  State<ClubHistoryDialog> createState() => _ClubHistoryDialogState();
}

class _ClubHistoryDialogState extends State<ClubHistoryDialog> {
  late Future<Map<String, dynamic>> future;
  int? season;
  List<int> seasons = [];
  String page = 'Temporadas';
  @override
  void initState() {
    super.initState();
    future = load();
  }

  Future<Map<String, dynamic>> load() async {
    final data = await widget.api.getClubHistory(
      widget.worldId,
      widget.clubId,
      season: season,
    );
    if (season == null) {
      seasons = historyRows(
        data['seasons'],
      ).map((s) => (s['season'] as num).toInt()).toList();
    }
    return data;
  }

  void selectSeason(int? value) {
    setState(() {
      season = value;
      future = load();
    });
  }

  @override
  Widget build(BuildContext context) => ClassicDialog(
    title: 'HISTÓRIA DO CLUBE',
    child: FutureBuilder<Map<String, dynamic>>(
      future: future,
      builder: (context, snapshot) {
        if (snapshot.hasError) {
          return Column(
            children: [
              const Text('Não foi possível carregar o histórico.'),
              TextButton(
                onPressed: () => setState(() => future = load()),
                child: const Text('TENTAR NOVAMENTE'),
              ),
            ],
          );
        }
        if (!snapshot.hasData) {
          return const Center(child: CircularProgressIndicator());
        }
        final data = snapshot.data!;
        final records = data['records'] as Map? ?? {};
        String result(dynamic match) {
          if (match is! Map) return 'Não registrada';
          return 'T${match['season']} • ${match['homeClub']} ${match['homeGoals']} × ${match['awayGoals']} ${match['awayClub']}';
        }

        final rows = historyRows(data['seasons']);
        return Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            const Text('Histórico construído neste save.'),
            Text(
              '${data['clubName']}',
              style: const TextStyle(fontSize: 24, fontWeight: FontWeight.bold),
            ),
            for (final warning in data['coverageWarnings'] as List? ?? [])
              Text('$warning'),
            DropdownButtonFormField<int>(
              key: ValueKey('club-history-season-$season'),
              initialValue: season ?? 0,
              isExpanded: true,
              decoration: const InputDecoration(labelText: 'Temporada'),
              items: [
                const DropdownMenuItem(
                  value: 0,
                  child: Text('Todas as temporadas'),
                ),
                for (final s in seasons)
                  DropdownMenuItem(value: s, child: Text('Temporada $s')),
              ],
              onChanged: (s) => selectSeason(s == 0 ? null : s),
            ),
            const SizedBox(height: 8),
            Wrap(
              spacing: 5,
              runSpacing: 5,
              children: [
                for (final tab in [
                  'Temporadas',
                  'Títulos',
                  'Treinadores',
                  'Jogadores',
                  'Transferências',
                  'Recordes',
                ])
                  OutlinedButton(
                    onPressed: () => setState(() => page = tab),
                    style: OutlinedButton.styleFrom(
                      backgroundColor: page == tab
                          ? ClassicTheme.of(context).surfaceAlternate
                          : null,
                    ),
                    child: Text(tab),
                  ),
              ],
            ),
            historySection(
              'ESTATÍSTICAS ACUMULADAS',
              _totals(data['total'] as Map? ?? {}),
            ),
            if (page == 'Temporadas') ...[
              historySection(
                'HISTÓRIA POR TEMPORADA',
                HistoryTable(
                  columns: const [
                    'Temporada',
                    'Divisão',
                    'Colocação',
                    'Resultado',
                    'J / V / E / D',
                    'GP / GC / SG',
                  ],
                  onSelect: (i) =>
                      selectSeason((rows[i]['season'] as num).toInt()),
                  rows: [
                    for (final s in rows)
                      [
                        '${s['season']}',
                        historyDivision(s['division']),
                        s['finalPosition'] == null
                            ? (s['closed'] == true
                                  ? 'Não registrada'
                                  : 'Em andamento')
                            : '${s['finalPosition']}º',
                        s['promoted'] == true
                            ? 'Promovido'
                            : s['relegated'] == true
                            ? 'Rebaixado'
                            : s['closed'] == true
                            ? (s['partial'] == true &&
                                      s['finalPosition'] == null
                                  ? 'Resultado não registrado'
                                  : 'Permanência')
                            : 'Em andamento',
                        _results(s['total'] as Map? ?? {}),
                        _goals(s['total'] as Map? ?? {}),
                      ],
                  ],
                ),
              ),
              for (final s in rows)
                ExpansionTile(
                  key: ValueKey('history-season-detail-$season-${s['season']}'),
                  title: Text('Detalhes da temporada ${s['season']}'),
                  initiallyExpanded: season != null,
                  children: [
                    Text('Taça: ${historyCupCampaign(s['cupCampaign'])}'),
                    _totals(s['league'] as Map? ?? {}, label: 'Liga'),
                    _totals(s['cup'] as Map? ?? {}, label: 'Taça'),
                    Text(
                      'Caixa inicial: ${s['openingCash'] == null ? 'Não registrado' : formatAuctionMoney(s['openingCash'] as num)} • Final: ${s['closingCash'] == null ? 'Em aberto / não registrado' : formatAuctionMoney(s['closingCash'] as num)}',
                    ),
                    Text(
                      'Receitas registradas: ${formatAuctionMoney(s['income'] as num? ?? 0)} • Despesas: ${formatAuctionMoney(s['expenses'] as num? ?? 0)}',
                    ),
                    if (s['partial'] == true)
                      const Text('Cobertura parcial nesta temporada.'),
                    HistoryTable(
                      columns: const ['Artilheiros do clube', 'Gols'],
                      rows: [
                        for (final p in historyRows(s['scorers']))
                          ['${p['playerName']}', '${p['goals']}'],
                      ],
                    ),
                  ],
                ),
            ],
            if (page == 'Títulos')
              for (final division in [
                'PrimeiraDivisao',
                'SegundaDivisao',
                'TerceiraDivisao',
                'QuartaDivisao',
                'CUP',
              ])
                historySection(
                  division == 'CUP' ? 'TAÇA' : historyDivision(division),
                  HistoryTable(
                    columns: const ['Temporada', 'Conquista'],
                    rows: [
                      for (final t in historyRows(data['titles']).where(
                        (t) => division == 'CUP'
                            ? t['competition'] == 'CUP'
                            : t['competition'] == 'LEAGUE' &&
                                  t['division'] == division,
                      ))
                        [
                          '${t['season']}',
                          t['runnerUp'] == true ? 'Vice-campeão' : 'Campeão',
                        ],
                    ],
                  ),
                ),
            if (page == 'Treinadores')
              historySection(
                'PASSAGENS DOS TREINADORES',
                HistoryTable(
                  columns: const [
                    'Treinador',
                    'Tipo',
                    'Entrada',
                    'Saída',
                    'J / V / E / D',
                    'Títulos',
                    'Motivo registrado',
                  ],
                  rows: [
                    for (final c in historyRows(data['coaches']))
                      [
                        '${c['name']}',
                        c['human'] == true ? 'Humano' : 'IA',
                        historyMoment(c['startSeason'], c['startRound']),
                        c['endSeason'] == null
                            ? 'Atual'
                            : historyMoment(c['endSeason'], c['endRound']),
                        _results(c['results'] as Map? ?? {}),
                        '${c['titles']}',
                        historyValue(c['exitReason']),
                      ],
                  ],
                ),
              ),
            if (page == 'Jogadores')
              historySection(
                'JOGADORES QUE PASSARAM PELO CLUBE',
                HistoryTable(
                  columns: const ['Jogador', 'Entrada', 'Saída', 'Cobertura'],
                  rows: [
                    for (final p in historyRows(data['players']))
                      [
                        '${p['playerName']}',
                        historyMoment(p['startSeason'], p['startRound']),
                        p['endSeason'] == null
                            ? 'Atual'
                            : historyMoment(p['endSeason'], p['endRound']),
                        p['partial'] == true ? 'Parcial' : 'Registrada',
                      ],
                  ],
                ),
              ),
            if (page == 'Transferências')
              historySection(
                'TRANSFERÊNCIAS',
                historyTransfers(data['transfers']),
              ),
            if (page == 'Recordes')
              historySection(
                'RECORDES REGISTRADOS',
                Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text('Maior vitória: ${result(records['biggestWin'])}'),
                    Text('Maior derrota: ${result(records['biggestLoss'])}'),
                    Text(
                      'Vitórias seguidas: ${historyValue(records['longestWinningRun'])}',
                    ),
                    Text(
                      'Sequência invicta: ${historyValue(records['longestUnbeatenRun'])}',
                    ),
                    const Text(
                      'Sequências calculadas somente entre partidas preservadas; pênaltis de desempate não integram o placar de gols.',
                    ),
                  ],
                ),
              ),
          ],
        );
      },
    ),
  );
  String _results(Map t) =>
      '${t['games'] ?? 0} / ${t['wins'] ?? 0} / ${t['draws'] ?? 0} / ${t['losses'] ?? 0}';
  String _goals(Map t) =>
      '${t['goalsFor'] ?? 0} / ${t['goalsAgainst'] ?? 0} / ${t['goalDifference'] ?? 0}';
  Widget _totals(Map t, {String label = 'Total'}) => Padding(
    padding: const EdgeInsets.all(8),
    child: Text(
      '$label • J / V / E / D: ${_results(t)} • GP / GC / SG: ${_goals(t)}',
    ),
  );
}

class PlayerCareerHistoryPanels extends StatelessWidget {
  final Map<String, dynamic> data;
  const PlayerCareerHistoryPanels({super.key, required this.data});
  @override
  Widget build(BuildContext context) => Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      const Text(
        'Carreira registrada neste save. Dados anteriores indisponíveis não são estimados.',
      ),
      Text(
        'Artilharias por competição registradas: ${historyRows(data['scoringRecognitions']).length}',
      ),
      historySection(
        'TOTAIS POR TEMPORADA — LIGA E TAÇA',
        HistoryTable(
          columns: const [
            'Temporada',
            'Jogos',
            'Gols',
            'Assistências',
            'Minutos',
          ],
          rows: [
            for (final s in historyRows(data['seasonTotals']))
              [
                '${s['season']}',
                '${s['games']}',
                '${s['goals']}',
                s['incompleteAssists'] == true
                    ? 'Parcial: ${s['assists']}'
                    : '${s['assists']}',
                s['partial'] == true
                    ? 'Parcial: ${s['minutes']}'
                    : '${s['minutes']}',
              ],
          ],
        ),
      ),
      Text(
        'Assistências na carreira: ${(data['career'] as Map?)?['assists'] ?? 'Não registradas'}',
      ),
      historySection(
        'CARREIRA ENTRE CLUBES',
        HistoryTable(
          columns: const ['Clube', 'Entrada', 'Saída', 'Cobertura'],
          rows: [
            for (final c in historyRows(data['clubCareer']))
              [
                '${c['clubName']}',
                historyMoment(c['startSeason'], c['startRound']),
                c['endSeason'] == null
                    ? 'Atual'
                    : historyMoment(c['endSeason'], c['endRound']),
                c['partial'] == true ? 'Parcial' : 'Registrada',
              ],
          ],
        ),
      ),
      historySection(
        'FORÇA POR TEMPORADA',
        HistoryTable(
          columns: const ['Temporada', 'Força inicial', 'Força final'],
          rows: [
            for (final s in historyRows(data['seasonStrengths']))
              [
                '${s['season']}',
                historyValue(s['initialStrength']),
                historyValue(s['finalStrength']),
              ],
          ],
        ),
      ),
      historySection(
        'TÍTULOS DO CLUBE COM PARTICIPAÇÃO REGISTRADA',
        HistoryTable(
          columns: const ['Temporada', 'Clube', 'Competição'],
          rows: [
            for (final t in historyRows(data['clubTitlesWithParticipation']))
              ['${t['season']}', '${t['clubName']}', historyCompetition(t)],
          ],
        ),
      ),
      historySection(
        'ARTILHARIAS POR COMPETIÇÃO',
        HistoryTable(
          columns: const ['Temporada', 'Competição', 'Gols'],
          rows: [
            for (final t in historyRows(data['scoringRecognitions']))
              ['${t['season']}', historyScope(t['scope']), '${t['goals']}'],
          ],
        ),
      ),
      historySection(
        'TRANSFERÊNCIAS DO JOGADOR',
        historyTransfers(data['transfers']),
      ),
      historySection(
        'RECORDES INDIVIDUAIS',
        Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Maior força registrada: ${historyValue(data['highestRecordedStrength'])}',
            ),
            Text(
              'Mais gols em uma temporada: ${historyValue(data['mostGoalsInSeason'])}',
            ),
            Text(
              'Mais jogos em uma temporada: ${historyValue(data['mostGamesInSeason'])}',
            ),
            Text(
              'Clubes com vínculo registrado: ${historyRows(data['clubCareer']).map((c) => c['clubId']).where((id) => id != null).toSet().length}',
            ),
          ],
        ),
      ),
      historySection(
        'ASSISTÊNCIAS POR TEMPORADA E CLUBE',
        HistoryTable(
          columns: const ['Temporada', 'Clube', 'Competição', 'Assistências'],
          rows: [
            for (final s in historyRows(data['statistics']))
              [
                '${s['season']}',
                '${s['clubName']}',
                '${s['competitionType']}',
                s['hasIncompleteAssists'] == true
                    ? 'Parcial: ${s['assists'] ?? 0}'
                    : '${s['assists'] ?? 0}',
              ],
          ],
        ),
      ),
    ],
  );
}
