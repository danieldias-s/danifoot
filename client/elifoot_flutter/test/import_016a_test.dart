import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:elifoot_flutter/api_client.dart';
import 'package:elifoot_flutter/import_admin_016a.dart';
import 'package:elifoot_flutter/catalog_provenance.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'dart:convert';

class ImportFixtureApi extends ElifootApiClient {
  final List<String> paths = [];
  final List<Map<String, dynamic>> bodies = [];
  @override
  Future<dynamic> import016a(
    String token,
    String path, {
    Map<String, dynamic>? body,
  }) async {
    paths.add(path);
    if (body != null) bodies.add(body);
    if (path == 'coverage') {
      return {
        'providers': [
          {
            'provider': 'api-football',
            'status': 'NOT_CONNECTED',
            'dailyBudget': 100,
            'requestsUsed': 0,
            'providerRemaining': null,
            'cacheEntries': 0,
          },
        ],
        'clubs': [],
        'lastUpdate': null,
        'realPlayers': 0,
        'currentReported': 0,
        'currentVerified': 0,
        'incompleteRecords': 0,
      };
    }
    return {
      'id': 'run-test',
      'mode': path.startsWith('apply') ? 'APPLY' : 'DRY_RUN',
      'status': path.startsWith('apply') ? 'APPLIED_INCOMPLETE' : 'INCOMPLETE',
      'applied': path.startsWith('apply'),
      'isFixture': true,
      'clubs': [
        {
          'clubKey': 'barcelona-female',
          'name': 'FC Barcelona Feminino',
          'countryCode': 'ES',
          'gender': 'Female',
          'provider': 'wikidata',
          'externalId': 'Q522899',
          'wikidataId': 'Q522899',
          'season': 'UNCONFIRMED',
          'observedAt': 'TEST_DATE',
          'status': 'INCOMPLETE',
          'receivedPlayers': 0,
          'usablePlayers': 0,
          'duplicates': 0,
          'currentReported': 0,
          'currentVerified': 0,
          'historical': 0,
          'positions': {},
          'issues': ['FIXTURE: roster unavailable'],
          'candidates': [],
          'players': [],
        },
      ],
    };
  }
}

void main() {
  for (final width in [320.0, 390.0, 1440.0]) {
    testWidgets(
      'Admin importer has explicit dry run/apply, fixture labeling and no automatic calls at $width',
      (tester) async {
        tester.view.physicalSize = Size(width, 1000);
        tester.view.devicePixelRatio = 1;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);
        final api = ImportFixtureApi();
        await tester.pumpWidget(MaterialApp(home: ImportAdmin016A(api: api)));
        await tester.pumpAndSettle();
        expect(api.paths, isEmpty);
        final token = find.byKey(const Key('catalog-admin-token-016a'));
        await tester.enterText(token, 'test-admin');
        final dry = find.byKey(const Key('catalog-dry-run-016a'));
        await tester.scrollUntilVisible(
          dry,
          200,
          scrollable: find.byType(Scrollable).first,
        );
        await tester.pumpAndSettle();
        await tester.ensureVisible(dry);
        await tester.pumpAndSettle();
        if (tester.getCenter(dry).dy > 950) {
          await tester.drag(find.byType(ListView).first, const Offset(0, -180));
          await tester.pumpAndSettle();
        }
        await tester.tap(dry);
        await tester.pumpAndSettle();
        expect(api.paths.first, 'dry-run');
        expect(api.bodies.single['clubKeys'], hasLength(6));
        expect(api.bodies.single.containsKey('apiKey'), false);
        await tester.scrollUntilVisible(
          find.text('FIXTURE DE TESTE — NÃO É IMPORTAÇÃO REAL'),
          200,
          scrollable: find.byType(Scrollable).first,
        );
        expect(
          find.text('FIXTURE DE TESTE — NÃO É IMPORTAÇÃO REAL'),
          findsOneWidget,
        );
        expect(find.textContaining('NOT_CONNECTED'), findsOneWidget);
        await tester.scrollUntilVisible(
          find.textContaining('FC Barcelona Feminino'),
          200,
          scrollable: find.byType(Scrollable).first,
        );
        await tester.pumpAndSettle();
        expect(find.textContaining('FC Barcelona Feminino'), findsWidgets);
        final apply = find.byKey(const Key('catalog-apply-016a'));
        await tester.scrollUntilVisible(
          apply,
          -200,
          scrollable: find.byType(Scrollable).first,
        );
        await tester.pumpAndSettle();
        await tester.ensureVisible(apply);
        await tester.pumpAndSettle();
        await tester.tap(apply);
        await tester.pumpAndSettle();
        expect(api.paths, contains('apply/run-test'));
        expect(tester.takeException(), isNull);
      },
    );
  }
  test('Provider-reported athlete is never labeled verified', () {
    expect(
      catalogIdentityOrigin({'playerOrigin': 'REAL_CURRENT_PROVIDER_REPORTED'}),
      contains('NÃO VERIFICADO'),
    );
    expect(
      catalogIdentityOrigin({'playerOrigin': 'REAL_HISTORICAL'}),
      contains('NÃO É ELENCO ATUAL'),
    );
  });
  test('Admin API sends only admin token and never a provider key', () async {
    final requests = <http.Request>[];
    final api = ElifootApiClient(
      baseUrl: 'http://localhost',
      httpClient: MockClient((r) async {
        requests.add(r);
        return http.Response(jsonEncode({'status': 'NOT_CONNECTED'}), 200);
      }),
    );
    await api.import016a(
      'local-admin',
      'dry-run',
      body: {
        'provider': 'api-football',
        'clubKeys': ['flamengo'],
      },
    );
    expect(requests.single.headers['X-Catalog-Admin-Token'], 'local-admin');
    expect(requests.single.headers.containsKey('x-apisports-key'), false);
    expect(requests.single.body, isNot(contains('APIFOOTBALL_API_KEY')));
  });
}
