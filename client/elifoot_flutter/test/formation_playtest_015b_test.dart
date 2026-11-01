import 'package:elifoot_flutter/classic_layout.dart';
import 'package:elifoot_flutter/classic_match_board.dart';
import 'package:elifoot_flutter/tactical_formation.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'classic_ux_014b_test.dart' show WaitingRoomApi, join;

class OneHumanFormationApi extends WaitingRoomApi {
  String? sentFormation;
  List<String> sentStarters = [], sentBench = [];
  OneHumanFormationApi() : super(1);
  @override
  Future<Map<String, dynamic>> submitTactics({
    required String worldId,
    required String coachId,
    required String formation,
    required List<String> starterIds,
    required List<String> benchIds,
    required int roundVersion,
  }) async {
    sentFormation = formation;
    sentStarters = List.from(starterIds);
    sentBench = List.from(benchIds);
    isReady = true;
    ready = 1;
    currentRoundState = 'RUNNING_FIRST_HALF';
    this.roundVersion++;
    return {
      'counter': (await getSnapshot(worldId))['readyCounter'],
      'roundStarted': true,
    };
  }
}

void main() {
  for (final width in [390.0, 1440.0]) {
    testWidgets(
      '5-3-2 updates the actual eleven and one human starts live automatically at $width',
      (tester) async {
        await tester.binding.setSurfaceSize(Size(width, 1000));
        addTearDown(() => tester.binding.setSurfaceSize(null));
        final api = OneHumanFormationApi();
        await join(tester, api);
        final before = tester
            .widget<ClassicTacticalPitch>(find.byType(ClassicTacticalPitch))
            .lines
            .expand((line) => line)
            .map((p) => p['id'])
            .toSet();
        await tester.ensureVisible(
          find.byKey(const ValueKey('dropdown-formation')),
        );
        await tester.tap(find.byKey(const ValueKey('dropdown-formation')));
        await tester.pumpAndSettle();
        await tester.tap(find.text('5-3-2').last);
        await tester.pumpAndSettle();
        final pitch = tester.widget<ClassicTacticalPitch>(
          find.byType(ClassicTacticalPitch),
        );
        expect(pitch.lines.map((line) => line.length), [2, 3, 5, 1]);
        final starters = pitch.lines.expand((line) => line).toList();
        final ids = starters.map((p) => p['id']).toSet();
        expect(ids.length, 11);
        expect(
          ids.intersection(before).length,
          10,
          reason: 'Only one defender/midfielder swap is necessary.',
        );
        expect(
          starters.every(
            (p) =>
                p['injuryRoundsRemaining'] == 0 &&
                p['suspensionRoundsRemaining'] == 0,
          ),
          isTrue,
        );
        expect(starters.where((p) => p['position'] == 'D').length, 5);
        expect(find.text('SETORES COMPATÍVEIS [OK]'), findsOneWidget);
        await tester.ensureVisible(find.byKey(const Key('btn-enviar-tatica')));
        await tester.tap(find.byKey(const Key('btn-enviar-tatica')));
        await tester.pumpAndSettle();
        expect(api.sentFormation, '5-3-2');
        expect(api.sentStarters.toSet(), ids);
        expect(
          api.sentStarters.toSet().intersection(api.sentBench.toSet()),
          isEmpty,
        );
        expect(find.text('TÁTICAS ENVIADAS: 1/1'), findsOneWidget);
        expect(find.byType(ClassicMatchBoard), findsOneWidget);
        expect(
          find.textContaining('AGUARDANDO OUTROS TREINADORES'),
          findsNothing,
        );
        expect(tester.takeException(), isNull);
        await tester.pumpWidget(const SizedBox());
      },
    );
  }
  testWidgets('Quick tactics button rebalances the pitch too', (tester) async {
    await tester.binding.setSurfaceSize(const Size(1440, 1000));
    addTearDown(() => tester.binding.setSurfaceSize(null));
    await join(tester, OneHumanFormationApi());
    final button = find.byKey(const ValueKey('quick-action-taticas'));
    await tester.ensureVisible(button);
    await tester.tap(button);
    await tester.pumpAndSettle();
    final shape = tester
        .widget<DropdownButton<String>>(
          find.byKey(const ValueKey('dropdown-formation')),
        )
        .value!;
    final counts = shape.split('-').map(int.parse).toList();
    final pitch = tester.widget<ClassicTacticalPitch>(
      find.byType(ClassicTacticalPitch),
    );
    expect(pitch.lines.map((line) => line.length), [
      counts[2],
      counts[1],
      counts[0],
      1,
    ]);
    expect(tester.takeException(), isNull);
  });
  test(
    'Formation shortage never selects an injured or suspended player or invents one',
    () {
      final squad = <Map<String, dynamic>>[
        {'id': 'g', 'position': 'G', 'strength': 30},
        for (var i = 0; i < 5; i++)
          {'id': 'd$i', 'position': 'D', 'strength': 30 - i},
        for (var i = 0; i < 3; i++)
          {'id': 'm$i', 'position': 'M', 'strength': 30 - i},
        {'id': 'a', 'position': 'A', 'strength': 30},
        {
          'id': 'injured',
          'position': 'A',
          'strength': 50,
          'injuryDaysRemaining': 3,
        },
        {
          'id': 'banned',
          'position': 'A',
          'strength': 50,
          'suspensionRoundsRemaining': 1,
        },
      ];
      final lineup = lineupForFormation(squad, '5-3-2', [], []);
      expect(lineup.starters.length, 10);
      expect(lineup.starters, isNot(contains('injured')));
      expect(lineup.starters, isNot(contains('banned')));
      final selected = squad
          .where((p) => lineup.starters.contains(p['id']))
          .toList();
      expect(
        tacticalPitchLines(selected, '5-3-2').expand((line) => line).length,
        10,
      );
    },
  );
  test(
    'Pitch uses formation slots while preserving nominal position and all manually chosen players',
    () {
      final players = <Map<String, dynamic>>[
        {'id': 'g', 'position': 'G'},
        for (var i = 0; i < 4; i++) {'id': 'd$i', 'position': 'D'},
        for (var i = 0; i < 4; i++) {'id': 'm$i', 'position': 'M'},
        for (var i = 0; i < 2; i++) {'id': 'a$i', 'position': 'A'},
      ];
      final lines = tacticalPitchLines(players, '5-3-2');
      expect(lines.map((line) => line.length), [2, 3, 5, 1]);
      expect(
        lines.expand((line) => line).map((p) => p['id']).toSet(),
        players.map((p) => p['id']).toSet(),
      );
      expect(lines[2].where((p) => p['position'] == 'M').length, 1);
      expect(players.where((p) => p['position'] == 'M').length, 4);
    },
  );
}
