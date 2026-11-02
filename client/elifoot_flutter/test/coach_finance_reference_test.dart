import 'package:elifoot_flutter/coach_finance_screens.dart';
import 'package:elifoot_flutter/club_theme.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  final rows = List.generate(
    18,
    (i) => <String, dynamic>{
      'rank': i + 1,
      'name': 'Treinador ${i + 1}',
      'isHuman': i == 1 || i == 12,
      'currentClubName': i.isEven ? 'Flamengo RJ' : 'FC Porto',
      'leagueTitles': i % 3,
      'cupTitles': i % 2,
      'coachPoints': i * 7,
      'totalWins': 3,
      'totalDraws': 2,
      'totalLosses': 1,
    },
  );
  Widget app(Widget child) => MaterialApp(
    theme: ClassicTheme.derive().material(),
    home: Scaffold(body: child),
  );
  for (final width in [320.0, 360.0, 390.0, 430.0, 768.0, 1366.0, 1920.0]) {
    testWidgets('Coach ranking preview, complete list and career at $width', (
      tester,
    ) async {
      await tester.binding.setSurfaceSize(Size(width, 1000));
      addTearDown(() => tester.binding.setSurfaceSize(null));
      String? selected;
      await tester.pumpWidget(
        app(
          ClassicCoachScreen(
            ranking: rows,
            career: const Text('Histórico de carreira preservado'),
            onCoach: (r) => selected = r['name'] as String,
          ),
        ),
      );
      await tester.pumpAndSettle();
      expect(tester.takeException(), isNull);
      if (width < 784) {
        expect(find.text('Treinador 18'), findsNothing);
        await tester.ensureVisible(
          find.byKey(const Key('coach-ranking-complete')),
        );
        await tester.tap(find.byKey(const Key('coach-ranking-complete')));
        await tester.pumpAndSettle();
      }
      expect(find.text('Treinador 18'), findsOneWidget);
      await tester.ensureVisible(find.text('Treinador 2'));
      await tester.tap(find.text('Treinador 2'));
      expect(selected, 'Treinador 2');
      await tester.ensureVisible(find.byKey(const Key('coach-tab-career')));
      await tester.tap(find.byKey(const Key('coach-tab-career')));
      await tester.pumpAndSettle();
      expect(find.text('Histórico de carreira preservado'), findsOneWidget);
      expect(tester.takeException(), isNull);
    });
    testWidgets(
      'Finance paper panels, round totals and stadium actions at $width',
      (tester) async {
        await tester.binding.setSurfaceSize(Size(width, 1000));
        addTearDown(() => tester.binding.setSurfaceSize(null));
        var acted = false;
        await tester.pumpWidget(
          app(
            ClassicFinanceView(
              club: {
                'cashBalance': 3550000,
                'stadium': {
                  'stadiumName': 'Estádio do Flamengo RJ',
                  'builtCapacity': 46354,
                  'usableCapacity': 42718,
                  'conditionPercent': 87,
                  'lastAttendance': 7288,
                },
              },
              data: {
                'ledger': [
                  {
                    'seasonNumber': 1,
                    'roundNumber': 2,
                    'description': 'Recebimento na rodada anterior',
                    'amount': 900000,
                  },
                  {
                    'seasonNumber': 1,
                    'roundNumber': 3,
                    'description': 'Compra de jogador',
                    'amount': -650000,
                  },
                  {
                    'seasonNumber': 1,
                    'roundNumber': 3,
                    'description': 'Receita de bilheteria',
                    'amount': 75000,
                  },
                ],
              },
              season: 1,
              round: 3,
              actions: [
                ElevatedButton(
                  onPressed: () => acted = true,
                  child: const Text('REFORMAR ESTÁDIO (R\$ 142.718)'),
                ),
                OutlinedButton(
                  onPressed: () {},
                  child: const Text(
                    'EMPRÉSTIMO (+R\$ 500.000; JUROS R\$ 25.000/JOGO)',
                  ),
                ),
              ],
              obligations: const Text('CRÉDITO INDIVIDUAL: 0/4 empréstimos'),
            ),
          ),
        );
        await tester.pumpAndSettle();
        expect(tester.takeException(), isNull);
        final income = find.byKey(const Key('finance-metric-income'));
        expect(
          find.descendant(of: income, matching: find.text('R\$ 75.000')),
          findsOneWidget,
        );
        expect(find.byKey(const Key('finance-metric-cash')), findsOneWidget);
        await tester.ensureVisible(find.text('REFORMAR ESTÁDIO (R\$ 142.718)'));
        await tester.tap(find.text('REFORMAR ESTÁDIO (R\$ 142.718)'));
        expect(acted, isTrue);
        await tester.ensureVisible(find.text('Compra de jogador'));
        expect(find.text('-R\$ 650.000'), findsWidgets);
        expect(tester.takeException(), isNull);
      },
    );
  }
  test(
    'Round summary excludes earlier rounds and seasons without altering the ledger',
    () {
      final ledger = [
        {
          'seasonNumber': 1,
          'roundNumber': 3,
          'amount': 99000,
          'description': 'Bilheteria antiga',
        },
        {
          'seasonNumber': 2,
          'roundNumber': 2,
          'amount': 88000,
          'description': 'Receita anterior',
        },
        {
          'seasonNumber': 2,
          'roundNumber': 3,
          'amount': 10000,
          'description': 'Bilheteria',
        },
        {
          'seasonNumber': 2,
          'roundNumber': 3,
          'amount': -1200,
          'description': 'Folha salarial',
        },
        {
          'seasonNumber': 2,
          'roundNumber': 3,
          'amount': -25000,
          'description': 'Juros bancários',
        },
      ];
      final result = financeSummaryForRound(ledger, 2, 3);
      expect(result['income'], 10000);
      expect(result['expenses'], 26200);
      expect(result['tickets'], 10000);
      expect(result['wages'], -1200);
      expect(ledger.length, 5);
    },
  );
}
