import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:elifoot_flutter/api_client.dart';
import 'package:elifoot_flutter/main.dart';
import 'package:elifoot_flutter/match_router.dart';
import 'package:elifoot_flutter/presentation_version.dart';
import 'package:elifoot_flutter/presentation_widgets.dart';
import 'automatic_round_flow_test.dart' show AutomaticRoundApi;
import 'classic_ux_014b_test.dart' show join;

class RetryPreparationApi extends AutomaticRoundApi {
  int attempts = 0;
  RetryPreparationApi() : super(1);
  @override
  Future<Map<String, dynamic>> advanceToNextRound(
    String worldId, {
    int? expectedSeasonNumber,
    int? expectedRoundNumber,
  }) {
    attempts++;
    if (attempts == 1) return Future.error(Exception('connection interrupted'));
    return super.advanceToNextRound(
      worldId,
      expectedSeasonNumber: expectedSeasonNumber,
      expectedRoundNumber: expectedRoundNumber,
    );
  }
}

void main() {
  test(
    'Automatic preparation sends the completed season and round in the request',
    () async {
      late Uri uri;
      final api = ElifootApiClient(
        httpClient: MockClient((request) async {
          uri = request.url;
          return http.Response('{}', 200);
        }),
      );
      await api.advanceToNextRound(
        'world',
        expectedSeasonNumber: 3,
        expectedRoundNumber: 18,
      );
      expect(uri.path, '/api/worlds/world/next-round');
      expect(uri.queryParameters, {
        'expectedSeasonNumber': '3',
        'expectedRoundNumber': '18',
      });
    },
  );
  for (final classic in [false, true]) {
    testWidgets(
      'Peer preparation keeps the full five seconds classic=$classic',
      (t) async {
        t.view.physicalSize = const Size(1366, 1000);
        t.view.devicePixelRatio = 1;
        addTearDown(t.view.resetPhysicalSize);
        addTearDown(t.view.resetDevicePixelRatio);
        final api = AutomaticRoundApi(2);
        await join(t, api);
        if (classic) {
          await PresentationScope.maybeOf(
            t.element(find.byType(ElifootShellScreen)),
          )!.select(api, PresentationVersion.classic);
          await t.pumpAndSettle();
        }
        final router =
            t.widget<MaterialApp>(find.byType(MaterialApp)).routerDelegate!
                as MatchRouterDelegate;
        await api.emit('RUNNING_FIRST_HALF');
        await t.pumpAndSettle();
        await api.emit('RESULTS');
        await t.pumpAndSettle();
        await t.pump(const Duration(seconds: 2));
        api.currentRound = 2;
        api.ready = 0;
        api.isReady = false;
        await api.emit('PREPARING');
        await t.pumpAndSettle();
        expect(router.currentConfiguration, '/divisoes');
        expect(find.byKey(const Key('btn-enviar-tatica')), findsNothing);
        await t.pump(const Duration(seconds: 3));
        await t.pumpAndSettle();
        expect(router.currentConfiguration, '/plantel');
        expect(find.byKey(const Key('btn-enviar-tatica')), findsOneWidget);
        expect(
          api.advances,
          isEmpty,
          reason: 'The peer already prepared this room',
        );
        await t.pumpWidget(const SizedBox());
      },
    );
  }
  testWidgets('Leaving the screen cancels the pending preparation', (t) async {
    t.view.physicalSize = const Size(1366, 1000);
    t.view.devicePixelRatio = 1;
    addTearDown(t.view.resetPhysicalSize);
    addTearDown(t.view.resetDevicePixelRatio);
    final api = AutomaticRoundApi(1);
    await join(t, api);
    await api.emit('RESULTS');
    await t.pumpAndSettle();
    await t.pumpWidget(const SizedBox());
    await t.pump(const Duration(seconds: 10));
    expect(api.advances, isEmpty);
    expect(t.takeException(), isNull);
  });
  testWidgets(
    'Preparation retries a connection failure without an advance button',
    (t) async {
      t.view.physicalSize = const Size(1366, 1000);
      t.view.devicePixelRatio = 1;
      addTearDown(t.view.resetPhysicalSize);
      addTearDown(t.view.resetDevicePixelRatio);
      final api = RetryPreparationApi();
      await join(t, api);
      await api.emit('RESULTS');
      await t.pumpAndSettle();
      await t.pump(const Duration(seconds: 5));
      await t.pumpAndSettle();
      expect(api.attempts, 1);
      expect(find.byKey(const Key('btn-proxima-rodada')), findsNothing);
      await t.pump(const Duration(seconds: 2));
      await t.pumpAndSettle();
      expect(api.attempts, 2);
      expect(api.currentRound, 2);
      expect(find.byKey(const Key('btn-enviar-tatica')), findsOneWidget);
      await t.pumpWidget(const SizedBox());
    },
  );
}
