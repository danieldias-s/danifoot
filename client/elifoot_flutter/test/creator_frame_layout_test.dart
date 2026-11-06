import 'package:elifoot_flutter/classic98_widgets.dart';
import 'package:elifoot_flutter/club_theme.dart';
import 'package:elifoot_flutter/creator_visual.dart';
import 'package:elifoot_flutter/game_viewport.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  const steps = [
    'Categoria',
    'Países',
    'Divisões',
    'Distritais',
    'Elencos / fontes',
    'Prévia',
    'Confirmar',
  ];
  const formKey = Key('creator-layout-form');
  const sizes = [
    Size(1920, 1080),
    Size(1600, 900),
    Size(1360, 768),
    Size(1024, 768),
    Size(390, 844),
    Size(360, 800),
  ];

  for (final classic in [false, true]) {
    testWidgets('Creator form and back button are usable (classic=$classic)', (
      tester,
    ) async {
      tester.view.devicePixelRatio = 1;
      tester.view.physicalSize = sizes.first;
      addTearDown(tester.view.resetDevicePixelRatio);
      addTearDown(tester.view.resetPhysicalSize);

      final current = ClassicTheme.classicGreen();
      final theme = classic ? classic98PresentationPalette(current) : current;
      var step = 0;
      await tester.pumpWidget(
        MaterialApp(
          theme: theme.material(),
          home: GameViewport(
            child: StatefulBuilder(
              builder: (context, update) => CreatorFrame(
                steps: steps,
                step: step,
                countryCount: 0,
                busy: false,
                error: '',
                back: step == 0 ? null : () => update(() => step = 0),
                next: () => update(() => step = 1),
                body: const SizedBox(key: formKey, height: 150),
              ),
            ),
          ),
        ),
      );
      final back = find.widgetWithText(OutlinedButton, 'VOLTAR');
      final next = find.widgetWithText(ElevatedButton, 'CONTINUAR');
      expect(find.text('1/7 • CATEGORIA'), findsOneWidget);

      Material backMaterial() => tester.widget<Material>(
        find.descendant(of: back, matching: find.byType(Material)),
      );
      void expectReadableBack() {
        final label = tester.widget<RichText>(
          find.descendant(
            of: find.text('VOLTAR'),
            matching: find.byType(RichText),
          ),
        );
        final arrow = tester.element(
          find.descendant(of: back, matching: find.byIcon(Icons.arrow_back)),
        );
        final fill = backMaterial().color!;
        expect(colorContrast(label.text.style!.color!, fill), greaterThan(7));
        expect(colorContrast(IconTheme.of(arrow).color!, fill), greaterThan(7));
      }

      for (final size in sizes) {
        tester.view.physicalSize = size;
        await tester.pumpAndSettle();
        final form = tester.getRect(find.byKey(formKey));
        final backButton = tester.getRect(back);
        final nextButton = tester.getRect(next);
        expect(form.center.dx, closeTo(size.width / 2, 1));
        expect(form.width, lessThanOrEqualTo(720));
        expect(backButton.top - form.bottom, inInclusiveRange(12, 32));
        expect(nextButton.right, lessThanOrEqualTo(size.width));
        expect(nextButton.bottom, lessThanOrEqualTo(size.height));
        expectReadableBack();
        expect(tester.takeException(), isNull, reason: 'viewport $size');
      }

      final inactiveFill = backMaterial().color!;
      expect(tester.widget<OutlinedButton>(back).onPressed, isNull);
      await tester.tap(next);
      await tester.pumpAndSettle();
      expect(find.text('2/7 • PAÍSES'), findsOneWidget);
      expectReadableBack();
      expect(
        colorContrast(inactiveFill, backMaterial().color!),
        greaterThan(3),
      );
      await tester.tap(back);
      await tester.pumpAndSettle();
      expect(step, 0);
    });
  }
}
