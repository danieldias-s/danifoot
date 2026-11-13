import 'dart:convert';
import 'historical_creator_test_support.dart';
import 'package:elifoot_flutter/api_client.dart';
import 'package:elifoot_flutter/classic98_widgets.dart';
import 'package:elifoot_flutter/club_selection_dialog.dart';
import 'package:elifoot_flutter/club_crest.dart';
import 'package:elifoot_flutter/club_theme.dart';
import 'package:elifoot_flutter/creator_divisions.dart';
import 'package:elifoot_flutter/creator_districts.dart';
import 'package:elifoot_flutter/creator_visual.dart';
import 'package:elifoot_flutter/world_creator.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

class _CreatorFixture {
  final requests = <http.Request>[];
  List<String> seasons = ['2026', '2025'];
  final Map<String, int> brazilClubsByYear = {};
  final countryRowsByYear = <String, List<(String, String, int, int)>>{};
  int uruguayClubs = 0;
  int seasonFailures = 0;
  int countryFailures = 0;
  int clubCount = 32;
  bool validPreview = false;
  final clubPowers = <int, int>{};
  bool includeOutsideClub = false;
  bool longClubName = false;
  bool sameClubInDifferentYears = false;
  final unavailableClubIds = <int>{};
  bool failLastConfiguration = false;
  Map<String, dynamic>? lastConfiguration;
  Map<String, dynamic>? previewSnapshot;
  List<Map<String, dynamic>> offlineCatalogs = [];
  int reassignmentCount = 0;
  late final api = ElifootApiClient(
    httpClient: MockClient((request) async {
      requests.add(request);
      final dynamic data;
      final male = request.url.queryParameters['gender'] != 'Female';
      if (request.url.path.endsWith('/historical-years')) {
        if (seasonFailures > 0) {
          seasonFailures--;
          return http.Response('Catálogo indisponível', 503);
        }
        final selected = request.url.queryParameters['countries']!.split(',');
        data = [
          for (final year in seasons)
            if (_rows(year).any(
              (row) =>
                  selected.contains(row.$1) && (male ? row.$3 : row.$4) > 0,
            ))
              {
                'year': year,
                'clubCount': _rows(year)
                    .where((row) => selected.contains(row.$1))
                    .fold(0, (total, row) => total + (male ? row.$3 : row.$4)),
                'countries': [
                  for (final row in _rows(year))
                    if (selected.contains(row.$1) &&
                        (male ? row.$3 : row.$4) > 0)
                      {
                        'code': row.$1,
                        'name': row.$2,
                        'clubCount': male ? row.$3 : row.$4,
                      },
                ],
              },
        ];
      } else if (request.url.path.endsWith('/historical-countries')) {
        if (countryFailures > 0) {
          countryFailures--;
          return http.Response('{"error":"Catálogo indisponível"}', 503);
        }
        final rows = <String, Map<String, dynamic>>{};
        for (final year in seasons) {
          for (final row in _rows(year)) {
            final option = rows.putIfAbsent(
              row.$1,
              () => {'code': row.$1, 'name': row.$2, 'clubCount': 0},
            );
            option['clubCount'] += male ? row.$3 : row.$4;
          }
        }
        data = rows.values.toList();
      } else if (request.url.path.endsWith('/offline/catalogs/summary')) {
        data = male ? offlineCatalogs : [];
      } else if (request.url.path.endsWith('/offline/clubs') ||
          request.url.path.endsWith('/offline/planning-clubs')) {
        expect(jsonDecode(request.body)['catalogIds'], ['es-2026-principal']);
        data = [
          for (var i = 0; i < 172; i++)
            {
              'key': 'offline-$i',
              'name': 'Clube espanhol $i',
              'countryCode': 'ES',
              'season': '2026',
              'offlineSource': {
                'v11': {},
                'definition': {
                  'players': [
                    {
                      'name': 'Goleiro cadastrado $i',
                      'position': 'G',
                      'behavior': null,
                    },
                  ],
                },
              },
              'players': [],
            },
        ];
      } else if (request.url.path.endsWith('/offline/preview') ||
          request.url.path.endsWith('/offline/planning-preview')) {
        final planning = request.url.path.endsWith('/offline/planning-preview');
        final body = jsonDecode(request.body) as Map;
        expect(body['catalogIds'], ['es-2026-principal']);
        expect(body['applyV16RosterPolicy'], isTrue);
        expect(
          body['useDefaultBehavior'],
          offlineCatalogs.any((c) => c['canUseDefaultBehavior'] == true)
              ? true
              : null,
        );
        final keys = (body['clubKeys'] as List).toSet();
        expect(keys, hasLength(128));
        final assignments = (body['assignments'] as List).cast<Map>();
        expect(assignments, isEmpty);
        data = {
          'draftId': planning
              ? '00000000-0000-0000-0000-000000000000'
              : 'offline-test',
          'pendingSlots': 0,
          'errors': [if (planning) 'Prévia de rascunho. Valide o catálogo.'],
          'warnings': [],
          'clubs': [
            for (final (index, key) in keys.indexed)
              {
                'division': index < 32 ? index ~/ 8 + 1 : 5,
                'club': {
                  'key': key,
                  'name': 'Clube $key',
                  'countryCode': 'ES',
                  'season': '2026',
                  'players': [],
                },
              },
          ],
        };
      } else if (request.url.path.endsWith('/offline/confirm/offline-test')) {
        data = {'id': 'offline-world'};
      } else if (request.url.path.endsWith('/last-configuration')) {
        if (failLastConfiguration) {
          return http.Response('{"error":"Configuração indisponível"}', 503);
        }
        data = lastConfiguration;
      } else if (request.url.path.endsWith('/catalog')) {
        final body = jsonDecode(request.body) as Map;
        final countries = body['countries'] as List;
        final years = body['catalogYears'] as List;
        data = {
          'clubs': [
            for (var i = 0; i < clubCount; i++)
              if (!unavailableClubIds.contains(i))
                {
                  'key': 'test-$i',
                  'name': longClubName && i == 0
                      ? 'Clube de Futebol com um nome comprido para leitura no celular'
                      : sameClubInDifferentYears && (i == 0 || i == 33)
                      ? 'Clube da ponte'
                      : 'Clube de teste $i',
                  'countryCode': countries[i % countries.length],
                  'countryName': countries[i % countries.length],
                  'gender': body['gender'],
                  'season': years[i % years.length],
                  'rank': i + 1,
                  'initialProfile': {
                    'clubInitialPower': clubPowers[i] ?? 20,
                    'worldRank': i + 1,
                  },
                  'verification': {'status': 'REAL_CLUB_VERIFIED'},
                  'players': [],
                },
            if (includeOutsideClub)
              {
                'key': 'outside-test',
                'name': 'Clube de país não selecionado',
                'countryCode': 'UY',
                'countryName': 'Uruguai',
                'gender': body['gender'],
                'season': body['season'],
                'rank': 999,
                'initialProfile': {'clubInitialPower': 1, 'worldRank': 999},
                'verification': {'status': 'REAL_CLUB_VERIFIED'},
                'players': [],
              },
          ],
          'references': [],
          'warnings': [],
        };
      } else if (request.url.path.endsWith('/preview')) {
        final body = jsonDecode(request.body) as Map;
        final countries = body['countries'] as List;
        final years = body['catalogYears'] as List;
        final assigned = (body['assignments'] as List).cast<Map>();
        final selectedKeys = assigned.map((entry) => entry['clubKey']).toSet();
        final orderedKeys = [
          ...assigned.map((entry) => entry['clubKey'].toString()),
          for (var i = 0; i < 128; i++)
            if (!selectedKeys.contains('test-$i')) 'test-$i',
        ];
        data = {
          'draftId': 'preview-test',
          'clubs': [
            if (validPreview)
              for (var position = 0; position < 128; position++)
                {
                  'division': position < 32 ? position ~/ 8 + 1 : 5,
                  'club': {
                    'key': orderedKeys[position],
                    'name':
                        'Clube de teste ${orderedKeys[position].split('-').last}',
                    'countryCode': countries[position % countries.length],
                    'season': years[position % years.length],
                    'verification': {'status': 'REAL_CLUB_VERIFIED'},
                    'players': [],
                  },
                },
          ],
          'pendingSlots': validPreview ? 0 : 96,
          'errors': validPreview ? [] : ['Faltam 96 clubes.'],
          'warnings': [],
        };
        previewSnapshot = Map<String, dynamic>.from(data);
      } else if (request.url.path.endsWith('/reassign')) {
        final body = jsonDecode(request.body) as Map;
        final previous = previewSnapshot!;
        final clubsByKey = {
          for (final entry in previous['clubs'] as List)
            (entry['club'] as Map)['key']: entry['club'],
        };
        final assignments = (body['assignments'] as List).cast<Map>();
        expect(assignments, hasLength(128));
        expect(
          assignments.map((entry) => entry['clubKey']).toSet(),
          clubsByKey.keys.toSet(),
        );
        data = {
          ...previous,
          'draftId': 'preview-test-reassigned-${++reassignmentCount}',
          'clubs': [
            for (final assignment in assignments)
              {
                'division': assignment['division'],
                'club': clubsByKey[assignment['clubKey']],
              },
          ],
        };
        previewSnapshot = Map<String, dynamic>.from(data);
      } else {
        throw StateError('Rota inesperada ${request.url}');
      }
      return http.Response.bytes(
        utf8.encode(jsonEncode(data)),
        200,
        headers: {'content-type': 'application/json; charset=utf-8'},
      );
    }),
  );

  List<(String, String, int, int)> _rows(String year) {
    final brazilClubs = brazilClubsByYear[year] ?? 20;
    return countryRowsByYear[year] ??
        [
          ('DE', 'Alemanha', 18, 18),
          ('AR', 'Argentina', 0, 0),
          ('AT', 'Áustria', 28, 28),
          ('BR', 'Brasil', brazilClubs, brazilClubs),
          ('ES', 'Espanha', 20, 20),
          ('FR', 'França', 18, 18),
          ('GB', 'Inglaterra', 44, 44),
          ('IT', 'Itália', 20, 20),
          ('PT', 'Portugal', 18, 18),
          ('UY', 'Uruguai', uruguayClubs, uruguayClubs),
        ];
  }
}

Future<void> _creator(
  WidgetTester tester,
  _CreatorFixture fixture,
  double width, {
  bool classic = false,
}) async {
  tester.view.physicalSize = Size(width, 900);
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);
  final currentTheme = ClassicTheme.classicGreen();
  await tester.pumpWidget(
    MaterialApp(
      theme:
          (classic ? classic98PresentationPalette(currentTheme) : currentTheme)
              .material(),
      home: WorldCreatorScreen(
        api: fixture.api,
        coachId: '11111111-1111-1111-1111-111111111111',
        worldCatalogRules: true,
      ),
    ),
  );
  await tester.pumpAndSettle();
}

Finder _countryCard(String code) => find.byKey(ValueKey('country-card-$code'));

Finder _divisionSlot(int division, int position) =>
    find.byKey(ValueKey('division-$division-position-$position'));

Finder _swapButton(int division, int position) =>
    find.byKey(ValueKey('swap-division-$division-position-$position'));

Future<void> _pressContinue(WidgetTester tester) async {
  final button = find.text('CONTINUAR');
  await tester.ensureVisible(button);
  await tester.tap(button);
  await tester.pumpAndSettle();
  expect(tester.takeException(), isNull);
}

Future<void> _next(WidgetTester tester) async {
  final fromCategory = find.text('1/8 • CATEGORIA').evaluate().isNotEmpty;
  final fromCountries = find.text('2/8 • PAÍSES').evaluate().isNotEmpty;
  await _pressContinue(tester);
  if (fromCategory && find.text('0/8 • Países').evaluate().isNotEmpty) {
    await selectHistoricalCountries(tester, [
      for (final code in ['DE', 'BR', 'ES', 'FR', 'GB', 'IT', 'PT'])
        if (_countryCard(code).evaluate().isNotEmpty) code,
    ]);
  }
  if (fromCountries && find.text('3/8 • ESCOLHER ANOS').evaluate().isNotEmpty) {
    final yearCards = tester.widgetList<CheckboxListTile>(
      find.byType(CheckboxListTile),
    );
    if (yearCards.isNotEmpty && !yearCards.any((card) => card.value == true)) {
      await _tapVisible(tester, find.byKey(yearCards.first.key!));
    }
    await _pressContinue(tester);
  }
}

Future<void> _chooseFictionalRoster(WidgetTester tester) async {
  final generated = find.byKey(const ValueKey('source-generated'));
  await tester.ensureVisible(generated);
  await tester.tap(generated);
  await tester.pumpAndSettle();
}

Finder _districtControl(String key) => find.byKey(ValueKey(key));

Finder _districtRow(int index) => _districtControl('district-test-$index');

Future<void> _openDistricts(WidgetTester tester) async {
  for (var step = 0; step < 3; step++) {
    await _next(tester);
  }
  expect(find.text('5/8 • DISTRITAIS'), findsOneWidget);
}

Future<void> _tapVisible(WidgetTester tester, Finder finder) async {
  await tester.ensureVisible(finder);
  await tester.tap(finder);
  await tester.pumpAndSettle();
  expect(tester.takeException(), isNull);
}

void _districtMetric(String key, String value) {
  expect(
    find.descendant(of: _districtControl(key), matching: find.text(value)),
    findsOneWidget,
  );
}

void main() {
  for (final width in [360.0, 390.0, 430.0, 768.0, 1366.0, 1920.0]) {
    testWidgets('Distritais mostra os dados e controles em $width px', (
      tester,
    ) async {
      final fixture = _CreatorFixture()
        ..clubCount = 128
        ..includeOutsideClub = true;
      await _creator(
        tester,
        fixture,
        width,
        classic: width == 390 || width == 1920,
      );
      await _openDistricts(tester);
      _districtMetric('districts-count', '96/96');
      _districtMetric('districts-available-count', '96');
      _districtMetric('districts-division-count', '4');
      expect(
        _districtControl(
          width >= 1366 ? 'district-desktop-table' : 'district-mobile-cards',
        ),
        findsOneWidget,
      );
      expect(_districtRow(0), findsNothing);
      expect(_districtControl('district-outside-test'), findsNothing);
      expect(_districtControl('district-crest-test-32'), findsOneWidget);
      expect(
        tester
            .widget<CreatorCountryFlag>(
              _districtControl('district-flag-test-32'),
            )
            .code,
        isNotEmpty,
      );
      await _tapVisible(tester, _districtControl('district-page-next'));
      expect(find.text('Mostrando 11–20 de 96 clubes'), findsOneWidget);
      expect(_districtRow(42), findsOneWidget);
      expect(_districtRow(32), findsNothing);
    });
  }

  testWidgets('Consulta distrital preserva filtro, página e escolhas manuais', (
    tester,
  ) async {
    final fixture = _CreatorFixture()..clubCount = 136;
    await _creator(tester, fixture, 1366);
    await _openDistricts(tester);

    await _tapVisible(tester, _districtControl('district-country-filter'));
    await _tapVisible(tester, find.text('Espanha').last);
    await _tapVisible(tester, _districtControl('district-order-filter'));
    await _tapVisible(tester, find.text('Força').last);
    await _tapVisible(tester, _districtControl('district-page-next'));
    final range = tester
        .widget<Text>(_districtControl('district-page-range'))
        .data;
    expect(range, startsWith('Mostrando 11–'));
    await _tapVisible(tester, find.text('VOLTAR'));
    await _next(tester);
    expect(
      tester.widget<Text>(_districtControl('district-page-range')).data,
      range,
    );

    await _tapVisible(tester, _districtControl('Distritais:Ranking'));
    await _tapVisible(tester, find.text('Escolher os 96 manualmente').last);
    await _tapVisible(tester, _districtControl('district-country-filter'));
    await _tapVisible(tester, find.text('Todos os países').last);
    final search = _districtControl('district-search');
    await tester.enterText(search, 'clube inexistente');
    await tester.pumpAndSettle();
    expect(_districtControl('district-empty'), findsOneWidget);
    await tester.enterText(search, 'Clube de teste 127');
    await tester.pumpAndSettle();
    await _tapVisible(tester, _districtControl('district-checkbox-test-127'));
    _districtMetric('districts-count', '1/96');
    await _tapVisible(tester, _districtControl('district-status-filter'));
    await _tapVisible(tester, find.text('Selecionados').last);
    expect(_districtRow(127), findsOneWidget);
    expect(
      tester
          .widget<ElevatedButton>(
            find.widgetWithText(ElevatedButton, 'CONTINUAR'),
          )
          .onPressed,
      isNull,
    );
  });

  final continueButton = find.widgetWithText(ElevatedButton, 'CONTINUAR');

  for (final classic in [false, true]) {
    testWidgets('Países locais respeitam a categoria ($classic)', (
      tester,
    ) async {
      final fixture = _CreatorFixture()
        ..countryRowsByYear.addAll({
          '2026': [
            ('BR', 'Brasil', 9, 1),
            ('DE', 'Alemanha', 4, 0),
            ('AR', 'Argentina', 0, 3),
          ],
          '2025': [('BR', 'Brasil', 14, 0)],
        });
      await _creator(tester, fixture, classic ? 390 : 1366, classic: classic);
      expect(find.text('ANO DO CATÁLOGO'), findsNothing);
      await _pressContinue(tester);
      expect(_countryCard('BR'), findsOneWidget);
      expect(_countryCard('DE'), findsOneWidget);
      expect(_countryCard('AR'), findsNothing);
      expect(find.text('0/8 • Países'), findsOneWidget);
      expect(find.text('CACHE, ATUALIZAÇÕES E COBERTURA'), findsNothing);
      expect(tester.widget<ElevatedButton>(continueButton).onPressed, isNull);

      await _tapVisible(tester, find.text('VOLTAR'));
      await _tapVisible(
        tester,
        find.widgetWithText(DropdownButtonFormField<String>, 'Categoria'),
      );
      await _tapVisible(tester, find.text('FEMININO').last);
      await _pressContinue(tester);
      expect(_countryCard('BR'), findsOneWidget);
      expect(_countryCard('DE'), findsNothing);
      expect(_countryCard('AR'), findsOneWidget);
      expect(
        find.textContaining(RegExp(r'^3 clubes\s+disponíveis$')),
        findsOneWidget,
      );
      await selectHistoricalCountries(tester, ['AR']);
      expect(find.text('1/8 • Países'), findsOneWidget);
      await _pressContinue(tester);
      expect(historicalYear('2026'), findsOneWidget);
      expect(historicalYear('2025'), findsNothing);
      expect(tester.widget<ElevatedButton>(continueButton).onPressed, isNull);

      expect(
        fixture.requests
            .where((r) => r.url.path.endsWith('/historical-countries'))
            .map((r) => r.url.queryParameters),
        [
          {'gender': 'Male'},
          {'gender': 'Female'},
        ],
      );
    });
  }

  testWidgets('Sem clubes, o Criador bloqueia a etapa de países', (
    tester,
  ) async {
    final fixture = _CreatorFixture()..seasons = [];
    await _creator(tester, fixture, 1366);
    await _pressContinue(tester);
    expect(
      find.text(
        'Nenhum país com clubes disponíveis para o catálogo selecionado.',
      ),
      findsOneWidget,
    );
    expect(tester.widget<ElevatedButton>(continueButton).onPressed, isNull);
  });

  Map<String, dynamic> spainCatalog({required bool ready}) => {
    'catalogId': 'es-2026-principal',
    'country': {'code': 'ES', 'name': 'Espanha'},
    'year': 2026,
    'clubCount': 172,
    'ready': ready,
    'valid': true,
  };

  testWidgets('Espanha em rascunho oferece 172 clubes para montar 128 vagas', (
    tester,
  ) async {
    final fixture = _CreatorFixture()
      ..seasons = []
      ..offlineCatalogs = [spainCatalog(ready: false)];
    await _creator(tester, fixture, 1366);
    await _pressContinue(tester);
    expect(find.text('Espanha'), findsOneWidget);
    expect(find.byType(CheckboxListTile), findsOneWidget);
    expect(tester.widget<ElevatedButton>(continueButton).onPressed, isNull);
    await selectHistoricalCountries(tester, ['ES']);
    expect(find.text('1/8 • Países'), findsOneWidget);
    expect(find.text('Catálogo histórico offline'), findsNothing);
    await _pressContinue(tester);
    expect(find.text('2026 • 172 clubes'), findsOneWidget);
    expect(find.text('Espanha (172) • Rascunho'), findsOneWidget);
    expect(
      tester.widget<CheckboxListTile>(historicalYear('2026')).value,
      false,
    );
    await selectHistoricalYear(tester, '2026');
    expect(find.text('1 ano selecionado'), findsOneWidget);
    expect(tester.widget<ElevatedButton>(continueButton).onPressed, isNotNull);
    await _tapVisible(tester, find.text('VOLTAR'));
    expect(find.text('1/8 • Países'), findsOneWidget);
    await _pressContinue(tester);
    expect(tester.widget<CheckboxListTile>(historicalYear('2026')).value, true);
    await _pressContinue(tester);
    final divisions = tester.widget<CreatorDivisions>(
      find.byType(CreatorDivisions),
    );
    expect(divisions.eligibleClubs, hasLength(172));
    expect(
      divisions.divisions.expand((d) => d).whereType<Map>(),
      hasLength(32),
    );
    await _pressContinue(tester);
    final districts = tester.widget<CreatorDistricts>(
      find.byType(CreatorDistricts),
    );
    expect(districts.availableCount, 140);
    expect(districts.districtCount, 96);
    await _pressContinue(tester);
    expect(find.textContaining('Ainda há dados pendentes'), findsOneWidget);
    expect(find.text('G • Goleiro cadastrado 0'), findsOneWidget);
    expect(find.text('1 com comportamento pendente.'), findsOneWidget);
    final clubDropdown = find.widgetWithText(
      DropdownButtonFormField<String>,
      'Clube',
    );
    expect(
      tester.widget<DropdownButtonFormField<String>>(clubDropdown).initialValue,
      'offline-0',
    );
    await _tapVisible(tester, clubDropdown);
    await _tapVisible(tester, find.text('Clube espanhol 1').last);
    expect(find.text('G • Goleiro cadastrado 1'), findsOneWidget);
    await _pressContinue(tester);
    expect(
      find.byKey(const ValueKey('creator-preview-review')),
      findsOneWidget,
    );
    expect(
      find.text(
        'Prévia de rascunho. Valide o catálogo para criar o campeonato.',
      ),
      findsOneWidget,
    );
    await _pressContinue(tester);
    expect(
      find.byKey(const ValueKey('creator-confirmation-review')),
      findsOneWidget,
    );
    final confirm = find.widgetWithText(ElevatedButton, 'CONFIRMAR CRIAÇÃO');
    expect(tester.widget<ElevatedButton>(confirm).onPressed, isNull);
    expect(
      fixture.requests.where(
        (r) => r.url.path.endsWith('/offline/planning-preview'),
      ),
      hasLength(1),
    );
    expect(
      fixture.requests.any((r) => r.url.path.endsWith('/offline/preview')),
      isFalse,
    );
  });

  for (final useDefaults in [false, true]) {
    testWidgets(
      'Catálogo jogável segue até confirmar (comportamento padrão: $useDefaults)',
      (tester) async {
        final fixture = _CreatorFixture()
          ..seasons = []
          ..offlineCatalogs = [
            {
              ...spainCatalog(ready: !useDefaults),
              'canUseDefaultBehavior': useDefaults,
            },
          ];
        await _creator(tester, fixture, 1366);
        await _pressContinue(tester);
        await selectHistoricalCountries(tester, ['ES']);
        await _pressContinue(tester);
        await selectHistoricalYear(tester, '2026');
        await _pressContinue(tester);
        expect(find.text('Clube espanhol 0'), findsWidgets);
        await _pressContinue(tester);
        await _pressContinue(tester);
        expect(
          useDefaults
              ? find.textContaining('seis comportamentos em proporções iguais')
              : find.text(
                  'Os elencos reais serão carregados do catálogo selecionado.',
                ),
          findsOneWidget,
        );
        await _pressContinue(tester);
        await _pressContinue(tester);
        await _tapVisible(tester, find.text('CONFIRMAR CRIAÇÃO'));
        expect(
          fixture.requests.where(
            (r) => r.url.path.endsWith('/offline/confirm/offline-test'),
          ),
          hasLength(1),
        );
      },
    );
  }

  testWidgets('Dois países exibem cobertura e anos dinâmicos ao voltar', (
    tester,
  ) async {
    final fixture = _CreatorFixture()
      ..seasons = ['2025', '1998', '2000']
      ..countryRowsByYear.addAll({
        '2025': [('PT', 'Portugal', 20, 0), ('ES', 'Espanha', 15, 0)],
        '2000': [('AR', 'Argentina', 15, 0)],
        '1998': [('PT', 'Portugal', 18, 0)],
      });
    await _creator(tester, fixture, 1366);
    await _pressContinue(tester);
    await selectHistoricalCountries(tester, ['PT', 'ES']);
    await _pressContinue(tester);
    expect(find.text('2025 • 35 clubes'), findsOneWidget);
    expect(find.text('1998 • 18 clubes'), findsOneWidget);
    expect(find.text('Portugal (18)'), findsOneWidget);
    expect(find.text('Sem clubes em Espanha'), findsOneWidget);
    expect(find.byType(CreatorCalendarYearBadge), findsNWidgets(2));
    expect(find.text('0 anos selecionados'), findsOneWidget);
    expect(historicalYear('2000'), findsNothing);
    await selectHistoricalYear(tester, '1998');
    expect(find.text('1 ano selecionado'), findsOneWidget);

    fixture.seasons.add('1985');
    fixture.countryRowsByYear['1985'] = [('ES', 'Espanha', 1, 0)];
    await _tapVisible(tester, find.text('VOLTAR'));
    expect(find.text('2/8 • Países'), findsOneWidget);
    await _pressContinue(tester);
    expect(tester.widget<CheckboxListTile>(historicalYear('1998')).value, true);
    expect(find.text('1985 • 1 clube'), findsOneWidget);
    await selectHistoricalYear(tester, '2025');
    expect(find.text('2 anos selecionados'), findsOneWidget);
    await _pressContinue(tester);

    final request = fixture.requests.singleWhere(
      (r) => r.url.path.endsWith('/catalog'),
    );
    expect(jsonDecode(request.body), containsPair('season', '2025'));
    expect(
      jsonDecode(request.body),
      containsPair('catalogYears', ['2025', '1998']),
    );
    expect(jsonDecode(request.body), containsPair('countries', ['ES', 'PT']));
    expect(find.text('Clube de teste 0 (2025)'), findsOneWidget);
    expect(find.text('Clube de teste 1 (1998)'), findsOneWidget);
  });

  testWidgets('Ano removido ou consulta com falha exige nova seleção válida', (
    tester,
  ) async {
    final fixture = _CreatorFixture()..seasons = ['2027'];
    await _creator(tester, fixture, 1366);
    await _pressContinue(tester);
    await selectHistoricalCountries(tester, ['BR']);
    await _pressContinue(tester);
    await selectHistoricalYear(tester, '2027');
    await _tapVisible(tester, find.text('VOLTAR'));

    fixture.seasonFailures = 1;
    fixture.seasons = ['2024'];
    await _pressContinue(tester);
    expect(
      find.text('Não foi possível consultar os anos do catálogo local.'),
      findsOneWidget,
    );
    expect(tester.widget<ElevatedButton>(continueButton).onPressed, isNull);
    await _tapVisible(tester, find.text('TENTAR NOVAMENTE'));
    expect(historicalYear('2027'), findsNothing);
    expect(tester.widget<ElevatedButton>(continueButton).onPressed, isNull);
    await selectHistoricalYear(tester, '2024');
    await _pressContinue(tester);
    expect(find.text('4/8 • DIVISÕES'), findsOneWidget);
    expect(find.text('Clube de teste 0'), findsOneWidget);
    expect(find.text('Clube de teste 0 (2024)'), findsNothing);
  });

  testWidgets('Troca a variante de um clube e devolve a anterior', (
    tester,
  ) async {
    final fixture = _CreatorFixture()
      ..clubCount = 34
      ..sameClubInDifferentYears = true;
    await _creator(tester, fixture, 1366);
    await _pressContinue(tester);
    await selectHistoricalCountries(tester, ['BR']);
    await _pressContinue(tester);
    await selectHistoricalYear(tester, '2026');
    await selectHistoricalYear(tester, '2025');
    await _pressContinue(tester);
    expect(find.text('Clube da ponte (2026)'), findsOneWidget);
    expect(find.text('Clube de teste 1 (2025)'), findsOneWidget);

    await _tapVisible(tester, _swapButton(1, 1));
    final countryFilter = find.byKey(const ValueKey('division-swap-country'));
    final yearFilter = find.byKey(const ValueKey('division-swap-year'));
    await _tapVisible(tester, countryFilter);
    await _tapVisible(tester, find.text('BR').last);
    await _tapVisible(tester, yearFilter);
    await _tapVisible(tester, find.text('2025').last);
    await tester.enterText(
      find.byKey(const ValueKey('division-swap-search')),
      'Clube da ponte',
    );
    await tester.pumpAndSettle();
    await _tapVisible(
      tester,
      find.byKey(const ValueKey('division-swap-option-test-33')),
    );
    await _tapVisible(
      tester,
      find.byKey(const ValueKey('division-swap-confirm')),
    );
    expect(
      find.descendant(
        of: _divisionSlot(1, 1),
        matching: find.text('Clube da ponte (2025)'),
      ),
      findsOneWidget,
    );
    expect(_divisionSlot(1, 9), findsNothing);
    await _tapVisible(tester, find.text('VER CLUBES NÃO ESCALADOS'));
    expect(
      find.byKey(const ValueKey('division-unassigned-test-0')),
      findsOneWidget,
    );
    expect(
      find.byKey(const ValueKey('division-unassigned-test-33')),
      findsNothing,
    );
  });

  testWidgets(
    'Países podem ser escolhidos quando a configuração anterior falha',
    (tester) async {
      final fixture = _CreatorFixture()..failLastConfiguration = true;
      await _creator(tester, fixture, 1366);
      await _pressContinue(tester);
      await selectHistoricalCountries(tester, ['BR']);
      await _next(tester);
      expect(find.text('4/8 • DIVISÕES'), findsOneWidget);
    },
  );
  testWidgets('Falha no catálogo tem recuperação na etapa de países', (
    tester,
  ) async {
    final fixture = _CreatorFixture()..countryFailures = 1;
    await _creator(tester, fixture, 1366);
    await _next(tester);
    expect(
      find.text('Não foi possível consultar os países do catálogo local.'),
      findsOneWidget,
    );
    await tester.tap(find.text('TENTAR NOVAMENTE'));
    await tester.pumpAndSettle();
    expect(find.byKey(const ValueKey('country-card-BR')), findsOneWidget);
    expect(
      find.text('Não foi possível consultar os países do catálogo local.'),
      findsNothing,
    );
  });

  for (final width in [360.0, 390.0, 430.0, 768.0, 1366.0, 1920.0]) {
    testWidgets(
      'Países, Divisões e clube mantêm controles acessíveis em $width px',
      (tester) async {
        final fixture = _CreatorFixture()..longClubName = true;
        await _creator(tester, fixture, width);
        await _next(tester);
        expect(find.text('7/8 • Países'), findsOneWidget);
        expect(_countryCard('AR'), findsNothing);
        expect(_countryCard('UY'), findsNothing);
        final germany = tester.getRect(
          find.byKey(const ValueKey('country-card-DE')),
        );
        final france = tester.getRect(
          find.byKey(const ValueKey('country-card-GB')),
        );
        if (width >= 1366) {
          expect(france.left, greaterThan(germany.left));
        } else {
          expect(france.left, germany.left);
        }
        await _next(tester);
        expect(find.text('4/8 • DIVISÕES'), findsOneWidget);
        final planner = tester.widget<CreatorDivisions>(
          find.byType(CreatorDivisions),
        );
        expect(planner.divisions.expand((division) => division), hasLength(32));
        final desktop = width >= 1366;
        expect(
          find.text(
            desktop
                ? '32 clubes • 4 divisões • 8 clubes por divisão'
                : '32 clubes • 4 divisões',
          ),
          findsOneWidget,
        );
        if (desktop) {
          expect(find.byKey(const ValueKey('division-tab-1')), findsNothing);
          final firstRows = [
            for (var division = 1; division <= 4; division++)
              tester.getRect(_divisionSlot(division, 1)),
          ];
          for (var division = 1; division < firstRows.length; division++) {
            expect(
              firstRows[division].left,
              greaterThanOrEqualTo(firstRows[division - 1].right),
            );
            expect(firstRows[division].top, firstRows.first.top);
          }
        }
        for (var division = 1; division <= 4; division++) {
          if (!desktop) {
            final tab = find.byKey(ValueKey('division-tab-$division'));
            await tester.ensureVisible(tab);
            await tester.tap(tab);
            await tester.pumpAndSettle();
            expect(_divisionSlot(division == 1 ? 2 : 1, 1), findsNothing);
          }
          for (var position = 1; position <= 8; position++) {
            final row = _divisionSlot(division, position);
            final flag = find.descendant(
              of: row,
              matching: find.byType(CreatorCountryFlag),
            );
            final club = planner.divisions[division - 1][position - 1]!;
            expect(
              tester.widget<CreatorCountryFlag>(flag).code,
              club['countryCode'],
            );
            final name = find.descendant(
              of: row,
              matching: find.text(club['name'] as String),
            );
            expect(
              tester.getRect(flag).right,
              lessThan(tester.getRect(name).left),
            );
            final button = tester.getRect(_swapButton(division, position));
            expect(button.right, lessThanOrEqualTo(width));
            if (!desktop) expect(button.height, greaterThanOrEqualTo(44));
          }
          expect(_divisionSlot(division, 9), findsNothing);
          expect(tester.takeException(), isNull);
        }
        final swap = _swapButton(desktop ? 1 : 4, 1);
        await tester.ensureVisible(swap);
        await tester.tap(swap);
        await tester.pumpAndSettle();
        expect(find.text('TROCAR CLUBE'), findsOneWidget);
        if (desktop) {
          expect(find.byType(Dialog), findsOneWidget);
        } else if (width <= 430) {
          expect(find.byType(BottomSheet), findsOneWidget);
        }
        final modalConfirm = tester.getRect(
          find.byKey(const ValueKey('division-swap-confirm')),
        );
        expect(modalConfirm.right, lessThanOrEqualTo(width));
        expect(modalConfirm.bottom, lessThanOrEqualTo(900));
        expect(tester.takeException(), isNull);
        await tester.tap(find.byKey(const ValueKey('division-swap-cancel')));
        await tester.pumpAndSettle();
        expect(find.text('Como montar'), findsNothing);
        expect(find.text('Modo de jogo'), findsNothing);
        expect(tester.takeException(), isNull);
        expect(
          tester.getRect(find.text('CONTINUAR')).bottom,
          lessThanOrEqualTo(900),
        );
        await tester.tap(find.text('VOLTAR'));
        await tester.pumpAndSettle();
        expect(find.text('3/8 • ESCOLHER ANOS'), findsOneWidget);
        await tester.tap(find.text('VOLTAR'));
        await tester.pumpAndSettle();
        expect(find.text('7/8 • Países'), findsOneWidget);
        await tester.pumpWidget(
          MaterialApp(
            theme: ClassicTheme.classicGreen().material(),
            home: Builder(
              builder: (context) => Scaffold(
                body: TextButton(
                  onPressed: () => showDialog<String>(
                    context: context,
                    builder: (_) => ClubSelectionDialog(
                      clubs: [
                        for (int i = 0; i < 32; i++)
                          {
                            'id': 'club-$i',
                            'name':
                                'Equipe de nome comprido para verificar a leitura no celular $i',
                            'divisionDisplay': '${i ~/ 8 + 1}ª Divisão',
                            'cashBalance': 4200000,
                            'primaryColorHex': '#C8102E',
                            'secondaryColorHex': '#FFFFFF',
                            if (i == 1)
                              'colorPalette': {
                                'primaryColor': '#006437',
                                'secondaryColor': '#FFFFFF',
                              },
                          },
                      ],
                    ),
                  ),
                  child: const Text('ABRIR'),
                ),
              ),
            ),
          ),
        );
        await tester.pumpAndSettle();
        await tester.tap(find.text('ABRIR'));
        await tester.pumpAndSettle();
        expect(find.text('SELECIONE SEU CLUBE'), findsOneWidget);
        expect(find.text('CANCELAR'), findsOneWidget);
        final crests = tester.widgetList<ClubCrest>(find.byType(ClubCrest));
        expect(crests.first.palette?['primaryColor'], '#C8102E');
        expect(crests.elementAt(1).palette?['primaryColor'], '#006437');
        expect(tester.takeException(), isNull);
        await tester.drag(
          find.byKey(const Key('club-selection-list')),
          const Offset(0, -1400),
        );
        await tester.pumpAndSettle();
        expect(find.text('CANCELAR'), findsOneWidget);
        expect(tester.takeException(), isNull);
        await tester.tap(find.text('CANCELAR'));
        await tester.pumpAndSettle();
        expect(find.byKey(const Key('club-selection-dialog')), findsNothing);
        fixture.api.disconnectRealtime();
      },
    );
  }

  testWidgets(
    'Pesquisa, limite de oito e limpar usam o estado real dos países',
    (tester) async {
      final fixture = _CreatorFixture()..uruguayClubs = 10;
      await _creator(tester, fixture, 390);
      await _next(tester);
      await tester.enterText(find.byType(TextField), 'Argentina');
      await tester.pumpAndSettle();
      expect(_countryCard('AR'), findsNothing);
      expect(find.text('Nenhum país corresponde à pesquisa.'), findsOneWidget);
      expect(find.text('7/8 • Países'), findsOneWidget);
      await tester.enterText(find.byType(TextField), 'Áustria');
      await tester.pumpAndSettle();
      await tester.tap(_countryCard('AT'));
      await tester.pumpAndSettle();
      expect(find.text('8/8 • Países'), findsOneWidget);
      await tester.enterText(find.byType(TextField), 'Uruguai');
      await tester.pumpAndSettle();
      await tester.tap(_countryCard('UY'));
      await tester.pumpAndSettle();
      expect(find.text('Selecione no máximo oito países.'), findsOneWidget);
      expect(find.text('8/8 • Países'), findsOneWidget);
      await tester.enterText(find.byType(TextField), 'Brasil');
      await tester.pumpAndSettle();
      await tester.tap(_countryCard('BR'));
      await tester.pumpAndSettle();
      expect(find.text('7/8 • Países'), findsOneWidget);
      await tester.enterText(find.byType(TextField), 'Uruguai');
      await tester.pumpAndSettle();
      await tester.tap(_countryCard('UY'));
      await tester.pumpAndSettle();
      expect(find.text('8/8 • Países'), findsOneWidget);
      await tester.ensureVisible(find.text('LIMPAR SELEÇÃO'));
      await tester.tap(find.text('LIMPAR SELEÇÃO'));
      await tester.pumpAndSettle();
      expect(find.text('0/8 • Países'), findsOneWidget);
      expect(tester.widget<ElevatedButton>(continueButton).onPressed, isNull);
      expect(
        fixture.requests.where((r) => r.url.path.endsWith('/catalog')),
        isEmpty,
      );
      fixture.api.disconnectRealtime();
    },
  );

  testWidgets('Elencos e fontes preservam a escolha ao voltar e avançar', (
    tester,
  ) async {
    final fixture = _CreatorFixture()..clubCount = 128;
    await _creator(tester, fixture, 1366);
    await _next(tester);
    await _next(tester);
    await _next(tester);
    await _next(tester);
    expect(find.text('6/8 • ELENCOS / FONTES'), findsOneWidget);
    expect(find.text('OPÇÕES AVANÇADAS E FONTES'), findsNothing);
    await _chooseFictionalRoster(tester);
    await tester.tap(find.text('VOLTAR'));
    await tester.pumpAndSettle();
    await _next(tester);
    expect(
      find.descendant(
        of: find.byKey(const ValueKey('source-generated')),
        matching: find.byIcon(Icons.radio_button_checked),
      ),
      findsOneWidget,
    );
    await _next(tester);
    final payload =
        jsonDecode(
              fixture.requests
                  .lastWhere((r) => r.url.path.endsWith('/preview'))
                  .body,
            )
            as Map;
    expect(payload['dataMode'], 'REAL_CLUBS_SIMULATED');
    expect(payload['allowProviderReportedPlayers'], isNot(true));
    expect(payload['generateAllRosterPlayers'], true);
    expect(payload['allowFictionalTeams'], false);
    expect(payload['useSavedData'], true);
    expect(payload['countries'], hasLength(7));
    fixture.api.disconnectRealtime();
  });

  testWidgets('Criador mundial usa catálogo salvo mesmo com preset legado', (
    tester,
  ) async {
    final fixture = _CreatorFixture()
      ..clubCount = 128
      ..lastConfiguration = {
        'name': 'Liga austríaca',
        'gender': 'Male',
        'season': '2025',
        'countries': ['AT'],
        'mode': 'Automatic',
        'districtMode': 'Ranking',
        'useSavedData': false,
      };
    await _creator(tester, fixture, 1366);
    await tester.tap(find.text('USAR ÚLTIMA CONFIGURAÇÃO'));
    await tester.pumpAndSettle();
    await _next(tester);
    expect(find.text('CACHE, ATUALIZAÇÕES E COBERTURA'), findsNothing);
    await _next(tester);
    await _next(tester);
    await _next(tester);
    expect(find.text('6/8 • ELENCOS / FONTES'), findsOneWidget);
    expect(find.text('OPÇÕES AVANÇADAS E FONTES'), findsNothing);
    expect(find.text('Usar imediatamente os dados salvos'), findsNothing);
    final request = fixture.requests.singleWhere(
      (request) => request.url.path.endsWith('/catalog'),
    );
    expect(jsonDecode(request.body), containsPair('useSavedData', true));
    expect(jsonDecode(request.body), containsPair('countries', ['AT']));
    expect(jsonDecode(request.body), containsPair('catalogYears', ['2025']));
  });

  testWidgets('Uma vaga bloqueia Continuar até haver uma opção autorizada', (
    tester,
  ) async {
    final fixture = _CreatorFixture()..clubCount = 31;
    await _creator(tester, fixture, 1366);
    await _next(tester);
    await _next(tester);
    expect(find.text('4/8 • DIVISÕES'), findsOneWidget);
    expect(_divisionSlot(4, 8), findsOneWidget);
    expect(_swapButton(4, 8), findsNothing);
    expect(find.text('Vaga disponível'), findsOneWidget);
    expect(
      find.textContaining(
        RegExp(r'Faltam? 1 clubes? para preencher as divisões'),
      ),
      findsOneWidget,
    );
    expect(tester.widget<ElevatedButton>(continueButton).onPressed, isNull);
    expect(
      fixture.requests.where((r) => r.url.path.endsWith('/preview')),
      isEmpty,
    );

    final adjust = find.text('AJUSTAR ELENCOS E FONTES');
    await tester.ensureVisible(adjust);
    await tester.tap(adjust);
    await tester.pumpAndSettle();
    await _chooseFictionalRoster(tester);
    final apply = find.text('APLICAR OPÇÕES');
    await tester.ensureVisible(apply);
    await tester.tap(apply);
    await tester.pumpAndSettle();
    expect(find.text('4/8 • DIVISÕES'), findsOneWidget);
    expect(find.textContaining('O mundo totalmente fictício'), findsOneWidget);
    expect(tester.widget<ElevatedButton>(continueButton).onPressed, isNotNull);
    await _next(tester);
    expect(find.text('5/8 • DISTRITAIS'), findsOneWidget);
    await _next(tester);
    expect(
      find.textContaining('128 clubes e todos os atletas fictícios'),
      findsOneWidget,
    );
    await _next(tester);
    final preview = fixture.requests.singleWhere(
      (request) => request.url.path.endsWith('/preview'),
    );
    final payload = jsonDecode(preview.body) as Map;
    expect(payload['fictionalOnly'], true);
    expect(payload['generateAllRosterPlayers'], true);
    expect(payload['allowFictionalTeams'], true);
    expect(payload['assignments'], isEmpty);
  });

  testWidgets(
    'Seletor mobile filtra clubes, confirma e consulta não escalados',
    (tester) async {
      final fixture = _CreatorFixture()..clubCount = 34;
      await _creator(tester, fixture, 390, classic: true);
      await _next(tester);
      await _next(tester);

      final firstSwap = _swapButton(1, 1);
      await tester.ensureVisible(firstSwap);
      await tester.tap(firstSwap);
      await tester.pumpAndSettle();
      expect(find.byType(BottomSheet), findsOneWidget);
      expect(find.text('TROCAR CLUBE'), findsOneWidget);
      expect(find.text('1ª Divisão • Posição 1'), findsOneWidget);

      final search = find.byKey(const ValueKey('division-swap-search'));
      final filter = find.byKey(const ValueKey('division-swap-filter'));
      final confirm = find.byKey(const ValueKey('division-swap-confirm'));
      final club8 = find.byKey(const ValueKey('division-swap-option-test-8'));
      await tester.tap(filter);
      await tester.pumpAndSettle();
      await tester.tap(find.text('2ª Divisão').last);
      await tester.pumpAndSettle();
      expect(
        find.byKey(const ValueKey('division-swap-option-test-1')),
        findsNothing,
      );
      await tester.enterText(search, 'Clube de teste 8');
      await tester.pumpAndSettle();
      expect(club8, findsOneWidget);

      tester.view.viewInsets = const FakeViewPadding(bottom: 325);
      addTearDown(tester.view.resetViewInsets);
      await tester.pumpAndSettle();
      expect(tester.getRect(confirm).bottom, lessThanOrEqualTo(575));
      expect(tester.takeException(), isNull);
      tester.view.resetViewInsets();
      await tester.pumpAndSettle();

      await tester.ensureVisible(club8);
      await tester.tap(club8);
      await tester.pumpAndSettle();
      expect(tester.widget<ListTile>(club8).selected, isTrue);
      expect(find.text('Entra: Clube de teste 8'), findsOneWidget);
      expect(find.text('CONFIRMAR TROCA'), findsOneWidget);
      await tester.tap(confirm);
      await tester.pumpAndSettle();

      Finder clubInSlot(int division, int index, int club) => find.descendant(
        of: _divisionSlot(division, index),
        matching: find.text('Clube de teste $club'),
      );
      expect(clubInSlot(1, 1, 8), findsOneWidget);
      final secondDivision = find.byKey(const ValueKey('division-tab-2'));
      await tester.ensureVisible(secondDivision);
      await tester.tap(secondDivision);
      await tester.pumpAndSettle();
      expect(clubInSlot(2, 1, 0), findsOneWidget);
      expect(clubInSlot(1, 1, 8), findsNothing);

      final unassigned = find.text('VER CLUBES NÃO ESCALADOS');
      await tester.ensureVisible(unassigned);
      await tester.tap(unassigned);
      await tester.pumpAndSettle();
      expect(find.text('2 clubes disponíveis'), findsOneWidget);
      expect(
        find.byKey(const ValueKey('division-unassigned-test-32')),
        findsOneWidget,
      );
      expect(
        find.byKey(const ValueKey('division-unassigned-test-0')),
        findsNothing,
      );
      await tester.enterText(
        find.byKey(const ValueKey('division-unassigned-search')),
        'Clube de teste 33',
      );
      await tester.pumpAndSettle();
      final result = find.byKey(const ValueKey('division-unassigned-test-33'));
      expect(result, findsOneWidget);
      expect(
        find.byKey(const ValueKey('division-unassigned-test-32')),
        findsNothing,
      );
      expect(
        find.descendant(of: result, matching: find.byType(CreatorCountryFlag)),
        findsOneWidget,
      );
      expect(find.textContaining('Não escalado'), findsOneWidget);
      expect(confirm, findsNothing);
      await tester.tap(find.text('FECHAR'));
      await tester.pumpAndSettle();
      expect(clubInSlot(2, 1, 0), findsOneWidget);

      tester.view.physicalSize = const Size(1366, 900);
      await tester.pumpAndSettle();
      expect(clubInSlot(1, 1, 8), findsOneWidget);
      expect(clubInSlot(2, 1, 0), findsOneWidget);
      tester.view.physicalSize = const Size(390, 900);
      await tester.pumpAndSettle();
      expect(clubInSlot(2, 1, 0), findsOneWidget);
      expect(clubInSlot(1, 1, 8), findsNothing);
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets('Trocas são enviadas à prévia na ordem das quatro divisões', (
    tester,
  ) async {
    final fixture = _CreatorFixture()
      ..clubCount = 128
      ..validPreview = true;
    await _creator(tester, fixture, 1366);
    await _next(tester);
    await _next(tester);

    Future<void> swap(
      int position,
      int candidate, {
      bool confirm = true,
    }) async {
      final button = _swapButton(1, position);
      await tester.ensureVisible(button);
      await tester.tap(button);
      await tester.pumpAndSettle();
      expect(find.byType(Dialog), findsOneWidget);
      expect(find.text('TROCAR CLUBES'), findsOneWidget);
      final confirmButton = find.byKey(const ValueKey('division-swap-confirm'));
      expect(tester.widget<ButtonStyleButton>(confirmButton).onPressed, isNull);
      await tester.enterText(
        find.byKey(const ValueKey('division-swap-search')),
        'Clube de teste $candidate',
      );
      await tester.pumpAndSettle();
      final option = find.byKey(
        ValueKey('division-swap-option-test-$candidate'),
      );
      await tester.ensureVisible(option);
      await tester.tap(option);
      await tester.pumpAndSettle();
      expect(
        tester.widget<ButtonStyleButton>(confirmButton).onPressed,
        isNotNull,
      );
      await tester.tap(confirm ? confirmButton : find.text('CANCELAR'));
      await tester.pumpAndSettle();
    }

    void expectSlot(int position, int club) {
      expect(
        find.descendant(
          of: find.byKey(ValueKey('division-1-position-$position')),
          matching: find.text('Clube de teste $club'),
        ),
        findsOneWidget,
      );
    }

    expectSlot(1, 0);
    await swap(1, 1, confirm: false);
    expectSlot(1, 0);
    await swap(1, 1);
    expectSlot(1, 1);
    expectSlot(2, 0);
    await swap(1, 8);
    await swap(3, 32);
    expectSlot(1, 8);
    expectSlot(3, 32);

    await tester.tap(find.text('VOLTAR'));
    await tester.pumpAndSettle();
    expect(find.text('3/8 • ESCOLHER ANOS'), findsOneWidget);
    await _next(tester);
    expectSlot(1, 8);
    expectSlot(3, 32);

    await _next(tester);
    expect(find.text('96/96'), findsOneWidget);
    final returnedClub = find.byKey(const ValueKey('district-test-2'));
    expect(returnedClub, findsOneWidget);
    expect(find.byKey(const ValueKey('district-test-32')), findsNothing);
    await _next(tester);
    await _next(tester);
    final previews = fixture.requests.where(
      (r) => r.url.path.endsWith('/preview'),
    );
    final edited = jsonDecode(previews.single.body) as Map;
    final assignments = (edited['assignments'] as List).cast<Map>();
    expect(edited['preserveRankedSquadStrengths'], isTrue);
    expect(assignments, hasLength(32));
    expect(assignments.map((a) => a['clubKey']).toSet(), hasLength(32));
    for (var division = 1; division <= 4; division++) {
      expect(assignments.where((a) => a['division'] == division), hasLength(8));
    }
    expect(
      assignments.where((a) => a['division'] == 1).map((a) => a['clubKey']),
      [
        'test-8',
        'test-0',
        'test-32',
        'test-3',
        'test-4',
        'test-5',
        'test-6',
        'test-7',
      ],
    );
    expect(
      assignments.firstWhere((a) => a['division'] == 2)['clubKey'],
      'test-1',
    );

    final editDivisions = find.text('EDITAR DIVISÕES');
    await tester.ensureVisible(editDivisions);
    await tester.tap(editDivisions);
    await tester.pumpAndSettle();
    expectSlot(1, 8);
    expectSlot(3, 32);
    final restore = find.byKey(const ValueKey('restore-automatic-divisions'));
    await tester.ensureVisible(restore);
    await tester.tap(restore);
    await tester.pumpAndSettle();
    expect(
      find.text('Deseja restaurar a distribuição automática?'),
      findsOneWidget,
    );
    await tester.tap(find.text('CANCELAR'));
    await tester.pumpAndSettle();
    expectSlot(1, 8);
    await tester.tap(restore);
    await tester.pumpAndSettle();
    await tester.tap(find.text('RESTAURAR'));
    await tester.pumpAndSettle();
    expectSlot(1, 0);
    expectSlot(3, 2);

    await _next(tester);
    await _next(tester);
    await _next(tester);
    final reassigned = fixture.requests.where(
      (request) => request.url.path.endsWith('/reassign'),
    );
    final restored = jsonDecode(reassigned.last.body) as Map;
    expect(restored.containsKey('seed'), isFalse);
    expect(
      (restored['assignments'] as List)
          .where((assignment) => assignment['division'] != 5)
          .map((assignment) => assignment['clubKey']),
      [for (var i = 0; i < 32; i++) 'test-$i'],
    );

    await tester.ensureVisible(editDivisions);
    await tester.tap(editDivisions);
    await tester.pumpAndSettle();
    await swap(1, 1);
    fixture.unavailableClubIds.add(1);
    await tester.tap(find.text('VOLTAR'));
    await tester.pumpAndSettle();
    await _next(tester);
    // A catalog update does not remove identities from an already validated
    // preview. Returning through unchanged countries/years uses that snapshot.
    expectSlot(1, 1);
    expect(find.textContaining('deixaram de estar disponíveis'), findsNothing);
    expect(_swapButton(1, 1), findsOneWidget);
    expect(tester.widget<ElevatedButton>(continueButton).onPressed, isNotNull);
    expect(previews, hasLength(1));
  });
}
