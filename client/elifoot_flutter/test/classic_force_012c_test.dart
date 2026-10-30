import 'package:elifoot_flutter/player_rankings.dart';
import 'package:elifoot_flutter/main.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'complete_gameplay_011_test.dart' show FakeGameplay011ApiClient;

class IdleClassicApi extends FakeGameplay011ApiClient {
  bool usedSpectatorReady = false;
  @override
  Future<Map<String, dynamic>> getSnapshot(String worldId) async => {
    ...await super.getSnapshot(worldId),
    'forceRulesVersion': 1, 'gameDate': '1998-08-01', 'matches': <dynamic>[],
  };
  @override
  Future<Map<String, dynamic>> getClubDetail(String worldId, String clubId) async {
    final data = await super.getClubDetail(worldId, clubId);
    for (final p in data['squad'] as List<dynamic>) { p['injuryDaysRemaining'] = 15; }
    data['preferredStarterIds'] = <String>[];
    data['preferredBenchIds'] = <String>[];
    return data;
  }
  @override
  Future<Map<String, dynamic>> submitUnemployedReady({required String worldId, required String coachId, required int roundVersion}) async {
    usedSpectatorReady = true;
    return super.submitUnemployedReady(worldId: worldId, coachId: coachId, roundVersion: roundVersion);
  }
}

void main() {
  testWidgets('Classic cup bye permits ready with an injured incomplete squad', (tester) async {
    await tester.binding.setSurfaceSize(const Size(1280, 900));
    addTearDown(() => tester.binding.setSurfaceSize(null));
    final api = IdleClassicApi();
    await tester.pumpWidget(ElifootClassicApp(apiClient: api));
    await tester.tap(find.text('ENTRAR NO LOBBY (LOCAL DEV)'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('ENTRAR COM CLUBE'));
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('choose-club-club-1')));
    await tester.pumpAndSettle();
    final ready = find.byKey(const Key('btn-enviar-tatica'));
    expect(tester.widget<ElevatedButton>(ready).onPressed, isNotNull);
    await tester.tap(ready);
    await tester.pumpAndSettle();
    expect(api.usedSpectatorReady, isTrue);
    expect(find.byKey(const Key('btn-retirar-pronto')), findsOneWidget);
    expect(tester.takeException(), isNull);
    await tester.pumpWidget(const SizedBox.shrink());
  });
  for (final width in [320.0, 390.0, 1280.0]) {
    testWidgets('Adapted force history separates injury and reserve at $width', (tester) async {
      tester.view.physicalSize = Size(width, 844);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      await tester.pumpWidget(MaterialApp(home: Scaffold(body:
        SingleChildScrollView(child: PlayerStrengthHistoryPanel(data: {
          'strengthHistory': [
            {'season': 1, 'round': 3, 'clubId': 'c', 'clubName': 'Clube Histórico', 'division': 1,
              'strengthBefore': 50, 'strengthAfter': 49, 'changeReason': 'INJURY_DAILY_LOSS',
              'gameDate': '1998-08-08', 'leaguePosition': 1, 'growthCeiling': 50, 'injuryId': 'i'},
            {'season': 1, 'round': 3, 'clubId': 'c', 'clubName': 'Clube Histórico', 'division': 1,
              'strengthBefore': 49, 'strengthAfter': 50, 'changeReason': 'RESERVE_PROGRESS',
              'gameDate': '1998-08-08', 'leaguePosition': 1, 'growthCeiling': 50},
            {'season': 1, 'round': 4, 'clubId': 'c', 'clubName': 'Clube Histórico', 'division': 1,
              'strengthBefore': 50, 'strengthAfter': 49, 'changeReason': 'PLAYED_MATCH_DECLINE',
              'gameDate': '1998-08-12', 'leaguePosition': 1, 'growthCeiling': 50},
          ],
        })))));
      await tester.pumpAndSettle();
      expect(find.text('Perda diária por contusão'), findsWidgets);
      expect(find.text('Evolução sem jogar'), findsWidgets);
      expect(find.text('Queda após participação'), findsWidgets);
      expect(find.text('08/08/1998'), findsWidgets);
      expect(find.text('TETO'), findsWidgets);
      expect(tester.takeException(), isNull);
    });
  }
}
