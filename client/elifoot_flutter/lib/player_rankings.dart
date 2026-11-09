import 'club_theme.dart';
import 'classic_layout.dart';
import 'visual_components.dart';
import 'dart:math' as math;

import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

import 'api_client.dart';
import 'history_views.dart';

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
String _rankingPosition(dynamic value) => value is num
    ? const ['G', 'D', 'M', 'A'][value.toInt().clamp(0, 3)]
    : '${value ?? '—'}';

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
  final String? requestedCategory;

  const PlayerRankingsScreen({
    super.key,
    required this.apiClient,
    required this.worldId,
    required this.currentSeason,
    this.revision = 0,
    this.requestedCategory,
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
    _category = widget.requestedCategory ?? _category;
    _season = widget.currentSeason;
    _future = _load();
  }

  @override
  void didUpdateWidget(PlayerRankingsScreen oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (widget.requestedCategory != null &&
        oldWidget.requestedCategory != widget.requestedCategory) {
      _category = widget.requestedCategory!;
      _future = _load();
      return;
    }
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
  Widget build(BuildContext context) => LayoutBuilder(
    builder: (context, viewport) => SingleChildScrollView(
      key: const ValueKey('player-rankings-scroll'),
      padding: const EdgeInsets.all(12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          _buildHeading(),
          const SizedBox(height: 14),
          LayoutBuilder(
            builder: (context, available) {
              final ranking = _buildRankingPanel();
              if (_category == 'assists') return ranking;
              final awards = SeasonTopScorersPanel(
                apiClient: widget.apiClient,
                worldId: widget.worldId,
                season: _period == 'season' ? _season : null,
                revision: widget.revision + _request,
                currentSeason: widget.currentSeason,
                framed: true,
              );
              return available.maxWidth >= 1000
                  ? Row(
                      key: const Key('player-rankings-desktop-columns'),
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Expanded(flex: 4, child: ranking),
                        const SizedBox(width: 14),
                        Expanded(flex: 3, child: awards),
                      ],
                    )
                  : Column(
                      key: const Key('player-rankings-stacked-panels'),
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [ranking, const SizedBox(height: 14), awards],
                    );
            },
          ),
          const SizedBox(height: 14),
          Container(
            key: const Key('player-rankings-information'),
            padding: const EdgeInsets.all(14),
            decoration: BoxDecoration(
              color: ClassicTheme.of(context).primary,
              border: Border.all(color: ClassicTheme.of(context).border),
              borderRadius: BorderRadius.circular(8),
            ),
            child: Row(
              children: [
                Icon(
                  Icons.info_outline,
                  size: 18,
                  color: readableInk(
                    ClassicTheme.of(context).primary,
                    Colors.white,
                  ),
                ),
                const SizedBox(width: 10),
                Expanded(
                  child: Text(
                    'Os rankings serão preenchidos conforme as partidas oficiais forem concluídas. Os dados desta tela vêm do jogo.',
                    style: TextStyle(
                      fontSize: 15,
                      color: readableInk(
                        ClassicTheme.of(context).primary,
                        Colors.white,
                      ),
                    ),
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    ),
  );

  Widget _buildHeading() {
    final t = ClassicTheme.of(context),
        ink = readableInk(t.primary, Colors.white);
    return Container(
      key: const Key('player-rankings-heading'),
      decoration: BoxDecoration(
        color: t.primary,
        border: Border.all(color: t.border),
        borderRadius: BorderRadius.circular(8),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 14),
            child: Row(
              children: [
                Icon(Icons.person, color: t.selectedBackground, size: 28),
                const SizedBox(width: 12),
                Expanded(
                  child: Text(
                    'RANKINGS DE JOGADORES',
                    style: TextStyle(
                      color: ink,
                      fontSize: 26,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                ),
              ],
            ),
          ),
          Divider(height: 1, color: t.border),
          Padding(
            padding: const EdgeInsets.symmetric(horizontal: 16, vertical: 10),
            child: Text(
              'Somente partidas oficiais concluídas. Toque no jogador para ver o histórico.',
              style: TextStyle(color: ink, fontSize: 15),
            ),
          ),
          Divider(height: 1, color: t.border),
          _buildFilterBar(),
        ],
      ),
    );
  }

  String get _categoryTitle => switch (_category) {
    'assists' => 'ASSISTÊNCIAS',
    'goalkeepers' => 'GOLEIROS',
    'defenders' => 'DEFENSORES',
    _ => 'ARTILHEIROS',
  };

  Widget _buildRankingPanel() => FutureBuilder<Map<String, dynamic>>(
    future: _future,
    builder: (context, snapshot) {
      final data = snapshot.data;
      final periodLabel = _period == 'career'
          ? 'CARREIRA INTEIRA'
          : 'TEMPORADA ${data?['season'] ?? data?['currentSeason'] ?? _season}';
      return ClassicPaperPanel(
        key: const Key('player-ranking-panel'),
        title: '$periodLabel — $_categoryTitle',
        icon: Icons.workspace_premium,
        iconColor: ClassicTheme.of(context).selectedBackground,
        padding: EdgeInsets.zero,
        child: ClassicPaper(
          builder: (context) {
            if (snapshot.connectionState != ConnectionState.done) {
              return const Padding(
                padding: EdgeInsets.all(20),
                child: _Loading('CARREGANDO RANKING...'),
              );
            }
            if (snapshot.hasError) {
              return Padding(
                padding: const EdgeInsets.all(20),
                child: _LoadError('ranking', _reload),
              );
            }
            final payload = snapshot.data!;
            final minimum = math.max(20, _integer(payload['minimumMatches']));
            final all = _records(payload['rows']);
            final needsMinimum = !['scorers', 'assists'].contains(_category);
            final rows = all
                .where(
                  (p) =>
                      !needsMinimum || _integer(p['matchesPlayed']) >= minimum,
                )
                .toList();
            final seen = <String>{};
            final ineligible = needsMinimum
                ? [
                        ..._records(payload['notEligible']),
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
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                _CoverageWarnings(payload['coverageWarnings']),
                if (needsMinimum)
                  Padding(
                    padding: const EdgeInsets.all(10),
                    child: Text(
                      'Mínimo de $minimum jogos no recorte consultado.',
                      style: const TextStyle(fontSize: 15),
                    ),
                  ),
                if (rows.isEmpty)
                  const _RankingEmptyState(
                    icon: Icons.sports_soccer,
                    title:
                        'Nenhum jogador elegível com estatísticas oficiais neste recorte.',
                    description:
                        'Ainda não há estatísticas oficiais de partidas concluídas para os filtros selecionados.',
                  )
                else
                  _rankingTable(rows, context: context),
                if (ineligible.isNotEmpty) ...[
                  const SizedBox(height: 12),
                  ClassicTableHeader(
                    title: 'AINDA NÃO ELEGÍVEIS',
                    subtitle:
                        'Faltam jogos para alcançar $minimum neste recorte.',
                  ),
                  _rankingTable(ineligible, minimum: minimum, context: context),
                ],
              ],
            );
          },
        ),
      );
    },
  );

  Widget _buildFilterBar() => LayoutBuilder(
    builder: (context, constraints) {
      final width = constraints.maxWidth;
      Widget category() => _filter(
        'ranking-category',
        'CATEGORIA',
        _category,
        const {
          'scorers': 'Artilheiros',
          'assists': 'Assistências',
          'goalkeepers': 'Goleiros',
          'defenders': 'Defensores',
        },
        (value) {
          setState(() {
            _category = value;
            _future = _load();
          });
        },
      );
      Widget scope() => _filter(
        'ranking-scope',
        'COMPETIÇÃO',
        _scope,
        const {
          'general': 'Geral',
          'division1': '1ª divisão',
          'division2': '2ª divisão',
          'division3': '3ª divisão',
          'division4': '4ª divisão',
          'cup': 'Taça',
        },
        (value) {
          setState(() {
            _scope = value;
            _future = _load();
          });
        },
      );
      Widget period() => _filter(
        'ranking-period',
        'PERÍODO',
        _period,
        const {
          'career': 'Carreira inteira',
          'current': 'Temporada atual',
          'season': 'Temporada selecionada',
        },
        (value) {
          setState(() {
            _period = value;
            _future = _load();
          });
        },
      );
      Widget season() => _filter(
        'ranking-season',
        'TEMPORADA',
        '$_season',
        {
          for (final value in {..._seasons, widget.currentSeason, _season})
            '$value': 'Temporada $value',
        },
        (value) {
          setState(() {
            _season = int.parse(value);
            _future = _load();
          });
        },
      );
      final t = ClassicTheme.of(context);
      final refresh = ElevatedButton.icon(
        key: const Key('ranking-refresh'),
        onPressed: _reload,
        style: ElevatedButton.styleFrom(
          backgroundColor: t.selectedBackground,
          foregroundColor: t.selectedText,
          minimumSize: const Size(44, 48),
          elevation: 0,
        ),
        icon: const Icon(Icons.refresh, size: 20),
        label: const Text('ATUALIZAR'),
      );
      return Padding(
        padding: const EdgeInsets.all(16),
        child: width >= 900
            ? Row(
                crossAxisAlignment: CrossAxisAlignment.end,
                children: [
                  Expanded(child: category()),
                  const SizedBox(width: 12),
                  Expanded(child: scope()),
                  const SizedBox(width: 12),
                  Expanded(child: period()),
                  if (_period == 'season') ...[
                    const SizedBox(width: 12),
                    Expanded(child: season()),
                  ],
                  const SizedBox(width: 12),
                  SizedBox(width: 154, child: refresh),
                ],
              )
            : Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  category(),
                  const SizedBox(height: 10),
                  Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Expanded(child: scope()),
                      const SizedBox(width: 10),
                      Expanded(child: period()),
                    ],
                  ),
                  if (_period == 'season') ...[
                    const SizedBox(height: 10),
                    season(),
                  ],
                  const SizedBox(height: 10),
                  refresh,
                ],
              ),
      );
    },
  );

  Widget _filter(
    String key,
    String label,
    String value,
    Map<String, String> options,
    ValueChanged<String> onChanged,
  ) {
    final t = ClassicTheme.of(context), paper = t.paper;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(
          label,
          style: TextStyle(
            fontSize: 13,
            fontWeight: FontWeight.bold,
            color: t.selectedBackground,
          ),
        ),
        const SizedBox(height: 5),
        Theme(
          data: paper.material(),
          child: DropdownButtonFormField<String>(
            key: ValueKey(key),
            initialValue: value,
            isExpanded: true,
            isDense: false,
            itemHeight: null,
            dropdownColor: paper.surface,
            iconEnabledColor: paper.textPrimary,
            style: TextStyle(
              fontFamily: 'ClassicCondensed',
              fontSize: 17,
              fontWeight: FontWeight.w600,
              color: paper.textPrimary,
              height: 1.15,
            ),
            decoration: InputDecoration(
              filled: true,
              fillColor: paper.surface,
              contentPadding: const EdgeInsets.symmetric(
                horizontal: 12,
                vertical: 0,
              ),
              border: OutlineInputBorder(
                borderRadius: BorderRadius.circular(6),
                borderSide: BorderSide(color: paper.border),
              ),
              enabledBorder: OutlineInputBorder(
                borderRadius: BorderRadius.circular(6),
                borderSide: BorderSide(color: paper.border),
              ),
            ),
            items: [
              for (final entry in options.entries)
                DropdownMenuItem(
                  value: entry.key,
                  child: Padding(
                    padding: const EdgeInsets.symmetric(vertical: 3),
                    child: Text(entry.value, softWrap: true),
                  ),
                ),
            ],
            onChanged: (next) {
              if (next != null) onChanged(next);
            },
          ),
        ),
      ],
    );
  }

  Widget _rankingTable(
    List<Map<String, dynamic>> players, {
    int? minimum,
    required BuildContext context,
  }) {
    if (MediaQuery.sizeOf(context).width < 700) {
      return _compactRankingTable(context, players, minimum: minimum);
    }
    final columns = <String>[
      '#',
      'JOGADOR',
      'POSIÇÃO',
      'CLUBE ATUAL',
      'JOGOS',
      if (_category == 'scorers') 'GOLS',
      if (_category == 'assists') 'ASSISTÊNCIAS',
      if (_category == 'goalkeepers') 'GOLS SOFRIDOS',
      if (_category == 'defenders') ...['AMARELOS', 'VERMELHOS'],
      _category == 'defenders' ? 'ÍNDICE' : 'MÉDIA',
      if (minimum != null) 'JOGOS FALTANTES',
    ];
    final numericColumns = <int>{0, for (var i = 4; i < columns.length; i++) i};

    return ClassicTable(
      tableKey: minimum == null ? 'ranking-table' : 'not-eligible-table',
      rankingStyle: true,
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
            ? ClassicTheme.of(context).surfaceAlternate
            : (index.isEven
                  ? ClassicTheme.of(context).tableRow
                  : ClassicTheme.of(context).tableRowAlternate);
        return DataRow(
          color: WidgetStateProperty.all(rowColor),
          cells: [
            DataCell(
              Text(
                minimum == null ? _value(p['rank']) : '—',
                style: TextStyle(
                  color: isTop3
                      ? ClassicTheme.of(context).accent
                      : ClassicTheme.of(context).textPrimary,
                  fontWeight: isTop3 ? FontWeight.bold : FontWeight.normal,
                ),
              ),
            ),
            _playerCell(
              context,
              widget.apiClient,
              widget.worldId,
              p,
              maxWidth: 190,
            ),
            DataCell(Text(_rankingPosition(p['position']))),
            DataCell(
              ConstrainedBox(
                constraints: const BoxConstraints(maxWidth: 140),
                child: Text(_club(p), softWrap: true),
              ),
            ),
            DataCell(Text(_value(p['matchesPlayed']))),
            if (_category == 'scorers')
              DataCell(Text(_value(p['goalsScored']))),
            if (_category == 'assists') DataCell(Text(_value(p['assists']))),
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

  Widget _compactRankingTable(
    BuildContext context,
    List<Map<String, dynamic>> players, {
    int? minimum,
  }) {
    final t = ClassicTheme.of(context);
    final metric = switch (_category) {
      'assists' => 'ASS.',
      'goalkeepers' => 'G.C.',
      'defenders' => 'ÍNDICE',
      _ => 'GOLS',
    };
    Widget number(String value, double width, {bool heading = false}) =>
        SizedBox(
          width: width,
          child: Text(
            value,
            textAlign: TextAlign.center,
            style: TextStyle(
              fontSize: heading ? 14 : 16,
              fontWeight: FontWeight.bold,
            ),
          ),
        );
    return Column(
      key: ValueKey(minimum == null ? 'ranking-table' : 'not-eligible-table'),
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Container(
          color: t.tableHeader,
          padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 10),
          child: DefaultTextStyle.merge(
            style: TextStyle(
              color: t.classicPresentation
                  ? readableInk(t.tableHeader, t.textPrimary)
                  : null,
            ),
            child: Row(
              children: [
                number('#', 25, heading: true),
                const Expanded(
                  child: Text(
                    'JOGADOR / CLUBE',
                    style: TextStyle(fontSize: 14, fontWeight: FontWeight.bold),
                  ),
                ),
                number('J', 30, heading: true),
                number('POS', 32, heading: true),
                number(metric, 55, heading: true),
                const SizedBox(width: 18),
              ],
            ),
          ),
        ),
        for (final (index, p) in players.indexed)
          Material(
            color: index.isEven ? t.tableRow : t.tableRowAlternate,
            child: InkWell(
              key: ValueKey('ranking-player-${p['playerId']}'),
              onTap: p['playerId'] == null
                  ? null
                  : () => showPlayerHistoryDialog(
                      context: context,
                      apiClient: widget.apiClient,
                      worldId: widget.worldId,
                      playerId: p['playerId'].toString(),
                    ),
              child: Padding(
                padding: const EdgeInsets.symmetric(
                  horizontal: 8,
                  vertical: 12,
                ),
                child: Row(
                  children: [
                    number(minimum == null ? _value(p['rank']) : '—', 25),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            _playerName(p),
                            style: TextStyle(
                              color: t.accent,
                              fontWeight: FontWeight.bold,
                              fontSize: 17,
                            ),
                          ),
                          Text(
                            _club(p),
                            style: TextStyle(
                              color: t.textSecondary,
                              fontSize: 14,
                            ),
                          ),
                          if (minimum != null)
                            Text(
                              'Faltam ${math.max(0, minimum - _integer(p['matchesPlayed']))} jogos',
                              style: const TextStyle(fontSize: 14),
                            ),
                        ],
                      ),
                    ),
                    number(_value(p['matchesPlayed']), 30),
                    number(_rankingPosition(p['position']), 32),
                    number(
                      _category == 'defenders'
                          ? _average(p['average'])
                          : _value(
                              p[_category == 'goalkeepers'
                                  ? 'goalsConceded'
                                  : _category == 'assists'
                                  ? 'assists'
                                  : 'goalsScored'],
                            ),
                      55,
                    ),
                    const Icon(Icons.chevron_right, size: 18),
                  ],
                ),
              ),
            ),
          ),
      ],
    );
  }
}

DataCell _playerCell(
  BuildContext context,
  ElifootApiClient api,
  String worldId,
  Map<String, dynamic> player, {
  double? maxWidth,
}) {
  final name = Text(
    _playerName(player),
    softWrap: true,
    style: TextStyle(
      color: ClassicTheme.of(context).accent,
      decoration: TextDecoration.underline,
      fontWeight: FontWeight.bold,
    ),
  );
  return DataCell(
    maxWidth == null
        ? name
        : ConstrainedBox(
            constraints: BoxConstraints(maxWidth: maxWidth),
            child: name,
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
}

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
              aggregated[key] = _integer(aggregated[key]) + _integer(row[key]);
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
                  color: ClassicTheme.of(context).surface,
                  border: Border.all(
                    color: ClassicTheme.of(context).selectedBackground,
                    width: 1,
                  ),
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'IDENTIFICAÇÃO DO ATLETA',
                      style: TextStyle(
                        color: ClassicTheme.of(context).textSecondary,
                        fontSize: 10,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                    const SizedBox(height: 2),
                    Text(
                      _playerName(player),
                      style: TextStyle(
                        color: ClassicTheme.of(context).accent,
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
                  color: ClassicTheme.of(context).tableRow,
                  border: Border.all(color: ClassicTheme.of(context).border),
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
                        color: ClassicTheme.of(context).surface,
                        border: Border.all(
                          color: ClassicTheme.of(context).selectedBackground,
                        ),
                      ),
                      child: Text(
                        formatTopScorerTitles(player['seasonTopScorerTitles']),
                        key: const ValueKey('player-scorer-titles'),
                        style: ClassicTheme.of(context).headingStyle,
                      ),
                    ),
                    const SizedBox(height: 8),
                    PlayerStatisticRow(
                      items: [
                        PlayerStatItem(
                          'JOGOS',
                          _value(career['matchesPlayed']),
                        ),
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
              PlayerStrengthHistoryPanel(data: data),
              PlayerCareerHistoryPanels(data: data),
              if (statistics.any((s) => s['hasIncompleteAppearances'] == true))
                Padding(
                  padding: EdgeInsets.only(top: 4),
                  child: Text(
                    'COBERTURA: Há aparições legadas incompletas; jogos e minutos podem estar parciais.',
                    style: TextStyle(
                      color: ClassicTheme.of(context).accent,
                      fontSize: 12,
                    ),
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
                        idx.isEven
                            ? ClassicTheme.of(context).tableRow
                            : ClassicTheme.of(context).tableRowAlternate,
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
                        idx.isEven
                            ? ClassicTheme.of(context).tableRow
                            : ClassicTheme.of(context).tableRowAlternate,
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
                        idx.isEven
                            ? ClassicTheme.of(context).tableRow
                            : ClassicTheme.of(context).tableRowAlternate,
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
  final bool framed;
  const SeasonTopScorersPanel({
    super.key,
    required this.apiClient,
    required this.worldId,
    this.season,
    this.currentSeason,
    this.revision = 0,
    this.framed = false,
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
  Widget build(BuildContext context) => widget.framed
      ? ClassicPaper(builder: (context) => _buildContent(context))
      : _buildContent(context);

  Widget _frame(BuildContext context, Widget child, {dynamic season}) =>
      widget.framed
      ? ClassicPaperPanel(
          key: const Key('player-season-awards-panel'),
          title: 'ARTILHEIROS DA TEMPORADA${season == null ? '' : ' $season'}',
          icon: Icons.emoji_events,
          iconColor: ClassicTheme.of(context).selectedBackground,
          padding: EdgeInsets.zero,
          child: child,
        )
      : child;

  Widget _buildContent(
    BuildContext context,
  ) => FutureBuilder<Map<String, dynamic>>(
    future: _future,
    builder: (context, snapshot) {
      if (snapshot.connectionState != ConnectionState.done) {
        return _frame(
          context,
          const Padding(
            padding: EdgeInsets.all(20),
            child: _Loading('CARREGANDO PREMIAÇÕES...'),
          ),
        );
      }
      if (snapshot.hasError) {
        return _frame(
          context,
          _LoadError(
            'premiações',
            () => setState(() {
              _future = _load();
            }),
          ),
        );
      }
      final data = snapshot.data!;
      final awards = _records(data['awards']);
      final totals = _records(data['clubTotals']);
      final season = data['season'];
      final isTie = awards.length > 1;
      return _frame(
        context,
        Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          mainAxisSize: MainAxisSize.min,
          children: [
            if (!widget.framed)
              ClassicSectionTitle(
                title:
                    'ARTILHEIROS DA TEMPORADA${season == null ? '' : ' $season'}',
                subtitle: widget.season == null
                    ? 'Última temporada encerrada.'
                    : null,
              ),
            _CoverageWarnings(data['coverageWarnings']),
            if (awards.isEmpty)
              widget.framed
                  ? _RankingEmptyState(
                      icon: Icons.emoji_events_outlined,
                      title: season == null
                          ? 'Nenhuma temporada encerrada com premiações registradas.'
                          : 'Nenhuma premiação registrada nesta temporada.',
                      description:
                          'As premiações de artilheiros aparecem após o encerramento das temporadas.',
                    )
                  : Padding(
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
                padding: const EdgeInsets.symmetric(
                  horizontal: 10,
                  vertical: 6,
                ),
                decoration: BoxDecoration(
                  color: ClassicTheme.of(context).surface,
                  border: Border.all(
                    color: isTie
                        ? ClassicTheme.of(context).selectedBackground
                        : ClassicTheme.of(context).border,
                  ),
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
                          style: TextStyle(
                            color: ClassicTheme.of(context).accent,
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
                rankingStyle: widget.framed,
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
                      idx.isEven
                          ? ClassicTheme.of(context).surfaceAlternate
                          : ClassicTheme.of(context).tableRowAlternate,
                    ),
                    cells: [
                      _playerCell(context, widget.apiClient, widget.worldId, a),
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
                      idx.isEven
                          ? ClassicTheme.of(context).tableRow
                          : ClassicTheme.of(context).tableRowAlternate,
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
        ),
        season: season,
      );
    },
  );
}

class _RankingEmptyState extends StatelessWidget {
  final IconData icon;
  final String title, description;
  const _RankingEmptyState({
    required this.icon,
    required this.title,
    required this.description,
  });
  @override
  Widget build(BuildContext context) {
    final t = ClassicTheme.of(context);
    return Container(
      constraints: const BoxConstraints(minHeight: 230),
      padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 32),
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Icon(
            icon,
            size: 48,
            color: icon == Icons.sports_soccer
                ? t.textSecondary
                : t.selectedBackground,
          ),
          const SizedBox(height: 16),
          Text(
            title,
            textAlign: TextAlign.center,
            style: TextStyle(
              color: t.textPrimary,
              fontSize: 18,
              fontWeight: FontWeight.bold,
            ),
          ),
          const SizedBox(height: 10),
          Text(
            description,
            textAlign: TextAlign.center,
            style: TextStyle(color: t.textSecondary, fontSize: 15, height: 1.3),
          ),
        ],
      ),
    );
  }
}

// ============================================================================
// SHARED CLASSIC COMPONENTS (SECTION 17)
// ============================================================================

/// Classic Elifoot 98 section title banner with yellow header typography.
class ClassicSectionTitle extends StatelessWidget {
  final String title;
  final String? subtitle;

  const ClassicSectionTitle({super.key, required this.title, this.subtitle});

  @override
  Widget build(BuildContext context) =>
      ClassicPageHeader(title: title, subtitle: subtitle);
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
    final t = ClassicTheme.of(context);
    return Container(
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
      decoration: BoxDecoration(
        gradient: ClassicTheme.of(context).panelGradient,
        border: Border.all(color: ClassicTheme.of(context).border),
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
              Text(
                title,
                style: t.headingStyle.copyWith(
                  color: t.classicPresentation ? Colors.white : null,
                ),
              ),
              if (trailing != null)
                Text(
                  trailing!,
                  style: TextStyle(
                    fontSize: 10,
                    color: t.classicPresentation
                        ? Colors.white
                        : t.textSecondary,
                    fontWeight: FontWeight.bold,
                  ),
                ),
            ],
          ),
          if (subtitle != null) ...[
            const SizedBox(height: 2),
            Text(
              subtitle!,
              style: TextStyle(
                fontSize: 11,
                color: t.classicPresentation ? Colors.white : null,
              ),
            ),
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
        dropdownColor: ClassicTheme.of(context).surface,
        icon: Padding(
          padding: EdgeInsets.only(right: 2),
          child: CustomPaint(
            size: Size(10, 6),
            painter: _ClassicDropdownArrowPainter(
              ClassicTheme.of(context).accent,
            ),
          ),
        ),
        style: TextStyle(
          fontSize: 12,
          color: ClassicTheme.of(context).textPrimary,
          fontFamily: ClassicTheme.of(context).classicPresentation
              ? 'ClassicCondensed'
              : 'Roboto',
        ),
        decoration: InputDecoration(
          labelText: label,
          labelStyle: TextStyle(
            color: ClassicTheme.of(context).accent,
            fontSize: 11,
            fontWeight: FontWeight.bold,
            fontFamily: ClassicTheme.of(context).classicPresentation
                ? 'ClassicCondensed'
                : 'Roboto',
          ),
          filled: true,
          fillColor: ClassicTheme.of(context).surface,
          isDense: true,
          contentPadding: const EdgeInsets.symmetric(
            horizontal: 10,
            vertical: 10,
          ),
          enabledBorder: OutlineInputBorder(
            borderRadius: BorderRadius.zero,
            borderSide: BorderSide(
              color: ClassicTheme.of(context).border,
              width: 1,
            ),
          ),
          focusedBorder: OutlineInputBorder(
            borderRadius: BorderRadius.zero,
            borderSide: BorderSide(
              color: ClassicTheme.of(context).border,
              width: 1.5,
            ),
          ),
          border: OutlineInputBorder(
            borderRadius: BorderRadius.zero,
            borderSide: BorderSide(
              color: ClassicTheme.of(context).border,
              width: 1,
            ),
          ),
        ),
        items: options.entries
            .map(
              (e) => DropdownMenuItem(
                value: e.key,
                child: Text(
                  e.value,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(
                    fontSize: 12,
                    color: ClassicTheme.of(context).textPrimary,
                    fontFamily: ClassicTheme.of(context).classicPresentation
                        ? 'ClassicCondensed'
                        : 'Roboto',
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
  final VoidCallback? onPressed;
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
    final t = ClassicTheme.of(context);
    final fg = t.classicPresentation ? t.buttonText : t.accent;
    return TextButton(
      key: buttonKey,
      autofocus: autofocus,
      onPressed: onPressed,
      style: TextButton.styleFrom(
        foregroundColor: fg,
        backgroundColor: t.buttonBackground,
        minimumSize: Size(
          72,
          MediaQuery.sizeOf(context).width < 1024 ? 44 : 32,
        ),
        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 7),
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.zero,
          side: BorderSide(color: t.border, width: 1.2),
        ),
      ),
      child: Row(
        mainAxisSize: MainAxisSize.min,
        children: [
          if (iconType == ClassicButtonIcon.refresh) ...[
            CustomPaint(
              size: const Size(12, 12),
              painter: _ClassicRefreshIconPainter(fg),
            ),
            const SizedBox(width: 6),
          ] else if (iconType == ClassicButtonIcon.close) ...[
            CustomPaint(
              size: const Size(10, 10),
              painter: _ClassicCloseIconPainter(fg),
            ),
            const SizedBox(width: 6),
          ],
          Flexible(
            child: Text(
              label,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: TextStyle(
                color: fg,
                fontSize: 12,
                fontWeight: FontWeight.bold,
                fontFamily: t.classicPresentation
                    ? 'ClassicCondensed'
                    : Theme.of(context).textTheme.bodyMedium?.fontFamily,
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
  final bool dense;

  const PlayerStatisticRow({
    super.key,
    required this.items,
    this.dense = false,
  });

  @override
  Widget build(BuildContext context) {
    if (dense) {
      const priority = {
        'TREINADOR',
        'DATA DO JOGO',
        'TETO DE EVOLUÇÃO',
        'PREPARAÇÃO',
      };
      Widget facts(Iterable<PlayerStatItem> values) => Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          for (final item in values)
            Padding(
              padding: const EdgeInsets.symmetric(vertical: 2),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Expanded(
                    flex: 4,
                    child: Text(
                      item.label,
                      style: TextStyle(
                        fontSize: 9,
                        color: ClassicTheme.of(context).textSecondary,
                      ),
                    ),
                  ),
                  const SizedBox(width: 8),
                  Expanded(
                    flex: 7,
                    child: Text(
                      item.value,
                      style: TextStyle(
                        fontSize: 11,
                        fontWeight: FontWeight.w600,
                        color: item.highlight
                            ? ClassicTheme.of(context).accent
                            : ClassicTheme.of(context).textPrimary,
                      ),
                    ),
                  ),
                ],
              ),
            ),
        ],
      );
      return Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          facts(items.where((item) => priority.contains(item.label))),
          ExpansionTile(
            tilePadding: EdgeInsets.zero,
            dense: true,
            title: const Text(
              'MAIS INFORMAÇÕES DO TREINADOR',
              style: TextStyle(fontSize: 10),
            ),
            children: [
              facts(items.where((item) => !priority.contains(item.label))),
            ],
          ),
        ],
      );
    }
    return Wrap(
      spacing: 8,
      runSpacing: 6,
      children: items
          .map(
            (item) => Container(
              padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
              decoration: BoxDecoration(
                color: ClassicTheme.of(context).surface,
                border: Border.all(
                  color: item.highlight
                      ? ClassicTheme.of(context).selectedBackground
                      : ClassicTheme.of(context).border,
                  width: item.highlight ? 1.2 : 1,
                ),
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                mainAxisSize: MainAxisSize.min,
                children: [
                  Text(
                    item.label,
                    style: TextStyle(
                      fontSize: 9,
                      color: ClassicTheme.of(context).textSecondary,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                  Text(
                    item.value,
                    style: TextStyle(
                      fontSize: 12,
                      color: item.highlight
                          ? ClassicTheme.of(context).accent
                          : ClassicTheme.of(context).textPrimary,
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
        backgroundColor: ClassicTheme.of(context).surface,
        insetPadding: inset,
        titlePadding: EdgeInsets.zero,
        contentPadding: EdgeInsets.all(isMobile ? 10 : 14),
        actionsPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.zero,
          side: BorderSide(color: ClassicTheme.of(context).border, width: 1.5),
        ),
        title: Container(
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
          decoration: BoxDecoration(
            color: ClassicTheme.of(context).classicPresentation
                ? ClassicTheme.of(context).primary
                : ClassicTheme.of(context).surface,
            border: Border(
              bottom: BorderSide(
                color: ClassicTheme.of(context).selectedBackground,
                width: 1,
              ),
            ),
          ),
          child: Row(
            children: [
              Expanded(
                child: Text(
                  title,
                  style: TextStyle(
                    color: ClassicTheme.of(context).classicPresentation
                        ? Colors.white
                        : ClassicTheme.of(context).accent,
                    fontSize: ClassicTheme.of(context).classicPresentation
                        ? 17
                        : 14,
                    fontWeight: FontWeight.bold,
                    fontFamily: ClassicTheme.of(context).classicPresentation
                        ? 'ClassicCondensed'
                        : Theme.of(context).textTheme.bodyMedium?.fontFamily,
                  ),
                ),
              ),
            ],
          ),
        ),
        content: SizedBox(
          width: targetWidth,
          child: SingleChildScrollView(key: scrollKey, child: child),
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
  final bool rankingStyle;

  const ClassicTable({
    super.key,
    required this.tableKey,
    required this.columns,
    required this.rows,
    this.numericColumns = const {},
    this.pinnedRows,
    this.pinnedHeader,
    this.rankingStyle = false,
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
        final showPinnedOverlay =
            hasPinned && _scrollOffset > 4.0 && !widget.rankingStyle;

        final pinnedWidth = isUltraNarrow
            ? 124.0
            : availableWidth < 400
            ? 142.0
            : isMobile
            ? 156.0
            : 190.0;

        final headingHeight = isMobile ? 38.0 : 34.0;
        final rowHeight = widget.rankingStyle
            ? 44.0
            : isMobile
            ? 42.0
            : 34.0;

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
          headingRowColor: WidgetStateProperty.all(
            ClassicTheme.of(context).tableHeader,
          ),
          headingRowHeight: headingHeight,
          dataRowMinHeight: rowHeight,
          dataRowMaxHeight: widget.rankingStyle ? 104 : rowHeight,
          columnSpacing: columnSpacing,
          horizontalMargin: horizontalMargin,
          border: TableBorder(
            horizontalInside: BorderSide(
              color: ClassicTheme.of(context).border.withValues(alpha: 0.45),
              width: 0.8,
            ),
          ),
          dataTextStyle: TextStyle(
            fontSize: widget.rankingStyle
                ? 16
                : isUltraNarrow
                ? 11
                : 12,
            color: ClassicTheme.of(context).textPrimary,
            fontFamily:
                (widget.rankingStyle ||
                    ClassicTheme.of(context).classicPresentation)
                ? 'ClassicCondensed'
                : 'Roboto',
          ),
          headingTextStyle: TextStyle(
            fontSize: widget.rankingStyle
                ? 14
                : isUltraNarrow
                ? 10
                : 11,
            fontWeight: FontWeight.bold,
            color: ClassicTheme.of(context).classicPresentation
                ? readableInk(
                    ClassicTheme.of(context).tableHeader,
                    ClassicTheme.of(context).textPrimary,
                  )
                : ClassicTheme.of(context).accent,
            fontFamily:
                (widget.rankingStyle ||
                    ClassicTheme.of(context).classicPresentation)
                ? 'ClassicCondensed'
                : 'Roboto',
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
            color: ClassicTheme.of(context).tableRow,
            border: Border.all(
              color: ClassicTheme.of(context).border,
              width: 1,
            ),
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
      decoration: BoxDecoration(
        color: ClassicTheme.of(context).tableRow,
        border: Border(
          right: BorderSide(
            color: ClassicTheme.of(context).selectedBackground,
            width: 1.5,
          ),
        ),
        boxShadow: [
          BoxShadow(color: Colors.black54, blurRadius: 4, offset: Offset(2, 0)),
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
            color: ClassicTheme.of(context).surface,
            child: Text(
              header,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: TextStyle(
                fontSize: compact ? 10 : 11,
                fontWeight: FontWeight.bold,
                color: ClassicTheme.of(context).accent,
                fontFamily: ClassicTheme.of(context).classicPresentation
                    ? 'ClassicCondensed'
                    : 'Roboto',
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
                      ? ClassicTheme.of(context).surfaceAlternate
                      : (row.isEven
                            ? ClassicTheme.of(context).tableRow
                            : ClassicTheme.of(context).tableRowAlternate),
                  border: Border(
                    top: BorderSide(
                      color: ClassicTheme.of(
                        context,
                      ).border.withValues(alpha: 0.45),
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
                            color: row.highlight
                                ? ClassicTheme.of(context).accent
                                : ClassicTheme.of(context).textPrimary,
                            fontWeight: row.highlight
                                ? FontWeight.bold
                                : FontWeight.normal,
                            fontFamily:
                                ClassicTheme.of(context).classicPresentation
                                ? 'ClassicCondensed'
                                : 'Roboto',
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
                              color: ClassicTheme.of(context).accent,
                              fontWeight: FontWeight.bold,
                              decoration: row.onTap != null
                                  ? TextDecoration.underline
                                  : TextDecoration.none,
                              fontFamily:
                                  ClassicTheme.of(context).classicPresentation
                                  ? 'ClassicCondensed'
                                  : 'Roboto',
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
                                color: ClassicTheme.of(context).textSecondary,
                                fontFamily:
                                    ClassicTheme.of(context).classicPresentation
                                    ? 'ClassicCondensed'
                                    : 'Roboto',
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
  final Color color;
  const _ClassicDropdownArrowPainter(this.color);

  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint()
      ..color = color
      ..style = PaintingStyle.fill;
    final path = Path()
      ..moveTo(0, 0)
      ..lineTo(size.width, 0)
      ..lineTo(size.width / 2, size.height)
      ..close();
    canvas.drawPath(path, paint);
  }

  @override
  bool shouldRepaint(covariant _ClassicDropdownArrowPainter oldDelegate) =>
      oldDelegate.color != color;
}

class _ClassicRefreshIconPainter extends CustomPainter {
  final Color color;
  const _ClassicRefreshIconPainter(this.color);

  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint()
      ..color = color
      ..style = PaintingStyle.stroke
      ..strokeWidth = 1.6;
    final rect = Rect.fromLTWH(1, 1, size.width - 2, size.height - 2);
    canvas.drawArc(rect, 0.4, math.pi * 1.55, false, paint);

    final fill = Paint()
      ..color = color
      ..style = PaintingStyle.fill;
    final arrow = Path()
      ..moveTo(size.width - 1, 1)
      ..lineTo(size.width - 5, 1)
      ..lineTo(size.width - 1, 5)
      ..close();
    canvas.drawPath(arrow, fill);
  }

  @override
  bool shouldRepaint(covariant _ClassicRefreshIconPainter oldDelegate) =>
      oldDelegate.color != color;
}

class _ClassicCloseIconPainter extends CustomPainter {
  final Color color;
  const _ClassicCloseIconPainter(this.color);

  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint()
      ..color = color
      ..style = PaintingStyle.stroke
      ..strokeWidth = 1.8;
    canvas.drawLine(Offset.zero, Offset(size.width, size.height), paint);
    canvas.drawLine(Offset(size.width, 0), Offset(0, size.height), paint);
  }

  @override
  bool shouldRepaint(covariant _ClassicCloseIconPainter oldDelegate) =>
      oldDelegate.color != color;
}

class PlayerStrengthHistoryPanel extends StatelessWidget {
  final Map<String, dynamic> data;
  const PlayerStrengthHistoryPanel({super.key, required this.data});

  @override
  Widget build(BuildContext context) {
    final events = _records(data['strengthHistory']);
    final startSeason = data['strengthHistoryStartSeason'];
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const ClassicSectionTitle(title: 'HISTÓRICO DE FORÇA'),
          if (startSeason != null)
            Text(
              'Registro desde T$startSeason R${data['strengthHistoryStartRound']}. Mudanças anteriores não estão disponíveis.',
              style: const TextStyle(fontSize: 12),
            ),
          if (events.isEmpty)
            const Text('Nenhuma mudança de força registrada.')
          else
            ClassicTable(
              tableKey: 'player-strength-history',
              columns: const [
                'TEMPORADA',
                'RODADA',
                'CLUBE',
                'DIVISÃO',
                'ANTES',
                'DEPOIS',
                'MOTIVO',
                'DATA DO JOGO',
                'POSIÇÃO DO CLUBE',
                'TETO',
              ],
              numericColumns: const {0, 1, 4, 5},
              rows: events
                  .map(
                    (event) => DataRow(
                      cells: [
                        DataCell(Text(_value(event['season']))),
                        DataCell(Text(_value(event['round']))),
                        DataCell(Text(resolveHistoricalClubName(event))),
                        DataCell(Text(_strengthDivision(event['division']))),
                        DataCell(Text(_value(event['strengthBefore']))),
                        DataCell(Text(_value(event['strengthAfter']))),
                        DataCell(Text(_strengthReason(event))),
                        DataCell(
                          Text(
                            event['gameDate'] == null
                                ? '-'
                                : formatClassicDate(event['gameDate']),
                          ),
                        ),
                        DataCell(
                          Text(event['leaguePosition']?.toString() ?? '-'),
                        ),
                        DataCell(
                          Text(event['growthCeiling']?.toString() ?? '-'),
                        ),
                      ],
                    ),
                  )
                  .toList(),
            ),
        ],
      ),
    );
  }

  static String _strengthDivision(dynamic division) => switch ('$division') {
    'PrimeiraDivisao' || '1' => '1ª DIVISÃO',
    'SegundaDivisao' || '2' => '2ª DIVISÃO',
    'TerceiraDivisao' || '3' => '3ª DIVISÃO',
    'QuartaDivisao' || '4' => '4ª DIVISÃO',
    'Distrital' || '5' => 'DISTRITAL',
    _ => 'SEM DIVISÃO',
  };

  static String _strengthReason(Map<String, dynamic> event) {
    if (event['rule'] == 'LEGACY_SEASONAL_005_SCALE_FIX_012') {
      return event['changeReason'] == 'NORMAL_DECLINE'
          ? 'Queda sazonal'
          : 'Evolução sazonal';
    }
    return switch (event['changeReason']) {
      'NORMAL_PROGRESS' => 'Evolução',
      'NORMAL_DECLINE' => 'Queda de força',
      'INJURY_STRENGTH_LOSS' => 'Perda por contusão',
      'RECOVERY_PROGRESS' => 'Recuperação',
      'RESERVE_PROGRESS' => 'Evolução sem jogar',
      'PLAYED_MATCH_DECLINE' => 'Queda após participação',
      'INJURY_DAILY_LOSS' => 'Perda diária por contusão',
      _ => 'Outra mudança',
    };
  }
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
            style: TextStyle(
              color: ClassicTheme.of(context).accent,
              fontSize: 12,
            ),
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
