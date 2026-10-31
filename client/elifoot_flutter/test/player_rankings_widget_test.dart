import 'dart:async';
import 'dart:convert';

import 'package:elifoot_flutter/api_client.dart';
import 'package:elifoot_flutter/main.dart';
import 'package:elifoot_flutter/player_rankings.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

Map<String, dynamic> player(
  String id,
  String name, {
  int matches = 20,
  int goals = 29,
  String position = 'A',
  num? average = 1.45,
  int rank = 1,
  String? clubId = 'club-now',
}) => {
  'rank': rank,
  'playerId': id,
  'displayName': name,
  'position': position,
  'strength': 46,
  'behaviorDisplay': 'Fair Play',
  'isStar': name.endsWith('*'),
  'currentClubId': clubId,
  'currentClubName': clubId == null ? '' : 'Clube Atual',
  'matchesPlayed': matches,
  'goalsScored': goals,
  'goalsConceded': 10,
  'yellowCards': 2,
  'redCards': 1,
  'minutesPlayed': matches * 90,
  'average': average,
  'redRate': 0.05,
  'yellowRate': 0.1,
  'matchesRemaining': matches < 20 ? 20 - matches : 0,
  'seasonTopScorerTitles': 5,
};

Map<String, dynamic> award(
  int season, {
  String id = 'p1',
  String name = 'Carlos Silva*',
  int matches = 30,
  String? clubId = 'club-close',
}) => {
  'awardId': '$id-$season',
  'playerId': id,
  'displayName': name,
  'season': season,
  'totalGoals': 29,
  'totalMatches': matches,
  'clubId': clubId,
  'clubNameAtClose': clubId == null ? 'Sem clube' : 'Clube no Fechamento',
  'awardAmount': clubId == null ? 0 : 3000000,
  'awardGrantedAt': '$season-12-31T00:00:00Z',
  'idempotencyKey': 'season_top_scorer:world:$season:$id',
};

Map<String, dynamic> statistics(
  int season,
  String competition,
  dynamic division,
  String club, {
  int matches = 10,
  int goals = 6,
  bool incomplete = false,
}) => {
  'season': season,
  'competitionType': competition,
  'division': division,
  'clubId': club,
  'matchesPlayed': matches,
  'goalsScored': goals,
  'goalsConceded': 3,
  'yellowCards': 1,
  'redCards': 0,
  'minutesPlayed': matches * 90,
  'hasIncompleteAppearances': incomplete,
};

class Fixture {
  final requests = <http.Request>[];
  late final ElifootApiClient api;
  List<Map<String, dynamic>> rows = [player('p1', 'Carlos Silva*')];
  List<Map<String, dynamic>> notEligible = [];
  List<Map<String, dynamic>> awards = [];
  List<Map<String, dynamic>> clubTotals = [];
  List<String> warnings = [];
  Map<String, dynamic>? history;
  int failures = 0;
  int historyFailures = 0;
  int awardFailures = 0;
  int season = 2036;
  int version = 10;
  Future<http.Response> Function(http.Request)? rankingOverride;

  Map<String, dynamic> get snapshot => {
    'gameWorldId': 'world',
    'worldName': 'Clássico',
    'seasonNumber': season,
    'roundNumber': 1,
    'roundVersion': version,
    'roundState': 'PREPARING',
    'readyCounter': {
      'readyCount': 0,
      'totalHumans': 1,
      'participants': [
        {
          'coachId': 'coach',
          'nickname': 'Treinador 1',
          'clubId': null,
          'isUnemployed': true,
          'morale': 70,
          'moraleBand': 'VERDE',
        },
      ],
    },
    'matches': [],
  };

  Fixture() {
    api = _OfflineApi(
      MockClient((request) async {
        requests.add(request);
        final path = request.url.path;
        if (path.endsWith('/player-rankings')) {
          if (rankingOverride != null) return rankingOverride!(request);
          if (failures > 0) {
            failures--;
            return response({'error': 'failed'}, 503);
          }
          return response({
            ...request.url.queryParameters,
            'season': request.url.queryParameters['period'] == 'career'
                ? null
                : int.tryParse(request.url.queryParameters['season'] ?? '') ??
                      season,
            'currentSeason': season,
            'minimumMatches': 20,
            'rows': rows,
            'notEligible': notEligible,
            'seasons': [2028, 2029, 2030, 2032, 2035, 2036],
            'coverageWarnings': warnings,
          });
        }
        if (path.endsWith('/season-top-scorers')) {
          if (awardFailures > 0) {
            awardFailures--;
            return response({'error': 'failed'}, 503);
          }
          return response({
            'currentSeason': season,
            'season': awards.isEmpty ? null : awards.first['season'],
            'awards': awards,
            'clubTotals': clubTotals,
            'coverageWarnings': warnings,
          });
        }
        if (path.endsWith('/history')) {
          if (historyFailures > 0) {
            historyFailures--;
            return response({'error': 'failed'}, 503);
          }
          return response(
            history ??
                {
                  'player': rows.first,
                  'career': rows.first,
                  'statistics': [],
                  'awards': [],
                  'coverageWarnings': warnings,
                },
          );
        }
        if (path.endsWith('/local-login')) {
          return response({'coachId': 'coach', 'nickname': 'Treinador 1'});
        }
        if (path == '/api/worlds') {
          return response([
            {
              'id': 'world',
              'name': 'Clássico',
              'seasonNumber': season,
              'roundNumber': 1,
              'totalClubs': 128,
              'maxHumans': 8,
              'currentHumans': 1,
              'readyCounter': snapshot['readyCounter'],
            },
          ]);
        }
        if (path.endsWith('/join')) {
          return response({
            'coachId': 'coach',
            'clubId': null,
            'isUnemployed': true,
            'snapshot': snapshot,
          });
        }
        if (path.endsWith('/snapshot')) return response(snapshot);
        if (path.endsWith('/ranking')) {
          return response({
            'ranking': [
              {
                'rank': 1,
                'name': 'Técnico 008',
                'isHuman': true,
                'currentClubName': 'Clube 008',
                'leagueTitles': 2,
                'cupTitles': 1,
                'coachPoints': 12.5,
                'totalWins': 12,
                'totalDraws': 1,
                'totalLosses': 2,
              },
            ],
            'seasonHistory': [],
          });
        }
        if (path.contains('/invitations/')) {
          return response({
            'invitations': [],
            'employmentHistory': [],
            'morale': 70,
          });
        }
        if (path.endsWith('/market')) {
          return response({'listings': [], 'auctions': [], 'offers': []});
        }
        return response([]);
      }),
    );
  }

  static http.Response response(Object body, [int status = 200]) =>
      http.Response(
        jsonEncode(body),
        status,
        headers: {'content-type': 'application/json; charset=utf-8'},
      );
}

class _OfflineApi extends ElifootApiClient {
  _OfflineApi(http.Client client) : super(httpClient: client);
  @override
  void connectRealtimeRoom(String worldId, String coachId) {}
}

Widget app(Widget child) => MaterialApp(
  theme: ThemeData(
    useMaterial3: false,
    brightness: Brightness.dark,
    fontFamily: 'monospace',
    scaffoldBackgroundColor: const Color(0xFF0A4D1C),
  ),
  home: Scaffold(body: child),
);

Future<void> openRankings(
  WidgetTester tester,
  Fixture f, {
  double width = 1100,
}) async {
  tester.view.physicalSize = Size(width, 844);
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);
  await tester.pumpWidget(
    app(
      PlayerRankingsScreen(
        apiClient: f.api,
        worldId: 'world',
        currentSeason: 2036,
      ),
    ),
  );
  await tester.pumpAndSettle();
}

Future<void> select(WidgetTester tester, String key, String label) async {
  await tester.ensureVisible(find.byKey(ValueKey(key)));
  await tester.tap(find.byKey(ValueKey(key)));
  await tester.pumpAndSettle();
  await tester.tap(find.text(label).last);
  await tester.pumpAndSettle();
}

Finder inTable(String key, String text) =>
    find.descendant(of: find.byKey(ValueKey(key)), matching: find.text(text));

void main() {
  testWidgets(
    '009 filters request the selected category, all competitions and season periods',
    (tester) async {
      final f = Fixture();
      await openRankings(tester, f);
      expect(
        f.requests
            .where((r) => r.url.path.endsWith('/player-rankings'))
            .single
            .url
            .queryParameters,
        {'category': 'scorers', 'scope': 'general', 'period': 'career'},
      );
      await select(tester, 'ranking-category', 'Goleiros');
      for (final entry in {
        '1ª divisão': 'division1',
        '2ª divisão': 'division2',
        '3ª divisão': 'division3',
        '4ª divisão': 'division4',
        'Taça': 'cup',
        'Geral': 'general',
      }.entries) {
        await select(tester, 'ranking-scope', entry.key);
        expect(
          f.requests
              .lastWhere((r) => r.url.path.endsWith('/player-rankings'))
              .url
              .queryParameters['scope'],
          entry.value,
        );
      }
      await select(tester, 'ranking-period', 'Temporada atual');
      expect(
        f.requests
            .lastWhere((r) => r.url.path.endsWith('/player-rankings'))
            .url
            .queryParameters,
        {'category': 'goalkeepers', 'scope': 'general', 'period': 'current'},
      );
      await select(tester, 'ranking-period', 'Temporada selecionada');
      await select(tester, 'ranking-season', 'Temporada 2028');
      expect(
        f.requests
            .lastWhere((r) => r.url.path.endsWith('/player-rankings'))
            .url
            .queryParameters,
        {
          'category': 'goalkeepers',
          'scope': 'general',
          'period': 'season',
          'season': '2028',
        },
      );
      await select(tester, 'ranking-category', 'Defensores');
      expect(find.text('ÍNDICE'), findsOneWidget);
      await select(tester, 'ranking-period', 'Carreira inteira');
      expect(
        f.requests
            .lastWhere((r) => r.url.path.endsWith('/player-rankings'))
            .url
            .queryParameters
            .containsKey('season'),
        isFalse,
      );
    },
  );

  for (final category in ['Goleiros', 'Defensores']) {
    testWidgets(
      '009 $category: 20 matches rank, 19 and 1 remain unranked with missing matches',
      (tester) async {
        final f = Fixture()
          ..rows = [
            player('eligible', 'Vinte jogos', matches: 20),
            player('legacy', 'Um jogo', matches: 1),
          ]
          ..notEligible = [
            player('almost', 'Dezenove jogos', matches: 19),
            player('legacy', 'Um jogo', matches: 1),
          ];
        await openRankings(tester, f);
        await select(tester, 'ranking-category', category);
        expect(inTable('ranking-table', 'Vinte jogos'), findsOneWidget);
        expect(inTable('ranking-table', 'Um jogo'), findsNothing);
        expect(find.text('AINDA NÃO ELEGÍVEIS'), findsOneWidget);
        final ineligible = tester
            .widget<DataTable>(find.byKey(const ValueKey('not-eligible-table')))
            .rows;
        expect(ineligible, hasLength(2));
        expect(inTable('not-eligible-table', 'Dezenove jogos'), findsOneWidget);
        expect((ineligible[0].cells.last.child as Text).data, '1');
        expect((ineligible[1].cells.last.child as Text).data, '19');
        expect(
          inTable('not-eligible-table', '—'),
          findsNWidgets(2),
        ); // no rank assigned
      },
    );
  }

  testWidgets(
    '009 scorers have no match minimum and preserve server goal-first order',
    (tester) async {
      final f = Fixture()
        ..rows = [
          player('leader', 'Mais gols', matches: 100, goals: 30, average: 0.3),
          player(
            'second',
            'Melhor média',
            matches: 1,
            goals: 29,
            average: 29,
            rank: 2,
          ),
        ];
      await openRankings(tester, f);
      final table = tester.widget<DataTable>(
        find.byKey(const ValueKey('ranking-table')),
      );
      expect((table.rows.first.cells[1].child as Text).data, 'Mais gols');
      expect(inTable('ranking-table', 'Melhor média'), findsOneWidget);
      expect(find.text('AINDA NÃO ELEGÍVEIS'), findsNothing);
    },
  );

  testWidgets(
    '009 legacy null averages show a dash and coverage, with honest empty and retry states',
    (tester) async {
      final f = Fixture()
        ..rows = [player('legacy', 'Dados parciais', average: null)]
        ..warnings = ['Aparições históricas incompletas'];
      await openRankings(tester, f);
      expect(inTable('ranking-table', '—'), findsOneWidget);
      expect(
        find.text('COBERTURA: Aparições históricas incompletas'),
        findsNWidgets(2),
      );
      f.rows = [];
      f.failures = 1;
      await tester.tap(find.text('ATUALIZAR'));
      await tester.pumpAndSettle();
      expect(
        find.text('Não foi possível carregar ranking. Tente novamente.'),
        findsOneWidget,
      );
      expect(find.textContaining('Nenhum jogador elegível'), findsNothing);
      await tester.tap(find.text('TENTAR NOVAMENTE'));
      await tester.pumpAndSettle();
      expect(
        find.text(
          'Nenhum jogador elegível com estatísticas oficiais neste recorte.',
        ),
        findsOneWidget,
      );
      expect(
        find.text('Nenhuma temporada encerrada com premiações registradas.'),
        findsOneWidget,
      );
      expect(find.byKey(const ValueKey('ranking-table')), findsNothing);
    },
  );

  testWidgets(
    '009 stale responses never replace the most recent filter selection',
    (tester) async {
      final f = Fixture();
      final stale = Completer<http.Response>();
      f.rankingOverride = (r) async {
        if (r.url.queryParameters['category'] == 'goalkeepers') {
          return stale.future;
        }
        return Fixture.response({
          'rows': [player('d', 'Defensor atual')],
          'seasons': [2036],
        });
      };
      await openRankings(tester, f);
      await select(tester, 'ranking-category', 'Goleiros');
      expect(find.text('CARREGANDO RANKING...'), findsOneWidget);
      await select(tester, 'ranking-category', 'Defensores');
      stale.complete(
        Fixture.response({
          'rows': [player('g', 'Goleiro atrasado')],
        }),
      );
      await tester.pumpAndSettle();
      expect(inTable('ranking-table', 'Defensor atual'), findsOneWidget);
      expect(find.text('Goleiro atrasado'), findsNothing);
    },
  );

  testWidgets(
    '009 player click shows exact ARTILHEIRO: 5 VEZES, five awards and historic divisions after transfer',
    (tester) async {
      final f = Fixture();
      f.history = {
        'player': player('p1', 'Carlos Silva*'),
        'career': player('p1', 'Carlos Silva*', matches: 50, goals: 30),
        'statistics': [
          statistics(
            2028,
            'League',
            'QuartaDivisao',
            'old-club',
            matches: 20,
            goals: 12,
          ),
          statistics(
            2029,
            'League',
            'PrimeiraDivisao',
            'new-club',
            matches: 20,
            goals: 13,
          ),
          statistics(
            2029,
            'CupPreliminary1',
            null,
            'new-club',
            matches: 3,
            goals: 2,
          ),
          statistics(
            2029,
            'CupFinal',
            null,
            'new-club',
            matches: 1,
            goals: 3,
            incomplete: true,
          ),
        ],
        'awards': [
          for (final s in [2028, 2029, 2030, 2032, 2035]) award(s),
        ],
        'coverageWarnings': ['Minutos legados parciais'],
      };
      await openRankings(tester, f);
      await tester.tap(inTable('ranking-table', 'Carlos Silva*'));
      await tester.pumpAndSettle();
      expect(f.requests.last.url.path, '/api/worlds/world/players/p1/history');
      expect(find.text('ARTILHEIRO: 5 VEZES'), findsOneWidget);
      expect(find.text('Comportamento: Fair Play'), findsOneWidget);
      expect(find.text('Posição: A | Força: 46'), findsOneWidget);
      expect(find.text('Craque (*): Sim (*)'), findsOneWidget);
      expect(find.text('Clube atual: Clube Atual'), findsOneWidget);
      expect(
        find.text('Total de jogos: 50 | Total de gols: 30'),
        findsOneWidget,
      );
      expect(
        inTable('player-competition-history', '4ª DIVISÃO'),
        findsOneWidget,
      );
      expect(
        inTable('player-competition-history', '1ª DIVISÃO'),
        findsOneWidget,
      );
      expect(inTable('player-competition-history', 'TAÇA'), findsOneWidget);
      expect(
        inTable('player-competition-history', '5'),
        findsOneWidget,
      ); // both cup phases combined
      expect(inTable('player-season-history', 'old-club'), findsOneWidget);
      expect(inTable('player-season-history', 'Incompleta'), findsOneWidget);
      expect(find.text('COBERTURA: Minutos legados parciais'), findsOneWidget);
      final table = tester.widget<DataTable>(
        find.byKey(const ValueKey('player-award-history')),
      );
      expect(table.rows, hasLength(5));
      for (final s in [2028, 2029, 2030, 2032, 2035]) {
        expect(inTable('player-award-history', '$s'), findsOneWidget);
      }
      expect(
        inTable('player-award-history', 'Clube no Fechamento'),
        findsNWidgets(5),
      );
    },
  );

  testWidgets(
    '009 season final displays every tied winner, unequal matches, 3 million each and 6 million club total',
    (tester) async {
      final f = Fixture()
        ..awards = [
          award(2035),
          award(2035, id: 'p2', name: 'João Pereira*', matches: 40),
        ]
        ..clubTotals = [
          {
            'clubId': 'club-close',
            'clubName': 'Clube no Fechamento',
            'amount': 6000000,
          },
        ];
      await tester.pumpWidget(
        app(
          Builder(
            builder: (context) => TextButton(
              onPressed: () => showSeasonTopScorersDialog(
                context: context,
                apiClient: f.api,
                worldId: 'world',
                season: 2035,
              ),
              child: const Text('FINAL'),
            ),
          ),
        ),
      );
      await tester.tap(find.text('FINAL'));
      await tester.pumpAndSettle();
      expect(find.text('ENCERRAMENTO DA TEMPORADA 2035'), findsOneWidget);
      expect(find.text('ARTILHEIROS DA TEMPORADA 2035'), findsOneWidget);
      expect(inTable('season-scorer-awards', 'Carlos Silva*'), findsOneWidget);
      expect(inTable('season-scorer-awards', 'João Pereira*'), findsOneWidget);
      expect(inTable('season-scorer-awards', '29'), findsNWidgets(2));
      expect(inTable('season-scorer-awards', '30'), findsOneWidget);
      expect(inTable('season-scorer-awards', '40'), findsOneWidget);
      expect(inTable('season-scorer-awards', 'R\$ 3.000.000'), findsNWidgets(2));
      expect(inTable('season-club-awards', 'R\$ 6.000.000'), findsOneWidget);
      expect(f.requests.single.url.queryParameters, {'season': '2035'});
      expect(f.requests.every((r) => r.method == 'GET'), isTrue);
    },
  );

  testWidgets(
    '009 unassigned scorer keeps the title and no invented club credit',
    (tester) async {
      final f = Fixture()..awards = [award(2035, clubId: null)];
      await openRankings(tester, f);
      expect(inTable('season-scorer-awards', 'Sem clube'), findsOneWidget);
      expect(inTable('season-scorer-awards', 'R\$ 0'), findsOneWidget);
      expect(find.byKey(const ValueKey('season-club-awards')), findsNothing);
    },
  );

  testWidgets(
    '009 history can retry an immediate HTTP failure without losing the player identity',
    (tester) async {
      final f = Fixture()..historyFailures = 1;
      await openRankings(tester, f);
      await tester.tap(inTable('ranking-table', 'Carlos Silva*'));
      await tester.pumpAndSettle();
      expect(
        find.text('Não foi possível carregar histórico. Tente novamente.'),
        findsOneWidget,
      );
      expect(find.text('ARTILHEIRO: 5 VEZES'), findsNothing);
      await tester.tap(find.text('TENTAR NOVAMENTE'));
      await tester.pumpAndSettle();
      expect(find.text('ARTILHEIRO: 5 VEZES'), findsOneWidget);
      expect(
        f.requests.where((r) => r.url.path.endsWith('/players/p1/history')),
        hasLength(2),
      );
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    '009 award errors retry independently without showing a false empty season',
    (tester) async {
      final f = Fixture()
        ..awardFailures = 1
        ..awards = [award(2035)];
      await openRankings(tester, f);
      expect(
        find.text('Não foi possível carregar premiações. Tente novamente.'),
        findsOneWidget,
      );
      expect(find.textContaining('Nenhuma temporada encerrada'), findsNothing);
      await tester.ensureVisible(find.text('TENTAR NOVAMENTE'));
      await tester.tap(find.text('TENTAR NOVAMENTE'));
      await tester.pumpAndSettle();
      expect(inTable('season-scorer-awards', 'Carlos Silva*'), findsOneWidget);
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    '009 390 pixels: filters, horizontal rankings, player history and awards fit',
    (tester) async {
      final f = Fixture()..awards = [award(2035)];
      f.history = {
        'player': f.rows.first,
        'career': f.rows.first,
        'statistics': [statistics(2035, 'League', 'QuartaDivisao', 'old-club')],
        'awards': [
          for (final s in [2028, 2029, 2030, 2032, 2035]) award(s),
        ],
        'coverageWarnings': [],
      };
      await openRankings(tester, f, width: 390);
      await select(tester, 'ranking-category', 'Defensores');
      final horizontal = find.byKey(const ValueKey('ranking-table-horizontal'));
      await tester.ensureVisible(horizontal);
      await tester.drag(horizontal, const Offset(-600, 0));
      await tester.pumpAndSettle();
      final scrollable = tester.state<ScrollableState>(
        find
            .descendant(of: horizontal, matching: find.byType(Scrollable))
            .first,
      );
      expect(scrollable.position.pixels, greaterThan(0));
      expect(tester.takeException(), isNull);
      await tester.drag(horizontal, const Offset(900, 0));
      await tester.pumpAndSettle();
      await tester.tap(inTable('ranking-table', 'Carlos Silva*'));
      await tester.pumpAndSettle();
      expect(find.text('ARTILHEIRO: 5 VEZES'), findsOneWidget);
      expect(tester.takeException(), isNull);
      final historyTable = find.byKey(
        const ValueKey('player-award-history-horizontal'),
      );
      await tester.ensureVisible(historyTable);
      await tester.drag(historyTable, const Offset(-600, 0));
      await tester.pumpAndSettle();
      expect(tester.takeException(), isNull);
      await tester.tap(find.text('FECHAR'));
      await tester.pumpAndSettle();
      await tester.ensureVisible(
        find.byKey(const ValueKey('season-scorer-awards-horizontal')),
      );
      await tester.drag(
        find.byKey(const ValueKey('season-scorer-awards-horizontal')),
        const Offset(-600, 0),
      );
      await tester.pumpAndSettle();
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    '009 shell menu preserves career008 and opens the season final once after rollover',
    (tester) async {
      final f = Fixture();
      tester.view.physicalSize = const Size(1400, 900);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      await tester.pumpWidget(ElifootClassicApp(apiClient: f.api));
      await tester.tap(find.text('ENTRAR NO LOBBY (LOCAL DEV)'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('ENTRAR DESEMPREGADO'));
      await tester.pumpAndSettle();
      await tester.ensureVisible(find.text('5. RANKING & CARREIRA'));
      await tester.tap(find.text('5. RANKING & CARREIRA'));
      await tester.pumpAndSettle();
      expect(find.text('Técnico 008'), findsOneWidget);
      expect(find.text('12.5'), findsOneWidget);
      expect(find.text('MORAL: 70/100 — VERDE'), findsNWidgets(2));
      await tester.ensureVisible(find.text('7. RANKINGS DE JOGADORES'));
      await tester.tap(find.text('7. RANKINGS DE JOGADORES'));
      await tester.pumpAndSettle();
      expect(find.byKey(const ValueKey('ranking-table')), findsOneWidget);
      f.awards = [award(2036)];
      f.season = 2037;
      f.version++;
      f.api.onSnapshotUpdated!(f.snapshot);
      await tester.pumpAndSettle();
      expect(
        find.byKey(const ValueKey('season-end-awards-dialog')),
        findsOneWidget,
      );
      expect(find.text('ENCERRAMENTO DA TEMPORADA 2036'), findsOneWidget);
      await tester.tap(find.text('FECHAR'));
      await tester.pumpAndSettle();
      f.api.onSnapshotUpdated!(f.snapshot);
      await tester.pumpAndSettle();
      expect(
        find.byKey(const ValueKey('season-end-awards-dialog')),
        findsNothing,
      );
      expect(
        f.requests.every(
          (r) =>
              r.method == 'GET' ||
              r.url.path.endsWith('/join') ||
              r.url.path.endsWith('/local-login'),
        ),
        isTrue,
      );
    },
  );
}
