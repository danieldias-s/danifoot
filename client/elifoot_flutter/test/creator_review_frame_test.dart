import 'package:elifoot_flutter/classic98_widgets.dart';
import 'package:elifoot_flutter/club_theme.dart';
import 'package:elifoot_flutter/creator_visual.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

const _steps = [
  'Categoria',
  'Países',
  'Escolher anos',
  'Divisões',
  'Distritais',
  'Elencos / fontes',
  'Prévia',
  'Confirmar',
];

void main() {
  const widths = [360.0, 390.0, 430.0, 768.0, 1366.0, 1920.0];
  for (final classic in [false, true]) {
    for (final step in [6, 7]) {
      for (final width in widths) {
        testWidgets(
          'Revisão ${step + 1}/8 em $width px, aparência clássica=$classic',
          (tester) async {
            final size = Size(width, width < 600 ? 844 : 1080);
            tester.view.devicePixelRatio = 1;
            tester.view.physicalSize = size;
            tester.view.padding = const FakeViewPadding(top: 24, bottom: 34);
            addTearDown(tester.view.resetDevicePixelRatio);
            addTearDown(tester.view.resetPhysicalSize);
            addTearDown(tester.view.resetPadding);
            final green = ClassicTheme.classicGreen();
            final outer = classic ? classic98PresentationPalette(green) : green;
            final nextLabel = step == 7 ? 'CONFIRMAR CRIAÇÃO' : 'CONTINUAR';
            var backCalls = 0, nextCalls = 0;
            ClassicTheme? paperTheme;
            await tester.pumpWidget(
              MaterialApp(
                theme: outer.material(),
                // The modern creator keeps its presentation when opened from
                // either app appearance, as WorldCreatorScreen does.
                home: Theme(
                  data: green.material(),
                  child: CreatorFrame(
                    steps: _steps,
                    step: step,
                    countryCount: 2,
                    forceStadium: true,
                    busy: false,
                    error: '',
                    finalAction: step == 7,
                    nextLabel: nextLabel,
                    back: () => backCalls++,
                    next: () => nextCalls++,
                    body: Builder(
                      builder: (context) {
                        paperTheme = ClassicTheme.of(context);
                        return Column(
                          crossAxisAlignment: CrossAxisAlignment.stretch,
                          children: [
                            for (var i = 0; i < 18; i++)
                              SizedBox(
                                height: 84,
                                child: Text('Clube de QA com nome extenso $i'),
                              ),
                            const SizedBox(
                              key: ValueKey('review-frame-last-content'),
                              height: 30,
                              child: Text('Fim da lista de revisão'),
                            ),
                          ],
                        );
                      },
                    ),
                  ),
                ),
              ),
            );
            await tester.pumpAndSettle();

            final back = find.widgetWithText(OutlinedButton, 'VOLTAR');
            final next = find.widgetWithText(ElevatedButton, nextLabel);
            expect(back, findsOneWidget);
            expect(next, findsOneWidget);
            final backRect = tester.getRect(back);
            final nextRect = tester.getRect(next);
            expect(backRect.height, greaterThanOrEqualTo(48));
            expect(nextRect.height, greaterThanOrEqualTo(48));
            expect(backRect.left, greaterThanOrEqualTo(0));
            expect(nextRect.right, lessThanOrEqualTo(width));
            expect(nextRect.bottom, lessThanOrEqualTo(size.height - 34));
            expect(nextRect.overlaps(backRect), isFalse);
            expect(paperTheme!.surface, const Color(0xFFF1F4F2));
            expect(paperTheme!.textPrimary, const Color(0xFF102A1C));

            final backMaterial = tester.widget<Material>(
              find.descendant(of: back, matching: find.byType(Material)),
            );
            final backLabel = tester.widget<RichText>(
              find.descendant(
                of: find.text('VOLTAR'),
                matching: find.byType(RichText),
              ),
            );
            expect(
              colorContrast(backLabel.text.style!.color!, backMaterial.color!),
              greaterThan(7),
            );
            final contentViewport = tester.getRect(
              find.byKey(ValueKey('creator-body-scroll-$step')),
            );
            expect(contentViewport.bottom, lessThanOrEqualTo(backRect.top));
            await tester.ensureVisible(
              find.byKey(const ValueKey('review-frame-last-content')),
            );
            await tester.pumpAndSettle();
            final last = tester.getRect(
              find.byKey(const ValueKey('review-frame-last-content')),
            );
            expect(last.bottom, lessThanOrEqualTo(contentViewport.bottom + 1));
            expect(tester.getRect(next), nextRect);
            expect(tester.getRect(back), backRect);

            if (width < 1024) {
              for (var i = 0; i < _steps.length; i++) {
                final marker = find.byKey(ValueKey('creator-step-marker-$i'));
                final shortLabel = find.byKey(
                  ValueKey('creator-step-short-$i'),
                );
                expect(marker, findsOneWidget);
                expect(shortLabel, findsOneWidget);
                expect(tester.getRect(marker).right, lessThanOrEqualTo(width));
                expect(
                  tester.getRect(shortLabel).left,
                  greaterThanOrEqualTo(0),
                );
              }
            } else {
              expect(
                find.byKey(const ValueKey('creator-step-short-0')),
                findsNothing,
              );
            }
            if (step == 7) {
              expect(
                find.descendant(of: next, matching: find.byIcon(Icons.check)),
                findsOneWidget,
              );
              expect(
                find.descendant(
                  of: next,
                  matching: find.byIcon(Icons.arrow_forward),
                ),
                findsNothing,
              );
              if (width < 1024) {
                final label = tester.widget<Text>(find.text(nextLabel));
                expect(label.style!.fontSize, greaterThanOrEqualTo(18));
                expect(label.maxLines, 2);
              }
            }
            await tester.tap(next);
            await tester.tap(back);
            expect(nextCalls, 1);
            expect(backCalls, 1);
            expect(tester.takeException(), isNull);
          },
        );
      }
    }
  }

  testWidgets('Ação final é opt-in e respeita botão desabilitado', (
    tester,
  ) async {
    Future<void> frame({required bool finalAction}) async {
      await tester.pumpWidget(
        MaterialApp(
          theme: ClassicTheme.classicGreen().material(),
          home: CreatorFrame(
            steps: _steps,
            step: 7,
            countryCount: 0,
            body: const Text('Revisão'),
            busy: false,
            error: '',
            nextLabel: 'CONFIRMAR CRIAÇÃO',
            finalAction: finalAction,
            next: null,
          ),
        ),
      );
      await tester.pumpAndSettle();
    }

    await frame(finalAction: false);
    expect(find.text('CONFIRMAR CRIAÇÃO'), findsNothing);
    await frame(finalAction: true);
    final confirm = find.widgetWithText(ElevatedButton, 'CONFIRMAR CRIAÇÃO');
    expect(confirm, findsOneWidget);
    expect(tester.widget<ElevatedButton>(confirm).onPressed, isNull);
    expect(tester.takeException(), isNull);
  });

  testWidgets(
    'Progresso das etapas anteriores permanece compacto sem rótulos',
    (tester) async {
      tester.view.devicePixelRatio = 1;
      tester.view.physicalSize = const Size(390, 844);
      addTearDown(tester.view.resetDevicePixelRatio);
      addTearDown(tester.view.resetPhysicalSize);
      await tester.pumpWidget(
        MaterialApp(
          theme: ClassicTheme.classicGreen().material(),
          home: const CreatorFrame(
            steps: _steps,
            step: 3,
            countryCount: 0,
            body: Text('Divisões'),
            busy: false,
            error: '',
          ),
        ),
      );
      await tester.pumpAndSettle();
      expect(find.byKey(const ValueKey('creator-step-short-0')), findsNothing);
      expect(
        find.byKey(const ValueKey('creator-step-marker-0')),
        findsOneWidget,
      );
      expect(tester.takeException(), isNull);
    },
  );
}
