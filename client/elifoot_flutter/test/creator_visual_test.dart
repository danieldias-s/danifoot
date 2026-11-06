import 'dart:convert';
import 'package:elifoot_flutter/api_client.dart';
import 'package:elifoot_flutter/classic98_widgets.dart';
import 'package:elifoot_flutter/club_selection_dialog.dart';
import 'package:elifoot_flutter/club_crest.dart';
import 'package:elifoot_flutter/club_theme.dart';
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
  bool failLastConfiguration = false;
  Map<String, dynamic>? lastConfiguration;
  late final api = ElifootApiClient(
    httpClient: MockClient((request) async {
      requests.add(request);
      final dynamic data;
      if (request.url.path.endsWith('/seasons')) {
        if (seasonFailures > 0) {
          seasonFailures--;
          return http.Response('Catálogo indisponível', 503);
        }
        data = seasons;
      } else if (request.url.path.endsWith('/countries')) {
        if (countryFailures > 0) {
          countryFailures--;
          return http.Response('{"error":"Catálogo indisponível"}', 503);
        }
        final year = request.url.queryParameters['season'];
        final brazilClubs = brazilClubsByYear[year] ?? 20;
        data = [
          for (final row
              in countryRowsByYear[year] ??
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
                  ])
            {
              'code': row.$1,
              'name': row.$2,
              'maleClubs': row.$3,
              'femaleClubs': row.$4,
            },
        ];
      } else if (request.url.path.endsWith('/last-configuration')) {
        if (failLastConfiguration) {
          return http.Response('{"error":"Configuração indisponível"}', 503);
        }
        data = lastConfiguration;
      } else if (request.url.path.endsWith('/catalog')) {
        final countries =
            (jsonDecode(request.body) as Map)['countries'] as List;
        data = {
          'clubs': [
            for (var i = 0; i < clubCount; i++)
              {
                'key': 'test-$i',
                'name': 'Clube de teste $i',
                'countryCode': countries[i % countries.length],
                'countryName': countries[i % countries.length],
                'rank': i + 1,
                'initialProfile': {'clubInitialPower': 20, 'worldRank': i + 1},
                'verification': {'status': 'REAL_CLUB_VERIFIED'},
                'players': [],
              },
          ],
          'references': [],
          'warnings': [],
        };
      } else if (request.url.path.endsWith('/preview')) {
        data = {
          'draftId': 'incomplete-test',
          'clubs': [],
          'pendingSlots': 96,
          'errors': ['Faltam 96 clubes.'],
          'warnings': [],
        };
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

Future<void> _next(WidgetTester tester) async {
  await tester.tap(find.text('CONTINUAR'));
  await tester.pumpAndSettle();
  expect(tester.takeException(), isNull);
}

void main() {
  final yearDropdown = find.widgetWithText(
    DropdownButtonFormField<String>,
    'ANO DO CATÁLOGO',
  );
  final continueButton = find.widgetWithText(ElevatedButton, 'CONTINUAR');

  for (final classic in [false, true]) {
    testWidgets(
      'Países seguem categoria e ano, com atualização (clássica=$classic)',
      (tester) async {
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
        await _next(tester);
        expect(_countryCard('BR'), findsOneWidget);
        expect(_countryCard('DE'), findsOneWidget);
        expect(_countryCard('AR'), findsNothing);
        expect(find.text('2/8 • Países'), findsOneWidget);

        await tester.tap(find.text('VOLTAR'));
        await tester.pumpAndSettle();
        await tester.tap(
          find.widgetWithText(DropdownButtonFormField<String>, 'Categoria'),
        );
        await tester.pumpAndSettle();
        await tester.tap(find.text('FEMININO').last);
        await tester.pumpAndSettle();
        await _next(tester);
        expect(_countryCard('BR'), findsOneWidget);
        expect(_countryCard('DE'), findsNothing);
        expect(_countryCard('AR'), findsOneWidget);
        expect(
          find.textContaining(RegExp(r'^3 clubes\s+disponíveis$')),
          findsOneWidget,
        );
        expect(find.text('1/8 • Países'), findsOneWidget);

        await tester.tap(find.text('VOLTAR'));
        await tester.pumpAndSettle();
        await tester.tap(yearDropdown);
        await tester.pumpAndSettle();
        await tester.tap(find.text('2025').last);
        await tester.pumpAndSettle();
        await _next(tester);
        expect(_countryCard('BR'), findsNothing);
        expect(find.text('0/8 • Países'), findsOneWidget);
        expect(
          find.text(
            'Nenhum país com clubes disponíveis para o catálogo selecionado.',
          ),
          findsOneWidget,
        );
        expect(tester.widget<ElevatedButton>(continueButton).onPressed, isNull);

        fixture.countryRowsByYear['2025'] = [('BR', 'Brasil', 14, 5)];
        final refresh = find.text('CONSULTAR DISPONIBILIDADE DA TEMPORADA');
        await tester.ensureVisible(refresh);
        await tester.tap(refresh);
        await tester.pumpAndSettle();
        expect(_countryCard('BR'), findsOneWidget);
        expect(
          find.textContaining(RegExp(r'^5 clubes\s+disponíveis$')),
          findsOneWidget,
        );
        expect(tester.widget<ElevatedButton>(continueButton).onPressed, isNull);
        await tester.ensureVisible(_countryCard('BR'));
        await tester.tap(
          find.descendant(
            of: _countryCard('BR'),
            matching: find.byType(CheckboxListTile),
          ),
        );
        await tester.pumpAndSettle();
        expect(find.text('1/8 • Países'), findsOneWidget);
        expect(
          tester.widget<ElevatedButton>(continueButton).onPressed,
          isNotNull,
        );

        expect(
          fixture.requests
              .where((r) => r.url.path.endsWith('/countries'))
              .map(
                (r) => (
                  r.url.queryParameters['season'],
                  r.url.queryParameters['gender'],
                ),
              ),
          [
            ('2026', 'Male'),
            ('2026', 'Female'),
            ('2025', 'Female'),
            ('2025', 'Female'),
          ],
        );
      },
    );
  }

  testWidgets('Sem catálogo, o Criador bloqueia a próxima etapa', (
    tester,
  ) async {
    final fixture = _CreatorFixture()..seasons = [];
    await _creator(tester, fixture, 1366);

    expect(find.text('Nenhum catálogo disponível.'), findsOneWidget);
    expect(
      tester.widget<DropdownButtonFormField<String>>(yearDropdown).onChanged,
      isNull,
    );
    expect(tester.widget<ElevatedButton>(continueButton).onPressed, isNull);
    expect(
      fixture.requests.where((r) => r.url.path.endsWith('/countries')),
      isEmpty,
    );
  });

  testWidgets('O ano escolhido filtra países e catálogo ao voltar e avançar', (
    tester,
  ) async {
    final fixture = _CreatorFixture()
      ..seasons = ['2027', '2023']
      ..brazilClubsByYear.addAll({'2027': 31, '2023': 29});
    await _creator(tester, fixture, 1366);

    expect(find.text('2027'), findsOneWidget);
    await tester.tap(yearDropdown);
    await tester.pumpAndSettle();
    await tester.tap(find.text('2023').last);
    await tester.pumpAndSettle();
    await _next(tester);
    expect(find.text('29 clubes disponíveis'), findsOneWidget);
    expect(find.text('31 clubes disponíveis'), findsNothing);
    await tester.tap(find.text('LIMPAR SELEÇÃO'));
    await tester.pumpAndSettle();
    await tester.tap(
      find.descendant(
        of: find.byKey(const ValueKey('country-card-BR')),
        matching: find.byType(CheckboxListTile),
      ),
    );
    await tester.pumpAndSettle();

    fixture.seasons = ['2028', '2027', '2023'];
    fixture.brazilClubsByYear['2023'] = 30;
    await tester.tap(find.text('VOLTAR'));
    await tester.pumpAndSettle();
    expect(find.text('2023'), findsOneWidget);
    await tester.tap(yearDropdown);
    await tester.pumpAndSettle();
    expect(find.text('2028'), findsOneWidget);
    await tester.tap(find.text('2023').last);
    await tester.pumpAndSettle();
    await _next(tester);
    expect(find.text('30 clubes disponíveis'), findsOneWidget);
    expect(find.text('29 clubes disponíveis'), findsNothing);
    expect(find.text('1/8 • Países'), findsOneWidget);
    await _next(tester);
    final catalog = fixture.requests.singleWhere(
      (r) => r.url.path.endsWith('/catalog'),
    );
    expect(jsonDecode(catalog.body), containsPair('season', '2023'));
    expect(jsonDecode(catalog.body), containsPair('countries', ['BR']));
    expect(jsonDecode(catalog.body), containsPair('dataMode', null));
    final countries = fixture.requests.where(
      (r) => r.url.path.endsWith('/countries'),
    );
    expect(countries.map((r) => r.url.queryParameters['season']), [
      '2023',
      '2023',
    ]);
  });

  testWidgets('Ano removido ou consulta com falha exige nova seleção válida', (
    tester,
  ) async {
    final fixture = _CreatorFixture()..seasons = ['2027'];
    await _creator(tester, fixture, 1366);
    expect(find.text('2027'), findsOneWidget);
    await _next(tester);

    fixture.seasonFailures = 1;
    fixture.seasons = ['2024'];
    await tester.tap(find.text('VOLTAR'));
    await tester.pumpAndSettle();
    expect(
      find.text('Não foi possível consultar os anos do catálogo.'),
      findsOneWidget,
    );
    expect(tester.widget<ElevatedButton>(continueButton).onPressed, isNull);
    await tester.tap(find.text('TENTAR NOVAMENTE'));
    await tester.pumpAndSettle();
    expect(
      find.textContaining('O ano selecionado não está mais disponível'),
      findsOneWidget,
    );
    expect(tester.widget<ElevatedButton>(continueButton).onPressed, isNull);
    await tester.tap(yearDropdown);
    await tester.pumpAndSettle();
    expect(find.text('2027'), findsNothing);
    await tester.tap(find.text('2024').last);
    await tester.pumpAndSettle();
    await _next(tester);
    expect(find.text('CACHE, ATUALIZAÇÕES E COBERTURA'), findsOneWidget);
  });

  testWidgets(
    'Países continuam selecionáveis quando a última configuração falha',
    (tester) async {
      final fixture = _CreatorFixture()..failLastConfiguration = true;
      await _creator(tester, fixture, 1366);
      expect(find.text('Seed dos sorteios'), findsNothing);
      await _next(tester);
      await tester.tap(find.text('LIMPAR SELEÇÃO'));
      await tester.pumpAndSettle();
      final brazil = find.descendant(
        of: find.byKey(const ValueKey('country-card-BR')),
        matching: find.byType(CheckboxListTile),
      );
      await tester.ensureVisible(brazil);
      await tester.tap(brazil);
      await tester.pumpAndSettle();
      expect(find.text('1/8 • Países'), findsOneWidget);
      await _next(tester);
      expect(find.text('3/7 • DIVISÕES'), findsOneWidget);
    },
  );

  testWidgets('Falha no catálogo tem recuperação na etapa de países', (
    tester,
  ) async {
    final fixture = _CreatorFixture()..countryFailures = 1;
    await _creator(tester, fixture, 1366);
    await _next(tester);
    expect(
      find.textContaining('A lista de países não está disponível'),
      findsOneWidget,
    );
    await tester.tap(find.text('CONSULTAR DISPONIBILIDADE DA TEMPORADA'));
    await tester.pumpAndSettle();
    expect(find.byKey(const ValueKey('country-card-BR')), findsOneWidget);
    expect(
      find.textContaining('A lista de países não está disponível'),
      findsNothing,
    );
  });

  for (final width in [360.0, 390.0, 430.0, 768.0, 1366.0, 1920.0]) {
    testWidgets(
      'Países, Divisões e clube mantêm controles acessíveis em $width px',
      (tester) async {
        final fixture = _CreatorFixture();
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
        expect(find.text('3/7 • DIVISÕES'), findsOneWidget);
        expect(find.text('Como montar'), findsNothing);
        expect(find.text('Modo de jogo'), findsNothing);
        expect(tester.takeException(), isNull);
        expect(
          tester.getRect(find.text('CONTINUAR')).bottom,
          lessThanOrEqualTo(900),
        );
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

  testWidgets('Elencos e fontes preservam permissões ao voltar e avançar', (
    tester,
  ) async {
    final fixture = _CreatorFixture();
    await _creator(tester, fixture, 1366);
    await _next(tester);
    await _next(tester);
    await _next(tester);
    await _next(tester);
    expect(find.text('5/7 • ELENCOS / FONTES'), findsOneWidget);
    final provider = find.byKey(const Key('allow-provider-reported-016a'));
    expect(tester.widget<CreatorConsent>(provider).onChanged, isNull);
    await tester.ensureVisible(find.text('SOMENTE DADOS REAIS COMPLETOS'));
    await tester.tap(find.text('SOMENTE DADOS REAIS COMPLETOS'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('MUNDO LIVRE').last);
    await tester.pumpAndSettle();
    await tester.ensureVisible(provider);
    await tester.tap(provider);
    await tester.pumpAndSettle();
    await tester.tap(find.text('VOLTAR'));
    await tester.pumpAndSettle();
    await _next(tester);
    expect(find.text('MUNDO LIVRE'), findsOneWidget);
    await _next(tester);
    final payload =
        jsonDecode(
              fixture.requests
                  .lastWhere((r) => r.url.path.endsWith('/preview'))
                  .body,
            )
            as Map;
    expect(payload['dataMode'], 'FREE_WORLD');
    expect(payload['allowProviderReportedPlayers'], true);
    expect(payload['useSavedData'], true);
    expect(payload['countries'], hasLength(7));
    fixture.api.disconnectRealtime();
  });

  testWidgets('Criador mundial usa catálogo salvo mesmo com preset legado', (
    tester,
  ) async {
    final fixture = _CreatorFixture()
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
    expect(find.text('CACHE, ATUALIZAÇÕES E COBERTURA'), findsOneWidget);
    await _next(tester);
    await _next(tester);
    await _next(tester);
    expect(find.text('Usar imediatamente os dados salvos'), findsNothing);
    final request = fixture.requests.singleWhere(
      (request) => request.url.path.endsWith('/catalog'),
    );
    expect(jsonDecode(request.body), containsPair('useSavedData', true));
    expect(jsonDecode(request.body), containsPair('countries', ['AT']));
  });

  testWidgets('Trocas são enviadas à prévia na ordem das quatro divisões', (
    tester,
  ) async {
    final fixture = _CreatorFixture()..clubCount = 33;
    await _creator(tester, fixture, 1366);
    await _next(tester);
    await _next(tester);

    Future<void> swap(int position, int candidate) async {
      final button = find.byKey(ValueKey('swap-division-1-position-$position'));
      await tester.ensureVisible(button);
      await tester.tap(button);
      await tester.pumpAndSettle();
      await tester.enterText(
        find.byKey(const ValueKey('division-swap-search')),
        'Clube de teste $candidate',
      );
      await tester.pumpAndSettle();
      await tester.tap(
        find.byKey(ValueKey('division-swap-option-test-$candidate')),
      );
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
    await swap(1, 1);
    expectSlot(1, 1);
    expectSlot(2, 0);
    await swap(1, 8);
    await swap(3, 32);
    expectSlot(1, 8);
    expectSlot(3, 32);

    await _next(tester);
    expect(find.text('Faltam 95 clubes distritais.'), findsOneWidget);
    await tester.tap(find.byKey(const ValueKey('Distritais:Ranking')));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Escolher os 96 manualmente').last);
    await tester.pumpAndSettle();
    final returnedClub = find.byKey(const ValueKey('district-test-2'));
    expect(returnedClub, findsOneWidget);
    expect(find.byKey(const ValueKey('district-test-32')), findsNothing);
    await tester.ensureVisible(returnedClub);
    await tester.tap(returnedClub);
    await tester.pumpAndSettle();
    await _next(tester);
    await _next(tester);
    final previews = fixture.requests.where(
      (r) => r.url.path.endsWith('/preview'),
    );
    final edited = jsonDecode(previews.single.body) as Map;
    final assignments = (edited['assignments'] as List).cast<Map>();
    expect(edited['preserveRankedSquadStrengths'], isTrue);
    expect(assignments, hasLength(33));
    expect(assignments.map((a) => a['clubKey']).toSet(), hasLength(33));
    expect(
      assignments.singleWhere((a) => a['division'] == 5)['clubKey'],
      'test-2',
    );
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
    expect(find.text('Restaurar distribuição automática?'), findsOneWidget);
    await tester.tap(find.text('RESTAURAR'));
    await tester.pumpAndSettle();
    expectSlot(1, 0);
    expectSlot(3, 2);

    await _next(tester);
    await _next(tester);
    await _next(tester);
    final restored = jsonDecode(previews.last.body) as Map;
    expect(restored['seed'], edited['seed']);
    expect((restored['assignments'] as List).map((a) => a['clubKey']), [
      for (var i = 0; i < 32; i++) 'test-$i',
    ]);
  });
}
