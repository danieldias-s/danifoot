import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:elifoot_flutter/api_client.dart';
import 'package:elifoot_flutter/classic_layout.dart' show ClassicTacticalPitch;
import 'package:elifoot_flutter/match_interaction_dialog.dart';
import 'package:elifoot_flutter/main.dart';
import 'package:elifoot_flutter/presentation_version.dart';
import 'package:elifoot_flutter/presentation_widgets.dart';
import 'classic_ux_014b_test.dart' show WaitingRoomApi, join;

class PenaltyRoomApi extends WaitingRoomApi {
  final bool mine;
  bool pending = true;
  String? chosen;
  String? outcome;
  bool automatic = false;
  bool otherFixture = false;
  PenaltyRoomApi(this.mine) : super(2) {
    currentRoundState = 'RUNNING_FIRST_HALF';
  }
  @override
  Future<Map<String, dynamic>> getSnapshot(String world) async {
    final snap = await super.getSnapshot(world);
    final m = (snap['matches'] as List).first;
    final other = {
      ...m as Map<String, dynamic>,
      'matchId': 'match-other',
      'homeClubId': 'club-3',
      'awayClubId': 'club-4',
      'events': <Map<String, dynamic>>[],
    };
    if (otherFixture) snap['matches'] = [m, other];
    if (pending) {
      snap['pendingPenalty'] = {
        'decisionId': 'decision-37',
        'matchId': otherFixture ? other['matchId'] : m['matchId'],
        'coachId': mine ? 'coach-1' : 'other-1',
        'coachName': mine ? 'Treinador 1' : 'Treinador 2',
        'clubId': otherFixture
            ? 'club-4'
            : mine
            ? 'club-1'
            : 'club-2',
        'clubName': mine ? 'Atlético Verde' : 'Real Dourado',
        'minute': 42,
        'eligiblePlayers': (m['homeStarters'] as List)
            .map(
              (p) => {
                'playerId': p['playerId'],
                'displayName': p['displayName'],
                'position': p['position'],
                'strength': p['strength'],
                'isStar': false,
              },
            )
            .toList(),
      };
    } else if (outcome != null) {
      ((otherFixture ? other : m)['events'] as List).add({
        'type': outcome == 'Goal' ? 'Goal' : 'PenaltyMissed',
        'minute': 42,
        'clubId': otherFixture
            ? 'club-4'
            : mine
            ? 'club-1'
            : 'club-2',
        'clubName': mine ? 'Atlético Verde' : 'Real Dourado',
        'description': 'Cobrança de pênalti',
        'penaltyKick': {
          'decisionId': automatic ? null : 'decision-37',
          'outcome': outcome,
          'homeGoals': 3,
          'awayGoals': 1,
          'taker': {
            'playerId': 'p-2',
            'displayName': 'Titular 2',
            'position': 'D',
            'strength': 43,
            'isStar': false,
          },
        },
      });
    }
    return snap;
  }

  @override
  Future<Map<String, dynamic>> choosePenaltyTaker(
    String world,
    String coach,
    String decision,
    String player,
  ) async {
    chosen = player;
    pending = false;
    roundVersion++;
    return getSnapshot(world);
  }
}

class SubstitutionFormationApi extends WaitingRoomApi {
  final List<String> _active = [
    'p-1',
    'p-2',
    'p-3',
    'p-4',
    'p-5',
    'p-6',
    'p-7',
    'p-8',
    'p-9',
    'p-10',
    'p-11',
  ];
  final List<String> _bench = ['p-12', 'p-13', 'p-14'];

  SubstitutionFormationApi() : super(2) {
    currentRoundState = 'HALFTIME_PAUSED';
    liveFormation = '5-4-1';
  }

  Map<String, dynamic> _player(String id) {
    final n = int.parse(id.substring(2));
    return {
      'playerId': id,
      'displayName': 'Jogador $n',
      'strength': 25,
      'position': n == 1 || n == 12
          ? 'G'
          : n <= 6
          ? 'D'
          : n <= 10
          ? 'M'
          : 'A',
    };
  }

  @override
  Future<Map<String, dynamic>> getSnapshot(String world) async {
    final state = await super.getSnapshot(world);
    final match = (state['matches'] as List).first;
    match['homeStarters'] = _active.map(_player).toList();
    match['homeBench'] = _bench.map(_player).toList();
    return state;
  }

  @override
  Future<Map<String, dynamic>> submitInMatchAdjustments({
    required String worldId,
    required String coachId,
    required String matchId,
    String? newFormation,
    required List<Map<String, String>> substitutions,
  }) {
    expect(newFormation, isNull);
    final swap = substitutions.single;
    final incoming = swap['playerInId']!;
    final outgoing = swap['playerOutId']!;
    _active[_active.indexOf(outgoing)] = incoming;
    _bench.remove(incoming);
    liveFormation = usedSubs == 0 ? '4-4-2' : '3-4-3';
    return super.submitInMatchAdjustments(
      worldId: worldId,
      coachId: coachId,
      matchId: matchId,
      substitutions: substitutions,
    );
  }
}

class FinishedClubPauseApi extends WaitingRoomApi {
  FinishedClubPauseApi() : super(2) {
    currentRoundState = 'EXTRA_TIME_BREAK';
  }
  @override
  Future<Map<String, dynamic>> getSnapshot(String world) async {
    final snapshot = await super.getSnapshot(world);
    snapshot['activePauseWindow'] = {
      'pauseWindowId': 'extra-time-other-clubs',
      'pauseType': 'EXTRA_TIME_BREAK',
      'minute': 90,
      'confirmedCoachIds': <String>[],
      'editedCoachIds': <String>[],
      'requestedByCoachName': 'Outro treinador',
      'confirmedCount': 0,
      'totalHumans': 2,
    };
    for (final match in snapshot['matches'] as List) {
      match['isFinished'] = true;
    }
    return snapshot;
  }
}

void main() {
  Future<void> joinClassic(
    WidgetTester tester,
    WaitingRoomApi api, {
    double width = 320,
  }) async {
    tester.view.physicalSize = Size(width, 950);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    await join(tester, api);
    tester.view.physicalSize = Size(width, 600);
    await PresentationScope.maybeOf(
      tester.element(find.byType(ElifootShellScreen)),
    )!.select(api, PresentationVersion.classic);
    await tester.pumpAndSettle();
  }

  Future<void> press(WidgetTester tester, String key) async {
    final button = find.byKey(ValueKey(key));
    await tester.ensureVisible(button);
    await tester.tap(button);
    await tester.pumpAndSettle();
  }

  testWidgets(
    'Classic penalty shows the chosen taker and score before closing',
    (tester) async {
      final api = PenaltyRoomApi(true)..outcome = 'Goal';
      await joinClassic(tester, api);
      expect(find.text('EM CAMPO (11)'), findsOneWidget);
      await press(tester, 'penalty-taker-p-2');
      await press(tester, 'announce-penalty-taker');
      expect(find.text('Titular 2 vai cobrar.'), findsOneWidget);
      expect(api.chosen, isNull);
      await press(tester, 'confirm-penalty-taker');
      expect(api.chosen, 'p-2');
      expect(find.text('GOL!'), findsOneWidget);
      expect(find.text('3 x 1'), findsOneWidget);
      await press(tester, 'acknowledge-penalty-result');
      await api.emit('RUNNING_FIRST_HALF');
      await tester.pumpAndSettle();
      expect(find.byKey(const Key('penalty-result-dialog')), findsNothing);
      expect(tester.takeException(), isNull);
    },
  );

  for (final automatic in [false, true]) {
    testWidgets(
      'Classic observer sees waiting, taker and outcome (AI: $automatic)',
      (tester) async {
        final api = PenaltyRoomApi(false)
          ..automatic = automatic
          ..pending = false;
        await joinClassic(tester, api);
        if (!automatic) {
          api
            ..otherFixture = true
            ..pending = true;
          await api.emit('RUNNING_FIRST_HALF');
          await tester.pumpAndSettle();
          expect(find.text('AGUARDE'), findsOneWidget);
        }
        expect(find.byKey(const Key('confirm-penalty-taker')), findsNothing);
        api
          ..pending = false
          ..outcome = automatic ? 'Wide' : 'Saved';
        await api.emit('RUNNING_FIRST_HALF');
        await tester.pump();
        if (automatic) {
          expect(find.text('AGUARDE'), findsOneWidget);
          await tester.pump(const Duration(milliseconds: 700));
        }
        expect(find.text('Titular 2 vai cobrar.'), findsOneWidget);
        await tester.pump(const Duration(milliseconds: 1450));
        expect(
          find.text(automatic ? 'PRA FORA!' : 'DEFENDEU!'),
          findsOneWidget,
        );
        await press(tester, 'acknowledge-penalty-result');
        expect(api.chosen, isNull);
        expect(tester.takeException(), isNull);
      },
    );
  }

  testWidgets('No penalty modal for a CPU fixture, even when selected', (
    tester,
  ) async {
    final api = PenaltyRoomApi(false)
      ..pending = false
      ..automatic = true;
    await joinClassic(tester, api);
    api.otherFixture = true;
    await api.emit('RUNNING_FIRST_HALF');
    await tester.pumpAndSettle();
    await press(tester, 'select-match-match-other');
    api.outcome = 'Goal';
    await api.emit('RUNNING_FIRST_HALF');
    await tester.pumpAndSettle();
    expect(find.byKey(const Key('penalty-waiting-dialog')), findsNothing);
    expect(find.byKey(const Key('penalty-taker-dialog')), findsNothing);
    expect(find.byKey(const Key('penalty-result-dialog')), findsNothing);
    expect(
      tester
          .widget<Tooltip>(
            find.byKey(const Key('classic-last-event-match-other-away')),
          )
          .message,
      contains('Pênalti convertido! Titular 2'),
    );
  });

  testWidgets('Substitution previews and refreshes 5-4-1 to 4-4-2 to 3-4-3', (
    tester,
  ) async {
    final api = SubstitutionFormationApi();
    await joinClassic(tester, api, width: 1150);
    ClassicTacticalPitch pitch() => tester.widget<ClassicTacticalPitch>(
      find.descendant(
        of: find.byType(MatchTeamTacticalPitch),
        matching: find.byType(ClassicTacticalPitch),
      ),
    );
    List<int> lines() => pitch().lines.map((line) => line.length).toList();
    expect(lines(), [1, 4, 5, 1]);
    for (final (out, incoming, shape) in [
      ('p-2', 'p-13', [2, 4, 4, 1]),
      ('p-3', 'p-14', [3, 4, 3, 1]),
    ]) {
      await press(tester, 'sub-select-out-$out');
      await press(tester, 'sub-select-in-$incoming');
      expect(lines(), shape);
      expect(pitch().lines.first.map((p) => p['playerId']), contains(incoming));
      expect(
        pitch().lines.expand((line) => line.map((p) => p['playerId'])),
        isNot(contains(out)),
      );
      await press(tester, 'btn-aplicar-ajuste-janela');
      expect(lines(), shape);
    }
    expect(api.usedSubs, 2);
    expect(tester.takeException(), isNull);
  });

  testWidgets(
    'Finished club observes another clubs extra-time break without edit controls',
    (tester) async {
      tester.view.physicalSize = const Size(390, 950);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      await join(tester, FinishedClubPauseApi());
      expect(find.byType(PauseWaitingContent), findsOneWidget);
      expect(find.byType(SubstitutionChoicePanel), findsNothing);
      expect(find.byKey(const Key('btn-confirmar-continuar')), findsOneWidget);
      expect(tester.takeException(), isNull);
    },
  );
  for (final width in [320.0, 390.0, 768.0, 1440.0]) {
    testWidgets(
      'Human chooses a real on-pitch taker, waiting until CHUTAR at $width',
      (tester) async {
        tester.view.physicalSize = Size(width, 950);
        tester.view.devicePixelRatio = 1;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);
        final api = PenaltyRoomApi(true);
        await join(tester, api);
        expect(find.byKey(const Key('penalty-choice-dialog')), findsOneWidget);
        expect(find.byKey(const Key('penalty-waiting-dialog')), findsNothing);
        final shoot = find.byKey(const Key('confirm-penalty-taker'));
        expect(tester.widget<OutlinedButton>(shoot).onPressed, isNull);
        expect(api.chosen, isNull);
        final taker = find.byKey(const ValueKey('penalty-taker-p-2'));
        await tester.ensureVisible(taker);
        await tester.tap(taker);
        await tester.pumpAndSettle();
        expect(api.chosen, isNull);
        await tester.ensureVisible(shoot);
        await tester.tap(shoot);
        await tester.pumpAndSettle();
        expect(api.chosen, 'p-2');
        expect(find.byType(PenaltyChoiceDialog), findsNothing);
        expect(tester.takeException(), isNull);
      },
    );
    testWidgets(
      'Other coaches see only AGUARDE for a human penalty at $width',
      (tester) async {
        tester.view.physicalSize = Size(width, 950);
        tester.view.devicePixelRatio = 1;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);
        final api = PenaltyRoomApi(false);
        await join(tester, api);
        expect(find.byKey(const Key('penalty-waiting-dialog')), findsOneWidget);
        expect(find.text('AGUARDE'), findsOneWidget);
        expect(find.byKey(const ValueKey('penalty-taker-p-2')), findsNothing);
        expect(find.byKey(const Key('confirm-penalty-taker')), findsNothing);
        expect(api.chosen, isNull);
        expect(tester.takeException(), isNull);
      },
    );
    testWidgets(
      'Easy Substituir opens shared two-list dialog, opening is free, actual substitution counts at $width',
      (tester) async {
        tester.view.physicalSize = Size(width, 950);
        tester.view.devicePixelRatio = 1;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);
        final api = WaitingRoomApi(2)..currentRoundState = 'RUNNING_FIRST_HALF';
        await join(tester, api);
        final button = find.byKey(
          Key(
            width >= 1000
                ? 'live-substitute-desktop'
                : 'live-substitute-mobile',
          ),
        );
        await tester.ensureVisible(button);
        await tester.tap(button);
        await tester.pumpAndSettle();
        expect(
          find.byKey(const Key('shared-substitution-dialog')),
          findsOneWidget,
        );
        expect(api.usedPauses, 0);
        expect(api.usedSubs, 0);
        final apply = find.byKey(const ValueKey('btn-aplicar-ajuste-janela'));
        expect(tester.widget<OutlinedButton>(apply).onPressed, isNull);
        for (final key in ['sub-select-out-p-2', 'sub-select-in-p-13']) {
          final choice = find.byKey(ValueKey(key));
          await tester.ensureVisible(choice);
          await tester.tap(choice);
          await tester.pumpAndSettle();
        }
        await tester.ensureVisible(apply);
        await tester.tap(apply);
        await tester.pumpAndSettle();
        expect(api.usedSubs, 1);
        expect(api.usedPauses, 1);
        expect(find.textContaining('RESTANTES: 4'), findsOneWidget);
        final next = find.byKey(const Key('btn-confirmar-continuar'));
        await tester.ensureVisible(next);
        await tester.tap(next);
        await tester.pumpAndSettle();
        expect(
          find.byKey(const Key('shared-substitution-dialog')),
          findsNothing,
        );
        expect(tester.takeException(), isNull);
      },
    );
  }
  testWidgets(
    'Halftime substitution counts once but consumes no tactical opportunity',
    (tester) async {
      tester.view.physicalSize = const Size(390, 950);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      final api = WaitingRoomApi(2)..currentRoundState = 'HALFTIME_PAUSED';
      await join(tester, api);
      expect(
        find.textContaining('INTERVALO — alterações não gastam parada.'),
        findsOneWidget,
      );
      for (final key in ['sub-select-out-p-2', 'sub-select-in-p-13']) {
        final choice = find.byKey(ValueKey(key));
        await tester.ensureVisible(choice);
        await tester.tap(choice);
        await tester.pumpAndSettle();
      }
      final apply = find.byKey(const ValueKey('btn-aplicar-ajuste-janela'));
      await tester.ensureVisible(apply);
      await tester.tap(apply);
      await tester.pumpAndSettle();
      expect(api.usedSubs, 1);
      expect(api.usedPauses, 0);
      expect(tester.takeException(), isNull);
    },
  );
  test(
    'Penalty choice HTTP body uses actual decision/player and stable idempotency for retry',
    () async {
      final bodies = <Map>[];
      final api = ElifootApiClient(
        baseUrl: 'http://local',
        httpClient: MockClient((r) async {
          expect(r.url.path, '/api/worlds/w/penalty-taker');
          bodies.add(jsonDecode(r.body) as Map);
          return http.Response('{}', 200);
        }),
      );
      await api.choosePenaltyTaker('w', 'coach', 'decision', 'player');
      await api.choosePenaltyTaker('w', 'coach', 'decision', 'player');
      expect(bodies[0]['playerId'], 'player');
      expect(bodies[0]['decisionId'], 'decision');
      expect(bodies[0]['idempotencyKey'], bodies[1]['idempotencyKey']);
    },
  );
}
