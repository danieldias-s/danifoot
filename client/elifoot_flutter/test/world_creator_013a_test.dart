import 'dart:convert';
import 'dart:io';
import 'dart:ui' as ui;
import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:elifoot_flutter/api_client.dart';
import 'package:elifoot_flutter/world_creator.dart';

void main() {
  final countries = ['BR', 'AR', 'GB', 'ES', 'DE', 'IT', 'FR', 'PT'];
  final names = [
    'Brasil',
    'Argentina',
    'Inglaterra',
    'Espanha',
    'Alemanha',
    'Itália',
    'França',
    'Portugal',
  ];
  for (final width in [390.0, 1366.0]) {
    testWidgets(
      'World creator completes female flow and preserves ownership at width $width',
      (tester) async {
        tester.view.physicalSize = Size(width, 900);
        tester.view.devicePixelRatio = 1;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);
        String gender = 'Male';
        Map<String, dynamic>? confirmed;
        Map<String, dynamic> club(int i) => {
          'key': 'club-$i',
          'name': 'Clube fictício $i',
          'countryName': names[i % 8],
          'rank': i + 1,
          'isFictional': false,
          'players': List.generate(
            18,
            (p) => {
              'playerName': 'Atleta $i-$p',
              'position': p < 2 ? 'G' : 'M',
              'strength': 30,
              'source': 'Fixture',
              'updatedAt': '2026-10-01',
            },
          ),
        };
        final api = ElifootApiClient(
          httpClient: MockClient((request) async {
            expect(
              request.headers['Authorization'],
              'Bearer localdev-11111111111111111111111111111111',
            );
            dynamic response;
            if (request.url.path.endsWith('/countries')) {
              response = List.generate(
                8,
                (i) => {
                  'code': countries[i],
                  'name': names[i],
                  'maleClubs': 20,
                  'femaleClubs': 20,
                },
              );
            } else if (request.url.path.endsWith('/last-configuration')) {
              response = null;
            } else if (request.url.path.endsWith('/catalog')) {
              final body = jsonDecode(request.body) as Map;
              gender = body['gender'].toString();
              expect(body['countries'], hasLength(8));
              response = {
                'clubs': List.generate(160, club),
                'references': [
                  {
                    'scope': {
                      'countryCode': 'BR',
                      'dataType': 'Clubs',
                      'provider': 'Fixture',
                    },
                    'status': 'Saved',
                    'updatedAt': '2026-10-01',
                    'version': 'v1',
                    'isFixture': true,
                  },
                ],
                'warnings': ['FIXTURES — dados fictícios'],
              };
            } else if (request.url.path.endsWith('/preview')) {
              expect(
                (jsonDecode(request.body) as Map)['allowFictionalTeams'],
                false,
              );
              response = {
                'draftId': 'draft013a',
                'errors': [],
                'warnings': ['FIXTURES — dados fictícios'],
                'pendingSlots': 0,
                'clubs': List.generate(
                  128,
                  (i) => {'division': i < 32 ? i ~/ 8 + 1 : 5, 'club': club(i)},
                ),
              };
            } else if (request.url.path.endsWith('/confirm/draft013a')) {
              confirmed = {'gender': gender};
              response = {'id': 'world013a'};
            } else {
              return http.Response('Unexpected ${request.url}', 500);
            }
            return http.Response.bytes(
              utf8.encode(jsonEncode(response)),
              200,
              headers: {'content-type': 'application/json; charset=utf-8'},
            );
          }),
        );
        if (const bool.fromEnvironment("CAPTURE_013A")) {
          await tester.runAsync(() async {
            await ui.loadFontFromList(
              await File(
                "C:/flutter/bin/cache/artifacts/material_fonts/MaterialIcons-Regular.otf",
              ).readAsBytes(),
              fontFamily: "MaterialIcons",
            );
            await ui.loadFontFromList(
              await File("C:/Windows/Fonts/arial.ttf").readAsBytes(),
              fontFamily: "Creator013A",
            );
          });
        }
        const boundaryKey = Key('creator-capture');
        await tester.pumpWidget(
          MaterialApp(
            theme: ThemeData(
              brightness: Brightness.dark,
              fontFamily: const bool.fromEnvironment("CAPTURE_013A")
                  ? "Creator013A"
                  : null,
            ),
            home: RepaintBoundary(
              key: boundaryKey,
              child: WorldCreatorScreen(
                api: api,
                coachId: '11111111-1111-1111-1111-111111111111',
              ),
            ),
          ),
        );
        await tester.pumpAndSettle();
        expect(tester.takeException(), isNull);
        await tester.tap(find.text('MASCULINO'));
        await tester.pumpAndSettle();
        await tester.tap(find.text('FEMININO').last);
        await tester.pumpAndSettle();
        Future<void> next() async {
          await tester.tap(find.text('CONTINUAR'));
          await tester.pumpAndSettle();
          expect(tester.takeException(), isNull);
        }

        await next();
        expect(find.text('8 países selecionados'), findsOneWidget);
        final search = find.byType(TextField);
        await tester.enterText(search, 'Brasil');
        await tester.pump();
        expect(find.widgetWithText(CheckboxListTile, 'Brasil'), findsOneWidget);
        expect(find.text('Argentina'), findsNothing);
        await tester.enterText(search, '');
        await tester.pump();
        await next();
        await next();
        expect(find.textContaining('160 clubes disponíveis'), findsOneWidget);
        await next();
        expect(find.textContaining('AUTORIZO completar'), findsOneWidget);
        await next();
        expect(find.textContaining('FIXTURES'), findsOneWidget);
        await next();
        expect(find.text('128/128 clubes • 0 vagas pendentes'), findsOneWidget);
        if (const bool.fromEnvironment('CAPTURE_013A')) {
          final boundary = tester.renderObject<RenderRepaintBoundary>(
            find.byKey(boundaryKey),
          );
          await tester.runAsync(() async {
            final picture = await boundary.toImage();
            final bytes = await picture.toByteData(
              format: ui.ImageByteFormat.png,
            );
            final path =
                '../../docs/audit-013a/world-creator-${width.toInt()}.png';
            await File(path).writeAsBytes(bytes!.buffer.asUint8List());
            picture.dispose();
          });
        }
        await next();
        await tester.ensureVisible(find.text('CONFIRMAR CRIAÇÃO'));
        await tester.tap(find.text('CONFIRMAR CRIAÇÃO'));
        await tester.pumpAndSettle();
        expect(confirmed?['gender'], 'Female');
        expect(tester.takeException(), isNull);
        api.disconnectRealtime();
      },
    );
  }
  testWidgets(
    'World creator reports unavailable catalog and keeps navigation usable',
    (tester) async {
      final api = ElifootApiClient(
        httpClient: MockClient((r) async => http.Response('{}', 404)),
      );
      await tester.pumpWidget(
        MaterialApp(
          home: WorldCreatorScreen(api: api, coachId: 'test'),
        ),
      );
      await tester.pumpAndSettle();
      expect(find.textContaining('Catálogo indisponível'), findsOneWidget);
      expect(tester.takeException(), isNull);
      api.disconnectRealtime();
    },
  );
}
