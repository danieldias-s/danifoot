import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:elifoot_flutter/main.dart';
import 'package:elifoot_flutter/presentation_version.dart';
import 'package:elifoot_flutter/presentation_widgets.dart';
import 'classic_ux_014b_test.dart' show join;
import 'presentation_017a_test.dart' show GameplayPresentationApi;
import 'player_rankings_widget_test.dart' as ranks;

class FinanceCalendarApi extends GameplayPresentationApi {
  @override
  Future<Map<String, dynamic>> getClubFinances(
    String world,
    String club,
  ) async => {
    ...await super.getClubFinances(world, club),
    'credit': {
      'activeLoans': 0,
      'loanLimit': 2,
      'availableLoans': 2,
      'outstandingDebt': 0,
      'interestPerMatch': 0,
    },
    'annualSponsorshipBudget': 72000,
    'sponsorshipInstallments': 21,
    'salaryRequests': [
      {
        'id': 'ask',
        'playerId': 'p-1',
        'status': 'PENDING',
        'currentSalary': 100,
        'requestedSalary': 200,
      },
    ],
  };
  @override
  Future<List<dynamic>> getCalendar(String world) async => [
    {
      'roundNumber': 1,
      'competition': 'League',
      'fixtures': [
        {
          'homeClubId': 'club-1',
          'awayClubId': 'club-2',
          'home': 'Atlético Verde',
          'away': 'Adversário do meu clube',
          'isFinished': false,
        },
        {
          'homeClubId': 'club-3',
          'awayClubId': 'club-4',
          'home': 'Jogo de terceiros',
          'away': 'Outro clube',
          'isFinished': false,
        },
      ],
    },
    {
      'roundNumber': 2,
      'competition': 'CupRoundOf16',
      'fixtures': [
        {
          'homeClubId': 'club-3',
          'awayClubId': 'club-4',
          'home': 'Terceiros na taça',
          'away': 'Outro clube',
          'isFinished': false,
        },
      ],
    },
  ];
}

void main() {
  for (final width in [390.0, 1366.0]) {
    testWidgets('Posição e assistências incluem todos com um jogo em $width', (
      t,
    ) async {
      final fixture = ranks.Fixture();
      fixture.rows = [
        for (final position in ['G', 'D', 'M', 'A'])
          {
            ...ranks.player(
              'p-$position',
              'Autor $position',
              matches: 1,
              position: position,
            ),
            'assists': 7,
          },
      ];
      await ranks.openRankings(t, fixture, width: width);
      expect(find.text(width < 700 ? 'POS' : 'POSIÇÃO'), findsOneWidget);
      await ranks.select(t, 'ranking-category', 'Assistências');
      for (final position in ['G', 'D', 'M', 'A']) {
        expect(find.text('Autor $position'), findsOneWidget);
      }
      expect(find.textContaining('Mínimo de 20'), findsNothing);
      expect(find.textContaining('ASSISTÊNCIAS'), findsWidgets);
      expect(t.takeException(), isNull);
    });

    testWidgets('Banco separado de contratos e estádio compacto em $width', (
      t,
    ) async {
      t.view.physicalSize = Size(width, 1000);
      t.view.devicePixelRatio = 1;
      addTearDown(t.view.resetPhysicalSize);
      addTearDown(t.view.resetDevicePixelRatio);
      final api = FinanceCalendarApi();
      await join(t, api);
      await PresentationScope.maybeOf(
        t.element(find.byType(ElifootShellScreen)),
      )!.select(api, PresentationVersion.classic);
      await t.pumpAndSettle();
      await t.tap(find.byKey(const ValueKey('retro-role-p-1')));
      await t.pumpAndSettle();
      await t.tap(find.text('Banco').last);
      await t.pumpAndSettle();
      expect(find.byKey(const Key('classic-bank-screen')), findsOneWidget);
      expect(find.textContaining('PEDIDO SALARIAL'), findsNothing);
      expect(find.textContaining('Carlos Muralha'), findsNothing);
      expect(find.textContaining('Patrocínio anual:'), findsOneWidget);
      await t.tap(find.text('Estádio').last);
      await t.pumpAndSettle();
      final stadium = find.byKey(const Key('classic-stadium-visual'));
      final details = find.byKey(const Key('classic-stadium-details'));
      if (width >= 900) {
        expect(
          t.getTopLeft(details).dx,
          greaterThan(t.getTopRight(stadium).dx),
        );
        expect(
          t
              .getBottomRight(find.byKey(const ValueKey('btn-ampliar-estadio')))
              .dy,
          lessThan(950),
        );
      }
      final renovation = t.widget<ElevatedButton>(
        find.byKey(const ValueKey('btn-reformar-estadio')),
      );
      final expansion = t.widget<ElevatedButton>(
        find.byKey(const ValueKey('btn-ampliar-estadio')),
      );
      expect(
        renovation.style!.backgroundColor!.resolve({}),
        isNot(expansion.style!.backgroundColor!.resolve({})),
      );
      expect(t.takeException(), isNull);
      await t.pumpWidget(const SizedBox());
      api.disconnectRealtime();
    });
  }

  testWidgets(
    'Calendário clássico mostra só o clube e menu mantém assistências',
    (t) async {
      t.view.physicalSize = const Size(1366, 1000);
      t.view.devicePixelRatio = 1;
      addTearDown(t.view.resetPhysicalSize);
      addTearDown(t.view.resetDevicePixelRatio);
      final api = FinanceCalendarApi();
      await join(t, api);
      await PresentationScope.maybeOf(
        t.element(find.byType(ElifootShellScreen)),
      )!.select(api, PresentationVersion.classic);
      await t.pumpAndSettle();
      await t.tap(find.text('Equipa'));
      await t.pumpAndSettle();
      expect(find.text('Jogadores...'), findsNothing);
      expect(find.text('Finanças...'), findsOneWidget);
      expect(find.text('História do clube...'), findsOneWidget);
      await t.tapAt(const Offset(1100, 800));
      await t.pumpAndSettle();
      await t.tap(find.text('Campeonato'));
      await t.pumpAndSettle();
      expect(find.text('Tabela de jogos...'), findsNothing);
      expect(find.text('Defensores...'), findsNothing);
      expect(find.text('Jogos ao vivo...'), findsNothing);
      expect(find.text('Assistências...'), findsOneWidget);
      await t.tap(find.text('Calendário...'));
      await t.pumpAndSettle();
      expect(find.textContaining('Adversário do meu clube'), findsOneWidget);
      expect(find.textContaining('Jogo de terceiros'), findsNothing);
      expect(find.textContaining('Terceiros na taça'), findsNothing);
      expect(t.takeException(), isNull);
      await t.pumpWidget(const SizedBox());
      api.disconnectRealtime();
    },
  );
}
