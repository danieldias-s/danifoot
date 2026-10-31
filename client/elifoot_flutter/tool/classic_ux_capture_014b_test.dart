// Automated Flutter renderer using frozen, authenticated responses recorded from the local API.
// These images are explicitly not Chrome screenshots or a human visual audit.
import 'dart:convert';
import 'dart:io';
import 'dart:ui' as ui;
import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:elifoot_flutter/main.dart';
import 'package:elifoot_flutter/api_client.dart';
import 'package:elifoot_flutter/custom_assembly_editor.dart';
import 'package:elifoot_flutter/world_creator.dart';
import '../test/complete_gameplay_011_test.dart' show FakeGameplay011ApiClient;

Map<String, dynamic> read(String name) =>
    jsonDecode(File('../../artifacts/014b/$name').readAsStringSync())
        as Map<String, dynamic>;

class RecordedSceneApi extends FakeGameplay011ApiClient {
  Map<String, dynamic> scene;
  RecordedSceneApi(this.scene);
  String get owner => scene['coach'].toString();
  Map<String, dynamic> get snap =>
      Map<String, dynamic>.from(scene['snapshot'] as Map);
  @override
  Future<Map<String, dynamic>> localLogin(
    String nickname,
    String deviceId,
  ) async => {
    'coachId': owner,
    'nickname': 'Treinador 014B',
    'deviceId': 'captura014b',
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
  Future<Map<String, dynamic>> getClubDetail(
    String worldId,
    String clubId,
  ) async => Map<String, dynamic>.from(scene['detail'] as Map);
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
}

void main() {
  for (final width in [390.0, 1366.0]) {
    testWidgets('Record actual server scenes at $width', (tester) async {
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
      const height = 1000.0;
      tester.view.physicalSize = Size(width, height);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      final boundaryKey = GlobalKey();
      final out = Directory('../../docs/audit-014b/automated-renders')
        ..createSync(recursive: true);
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

      final api = RecordedSceneApi(read('ui-scene-waiting.json'));
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
      expect(find.byKey(const ValueKey('ready-waiting-panel')), findsOneWidget);
      await capture('waiting-1-of-2');
      for (final label in ['cup', 'league', 'pause', 'results']) {
        api.scene = read('ui-scene-$label.json');
        api.onSnapshotUpdated?.call(api.snap);
        await tester.pumpAndSettle();
        if (label == 'cup') {
          final group = find.textContaining('JOGOS');
          if (group.evaluate().length == 1) {
            await tester.ensureVisible(group);
            await tester.tap(group);
            await tester.pumpAndSettle();
          }
        }
        await capture(label);
        if (label == 'results') {
          final own = find.byKey(
            const ValueKey('coach-highlighted-match-card'),
          );
          await tester.ensureVisible(own);
          await tester.tap(own);
          await tester.pumpAndSettle();
          await capture('result-events');
        }
        if (label == 'league') {
          final fourth = find.byKey(
            const ValueKey('division-heading-4ª DIVISÃO'),
          );
          await tester.ensureVisible(fourth);
          await capture('league-lower-divisions');
        }
      }
      final classicTheme = Theme.of(
        tester.element(find.byType(ElifootShellScreen)),
      );
      final a = Map<String, dynamic>.from(
        read('custom-ui-Female.json')['assembly'] as Map,
      );
      final editorApi = ElifootApiClient(
        httpClient: MockClient((r) async => http.Response(jsonEncode(a), 200)),
      );
      await tester.pumpWidget(
        RepaintBoundary(
          key: boundaryKey,
          child: MaterialApp(
            debugShowCheckedModeBanner: false,
            theme: classicTheme,
            home: CustomAssemblyEditor(
              api: editorApi,
              coachId: api.owner,
              assembly: a,
              countryOptions: const [
                {'code': 'BR', 'name': 'Brasil'},
                {'code': 'PT', 'name': 'Portugal'},
              ],
            ),
          ),
        ),
      );
      await capture('pre-save-editor');
      final clubName = ((a['package'] as Map)['clubs'] as List).first['name']
          .toString();
      await tester.tap(find.text(clubName));
      await tester.pumpAndSettle();
      await capture('pre-save-roster');
      final presetApi = ElifootApiClient(
        httpClient: MockClient((r) async {
          dynamic response;
          if (r.url.path.endsWith('/countries')) {
            response = [
              {
                'code': 'BR',
                'name': 'Brasil',
                'maleClubs': 0,
                'femaleClubs': 0,
              },
              {
                'code': 'PT',
                'name': 'Portugal',
                'maleClubs': 0,
                'femaleClubs': 0,
              },
            ];
          } else if (r.url.path.endsWith('/last-configuration')) {
            response = null;
          } else if (r.url.path.endsWith('/assemblies')) {
            response = [a];
          } else {
            response = a;
          }
          return http.Response(jsonEncode(response), 200);
        }),
      );
      await tester.pumpWidget(
        RepaintBoundary(
          key: boundaryKey,
          child: MaterialApp(
            debugShowCheckedModeBanner: false,
            theme: classicTheme,
            home: WorldCreatorScreen(api: presetApi, coachId: api.owner),
          ),
        ),
      );
      await tester.pumpAndSettle();
      await tester.tap(find.text('USAR MONTAGEM SALVA'));
      await tester.pumpAndSettle();
      await tester.tap(find.textContaining('${a['name']} • FEMININO'));
      await tester.pumpAndSettle();
      await capture('saved-assembly-restored');
      expect(tester.takeException(), isNull);
    });
  }
}
