// Flutter renderer of recorded real API responses. These PNGs are not Chrome screenshots.
// Local auth/join are only navigation adapters and never write to the real server.
import 'dart:convert';
import 'dart:io';
import 'dart:ui' as ui;
import 'package:elifoot_flutter/api_client.dart';
import 'package:elifoot_flutter/main.dart';
import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

class RecordedRankingApi extends ElifootApiClient {
  RecordedRankingApi(Map<String, dynamic> scene)
    : super(
        httpClient: MockClient((request) async {
          final path = request.url.path;
          dynamic value;
          if (path.endsWith('/auth/local-login')) {
            value = scene['auth'];
          } else if (path == '/api/worlds') {
            value = [scene['world']];
          } else if (path.endsWith('/join')) {
            value = {
              'coachId': scene['coach']['coachId'],
              'nickname': scene['coach']['nickname'],
              'clubId': scene['coach']['clubId'],
              'isUnemployed': scene['coach']['isUnemployed'],
              'snapshot': scene['snapshot'],
            };
          } else if (path.endsWith('/snapshot')) {
            value = scene['snapshot'];
          } else if (path.endsWith('/player-rankings')) {
            final q = request.url.queryParameters;
            value =
                scene['rankings']['${q['category']}|${q['scope']}|${q['period']}'];
          } else if (path.endsWith('/season-top-scorers')) {
            value = request.url.queryParameters.containsKey('season')
                ? scene['awardsSelected']
                : scene['awards'];
          } else if (path.contains('/appearance/')) {
            value = scene['appearance'];
          } else if (path.contains('/standings/')) {
            value = scene['standings'];
          } else if (path.endsWith('/calendar')) {
            value = scene['calendar'];
          } else if (path.endsWith('/clubs')) {
            value = scene['clubs'];
          } else if (path.endsWith('/finances')) {
            value = scene['auxiliary']['finances'];
          } else if (path.contains('/clubs/')) {
            value = scene['detail'];
          } else if (path.endsWith('/cup')) {
            value = scene['auxiliary']['cup'];
          } else if (path.endsWith('/top-scorers')) {
            value = scene['auxiliary']['topScorers'];
          } else if (path.endsWith('/market')) {
            value = scene['auxiliary']['market'];
          } else if (path.endsWith('/ranking')) {
            value = scene['auxiliary']['coachRanking'];
          } else if (path.contains('/invitations/')) {
            value = scene['auxiliary']['invitations'];
          } else {
            throw StateError('No recorded response for ${request.url}');
          }
          if (value == null) {
            throw StateError('Missing recorded response for ${request.url}');
          }
          return http.Response(
            jsonEncode(value),
            200,
            headers: {'content-type': 'application/json; charset=utf-8'},
          );
        }),
      );
  @override
  void connectRealtimeRoom(String world, String coach) {}
  @override
  void disconnectRealtime() {}
}

void main() {
  for (final width in [360.0, 390.0, 430.0, 768.0, 1366.0, 1920.0]) {
    testWidgets('Recorded official player rankings render at $width', (
      tester,
    ) async {
      final height = width < 700 ? 1400.0 : 1000.0;
      await tester.binding.setSurfaceSize(Size(width, height));
      addTearDown(() => tester.binding.setSurfaceSize(null));
      await tester.runAsync(() async {
        final fonts = FontLoader('ClassicCondensed');
        for (final name in ['Regular', 'Bold', 'BoldItalic']) {
          fonts.addFont(
            File(
              'assets/fonts/BarlowCondensed-$name.ttf',
            ).readAsBytes().then((bytes) => ByteData.sublistView(bytes)),
          );
        }
        await fonts.load();
        await ui.loadFontFromList(
          await File('C:/Windows/Fonts/arial.ttf').readAsBytes(),
          fontFamily: 'Roboto',
        );
        await ui.loadFontFromList(
          await File(
            'C:/flutter/bin/cache/artifacts/material_fonts/MaterialIcons-Regular.otf',
          ).readAsBytes(),
          fontFamily: 'MaterialIcons',
        );
      });
      final scene =
          jsonDecode(
                File(
                  '../../artifacts/016b/player-rankings/real-api-scene.json',
                ).readAsStringSync(),
              )
              as Map<String, dynamic>;
      final output = Directory(
        '../../docs/player-rankings-visual-016b/flutter-renders',
      )..createSync(recursive: true);
      final boundaryKey = GlobalKey();
      Future<void> capture(String name) async {
        await tester.pumpAndSettle();
        expect(tester.takeException(), isNull);
        final boundary = tester.renderObject<RenderRepaintBoundary>(
          find.byKey(boundaryKey),
        );
        await tester.runAsync(() async {
          final picture = await boundary.toImage(pixelRatio: 1);
          final data = await picture.toByteData(format: ui.ImageByteFormat.png);
          await File(
            '${output.path}/$name-${width.toInt()}.png',
          ).writeAsBytes(data!.buffer.asUint8List());
          picture.dispose();
        });
      }

      await tester.pumpWidget(
        RepaintBoundary(
          key: boundaryKey,
          child: ElifootClassicApp(apiClient: RecordedRankingApi(scene)),
        ),
      );
      await tester.tap(find.text('ENTRAR NO LOBBY (LOCAL DEV)'));
      await tester.pumpAndSettle();
      await tester.ensureVisible(find.text('ENTRAR COM CLUBE'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('ENTRAR COM CLUBE'));
      await tester.pumpAndSettle();
      if (width < 1024) {
        await tester.tap(find.byKey(const Key('open-navigation')));
        await tester.pumpAndSettle();
      }
      await tester.ensureVisible(find.byKey(const ValueKey('shell-nav-6')));
      await tester.tap(find.byKey(const ValueKey('shell-nav-6')));
      await tester.pumpAndSettle();
      expect(
        find.text(
          'Nenhum jogador elegível com estatísticas oficiais neste recorte.',
        ),
        findsOneWidget,
      );
      await capture('rankings-empty');
      if (width == 390 || width == 1366) {
        await tester.ensureVisible(
          find.byKey(const ValueKey('ranking-category')),
        );
        await tester.tap(find.byKey(const ValueKey('ranking-category')));
        await tester.pumpAndSettle();
        await capture('category-open');
        await tester.tap(find.text('Goleiros').last);
        await tester.pumpAndSettle();
        await capture('goalkeepers');
        await tester.tap(find.byKey(const ValueKey('ranking-scope')));
        await tester.pumpAndSettle();
        await capture('scope-open');
        await tester.tap(find.text('Taça').last);
        await tester.pumpAndSettle();
        await tester.tap(find.byKey(const ValueKey('ranking-period')));
        await tester.pumpAndSettle();
        await capture('period-open');
        await tester.tap(find.text('Temporada selecionada').last);
        await tester.pumpAndSettle();
        await capture('selected-season');
        await tester.tap(find.byKey(const ValueKey('ranking-category')));
        await tester.pumpAndSettle();
        await tester.tap(find.text('Defensores').last);
        await tester.pumpAndSettle();
        await capture('defenders');
        await tester.ensureVisible(find.byKey(const Key('ranking-refresh')));
        await tester.tap(find.byKey(const Key('ranking-refresh')));
        await tester.pumpAndSettle();
      }
      if (width < 1024) {
        await tester.ensureVisible(
          find.byKey(const Key('player-rankings-information')),
        );
        await tester.pumpAndSettle();
        await capture('rankings-bottom');
      }
      await tester.pumpWidget(const SizedBox());
      await tester.pumpAndSettle();
    });
  }
}
