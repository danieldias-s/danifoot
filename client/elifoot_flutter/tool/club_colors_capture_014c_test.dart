// Flutter renderer of real recorded HTTP responses. NOT a browser screenshot.
import 'dart:convert';
import 'dart:io';
import 'dart:ui' as ui;
import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:elifoot_flutter/main.dart';
import '../test/complete_gameplay_011_test.dart' show FakeGameplay011ApiClient;

Map<String, dynamic> read(String name) =>
    jsonDecode(File('../../artifacts/014c/scene-$name.json').readAsStringSync())
        as Map<String, dynamic>;

class SceneApi extends FakeGameplay011ApiClient {
  final Map<String, dynamic> scene;
  SceneApi(this.scene);
  String get owner => scene['coach'].toString();
  Map<String, dynamic> get snap =>
      Map<String, dynamic>.from(scene['snapshot'] as Map);
  @override
  Future<Map<String, dynamic>> localLogin(
    String nickname,
    String deviceId,
  ) async => {
    'coachId': owner,
    'nickname': 'Treinador 014C',
    'deviceId': 'render014c',
  };
  @override
  Future<List<dynamic>> listWorlds() async => [
    {
      'id': snap['gameWorldId'],
      'name': snap['worldName'],
      'roundState': snap['roundState'],
      'seasonNumber': snap['seasonNumber'],
      'roundNumber': snap['roundNumber'],
      'totalClubs': 128,
      'readyCounter': snap['readyCounter'],
    },
  ];
  @override
  Future<Map<String, dynamic>> getSnapshot(String worldId) async => snap;
  @override
  Future<Map<String, dynamic>> getClubDetail(String w, String c) async =>
      Map<String, dynamic>.from(scene['detail'] as Map);
  @override
  Future<List<dynamic>> getClubs(String worldId, {String? tier}) async =>
      (scene['clubs'] as List)
          .where((c) => tier == null || c['divisionTier'] == tier)
          .toList();
  @override
  Future<Map<String, dynamic>> joinWorld({
    required String worldId,
    required String coachId,
    required String nickname,
    required String deviceId,
    String? preferredClubId,
    bool joinUnemployed = false,
  }) async => {
    'worldId': snap['gameWorldId'],
    'clubId': scene['club'],
    'isUnemployed': false,
    'isHost': true,
    'snapshot': snap,
  };
  @override
  Future<Map<String, dynamic>> getAppearance(
    String o,
    String w,
    String c,
  ) async => Map<String, dynamic>.from(scene['appearance'] as Map);
  @override
  Future<Map<String, dynamic>> saveAppearance(
    String o,
    String w,
    String c,
    Map<String, dynamic> p,
    int version,
  ) async => {
    ...scene['appearance'] as Map,
    'preferences': {...p, 'version': version + 1},
  };
}

void main() {
  for (final width in [390.0, 1366.0]) {
    testWidgets('Recorded authoritative color scenes at $width', (
      tester,
    ) async {
      await tester.runAsync(() async {
        await ui.loadFontFromList(
          await File('C:/Windows/Fonts/consola.ttf').readAsBytes(),
          fontFamily: 'monospace',
        );
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
      tester.view.physicalSize = Size(width, 1000);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      final out = Directory('../../docs/audit-014c/automated-renders')
        ..createSync(recursive: true);
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
            '${out.path}/$name-${width.toInt()}.png',
          ).writeAsBytes(data!.buffer.asUint8List());
          picture.dispose();
        });
      }

      for (final name in [
        'male-red',
        'male-blue',
        'matches-red',
        'matches-blue',
        'female-red',
      ]) {
        await tester.pumpWidget(const SizedBox());
        await tester.pumpAndSettle();
        final api = SceneApi(read(name));
        await tester.pumpWidget(
          RepaintBoundary(
            key: boundaryKey,
            child: ElifootClassicApp(apiClient: api),
          ),
        );
        await tester.tap(find.text('ENTRAR NO LOBBY (LOCAL DEV)'));
        await tester.pumpAndSettle();
        await tester.tap(find.text('ENTRAR COM CLUBE'));
        await tester.pumpAndSettle();
        if (name.startsWith('matches')) {
          final group = find.textContaining('JOGOS');
          if (group.evaluate().length == 1) {
            await tester.ensureVisible(group);
            await tester.tap(group);
            await tester.pumpAndSettle();
          }
        }
        await capture(name);
        if (name == 'male-red') {
          await tester.ensureVisible(
            find.byKey(const ValueKey('appearance-menu')),
          );
          await tester.tap(find.byKey(const ValueKey('appearance-menu')));
          await tester.pumpAndSettle();
          await capture('appearance');
          await tester.tap(find.text('PERSONALIZAR CORES'));
          await tester.pumpAndSettle();
          await tester.ensureVisible(find.byIcon(Icons.palette).first);
          await tester.tap(find.byIcon(Icons.palette).first);
          await tester.pumpAndSettle();
          await capture('color-picker');
          await tester.drag(find.byType(Slider).first, const Offset(95, 0));
          await tester.pumpAndSettle();
          await capture('live-preview-picker');
          await tester.tap(find.text('CONCLUIR'));
          await tester.pumpAndSettle();
          await tester.ensureVisible(
            find.byKey(const ValueKey('appearance-preview')),
          );
          await capture('live-preview');
          await tester.pageBack();
          await tester.pumpAndSettle();
        }
      }
    });
  }
}
