import 'dart:convert';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:elifoot_flutter/api_client.dart';
import 'package:elifoot_flutter/world_creator.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:elifoot_flutter/catalog_provenance.dart';

void main() {
  testWidgets(
    'New configuration clears previously saved consent to generated athletes',
    (tester) async {
      bool? consent;
      final api = ElifootApiClient(
        httpClient: MockClient((request) async {
          dynamic result;
          if (request.url.path.endsWith('/countries')) {
            result = [];
          } else if (request.url.path.endsWith('/last-configuration')) {
            result = {
              'name': 'Configuração sintética',
              'gender': 'Female',
              'season': '2026',
              'seed': 1,
              'mode': 'Custom',
              'districtMode': 'Manual',
              'maxHumans': 1,
              'countries': ['ES'],
              'assignments': [],
              'allowFictionalTeams': false,
              'allowFictionalPlayers': true,
            };
          } else {
            final body = jsonDecode(request.body) as Map;
            consent = body['allowFictionalPlayers'] as bool;
            result = {'clubs': [], 'references': [], 'warnings': []};
          }
          return http.Response.bytes(
            utf8.encode(jsonEncode(result)),
            200,
            headers: {'content-type': 'application/json; charset=utf-8'},
          );
        }),
      );
      await tester.pumpWidget(
        MaterialApp(
          home: WorldCreatorScreen(api: api, coachId: 'consent-test'),
        ),
      );
      await tester.pumpAndSettle();
      await tester.tap(find.text('USAR ÚLTIMA CONFIGURAÇÃO'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('CRIAR NOVA CONFIGURAÇÃO'));
      await tester.pumpAndSettle();
      for (var step = 0; step < 3; step++) {
        await tester.tap(find.text('CONTINUAR'));
        await tester.pumpAndSettle();
      }
      expect(consent, false);
      expect(tester.takeException(), isNull);
      api.disconnectRealtime();
    },
  );

  testWidgets(
    'Real identity and estimated strength are clearly different labels',
    (tester) async {
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: catalogPlayerRow({
              'identityOrigin': 'Real',
              'position': 'G',
              'playerName': 'Nome sintético de teste',
              'strength': 39,
              'source': 'mock-provider',
              'updatedAt': '2026-10-01',
              'strengthEstimate': {
                'method': 'relative-v1:missing-individual',
                'origin': 'Estimated',
                'confidence': 0.2,
              },
            }),
          ),
        ),
      );
      expect(find.textContaining('DADO REAL'), findsOneWidget);
      expect(find.textContaining('força estimada 39'), findsOneWidget);
      expect(tester.takeException(), isNull);
    },
  );
  testWidgets(
    'Generated athletes and estimated rankings cannot look official',
    (tester) async {
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: Column(
              children: [
                catalogPlayerRow({
                  'identityOrigin': 'Generated',
                  'position': 'D',
                  'playerName': 'Atleta fictício TEST-1',
                  'strength': 18,
                  'source': 'generated013b',
                  'updatedAt': '1970-01-01',
                }),
                catalogRankingLabel({
                  'rankingInfo': {
                    'method': 'derived-elo-v1',
                    'source': 'elifoot-derived-elo',
                    'updatedAt': '2026-10-01',
                    'score': 1510,
                    'confidence': 0.2,
                    'origin': 'Estimated',
                  },
                }),
              ],
            ),
          ),
        ),
      );
      expect(find.textContaining('FICTÍCIO GERADO'), findsOneWidget);
      expect(find.textContaining('ESTIMADO'), findsOneWidget);
      expect(find.textContaining('Opta'), findsNothing);
      expect(tester.takeException(), isNull);
    },
  );
  test('Pending calibration is never shown as an official force value', () {
    expect(
      catalogStrengthDescription({
        'strength': 25,
        'strengthEstimate': {'method': 'relative-v1:pending'},
      }),
      'força estimada na prévia',
    );
  });
}
