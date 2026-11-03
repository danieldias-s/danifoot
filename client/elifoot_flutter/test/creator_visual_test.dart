import 'dart:convert';
import 'package:elifoot_flutter/api_client.dart';
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
  late final api = ElifootApiClient(
    httpClient: MockClient((request) async {
      requests.add(request);
      final dynamic data;
      if (request.url.path.endsWith('/countries')) {
        data = [
          for (final row in [
            ('DE', 'Alemanha', 18),
            ('AR', 'Argentina', 0),
            ('BR', 'Brasil', 20),
            ('ES', 'Espanha', 20),
            ('FR', 'França', 18),
            ('GB', 'Inglaterra', 44),
            ('IT', 'Itália', 20),
            ('PT', 'Portugal', 18),
            ('UY', 'Uruguai', 0),
          ])
            {
              'code': row.$1,
              'name': row.$2,
              'maleClubs': row.$3,
              'femaleClubs': row.$3,
            },
        ];
      } else if (request.url.path.endsWith('/last-configuration')) {
        data = null;
      } else if (request.url.path.endsWith('/catalog')) {
        data = {'clubs': [], 'references': [], 'warnings': []};
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
  double width,
) async {
  tester.view.physicalSize = Size(width, 900);
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);
  await tester.pumpWidget(
    MaterialApp(
      theme: ClassicTheme.classicGreen().material(),
      home: WorldCreatorScreen(
        api: fixture.api,
        coachId: '11111111-1111-1111-1111-111111111111',
        worldCatalogRules: true,
      ),
    ),
  );
  await tester.pumpAndSettle();
}

Future<void> _next(WidgetTester tester) async {
  await tester.tap(find.text('CONTINUAR'));
  await tester.pumpAndSettle();
  expect(tester.takeException(), isNull);
}

void main() {
  for (final width in [360.0, 390.0, 430.0, 768.0, 1366.0, 1920.0]) {
    testWidgets('Países, Modo e clube mantêm controles acessíveis em $width px', (
      tester,
    ) async {
      final fixture = _CreatorFixture();
      await _creator(tester, fixture, width);
      await _next(tester);
      expect(find.text('8/8 • Países'), findsOneWidget);
      expect(find.textContaining('0 clubes'), findsWidgets);
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
      expect(find.text('3/8 • MODO'), findsOneWidget);
      final provider = tester.widget<CreatorConsent>(
        find.byKey(const Key('allow-provider-reported-016a')),
      );
      expect(provider.onChanged, isNull);
      await tester.ensureVisible(
        find.text('INCLUIR clubes sem verificação de atividade'),
      );
      await tester.pumpAndSettle();
      expect(tester.takeException(), isNull);
      expect(
        tester.getRect(find.text('CONTINUAR')).bottom,
        lessThanOrEqualTo(900),
      );
      await tester.tap(find.text('VOLTAR'));
      await tester.pumpAndSettle();
      expect(find.text('8/8 • Países'), findsOneWidget);
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
    });
  }

  testWidgets(
    'Pesquisa, limite de oito e limpar usam o estado real dos países',
    (tester) async {
      final fixture = _CreatorFixture();
      await _creator(tester, fixture, 390);
      await _next(tester);
      await tester.enterText(find.byType(TextField), 'Uruguai');
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const ValueKey('country-card-UY')));
      await tester.pumpAndSettle();
      expect(find.text('Selecione no máximo oito países.'), findsOneWidget);
      expect(find.text('8/8 • Países'), findsOneWidget);
      await tester.enterText(find.byType(TextField), 'Brasil');
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const ValueKey('country-card-BR')));
      await tester.pumpAndSettle();
      expect(find.text('7/8 • Países'), findsOneWidget);
      await tester.enterText(find.byType(TextField), 'Uruguai');
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const ValueKey('country-card-UY')));
      await tester.pumpAndSettle();
      expect(find.text('8/8 • Países'), findsOneWidget);
      await tester.ensureVisible(find.text('LIMPAR SELEÇÃO'));
      await tester.tap(find.text('LIMPAR SELEÇÃO'));
      await tester.pumpAndSettle();
      expect(find.text('0/8 • Países'), findsOneWidget);
      await tester.tap(find.text('CONTINUAR'));
      await tester.pumpAndSettle();
      expect(
        find.textContaining('Selecione pelo menos um país.'),
        findsOneWidget,
      );
      expect(
        fixture.requests.where((r) => r.url.path.endsWith('/catalog')),
        isEmpty,
      );
      fixture.api.disconnectRealtime();
    },
  );

  testWidgets('Modo mantém permissões, dados salvos e payload de criação', (
    tester,
  ) async {
    final fixture = _CreatorFixture();
    await _creator(tester, fixture, 1366);
    await _next(tester);
    await _next(tester);
    await tester.tap(find.text('SOMENTE DADOS REAIS COMPLETOS'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('MUNDO LIVRE').last);
    await tester.pumpAndSettle();
    final provider = find.byKey(const Key('allow-provider-reported-016a'));
    await tester.ensureVisible(provider);
    await tester.tap(provider);
    await tester.pumpAndSettle();
    await tester.ensureVisible(find.text('Usar imediatamente os dados salvos'));
    await tester.tap(find.text('Usar imediatamente os dados salvos'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('VOLTAR'));
    await tester.pumpAndSettle();
    await _next(tester);
    expect(find.text('MUNDO LIVRE'), findsOneWidget);
    await _next(tester);
    final payload =
        jsonDecode(
              fixture.requests
                  .lastWhere((r) => r.url.path.endsWith('/catalog'))
                  .body,
            )
            as Map;
    expect(payload['dataMode'], 'FREE_WORLD');
    expect(payload['allowProviderReportedPlayers'], true);
    expect(payload['useSavedData'], true);
    expect(payload['countries'], hasLength(8));
    fixture.api.disconnectRealtime();
  });
}
