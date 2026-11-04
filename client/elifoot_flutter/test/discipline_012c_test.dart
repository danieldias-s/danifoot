import 'package:elifoot_flutter/main.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'complete_gameplay_011_test.dart' show FakeGameplay011ApiClient;

class ShortHandedApi extends FakeGameplay011ApiClient {
  List<String>? submitted;
  @override
  Future<Map<String, dynamic>> getClubDetail(
    String worldId,
    String clubId,
  ) async {
    final data = await super.getClubDetail(worldId, clubId);
    for (final p in data['squad'] as List<dynamic>) {
      final number = int.parse(p['id'].toString().split('-').last);
      if (number == 4 || number > 11) p['suspensionRoundsRemaining'] = 2;
    }
    data['requiredStarterCount'] = 10;
    data['preferredStarterIds'] = List.generate(
      11,
      (i) => 'p-${i + 1}',
    ).where((id) => id != 'p-4').toList();
    data['preferredBenchIds'] = <String>[];
    return data;
  }

  @override
  Future<Map<String, dynamic>> getSnapshot(String worldId) async {
    final data = await super.getSnapshot(worldId);
    for (final m in data['matches'] as List<dynamic>) {
      m['homePlayersOnPitch'] = 10;
      m['awayPlayersOnPitch'] = 11;
    }
    return data;
  }

  @override
  Future<Map<String, dynamic>> submitTactics({
    required String worldId,
    required String coachId,
    required String formation,
    required List<String> starterIds,
    required List<String> benchIds,
    required int roundVersion,
  }) async {
    submitted = starterIds;
    return super.submitTactics(
      worldId: worldId,
      coachId: coachId,
      formation: formation,
      starterIds: starterIds,
      benchIds: benchIds,
      roundVersion: roundVersion,
    );
  }
}

void main() {
  for (final width in [390.0, 1280.0]) {
    testWidgets(
      'Server allows ten eligible starters and shows real players on pitch at $width',
      (tester) async {
        tester.view.physicalSize = Size(width, 900);
        tester.view.devicePixelRatio = 1;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);
        final api = ShortHandedApi();
        await tester.pumpWidget(
          ElifootClassicApp(accountLogin: false, apiClient: api),
        );
        await tester.tap(find.text('ENTRAR NO LOBBY'));
        await tester.pumpAndSettle();
        await tester.ensureVisible(find.text('ENTRAR COM CLUBE'));
        await tester.pumpAndSettle();
        await tester.tap(find.text('ENTRAR COM CLUBE'));
        await tester.pumpAndSettle();
        await tester.tap(find.byKey(const Key('choose-club-club-1')));
        await tester.pumpAndSettle();
        final ready = find.byKey(const Key('btn-enviar-tatica'));
        expect(tester.widget<ElevatedButton>(ready).onPressed, isNotNull);
        expect(find.text('EM INFERIORIDADE (10/11 APTOS)'), findsOneWidget);
        await tester.ensureVisible(ready);
        await tester.tap(ready);
        await tester.pumpAndSettle();
        expect(api.submitted, hasLength(10));
        expect(api.submitted, isNot(contains('p-4')));
        expect(find.textContaining('10 em campo'), findsWidgets);
        expect(find.textContaining('11 em campo'), findsWidgets);
        expect(tester.takeException(), isNull);
        await tester.pumpWidget(const SizedBox.shrink());
      },
    );
  }
}
