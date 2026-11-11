import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'classic_desktop_015b_test.dart' show Desktop015BApi, select015B;
import 'classic_ux_014b_test.dart' show join;

class FinalGateCalendarApi extends Desktop015BApi {
  @override
  Future<Map<String, dynamic>> getSnapshot(String worldId) async {
    final snapshot = await super.getSnapshot(worldId);
    return {
      ...snapshot,
      'roundNumber': 4,
      'readyCounter': {...snapshot['readyCounter'] as Map, 'roundNumber': 4},
    };
  }

  @override
  Future<List<dynamic>> getCalendar(String worldId) async => [
    {
      'roundNumber': 3,
      'competition': 'CupRoundOf128',
      'fixtures': List.generate(
        64,
        (i) => {
          'home': 'Clube da Taça $i',
          'away': 'Adversário $i',
          'homeGoals': 2,
          'awayGoals': 1,
          'isFinished': true,
        },
      ),
    },
  ];
}

void main() {
  for (final width in [390.0, 768.0, 1024.0, 1280.0, 1360.0]) {
    testWidgets(
      'Calendar reopens past round without losing results at $width',
      (tester) async {
        tester.view.devicePixelRatio = 1;
        tester.view.physicalSize = Size(width, 900);
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);
        await join(tester, FinalGateCalendarApi());
        await select015B(tester, 5);
        final pastRound = find.byKey(const PageStorageKey('calendar-ALL-3'));
        await tester.ensureVisible(pastRound);
        await tester.tap(
          find
              .descendant(
                of: pastRound,
                matching: find.textContaining('Rodada 3'),
              )
              .first,
        );
        await tester.pumpAndSettle();
        expect(tester.takeException(), isNull);
        expect(find.text('Clube da Taça 0'), findsOneWidget);
        expect(find.text('2 x 1'), findsWidgets);
        await tester.tap(
          find
              .descendant(
                of: pastRound,
                matching: find.textContaining('Rodada 3'),
              )
              .first,
        );
        await tester.pumpAndSettle();
        await tester.tap(
          find
              .descendant(
                of: pastRound,
                matching: find.textContaining('Rodada 3'),
              )
              .first,
        );
        await tester.pumpAndSettle();
        expect(tester.takeException(), isNull);
        expect(find.text('Clube da Taça 0'), findsOneWidget);
        expect(find.text('FINALIZADO'), findsWidgets);
        await tester.pumpWidget(const SizedBox());
      },
    );
  }
}
