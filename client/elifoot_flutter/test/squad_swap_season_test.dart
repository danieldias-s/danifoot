import 'package:elifoot_flutter/classic_layout.dart';
import 'package:elifoot_flutter/tactical_formation.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'classic_ux_014b_test.dart' show WaitingRoomApi, join;

void main() {
  for (final width in [390.0, 1440.0]) {
    testWidgets('Two clicks exchange a midfielder for a defender at $width', (
      tester,
    ) async {
      await tester.binding.setSurfaceSize(Size(width, 950));
      addTearDown(() => tester.binding.setSurfaceSize(null));
      await join(tester, WaitingRoomApi(2));

      Future<void> pick(String player) async {
        final button = find.byKey(ValueKey('swap-pick-$player'));
        await tester.ensureVisible(button);
        await tester.tap(button);
        await tester.pumpAndSettle();
      }

      await pick('p-13'); // Reserve defender first.
      await pick('p-6');
      final dropdown = find.byKey(const ValueKey('dropdown-formation'));
      expect(tester.widget<DropdownButton<String>>(dropdown).value, '5-3-2');
      if (width >= 768) {
        final pitch = tester.widget<ClassicTacticalPitch>(
          find.byType(ClassicTacticalPitch),
        );
        expect(pitch.lines[2].any((p) => p['id'] == 'p-13'), isTrue);
      }
      await pick('p-13'); // Now starter first.
      await pick('p-6');
      expect(tester.widget<DropdownButton<String>>(dropdown).value, '4-4-2');
      expect(tester.takeException(), isNull);
    });
  }

  test('Best lineup can select ten defenders or prefer the leading scorer', () {
    final squad = <Map<String, dynamic>>[
      {'id': 'g', 'position': 'G', 'strength': 45},
      for (var i = 0; i < 10; i++)
        {'id': 'd$i', 'position': 'D', 'strength': 40 - i},
      {'id': 'scorer', 'position': 'A', 'strength': 20, 'seasonGoals': 12},
      {
        'id': 'injured',
        'position': 'A',
        'strength': 50,
        'seasonGoals': 50,
        'injuryDaysRemaining': 1,
      },
    ];
    final strongest = bestSquadLineup(squad, '4-4-2', preferGoals: false);
    expect(strongest.formation, '10-0-0');
    final scorers = bestSquadLineup(squad, '4-4-2', preferGoals: true);
    expect(scorers.formation, '9-0-1');
    expect(scorers.starters, contains('scorer'));
    expect(scorers.starters, isNot(contains('injured')));
  });
}
