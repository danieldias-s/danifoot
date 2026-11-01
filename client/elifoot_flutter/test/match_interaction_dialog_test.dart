import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:elifoot_flutter/api_client.dart';
import 'package:elifoot_flutter/match_interaction_dialog.dart';
import 'classic_ux_014b_test.dart' show WaitingRoomApi, join;

class PenaltyRoomApi extends WaitingRoomApi {
  final bool mine;
  bool pending = true;
  String? chosen;
  PenaltyRoomApi(this.mine) : super(2) {
    currentRoundState = 'RUNNING_FIRST_HALF';
  }
  @override
  Future<Map<String, dynamic>> getSnapshot(String world) async {
    final snap = await super.getSnapshot(world);
    if (pending) {
      final m = (snap['matches'] as List).first;
      snap['pendingPenalty'] = {
        'decisionId': 'decision-37',
        'matchId': m['matchId'],
        'coachId': mine ? 'coach-1' : 'other-1',
        'coachName': mine ? 'Treinador 1' : 'Treinador 2',
        'clubId': mine ? 'club-1' : 'club-2',
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
