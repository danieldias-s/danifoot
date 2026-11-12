import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:elifoot_flutter/api_client.dart';
import 'package:elifoot_flutter/offline_catalog_creator.dart';

Map<String, dynamic> catalog({bool ready = false}) => {
  'path': 'brasil/1998/manifest.json',
  'manifest': {
    'catalogId': 'br-1998',
    'name': 'Catálogo sintético Brasil',
    'year': 1998,
    'country': {'code': 'BR', 'name': 'Brasil'},
  },
  'ready': ready,
  'clubs': List.generate(ready ? 128 : 0, (_) => {}),
  'issues': ready
      ? []
      : [
          {'message': 'Rascunho sem clubes; não está pronto para criar mundo.'},
        ],
};

List<Map<String, dynamic>> clubs() => List.generate(
  128,
  (i) => {
    'key': 'offline:BR:1998:c-$i',
    'name': 'Clube sintético $i',
    'countryCode': 'BR',
    'season': '1998',
    'players': List.generate(11, (_) => {}),
    'offlineSource': {
      'definition': {
        'coach': {'name': 'Treinador sintético $i'},
      },
    },
  },
);

http.Response jsonResponse(dynamic data, {int code = 200}) => http.Response(
  jsonEncode(data),
  code,
  headers: {'content-type': 'application/json'},
);

void main() {
  testWidgets(
    'V16 surplus toggle submits 127 real clubs and preserves server block',
    (tester) async {
      Map<String, dynamic>? submitted;
      final rows = clubs();
      for (final row in rows) {
        row['offlineSource'] = Map<String, dynamic>.from(
          row['offlineSource'] as Map,
        )..['v11'] = {'teamStrength': 20};
      }
      final api = ElifootApiClient(
        baseUrl: 'http://localhost',
        httpClient: MockClient((r) async {
          if (r.url.path.endsWith('/catalogs'))
            return jsonResponse({
              'catalogs': [catalog(ready: true)],
              'issues': [],
            });
          if (r.url.path.endsWith('/clubs')) return jsonResponse(rows);
          if (r.url.path.endsWith('/preview')) {
            submitted = Map<String, dynamic>.from(jsonDecode(r.body));
            return jsonResponse({
              'name': 'V16',
              'draftId': 'blocked',
              'errors': ['Excedentes insuficientes: 3 G reais necessários.'],
              'pendingSlots': 1,
              'clubs': [],
            });
          }
          fail('Unexpected write ${r.url.path}');
        }),
      );
      await tester.pumpWidget(
        MaterialApp(
          home: OfflineCatalogCreator(api: api, coachId: 'owner'),
        ),
      );
      await tester.pumpAndSettle();
      await tester.tap(find.byType(CheckboxListTile));
      await tester.pumpAndSettle();
      await tester.scrollUntilVisible(
        find.text('CARREGAR CLUBES'),
        300,
        scrollable: find.byType(Scrollable).first,
      );
      await tester.tap(find.text('CARREGAR CLUBES'));
      await tester.pumpAndSettle();
      final surplusControl = find.descendant(
        of: find.byKey(const Key('offline-v16-surplus')),
        matching: find.byType(Checkbox),
      );
      await tester.scrollUntilVisible(
        surplusControl,
        200,
        scrollable: find.byType(Scrollable).first,
      );
      await tester.pumpAndSettle();
      expect(surplusControl.hitTestable(), findsOneWidget);
      await tester.tap(surplusControl);
      await tester.pumpAndSettle();
      expect(find.text('Clubes selecionados: 127/127'), findsOneWidget);
      await tester.scrollUntilVisible(
        find.text('VALIDAR PRÉVIA'),
        1000,
        maxScrolls: 100,
        scrollable: find.byType(Scrollable).first,
      );
      await tester.tap(find.text('VALIDAR PRÉVIA'));
      await tester.pumpAndSettle();
      expect(submitted!['applyV16RosterPolicy'], isTrue);
      expect(submitted!['includeSurplusClub'], isTrue);
      expect((submitted!['clubKeys'] as List).length, 127);
      expect(find.textContaining('Excedentes insuficientes'), findsOneWidget);
      final button = tester.widget<FilledButton>(
        find.widgetWithText(FilledButton, 'CRIAR NOVO MUNDO'),
      );
      expect(button.onPressed, isNull);
      expect(tester.takeException(), isNull);
    },
  );
  for (final width in [360.0, 768.0, 1024.0, 1440.0]) {
    testWidgets(
      'V11 ranking, metadata and 384 candidates fit ${width.toInt()}px',
      (tester) async {
        tester.view.devicePixelRatio = 1;
        tester.view.physicalSize = Size(width, 768);
        addTearDown(tester.view.resetDevicePixelRatio);
        addTearDown(tester.view.resetPhysicalSize);
        final rows = List.generate(384, (i) {
          final c = Map<String, dynamic>.from(clubs()[i % 128]);
          c['key'] = 'c-$i';
          c['name'] = 'Sintético V11 $i';
          c['countryCode'] = ['ES', 'GB', 'IT'][i % 3];
          c['offlineSource'] = {
            'definition': {
              'coach': {'name': 'Técnico sintético'},
              'players': List.generate(11, (_) => {}),
            },
            'v11': {
              'teamStrength': 20,
              'tieBreakPriority': i,
              'identity': {
                'colors': ['#123456', '#FFFFFF'],
              },
              'historicalRanking': {
                'rating': 1000 - i,
                'source': 'MANUAL:synthetic',
                'referenceDate': '1998-07-01',
              },
            },
          };
          return c;
        });
        var previews = 0, confirms = 0;
        final api = ElifootApiClient(
          baseUrl: 'http://localhost',
          httpClient: MockClient((r) async {
            if (r.url.path.endsWith('/catalogs')) {
              return jsonResponse({
                'catalogs': [catalog(ready: true)],
                'issues': [],
              });
            }
            if (r.url.path.endsWith('/clubs')) return jsonResponse(rows);
            if (r.url.path.endsWith('/preview')) {
              previews++;
              final keys = (jsonDecode(r.body)['clubKeys'] as List);
              expect(keys.length, 128);
              expect(keys, isNot(contains('c-0')));
              expect(keys, contains('c-383'));
              return jsonResponse({
                'draftId': 'blocked',
                'name': 'V11',
                'pendingSlots': 0,
                'errors': ['Criação bloqueada: gerador de forças pendente.'],
                'warnings': ['Empate: informe prioridade manual.'],
                'clubs': [
                  for (int i = 0; i < 128; i++)
                    {'division': i < 32 ? i ~/ 8 + 1 : 5, 'club': rows[i]},
                ],
              });
            }
            confirms++;
            return jsonResponse({});
          }),
        );
        await tester.pumpWidget(
          MaterialApp(
            home: OfflineCatalogCreator(api: api, coachId: 'owner'),
          ),
        );
        await tester.pumpAndSettle();
        await tester.tap(find.byType(CheckboxListTile).first);
        await tester.pumpAndSettle();
        Future<void> show(Finder finder) async {
          await tester.scrollUntilVisible(
            finder,
            400,
            maxScrolls: 1000,
            scrollable: find.byType(Scrollable).first,
          );
          await tester.pumpAndSettle();
        }

        await show(find.text('CARREGAR CLUBES'));
        await tester.tap(find.text('CARREGAR CLUBES'));
        await tester.pumpAndSettle();
        await show(find.textContaining('Classificação geral: 384'));
        expect(find.textContaining('Excedentes: 256'), findsOneWidget);
        await show(find.textContaining('1. Sintético V11 0'));
        expect(find.textContaining('Força coletiva: 20/20'), findsWidgets);
        expect(find.textContaining('MANUAL:synthetic'), findsWidgets);
        await tester.tap(find.textContaining('1. Sintético V11 0'));
        await tester.pumpAndSettle();
        await show(find.textContaining('384. Sintético V11 383'));
        expect(find.textContaining('EXCEDENTE'), findsWidgets);
        await tester.tap(find.textContaining('384. Sintético V11 383'));
        await tester.pumpAndSettle();
        await show(find.text('VALIDAR PRÉVIA'));
        await tester.tap(find.text('VALIDAR PRÉVIA'));
        await tester.pumpAndSettle();
        expect(previews, 1);
        await show(find.text('CRIAR NOVO MUNDO'));
        expect(
          tester
              .widget<FilledButton>(
                find.widgetWithText(FilledButton, 'CRIAR NOVO MUNDO'),
              )
              .onPressed,
          isNull,
        );
        expect(confirms, 0);
        expect(tester.takeException(), isNull);
      },
    );
  }
  for (final width in [360.0, 768.0, 1024.0, 1440.0]) {
    testWidgets('Offline draft is blocked and fits ${width.toInt()}px', (
      tester,
    ) async {
      tester.view.devicePixelRatio = 1;
      tester.view.physicalSize = Size(width, 768);
      addTearDown(tester.view.resetDevicePixelRatio);
      addTearDown(tester.view.resetPhysicalSize);
      final api = ElifootApiClient(
        baseUrl: 'http://localhost',
        httpClient: MockClient(
          (r) async => jsonResponse({
            'catalogs': [catalog()],
            'issues': [],
          }),
        ),
      );
      await tester.pumpWidget(
        MaterialApp(
          home: OfflineCatalogCreator(api: api, coachId: 'owner'),
        ),
      );
      await tester.pumpAndSettle();
      expect(find.textContaining('RASCUNHO / INVÁLIDO'), findsOneWidget);
      final checkbox = tester.widget<CheckboxListTile>(
        find.byType(CheckboxListTile),
      );
      expect(checkbox.onChanged, isNull);
      await tester.scrollUntilVisible(
        find.text('CARREGAR CLUBES'),
        200,
        scrollable: find.byType(Scrollable).first,
      );
      expect(
        tester
            .widget<FilledButton>(
              find.widgetWithText(FilledButton, 'CARREGAR CLUBES'),
            )
            .onPressed,
        isNull,
      );
      expect(tester.takeException(), isNull);
    });
  }

  testWidgets(
    'User selects ready catalog, reviews 128 clubs and explicitly confirms new world',
    (tester) async {
      tester.view.devicePixelRatio = 1;
      tester.view.physicalSize = const Size(1024, 768);
      addTearDown(tester.view.resetDevicePixelRatio);
      addTearDown(tester.view.resetPhysicalSize);
      final requests = <http.Request>[];
      final rows = clubs();
      final api = ElifootApiClient(
        baseUrl: 'http://localhost',
        httpClient: MockClient((r) async {
          requests.add(r);
          if (r.url.path.endsWith('/catalogs')) {
            return jsonResponse({
              'catalogs': [catalog(ready: true)],
              'issues': [],
            });
          }
          if (r.url.path.endsWith('/clubs')) return jsonResponse(rows);
          if (r.url.path.endsWith('/preview')) {
            final request = jsonDecode(r.body) as Map;
            expect(request['catalogIds'], ['br-1998']);
            expect((request['clubKeys'] as List).length, 128);
            expect(request['maxHumans'], 1);
            return jsonResponse({
              'draftId': 'draft-1',
              'name': 'Liga offline 98',
              'pendingSlots': 0,
              'errors': [],
              'clubs': [
                for (int i = 0; i < 128; i++)
                  {'division': i < 32 ? i ~/ 8 + 1 : 5, 'club': rows[i]},
              ],
            });
          }
          if (r.url.path.endsWith('/confirm/draft-1')) {
            return jsonResponse({'id': 'new-world'});
          }
          return jsonResponse({'error': 'Unexpected path'}, code: 400);
        }),
      );
      bool? created;
      await tester.pumpWidget(
        MaterialApp(
          home: Builder(
            builder: (context) => Scaffold(
              body: TextButton(
                onPressed: () async {
                  created = await Navigator.push<bool>(
                    context,
                    MaterialPageRoute(
                      builder: (_) =>
                          OfflineCatalogCreator(api: api, coachId: 'owner'),
                    ),
                  );
                },
                child: const Text('ABRIR'),
              ),
            ),
          ),
        ),
      );
      await tester.tap(find.text('ABRIR'));
      await tester.pumpAndSettle();
      expect(requests.where((r) => r.method == 'POST'), isEmpty);
      await tester.tap(find.byType(CheckboxListTile));
      await tester.pumpAndSettle();
      await tester.scrollUntilVisible(
        find.text('CARREGAR CLUBES'),
        300,
        scrollable: find.byType(Scrollable).first,
      );
      await tester.tap(find.text('CARREGAR CLUBES'));
      await tester.pumpAndSettle();
      await tester.scrollUntilVisible(
        find.text('VALIDAR PRÉVIA'),
        1000,
        maxScrolls: 80,
        scrollable: find.byType(Scrollable).first,
      );
      expect(tester.takeException(), isNull);
      await tester.tap(find.text('VALIDAR PRÉVIA'));
      await tester.pumpAndSettle();
      expect(requests.where((r) => r.url.path.contains('/confirm/')), isEmpty);
      await tester.scrollUntilVisible(
        find.text('CRIAR NOVO MUNDO'),
        1000,
        maxScrolls: 80,
        scrollable: find.byType(Scrollable).first,
      );
      await tester.tap(find.text('CRIAR NOVO MUNDO'));
      await tester.pumpAndSettle();
      expect(created, isTrue);
      expect(
        requests.where((r) => r.url.path.endsWith('/confirm/draft-1')).length,
        1,
      );
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets('Server read error is visible and creates no world', (
    tester,
  ) async {
    int confirms = 0;
    final api = ElifootApiClient(
      baseUrl: 'http://localhost',
      httpClient: MockClient((r) async {
        if (r.url.path.contains('/confirm/')) confirms++;
        return jsonResponse({'error': 'Arquivo JSON inválido'}, code: 400);
      }),
    );
    await tester.pumpWidget(
      MaterialApp(
        home: OfflineCatalogCreator(api: api, coachId: 'owner'),
      ),
    );
    await tester.pumpAndSettle();
    expect(find.textContaining('Arquivo JSON inválido'), findsOneWidget);
    expect(confirms, 0);
  });
}
