import 'package:elifoot_flutter/classic_layout.dart';
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
}
