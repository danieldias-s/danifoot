import 'package:elifoot_flutter/main.dart';
import 'package:elifoot_flutter/round_navigation.dart';
import 'package:elifoot_flutter/classic_match_board.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'complete_gameplay_011_test.dart' show FakeGameplay011ApiClient;

class WaitingRoomApi extends FakeGameplay011ApiClient {
  final int humans;
  int ready = 0;
  WaitingRoomApi(this.humans);
  @override
  Future<Map<String, dynamic>> getSnapshot(String worldId) async {
    final snap = await super.getSnapshot(worldId);
    snap['gameWorldId'] = worldId;
    snap['competitionType'] = 'League';
    snap['readyCounter'] = {
      'gameWorldId': worldId,
      'roundVersion': roundVersion,
      'readyCount': ready,
      'totalHumans': humans,
      'counterLabel': 'TÁTICAS ENVIADAS: $ready/$humans',
      'participants': List.generate(
        humans,
        (i) => {
          'coachId': i == 0 ? 'coach-1' : 'other-$i',
          'nickname': 'Treinador ${i + 1}',
          'clubId': i == 0 ? myClubId : 'club-${i + 1}',
          'clubName': i == 0 ? myClubName : 'Clube ${i + 1}',
          'isUnemployed': i == 0 && isUnemployed,
          'isReady': i < ready,
        },
      ),
    };
    if (!hasJoined) {
      (snap['readyCounter']['participants'] as List).removeWhere(
        (p) => p['coachId'] == 'coach-1',
      );
    }
    for (final m in snap['matches'] as List) {
      m['competitionType'] = 'League';
      m['divisionTier'] = 'PrimeiraDivisao';
    }
    return snap;
  }

  Future<Map<String, dynamic>> _ready(String worldId) async {
    isReady = true;
    ready = 1;
    roundVersion++;
    // A stale/optimistic HTTP flag is insufficient: the snapshot is PREPARING.
    return {
      'roundStarted': true,
      'counter': (await getSnapshot(worldId))['readyCounter'],
    };
  }

  @override
  Future<Map<String, dynamic>> submitTactics({
    required String worldId,
    required String coachId,
    required String formation,
    required List<String> starterIds,
    required List<String> benchIds,
    required int roundVersion,
  }) => _ready(worldId);
  @override
  Future<Map<String, dynamic>> submitUnemployedReady({
    required String worldId,
    required String coachId,
    required int roundVersion,
  }) => _ready(worldId);
  Future<void> emit(String stage) async {
    currentRoundState = stage;
    roundVersion++;
    onSnapshotUpdated?.call(await getSnapshot('world-011'));
  }
}

Future<void> join(
  WidgetTester tester,
  WaitingRoomApi api, {
  bool unemployed = false,
}) async {
  await tester.pumpWidget(
    ElifootClassicApp(accountLogin: false, apiClient: api),
  );
  await tester.ensureVisible(find.text('ENTRAR NO LOBBY'));
  await tester.tap(find.text('ENTRAR NO LOBBY'));
  await tester.pumpAndSettle();
  final enterRoom = find.text(
    unemployed ? 'ENTRAR DESEMPREGADO' : 'ENTRAR COM CLUBE',
  );
  await tester.ensureVisible(enterRoom);
  await tester.pumpAndSettle();
  await tester.tap(enterRoom);
  await tester.pumpAndSettle();
  if (!unemployed) {
    await tester.tap(find.byKey(const Key('choose-club-club-1')));
    await tester.pumpAndSettle();
  }
}

Map<String, dynamic> snap(
  String state, {
  int round = 1,
  int ready = 0,
  int total = 0,
  String? pause,
}) => {
  'gameWorldId': 'w',
  'seasonNumber': 1,
  'roundNumber': round,
  'roundState': state,
  'readyCounter': {'readyCount': ready, 'totalHumans': total},
  'activePauseWindow': pause == null ? null : {'pauseWindowId': pause},
};
void main() {
  test(
    'Event labels follow actual eventType, including goals for Palestra',
    () {
      expect(
        classicEventBadge({
          'eventType': 'Goal',
          'description': 'GOOOL! Palestra Verde',
        }),
        '[GOL]',
      );
      expect(
        classicEventBadge({
          'eventType': 'Injury',
          'description': 'Atleta saiu',
        }),
        '[LESÃO]',
      );
      expect(classicEventBadge({'eventType': 'DirectRedCard'}), '[EXPULSÃO]');
      expect(classicEventBadge({'eventType': 'Substitution'}), '[SUB]');
      expect(classicEventBadge({'eventType': 'Offside'}), '[IMPEDIMENTO]');
      expect(
        classicEventBadge({'eventType': 'PenaltyShootoutGoal'}),
        '[GOL NOS PÊNALTIS]',
      );
      expect(classicEventBadge({'eventType': 0}), '[GOL]');
      expect(classicEventBadge({'type': 'Goal'}), '[GOL]');
      expect(
        classicEventBadge({'description': 'Palestra sem evento tipado'}),
        '[LANCE]',
      );
    },
  );

  test(
    'Only authoritative started snapshots open matches, once per transition',
    () {
      final nav = RoundViewCoordinator();
      expect(nav.accept(snap('PREPARING')), RoundViewDecision.tactics);
      expect(
        nav.accept(snap('PREPARING', ready: 8, total: 8)),
        RoundViewDecision.none,
      );
      expect(
        nav.accept(snap('ALL_READY', ready: 8, total: 8)),
        RoundViewDecision.none,
      );
      expect(nav.accept(snap('RUNNING_FIRST_HALF')), RoundViewDecision.match);
      for (int i = 0; i < 100; i++) {
        expect(nav.accept(snap('RUNNING_FIRST_HALF')), RoundViewDecision.none);
      }
      expect(
        nav.accept(snap('GLOBAL_PAUSED', pause: 'p1')),
        RoundViewDecision.match,
      );
      expect(
        nav.accept(snap('GLOBAL_PAUSED', pause: 'p1')),
        RoundViewDecision.none,
      );
      expect(nav.accept(snap('RUNNING_SECOND_HALF')), RoundViewDecision.none);
      expect(nav.accept(snap('RESULTS')), RoundViewDecision.match);
      expect(nav.accept(snap('RESULTS')), RoundViewDecision.none);
      expect(
        nav.accept(snap('PREPARING', round: 2)),
        RoundViewDecision.tactics,
      );
    },
  );
  test('Switching worlds and rejoining restore an already running match', () {
    final nav = RoundViewCoordinator();
    expect(nav.accept(snap('RUNNING_FIRST_HALF')), RoundViewDecision.match);
    expect(
      nav.accept({...snap('PREPARING'), 'gameWorldId': 'another'}),
      RoundViewDecision.tactics,
    );
    expect(nav.accept(snap('RUNNING_FIRST_HALF')), RoundViewDecision.match);
    nav.reset();
    expect(nav.accept(snap('RUNNING_FIRST_HALF')), RoundViewDecision.match);
  });
  for (final state in [
    'RUNNING_FIRST_HALF',
    'HALFTIME_PAUSED',
    'RUNNING_SECOND_HALF',
    'GLOBAL_PAUSED',
    'EXTRA_TIME_BREAK',
    'RUNNING_EXTRA_TIME',
    'EXTRA_TIME_HALFTIME',
    'RESULTS',
    'FINISHED',
  ]) {
    test(
      'Refresh/reconnect restores $state from snapshot',
      () => expect(
        RoundViewCoordinator().accept(snap(state)),
        RoundViewDecision.match,
      ),
    );
  }
  for (final n in [2, 4, 6, 8]) {
    testWidgets(
      '$n coaches wait for server start, navigate automatically and block menus until results',
      (tester) async {
        await tester.binding.setSurfaceSize(const Size(1280, 950));
        addTearDown(() => tester.binding.setSurfaceSize(null));
        final api = WaitingRoomApi(n);
        await join(tester, api);
        expect(find.text('2. JOGOS AO VIVO'), findsNothing);
        expect(find.textContaining('EDITOR & PACOTES'), findsNothing);
        final send = find.byKey(const Key('btn-enviar-tatica'));
        await tester.ensureVisible(send);
        await tester.tap(send);
        await tester.pumpAndSettle();
        expect(api.ready, 1);
        api.onReadyCounterUpdated?.call({
          'gameWorldId': 'foreign-room',
          'roundVersion': 99999,
          'readyCount': n,
          'totalHumans': n,
          'counterLabel': 'CONTADOR DE OUTRA SALA',
          'participants': [],
        });
        await tester.pumpAndSettle();
        expect(find.text('CONTADOR DE OUTRA SALA'), findsNothing);
        expect(
          find.byKey(const ValueKey('ready-waiting-panel')),
          findsOneWidget,
        );
        expect(
          find.textContaining('AGUARDANDO OUTROS TREINADORES'),
          findsWidgets,
        );
        expect(find.byType(ClassicMatchBoard), findsNothing);
        await tester.ensureVisible(find.text('4. FINANÇAS & ESTÁDIO'));
        await tester.tap(find.text('4. FINANÇAS & ESTÁDIO'));
        await tester.pumpAndSettle();
        api.ready = n;
        await api.emit('RUNNING_FIRST_HALF');
        await tester.pumpAndSettle();
        expect(find.byType(ClassicMatchBoard), findsOneWidget);
        await tester.ensureVisible(find.byKey(const ValueKey('shell-nav-2')));
        await tester.tap(find.byKey(const ValueKey('shell-nav-2')));
        await tester.pumpAndSettle();
        await api.emit('RUNNING_FIRST_HALF');
        await tester.pumpAndSettle();
        expect(find.byType(ClassicMatchBoard), findsOneWidget);
        await api.emit('GLOBAL_PAUSED');
        await tester.pumpAndSettle();
        expect(find.byType(ClassicMatchBoard), findsOneWidget);
        expect(
          find.byKey(const ValueKey('in-match-substitution-panel')),
          findsOneWidget,
        );
        await api.emit('RESULTS');
        await tester.pumpAndSettle();
        expect(find.textContaining('PARTIDA FINALIZADA'), findsWidgets);
        api.currentRound = 2;
        api.ready = 0;
        api.isReady = false;
        await api.emit('PREPARING');
        await tester.pumpAndSettle();
        expect(find.byType(ClassicMatchBoard), findsNothing);
        expect(find.byKey(const ValueKey('coach-main-panel')), findsOneWidget);
        expect(tester.takeException(), isNull);
        await tester.pumpWidget(const SizedBox.shrink());
      },
    );
  }
  testWidgets('Unemployed coach waits and follows authoritative start', (
    tester,
  ) async {
    await tester.binding.setSurfaceSize(const Size(1280, 950));
    addTearDown(() => tester.binding.setSurfaceSize(null));
    final api = WaitingRoomApi(4);
    await join(tester, api, unemployed: true);
    await tester.tap(find.byKey(const Key('btn-enviar-tatica')));
    await tester.pumpAndSettle();
    expect(find.byKey(const ValueKey('ready-waiting-panel')), findsOneWidget);
    expect(find.byType(ClassicMatchBoard), findsNothing);
    api.ready = 4;
    await api.emit('RUNNING_FIRST_HALF');
    await tester.pumpAndSettle();
    expect(find.byType(ClassicMatchBoard), findsOneWidget);
    expect(tester.takeException(), isNull);
  });
  for (final width in [320.0, 390.0, 1366.0]) {
    testWidgets(
      'Classic board has four real fixtures per division, live scores and no overflow at $width',
      (tester) async {
        await tester.binding.setSurfaceSize(Size(width, 1200));
        addTearDown(() => tester.binding.setSurfaceSize(null));
        final matches = List.generate(
          16,
          (i) => <String, dynamic>{
            'matchId': 'm$i',
            'competitionType': 'League',
            'divisionTier': i ~/ 4 + 1,
            'homeClubId': 'h$i',
            'awayClubId': 'a$i',
            'homeClubName': 'Nome completo do clube mandante $i',
            'awayClubName': 'Nome completo do visitante $i',
            'homeGoals': i % 3,
            'awayGoals': 1,
            'minute': 34,
            'events': [],
          },
        );
        Future<void> render() => tester.pumpWidget(
          MaterialApp(
            theme: ThemeData.dark(),
            home: Scaffold(
              body: SingleChildScrollView(
                child: ClassicMatchBoard(
                  matches: matches,
                  competitionType: 'League',
                  competitionLabel: 'Campeonato',
                  coachClubId: 'h0',
                ),
              ),
            ),
          ),
        );
        await render();
        await tester.pumpAndSettle();
        for (int d = 1; d <= 4; d++) {
          expect(find.text('$dª DIVISÃO'), findsOneWidget);
        }
        expect(find.byType(ExpansionTile), findsNWidgets(16));
        expect(
          find.byKey(const ValueKey('coach-highlighted-match-card')),
          findsOneWidget,
        );
        final score = find.byKey(const ValueKey('score-m0'));
        final before = tester.getRect(score);
        expect(before.width, lessThanOrEqualTo(74));
        expect(before.center.dx, closeTo(width / 2 + 1, 25));
        matches[0]['homeGoals'] = 4;
        await render();
        await tester.pump();
        expect(find.text('4 - 1'), findsOneWidget);
        expect(tester.takeException(), isNull);
      },
    );
  }
  testWidgets('Cup group expansion does not open every match detail', (
    tester,
  ) async {
    final matches = List.generate(
      12,
      (i) => <String, dynamic>{
        'matchId': 'isolated-$i',
        'competitionType': 'CupPreliminary1',
        'homeClubId': 'h$i',
        'awayClubId': 'a$i',
        'homeClubName': 'Mandante $i',
        'awayClubName': 'Visitante $i',
        'events': [
          {'minute': 2, 'description': 'Evento específico $i'},
        ],
      },
    );
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: SingleChildScrollView(
            child: ClassicMatchBoard(
              matches: matches,
              competitionType: 'CupPreliminary1',
              competitionLabel: 'Taça - Preliminar 1',
            ),
          ),
        ),
      ),
    );
    await tester.tap(find.textContaining('12 JOGOS'));
    await tester.pumpAndSettle();
    expect(
      find.text("2' • [LANCE] Evento específico 0").hitTestable(),
      findsNothing,
    );
    await tester.tap(find.byKey(const ValueKey('match-details-isolated-0')));
    await tester.pumpAndSettle();
    expect(
      find.text("2' • [LANCE] Evento específico 0").hitTestable(),
      findsOneWidget,
    );
    expect(
      find.text("2' • [LANCE] Evento específico 1").hitTestable(),
      findsNothing,
    );
    expect(tester.takeException(), isNull);
  });
  testWidgets(
    'Cup preliminary keeps all district fixtures under actual cup stage',
    (tester) async {
      final matches = List.generate(
        48,
        (i) => <String, dynamic>{
          'matchId': 'cup$i',
          'competitionType': 'CupPreliminary1',
          'divisionTier': 'Distrital',
          'homeClubId': 'district$i',
          'awayClubId': 'other$i',
          'homeClubName': 'Distrital $i',
          'awayClubName': 'Outro $i',
          'events': [],
        },
      );
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: SingleChildScrollView(
              child: ClassicMatchBoard(
                matches: matches,
                competitionType: 'CupPreliminary1',
                competitionLabel: 'Taça - Preliminar 1',
                coachClubId: 'district0',
              ),
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();
      expect(find.textContaining('TAÇA - PRELIMINAR 1'), findsOneWidget);
      expect(find.text('DISTRITAIS'), findsNothing);
      expect(find.byType(ExpansionTile), findsNWidgets(49));
      expect(find.text('Distrital 47'), findsOneWidget);
      expect(tester.takeException(), isNull);
    },
  );
}
