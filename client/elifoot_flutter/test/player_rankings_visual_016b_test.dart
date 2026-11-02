import 'package:elifoot_flutter/classic_layout.dart';
import 'package:elifoot_flutter/club_theme.dart';
import 'package:elifoot_flutter/main.dart';
import 'package:elifoot_flutter/player_rankings.dart';
import 'package:elifoot_flutter/visual_components.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'player_rankings_widget_test.dart'
    show Fixture, player, openRankings, select;

void main() {
  for (final width in [360.0, 390.0, 430.0, 768.0, 1366.0, 1920.0]) {
    testWidgets(
      'Player ranking empty panels, open filters and full names fit $width',
      (tester) async {
        final fixture = Fixture()..rows = [];
        await openRankings(tester, fixture, width: width);
        final content = find.byKey(const Key('player-rankings-heading'));
        expect(tester.getRect(content).width, greaterThan(width - 35));
        expect(tester.getRect(content).right, lessThanOrEqualTo(width));
        final ranking = find.byKey(const Key('player-ranking-panel'));
        final awards = find.byKey(const Key('player-season-awards-panel'));
        if (width >= 1366) {
          expect(tester.getTopLeft(ranking).dy, tester.getTopLeft(awards).dy);
          expect(
            tester.getTopLeft(awards).dx,
            greaterThan(tester.getTopLeft(ranking).dx),
          );
        } else {
          expect(
            tester.getTopLeft(awards).dy,
            greaterThan(tester.getBottomLeft(ranking).dy),
          );
        }
        expect(
          find.text(
            'Nenhum jogador elegível com estatísticas oficiais neste recorte.',
          ),
          findsOneWidget,
        );
        expect(
          find.text('Nenhuma temporada encerrada com premiações registradas.'),
          findsOneWidget,
        );
        await select(tester, 'ranking-category', 'Goleiros');
        await select(tester, 'ranking-scope', 'Taça');
        await select(tester, 'ranking-period', 'Temporada selecionada');
        await select(tester, 'ranking-season', 'Temporada 2028');
        final selectedPeriod = find.text('Temporada selecionada').hitTestable();
        expect(selectedPeriod, findsOneWidget);
        final periodBounds = tester.getRect(
          find.byKey(const ValueKey('ranking-period')),
        );
        expect(
          tester.getRect(selectedPeriod).top,
          greaterThanOrEqualTo(periodBounds.top),
        );
        expect(
          tester.getRect(selectedPeriod).bottom,
          lessThanOrEqualTo(periodBounds.bottom),
        );
        final query = fixture.requests
            .lastWhere((r) => r.url.path.endsWith('/player-rankings'))
            .url
            .queryParameters;
        expect(query, {
          'category': 'goalkeepers',
          'scope': 'cup',
          'period': 'season',
          'season': '2028',
        });
        await tester.ensureVisible(find.byKey(const Key('ranking-refresh')));
        await tester.pumpAndSettle();
        final before = fixture.requests
            .where((r) => r.url.path.endsWith('/player-rankings'))
            .length;
        await tester.tap(find.byKey(const Key('ranking-refresh')));
        await tester.pumpAndSettle();
        expect(
          fixture.requests
              .where((r) => r.url.path.endsWith('/player-rankings'))
              .length,
          before + 1,
        );
        expect(tester.takeException(), isNull);
        // Only the ranking GET responses supply these rows; this is a test fixture, not app content.
        fixture.rows = [
          player('long', 'João Guilherme de Albuquerque e Vasconcelos*')
            ..['currentClubName'] =
                'Associação Atlética Ferroviária de Paranapiacaba',
        ];
        await select(tester, 'ranking-category', 'Defensores');
        expect(
          find.text('João Guilherme de Albuquerque e Vasconcelos*'),
          findsWidgets,
        );
        expect(
          tester.getRect(find.byKey(const Key('player-ranking-panel'))).right,
          lessThanOrEqualTo(width),
        );
        if (width < 700) {
          expect(
            find.byKey(const ValueKey('ranking-table-horizontal')),
            findsNothing,
          );
        }
        await tester.ensureVisible(
          find.text('João Guilherme de Albuquerque e Vasconcelos*').first,
        );
        await tester.pumpAndSettle();
        await tester.tap(
          find.text('João Guilherme de Albuquerque e Vasconcelos*').first,
        );
        await tester.pumpAndSettle();
        expect(find.text('HISTÓRICO DO JOGADOR'), findsOneWidget);
        expect(tester.takeException(), isNull);
      },
    );
  }
  for (final variant in ['Classico', 'Escuro', 'AltoContraste']) {
    testWidgets(
      'Ranking panels respect saved $variant theme and remain readable',
      (tester) async {
        final fixture = Fixture()..rows = [];
        final theme = ClassicTheme.classicGreen(variant: variant);
        await tester.pumpWidget(
          MaterialApp(
            theme: theme.material(),
            home: Scaffold(
              body: PlayerRankingsScreen(
                apiClient: fixture.api,
                worldId: 'world',
                currentSeason: 1,
              ),
            ),
          ),
        );
        await tester.pumpAndSettle();
        final panelContext = tester.element(
          find.byKey(const Key('player-ranking-panel')),
        );
        expect(ClassicTheme.of(panelContext).background, theme.background);
        final messageContext = tester.element(
          find.text(
            'Nenhum jogador elegível com estatísticas oficiais neste recorte.',
          ),
        );
        final local = ClassicTheme.of(messageContext);
        expect(
          colorContrast(local.textPrimary, local.surface),
          greaterThanOrEqualTo(4.5),
        );
        expect(tester.takeException(), isNull);
      },
    );
  }
  testWidgets(
    'Mobile shell highlights players, keeps sessions and exposes two rows of club facts',
    (tester) async {
      await tester.binding.setSurfaceSize(const Size(390, 1000));
      addTearDown(() => tester.binding.setSurfaceSize(null));
      final fixture = Fixture();
      await tester.pumpWidget(ElifootClassicApp(apiClient: fixture.api));
      await tester.tap(find.text('ENTRAR NO LOBBY (LOCAL DEV)'));
      await tester.pumpAndSettle();
      await tester.ensureVisible(find.text('ENTRAR DESEMPREGADO'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('ENTRAR DESEMPREGADO'));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('open-navigation')));
      await tester.pumpAndSettle();
      await tester.ensureVisible(find.byKey(const ValueKey('shell-nav-6')));
      await tester.tap(find.byKey(const ValueKey('shell-nav-6')));
      await tester.pumpAndSettle();
      final nav = tester.widget<ClassicBottomNavigation>(
        find.byType(ClassicBottomNavigation),
      );
      expect(nav.selected, 3);
      expect(nav.showPlayerRankings, isTrue);
      expect(find.text('RANKINGS'), findsOneWidget);
      final cards = find.byType(ClassicSummaryCard);
      expect(cards, findsNWidgets(5));
      expect(
        tester.getTopLeft(cards.at(1)).dy,
        tester.getTopLeft(cards.at(2)).dy,
      );
      expect(
        tester.getTopLeft(cards.at(3)).dy,
        greaterThan(tester.getTopLeft(cards.at(1)).dy),
      );
      expect(
        tester.getTopLeft(cards.at(3)).dy,
        tester.getTopLeft(cards.at(4)).dy,
      );
      await tester.tap(find.byKey(const ValueKey('bottom-nav-2')));
      await tester.pumpAndSettle();
      expect(find.text('MERCADO & LEILÃO'), findsOneWidget);
      expect(
        tester
            .widget<ClassicBottomNavigation>(
              find.byType(ClassicBottomNavigation),
            )
            .showPlayerRankings,
        isFalse,
      );
      expect(tester.takeException(), isNull);
    },
  );
}
