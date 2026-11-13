import 'dart:convert';

import 'package:elifoot_flutter/api_client.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

void main() {
  test(
    'Continue retries the same pause and hides a raw server error',
    () async {
      final requests = <Map<String, dynamic>>[];
      final api = ElifootApiClient(
        httpClient: MockClient((request) async {
          requests.add(jsonDecode(request.body) as Map<String, dynamic>);
          return requests.length == 1
              ? http.Response('SqliteException Cookie: private-token', 503)
              : http.Response('{"resumed":true}', 200);
        }),
      );

      await expectLater(
        api.confirmContinue('world', 'coach', pauseWindowId: 'pause-1'),
        throwsA(
          predicate<Object>(
            (error) =>
                error.toString().contains('tente continuar novamente') &&
                !error.toString().contains('private-token'),
          ),
        ),
      );
      final result = await api.confirmContinue(
        'world',
        'coach',
        pauseWindowId: 'pause-1',
      );
      await api.confirmContinue('world', 'coach', pauseWindowId: 'pause-2');

      expect(result['resumed'], isTrue);
      expect(requests.first['pauseWindowId'], 'pause-1');
      expect(requests[1], requests.first);
      expect(requests.last['pauseWindowId'], 'pause-2');
      expect(
        requests.last['idempotencyKey'],
        isNot(requests.first['idempotencyKey']),
      );
    },
  );
}
