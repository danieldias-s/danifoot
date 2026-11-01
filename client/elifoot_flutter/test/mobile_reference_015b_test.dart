import 'package:elifoot_flutter/mobile_classic.dart';
import 'package:elifoot_flutter/classic_layout.dart';
import 'package:elifoot_flutter/classic_match_board.dart';
import 'package:elifoot_flutter/morale_bar.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'classic_ux_014b_test.dart' show join, WaitingRoomApi;

void main() {
  for (final width in [320.0, 360.0, 390.0, 430.0, 600.0]) {
    testWidgets(
      'Reference mobile roster, real counts, actions and bottom navigation at $width',
      (tester) async {
        await tester.binding.setSurfaceSize(Size(width, 900));
        addTearDown(() => tester.binding.setSurfaceSize(null));
        final api = WaitingRoomApi(2);
        await join(tester, api);
        expect(find.byType(MobileClubOverview), findsOneWidget);
        expect(find.byType(MobileClassicNavigation), findsOneWidget);
        expect(find.byType(MobileSquadScreen), findsOneWidget);
        expect(
          find.byType(ClassicTacticalPitch),
          findsNothing,
          reason: 'Plantel starts with lists, not a pitch.',
        );
        expect(find.text('RESUMO TÁTICO'), findsOneWidget);
        expect(find.text('TITULARES (11)'), findsOneWidget);
        expect(find.text('BANCO (5)'), findsOneWidget);
        expect(find.text('PLANTEL DE JOGADORES (18)'), findsOneWidget);
        final bar = tester.widget<MoraleBar>(find.byType(MoraleBar));
        expect(bar.value, 75);
        expect(find.text('Moral'), findsNothing);
        expect(find.text('75/100'), findsNothing);
        expect(find.text('VERDE'), findsNothing);
        expect(tester.takeException(), isNull);
        await tester.tap(find.byKey(const Key('mobile-nav-1')));
        await tester.pumpAndSettle();
        expect(find.byType(ClassicTacticalPitch), findsOneWidget);
        await tester.tap(find.byKey(const Key('mobile-nav-0')));
        await tester.pumpAndSettle();
        expect(find.byType(ClassicTacticalPitch), findsNothing);
        await tester.tap(find.byKey(const Key('mobile-nav-2')));
        await tester.pumpAndSettle();
        expect(find.byKey(const ValueKey('market-tab-0')), findsOneWidget);
        await tester.tap(find.byKey(const Key('mobile-nav-3')));
        await tester.pumpAndSettle();
        expect(find.byKey(const ValueKey('standings-table')), findsOneWidget);
        await tester.tap(find.byKey(const Key('open-navigation')));
        await tester.pumpAndSettle();
        expect(find.byType(AppSidebar), findsOneWidget);
        await tester.tap(find.byKey(const ValueKey('shell-nav-5')));
        await tester.pumpAndSettle();
        expect(find.byKey(const Key('calendar-filters')), findsOneWidget);
        expect(tester.takeException(), isNull);
        await tester.pumpWidget(const SizedBox());
      },
    );
  }
  testWidgets(
    'Mobile list opens player facts and retains contracted salary, filters and auction access',
    (tester) async {
      await tester.binding.setSurfaceSize(const Size(390, 1000));
      addTearDown(() => tester.binding.setSurfaceSize(null));
      await join(tester, WaitingRoomApi(2));
      final firstFacts = find.byTooltip('Ficha de Carlos Muralha*');
      await tester.ensureVisible(firstFacts);
      await tester.tap(firstFacts);
      await tester.pumpAndSettle();
      expect(find.text('Carlos Muralha*'), findsWidgets);
      expect(find.textContaining('Comportamento:'), findsWidgets);
      await tester.tap(find.text('FECHAR'));
      await tester.pumpAndSettle();
      final filters = find.byKey(const Key('mobile-squad-filters'));
      await tester.ensureVisible(filters);
      await tester.tap(filters);
      await tester.pumpAndSettle();
      await tester.ensureVisible(
        find.byKey(const ValueKey('filter-squad-pos-G')),
      );
      await tester.tap(find.byKey(const ValueKey('filter-squad-pos-G')));
      await tester.pumpAndSettle();
      final table = tester.widget<DataTable>(
        find.byKey(const ValueKey('squad-table')),
      );
      expect(table.rows.length, 2);
      expect(
        find.descendant(
          of: find.byKey(const ValueKey('squad-table')),
          matching: find.text('R\$ 12.000'),
        ),
        findsOneWidget,
      );
      expect(find.byTooltip('Colocar em leilão'), findsNWidgets(2));
      await tester.tap(find.byKey(const Key('mobile-nav-1')));
      await tester.pumpAndSettle();
      final tacticalScroll = find
          .descendant(
            of: find.byKey(const Key('mobile-tactics-scroll')),
            matching: find.byType(Scrollable),
          )
          .first;
      expect(
        tester.state<ScrollableState>(tacticalScroll).position.pixels,
        0,
        reason:
            'Opening tactics must not retain the roster filter scroll offset.',
      );
      expect(
        tester.getTopLeft(find.byType(ClassicTacticalPitch)).dy,
        lessThan(tester.getTopLeft(find.byType(MobileClassicNavigation)).dy),
      );
      expect(tester.takeException(), isNull);
    },
  );
  testWidgets(
    'Bottom navigation and More drawer stay locked during a live round',
    (tester) async {
      await tester.binding.setSurfaceSize(const Size(390, 900));
      addTearDown(() => tester.binding.setSurfaceSize(null));
      final api = WaitingRoomApi(2);
      await join(tester, api);
      await tester.tap(find.byKey(const Key('open-navigation')));
      await tester.pumpAndSettle();
      await api.emit('RUNNING_FIRST_HALF');
      await tester.pumpAndSettle();
      expect(find.byType(ClassicMatchBoard), findsOneWidget);
      expect(find.byType(AppSidebar), findsNothing);
      expect(find.byKey(const Key('open-navigation')), findsNothing);
      for (var i = 0; i < 5; i++) {
        expect(
          tester.widget<InkWell>(find.byKey(ValueKey('mobile-nav-$i'))).onTap,
          isNull,
        );
      }
      await api.emit('RESULTS');
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('mobile-nav-0')));
      await tester.pumpAndSettle();
      expect(find.byType(MobileSquadScreen), findsOneWidget);
      expect(tester.takeException(), isNull);
    },
  );
}
