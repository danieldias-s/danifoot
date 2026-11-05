import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:elifoot_flutter/main.dart';
import 'package:elifoot_flutter/history_views.dart';
import 'package:elifoot_flutter/presentation_version.dart';
import 'package:elifoot_flutter/presentation_widgets.dart';
import 'classic_ux_014b_test.dart' show join;
import 'presentation_017a_test.dart' show GameplayPresentationApi;

class HistoryApi extends GameplayPresentationApi {
  final queried = <int?>[];
  @override
  Future<Map<String, dynamic>> getClubHistory(
    String world,
    String club, {
    int? season,
  }) async {
    queried.add(season);
    return {
      'clubName': 'Atlético Verde',
      'total': {
        'games': 14,
        'wins': 8,
        'draws': 3,
        'losses': 3,
        'goalsFor': 21,
        'goalsAgainst': 12,
        'goalDifference': 9,
      },
      'seasons': [
        for (final s in season == null ? [100, 10, 1] : [season])
          {
            'season': s,
            'division': 'QuartaDivisao',
            'closed': true,
            'finalPosition': 2,
            'promoted': true,
            'total': {
              'games': 14,
              'wins': 8,
              'draws': 3,
              'losses': 3,
              'goalsFor': 21,
              'goalsAgainst': 12,
              'goalDifference': 9,
            },
            'scorers': [
              {'playerName': 'Daniel Dias', 'goals': 7},
            ],
          },
      ],
      'titles': [
        {
          'season': 1,
          'competition': 'LEAGUE',
          'division': 'QuartaDivisao',
          'runnerUp': true,
        },
      ],
      'coaches': [
        {
          'name': 'Treinador anterior',
          'human': true,
          'startSeason': 1,
          'startRound': 1,
          'endSeason': 2,
          'endRound': 3,
          'titles': 1,
          'exitReason': 'CLUB_PROJECT_UPGRADE',
        },
      ],
      'players': [
        {
          'playerName': 'Daniel Dias',
          'startSeason': 1,
          'startRound': 1,
          'endSeason': 2,
          'endRound': 4,
        },
      ],
      'transfers': [
        {
          'playerName': 'Daniel Dias',
          'season': 2,
          'round': 4,
          'fromClub': 'Atlético Verde',
          'toClub': 'Novo clube',
          'operation': 'DirectPurchase',
          'amount': 50000,
        },
      ],
      'records': {'longestWinningRun': 3, 'longestUnbeatenRun': 5},
      'coverageWarnings': [],
    };
  }
}

void main() {
  for (final classic in [true, false]) {
    for (final width in [390.0, 1366.0]) {
      testWidgets(
        'club history accessible and seasonal at $width classic=$classic',
        (t) async {
          t.view.physicalSize = Size(width, 1000);
          t.view.devicePixelRatio = 1;
          addTearDown(t.view.resetPhysicalSize);
          addTearDown(t.view.resetDevicePixelRatio);
          final api = HistoryApi();
          await join(t, api);
          if (classic) {
            await PresentationScope.maybeOf(
              t.element(find.byType(ElifootShellScreen)),
            )!.select(api, PresentationVersion.classic);
            await t.pumpAndSettle();
            if (width < 700) {
              await t.tap(find.byTooltip('Abrir menus'));
              await t.pumpAndSettle();
              await t.tap(find.text('Equipa'));
              await t.pumpAndSettle();
              await t.tap(find.text('História do clube...'));
            } else {
              await t.tap(find.text('História').last);
            }
          } else {
            await t.ensureVisible(find.text('HISTÓRIA DO CLUBE'));
            await t.tap(find.text('HISTÓRIA DO CLUBE'));
          }
          await t.pumpAndSettle();
          expect(find.byType(ClubHistoryDialog), findsOneWidget);
          expect(api.queried, [null]);
          expect(find.textContaining('100'), findsWidgets);
          expect(t.takeException(), isNull);
          await t.tap(find.byType(DropdownButtonFormField<int>));
          await t.pumpAndSettle();
          await t.tap(find.text('Temporada 1').last);
          await t.pumpAndSettle();
          expect(api.queried.last, 1);
          expect(find.textContaining('Daniel Dias'), findsWidgets);
          expect(t.takeException(), isNull);
          await t.pumpWidget(const SizedBox());
          api.disconnectRealtime();
        },
      );
    }
  }
  testWidgets(
    'player history includes club stints, assists and only known season strengths',
    (t) async {
      t.view.physicalSize = const Size(390, 1000);
      t.view.devicePixelRatio = 1;
      addTearDown(t.view.resetPhysicalSize);
      addTearDown(t.view.resetDevicePixelRatio);
      await t.pumpWidget(
        const MaterialApp(
          home: Scaffold(
            body: SingleChildScrollView(
              child: PlayerCareerHistoryPanels(
                data: {
                  'clubCareer': [
                    {
                      'clubName': 'Clube anterior',
                      'startSeason': 1,
                      'startRound': 1,
                      'endSeason': 2,
                      'endRound': 4,
                      'clubId': 'old',
                    },
                  ],
                  'seasonStrengths': [
                    {'season': 1, 'initialStrength': null, 'finalStrength': 27},
                  ],
                  'seasonTotals': [
                    {
                      'season': 1,
                      'games': 14,
                      'goals': 4,
                      'assists': 6,
                      'minutes': 920,
                      'partial': false,
                      'incompleteAssists': false,
                    },
                  ],
                  'career': {'assists': 6},
                  'highestRecordedStrength': 27,
                  'mostGoalsInSeason': 4,
                  'mostGamesInSeason': 14,
                  'scoringRecognitions': [
                    {'season': 1, 'scope': 'division4', 'goals': 4},
                  ],
                },
              ),
            ),
          ),
        ),
      );
      expect(find.textContaining('Assistências: 6'), findsOneWidget);
      expect(
        find.textContaining('Força inicial: Não registrado'),
        findsOneWidget,
      );
      expect(find.textContaining('Clube anterior'), findsOneWidget);
      expect(t.takeException(), isNull);
    },
  );
}
