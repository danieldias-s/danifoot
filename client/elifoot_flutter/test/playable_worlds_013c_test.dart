import 'dart:convert';
import 'historical_creator_test_support.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:elifoot_flutter/api_client.dart';
import 'package:elifoot_flutter/world_creator.dart';
import 'package:elifoot_flutter/catalog_provenance.dart';

const countries = ['BR', 'AR', 'GB', 'ES', 'DE', 'IT', 'FR', 'PT'];
Map<String, dynamic> summary({
  int selected = 0,
  int generated = 0,
  int complete = 0,
}) => {
  'verifiedRealClubs': 128,
  'currentSeasonConfirmedClubs': 0,
  'completeRealSquads': complete,
  'incompleteRealSquads': 128 - complete,
  'missingClubSlots': 128 - selected,
  'selectedRealClubs': selected,
  'selectedFictionalClubs': 0,
  'realCurrentPlayers': 0,
  'generatedPlayers': generated,
};
Map<String, dynamic> club(int i, {bool generated = false}) => {
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
      'playerName': 'Atleta ${generated ? 'fictício' : 'real'} $i-$j',
      'position': j < 2
          ? 'G'
          : j < 8
          ? 'D'
          : j < 14
          ? 'M'
          : 'A',
      'strength': 20 + j % 10,
      'source': generated ? 'generated013c' : 'test-catalog',
      'updatedAt': generated ? '1970-01-01' : '2026-01-01',
      'playerOrigin': generated
          ? 'FICTIONAL_GENERATED'
          : 'REAL_VERIFIED_CURRENT',
      'identityOrigin': generated ? 'Generated' : 'Real',
    },
  ),
};
Future<void> next(WidgetTester tester) async {
  final button = find.widgetWithText(ElevatedButton, 'CONTINUAR');
  await tester.ensureVisible(button);
  await tester.tap(button);
  await tester.pumpAndSettle();
}

Future<void> choose(WidgetTester tester, Finder card) async {
  await tester.ensureVisible(card);
  await tester.tap(card);
  await tester.pumpAndSettle();
}

ElifootApiClient api(
  List<Map<String, dynamic>> calls, {
  int completeRealSquads = 128,
}) => ElifootApiClient(
  httpClient: MockClient((request) async {
    dynamic response;
    if (request.url.path.endsWith('/historical-years')) {
      response = historicalYearResponse(request.url, '2026');
    } else if (request.url.path.endsWith('/historical-countries')) {
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
      if (request.url.path.endsWith('/catalog')) {
        response = {
          'clubs': List.generate(128, (i) => club(i)),
          'references': [],
          'warnings': [],
          'dataSummary': summary(complete: completeRealSquads),
        };
      } else if (request.url.path.endsWith('/preview')) {
        final fictional = body['generateAllRosterPlayers'] == true;
        final selected = fictional ? 128 : completeRealSquads;
        response = {
          'draftId': 'draft-013c',
          'clubs': List.generate(
            selected,
            (i) => {
              'division': i < 32 ? i ~/ 8 + 1 : 5,
              'club': club(i, generated: fictional),
            },
          ),
          'pendingSlots': 128 - selected,
          'errors': [if (selected < 128) 'Elencos reais atuais insuficientes.'],
          'warnings': [],
          'dataSummary': summary(
            selected: selected,
            complete: completeRealSquads,
            generated: fictional ? 2304 : 0,
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
  int completeRealSquads = 128,
}) async {
  await tester.pumpWidget(
    MaterialApp(
      theme: ThemeData.dark(),
      home: WorldCreatorScreen(
        api: api(calls, completeRealSquads: completeRealSquads),
        coachId: '11111111-1111-1111-1111-111111111111',
      ),
    ),
  );
  await tester.pumpAndSettle();
  await next(tester);
  await selectHistoricalCountries(tester, countries);
  await next(tester);
  await selectHistoricalYear(tester, '2026');
  await next(tester);
  await next(tester);
  await next(tester);
  expect(find.text('6/8 • ELENCOS / FONTES'), findsOneWidget);
}

void main() {
  for (final complete in [0, 127]) {
    testWidgets('$complete elencos reais bloqueiam o avanço e explicam a falta', (
      tester,
    ) async {
      final calls = <Map<String, dynamic>>[];
      await open(tester, calls, completeRealSquads: complete);
      expect(find.text('OPÇÕES AVANÇADAS E FONTES'), findsNothing);

      await next(tester);
      final strict = calls.last;
      expect(strict['path'], endsWith('/preview'));
      expect(strict['dataMode'], 'REAL_COMPLETE');
      expect(strict['allowFictionalPlayers'], isFalse);
      expect(strict['allowFictionalTeams'], isFalse);
      expect(find.text('6/8 • ELENCOS / FONTES'), findsOneWidget);
      expect(find.text('7/8 • PRÉVIA'), findsNothing);
      expect(
        find.text('Modo real exige 128 clubes com elencos reais completos.'),
        findsOneWidget,
      );
      final coverage = complete == 0
          ? 'ainda não tem elencos reais completos.'
          : 'oferece $complete dos 128 elencos reais completos necessários.';
      expect(
        find.textContaining(
          'Nos países escolhidos, o catálogo tem 128 clubes reais e $coverage',
        ),
        findsOneWidget,
      );
      expect(
        find.textContaining(
          'Para jogar agora, selecione USAR ELENCO FICTÍCIO e clique em CONTINUAR.',
        ),
        findsOneWidget,
      );
      expect(find.textContaining('$complete/128 clubes'), findsNothing);

      await choose(tester, find.byKey(const ValueKey('source-generated')));
      expect(
        find.textContaining('o catálogo tem 128 clubes reais'),
        findsNothing,
      );
      await next(tester);
      final generated = calls.last;
      expect(generated['path'], endsWith('/preview'));
      expect(generated['dataMode'], 'REAL_CLUBS_SIMULATED');
      expect(generated['allowFictionalPlayers'], isTrue);
      expect(generated['allowFictionalTeams'], isFalse);
      expect(generated['generateAllRosterPlayers'], isTrue);
      expect(generated['countries'], strict['countries']);
      expect(generated['assignments'], strict['assignments']);
      expect(find.text('7/8 • PRÉVIA'), findsOneWidget);
      expect(find.textContaining('128/128 clubes'), findsOneWidget);
      expect(find.textContaining('2304 fictícios gerados'), findsOneWidget);
    });
  }

  for (final (width, fictionalRoster) in [(1366.0, false), (390.0, true)]) {
    testWidgets('Elencos e fontes preserva clubes a $width px', (tester) async {
      tester.view.physicalSize = Size(width, 900);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      final calls = <Map<String, dynamic>>[];
      await open(tester, calls);
      expect(find.text('USAR ELENCO REAL'), findsOneWidget);
      expect(find.text('USAR ELENCO FICTÍCIO'), findsOneWidget);
      expect(find.text('OPÇÕES AVANÇADAS E FONTES'), findsNothing);
      final real = find.byKey(const ValueKey('source-real'));
      final fictional = find.byKey(const ValueKey('source-generated'));
      Finder selected(Finder card) => find.descendant(
        of: card,
        matching: find.byIcon(Icons.radio_button_checked),
      );
      expect(selected(real), findsOneWidget);
      expect(selected(fictional), findsNothing);
      final realRect = tester.getRect(real);
      final fictionalRect = tester.getRect(fictional);
      if (width == 1366) {
        expect(realRect.top, fictionalRect.top);
        expect(realRect.right, lessThan(fictionalRect.left));
      } else {
        expect(realRect.left, fictionalRect.left);
        expect(realRect.bottom, lessThan(fictionalRect.top));
      }
      expect(
        calls.any((call) => call['path'].toString().endsWith('/preview')),
        isFalse,
      );

      await choose(tester, fictional);
      expect(selected(fictional), findsOneWidget);
      if (fictionalRoster) {
        await tester.tap(find.text('VOLTAR'));
        await tester.pumpAndSettle();
        await next(tester);
        expect(selected(fictional), findsOneWidget);
      } else {
        await choose(tester, real);
        expect(selected(real), findsOneWidget);
      }
      await next(tester);
      final preview = calls.last;
      expect(preview['path'], endsWith('/preview'));
      expect(
        preview['dataMode'],
        fictionalRoster ? 'REAL_CLUBS_SIMULATED' : 'REAL_COMPLETE',
      );
      expect(preview['allowFictionalPlayers'], fictionalRoster);
      expect(preview['allowFictionalTeams'], isFalse);
      expect(preview['generateAllRosterPlayers'] == true, fictionalRoster);
      expect(preview['countries'], unorderedEquals(countries));
      expect(preview['assignments'], [
        for (var i = 0; i < 32; i++)
          {'clubKey': 'real-test-$i', 'division': i ~/ 8 + 1},
      ]);
      expect(find.text('7/8 • PRÉVIA'), findsOneWidget);
      expect(find.textContaining('128/128 clubes'), findsOneWidget);
      expect(tester.takeException(), isNull);
    });
  }
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
