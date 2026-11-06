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
  for (final (width, maxHumans) in [(390.0, 1), (1366.0, 8)]) {
    testWidgets(
      'World creator completes female flow for $maxHumans coaches at $width',
      (tester) async {
        tester.view.physicalSize = Size(width, 900);
        tester.view.devicePixelRatio = 1;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);
        String gender = 'Male';
        int? seed;
        Map<String, dynamic>? confirmed;
        Map<String, dynamic> club(int i) => {
          'key': 'club-$i',
          'name': 'Clube fictício $i',
          'countryCode': countries[i % 8],
          'countryName': names[i % 8],
          'season': '2026',
          'gender': gender,
          'rank': i + 1,
          'isFictional': false,
          'verification': {'status': 'REAL_CLUB_VERIFIED'},
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
            if (request.url.path.endsWith('/seasons')) {
              response = ['2026'];
            } else if (request.url.path.endsWith('/countries')) {
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
              seed = body['seed'] as int;
              expect(seed, inInclusiveRange(1, 2147483647));
              gender = body['gender'].toString();
              expect(body['season'], '2026');
              expect(body['countries'], unorderedEquals(countries));
              expect(body['dataMode'], isNull);
              expect(body['useSavedData'], isTrue);
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
              final body = jsonDecode(request.body) as Map;
              expect(body['seed'], seed);
              expect(body['gender'], 'Female');
              expect(body['season'], '2026');
              expect(body['countries'], unorderedEquals(countries));
              expect(body['maxHumans'], maxHumans);
              expect(body['allowFictionalTeams'], isFalse);
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
        if (maxHumans == 8) {
          final trainers = find.byKey(const ValueKey('Treinadores:1'));
          await tester.ensureVisible(trainers);
          await tester.tap(trainers);
          await tester.pumpAndSettle();
          await tester.tap(find.text('8').last);
          await tester.pumpAndSettle();
        }
        Future<void> next() async {
          await tester.tap(find.text('CONTINUAR'));
          await tester.pumpAndSettle();
          expect(tester.takeException(), isNull);
        }

        await next();
        expect(find.text('8 países selecionados • máximo 8'), findsOneWidget);
        final search = find.byType(TextField);
        await tester.enterText(search, 'Brasil');
        await tester.pump();
        expect(find.widgetWithText(CheckboxListTile, 'Brasil'), findsOneWidget);
        expect(find.text('Argentina'), findsNothing);
        await tester.enterText(search, '');
        await tester.pump();
        await next();
        expect(find.text('3/7 • DIVISÕES'), findsOneWidget);
        expect(find.text('Como montar'), findsNothing);
        await next();
        expect(find.text('4/7 • DISTRITAIS'), findsOneWidget);
        expect(find.textContaining('AUTORIZO completar'), findsNothing);
        await next();
        expect(find.text('5/7 • ELENCOS / FONTES'), findsOneWidget);
        final advanced = find.text('OPÇÕES AVANÇADAS E FONTES');
        await tester.ensureVisible(advanced);
        await tester.tap(advanced);
        await tester.pumpAndSettle();
        expect(find.textContaining('AUTORIZO completar'), findsOneWidget);
        final sources = find.text('Fontes e avisos do catálogo');
        await tester.ensureVisible(sources);
        await tester.pumpAndSettle();
        await tester.tap(sources);
        await tester.pumpAndSettle();
        expect(find.textContaining('FIXTURES'), findsOneWidget);
        await next();
        expect(find.text('6/7 • PRÉVIA'), findsOneWidget);
        expect(find.text('128/128 clubes • 0 vagas pendentes'), findsOneWidget);
        expect(find.text('DISTRITAIS • 96/96'), findsOneWidget);
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
        expect(find.text('7/7 • CONFIRMAR'), findsOneWidget);
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
      expect(
        find.text('Não foi possível consultar os anos do catálogo.'),
        findsOneWidget,
      );
      expect(tester.takeException(), isNull);
      api.disconnectRealtime();
    },
  );
}
