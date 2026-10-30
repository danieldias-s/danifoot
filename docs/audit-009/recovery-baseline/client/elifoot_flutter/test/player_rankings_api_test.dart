import 'dart:convert';

import 'package:elifoot_flutter/api_client.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

void main() {
  test(
    '009 sends every category, competition and period using the stable query contract',
    () async {
      final requests = <http.Request>[];
      final api = ElifootApiClient(
        httpClient: MockClient((request) async {
          requests.add(request);
          return http.Response('{"rows":[],"notEligible":[]}', 200);
        }),
      );
      for (final category in ['scorers', 'goalkeepers', 'defenders']) {
        for (final scope in [
          'general',
          'division1',
          'division2',
          'division3',
          'division4',
          'cup',
        ]) {
          for (final period in ['career', 'current', 'season']) {
            await api.getPlayerRankings(
              'world',
              category: category,
              scope: scope,
              period: period,
              season: 2028,
            );
            final request = requests.last;
            expect(request.method, 'GET');
            expect(request.url.path, '/api/worlds/world/player-rankings');
            expect(request.url.queryParameters, {
              'category': category,
              'scope': scope,
              'period': period,
              if (period == 'season') 'season': '2028',
            });
          }
        }
      }
      expect(requests, hasLength(54));
    },
  );

  test(
    '009 requires the selected season instead of silently querying a different period',
    () async {
      var requests = 0;
      final api = ElifootApiClient(
        httpClient: MockClient((_) async {
          requests++;
          return http.Response('{}', 200);
        }),
      );
      await expectLater(
        api.getPlayerRankings('world', period: 'season'),
        throwsArgumentError,
      );
      expect(requests, 0);
    },
  );

  test(
    '009 preserves exact averages, null legacy average and coverage warnings',
    () async {
      final api = ElifootApiClient(
        httpClient: MockClient(
          (_) async => http.Response(
            jsonEncode({
              'rows': [
                {'playerId': 'p1', 'average': 0.123456789},
                {'playerId': 'legacy', 'average': null},
              ],
              'coverageWarnings': ['Incomplete appearances'],
            }),
            200,
          ),
        ),
      );
      final result = await api.getPlayerRankings('world');
      expect(result['rows'][0]['average'], 0.123456789);
      expect(result['rows'][1]['average'], isNull);
      expect(result['coverageWarnings'], ['Incomplete appearances']);
    },
  );

  test(
    '009 history and awards use official read-only endpoints and optional season',
    () async {
      final requests = <http.Request>[];
      final api = ElifootApiClient(
        httpClient: MockClient((request) async {
          requests.add(request);
          return http.Response(
            jsonEncode({
              'awards': [
                {'awardAmount': 3000000},
              ],
              'clubTotals': [
                {'amount': 6000000},
              ],
            }),
            200,
          );
        }),
      );
      await api.getPlayerHistory('world', 'player');
      expect(
        requests.last.url.path,
        '/api/worlds/world/players/player/history',
      );
      final latest = await api.getSeasonTopScorers('world');
      expect(requests.last.url.path, '/api/worlds/world/season-top-scorers');
      expect(requests.last.url.queryParameters, isEmpty);
      expect(latest['clubTotals'][0]['amount'], 6000000);
      await api.getSeasonTopScorers('world', season: 2032);
      expect(requests.last.url.queryParameters, {'season': '2032'});
      expect(requests.every((r) => r.method == 'GET'), isTrue);
    },
  );

  test(
    '009 surfaces API failures without substituting invented empty rankings',
    () async {
      final api = ElifootApiClient(
        httpClient: MockClient(
          (_) async => http.Response('{"error":"unavailable"}', 503),
        ),
      );
      await expectLater(api.getPlayerRankings('world'), throwsException);
      await expectLater(api.getPlayerHistory('world', 'p1'), throwsException);
      await expectLater(api.getSeasonTopScorers('world'), throwsException);
    },
  );
}
