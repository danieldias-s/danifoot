// Flutter renderer of recorded GET responses from the isolated 015B API.
// NOT a Chrome/device screenshot. Does not mutate the room or contact the API.
import 'dart:convert';
import 'dart:io';
import 'dart:ui' as ui;
import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:elifoot_flutter/api_client.dart';
import 'package:elifoot_flutter/main.dart';

class RecordedMobileApi extends ElifootApiClient {
  RecordedMobileApi(Map<String, dynamic> scene)
    : super(
        baseUrl: 'http://recorded.invalid',
        httpClient: MockClient((request) async {
          final world = scene['snapshot']['gameWorldId'];
          final club = scene['club'];
          final coach = scene['coach'];
          final root = '/api/worlds/$world';
          final recorded = <String, dynamic>{
            '/api/worlds': [
              {
                'id': world,
                'name': scene['snapshot']['worldName'],
                'seasonNumber': scene['snapshot']['seasonNumber'],
                'roundNumber': scene['snapshot']['roundNumber'],
                'roundState': scene['snapshot']['roundState'],
                'totalClubs': (scene['clubs'] as List).length,
                'readyCounter': scene['snapshot']['readyCounter'],
              },
            ],
            '$root/snapshot': scene['snapshot'],
            '$root/clubs': scene['clubs'],
            '$root/clubs/$club': scene['detail'],
            '$root/clubs/$club/finances': scene['finances'],
            '$root/invitations/$coach': scene['invitations'],
            '/api/world-creator/appearance/$world/$club': scene['appearance'],
            for (final key in ['calendar', 'cup', 'market', 'ranking'])
              '$root/$key': scene[key],
            '$root/top-scorers': scene['scorers'],
            for (final tier in (scene['standings'] as Map).keys)
              '$root/standings/$tier': scene['standings'][tier],
          };
          dynamic data;
          if (request.method == 'GET' &&
              recorded.containsKey(request.url.path)) {
            data = recorded[request.url.path];
          } else if (request.method == 'POST' &&
              request.url.path == '/api/auth/local-login') {
            data = {
              'coachId': coach,
              'nickname': scene['nickname'],
              'deviceId': 'recorded-render-015b',
            };
          } else if (request.method == 'POST' &&
              request.url.path == '$root/join') {
            data = {
              'worldId': world,
              'clubId': club,
              'isUnemployed': false,
              'snapshot': scene['snapshot'],
            };
          } else {
            throw StateError(
              'Unrecorded request: ${request.method} ${request.url.path}',
            );
          }
          return http.Response(
            jsonEncode(data),
            200,
            headers: {'content-type': 'application/json; charset=utf-8'},
          );
        }),
      );
  @override
  void connectRealtimeRoom(String worldId, String coachId) {}
  @override
  void disconnectRealtime() {}
}

void main() {
  testWidgets('Recorded real 015B mobile scenes', (tester) async {
    await tester.runAsync(() async {
      for (final font in ['roboto-regular.ttf', 'roboto-bold.ttf']) {
        await ui.loadFontFromList(
          await File(
            'C:/flutter/bin/cache/artifacts/material_fonts/$font',
          ).readAsBytes(),
          fontFamily: 'Roboto',
        );
      }
      await ui.loadFontFromList(
        await File(
          'C:/flutter/bin/cache/artifacts/material_fonts/materialicons-regular.otf',
        ).readAsBytes(),
        fontFamily: 'MaterialIcons',
      );
    });
    final scene =
        jsonDecode(
              File('../../artifacts/015b/mobile-scene.json').readAsStringSync(),
            )
            as Map<String, dynamic>;
    final out = Directory('../../docs/evidence-015b-mobile/automated-renders')
      ..createSync(recursive: true);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetDevicePixelRatio);
    addTearDown(tester.view.resetPhysicalSize);
    for (final size in [
      const Size(390, 844),
      const Size(390, 1800),
      const Size(1440, 1000),
    ]) {
      tester.view.physicalSize = size;
      await tester.pumpWidget(const SizedBox());
      await tester.pumpAndSettle();
      final key = GlobalKey();
      await tester.pumpWidget(
        RepaintBoundary(
          key: key,
          child: ElifootClassicApp(apiClient: RecordedMobileApi(scene)),
        ),
      );
      await tester.enterText(
        find.byType(TextField).first,
        scene['nickname'] as String,
      );
      final login = find.text('ENTRAR NO LOBBY (LOCAL DEV)');
      await tester.ensureVisible(login);
      await tester.tap(login);
      await tester.pumpAndSettle();
      final enter = find.text('ENTRAR COM CLUBE');
      await tester.ensureVisible(enter);
      await tester.tap(enter);
      await tester.pumpAndSettle();
      Future<void> capture(String label) async {
        await tester.pumpAndSettle();
        expect(tester.takeException(), isNull);
        final boundary = tester.renderObject<RenderRepaintBoundary>(
          find.byKey(key),
        );
        await tester.runAsync(() async {
          final picture = await boundary.toImage(pixelRatio: 2);
          final bytes = await picture.toByteData(
            format: ui.ImageByteFormat.png,
          );
          await File(
            '${out.path}/$label-${size.width.toInt()}x${size.height.toInt()}.png',
          ).writeAsBytes(bytes!.buffer.asUint8List());
          picture.dispose();
        });
      }

      await capture('plantel');
      if (size.width < 768) {
        final filter = find.byKey(const Key('mobile-squad-filters'));
        await tester.ensureVisible(filter);
        await tester.tap(filter);
        await tester.pumpAndSettle();
        await capture('filtros');
        await tester.tap(filter);
        await tester.pumpAndSettle();
      }

      if (size.width < 768) {
        await tester.tap(find.byKey(const Key('mobile-nav-1')));
        await tester.pumpAndSettle();
        await capture('taticas');
      }
    }
    await tester.pumpWidget(const SizedBox());
  });
}
