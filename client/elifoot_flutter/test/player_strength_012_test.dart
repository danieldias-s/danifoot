import 'package:elifoot_flutter/player_rankings.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  testWidgets('Legacy profile has an honest empty force history', (
    tester,
  ) async {
    await tester.pumpWidget(
      const MaterialApp(
        home: Scaffold(body: PlayerStrengthHistoryPanel(data: {})),
      ),
    );
    expect(find.text('HISTÓRICO DE FORÇA'), findsOneWidget);
    expect(find.text('Nenhuma mudança de força registrada.'), findsOneWidget);
    expect(find.textContaining('Registro desde'), findsNothing);
    expect(tester.takeException(), isNull);
  });

  for (final width in [320.0, 390.0, 1280.0]) {
    testWidgets('Force events retain original club and fit width $width', (
      tester,
    ) async {
      tester.view.physicalSize = Size(width, 844);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: SingleChildScrollView(
              child: PlayerStrengthHistoryPanel(
                data: {
                  'strengthHistoryStartSeason': 1,
                  'strengthHistoryStartRound': 22,
                  'strengthHistory': [
                    {
                      'season': 1,
                      'round': 22,
                      'clubId': 'historical-club',
                      'clubName': 'Clube da Formação',
                      'division': 'QuartaDivisao',
                      'strengthBefore': 1,
                      'strengthAfter': 2,
                      'changeReason': 'NORMAL_PROGRESS',
                      'rule': 'LEGACY_SEASONAL_005_SCALE_FIX_012',
                    },
                    {
                      'season': 3,
                      'round': 22,
                      'clubId': 'next-club',
                      'clubName': 'Clube Seguinte',
                      'division': 3,
                      'strengthBefore': 31,
                      'strengthAfter': 30,
                      'changeReason': 'NORMAL_DECLINE',
                      'rule': 'LEGACY_SEASONAL_005_SCALE_FIX_012',
                    },
                  ],
                },
              ),
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();
      expect(find.text('Clube da Formação'), findsWidgets);
      expect(find.text('Clube Seguinte'), findsWidgets);
      expect(find.textContaining('Registro desde T1 R22.'), findsOneWidget);
      expect(find.text('Evolução sazonal'), findsWidgets);
      expect(find.text('Queda sazonal'), findsWidgets);
      expect(find.textContaining('histórico indisponível'), findsNothing);
      expect(tester.takeException(), isNull);
    });
  }
}
