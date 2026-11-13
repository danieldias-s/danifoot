import 'package:elifoot_flutter/classic98_widgets.dart';
import 'package:elifoot_flutter/club_crest.dart';
import 'package:elifoot_flutter/club_theme.dart';
import 'package:elifoot_flutter/creator_visual.dart';
import 'package:elifoot_flutter/creator_world_review.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

const _catalogCrest =
    'data:image/png;base64,'
    'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+aQm8AAAAASUVORK5CYII=';

Map<String, dynamic> _preview({
  bool blocking = false,
  bool longNames = false,
}) => {
  'clubs': [
    for (var index = 0; index < 128; index++)
      {
        'division': index < 32 ? index ~/ 8 + 1 : 5,
        'club': {
          'key': 'qa-$index',
          'name': longNames
              ? 'Associação Desportiva de Futebol com Nome Extenso para Testar a Revisão $index'
              : index < 2
              ? 'Clube de catálogo'
              : 'Clube teste $index',
          'countryCode': index.isEven ? 'ES' : 'PT',
          'countryName': index.isEven ? 'Espanha' : 'Portugal',
          'season': index.isEven ? '2025' : '1985',
          'provider': 'local-historical',
          if (index == 0)
            'palette': {'primaryColor': '#00512E', 'secondaryColor': '#FFFFFF'},
          if (index < 2) 'offlineSource': {'crestDataUrl': _catalogCrest},
          'players': [
            {
              'playerName': 'Atleta do catálogo $index',
              'position': 'G',
              'playerOrigin': 'REAL_HISTORICAL_CATALOG',
              'source': 'Arquivo de QA',
              'strength': 12,
              'season': index.isEven ? '2025' : '1985',
            },
            {
              'playerName': 'Atleta gerado $index',
              'position': 'D',
              'playerOrigin': 'FICTIONAL_GENERATED',
              'identityOrigin': 'Generated',
              'source': 'Teste determinístico',
              'strength': 10,
            },
          ],
        },
      },
  ],
  'errors': [if (blocking) 'A divisão possui um clube inelegível.'],
  'warnings': ['Ranking provisório de baixa confiança.'],
  'dataAsOf': '2026-11-09T12:00:00Z',
};

Widget _host({
  bool confirmation = false,
  bool classic = false,
  bool blocking = false,
  bool longNames = false,
  VoidCallback? refresh,
  VoidCallback? edit,
  ValueChanged<String>? swap,
}) {
  final base = ClassicTheme.classicGreen();
  final palette = classic ? classic98PresentationPalette(base) : base;
  return MaterialApp(
    theme: palette.material(),
    home: Scaffold(
      body: SingleChildScrollView(
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: CreatorWorldReview(
            preview: _preview(blocking: blocking, longNames: longNames),
            worldName: longNames
                ? 'Campeonato Internacional de Futebol com Nome Extenso'
                : 'Campeonato de QA',
            years: const ['2025', '1985'],
            countries: const {'ES': 'Espanha', 'PT': 'Portugal'},
            rosterMode: 'Real com complemento fictício',
            maxHumans: 3,
            synchronized: true,
            confirmation: confirmation,
            onRefresh: refresh ?? () {},
            onEditDivisions: edit ?? () {},
            onSwapClub: swap,
          ),
        ),
      ),
    ),
  );
}

Future<void> _expandDistricts(
  WidgetTester tester, {
  bool confirmation = false,
}) async {
  final panel = find.byKey(
    PageStorageKey('review-${confirmation ? 'confirm' : 'preview'}-division-5'),
  );
  await tester.ensureVisible(panel);
  await tester.tap(panel);
  await tester.pumpAndSettle();
}

void main() {
  testWidgets(
    'Preview reads real preview totals, variants, flags and origins',
    (tester) async {
      tester.view.physicalSize = const Size(1366, 1000);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      await tester.pumpWidget(_host());
      await tester.pumpAndSettle();

      expect(find.text('Campeonato de QA'), findsOneWidget);
      expect(find.text('2025, 1985'), findsOneWidget);
      expect(find.text('Espanha, Portugal'), findsOneWidget);
      expect(find.text('128'), findsOneWidget);
      expect(find.text('256'), findsOneWidget);
      expect(
        find.text('128 jogadores do catálogo • 128 fictícios gerados'),
        findsOneWidget,
      );
      expect(find.text('Clube de catálogo (2025)'), findsOneWidget);
      expect(find.text('Clube de catálogo (1985)'), findsOneWidget);
      expect(find.byType(CreatorCountryFlag), findsNWidgets(8));
      final crests = tester.widgetList<ClubCrest>(find.byType(ClubCrest));
      expect(crests.map((crest) => crest.crestDataUrl), [
        _catalogCrest,
        _catalogCrest,
      ]);
      expect(
        find.descendant(
          of: find.byType(ClubCrest),
          matching: find.byType(Image),
        ),
        findsNWidgets(2),
      );
      expect(find.byKey(const ValueKey('review-club-2-qa-8')), findsNothing);
      for (var division = 1; division <= 4; division++) {
        expect(
          find.descendant(
            of: find.byKey(ValueKey('review-division-count-$division')),
            matching: find.text('8/8'),
          ),
          findsOneWidget,
        );
      }
      expect(
        find.descendant(
          of: find.byKey(const ValueKey('review-division-count-5')),
          matching: find.text('96/96'),
        ),
        findsOneWidget,
      );
    },
  );

  testWidgets('Preview actions delegate without rewriting assignments', (
    tester,
  ) async {
    var refreshCount = 0;
    var editCount = 0;
    await tester.pumpWidget(
      _host(refresh: () => refreshCount++, edit: () => editCount++),
    );
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const ValueKey('review-refresh')));
    await tester.tap(find.byKey(const ValueKey('review-edit-divisions')));
    expect(refreshCount, 1);
    expect(editCount, 1);
    expect(find.text('Clube de catálogo (2025)'), findsOneWidget);
  });

  testWidgets(
    'Districts paginate every selected club and search the whole set',
    (tester) async {
      await tester.pumpWidget(_host());
      await tester.pumpAndSettle();
      await _expandDistricts(tester);
      expect(find.text('Mostrando 1–10 de 96 clubes'), findsOneWidget);
      expect(find.byKey(const ValueKey('review-club-5-qa-32')), findsOneWidget);
      expect(find.byKey(const ValueKey('review-club-5-qa-42')), findsNothing);
      await tester.ensureVisible(
        find.byKey(const ValueKey('review-district-next')),
      );
      await tester.tap(find.byKey(const ValueKey('review-district-next')));
      await tester.pumpAndSettle();
      expect(find.text('Mostrando 11–20 de 96 clubes'), findsOneWidget);
      expect(find.byKey(const ValueKey('review-club-5-qa-42')), findsOneWidget);
      await tester.ensureVisible(
        find.byKey(const ValueKey('review-district-search')),
      );
      await tester.enterText(
        find.byKey(const ValueKey('review-district-search')),
        'Clube teste 127',
      );
      await tester.pumpAndSettle();
      expect(find.text('Mostrando 1–1 de 1 clubes'), findsOneWidget);
      expect(
        find.byKey(const ValueKey('review-club-5-qa-127')),
        findsOneWidget,
      );
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets('District country filter uses selected club countries', (
    tester,
  ) async {
    await tester.pumpWidget(_host());
    await tester.pumpAndSettle();
    await _expandDistricts(tester);
    final country = find.byKey(const ValueKey('review-district-country'));
    await tester.ensureVisible(country);
    await tester.tap(country);
    await tester.pumpAndSettle();
    await tester.tap(find.text('Portugal').last);
    await tester.pumpAndSettle();
    expect(find.text('Mostrando 1–10 de 48 clubes'), findsOneWidget);
    expect(find.byKey(const ValueKey('review-club-5-qa-33')), findsOneWidget);
    expect(find.byKey(const ValueKey('review-club-5-qa-32')), findsNothing);
  });

  testWidgets(
    'Roster consultation keeps generated athlete provenance explicit',
    (tester) async {
      await tester.pumpWidget(_host());
      await tester.pumpAndSettle();
      final roster = find.byTooltip(
        'Consultar elenco de Clube de catálogo (2025)',
      );
      await tester.ensureVisible(roster);
      await tester.tap(roster);
      await tester.pumpAndSettle();
      expect(find.text('ELENCO • Clube de catálogo (2025)'), findsOneWidget);
      expect(find.textContaining('FICTÍCIO GERADO'), findsOneWidget);
      expect(find.textContaining('ELENCO REAL DO CATÁLOGO'), findsOneWidget);
      await tester.tap(find.text('FECHAR'));
      await tester.pumpAndSettle();
      expect(find.byType(AlertDialog), findsNothing);
    },
  );

  testWidgets('Confirmation delegates swap using complete variant key', (
    tester,
  ) async {
    String? swapped;
    await tester.pumpWidget(
      _host(confirmation: true, swap: (key) => swapped = key),
    );
    await tester.pumpAndSettle();
    expect(
      find.byKey(const ValueKey('review-confirmation-hero')),
      findsOneWidget,
    );
    final swap = find.byKey(const ValueKey('review-swap-qa-1'));
    await tester.ensureVisible(swap);
    await tester.tap(swap);
    expect(swapped, 'qa-1');
    expect(find.text('Treinadores humanos permitidos: 3'), findsOneWidget);
    expect(find.text('Modo de jogo: Clássico sincronizado'), findsOneWidget);
    expect(find.textContaining('Data inicial:'), findsNothing);
    expect(find.byKey(const ValueKey('review-club-2-qa-8')), findsNothing);
  });

  testWidgets('Blocking errors are visible and warnings retain their message', (
    tester,
  ) async {
    await tester.pumpWidget(_host(confirmation: true, blocking: true));
    await tester.pumpAndSettle();
    expect(
      find.text('Bloqueante: A divisão possui um clube inelegível.'),
      findsOneWidget,
    );
    expect(
      find.text('Atenção: Ranking provisório de baixa confiança.'),
      findsOneWidget,
    );
    expect(find.text('1 bloqueante(s) • 1 aviso(s)'), findsOneWidget);
  });

  for (final classic in [false, true]) {
    for (final confirmation in [false, true]) {
      testWidgets(
        'Responsive review with long names (classic=$classic, confirmation=$confirmation)',
        (tester) async {
          tester.view.devicePixelRatio = 1;
          addTearDown(tester.view.resetPhysicalSize);
          addTearDown(tester.view.resetDevicePixelRatio);
          for (final width in [360.0, 390.0, 430.0, 768.0, 1366.0, 1920.0]) {
            tester.view.physicalSize = Size(width, 1000);
            await tester.pumpWidget(
              _host(
                classic: classic,
                confirmation: confirmation,
                longNames: true,
                swap: (_) {},
              ),
            );
            await tester.pumpAndSettle();
            expect(tester.takeException(), isNull, reason: 'viewport $width');
            final review = tester.getRect(find.byType(CreatorWorldReview));
            expect(review.left, greaterThanOrEqualTo(0));
            expect(review.right, lessThanOrEqualTo(width));
            final clubs = tester.getRect(
              find.byKey(const ValueKey('review-summary-clubs')),
            );
            expect(clubs.right, lessThanOrEqualTo(width));
            expect(
              find.textContaining('Associação Desportiva de Futebol'),
              findsNWidgets(8),
            );
          }
        },
      );
    }
  }
}
