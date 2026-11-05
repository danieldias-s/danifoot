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
      bool? consent, historical, unverified;
      final api = ElifootApiClient(
        httpClient: MockClient((request) async {
          dynamic result;
          if (request.url.path.endsWith('/countries')) {
            result = [
              {
                'code': 'ES',
                'name': 'Espanha',
                'maleClubs': 20,
                'femaleClubs': 20,
              },
            ];
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
              'includeHistoricalClubs': true,
              'includeUnverifiedClubs': true,
            };
          } else {
            final body = jsonDecode(request.body) as Map;
            consent = body['allowFictionalPlayers'] as bool;
            historical = body['includeHistoricalClubs'] as bool;
            unverified = body['includeUnverifiedClubs'] as bool;
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
      expect(historical, false);
      expect(unverified, false);
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

  testWidgets('Historical clubs and undated activity are visible separately', (
    tester,
  ) async {
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: Column(
            children: [
              catalogClubStatus({
                'verification': {
                  'status': 'REAL_CLUB_HISTORICAL',
                  'sourceSeason': '2025',
                  'currentSeasonConfirmed': false,
                },
              }),
              catalogClubStatus({
                'verification': {
                  'status': 'REAL_CLUB_VERIFIED',
                  'sourceSeason': '2026-27',
                  'currentSeasonConfirmed': true,
                },
              }),
              catalogClubStatus({
                'verification': {
                  'status': 'REAL_CLUB_UNVERIFIED',
                  'currentSeasonConfirmed': false,
                },
              }),
            ],
          ),
        ),
      ),
    );
    expect(find.textContaining('REAL_CLUB_HISTORICAL'), findsOneWidget);
    expect(find.textContaining('REAL_CLUB_UNVERIFIED'), findsOneWidget);
    expect(
      find.textContaining('atividade atual não certificada'),
      findsNWidgets(2),
    );
    expect(find.textContaining('participação datada'), findsOneWidget);
  });
  testWidgets('A technical identifier tie break is labeled unranked', (
    tester,
  ) async {
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: catalogRankingLabel({
            'rankingInfo': {'method': 'Unranked-v2', 'rank': 1},
          }),
        ),
      ),
    );
    expect(find.textContaining('NÃO CLASSIFICADO'), findsOneWidget);
    expect(
      find.textContaining('desempate técnico não é ranking'),
      findsOneWidget,
    );
  });
}
