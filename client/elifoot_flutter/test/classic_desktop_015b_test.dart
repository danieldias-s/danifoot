import 'package:elifoot_flutter/classic_layout.dart';
import 'package:elifoot_flutter/classic_match_board.dart';
import 'package:elifoot_flutter/club_theme.dart';
import 'package:elifoot_flutter/morale_bar.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'classic_ux_014b_test.dart' show WaitingRoomApi, join;

class Desktop015BApi extends WaitingRoomApi {
  final divisions = <String>[];
  Desktop015BApi() : super(2);
  @override
  Future<Map<String, dynamic>> getSnapshot(String worldId) async => {
    ...await super.getSnapshot(worldId),
    'gameDate': '1999-02-03',
  };
  @override
  Future<List<dynamic>> getStandings(String worldId, String tier) {
    divisions.add(tier);
    return super.getStandings(worldId, tier);
  }

  @override
  Future<List<dynamic>> getCalendar(String worldId) async => [
    ...await super.getCalendar(worldId),
    {
      'roundNumber': 3,
      'competition': 'Taça',
      'fixtures': [
        {
          'home': 'Outro clube do payload',
          'away': 'Adversário do payload',
          'isFinished': false,
        },
      ],
    },
  ];
  @override
  Future<Map<String, dynamic>> getPlayerRankings(
    String worldId, {
    String category = 'scorers',
    String scope = 'general',
    String period = 'career',
    int? season,
  }) async => {
    'currentSeason': 1,
    'seasons': [1],
    'rows': [
      {
        'rank': 1,
        'playerId': 'fixture-player',
        'displayName': 'Jogador do payload de teste',
        'currentClubId': 'club-1',
        'currentClubName': 'Clube do payload',
        'matchesPlayed': 24,
        'goalsScored': 30,
        'average': 1.25,
      },
    ],
  };
}

Future<void> select015B(WidgetTester tester, int index) async {
  if (find.byKey(ValueKey('shell-nav-$index')).evaluate().isEmpty) {
    await tester.tap(find.byKey(const Key('open-navigation')));
    await tester.pumpAndSettle();
  }
  await tester.ensureVisible(find.byKey(ValueKey('shell-nav-$index')));
  await tester.tap(find.byKey(ValueKey('shell-nav-$index')));
  await tester.pumpAndSettle();
}

void main() {
  for (final width in [360.0, 390.0, 768.0, 1024.0, 1440.0, 1920.0]) {
    testWidgets('015B real shell, navigation and seven screens at $width', (
      tester,
    ) async {
      await tester.binding.setSurfaceSize(Size(width, 1000));
      addTearDown(() => tester.binding.setSurfaceSize(null));
      final api = Desktop015BApi();
      await join(tester, api);
      expect(find.byType(AppHeader), findsOneWidget);
      expect(
        find.descendant(
          of: find.byType(AppHeader),
          matching: find.text('03/02/1999'),
        ),
        findsOneWidget,
      );
      expect(
        find.byType(AppSidebar),
        width >= 1024 ? findsOneWidget : findsNothing,
      );
      expect(find.byType(MoraleBar), findsWidgets);
      expect(find.textContaining('MORAL DO CARGO'), findsNothing);
      expect(find.textContaining('EDITOR & PACOTES'), findsNothing);
      expect(find.text('2. JOGOS AO VIVO'), findsNothing);
      expect(
        find.byKey(const Key('desktop-tactics-column')),
        width >= 1440 ? findsOneWidget : findsNothing,
      );
      expect(tester.takeException(), isNull, reason: 'squad $width');
      for (int tab = 1; tab < 7; tab++) {
        await select015B(tester, tab);
        expect(tester.takeException(), isNull, reason: 'screen $tab at $width');
        if (tab == 1) {
          expect(find.byKey(const ValueKey('standings-table')), findsOneWidget);
        }
        if (tab == 2) {
          expect(find.byKey(const ValueKey('market-tab-0')), findsOneWidget);
          expect(find.byKey(const ValueKey('market-tab-1')), findsOneWidget);
        }
        if (tab == 5) {
          expect(find.byKey(const Key('calendar-filters')), findsOneWidget);
          expect(find.text('JOGAR'), findsNothing);
          expect(find.text('FINALIZADO'), findsWidgets);
          expect(
            find.byKey(const ValueKey('calendar-round-1')),
            findsOneWidget,
          );
        }
        if (tab == 6) {
          expect(find.byKey(const ValueKey('ranking-table')), findsOneWidget);
        }
      }
      await tester.pumpWidget(const SizedBox());
    });
  }
  for (final width in [390.0, 1440.0]) {
    testWidgets(
      '015B sidebar/drawer cannot escape server match lock at $width',
      (tester) async {
        await tester.binding.setSurfaceSize(Size(width, 1000));
        addTearDown(() => tester.binding.setSurfaceSize(null));
        final api = Desktop015BApi();
        await join(tester, api);
        await select015B(tester, 2);
        if (width < 1024) {
          await tester.tap(find.byKey(const Key('open-navigation')));
          await tester.pumpAndSettle();
          expect(find.byType(AppSidebar), findsOneWidget);
        }
        await api.emit('RUNNING_FIRST_HALF');
        await tester.pumpAndSettle();
        expect(find.byType(ClassicMatchBoard), findsOneWidget);
        expect(find.byKey(const Key('open-navigation')), findsNothing);
        if (width < 1024) {
          expect(
            find.byType(AppSidebar),
            findsNothing,
            reason: 'The open drawer must close automatically at kickoff.',
          );
        }

        if (width >= 1024) {
          for (int i = 0; i < 7; i++) {
            expect(
              tester
                  .widget<InkWell>(find.byKey(ValueKey('shell-nav-$i')))
                  .onTap,
              isNull,
            );
          }
        }
        await api.emit('RESULTS');
        await tester.pumpAndSettle();
        await select015B(tester, 5);
        expect(find.byKey(const Key('calendar-filters')), findsOneWidget);
        expect(tester.takeException(), isNull);
        await tester.pumpWidget(const SizedBox());
      },
    );
  }
  testWidgets(
    '015B division tabs fetch authoritative data and calendar keeps active filter',
    (tester) async {
      await tester.binding.setSurfaceSize(const Size(1440, 1000));
      addTearDown(() => tester.binding.setSurfaceSize(null));
      final api = Desktop015BApi();
      await join(tester, api);
      await select015B(tester, 1);
      await tester.tap(
        find.byKey(const ValueKey('btn-division-QuartaDivisao')),
      );
      await tester.pumpAndSettle();
      expect(api.divisions.last, 'QuartaDivisao');
      await select015B(tester, 5);
      await tester.tap(find.text('RODADA ATUAL (R1)'));
      await tester.pumpAndSettle();
      final tabs = tester.widget<ClassicTabs>(
        find.byKey(const Key('calendar-filters')),
      );
      expect(tabs.selected, 'CURRENT');
      expect(find.byKey(const ValueKey('calendar-round-2')), findsNothing);
      await tester.tap(find.text('MEU CLUBE (Atlético Verde)'));
      await tester.pumpAndSettle();
      expect(find.text('Seu clube não disputa esta rodada.'), findsOneWidget);
      expect(tester.takeException(), isNull);
    },
  );
  test('015B classic design tokens remain legible', () {
    final t = ClassicTheme.classicGreen();
    expect(t.appBackground, const Color(0xFF002E20));
    expect(t.accentYellow, const Color(0xFFFFD400));
    for (final bg in [
      t.appBackground,
      t.sidebarBackground,
      t.panelBackground,
      t.tableHeader,
    ]) {
      expect(colorContrast(t.primaryText, bg), greaterThanOrEqualTo(4.5));
      expect(colorContrast(t.accentYellow, bg), greaterThanOrEqualTo(4.5));
    }
  });
}
