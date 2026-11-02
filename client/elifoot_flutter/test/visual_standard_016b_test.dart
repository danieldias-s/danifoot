import 'package:elifoot_flutter/appearance_settings.dart';
import 'package:elifoot_flutter/classic_layout.dart';
import 'package:elifoot_flutter/club_theme.dart';
import 'package:elifoot_flutter/morale_bar.dart';
import 'package:elifoot_flutter/visual_components.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'classic_desktop_015b_test.dart' show Desktop015BApi, select015B;
import 'classic_ux_014b_test.dart' show join;

void fits(WidgetTester tester, Finder finder, double width) {
  final rect = tester.getRect(finder);
  expect(rect.left, greaterThanOrEqualTo(0));
  expect(rect.right, lessThanOrEqualTo(width + .1));
}

void main() {
  for (final width in [360.0, 390.0, 430.0, 768.0, 1366.0, 1920.0]) {
    testWidgets(
      '016B priority screens and persistent mobile navigation at $width',
      (tester) async {
        await tester.binding.setSurfaceSize(Size(width, 1000));
        addTearDown(() => tester.binding.setSurfaceSize(null));
        final api = Desktop015BApi();
        await join(tester, api);
        final mobile = width < 1024;
        fits(tester, find.byKey(const Key('classic-club-summary')), width);
        expect(find.byType(ClassicSummaryCard), findsNWidgets(5));
        expect(
          find.descendant(
            of: find.byType(MoraleBar),
            matching: find.byType(Text),
          ),
          findsNothing,
        );
        expect(
          find.byType(ClassicBottomNavigation),
          mobile ? findsOneWidget : findsNothing,
        );
        if (mobile) {
          await tester.tap(find.byKey(const ValueKey('bottom-nav-2')));
          await tester.pumpAndSettle();
        } else {
          await select015B(tester, 2);
        }
        fits(tester, find.byKey(const ValueKey('market-tab-0')), width);
        await tester.tap(find.byKey(const ValueKey('market-tab-1')));
        await tester.pumpAndSettle();
        expect(tester.takeException(), isNull, reason: 'market $width');
        if (mobile) {
          await tester.tap(find.byKey(const ValueKey('bottom-nav-3')));
          await tester.pumpAndSettle();
        } else {
          await select015B(tester, 1);
        }
        fits(tester, find.byKey(const ValueKey('standings-table')), width);
        final standingsContext = tester.element(
          find.byKey(const ValueKey('standings-table')),
        );
        final paper = ClassicTheme.of(standingsContext);
        expect(
          colorContrast(
            DefaultTextStyle.of(standingsContext).style.color!,
            paper.tableRow,
          ),
          greaterThanOrEqualTo(4.5),
          reason: 'Light tables must inherit dark readable text.',
        );
        await tester.tap(
          find.byKey(const ValueKey('btn-division-QuartaDivisao')),
        );
        await tester.pumpAndSettle();
        expect(api.divisions.last, 'QuartaDivisao');
        await tester.tap(find.text('TAÇA NACIONAL'));
        await tester.pumpAndSettle();
        expect(
          find.byKey(const ValueKey('cup-champion-banner')),
          findsOneWidget,
        );
        await select015B(tester, 5);
        fits(tester, find.byKey(const Key('calendar-filters')), width);
        await tester.tap(find.text('RODADA ATUAL (R1)'));
        await tester.pumpAndSettle();
        expect(find.byKey(const ValueKey('calendar-round-2')), findsNothing);
        expect(tester.takeException(), isNull, reason: 'competitions $width');
        if (mobile) {
          await tester.tap(find.byKey(const ValueKey('bottom-nav-4')));
          await tester.pumpAndSettle();
        }
        await tester.ensureVisible(
          find.byKey(const ValueKey('appearance-menu')),
        );
        await tester.tap(find.byKey(const ValueKey('appearance-menu')));
        await tester.pumpAndSettle();
        expect(find.byType(AppearanceSettings), findsOneWidget);
        expect(find.byType(AppHeader), findsOneWidget);
        expect(
          find.byType(ClassicBottomNavigation),
          mobile ? findsOneWidget : findsNothing,
        );
        await tester.ensureVisible(
          find.byKey(const ValueKey('theme-variant-Escuro')),
        );
        await tester.pumpAndSettle();
        await tester.tap(find.byKey(const ValueKey('theme-variant-Escuro')));
        await tester.pumpAndSettle();
        await tester.ensureVisible(find.byType(BackButton));
        await tester.pumpAndSettle();
        await tester.tap(find.byType(BackButton));
        await tester.pumpAndSettle();
        expect(find.byType(AppearanceSettings), findsNothing);
        final context = tester.element(find.byType(AppHeader));
        expect(
          ClassicTheme.of(context).background,
          ClassicTheme.classicGreen().background,
          reason: 'Unsaved appearance changes must be discarded on return.',
        );
        expect(tester.takeException(), isNull, reason: 'appearance $width');
        await tester.pumpWidget(const SizedBox());
      },
    );
  }
}
