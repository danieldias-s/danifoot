import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:elifoot_flutter/classic98_widgets.dart';
import 'package:elifoot_flutter/classic_live_round.dart';
import 'package:elifoot_flutter/live_match_data.dart';
import 'package:elifoot_flutter/live_match_view.dart';
import 'package:elifoot_flutter/main.dart';
import 'package:elifoot_flutter/match_interaction_dialog.dart';
import 'package:elifoot_flutter/presentation_version.dart';
import 'package:elifoot_flutter/presentation_widgets.dart';
import 'classic_ux_014b_test.dart' show WaitingRoomApi, join;
import 'live_match_screen_test.dart' as fixture;

List<Map<String, dynamic>> players(String id, {bool bench = false}) =>
    List.generate(
      bench ? 4 : 11,
      (i) => {
        'playerId': '$id-$i',
        'displayName': 'Jogador $id $i',
        'position': i == 0
            ? 'G'
            : i < 5
            ? 'D'
            : i < 9
            ? 'M'
            : 'A',
        'strength': 10 + i % 4,
        'isOnPitch': !bench,
      },
    );
Map<String, dynamic> classicSnapshot(int minute) {
  final snap = fixture.snapshot();
  snap['currentMinute'] = minute;
  for (final match in snap['matches'] as List) {
    match['minute'] = minute;
    for (final side in ['home', 'away']) {
      match['${side}Formation'] = '4-4-2';
      match['${side}Starters'] = players('${match['matchId']}-$side');
      match['${side}Bench'] = players(
        '${match['matchId']}-$side-bench',
        bench: true,
      );
    }
  }
  return snap;
}

Future<void> renderClassic(
  WidgetTester t,
  Map<String, dynamic> snap,
  LiveMatchSelection selection,
  VoidCallback onPause,
) => t.pumpWidget(
  MaterialApp(
    theme: classic98Theme(),
    home: Scaffold(
      body: LiveMatchScreen(
        snapshot: snap,
        calendar: const [],
        clubs: fixture.clubs(),
        coachId: 'coach-a',
        coachName: 'Treinador A',
        coachClubId: 'club-1',
        selection: selection,
        onSelectionChanged: () {},
        onSubstitute: onPause,
      ),
    ),
  ),
);

class ClubClickApi extends WaitingRoomApi {
  int pauses = 0;
  ClubClickApi() : super(2);
  @override
  Future<Map<String, dynamic>> getSnapshot(String world) async {
    final snap = await super.getSnapshot(world);
    final matches = List<dynamic>.from(snap['matches'] as List);
    snap['matches'] = matches;
    if (matches.isNotEmpty) {
      matches.add({
        ...Map<String, dynamic>.from(matches.first as Map),
        'matchId': 'foreign-live',
        'homeClubId': 'foreign-home',
        'awayClubId': 'foreign-away',
        'homeClubName': 'Outro clube',
        'awayClubName': 'Rival observado',
      });
    }
    return snap;
  }

  @override
  Future<Map<String, dynamic>> requestGlobalPause(
    String worldId,
    String coachId,
  ) {
    pauses++;
    return super.requestGlobalPause(worldId, coachId);
  }
}

void main() {
  test(
    'Last-event text uses official type and never invents actions or a past score',
    () {
      expect(classicLiveEventText(null), 'Sem lance registrado');
      expect(
        classicLiveEventText({'eventType': 'Goal', 'minute': 41}),
        'Gol! (41′)',
      );
      expect(
        classicLiveEventText({'eventType': 1, 'minute': 42}),
        'Cartão amarelo (42′)',
      );
      expect(
        classicLiveEventText({
          'eventType': 'Unknown',
          'minute': 43,
          'description': 'Lance oficial',
        }),
        'Lance oficial (43′)',
      );
    },
  );
  for (final width in [360.0, 390.0, 430.0, 768.0, 1366.0, 1920.0]) {
    testWidgets('Classic list and live team consultation at $width px', (
      t,
    ) async {
      t.view.physicalSize = Size(width, 1000);
      t.view.devicePixelRatio = 1;
      addTearDown(t.view.resetPhysicalSize);
      addTearDown(t.view.resetDevicePixelRatio);
      var pauses = 0;
      final selection = LiveMatchSelection();
      await renderClassic(t, classicSnapshot(31), selection, () => pauses++);
      await t.pumpAndSettle();
      expect(find.byType(ClassicReferenceMatchList), findsOneWidget);
      for (final group in [
        '1ª DIVISÃO',
        '2ª DIVISÃO',
        '3ª DIVISÃO',
        '4ª DIVISÃO',
      ]) {
        expect(find.byKey(ValueKey('division-heading-$group')), findsOneWidget);
      }
      expect(
        find.byKey(const Key('selected-match-scoreboard')),
        width >= 1000 ? findsOneWidget : findsNothing,
      );
      expect(find.byKey(const Key('classic-round-clock')), findsOneWidget);
      final team = find.byKey(const ValueKey('live-club-live-2-home'));
      await t.ensureVisible(team);
      await t.tap(team);
      await t.pumpAndSettle();
      expect(
        find.byKey(const Key('live-team-inspection-dialog')),
        findsOneWidget,
      );
      expect(find.byType(MatchTeamTacticalPitch), findsOneWidget);
      expect(
        find.byKey(const ValueKey('btn-aplicar-ajuste-janela')),
        findsNothing,
      );
      expect(pauses, 0);
      final updated = classicSnapshot(32);
      (updated['matches'] as List)[2]['homeFormation'] = '4-3-3';
      await renderClassic(t, updated, selection, () => pauses++);
      await t.pumpAndSettle();
      expect(find.textContaining('Minuto 32′'), findsOneWidget);
      expect(find.textContaining('Formação 4-3-3'), findsOneWidget);
      expect(pauses, 0);
      await t.tap(find.byKey(const Key('close-team-inspection')));
      await t.pumpAndSettle();
      final own = find.byKey(const ValueKey('live-club-live-0-home'));
      await t.ensureVisible(own);
      await t.tap(own);
      await t.pumpAndSettle();
      expect(pauses, 1);
      expect(
        find.byKey(const Key('live-team-inspection-dialog')),
        findsNothing,
      );
      expect(t.takeException(), isNull);
      await t.pumpWidget(const SizedBox());
    });
  }
  for (final width in [390.0, 1366.0]) {
    testWidgets(
      'Own team opens existing shared pause and substitution at $width px',
      (t) async {
        t.view.physicalSize = Size(width, 1000);
        t.view.devicePixelRatio = 1;
        addTearDown(t.view.resetPhysicalSize);
        addTearDown(t.view.resetDevicePixelRatio);
        final api = ClubClickApi();
        await join(t, api);
        await PresentationScope.maybeOf(
          t.element(find.byType(ElifootShellScreen)),
        )!.select(api, PresentationVersion.classic);
        await t.pumpAndSettle();
        await api.emit('RUNNING_FIRST_HALF');
        await t.pumpAndSettle();
        final foreign = find.byKey(
          const ValueKey('live-club-foreign-live-home'),
        );
        await t.ensureVisible(foreign);
        await t.tap(foreign);
        await t.pumpAndSettle();
        expect(api.pauses, 0);
        expect(api.currentRoundState, 'RUNNING_FIRST_HALF');
        expect(
          find.byKey(const Key('live-team-inspection-dialog')),
          findsOneWidget,
        );
        await api.emit('RUNNING_SECOND_HALF');
        await t.pumpAndSettle();
        expect(
          find.byKey(const Key('live-team-inspection-dialog')),
          findsOneWidget,
        );
        await t.tap(find.byKey(const Key('close-team-inspection')));
        await t.pumpAndSettle();
        final own = find.byKey(const ValueKey('live-club-match-1-home'));
        await t.ensureVisible(own);
        await t.tap(own);
        await t.pumpAndSettle();
        expect(api.pauses, 1);
        expect(api.currentRoundState, 'GLOBAL_PAUSED');
        expect(
          find.byKey(const Key('shared-substitution-dialog')),
          findsOneWidget,
        );
        expect(
          find.byKey(const ValueKey('in-match-substitution-panel')),
          findsOneWidget,
        );
        expect(find.byType(MatchTeamTacticalPitch), findsOneWidget);
        expect(t.takeException(), isNull);
        await t.pumpWidget(const SizedBox());
      },
    );
  }
}
