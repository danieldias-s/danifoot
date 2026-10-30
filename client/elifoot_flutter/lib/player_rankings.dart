import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import 'api_client.dart';

const _gold = Color(0xFFFFD700);
const _green = Color(0xFF062E0F);
const _panelGreen = Color(0xFF0E3B19);
const _rowEvenGreen = Color(0xFF0B3515);
const _rowOddGreen = Color(0xFF10421C);
const _rowTopGreen = Color(0xFF185728);
const _borderGreen = Color(0xFF3E8450);
const _mutedText = Color(0xFFD2EBD6);

const _headingStyle = TextStyle(
  color: _gold,
  fontWeight: FontWeight.bold,
  fontFamily: 'monospace',
  letterSpacing: 0.4,
);

final _guidPattern = RegExp(
  r'^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$',
);

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

/// Formats top scorer achievement counts with correct Portuguese pluralization.
/// Examples:
/// - 0 -> `ARTILHEIRO: 0 VEZES`
/// - 1 -> `ARTILHEIRO: 1 VEZ`
/// - 2 -> `ARTILHEIRO: 2 VEZES`
String formatTopScorerTitles(dynamic value) {
  final count = math.max(0, _integer(value));
  final unit = count == 1 ? 'VEZ' : 'VEZES';
  return 'ARTILHEIRO: $count $unit';
}

/// Formats financial values using the classic Elifoot 98 Brazilian Real (`R$`) convention.
/// Examples:
/// - `3000000` -> `R$ 3.000.000`
/// - `0` -> `R$ 0`
String formatClassicMoney(dynamic value, {bool includeCurrency = true}) {
  if (value is! num) return '—';
  final negative = value < 0;
  final formattedDigits = value
      .abs()
      .toStringAsFixed(0)
      .replaceAllMapped(
        RegExp(r'(\d)(?=(\d{3})+(?!\d))'),
        (match) => '${match[1]}.',
      );
  if (!includeCurrency) {
    return negative ? '-$formattedDigits' : formattedDigits;
  }
  return negative ? '-R\$ $formattedDigits' : 'R\$ $formattedDigits';
}

/// Formats backend ISO-8601 timestamps into readable Brazilian Portuguese dates (`dd/MM/yyyy`
/// or `dd/MM/yyyy HH:mm`) without mutating the underlying persisted timestamp.
/// Uses deterministic UTC calendar components when an ISO UTC/offset timestamp is provided.
String formatClassicDate(dynamic value, {bool includeTime = false}) {
  if (value == null) return '—';
  if (value is DateTime) {
    return _formatDateParts(value.toUtc(), includeTime: includeTime);
  }
  final raw = value.toString().trim();
  if (raw.isEmpty || raw == '—') return '—';

  final alreadyFormatted = RegExp(
    r'^\d{2}/\d{2}/\d{4}(?:\s+\d{2}:\d{2})?$',
  ).hasMatch(raw);
  if (alreadyFormatted) return raw;

  // Normalize sub-microsecond ISO fractional digits (e.g. 7-8 digits from .NET DateTimeOffset).
  final normalized = raw.replaceFirstMapped(
    RegExp(r'\.(\d{6})\d+'),
    (match) => '.${match[1]}',
  );
  final parsed = DateTime.tryParse(normalized);
  if (parsed == null) return '—';
  final dt = parsed.isUtc ? parsed : parsed.toUtc();
  return _formatDateParts(dt, includeTime: includeTime);
}

String _formatDateParts(DateTime dt, {required bool includeTime}) {
  final day = dt.day.toString().padLeft(2, '0');
  final month = dt.month.toString().padLeft(2, '0');
  final year = dt.year.toString().padLeft(4, '0');
  if (!includeTime) return '$day/$month/$year';
  final hour = dt.hour.toString().padLeft(2, '0');
  final minute = dt.minute.toString().padLeft(2, '0');
  return '$day/$month/$year $hour:$minute';
}

/// Checks whether a string is a raw internal GUID/UUID that must not be shown in the UI.
bool isRawGuid(String? value) {
  if (value == null) return false;
  return _guidPattern.hasMatch(value.trim());
}

/// Session-scoped registry of club IDs to club names observed from official API payloads.
/// Used only as an exact-ID fallback for legacy snapshots where `clubName` was not embedded
/// on historical row DTOs; never substitutes a player's current club for a different club ID.
class ClubNameDirectory {
  static final Map<String, String> _byId = <String, String>{};

  static void register(dynamic rawClubId, dynamic rawClubName) {
    final id = rawClubId?.toString().trim() ?? '';
    final name = rawClubName?.toString().trim() ?? '';
    if (id.isEmpty || name.isEmpty || name == '—' || name == 'Sem clube') {
      return;
    }
    if (isRawGuid(name)) return;
    _byId[id.toLowerCase()] = name;
  }

  static void registerRankingPayload(Map<String, dynamic> data) {
    for (final row in _records(data['rows'])) {
      register(row['currentClubId'], row['currentClubName']);
    }
    for (final row in _records(data['notEligible'])) {
      register(row['currentClubId'], row['currentClubName']);
    }
  }

  static void registerAwardsPayload(Map<String, dynamic> data) {
    for (final award in _records(data['awards'])) {
      register(award['clubId'], award['clubNameAtClose']);
    }
    for (final total in _records(data['clubTotals'])) {
      register(total['clubId'], total['clubName']);
    }
  }

  static void registerHistoryPayload(Map<String, dynamic> data) {
    final player = Map<String, dynamic>.from(data['player'] as Map? ?? {});
    register(player['currentClubId'], player['currentClubName']);
    for (final award in _records(data['awards'])) {
      register(award['clubId'], award['clubNameAtClose']);
    }
    for (final stat in _records(data['statistics'])) {
      register(stat['clubId'], stat['clubName']);
    }
  }

  static String? lookup(String clubId) => _byId[clubId.trim().toLowerCase()];

  @visibleForTesting
  static void clearForTesting() => _byId.clear();
}

/// Resolves the historical club name for a player's season/competition statistics row.
/// Priority:
/// 1. Explicit `clubName` persisted/returned on the statistics row.
/// 2. Exact `clubId` match in the player's awards (`clubId -> clubNameAtClose`).
/// 3. Exact `clubId` match in `ClubNameDirectory` (which also includes the player's current club
///    if and only if `row['clubId'] == player['currentClubId']`).
/// 4. If `clubId` is a human-readable identifier (not a GUID), returns it directly.
/// 5. If `clubId` is an unresolvable raw GUID, logs a technical diagnostic and returns
///    `Clube histórico indisponível`.
String resolveHistoricalClubName(
  Map<String, dynamic> row, {
  Map<String, dynamic>? player,
  List<Map<String, dynamic>> awards = const [],
  Map<String, String> knownClubs = const {},
}) {
  final explicitName = row['clubName']?.toString().trim() ?? '';
  if (explicitName.isNotEmpty &&
      explicitName != '—' &&
      !isRawGuid(explicitName)) {
    return explicitName;
  }

  final rawClubId = row['clubId']?.toString().trim() ?? '';
  if (rawClubId.isEmpty ||
      rawClubId == '00000000-0000-0000-0000-000000000000') {
    return 'Sem clube';
  }

  final normalizedId = rawClubId.toLowerCase();

  if (knownClubs.isNotEmpty) {
    for (final entry in knownClubs.entries) {
      if (entry.key.trim().toLowerCase() == normalizedId &&
          entry.value.trim().isNotEmpty &&
          !isRawGuid(entry.value)) {
        return entry.value.trim();
      }
    }
  }

  for (final award in awards) {
    final awardClubId = award['clubId']?.toString().trim().toLowerCase() ?? '';
    final awardClubName = award['clubNameAtClose']?.toString().trim() ?? '';
    if (awardClubId == normalizedId &&
        awardClubName.isNotEmpty &&
        awardClubName != 'Sem clube' &&
        !isRawGuid(awardClubName)) {
      return awardClubName;
    }
  }

  if (player != null) {
    final currentId =
        player['currentClubId']?.toString().trim().toLowerCase() ?? '';
    final currentName = player['currentClubName']?.toString().trim() ?? '';
    if (currentId == normalizedId &&
        currentName.isNotEmpty &&
        !isRawGuid(currentName)) {
      return currentName;
    }
  }

  final cached = ClubNameDirectory.lookup(rawClubId);
  if (cached != null && cached.isNotEmpty) {
    return cached;
  }

  if (!isRawGuid(rawClubId)) {
    return rawClubId;
  }

  debugPrint(
    '[Elifoot010] Clube histórico indisponível para clubId=$rawClubId '
    '(season=${row['season']}, player=${player?['playerId'] ?? 'unknown'})',
  );
  return 'Clube histórico indisponível';
}

String _playerName(Map<String, dynamic> player) {
  final name = _value(player['displayName']);
  return player['isStar'] == true && !name.endsWith('*') ? '$name*' : name;
}

String _club(Map<String, dynamic> player) {
  if (player['currentClubId'] == null) return 'Sem clube';
  final name = player['currentClubName']?.toString().trim() ?? '';
  if (name.isEmpty) return 'Sem clube';
  if (isRawGuid(name)) {
    return ClubNameDirectory.lookup(name) ?? 'Clube histórico indisponível';
  }
  return name;
}

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
    ClubNameDirectory.registerRankingPayload(data);
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

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, viewport) {
        final isCompact = viewport.maxWidth < 420;
        final horizontalPad = isCompact ? 8.0 : 16.0;
        return Align(
          alignment: Alignment.topCenter,
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 1160),
            child: SingleChildScrollView(
              key: const ValueKey('player-rankings-scroll'),
              padding: EdgeInsets.symmetric(
                horizontal: horizontalPad,
                vertical: 12,
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  const ClassicSectionTitle(
                    title: 'RANKINGS DE JOGADORES',
                    subtitle:
                        'Somente partidas oficiais concluídas. Toque no jogador para ver o histórico.',
                  ),
                  const SizedBox(height: 10),
                  _buildFilterBar(viewport.maxWidth),
                  if (_category != 'scorers') ...[
                    const SizedBox(height: 8),
                    Container(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 10,
                        vertical: 6,
                      ),
                      decoration: BoxDecoration(
                        color: _green,
                        border: Border.all(color: _borderGreen),
                      ),
                      child: const Text(
                        'Mínimo de 20 jogos no recorte consultado.',
                        style: TextStyle(fontSize: 12, color: _gold),
                      ),
                    ),
                  ],
                  const SizedBox(height: 10),
                  FutureBuilder<Map<String, dynamic>>(
                    future: _future,
                    builder: (context, snapshot) {
                      if (snapshot.connectionState != ConnectionState.done) {
                        return const _Loading('CARREGANDO RANKING...');
                      }
                      if (snapshot.hasError) {
                        return _LoadError('ranking', _reload);
                      }
                      final data = snapshot.data!;
                      final minimum = math.max(
                        20,
                        _integer(data['minimumMatches']),
                      );
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
                                    (p) =>
                                        _integer(p['matchesPlayed']) < minimum,
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
                        crossAxisAlignment: CrossAxisAlignment.stretch,
                        children: [
                          _CoverageWarnings(data['coverageWarnings']),
                          ClassicTableHeader(
                            title: _period == 'career'
                                ? 'CARREIRA INTEIRA'
                                : 'TEMPORADA ${data['season'] ?? data['currentSeason'] ?? _season}',
                            trailing: _categoryBadgeLabel(_category),
                          ),
                          const SizedBox(height: 4),
                          if (rows.isEmpty)
                            Container(
                              padding: const EdgeInsets.all(12),
                              decoration: BoxDecoration(
                                color: _panelGreen,
                                border: Border.all(color: _borderGreen),
                              ),
                              child: const Text(
                                'Nenhum jogador elegível com estatísticas oficiais neste recorte.',
                              ),
                            )
                          else
                            _rankingTable(rows),
                          if (ineligible.isNotEmpty) ...[
                            const SizedBox(height: 16),
                            ClassicTableHeader(
                              title: 'AINDA NÃO ELEGÍVEIS',
                              subtitle:
                                  'Faltam jogos para alcançar $minimum neste recorte.',
                            ),
                            const SizedBox(height: 4),
                            _rankingTable(ineligible, minimum: minimum),
                          ],
                        ],
                      );
                    },
                  ),
                  const SizedBox(height: 20),
                  SeasonTopScorersPanel(
                    apiClient: widget.apiClient,
                    worldId: widget.worldId,
                    season: _period == 'season' ? _season : null,
                    revision: widget.revision + _request,
                    currentSeason: widget.currentSeason,
                  ),
                ],
              ),
            ),
          ),
        );
      },
    );
  }

  String _categoryBadgeLabel(String category) => switch (category) {
    'goalkeepers' => 'GOLEIROS (MENOR MÉDIA SOFRIDA)',
    'defenders' => 'DEFENSORES (MENOR ÍNDICE DISCIPLINAR)',
    _ => 'ARTILHEIROS (MAIOR Nº DE GOLS)',
  };

  Widget _buildFilterBar(double maxWidth) {
    final isNarrow = maxWidth < 520;
    final isUltraNarrow = maxWidth < 365;
    final filterWidth = isUltraNarrow
        ? (maxWidth - 20)
        : isNarrow
        ? ((maxWidth - 28) / 2).clamp(140.0, 220.0)
        : 205.0;

    return Container(
      padding: EdgeInsets.all(isNarrow ? 8 : 10),
      decoration: BoxDecoration(
        color: _panelGreen,
        border: Border.all(color: _borderGreen),
      ),
      child: Wrap(
        spacing: 10,
        runSpacing: 10,
        crossAxisAlignment: WrapCrossAlignment.center,
        children: [
          ClassicFilter(
            filterKey: 'ranking-category',
            label: 'CATEGORIA',
            value: _category,
            width: filterWidth,
            options: const {
              'scorers': 'Artilheiros',
              'goalkeepers': 'Goleiros',
              'defenders': 'Defensores',
            },
            onChanged: (v) {
              setState(() {
                _category = v;
                _future = _load();
              });
            },
          ),
          ClassicFilter(
            filterKey: 'ranking-scope',
            label: 'COMPETIÇÃO',
            value: _scope,
            width: filterWidth,
            options: const {
              'general': 'Geral',
              'division1': '1ª divisão',
              'division2': '2ª divisão',
              'division3': '3ª divisão',
              'division4': '4ª divisão',
              'cup': 'Taça',
            },
            onChanged: (v) {
              setState(() {
                _scope = v;
                _future = _load();
              });
            },
          ),
          ClassicFilter(
            filterKey: 'ranking-period',
            label: 'PERÍODO',
            value: _period,
            width: filterWidth,
            options: const {
              'career': 'Carreira inteira',
              'current': 'Temporada atual',
              'season': 'Temporada selecionada',
            },
            onChanged: (v) {
              setState(() {
                _period = v;
                _future = _load();
              });
            },
          ),
          if (_period == 'season')
            ClassicFilter(
              filterKey: 'ranking-season',
              label: 'TEMPORADA',
              value: '$_season',
              width: filterWidth,
              options: {
                for (final s in {..._seasons, widget.currentSeason, _season})
                  '$s': 'Temporada $s',
              },
              onChanged: (v) {
                setState(() {
                  _season = int.parse(v);
                  _future = _load();
                });
              },
            ),
          ClassicActionButton(
            label: 'ATUALIZAR',
            iconType: ClassicButtonIcon.refresh,
            onPressed: _reload,
          ),
        ],
      ),
    );
  }

  Widget _rankingTable(List<Map<String, dynamic>> players, {int? minimum}) {
    final columns = <String>[
      '#',
      'JOGADOR',
      'CLUBE ATUAL',
      'JOGOS',
      if (_category == 'scorers') 'GOLS',
      if (_category == 'goalkeepers') 'GOLS SOFRIDOS',
      if (_category == 'defenders') ...['AMARELOS', 'VERMELHOS'],
      _category == 'defenders' ? 'ÍNDICE' : 'MÉDIA',
      if (minimum != null) 'JOGOS FALTANTES',
    ];
    final numericColumns = <int>{
      0,
      for (var i = 3; i < columns.length; i++) i,
    };

    return ClassicTable(
      tableKey: minimum == null ? 'ranking-table' : 'not-eligible-table',
      columns: columns,
      numericColumns: numericColumns,
      pinnedHeader: '#  JOGADOR',
      pinnedRows: players.asMap().entries.map((entry) {
        final index = entry.key;
        final p = entry.value;
        final rankValue = minimum == null ? _value(p['rank']) : '—';
        final rankInt = _integer(p['rank']);
        return ClassicPinnedRow(
          rank: rankValue,
          title: _playerName(p),
          subtitle: _club(p),
          highlight: minimum == null && rankInt >= 1 && rankInt <= 3,
          isEven: index.isEven,
          onTap: p['playerId'] == null
              ? null
              : () => showPlayerHistoryDialog(
                  context: context,
                  apiClient: widget.apiClient,
                  worldId: widget.worldId,
                  playerId: p['playerId'].toString(),
                ),
        );
      }).toList(),
      rows: players.asMap().entries.map((entry) {
        final index = entry.key;
        final p = entry.value;
        final rankInt = _integer(p['rank']);
        final isTop3 = minimum == null && rankInt >= 1 && rankInt <= 3;
        final rowColor = isTop3
            ? _rowTopGreen
            : (index.isEven ? _rowEvenGreen : _rowOddGreen);
        return DataRow(
          color: WidgetStateProperty.all(rowColor),
          cells: [
            DataCell(
              Text(
                minimum == null ? _value(p['rank']) : '—',
                style: TextStyle(
                  color: isTop3 ? _gold : Colors.white,
                  fontWeight: isTop3 ? FontWeight.bold : FontWeight.normal,
                ),
              ),
            ),
            _playerCell(context, widget.apiClient, widget.worldId, p),
            DataCell(Text(_club(p))),
            DataCell(Text(_value(p['matchesPlayed']))),
            if (_category == 'scorers') DataCell(Text(_value(p['goalsScored']))),
            if (_category == 'goalkeepers')
              DataCell(Text(_value(p['goalsConceded']))),
            if (_category == 'defenders') ...[
              DataCell(Text(_value(p['yellowCards']))),
              DataCell(Text(_value(p['redCards']))),
            ],
            DataCell(Text(_average(p['average']))),
            if (minimum != null)
              DataCell(
                Text('${math.max(0, minimum - _integer(p['matchesPlayed']))}'),
              ),
          ],
        );
      }).toList(),
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
    style: const TextStyle(
      color: _gold,
      decoration: TextDecoration.underline,
      fontWeight: FontWeight.bold,
    ),
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

  Future<Map<String, dynamic>> _load() => _forBuilder(_fetch());

  Future<Map<String, dynamic>> _fetch() async {
    final data = await widget.apiClient.getPlayerHistory(
      widget.worldId,
      widget.playerId,
    );
    ClubNameDirectory.registerHistoryPayload(data);
    return data;
  }

  @override
  Widget build(BuildContext context) {
    return ClassicDialog(
      title: 'HISTÓRICO DO JOGADOR',
      scrollKey: const ValueKey('player-history-scroll'),
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
          final seasonRowsByKey = <String, Map<String, dynamic>>{};
          for (final row in statistics) {
            final compLabel = _competition(row);
            final total = totals.putIfAbsent(
              compLabel,
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

            final clubName = resolveHistoricalClubName(
              row,
              player: player,
              awards: awards,
            );
            final incomplete = row['hasIncompleteAppearances'] == true;
            final seasonKey =
                '${row['season']}|$compLabel|${row['clubId'] ?? ''}|$clubName|$incomplete';
            final aggregated = seasonRowsByKey.putIfAbsent(
              seasonKey,
              () => {
                'season': row['season'],
                'competitionLabel': compLabel,
                'clubName': clubName,
                'matchesPlayed': 0,
                'goalsScored': 0,
                'goalsConceded': 0,
                'yellowCards': 0,
                'redCards': 0,
                'minutesPlayed': 0,
                'hasIncompleteAppearances': incomplete,
              },
            );
            for (final key in const [
              'matchesPlayed',
              'goalsScored',
              'goalsConceded',
              'yellowCards',
              'redCards',
              'minutesPlayed',
            ]) {
              aggregated[key] =
                  _integer(aggregated[key]) + _integer(row[key]);
            }
          }
          final seasonRows = seasonRowsByKey.values.toList();

          final pos = _value(player['position']).toUpperCase();
          final isGoalkeeper = pos.startsWith('G');
          final isDefender =
              pos.startsWith('D') || pos.startsWith('Z') || pos.startsWith('L');

          return Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            mainAxisSize: MainAxisSize.min,
            children: [
              // SEÇÃO 1 — IDENTIFICAÇÃO
              Container(
                padding: const EdgeInsets.all(10),
                decoration: BoxDecoration(
                  color: _green,
                  border: Border.all(color: _gold, width: 1),
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    const Text(
                      'IDENTIFICAÇÃO DO ATLETA',
                      style: TextStyle(
                        color: _mutedText,
                        fontSize: 10,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                    const SizedBox(height: 2),
                    Text(
                      _playerName(player),
                      style: const TextStyle(
                        color: _gold,
                        fontSize: 15,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                    const SizedBox(height: 6),
                    Wrap(
                      spacing: 16,
                      runSpacing: 4,
                      children: [
                        Text(
                          'Posição: ${_value(player['position'])} | Força: ${_value(player['strength'])}',
                          style: const TextStyle(fontSize: 12),
                        ),
                        Text(
                          'Comportamento: ${_value(player['behaviorDisplay'])}',
                          style: const TextStyle(fontSize: 12),
                        ),
                        Text(
                          'Craque (*): ${player['isStar'] == true ? 'Sim (*)' : 'Não'}',
                          style: const TextStyle(fontSize: 12),
                        ),
                        Text(
                          'Clube atual: ${_club(player)}',
                          style: const TextStyle(fontSize: 12),
                        ),
                      ],
                    ),
                  ],
                ),
              ),
              const SizedBox(height: 10),

              // SEÇÃO 2 — RESUMO DE CARREIRA
              const ClassicSectionTitle(title: 'RESUMO DE CARREIRA'),
              const SizedBox(height: 4),
              Container(
                padding: const EdgeInsets.all(10),
                decoration: BoxDecoration(
                  color: _rowEvenGreen,
                  border: Border.all(color: _borderGreen),
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Container(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 8,
                        vertical: 4,
                      ),
                      decoration: BoxDecoration(
                        color: _green,
                        border: Border.all(color: _gold),
                      ),
                      child: Text(
                        formatTopScorerTitles(player['seasonTopScorerTitles']),
                        key: const ValueKey('player-scorer-titles'),
                        style: _headingStyle,
                      ),
                    ),
                    const SizedBox(height: 8),
                    PlayerStatisticRow(
                      items: [
                        PlayerStatItem('JOGOS', _value(career['matchesPlayed'])),
                        if (isGoalkeeper) ...[
                          PlayerStatItem(
                            'GOLS SOFRIDOS',
                            _value(career['goalsConceded']),
                            highlight: true,
                          ),
                          PlayerStatItem('MÉDIA', _average(career['average'])),
                          PlayerStatItem(
                            'GOLS MARCADOS',
                            _value(career['goalsScored']),
                          ),
                        ] else if (isDefender) ...[
                          PlayerStatItem(
                            'AMARELOS',
                            _value(career['yellowCards']),
                            highlight: true,
                          ),
                          PlayerStatItem(
                            'VERMELHOS',
                            _value(career['redCards']),
                            highlight: true,
                          ),
                          PlayerStatItem('GOLS', _value(career['goalsScored'])),
                        ] else ...[
                          PlayerStatItem(
                            'GOLS',
                            _value(career['goalsScored']),
                            highlight: true,
                          ),
                          PlayerStatItem('MÉDIA', _average(career['average'])),
                          PlayerStatItem(
                            'CARTÕES (A/V)',
                            '${_value(career['yellowCards'])}/${_value(career['redCards'])}',
                          ),
                        ],
                        PlayerStatItem(
                          'MINUTOS',
                          _value(career['minutesPlayed']),
                        ),
                      ],
                    ),
                    const SizedBox(height: 6),
                    Text(
                      'Total de jogos: ${_value(career['matchesPlayed'])} | Total de gols: ${_value(career['goalsScored'])}',
                      style: const TextStyle(fontSize: 12),
                    ),
                    Text(
                      'Gols sofridos: ${_value(career['goalsConceded'])} | Amarelos: ${_value(career['yellowCards'])} | Vermelhos: ${_value(career['redCards'])}',
                      style: const TextStyle(fontSize: 12),
                    ),
                    Text(
                      'Minutos jogados: ${_value(career['minutesPlayed'])}',
                      style: const TextStyle(fontSize: 12),
                    ),
                  ],
                ),
              ),
              _CoverageWarnings(data['coverageWarnings']),
              if (statistics.any(
                (s) => s['hasIncompleteAppearances'] == true,
              ))
                const Padding(
                  padding: EdgeInsets.only(top: 4),
                  child: Text(
                    'COBERTURA: Há aparições legadas incompletas; jogos e minutos podem estar parciais.',
                    style: TextStyle(color: _gold, fontSize: 12),
                  ),
                ),
              const SizedBox(height: 12),

              // SEÇÃO 3 — ESTATÍSTICAS POR COMPETIÇÃO
              const ClassicSectionTitle(
                title: 'JOGOS E GOLS POR COMPETIÇÃO — CARREIRA',
              ),
              const SizedBox(height: 4),
              if (totals.isEmpty)
                const Text('Sem estatísticas oficiais registradas.')
              else
                ClassicTable(
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
                  numericColumns: const {1, 2, 3, 4, 5, 6},
                  rows: totals.entries.toList().asMap().entries.map((entry) {
                    final idx = entry.key;
                    final e = entry.value;
                    return DataRow(
                      color: WidgetStateProperty.all(
                        idx.isEven ? _rowEvenGreen : _rowOddGreen,
                      ),
                      cells: [
                        DataCell(Text(e.key)),
                        for (final value in e.value.values)
                          DataCell(Text('$value')),
                      ],
                    );
                  }).toList(),
                ),
              const SizedBox(height: 12),

              // SEÇÃO 4 — HISTÓRICO DE TEMPORADAS
              const ClassicSectionTitle(title: 'HISTÓRICO DE TEMPORADAS'),
              const SizedBox(height: 4),
              if (seasonRows.isEmpty)
                const Text('Sem temporadas com partidas oficiais concluídas.')
              else
                ClassicTable(
                  tableKey: 'player-season-history',
                  columns: const [
                    'TEMPORADA',
                    'COMPETIÇÃO',
                    'CLUBE',
                    'JOGOS',
                    'GOLS',
                    'GOLS SOFRIDOS',
                    'AMARELOS',
                    'VERMELHOS',
                    'MINUTOS',
                    'COBERTURA',
                  ],
                  numericColumns: const {0, 3, 4, 5, 6, 7, 8},
                  rows: seasonRows.asMap().entries.map((entry) {
                    final idx = entry.key;
                    final s = entry.value;
                    return DataRow(
                      color: WidgetStateProperty.all(
                        idx.isEven ? _rowEvenGreen : _rowOddGreen,
                      ),
                      cells: [
                        DataCell(Text(_value(s['season']))),
                        DataCell(Text(_value(s['competitionLabel']))),
                        DataCell(Text(_value(s['clubName']))),
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
                    );
                  }).toList(),
                ),
              const SizedBox(height: 12),

              // SEÇÃO 5 — CONQUISTAS DE ARTILHARIA
              const ClassicSectionTitle(title: 'CONQUISTAS DE ARTILHARIA'),
              const SizedBox(height: 4),
              if (awards.isEmpty)
                const Text('Nenhuma artilharia de temporada registrada.')
              else
                ClassicTable(
                  tableKey: 'player-award-history',
                  columns: const [
                    'TEMPORADA',
                    'GOLS',
                    'JOGOS',
                    'CLUBE NO ENCERRAMENTO',
                    'PRÊMIO AO CLUBE',
                    'DATA',
                  ],
                  numericColumns: const {0, 1, 2, 4},
                  rows: awards.asMap().entries.map((entry) {
                    final idx = entry.key;
                    final a = entry.value;
                    return DataRow(
                      key: ValueKey('player-award-${a['awardId']}'),
                      color: WidgetStateProperty.all(
                        idx.isEven ? _rowEvenGreen : _rowOddGreen,
                      ),
                      cells: [
                        DataCell(Text(_value(a['season']))),
                        DataCell(Text(_value(a['totalGoals']))),
                        DataCell(Text(_value(a['totalMatches']))),
                        DataCell(
                          Text(a['clubNameAtClose']?.toString() ?? 'Sem clube'),
                        ),
                        DataCell(Text(formatClassicMoney(a['awardAmount']))),
                        DataCell(Text(formatClassicDate(a['awardGrantedAt']))),
                      ],
                    );
                  }).toList(),
                ),
            ],
          );
        },
      ),
    );
  }
}

Future<void> showSeasonTopScorersDialog({
  required BuildContext context,
  required ElifootApiClient apiClient,
  required String worldId,
  required int season,
}) => showDialog<void>(
  context: context,
  builder: (context) => ClassicDialog(
    dialogKey: const ValueKey('season-end-awards-dialog'),
    title: 'ENCERRAMENTO DA TEMPORADA $season',
    child: SeasonTopScorersPanel(
      apiClient: apiClient,
      worldId: worldId,
      season: season,
    ),
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

  Future<Map<String, dynamic>> _load() => _forBuilder(_fetch());

  Future<Map<String, dynamic>> _fetch() async {
    final data = await widget.apiClient.getSeasonTopScorers(
      widget.worldId,
      season: widget.season,
    );
    ClubNameDirectory.registerAwardsPayload(data);
    return data;
  }

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
      final isTie = awards.length > 1;
      return Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        mainAxisSize: MainAxisSize.min,
        children: [
          ClassicSectionTitle(
            title: 'ARTILHEIROS DA TEMPORADA${season == null ? '' : ' $season'}',
            subtitle: widget.season == null
                ? 'Última temporada encerrada.'
                : null,
          ),
          _CoverageWarnings(data['coverageWarnings']),
          if (awards.isEmpty)
            Padding(
              padding: const EdgeInsets.only(top: 6),
              child: Text(
                season == null
                    ? 'Nenhuma temporada encerrada com premiações registradas.'
                    : 'Nenhuma premiação registrada nesta temporada.',
              ),
            ),
          if (awards.isNotEmpty) ...[
            const SizedBox(height: 6),
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
              decoration: BoxDecoration(
                color: _green,
                border: Border.all(color: isTie ? _gold : _borderGreen),
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  const Text(
                    'Todos os empatados em gols recebem o título. Prêmio por artilheiro ao clube: R\$ 3.000.000.',
                    style: TextStyle(fontSize: 12),
                  ),
                  if (isTie)
                    Padding(
                      padding: const EdgeInsets.only(top: 2),
                      child: Text(
                        'EMPATE NA ARTILHARIA: ${awards.length} ARTILHEIROS EMPATADOS COM ${_value(awards.first['totalGoals'])} GOLS.',
                        style: const TextStyle(
                          color: _gold,
                          fontSize: 11,
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                    ),
                ],
              ),
            ),
            const SizedBox(height: 6),
            ClassicTable(
              tableKey: 'season-scorer-awards',
              columns: const [
                'JOGADOR',
                'CLUBE NO ENCERRAMENTO',
                'GOLS',
                'JOGOS',
                'PRÊMIO AO CLUBE',
              ],
              numericColumns: const {2, 3, 4},
              pinnedHeader: 'JOGADOR',
              pinnedRows: awards.asMap().entries.map((entry) {
                final idx = entry.key;
                final a = entry.value;
                return ClassicPinnedRow(
                  title: _playerName(a),
                  subtitle: a['clubNameAtClose']?.toString() ?? 'Sem clube',
                  highlight: true,
                  isEven: idx.isEven,
                  onTap: a['playerId'] == null
                      ? null
                      : () => showPlayerHistoryDialog(
                          context: context,
                          apiClient: widget.apiClient,
                          worldId: widget.worldId,
                          playerId: a['playerId'].toString(),
                        ),
                );
              }).toList(),
              rows: awards.asMap().entries.map((entry) {
                final idx = entry.key;
                final a = entry.value;
                return DataRow(
                  color: WidgetStateProperty.all(
                    idx.isEven ? _rowTopGreen : _rowOddGreen,
                  ),
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
                    DataCell(Text(formatClassicMoney(a['awardAmount']))),
                  ],
                );
              }).toList(),
            ),
          ],
          if (totals.isNotEmpty) ...[
            const SizedBox(height: 12),
            const ClassicTableHeader(title: 'TOTAL CREDITADO POR CLUBE'),
            const SizedBox(height: 4),
            ClassicTable(
              tableKey: 'season-club-awards',
              columns: const ['CLUBE', 'TOTAL'],
              numericColumns: const {1},
              rows: totals.asMap().entries.map((entry) {
                final idx = entry.key;
                final c = entry.value;
                return DataRow(
                  color: WidgetStateProperty.all(
                    idx.isEven ? _rowEvenGreen : _rowOddGreen,
                  ),
                  cells: [
                    DataCell(Text(_value(c['clubName']))),
                    DataCell(Text(formatClassicMoney(c['amount']))),
                  ],
                );
              }).toList(),
            ),
          ],
        ],
      );
    },
  );
}

// ============================================================================
// SHARED CLASSIC COMPONENTS (SECTION 17)
// ============================================================================

/// Classic Elifoot 98 section title banner with yellow header typography.
class ClassicSectionTitle extends StatelessWidget {
  final String title;
  final String? subtitle;

  const ClassicSectionTitle({
    super.key,
    required this.title,
    this.subtitle,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
      decoration: const BoxDecoration(
        color: _green,
        border: Border(
          left: BorderSide(color: _gold, width: 3),
          top: BorderSide(color: _borderGreen, width: 1),
          right: BorderSide(color: _borderGreen, width: 1),
          bottom: BorderSide(color: _borderGreen, width: 1),
        ),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(title, style: _headingStyle),
          if (subtitle != null) ...[
            const SizedBox(height: 2),
            Text(
              subtitle!,
              style: const TextStyle(fontSize: 11, color: _mutedText),
            ),
          ],
        ],
      ),
    );
  }
}

/// Classic Elifoot 98 table section subheader.
class ClassicTableHeader extends StatelessWidget {
  final String title;
  final String? subtitle;
  final String? trailing;

  const ClassicTableHeader({
    super.key,
    required this.title,
    this.subtitle,
    this.trailing,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
      decoration: BoxDecoration(
        color: _green,
        border: Border.all(color: _borderGreen),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Wrap(
            spacing: 12,
            runSpacing: 2,
            alignment: WrapAlignment.spaceBetween,
            crossAxisAlignment: WrapCrossAlignment.center,
            children: [
              Text(title, style: _headingStyle),
              if (trailing != null)
                Text(
                  trailing!,
                  style: const TextStyle(
                    fontSize: 10,
                    color: _mutedText,
                    fontWeight: FontWeight.bold,
                  ),
                ),
            ],
          ),
          if (subtitle != null) ...[
            const SizedBox(height: 2),
            Text(subtitle!, style: const TextStyle(fontSize: 11)),
          ],
        ],
      ),
    );
  }
}

/// Classic Elifoot 98 dropdown filter with vector arrow (never renders missing-glyph boxes).
class ClassicFilter extends StatelessWidget {
  final String filterKey;
  final String label;
  final String value;
  final Map<String, String> options;
  final ValueChanged<String> onChanged;
  final double width;

  const ClassicFilter({
    super.key,
    required this.filterKey,
    required this.label,
    required this.value,
    required this.options,
    required this.onChanged,
    this.width = 210,
  });

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      width: width,
      child: DropdownButtonFormField<String>(
        key: ValueKey(filterKey),
        initialValue: value,
        isExpanded: true,
        dropdownColor: _green,
        icon: const Padding(
          padding: EdgeInsets.only(right: 2),
          child: CustomPaint(
            size: Size(10, 6),
            painter: _ClassicDropdownArrowPainter(),
          ),
        ),
        style: const TextStyle(
          fontSize: 12,
          color: Colors.white,
          fontFamily: 'monospace',
        ),
        decoration: InputDecoration(
          labelText: label,
          labelStyle: const TextStyle(
            color: _gold,
            fontSize: 11,
            fontWeight: FontWeight.bold,
            fontFamily: 'monospace',
          ),
          filled: true,
          fillColor: _green,
          isDense: true,
          contentPadding: const EdgeInsets.symmetric(
            horizontal: 10,
            vertical: 10,
          ),
          enabledBorder: const OutlineInputBorder(
            borderRadius: BorderRadius.zero,
            borderSide: BorderSide(color: _borderGreen, width: 1),
          ),
          focusedBorder: const OutlineInputBorder(
            borderRadius: BorderRadius.zero,
            borderSide: BorderSide(color: _gold, width: 1.5),
          ),
          border: const OutlineInputBorder(
            borderRadius: BorderRadius.zero,
            borderSide: BorderSide(color: _borderGreen, width: 1),
          ),
        ),
        items: options.entries
            .map(
              (e) => DropdownMenuItem(
                value: e.key,
                child: Text(
                  e.value,
                  overflow: TextOverflow.ellipsis,
                  style: const TextStyle(
                    fontSize: 12,
                    color: Colors.white,
                    fontFamily: 'monospace',
                  ),
                ),
              ),
            )
            .toList(),
        onChanged: (v) {
          if (v == null) return;
          onChanged(v);
        },
      ),
    );
  }
}

enum ClassicButtonIcon { none, refresh, close }

/// Classic Elifoot 98 high-contrast button with vector icon support and full keyboard accessibility.
class ClassicActionButton extends StatelessWidget {
  final String label;
  final VoidCallback onPressed;
  final ClassicButtonIcon iconType;
  final bool autofocus;
  final Key? buttonKey;

  const ClassicActionButton({
    super.key,
    required this.label,
    required this.onPressed,
    this.iconType = ClassicButtonIcon.none,
    this.autofocus = false,
    this.buttonKey,
  });

  @override
  Widget build(BuildContext context) {
    return TextButton(
      key: buttonKey,
      autofocus: autofocus,
      onPressed: onPressed,
      style: TextButton.styleFrom(
        foregroundColor: _gold,
        backgroundColor: const Color(0xFF145224),
        minimumSize: const Size(88, 38),
        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 10),
        shape: const RoundedRectangleBorder(
          borderRadius: BorderRadius.zero,
          side: BorderSide(color: _gold, width: 1.2),
        ),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          if (iconType == ClassicButtonIcon.refresh) ...[
            const CustomPaint(
              size: Size(12, 12),
              painter: _ClassicRefreshIconPainter(),
            ),
            const SizedBox(width: 6),
          ] else if (iconType == ClassicButtonIcon.close) ...[
            const CustomPaint(
              size: Size(10, 10),
              painter: _ClassicCloseIconPainter(),
            ),
            const SizedBox(width: 6),
          ],
          Flexible(
            child: Text(
              label,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: const TextStyle(
                color: _gold,
                fontSize: 12,
                fontWeight: FontWeight.bold,
                fontFamily: 'monospace',
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class PlayerStatItem {
  final String label;
  final String value;
  final bool highlight;

  const PlayerStatItem(this.label, this.value, {this.highlight = false});
}

/// Classic compact statistic strip for player career KPIs.
class PlayerStatisticRow extends StatelessWidget {
  final List<PlayerStatItem> items;

  const PlayerStatisticRow({super.key, required this.items});

  @override
  Widget build(BuildContext context) {
    return Wrap(
      spacing: 8,
      runSpacing: 6,
      children: items
          .map(
            (item) => Container(
              padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
              decoration: BoxDecoration(
                color: _green,
                border: Border.all(
                  color: item.highlight ? _gold : _borderGreen,
                  width: item.highlight ? 1.2 : 1,
                ),
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(
                    item.label,
                    style: const TextStyle(
                      fontSize: 9,
                      color: _mutedText,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                  Text(
                    item.value,
                    style: TextStyle(
                      fontSize: 12,
                      color: item.highlight ? _gold : Colors.white,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                ],
              ),
            ),
          )
          .toList(),
    );
  }
}

/// Classic responsive modal dialog with prominent `FECHAR` action and keyboard support.
class ClassicDialog extends StatelessWidget {
  final String title;
  final Widget child;
  final Key? dialogKey;
  final Key? scrollKey;

  const ClassicDialog({
    super.key,
    required this.title,
    required this.child,
    this.dialogKey,
    this.scrollKey,
  });

  @override
  Widget build(BuildContext context) {
    final screen = MediaQuery.sizeOf(context);
    final isMobile = screen.width < 600;
    final inset = isMobile
        ? const EdgeInsets.symmetric(horizontal: 8, vertical: 10)
        : const EdgeInsets.symmetric(horizontal: 24, vertical: 20);
    final targetWidth = isMobile
        ? (screen.width - 16).clamp(280.0, 600.0)
        : math.min(920.0, screen.width - 48);

    return CallbackShortcuts(
      bindings: <ShortcutActivator, VoidCallback>{
        const SingleActivator(LogicalKeyboardKey.escape): () =>
            Navigator.of(context).maybePop(),
      },
      child: AlertDialog(
        key: dialogKey,
        backgroundColor: _panelGreen,
        insetPadding: inset,
        titlePadding: EdgeInsets.zero,
        contentPadding: EdgeInsets.all(isMobile ? 10 : 14),
        actionsPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
        shape: const RoundedRectangleBorder(
          borderRadius: BorderRadius.zero,
          side: BorderSide(color: _gold, width: 1.5),
        ),
        title: Container(
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
          decoration: const BoxDecoration(
            color: _green,
            border: Border(bottom: BorderSide(color: _gold, width: 1)),
          ),
          child: Row(
            children: [
              Expanded(
                child: Text(
                  title,
                  style: const TextStyle(
                    color: _gold,
                    fontSize: 14,
                    fontWeight: FontWeight.bold,
                    fontFamily: 'monospace',
                  ),
                ),
              ),
            ],
          ),
        ),
        content: SizedBox(
          width: targetWidth,
          child: SingleChildScrollView(
            key: scrollKey,
            child: child,
          ),
        ),
        actions: [
          ClassicActionButton(
            label: 'FECHAR',
            iconType: ClassicButtonIcon.close,
            autofocus: true,
            onPressed: () => Navigator.pop(context),
          ),
        ],
      ),
    );
  }
}

class ClassicPinnedRow {
  final String? rank;
  final String title;
  final String? subtitle;
  final VoidCallback? onTap;
  final bool highlight;
  final bool isEven;

  const ClassicPinnedRow({
    this.rank,
    required this.title,
    this.subtitle,
    this.onTap,
    this.highlight = false,
    this.isEven = true,
  });
}

/// Classic Elifoot 98 table component:
/// - On desktop/tablet: expands cleanly across the available container width with balanced
///   column spacing and right-aligned numeric columns.
/// - On mobile: supports smooth horizontal scrolling while pinning the `# / JOGADOR`
///   identification column on the left whenever scrolled horizontally so player identity
///   is never lost.
class ClassicTable extends StatefulWidget {
  final String tableKey;
  final List<String> columns;
  final List<DataRow> rows;
  final Set<int> numericColumns;
  final List<ClassicPinnedRow>? pinnedRows;
  final String? pinnedHeader;

  const ClassicTable({
    super.key,
    required this.tableKey,
    required this.columns,
    required this.rows,
    this.numericColumns = const {},
    this.pinnedRows,
    this.pinnedHeader,
  });

  @override
  State<ClassicTable> createState() => _ClassicTableState();
}

class _ClassicTableState extends State<ClassicTable> {
  final _horizontal = ScrollController();
  double _scrollOffset = 0;

  @override
  void initState() {
    super.initState();
    _horizontal.addListener(_onScroll);
  }

  @override
  void didUpdateWidget(ClassicTable oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (oldWidget.columns.length != widget.columns.length ||
        !_sameColumns(oldWidget.columns, widget.columns)) {
      _scrollOffset = 0;
      if (_horizontal.hasClients) {
        _horizontal.jumpTo(0);
      }
    }
  }

  bool _sameColumns(List<String> a, List<String> b) {
    if (a.length != b.length) return false;
    for (var i = 0; i < a.length; i++) {
      if (a[i] != b[i]) return false;
    }
    return true;
  }

  void _onScroll() {
    final offset = _horizontal.hasClients ? _horizontal.offset : 0.0;
    final wasPinned = _scrollOffset > 4.0;
    final isPinned = offset > 4.0;
    if (wasPinned != isPinned || (offset - _scrollOffset).abs() > 8.0) {
      if (mounted) {
        setState(() {
          _scrollOffset = offset;
        });
      }
    }
  }

  @override
  void dispose() {
    _horizontal.removeListener(_onScroll);
    _horizontal.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return LayoutBuilder(
      builder: (context, constraints) {
        final availableWidth = constraints.maxWidth.isFinite
            ? constraints.maxWidth
            : MediaQuery.sizeOf(context).width;
        final isMobile = availableWidth < 640;
        final isUltraNarrow = availableWidth < 350;
        final hasPinned =
            widget.pinnedRows != null &&
            widget.pinnedRows!.length == widget.rows.length;
        final showPinnedOverlay = hasPinned && _scrollOffset > 4.0;

        final pinnedWidth = isUltraNarrow
            ? 124.0
            : availableWidth < 400
            ? 142.0
            : isMobile
            ? 156.0
            : 190.0;

        const headingHeight = 38.0;
        const rowHeight = 38.0;

        final columnSpacing = isMobile
            ? (isUltraNarrow ? 10.0 : 12.0)
            : _desktopColumnSpacing(availableWidth, widget.columns.length);
        final horizontalMargin = isMobile ? 6.0 : 12.0;

        // Calculate balanced right-side column minimum widths on mobile so that when
        // scrolled horizontally to the end, the statistics columns fill the exact space
        // to the right of the pinned player column (`availableWidth - pinnedWidth`),
        // preventing any underlying text column from being partially exposed or cut off.
        final rightStatsCount = (isMobile && hasPinned)
            ? math.max(1, widget.columns.length - 3)
            : 0;
        final targetRightZone = (isMobile && hasPinned)
            ? math.max(140.0, availableWidth - pinnedWidth - 2)
            : 0.0;
        final statColMinWidth = (isMobile && hasPinned)
            ? ((targetRightZone -
                          horizontalMargin -
                          (rightStatsCount * columnSpacing)) /
                      rightStatsCount)
                  .clamp(34.0, 96.0)
            : 0.0;

        final tableWidget = DataTable(
          key: ValueKey(widget.tableKey),
          headingRowColor: WidgetStateProperty.all(_green),
          headingRowHeight: headingHeight,
          dataRowMinHeight: rowHeight,
          dataRowMaxHeight: rowHeight,
          columnSpacing: columnSpacing,
          horizontalMargin: horizontalMargin,
          border: TableBorder(
            horizontalInside: BorderSide(
              color: _borderGreen.withValues(alpha: 0.45),
              width: 0.8,
            ),
          ),
          dataTextStyle: TextStyle(
            fontSize: isUltraNarrow ? 11 : 12,
            color: Colors.white,
            fontFamily: 'monospace',
          ),
          headingTextStyle: TextStyle(
            fontSize: isUltraNarrow ? 10 : 11,
            fontWeight: FontWeight.bold,
            color: _gold,
            fontFamily: 'monospace',
          ),
          columns: widget.columns.asMap().entries.map((entry) {
            final idx = entry.key;
            final label = entry.value;
            final isNumeric = widget.numericColumns.contains(idx);
            if (isMobile && hasPinned && idx >= 3) {
              return DataColumn(
                numeric: isNumeric,
                label: ConstrainedBox(
                  constraints: BoxConstraints(minWidth: statColMinWidth),
                  child: Text(
                    label,
                    textAlign: isNumeric ? TextAlign.right : TextAlign.left,
                  ),
                ),
              );
            }
            return DataColumn(numeric: isNumeric, label: Text(label));
          }).toList(),
          rows: widget.rows,
        );

        return Container(
          decoration: BoxDecoration(
            color: _rowEvenGreen,
            border: Border.all(color: _borderGreen, width: 1),
          ),
          child: Stack(
            children: [
              Scrollbar(
                controller: _horizontal,
                thumbVisibility: isMobile,
                child: SingleChildScrollView(
                  key: ValueKey('${widget.tableKey}-horizontal'),
                  controller: _horizontal,
                  scrollDirection: Axis.horizontal,
                  padding: const EdgeInsets.only(bottom: 10),
                  child: ConstrainedBox(
                    constraints: BoxConstraints(
                      minWidth: math.max(0.0, availableWidth - 2),
                    ),
                    child: tableWidget,
                  ),
                ),
              ),
              if (showPinnedOverlay)
                Positioned(
                  left: 0,
                  top: 0,
                  width: pinnedWidth,
                  child: _PinnedPlayerColumn(
                    header: widget.pinnedHeader ?? 'JOGADOR',
                    rows: widget.pinnedRows!,
                    headingHeight: headingHeight,
                    rowHeight: rowHeight,
                    compact: isUltraNarrow,
                  ),
                ),
            ],
          ),
        );
      },
    );
  }

  double _desktopColumnSpacing(double width, int columnCount) {
    if (columnCount <= 2) return math.min(64.0, math.max(24.0, width * 0.12));
    if (columnCount <= 5) return math.min(42.0, math.max(20.0, width * 0.04));
    if (columnCount <= 7) return math.min(32.0, math.max(16.0, width * 0.025));
    return 16.0;
  }
}

class _PinnedPlayerColumn extends StatelessWidget {
  final String header;
  final List<ClassicPinnedRow> rows;
  final double headingHeight;
  final double rowHeight;
  final bool compact;

  const _PinnedPlayerColumn({
    required this.header,
    required this.rows,
    required this.headingHeight,
    required this.rowHeight,
    required this.compact,
  });

  @override
  Widget build(BuildContext context) {
    return Container(
      decoration: const BoxDecoration(
        color: _rowEvenGreen,
        border: Border(right: BorderSide(color: _gold, width: 1.5)),
        boxShadow: [
          BoxShadow(
            color: Colors.black54,
            blurRadius: 4,
            offset: Offset(2, 0),
          ),
        ],
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        mainAxisSize: MainAxisSize.min,
        children: [
          Container(
            height: headingHeight,
            padding: const EdgeInsets.symmetric(horizontal: 6),
            alignment: Alignment.centerLeft,
            color: _green,
            child: Text(
              header,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: TextStyle(
                fontSize: compact ? 10 : 11,
                fontWeight: FontWeight.bold,
                color: _gold,
                fontFamily: 'monospace',
              ),
            ),
          ),
          for (final row in rows)
            GestureDetector(
              behavior: HitTestBehavior.opaque,
              onTap: row.onTap,
              child: Container(
                height: rowHeight,
                padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                decoration: BoxDecoration(
                  color: row.highlight
                      ? _rowTopGreen
                      : (row.isEven ? _rowEvenGreen : _rowOddGreen),
                  border: Border(
                    top: BorderSide(
                      color: _borderGreen.withValues(alpha: 0.45),
                      width: 0.8,
                    ),
                  ),
                ),
                child: Row(
                  children: [
                    if (row.rank != null) ...[
                      SizedBox(
                        width: compact ? 16 : 20,
                        child: Text(
                          row.rank!,
                          style: TextStyle(
                            fontSize: compact ? 10 : 11,
                            color: row.highlight ? _gold : Colors.white,
                            fontWeight: row.highlight
                                ? FontWeight.bold
                                : FontWeight.normal,
                            fontFamily: 'monospace',
                          ),
                        ),
                      ),
                      const SizedBox(width: 2),
                    ],
                    Expanded(
                      child: Column(
                        mainAxisAlignment: MainAxisAlignment.center,
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            row.title,
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                            style: TextStyle(
                              fontSize: compact ? 10 : 11,
                              color: _gold,
                              fontWeight: FontWeight.bold,
                              decoration: row.onTap != null
                                  ? TextDecoration.underline
                                  : TextDecoration.none,
                              fontFamily: 'monospace',
                            ),
                          ),
                          if (row.subtitle != null &&
                              row.subtitle!.isNotEmpty) ...[
                            Text(
                              row.subtitle!,
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                              style: TextStyle(
                                fontSize: compact ? 8.5 : 9.5,
                                color: _mutedText,
                                fontFamily: 'monospace',
                              ),
                            ),
                          ],
                        ],
                      ),
                    ),
                  ],
                ),
              ),
            ),
        ],
      ),
    );
  }
}

class _ClassicDropdownArrowPainter extends CustomPainter {
  const _ClassicDropdownArrowPainter();

  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint()
      ..color = _gold
      ..style = PaintingStyle.fill;
    final path = Path()
      ..moveTo(0, 0)
      ..lineTo(size.width, 0)
      ..lineTo(size.width / 2, size.height)
      ..close();
    canvas.drawPath(path, paint);
  }

  @override
  bool shouldRepaint(covariant CustomPainter oldDelegate) => false;
}

class _ClassicRefreshIconPainter extends CustomPainter {
  const _ClassicRefreshIconPainter();

  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint()
      ..color = _gold
      ..style = PaintingStyle.stroke
      ..strokeWidth = 1.6;
    final rect = Rect.fromLTWH(1, 1, size.width - 2, size.height - 2);
    canvas.drawArc(rect, 0.4, math.pi * 1.55, false, paint);

    final fill = Paint()
      ..color = _gold
      ..style = PaintingStyle.fill;
    final arrow = Path()
      ..moveTo(size.width - 1, 1)
      ..lineTo(size.width - 5, 1)
      ..lineTo(size.width - 1, 5)
      ..close();
    canvas.drawPath(arrow, fill);
  }

  @override
  bool shouldRepaint(covariant CustomPainter oldDelegate) => false;
}

class _ClassicCloseIconPainter extends CustomPainter {
  const _ClassicCloseIconPainter();

  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint()
      ..color = _gold
      ..style = PaintingStyle.stroke
      ..strokeWidth = 1.8;
    canvas.drawLine(Offset.zero, Offset(size.width, size.height), paint);
    canvas.drawLine(Offset(size.width, 0), Offset(0, size.height), paint);
  }

  @override
  bool shouldRepaint(covariant CustomPainter oldDelegate) => false;
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
      const SizedBox(height: 6),
      ClassicActionButton(
        label: 'TENTAR NOVAMENTE',
        iconType: ClassicButtonIcon.refresh,
        onPressed: retry,
      ),
    ],
  );
}
