import 'dart:math' as math;

import 'package:flutter/material.dart';

import 'api_client.dart';

const _gold = Color(0xFFFFD700);
const _green = Color(0xFF062E0F);
const _headingStyle = TextStyle(color: _gold, fontWeight: FontWeight.bold);

// HTTP may fail before the next frame attaches FutureBuilder's listener.
// Register an error listener immediately while preserving the result for the UI.
Future<T> _forBuilder<T>(Future<T> future) {
  future.ignore();
  return future;
}

List<Map<String, dynamic>> _records(dynamic value) => (value as List? ?? [])
    .map((row) => Map<String, dynamic>.from(row as Map))
    .toList();

String _value(dynamic value) => value?.toString() ?? '—';
int _integer(dynamic value) => (value as num?)?.toInt() ?? 0;
String _average(dynamic value) => value is num ? value.toStringAsFixed(3) : '—';
String _money(dynamic value) => value is num
    ? value
          .toStringAsFixed(0)
          .replaceAllMapped(
            RegExp(r'(\d)(?=(\d{3})+(?!\d))'),
            (match) => '${match[1]}.',
          )
    : '—';

String _playerName(Map<String, dynamic> player) {
  final name = _value(player['displayName']);
  return player['isStar'] == true && !name.endsWith('*') ? '$name*' : name;
}

String _club(Map<String, dynamic> player) => player['currentClubId'] == null
    ? 'Sem clube'
    : _value(player['currentClubName']);

String _competition(Map<String, dynamic> row) {
  final type = row['competitionType']?.toString();
  final numericType = int.tryParse(type ?? '');
  if ((type?.startsWith('Cup') ?? false) ||
      type == 'Taca' ||
      type == 'Taça' ||
      (numericType != null && numericType >= 1 && numericType <= 8)) {
    return 'TAÇA';
  }
  return switch (row['division']?.toString()) {
    'PrimeiraDivisao' || '1' => '1ª DIVISÃO',
    'SegundaDivisao' || '2' => '2ª DIVISÃO',
    'TerceiraDivisao' || '3' => '3ª DIVISÃO',
    'QuartaDivisao' || '4' => '4ª DIVISÃO',
    'Distrital' || '5' => 'DISTRITAL',
    _ => _value(row['division'] ?? row['competitionType']),
  };
}

/// The server owns ranking order, exact averages, eligibility and award credits.
class PlayerRankingsScreen extends StatefulWidget {
  final ElifootApiClient apiClient;
  final String worldId;
  final int currentSeason;
  final int revision;

  const PlayerRankingsScreen({
    super.key,
    required this.apiClient,
    required this.worldId,
    required this.currentSeason,
    this.revision = 0,
  });

  @override
  State<PlayerRankingsScreen> createState() => _PlayerRankingsScreenState();
}

class _PlayerRankingsScreenState extends State<PlayerRankingsScreen> {
  String _category = 'scorers';
  String _scope = 'general';
  String _period = 'career';
  late int _season;
  late Future<Map<String, dynamic>> _future;
  List<int> _seasons = [];
  int _request = 0;

  @override
  void initState() {
    super.initState();
    _season = widget.currentSeason;
    _future = _load();
  }

  @override
  void didUpdateWidget(PlayerRankingsScreen oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.worldId != widget.worldId ||
        oldWidget.currentSeason != widget.currentSeason ||
        oldWidget.revision != widget.revision) {
      _future = _load();
    }
  }

  Future<Map<String, dynamic>> _load() => _forBuilder(_fetch());

  Future<Map<String, dynamic>> _fetch() async {
    final request = ++_request;
    final data = await widget.apiClient.getPlayerRankings(
      widget.worldId,
      category: _category,
      scope: _scope,
      period: _period,
      season: _period == 'season' ? _season : null,
    );
    if (mounted && request == _request) {
      _seasons =
          (data['seasons'] as List? ?? [])
              .cast<num>()
              .map((s) => s.toInt())
              .toSet()
              .toList()
            ..sort((a, b) => b.compareTo(a));
    }
    return data;
  }

  void _reload() => setState(() {
    _future = _load();
  });

  Widget _filter(
    String key,
    String label,
    String value,
    Map<String, String> options,
    ValueChanged<String> change,
  ) {
    return SizedBox(
      width: 230,
      child: DropdownButtonFormField<String>(
        key: ValueKey(key),
        initialValue: value,
        isExpanded: true,
        decoration: InputDecoration(labelText: label, isDense: true),
        items: options.entries
            .map(
              (e) => DropdownMenuItem(
                value: e.key,
                child: Text(e.value, style: const TextStyle(fontSize: 12)),
              ),
            )
            .toList(),
        onChanged: (v) {
          if (v == null) return;
          setState(() {
            change(v);
            _future = _load();
          });
        },
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    return SingleChildScrollView(
      key: const ValueKey('player-rankings-scroll'),
      padding: const EdgeInsets.all(12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text('RANKINGS DE JOGADORES', style: _headingStyle),
          const SizedBox(height: 12),
          Wrap(
            spacing: 12,
            runSpacing: 12,
            children: [
              _filter('ranking-category', 'Categoria', _category, const {
                'scorers': 'Artilheiros',
                'goalkeepers': 'Goleiros',
                'defenders': 'Defensores',
              }, (v) => _category = v),
              _filter('ranking-scope', 'Competição', _scope, const {
                'general': 'Geral',
                'division1': '1ª divisão',
                'division2': '2ª divisão',
                'division3': '3ª divisão',
                'division4': '4ª divisão',
                'cup': 'Taça',
              }, (v) => _scope = v),
              _filter('ranking-period', 'Período', _period, const {
                'career': 'Carreira inteira',
                'current': 'Temporada atual',
                'season': 'Temporada selecionada',
              }, (v) => _period = v),
              if (_period == 'season')
                _filter(
                  'ranking-season',
                  'Temporada',
                  '$_season',
                  {
                    for (final s in {
                      ..._seasons,
                      widget.currentSeason,
                      _season,
                    })
                      '$s': 'Temporada $s',
                  },
                  (v) => _season = int.parse(v),
                ),
              TextButton.icon(
                onPressed: _reload,
                icon: const Icon(Icons.refresh, size: 16),
                label: const Text('ATUALIZAR'),
              ),
            ],
          ),
          const SizedBox(height: 12),
          const Text(
            'Somente partidas oficiais concluídas. Toque no jogador para ver o histórico.',
            style: TextStyle(fontSize: 12),
          ),
          if (_category != 'scorers')
            const Text(
              'Mínimo de 20 jogos no recorte consultado.',
              style: TextStyle(fontSize: 12),
            ),
          const SizedBox(height: 8),
          FutureBuilder<Map<String, dynamic>>(
            future: _future,
            builder: (context, snapshot) {
              if (snapshot.connectionState != ConnectionState.done) {
                return const _Loading('CARREGANDO RANKING...');
              }
              if (snapshot.hasError) return _LoadError('ranking', _reload);
              final data = snapshot.data!;
              final minimum = math.max(20, _integer(data['minimumMatches']));
              final all = _records(data['rows']);
              final needsMinimum = _category != 'scorers';
              final rows = all
                  .where(
                    (p) =>
                        !needsMinimum ||
                        _integer(p['matchesPlayed']) >= minimum,
                  )
                  .toList();
              final seen = <String>{};
              final ineligible = needsMinimum
                  ? [
                          ..._records(data['notEligible']),
                          ...all.where(
                            (p) => _integer(p['matchesPlayed']) < minimum,
                          ),
                        ]
                        .where(
                          (p) =>
                              _integer(p['matchesPlayed']) < minimum &&
                              seen.add(p['playerId'].toString()),
                        )
                        .toList()
                  : <Map<String, dynamic>>[];
              return Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  _CoverageWarnings(data['coverageWarnings']),
                  Text(
                    _period == 'career'
                        ? 'CARREIRA INTEIRA'
                        : 'TEMPORADA ${data['season'] ?? data['currentSeason'] ?? _season}',
                    style: _headingStyle,
                  ),
                  if (rows.isEmpty)
                    const Padding(
                      padding: EdgeInsets.symmetric(vertical: 12),
                      child: Text(
                        'Nenhum jogador elegível com estatísticas oficiais neste recorte.',
                      ),
                    )
                  else
                    _rankingTable(rows),
                  if (ineligible.isNotEmpty) ...[
                    const SizedBox(height: 16),
                    const Text('AINDA NÃO ELEGÍVEIS', style: _headingStyle),
                    Text('Faltam jogos para alcançar $minimum neste recorte.'),
                    _rankingTable(ineligible, minimum: minimum),
                  ],
                ],
              );
            },
          ),
          const SizedBox(height: 24),
          SeasonTopScorersPanel(
            apiClient: widget.apiClient,
            worldId: widget.worldId,
            season: _period == 'season' ? _season : null,
            revision: widget.revision + _request,
            currentSeason: widget.currentSeason,
          ),
        ],
      ),
    );
  }

  Widget _rankingTable(List<Map<String, dynamic>> players, {int? minimum}) {
    return _ClassicTable(
      tableKey: minimum == null ? 'ranking-table' : 'not-eligible-table',
      columns: [
        '#',
        'JOGADOR',
        'CLUBE ATUAL',
        'JOGOS',
        if (_category == 'scorers') 'GOLS',
        if (_category == 'goalkeepers') 'GOLS SOFRIDOS',
        if (_category == 'defenders') ...['AMARELOS', 'VERMELHOS'],
        _category == 'defenders' ? 'ÍNDICE' : 'MÉDIA',
        if (minimum != null) 'JOGOS FALTANTES',
      ],
      rows: players
          .map(
            (p) => DataRow(
              cells: [
                DataCell(Text(minimum == null ? _value(p['rank']) : '—')),
                _playerCell(context, widget.apiClient, widget.worldId, p),
                DataCell(Text(_club(p))),
                DataCell(Text(_value(p['matchesPlayed']))),
                if (_category == 'scorers')
                  DataCell(Text(_value(p['goalsScored']))),
                if (_category == 'goalkeepers')
                  DataCell(Text(_value(p['goalsConceded']))),
                if (_category == 'defenders') ...[
                  DataCell(Text(_value(p['yellowCards']))),
                  DataCell(Text(_value(p['redCards']))),
                ],
                DataCell(Text(_average(p['average']))),
                if (minimum != null)
                  DataCell(
                    Text(
                      '${math.max(0, minimum - _integer(p['matchesPlayed']))}',
                    ),
                  ),
              ],
            ),
          )
          .toList(),
    );
  }
}

DataCell _playerCell(
  BuildContext context,
  ElifootApiClient api,
  String worldId,
  Map<String, dynamic> player,
) => DataCell(
  Text(
    _playerName(player),
    style: const TextStyle(color: _gold, decoration: TextDecoration.underline),
  ),
  onTap: player['playerId'] == null
      ? null
      : () => showPlayerHistoryDialog(
          context: context,
          apiClient: api,
          worldId: worldId,
          playerId: player['playerId'].toString(),
        ),
);

Future<void> showPlayerHistoryDialog({
  required BuildContext context,
  required ElifootApiClient apiClient,
  required String worldId,
  required String playerId,
}) => showDialog<void>(
  context: context,
  builder: (_) => _PlayerHistoryDialog(
    apiClient: apiClient,
    worldId: worldId,
    playerId: playerId,
  ),
);

class _PlayerHistoryDialog extends StatefulWidget {
  final ElifootApiClient apiClient;
  final String worldId;
  final String playerId;
  const _PlayerHistoryDialog({
    required this.apiClient,
    required this.worldId,
    required this.playerId,
  });

  @override
  State<_PlayerHistoryDialog> createState() => _PlayerHistoryDialogState();
}

class _PlayerHistoryDialogState extends State<_PlayerHistoryDialog> {
  late Future<Map<String, dynamic>> _future;
  @override
  void initState() {
    super.initState();
    _future = _load();
  }

  Future<Map<String, dynamic>> _load() => _forBuilder(
    widget.apiClient.getPlayerHistory(widget.worldId, widget.playerId),
  );

  @override
  Widget build(BuildContext context) => AlertDialog(
    backgroundColor: const Color(0xFF0E3B19),
    insetPadding: const EdgeInsets.all(16),
    title: const Text('HISTÓRICO DO JOGADOR', style: _headingStyle),
    content: SizedBox(
      width: 900,
      child: SingleChildScrollView(
        key: const ValueKey('player-history-scroll'),
        child: FutureBuilder<Map<String, dynamic>>(
          future: _future,
          builder: (context, snapshot) {
            if (snapshot.connectionState != ConnectionState.done) {
              return const _Loading('CARREGANDO HISTÓRICO...');
            }
            if (snapshot.hasError) {
              return _LoadError(
                'histórico',
                () => setState(() {
                  _future = _load();
                }),
              );
            }
            final data = snapshot.data!;
            final player = Map<String, dynamic>.from(
              data['player'] as Map? ?? {},
            );
            final career = Map<String, dynamic>.from(
              data['career'] as Map? ?? {},
            );
            final statistics = _records(data['statistics']);
            final awards = _records(data['awards']);
            final totals = <String, Map<String, int>>{};
            for (final row in statistics) {
              final total = totals.putIfAbsent(
                _competition(row),
                () => {
                  'matchesPlayed': 0,
                  'goalsScored': 0,
                  'goalsConceded': 0,
                  'yellowCards': 0,
                  'redCards': 0,
                  'minutesPlayed': 0,
                },
              );
              for (final key in total.keys) {
                total[key] = total[key]! + _integer(row[key]);
              }
            }
            return Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(_playerName(player), style: _headingStyle),
                Text(
                  'Posição: ${_value(player['position'])} | Força: ${_value(player['strength'])}',
                ),
                Text('Comportamento: ${_value(player['behaviorDisplay'])}'),
                Text(
                  'Craque (*): ${player['isStar'] == true ? 'Sim (*)' : 'Não'}',
                ),
                Text('Clube atual: ${_club(player)}'),
                const SizedBox(height: 8),
                Text(
                  'ARTILHEIRO: ${_value(player['seasonTopScorerTitles'])} VEZES',
                  key: const ValueKey('player-scorer-titles'),
                  style: _headingStyle,
                ),
                Text(
                  'Total de jogos: ${_value(career['matchesPlayed'])} | Total de gols: ${_value(career['goalsScored'])}',
                ),
                Text(
                  'Gols sofridos: ${_value(career['goalsConceded'])} | Amarelos: ${_value(career['yellowCards'])} | Vermelhos: ${_value(career['redCards'])}',
                ),
                Text('Minutos jogados: ${_value(career['minutesPlayed'])}'),
                _CoverageWarnings(data['coverageWarnings']),
                if (statistics.any(
                  (s) => s['hasIncompleteAppearances'] == true,
                ))
                  const Text(
                    'COBERTURA: Há aparições legadas incompletas; jogos e minutos podem estar parciais.',
                    style: TextStyle(color: _gold, fontSize: 12),
                  ),
                const SizedBox(height: 12),
                const Text(
                  'JOGOS E GOLS POR COMPETIÇÃO — CARREIRA',
                  style: _headingStyle,
                ),
                if (totals.isEmpty)
                  const Text('Sem estatísticas oficiais registradas.')
                else
                  _ClassicTable(
                    tableKey: 'player-competition-history',
                    columns: const [
                      'COMPETIÇÃO',
                      'JOGOS',
                      'GOLS',
                      'GOLS SOFRIDOS',
                      'AMARELOS',
                      'VERMELHOS',
                      'MINUTOS',
                    ],
                    rows: totals.entries
                        .map(
                          (e) => DataRow(
                            cells: [
                              DataCell(Text(e.key)),
                              for (final value in e.value.values)
                                DataCell(Text('$value')),
                            ],
                          ),
                        )
                        .toList(),
                  ),
                const SizedBox(height: 12),
                const Text('HISTÓRICO DE TEMPORADAS', style: _headingStyle),
                if (statistics.isEmpty)
                  const Text('Sem temporadas com partidas oficiais concluídas.')
                else
                  _ClassicTable(
                    tableKey: 'player-season-history',
                    columns: const [
                      'TEMPORADA',
                      'COMPETIÇÃO',
                      'CLUBE (ID HISTÓRICO)',
                      'JOGOS',
                      'GOLS',
                      'GOLS SOFRIDOS',
                      'AMARELOS',
                      'VERMELHOS',
                      'MINUTOS',
                      'COBERTURA',
                    ],
                    rows: statistics
                        .map(
                          (s) => DataRow(
                            cells: [
                              DataCell(Text(_value(s['season']))),
                              DataCell(Text(_competition(s))),
                              DataCell(Text(_value(s['clubId']))),
                              for (final key in [
                                'matchesPlayed',
                                'goalsScored',
                                'goalsConceded',
                                'yellowCards',
                                'redCards',
                                'minutesPlayed',
                              ])
                                DataCell(Text(_value(s[key]))),
                              DataCell(
                                Text(
                                  s['hasIncompleteAppearances'] == true
                                      ? 'Incompleta'
                                      : 'Completa',
                                ),
                              ),
                            ],
                          ),
                        )
                        .toList(),
                  ),
                const SizedBox(height: 12),
                const Text('CONQUISTAS DE ARTILHARIA', style: _headingStyle),
                if (awards.isEmpty)
                  const Text('Nenhuma artilharia de temporada registrada.')
                else
                  _ClassicTable(
                    tableKey: 'player-award-history',
                    columns: const [
                      'TEMPORADA',
                      'GOLS',
                      'JOGOS',
                      'CLUBE NO ENCERRAMENTO',
                      'PRÊMIO AO CLUBE',
                      'DATA',
                    ],
                    rows: awards
                        .map(
                          (a) => DataRow(
                            key: ValueKey('player-award-${a['awardId']}'),
                            cells: [
                              DataCell(Text(_value(a['season']))),
                              DataCell(Text(_value(a['totalGoals']))),
                              DataCell(Text(_value(a['totalMatches']))),
                              DataCell(
                                Text(
                                  a['clubNameAtClose']?.toString() ??
                                      'Sem clube',
                                ),
                              ),
                              DataCell(Text(_money(a['awardAmount']))),
                              DataCell(Text(_value(a['awardGrantedAt']))),
                            ],
                          ),
                        )
                        .toList(),
                  ),
              ],
            );
          },
        ),
      ),
    ),
    actions: [
      TextButton(
        onPressed: () => Navigator.pop(context),
        child: const Text('FECHAR'),
      ),
    ],
  );
}

Future<void> showSeasonTopScorersDialog({
  required BuildContext context,
  required ElifootApiClient apiClient,
  required String worldId,
  required int season,
}) => showDialog<void>(
  context: context,
  builder: (context) => AlertDialog(
    key: const ValueKey('season-end-awards-dialog'),
    backgroundColor: const Color(0xFF0E3B19),
    insetPadding: const EdgeInsets.all(16),
    title: Text('ENCERRAMENTO DA TEMPORADA $season', style: _headingStyle),
    content: SizedBox(
      width: 900,
      child: SingleChildScrollView(
        child: SeasonTopScorersPanel(
          apiClient: apiClient,
          worldId: worldId,
          season: season,
        ),
      ),
    ),
    actions: [
      TextButton(
        onPressed: () => Navigator.pop(context),
        child: const Text('FECHAR'),
      ),
    ],
  ),
);

class SeasonTopScorersPanel extends StatefulWidget {
  final ElifootApiClient apiClient;
  final String worldId;
  final int? season;
  final int? currentSeason;
  final int revision;
  const SeasonTopScorersPanel({
    super.key,
    required this.apiClient,
    required this.worldId,
    this.season,
    this.currentSeason,
    this.revision = 0,
  });

  @override
  State<SeasonTopScorersPanel> createState() => _SeasonTopScorersPanelState();
}

class _SeasonTopScorersPanelState extends State<SeasonTopScorersPanel> {
  late Future<Map<String, dynamic>> _future;
  Future<Map<String, dynamic>> _load() => _forBuilder(
    widget.apiClient.getSeasonTopScorers(widget.worldId, season: widget.season),
  );
  @override
  void initState() {
    super.initState();
    _future = _load();
  }

  @override
  void didUpdateWidget(SeasonTopScorersPanel oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (widget.worldId != oldWidget.worldId ||
        widget.season != oldWidget.season ||
        widget.currentSeason != oldWidget.currentSeason ||
        widget.revision != oldWidget.revision) {
      _future = _load();
    }
  }

  @override
  Widget build(BuildContext context) => FutureBuilder<Map<String, dynamic>>(
    future: _future,
    builder: (context, snapshot) {
      if (snapshot.connectionState != ConnectionState.done) {
        return const _Loading('CARREGANDO PREMIAÇÕES...');
      }
      if (snapshot.hasError) {
        return _LoadError(
          'premiações',
          () => setState(() {
            _future = _load();
          }),
        );
      }
      final data = snapshot.data!;
      final awards = _records(data['awards']);
      final totals = _records(data['clubTotals']);
      final season = data['season'];
      return Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        mainAxisSize: MainAxisSize.min,
        children: [
          Text(
            'ARTILHEIROS DA TEMPORADA${season == null ? '' : ' $season'}',
            style: _headingStyle,
          ),
          if (widget.season == null)
            const Text(
              'Última temporada encerrada.',
              style: TextStyle(fontSize: 12),
            ),
          _CoverageWarnings(data['coverageWarnings']),
          if (awards.isEmpty)
            Text(
              season == null
                  ? 'Nenhuma temporada encerrada com premiações registradas.'
                  : 'Nenhuma premiação registrada nesta temporada.',
            ),
          if (awards.isNotEmpty) ...[
            const Text(
              'Todos os empatados em gols recebem o título. Prêmio por artilheiro ao clube: 3.000.000.',
              style: TextStyle(fontSize: 12),
            ),
            _ClassicTable(
              tableKey: 'season-scorer-awards',
              columns: const [
                'JOGADOR',
                'CLUBE NO ENCERRAMENTO',
                'GOLS',
                'JOGOS',
                'PRÊMIO AO CLUBE',
              ],
              rows: awards
                  .map(
                    (a) => DataRow(
                      cells: [
                        _playerCell(
                          context,
                          widget.apiClient,
                          widget.worldId,
                          a,
                        ),
                        DataCell(
                          Text(a['clubNameAtClose']?.toString() ?? 'Sem clube'),
                        ),
                        DataCell(Text(_value(a['totalGoals']))),
                        DataCell(Text(_value(a['totalMatches']))),
                        DataCell(Text(_money(a['awardAmount']))),
                      ],
                    ),
                  )
                  .toList(),
            ),
          ],
          if (totals.isNotEmpty) ...[
            const SizedBox(height: 8),
            const Text('TOTAL CREDITADO POR CLUBE', style: _headingStyle),
            _ClassicTable(
              tableKey: 'season-club-awards',
              columns: const ['CLUBE', 'TOTAL'],
              rows: totals
                  .map(
                    (c) => DataRow(
                      cells: [
                        DataCell(Text(_value(c['clubName']))),
                        DataCell(Text(_money(c['amount']))),
                      ],
                    ),
                  )
                  .toList(),
            ),
          ],
        ],
      );
    },
  );
}

class _ClassicTable extends StatefulWidget {
  final String tableKey;
  final List<String> columns;
  final List<DataRow> rows;
  const _ClassicTable({
    required this.tableKey,
    required this.columns,
    required this.rows,
  });
  @override
  State<_ClassicTable> createState() => _ClassicTableState();
}

class _ClassicTableState extends State<_ClassicTable> {
  final _horizontal = ScrollController();
  @override
  void dispose() {
    _horizontal.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => Scrollbar(
    controller: _horizontal,
    child: SingleChildScrollView(
      key: ValueKey('${widget.tableKey}-horizontal'),
      controller: _horizontal,
      scrollDirection: Axis.horizontal,
      padding: const EdgeInsets.only(bottom: 10),
      child: DataTable(
        key: ValueKey(widget.tableKey),
        headingRowColor: WidgetStateProperty.all(_green),
        headingRowHeight: 36,
        dataRowMinHeight: 36,
        dataRowMaxHeight: 44,
        columnSpacing: 18,
        horizontalMargin: 8,
        dataTextStyle: const TextStyle(fontSize: 12, fontFamily: 'monospace'),
        headingTextStyle: const TextStyle(
          fontSize: 11,
          fontWeight: FontWeight.bold,
          color: _gold,
        ),
        columns: widget.columns.map((c) => DataColumn(label: Text(c))).toList(),
        rows: widget.rows,
      ),
    ),
  );
}

class _CoverageWarnings extends StatelessWidget {
  final dynamic warnings;
  const _CoverageWarnings(this.warnings);
  @override
  Widget build(BuildContext context) => Column(
    crossAxisAlignment: CrossAxisAlignment.start,
    children: [
      for (final warning in warnings as List? ?? [])
        Padding(
          padding: const EdgeInsets.symmetric(vertical: 4),
          child: Text(
            'COBERTURA: $warning',
            style: const TextStyle(color: _gold, fontSize: 12),
          ),
        ),
    ],
  );
}

class _Loading extends StatelessWidget {
  final String message;
  const _Loading(this.message);
  @override
  Widget build(BuildContext context) =>
      Padding(padding: const EdgeInsets.all(8), child: Text(message));
}

class _LoadError extends StatelessWidget {
  final String subject;
  final VoidCallback retry;
  const _LoadError(this.subject, this.retry);
  @override
  Widget build(BuildContext context) => Column(
    crossAxisAlignment: CrossAxisAlignment.start,
    children: [
      Text('Não foi possível carregar $subject. Tente novamente.'),
      TextButton(onPressed: retry, child: const Text('TENTAR NOVAMENTE')),
    ],
  );
}
