import 'dart:convert';

import 'package:elifoot_flutter/api_client.dart';
import 'package:elifoot_flutter/player_rankings.dart';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

class _OfflineApi extends ElifootApiClient {
  _OfflineApi(http.Client client) : super(httpClient: client);
  @override
  void connectRealtimeRoom(String worldId, String coachId) {}
}

Map<String, dynamic> _playerRow(
  String id,
  String name, {
  int rank = 1,
  String position = 'A',
  int strength = 45,
  String? clubId = 'c1111111-1111-4111-8111-111111111111',
  String clubName = 'Guarani da Serra',
  int matches = 24,
  int goals = 14,
  int conceded = 8,
  int yellows = 3,
  int reds = 1,
  num? average = 0.583,
  int titles = 1,
}) => {
  'rank': rank,
  'playerId': id,
  'displayName': name,
  'position': position,
  'strength': strength,
  'behaviorDisplay': 'Fair Play',
  'isStar': name.endsWith('*'),
  'currentClubId': clubId,
  'currentClubName': clubId == null ? '' : clubName,
  'matchesPlayed': matches,
  'goalsScored': goals,
  'goalsConceded': conceded,
  'yellowCards': yellows,
  'redCards': reds,
  'minutesPlayed': matches * 90,
  'average': average,
  'redRate': matches > 0 ? reds / matches : 0.0,
  'yellowRate': matches > 0 ? yellows / matches : 0.0,
  'matchesRemaining': matches < 20 ? 20 - matches : 0,
  'seasonTopScorerTitles': titles,
};

Map<String, dynamic> _statRow({
  required int season,
  required String competitionType,
  String? division,
  required String? clubId,
  String clubName = '',
  int matches = 15,
  int goals = 8,
  int conceded = 4,
  int yellows = 2,
  int reds = 0,
  bool incomplete = false,
}) => {
  'season': season,
  'competitionType': competitionType,
  'division': division,
  'clubId': clubId,
  'clubName': clubName,
  'matchesPlayed': matches,
  'goalsScored': goals,
  'goalsConceded': conceded,
  'yellowCards': yellows,
  'redCards': reds,
  'minutesPlayed': matches * 90,
  'hasIncompleteAppearances': incomplete,
};

Map<String, dynamic> _awardRow({
  required int season,
  String id = 'p1',
  String name = 'Mateus Alves*',
  int goals = 10,
  int matches = 17,
  String? clubId = 'f7ca895f-a395-994f-a2e4-8b0fc7ad8fb7',
  String clubName = 'Guarani da Serra',
  int amount = 3000000,
  String grantedAt = '2026-10-30T12:51:52.36020317Z',
}) => {
  'awardId': '$id-$season',
  'playerId': id,
  'displayName': name,
  'season': season,
  'totalGoals': goals,
  'totalMatches': matches,
  'clubId': clubId,
  'clubNameAtClose': clubId == null ? 'Sem clube' : clubName,
  'awardAmount': clubId == null ? 0 : amount,
  'awardGrantedAt': grantedAt,
  'idempotencyKey': 'season_top_scorer:w:$season:$id',
};

class _Visual010Fixture {
  final List<http.Request> requests = [];
  late final ElifootApiClient api;

  List<Map<String, dynamic>> scorers = [
    _playerRow(
      'p1',
      'Mateus Alves*',
      rank: 1,
      clubId: 'f7ca895f-a395-994f-a2e4-8b0fc7ad8fb7',
      clubName: 'Guarani da Serra',
      matches: 17,
      goals: 10,
      average: 0.588,
      titles: 1,
    ),
    _playerRow(
      'p2',
      'Thiago Machado*',
      rank: 2,
      clubId: '836cea4d-dd81-d24a-805b-c2be06bd2e00',
      clubName: 'Real Pampulha',
      matches: 17,
      goals: 10,
      average: 0.588,
      titles: 1,
    ),
    _playerRow(
      'p3',
      'Rodrigo Cardoso*',
      rank: 3,
      clubId: 'a1111111-2222-4333-8444-555555555555',
      clubName: 'Atlético do Norte',
      matches: 16,
      goals: 9,
      average: 0.563,
      titles: 0,
    ),
  ];

  List<Map<String, dynamic>> goalkeepers = [
    _playerRow(
      'gk1',
      'Pedro Santana*',
      rank: 1,
      position: 'G',
      clubId: 'f7ca895f-a395-994f-a2e4-8b0fc7ad8fb7',
      clubName: 'Guarani da Serra',
      matches: 24,
      goals: 0,
      conceded: 11,
      average: 0.458,
      titles: 0,
    ),
  ];

  List<Map<String, dynamic>> goalkeepersIneligible = [
    _playerRow(
      'gk2',
      'Fábio Moraes',
      rank: 0,
      position: 'G',
      clubId: '836cea4d-dd81-d24a-805b-c2be06bd2e00',
      clubName: 'Real Pampulha',
      matches: 16,
      goals: 0,
      conceded: 9,
      average: 0.563,
      titles: 0,
    ),
  ];

  List<Map<String, dynamic>> defenders = [
    _playerRow(
      'df1',
      'Lucas Ferraz*',
      rank: 1,
      position: 'D',
      clubId: 'f7ca895f-a395-994f-a2e4-8b0fc7ad8fb7',
      clubName: 'Guarani da Serra',
      matches: 22,
      goals: 2,
      yellows: 2,
      reds: 0,
      average: 0.091,
      titles: 0,
    ),
  ];

  List<Map<String, dynamic>> defendersIneligible = [
    _playerRow(
      'df2',
      'Henrique Dias',
      rank: 0,
      position: 'D',
      clubId: '836cea4d-dd81-d24a-805b-c2be06bd2e00',
      clubName: 'Real Pampulha',
      matches: 14,
      goals: 0,
      yellows: 3,
      reds: 1,
      average: 0.429,
      titles: 0,
    ),
  ];

  Map<String, Map<String, dynamic>> historiesByPlayerId = {};
  List<Map<String, dynamic>> awards = [
    _awardRow(
      season: 1,
      id: 'p1',
      name: 'Mateus Alves*',
      clubId: 'f7ca895f-a395-994f-a2e4-8b0fc7ad8fb7',
      clubName: 'Guarani da Serra',
    ),
    _awardRow(
      season: 1,
      id: 'p2',
      name: 'Thiago Machado*',
      clubId: '836cea4d-dd81-d24a-805b-c2be06bd2e00',
      clubName: 'Real Pampulha',
    ),
  ];
  List<Map<String, dynamic>> clubTotals = [
    {
      'clubId': 'f7ca895f-a395-994f-a2e4-8b0fc7ad8fb7',
      'clubName': 'Guarani da Serra',
      'amount': 3000000,
    },
    {
      'clubId': '836cea4d-dd81-d24a-805b-c2be06bd2e00',
      'clubName': 'Real Pampulha',
      'amount': 3000000,
    },
  ];

  _Visual010Fixture() {
    historiesByPlayerId['p1'] = {
      'player': scorers.first,
      'career': scorers.first,
      'statistics': [
        _statRow(
          season: 1,
          competitionType: 'League',
          division: 'PrimeiraDivisao',
          clubId: 'f7ca895f-a395-994f-a2e4-8b0fc7ad8fb7',
          clubName: 'Guarani da Serra',
          matches: 15,
          goals: 7,
        ),
        _statRow(
          season: 1,
          competitionType: 'CupPreliminary1',
          division: null,
          clubId: 'f7ca895f-a395-994f-a2e4-8b0fc7ad8fb7',
          clubName: 'Guarani da Serra',
          matches: 2,
          goals: 3,
        ),
      ],
      'awards': [awards.first],
      'coverageWarnings': [],
    };

    api = _OfflineApi(
      MockClient((request) async {
        requests.add(request);
        final path = request.url.path;
        if (path.endsWith('/player-rankings')) {
          final category = request.url.queryParameters['category'] ?? 'scorers';
          final rows = switch (category) {
            'goalkeepers' => goalkeepers,
            'defenders' => defenders,
            _ => scorers,
          };
          final notEligible = switch (category) {
            'goalkeepers' => goalkeepersIneligible,
            'defenders' => defendersIneligible,
            _ => <Map<String, dynamic>>[],
          };
          return _json({
            ...request.url.queryParameters,
            'season': request.url.queryParameters['period'] == 'career'
                ? null
                : int.tryParse(request.url.queryParameters['season'] ?? '') ??
                      2,
            'currentSeason': 2,
            'minimumMatches': 20,
            'rows': rows,
            'notEligible': notEligible,
            'seasons': [1, 2],
            'coverageWarnings': [],
          });
        }
        if (path.endsWith('/season-top-scorers')) {
          return _json({
            'currentSeason': 2,
            'season': 1,
            'awards': awards,
            'clubTotals': clubTotals,
            'coverageWarnings': [],
          });
        }
        if (path.endsWith('/history')) {
          final segments = request.url.pathSegments;
          final playerId = segments[segments.length - 2];
          final payload =
              historiesByPlayerId[playerId] ?? historiesByPlayerId['p1']!;
          return _json(payload);
        }
        return _json([]);
      }),
    );
  }

  static http.Response _json(Object body, [int status = 200]) => http.Response(
    jsonEncode(body),
    status,
    headers: {'content-type': 'application/json; charset=utf-8'},
  );
}

Widget _wrapApp(Widget child) => MaterialApp(
  theme: ThemeData(
    useMaterial3: false,
    brightness: Brightness.dark,
    fontFamily: 'monospace',
    scaffoldBackgroundColor: const Color(0xFF0A4D1C),
  ),
  home: Scaffold(body: child),
);

Finder _inTable(String key, String text) =>
    find.descendant(of: find.byKey(ValueKey(key)), matching: find.text(text));

Future<void> _selectFilter(
  WidgetTester tester,
  String key,
  String optionLabel,
) async {
  await tester.ensureVisible(find.byKey(ValueKey(key)));
  await tester.tap(find.byKey(ValueKey(key)));
  await tester.pumpAndSettle();
  await tester.tap(find.text(optionLabel).last);
  await tester.pumpAndSettle();
}

void main() {
  setUp(() {
    ClubNameDirectory.clearForTesting();
  });

  group('010 Unit Formatters (Pluralization, Dates, Money, Historical Club)', () {
    test(
      'formatTopScorerTitles formats 0, 1, 2, 5, and 10 titles accurately',
      () {
        expect(formatTopScorerTitles(0), 'ARTILHEIRO: 0 VEZES');
        expect(formatTopScorerTitles(1), 'ARTILHEIRO: 1 VEZ');
        expect(formatTopScorerTitles(2), 'ARTILHEIRO: 2 VEZES');
        expect(formatTopScorerTitles(5), 'ARTILHEIRO: 5 VEZES');
        expect(formatTopScorerTitles(10), 'ARTILHEIRO: 10 VEZES');
      },
    );

    test(
      'formatClassicDate formats ISO timestamps with and without time in pt-BR',
      () {
        expect(formatClassicDate('2026-10-30T12:51:52.36020317'), '30/10/2026');
        expect(
          formatClassicDate('2026-10-30T12:51:52.36020317Z', includeTime: true),
          '30/10/2026 12:51',
        );
        expect(formatClassicDate('2026-01-05T08:04:00Z'), '05/01/2026');
        expect(
          formatClassicDate('2026-01-05T08:04:00Z', includeTime: true),
          '05/01/2026 08:04',
        );
        expect(formatClassicDate(null), '—');
        expect(formatClassicDate(''), '—');
      },
    );

    test('formatClassicMoney formats R\$ values without altering amounts', () {
      expect(formatClassicMoney(3000000), 'R\$ 3.000.000');
      expect(formatClassicMoney(6000000), 'R\$ 6.000.000');
      expect(formatClassicMoney(0), 'R\$ 0');
      expect(formatClassicMoney(null), '—');
    });

    test(
      'resolveHistoricalClubName resolves explicit clubName, exact clubId, and friendly fallback without leaking raw GUIDs',
      () {
        // Explicit historical club name from persisted match/season record.
        expect(
          resolveHistoricalClubName({
            'season': 1,
            'clubId': 'f7ca895f-a395-994f-a2e4-8b0fc7ad8fb7',
            'clubName': 'Guarani da Serra',
          }),
          'Guarani da Serra',
        );

        // Must never indiscriminately apply player's currentClubName to a different clubId.
        final transferredRow = {
          'season': 1,
          'clubId': '11111111-2222-4333-8444-555555555555',
        };
        final currentPlayer = {
          'playerId': 'p1',
          'currentClubId': '99999999-8888-4777-8666-555555555555',
          'currentClubName': 'Clube Atual Diferente',
        };
        expect(
          resolveHistoricalClubName(transferredRow, player: currentPlayer),
          'Clube histórico indisponível',
        );

        // Resolves when knownClubs or awards map that exact historical clubId.
        expect(
          resolveHistoricalClubName(
            transferredRow,
            player: currentPlayer,
            knownClubs: {
              '11111111-2222-4333-8444-555555555555': 'Clube Antigo da Série B',
            },
          ),
          'Clube Antigo da Série B',
        );
      },
    );
  });

  testWidgets(
    '010 player history renders 1 VEZ, 0 VEZES, 2 VEZES, 5 VEZES and formatted dates & money',
    (tester) async {
      tester.view.physicalSize = const Size(1280, 900);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);

      for (final entry in {
        0: 'ARTILHEIRO: 0 VEZES',
        1: 'ARTILHEIRO: 1 VEZ',
        2: 'ARTILHEIRO: 2 VEZES',
        5: 'ARTILHEIRO: 5 VEZES',
      }.entries) {
        final f = _Visual010Fixture();
        f.historiesByPlayerId['p1'] = {
          'player': _playerRow('p1', 'Mateus Alves*', titles: entry.key),
          'career': _playerRow('p1', 'Mateus Alves*', titles: entry.key),
          'statistics': [
            _statRow(
              season: 1,
              competitionType: 'League',
              division: 'PrimeiraDivisao',
              clubId: 'f7ca895f-a395-994f-a2e4-8b0fc7ad8fb7',
              clubName: 'Guarani da Serra',
            ),
          ],
          'awards': [
            if (entry.key > 0)
              _awardRow(
                season: 1,
                grantedAt: '2026-10-30T12:51:52.36020317Z',
                amount: 3000000,
              ),
          ],
          'coverageWarnings': [],
        };

        await tester.pumpWidget(
          _wrapApp(
            PlayerRankingsScreen(
              apiClient: f.api,
              worldId: 'w',
              currentSeason: 2,
            ),
          ),
        );
        await tester.pumpAndSettle();

        await tester.tap(_inTable('ranking-table', 'Mateus Alves*'));
        await tester.pumpAndSettle();

        expect(find.text(entry.value), findsOneWidget);
        expect(
          _inTable('player-season-history', 'Guarani da Serra'),
          findsOneWidget,
        );
        expect(find.text('f7ca895f-a395-994f-a2e4-8b0fc7ad8fb7'), findsNothing);
        if (entry.key > 0) {
          expect(
            _inTable('player-award-history', '30/10/2026'),
            findsOneWidget,
          );
          expect(
            _inTable('player-award-history', 'R\$ 3.000.000'),
            findsOneWidget,
          );
          expect(find.textContaining('2026-10-30T12:51:52'), findsNothing);
        }

        await tester.tap(find.text('FECHAR'));
        await tester.pumpAndSettle();
      }
    },
  );

  testWidgets(
    '010 historical clubs: same club, club transfer, division change, two clubs in same season, and unknown GUID fallback',
    (tester) async {
      tester.view.physicalSize = const Size(1280, 900);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);

      final f = _Visual010Fixture();
      f.historiesByPlayerId['p1'] = {
        'player': _playerRow(
          'p1',
          'Mateus Alves*',
          clubId: 'c3333333-3333-4333-8333-333333333333',
          clubName: 'Estrela do Sul',
          titles: 1,
        ),
        'career': _playerRow(
          'p1',
          'Mateus Alves*',
          clubId: 'c3333333-3333-4333-8333-333333333333',
          clubName: 'Estrela do Sul',
          matches: 48,
          goals: 26,
          titles: 1,
        ),
        'statistics': [
          // Season 1: Two different clubs in the same season & different divisions
          _statRow(
            season: 1,
            competitionType: 'League',
            division: 'SegundaDivisao',
            clubId: 'c1111111-1111-4111-8111-111111111111',
            clubName: 'Guarani da Serra',
            matches: 10,
            goals: 6,
          ),
          _statRow(
            season: 1,
            competitionType: 'League',
            division: 'PrimeiraDivisao',
            clubId: 'c2222222-2222-4222-8222-222222222222',
            clubName: 'Real Pampulha',
            matches: 12,
            goals: 8,
          ),
          // Season 2: Transferred to current club
          _statRow(
            season: 2,
            competitionType: 'League',
            division: 'PrimeiraDivisao',
            clubId: 'c3333333-3333-4333-8333-333333333333',
            clubName: 'Estrela do Sul',
            matches: 18,
            goals: 10,
          ),
          // Legacy unresolvable GUID record
          _statRow(
            season: 0,
            competitionType: 'League',
            division: 'QuartaDivisao',
            clubId: 'deadbeef-dead-4eef-8eef-deadbeef0000',
            clubName: '',
            matches: 8,
            goals: 2,
          ),
        ],
        'awards': [_awardRow(season: 1)],
        'coverageWarnings': [],
      };

      await tester.pumpWidget(
        _wrapApp(
          PlayerRankingsScreen(
            apiClient: f.api,
            worldId: 'w',
            currentSeason: 2,
          ),
        ),
      );
      await tester.pumpAndSettle();

      await tester.tap(_inTable('ranking-table', 'Mateus Alves*'));
      await tester.pumpAndSettle();

      expect(
        _inTable('player-season-history', 'Guarani da Serra'),
        findsOneWidget,
      );
      expect(
        _inTable('player-season-history', 'Real Pampulha'),
        findsOneWidget,
      );
      expect(
        _inTable('player-season-history', 'Estrela do Sul'),
        findsOneWidget,
      );
      expect(
        _inTable('player-season-history', 'Clube histórico indisponível'),
        findsOneWidget,
      );
      expect(find.text('deadbeef-dead-4eef-8eef-deadbeef0000'), findsNothing);

      // Close via Escape key (Section 21 accessibility)
      await tester.sendKeyEvent(LogicalKeyboardKey.escape);
      await tester.pumpAndSettle();
      expect(find.text('HISTÓRICO DO JOGADOR'), findsNothing);
    },
  );

  testWidgets(
    '010 filters, competitions, periods, goalkeepers >=20 & <20, defenders >=20 & <20, and tied season awards',
    (tester) async {
      tester.view.physicalSize = const Size(1280, 900);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);

      final f = _Visual010Fixture();
      await tester.pumpWidget(
        _wrapApp(
          PlayerRankingsScreen(
            apiClient: f.api,
            worldId: 'w',
            currentSeason: 2,
          ),
        ),
      );
      await tester.pumpAndSettle();

      // Competition filters: Geral, 1ª..4ª divisão, Taça
      for (final scopeLabel in [
        '1ª divisão',
        '2ª divisão',
        '3ª divisão',
        '4ª divisão',
        'Taça',
        'Geral',
      ]) {
        await _selectFilter(tester, 'ranking-scope', scopeLabel);
        expect(tester.takeException(), isNull);
      }

      // Period filters
      await _selectFilter(tester, 'ranking-period', 'Temporada atual');
      await _selectFilter(tester, 'ranking-period', 'Temporada selecionada');
      await _selectFilter(tester, 'ranking-season', 'Temporada 1');
      await _selectFilter(tester, 'ranking-period', 'Carreira inteira');

      // Goalkeepers >= 20 and < 20
      await _selectFilter(tester, 'ranking-category', 'Goleiros');
      expect(find.text('GOLS SOFRIDOS'), findsWidgets);
      expect(_inTable('ranking-table', 'Pedro Santana*'), findsOneWidget);
      expect(_inTable('not-eligible-table', 'Fábio Moraes'), findsOneWidget);
      expect(
        _inTable('not-eligible-table', '4'),
        findsOneWidget,
      ); // 20 - 16 = 4

      // Defenders >= 20 and < 20
      await _selectFilter(tester, 'ranking-category', 'Defensores');
      expect(find.text('AMARELOS'), findsWidgets);
      expect(find.text('VERMELHOS'), findsWidgets);
      expect(find.text('ÍNDICE'), findsWidgets);
      expect(_inTable('ranking-table', 'Lucas Ferraz*'), findsOneWidget);
      expect(_inTable('not-eligible-table', 'Henrique Dias'), findsOneWidget);
      expect(
        _inTable('not-eligible-table', '6'),
        findsOneWidget,
      ); // 20 - 14 = 6

      // Tied top scorer awards in panel
      expect(
        find.textContaining('EMPATE NA ARTILHARIA: 2 ARTILHEIROS EMPATADOS'),
        findsOneWidget,
      );
      expect(
        _inTable('season-scorer-awards', 'R\$ 3.000.000'),
        findsNWidgets(2),
      );
      expect(_inTable('season-club-awards', 'R\$ 3.000.000'), findsNWidgets(2));
    },
  );

  for (final width in <double>[320, 360, 390, 430, 768, 1024, 1280]) {
    testWidgets(
      '010 responsive layout at ${width.toInt()}px: zero overflow, readable tables, pinned player name on mobile scroll, and long names',
      (tester) async {
        tester.view.physicalSize = Size(width, 844);
        tester.view.devicePixelRatio = 1;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);

        final f = _Visual010Fixture();
        f.scorers.add(
          _playerRow(
            'p-long',
            'Maximiliano Albuquerque de Bragança Júnior*',
            rank: 4,
            clubId: 'c4444444-4444-4444-8444-444444444444',
            clubName: 'Associação Atlética Ferroviária de Paranapiacaba',
            matches: 21,
            goals: 8,
            average: 0.381,
          ),
        );

        await tester.pumpWidget(
          _wrapApp(
            PlayerRankingsScreen(
              apiClient: f.api,
              worldId: 'w',
              currentSeason: 2,
            ),
          ),
        );
        await tester.pumpAndSettle();
        expect(tester.takeException(), isNull);

        // Verify player name is visible before horizontal scroll
        expect(find.text('Mateus Alves*'), findsWidgets);

        // Compact ranking keeps complete identities visible; desktop tables preserve their internal scrolling.
        if (width < 700) {
          final compact = find.byKey(const ValueKey('ranking-table'));
          await tester.ensureVisible(compact);
          expect(tester.getRect(compact).right, lessThanOrEqualTo(width));
          expect(
            find.byKey(const ValueKey('ranking-table-horizontal')),
            findsNothing,
          );
        } else {
          final horizontal = find.byKey(
            const ValueKey('ranking-table-horizontal'),
          );
          await tester.ensureVisible(horizontal);
          await tester.drag(horizontal, const Offset(-320, 0));
          await tester.pumpAndSettle();
        }
        expect(tester.takeException(), isNull);
        expect(find.text('Mateus Alves*'), findsWidgets);

        // Return to the identity column on larger tables before opening the full history.
        if (width >= 700) {
          await tester.drag(
            find.byKey(const ValueKey('ranking-table-horizontal')),
            const Offset(900, 0),
          );
          await tester.pumpAndSettle();
        }
        // Tap player name in the compact mobile row or full desktop table.
        await tester.tap(find.text('Mateus Alves*').hitTestable().first);
        await tester.pumpAndSettle();
        expect(tester.takeException(), isNull);
        expect(find.text('HISTÓRICO DO JOGADOR'), findsOneWidget);
        expect(find.text('ARTILHEIRO: 1 VEZ'), findsOneWidget);

        await tester.tap(find.text('FECHAR'));
        await tester.pumpAndSettle();
        expect(tester.takeException(), isNull);
      },
    );
  }
}
