import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:elifoot_flutter/api_client.dart';
import 'package:elifoot_flutter/world_creator.dart';
import 'package:elifoot_flutter/catalog_provenance.dart';

const countries = ['BR', 'AR', 'GB', 'ES', 'DE', 'IT', 'FR', 'PT'];
Map<String, dynamic> summary({int selected = 0, int generated = 0}) => {
  'verifiedRealClubs': 128,
  'currentSeasonConfirmedClubs': 0,
  'completeRealSquads': 0,
  'incompleteRealSquads': 128,
  'missingClubSlots': 0,
  'selectedRealClubs': selected,
  'selectedFictionalClubs': 0,
  'realCurrentPlayers': 0,
  'generatedPlayers': generated,
};
Map<String, dynamic> club(int i) => {
  'key': 'real-test-$i',
  'name': 'Test verified club $i',
  'countryCode': countries[i % countries.length],
  'countryName': countries[i % countries.length],
  'season': '2026',
  'gender': 'Male',
  'rank': i + 1,
  'isFictional': false,
  'verification': {
    'status': 'REAL_CLUB_VERIFIED',
    'currentSeasonConfirmed': false,
  },
  'players': List.generate(
    18,
    (j) => {
      'playerName': 'Atleta fictício $i-$j',
      'position': j < 2
          ? 'G'
          : j < 8
          ? 'D'
          : j < 14
          ? 'M'
          : 'A',
      'strength': 20 + j % 10,
      'source': 'generated013c',
      'updatedAt': '1970-01-01',
      'playerOrigin': 'FICTIONAL_GENERATED',
      'identityOrigin': 'Generated',
    },
  ),
};
Future<void> next(WidgetTester tester) async {
  final button = find.widgetWithText(ElevatedButton, 'CONTINUAR');
  await tester.ensureVisible(button);
  await tester.tap(button);
  await tester.pumpAndSettle();
}

Future<void> mode(WidgetTester tester, String text) async {
  final finder = find.byWidgetPredicate(
    (w) =>
        w is DropdownButtonFormField<String> &&
        w.key.toString().contains('Dados do mundo:'),
  );
  await tester.ensureVisible(finder);
  await tester.tap(finder);
  await tester.pumpAndSettle();
  await tester.tap(find.text(text).last);
  await tester.pumpAndSettle();
}

ElifootApiClient api(
  List<Map<String, dynamic>> calls, {
  bool reject = false,
}) => ElifootApiClient(
  httpClient: MockClient((request) async {
    dynamic response;
    if (request.url.path.endsWith('/seasons')) {
      response = ['2026'];
    } else if (request.url.path.endsWith('/countries')) {
      response = countries
          .map(
            (c) => {'code': c, 'name': c, 'maleClubs': 128, 'femaleClubs': 128},
          )
          .toList();
    } else if (request.url.path.endsWith('/last-configuration')) {
      response = null;
    } else {
      final body = request.body.isEmpty
          ? <String, dynamic>{}
          : Map<String, dynamic>.from(jsonDecode(request.body) as Map);
      calls.add({'path': request.url.path, ...body});
      final blocked =
          reject &&
          (body['dataMode'] != 'REAL_CLUBS_SIMULATED' ||
              body['allowFictionalPlayers'] != true);
      if (request.url.path.endsWith('/catalog')) {
        response = {
          'clubs': List.generate(128, club),
          'references': [],
          'warnings': [],
          'dataSummary': summary(),
        };
      } else if (request.url.path.endsWith('/preview')) {
        response = {
          'draftId': 'draft-013c',
          'clubs': blocked
              ? []
              : List.generate(
                  128,
                  (i) => {'division': i < 32 ? i ~/ 8 + 1 : 5, 'club': club(i)},
                ),
          'pendingSlots': blocked ? 128 : 0,
          'errors': blocked
              ? ['Elencos reais atuais insuficientes; criação bloqueada.']
              : [],
          'warnings': [],
          'dataSummary': summary(
            selected: blocked ? 0 : 128,
            generated: blocked ? 0 : 2304,
          ),
        };
      } else if (request.url.path.contains('/confirm/')) {
        response = {'id': 'world-013c'};
      } else {
        throw StateError(request.url.toString());
      }
    }
    return http.Response(
      jsonEncode(response),
      200,
      headers: {'content-type': 'application/json'},
    );
  }),
);
Future<void> open(
  WidgetTester tester,
  List<Map<String, dynamic>> calls, {
  bool reject = false,
}) async {
  await tester.pumpWidget(
    MaterialApp(
      theme: ThemeData.dark(),
      home: WorldCreatorScreen(
        api: api(calls, reject: reject),
        coachId: '11111111-1111-1111-1111-111111111111',
      ),
    ),
  );
  await tester.pumpAndSettle();
  await next(tester);
  await next(tester);
  await next(tester);
  await next(tester);
  expect(find.text('5/7 • ELENCOS / FONTES'), findsOneWidget);
}

void main() {
  testWidgets(
    'Prévia explica a falta de elencos e só regenera após consentimento',
    (tester) async {
      final calls = <Map<String, dynamic>>[];
      await open(tester, calls, reject: true);
      expect(find.text('SOMENTE DADOS REAIS COMPLETOS'), findsOneWidget);
      final boxes = tester
          .widgetList<CheckboxListTile>(find.byType(CheckboxListTile))
          .where(
            (w) =>
                w.title is Text &&
                (w.title as Text).data!.startsWith('AUTORIZO'),
          );
      expect(boxes, isNotEmpty);
      expect(
        boxes.every((w) => w.value == false && w.onChanged == null),
        isTrue,
      );
      await next(tester);
      expect(
        find.textContaining('nenhum tem elenco real completo'),
        findsOneWidget,
      );
      expect(find.textContaining('criação bloqueada'), findsNothing);
      expect(
        tester
            .widget<ElevatedButton>(
              find.widgetWithText(ElevatedButton, 'CONTINUAR'),
            )
            .onPressed,
        isNull,
      );
      expect(
        calls.where((c) => c['path'].toString().endsWith('/confirm')),
        isEmpty,
      );
      expect(
        calls.every(
          (c) =>
              c['allowFictionalPlayers'] == false &&
              c['allowFictionalTeams'] == false,
        ),
        isTrue,
      );
      final strict = calls.last;
      expect(strict['path'], endsWith('/preview'));
      expect(strict['dataMode'], 'REAL_COMPLETE');
      final authorize = find.text('AUTORIZAR ATLETAS SIMULADOS E GERAR PRÉVIA');
      await tester.ensureVisible(authorize);
      await tester.tap(authorize);
      await tester.pumpAndSettle();
      final regenerated = calls.last;
      expect(regenerated['path'], endsWith('/preview'));
      expect(regenerated['dataMode'], 'REAL_CLUBS_SIMULATED');
      expect(regenerated['allowFictionalPlayers'], isTrue);
      expect(regenerated['allowFictionalTeams'], isFalse);
      for (final key in [
        'name',
        'season',
        'countries',
        'seed',
        'mode',
        'districtMode',
        'assignments',
      ]) {
        expect(regenerated[key], strict[key], reason: key);
      }
      expect(find.text('6/7 • PRÉVIA'), findsOneWidget);
      expect(find.textContaining('128/128 clubes'), findsOneWidget);
      expect(authorize, findsNothing);
      expect(
        tester
            .widget<ElevatedButton>(
              find.widgetWithText(ElevatedButton, 'CONTINUAR'),
            )
            .onPressed,
        isNotNull,
      );
    },
  );
  for (final width in [390.0, 1366.0]) {
    testWidgets(
      '013C real clubs and simulated squads require consent and preview labels at $width',
      (tester) async {
        tester.view.physicalSize = Size(width, 900);
        tester.view.devicePixelRatio = 1;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);
        final calls = <Map<String, dynamic>>[];
        await open(tester, calls);
        await mode(tester, 'CLUBES REAIS + ELENCOS SIMULADOS');
        final consent = find.widgetWithText(
          CheckboxListTile,
          'AUTORIZO atletas fictícios para completar elencos reais insuficientes',
        );
        expect(tester.widget<CheckboxListTile>(consent).value, isFalse);
        await tester.ensureVisible(consent);
        await tester.tap(consent);
        await tester.pumpAndSettle();
        await next(tester);
        expect(
          calls
              .where((c) => c['path'].toString().endsWith('/preview'))
              .last['allowFictionalPlayers'],
          isTrue,
        );
        expect(
          calls
              .where((c) => c['path'].toString().endsWith('/preview'))
              .last['allowFictionalTeams'],
          isFalse,
        );
        expect(find.textContaining('2304 fictícios gerados'), findsOneWidget);
        await tester.ensureVisible(find.textContaining('DIV1 • 8/8'));
        await tester.tap(find.textContaining('DIV1 • 8/8'));
        await tester.pumpAndSettle();
        expect(
          find.textContaining('CLUBE REAL — ELENCO SIMULADO'),
          findsWidgets,
        );
        expect(tester.takeException(), isNull);
      },
    );
  }
  testWidgets(
    '013C changing mode clears prior consents and keeps strict mode explicit',
    (tester) async {
      final calls = <Map<String, dynamic>>[];
      await open(tester, calls);
      await mode(tester, 'MUNDO LIVRE');
      final consent = find.widgetWithText(
        CheckboxListTile,
        'AUTORIZO completar vagas com equipes e atletas fictícios',
      );
      await tester.ensureVisible(consent);
      await tester.tap(consent);
      await tester.pumpAndSettle();
      await mode(tester, 'SOMENTE DADOS REAIS COMPLETOS');
      await next(tester);
      final request = calls.last;
      expect(request['dataMode'], 'REAL_COMPLETE');
      expect(request['allowFictionalPlayers'], isFalse);
      expect(request['allowFictionalTeams'], isFalse);
    },
  );
  testWidgets(
    '013C free world consent explicitly covers generated teams and players',
    (tester) async {
      final calls = <Map<String, dynamic>>[];
      await open(tester, calls);
      await mode(tester, 'MUNDO LIVRE');
      final consent = find.widgetWithText(
        CheckboxListTile,
        'AUTORIZO completar vagas com equipes e atletas fictícios',
      );
      await tester.ensureVisible(consent);
      await tester.tap(consent);
      await tester.pumpAndSettle();
      await next(tester);
      expect(calls.last['dataMode'], 'FREE_WORLD');
      expect(calls.last['allowFictionalPlayers'], isTrue);
      expect(calls.last['allowFictionalTeams'], isTrue);
    },
  );
  test(
    '013C provenance distinguishes current, historical reference and generated identities',
    () {
      expect(
        catalogIdentityOrigin({'playerOrigin': 'REAL_VERIFIED_CURRENT'}),
        contains('COMPROVADO'),
      );
      expect(
        catalogIdentityOrigin({'playerOrigin': 'REAL_CURRENT_VERIFIED'}),
        contains('COMPROVADO'),
      );
      expect(
        catalogIdentityOrigin({'playerOrigin': 'REAL_HISTORICAL_REFERENCE'}),
        contains('NÃO É ELENCO ATUAL'),
      );
      expect(
        catalogIdentityOrigin({
          'catalogIdentity': {'origin': 'FICTIONAL_GENERATED'},
        }),
        'FICTÍCIO GERADO',
      );
    },
  );
}
