import 'package:elifoot_flutter/main.dart';
import 'package:elifoot_flutter/presentation_version.dart';
import 'package:elifoot_flutter/presentation_widgets.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'classic_squad_bank_stadium_test.dart' show ClassicPlayerStatsPanelApi;
import 'classic_ux_014b_test.dart' show join;

class CupPanelApi extends ClassicPlayerStatsPanelApi {
  final bool withHistory;
  CupPanelApi(this.withHistory);
  @override
  Future<Map<String, dynamic>> getClubDetail(
    String worldId,
    String clubId,
  ) async {
    final club = await super.getClubDetail(worldId, clubId);
    final player = (club['squad'] as List).first as Map;
    player['seasonGoals'] = 1;
    player['cupGoals'] = 1;
    player['careerGoals'] = 1;
    return club;
  }

  @override
  Future<Map<String, dynamic>> getPlayerHistory(
    String worldId,
    String playerId,
  ) async => {
    'playerId': playerId,
    'currentSeason': 1,
    if (withHistory) 'seasonGoals': 1,
    'rows': withHistory
        ? [
            {'season': 1, 'competitionType': 'CupFinal', 'goalsScored': 1},
          ]
        : [],
  };
}

void main() {
  for (final width in [390.0, 1366.0]) {
    for (final history in [false, true]) {
      testWidgets('One cup goal appears once at $width with history=$history', (
        tester,
      ) async {
        await tester.binding.setSurfaceSize(Size(width, 1000));
        addTearDown(() => tester.binding.setSurfaceSize(null));
        final api = CupPanelApi(history);
        await join(tester, api);
        final controller = PresentationScope.maybeOf(
          tester.element(find.byType(ElifootShellScreen)),
        )!;
        await controller.select(api, PresentationVersion.classic);
        await tester.pumpAndSettle();
        final player = find.text('Atleta sintético 1985-23-7 ★').first;
        await tester.ensureVisible(player);
        await tester.tap(player);
        await tester.pumpAndSettle();
        expect(
          tester
              .widget<Text>(
                find.byKey(const ValueKey('classic-player-stat-value-1')),
              )
              .data,
          '1',
        );
        expect(tester.takeException(), isNull);
      });
    }
  }
}
