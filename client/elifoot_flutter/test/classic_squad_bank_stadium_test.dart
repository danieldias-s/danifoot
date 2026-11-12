import 'dart:async';
import 'dart:ui' as ui;
import 'package:flutter/material.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:elifoot_flutter/main.dart';
import 'package:elifoot_flutter/classic98_widgets.dart';
import 'package:elifoot_flutter/stadium_visual.dart';
import 'package:elifoot_flutter/presentation_version.dart';
import 'package:elifoot_flutter/presentation_widgets.dart';
import 'presentation_017a_test.dart' show GameplayPresentationApi;
import 'classic_ux_014b_test.dart' show join;

class SeasonContractRosterApi extends GameplayPresentationApi {
  bool renewed = true;

  @override
  Future<Map<String, dynamic>> getClubDetail(
    String world,
    String clubId,
  ) async {
    final club = await super.getClubDetail(world, clubId);
    for (final p in club['squad'] as List) {
      p['contractRenewedForSeason'] =
          renewed && (p['id'] == 'p-1' || p['id'] == 'p-12');
    }
    return club;
  }
}

class FullBenchApi extends GameplayPresentationApi {
  @override
  Future<Map<String, dynamic>> getClubDetail(
    String worldId,
    String clubId,
  ) async {
    final club = await super.getClubDetail(worldId, clubId);
    final squad = club['squad'] as List;
    for (var i = 19; i <= 23; i++) {
      squad.add({
        ...Map<String, dynamic>.from(squad[12] as Map),
        'id': 'p-$i',
        'displayName': 'Reserva extra $i',
      });
    }
    club['preferredBenchIds'] = [
      for (var i = 12; i <= 16; i++) 'p-$i',
      for (var i = 19; i <= 22; i++) 'p-$i',
    ];
    return club;
  }
}

class ClassicPlayerStatsPanelApi extends GameplayPresentationApi {
  String currentDivisionTier = 'QuartaDivisao';
  String currentDivisionDisplay = '4ª Divisão';
  final List<String> requestedPlayerHistoryIds = [];
  final Map<String, Completer<Map<String, dynamic>>> pendingHistory = {};

  @override
  Future<Map<String, dynamic>> getClubDetail(
    String worldId,
    String clubId,
  ) async {
    final club = await super.getClubDetail(worldId, clubId);
    club['divisionTier'] = currentDivisionTier;
    club['divisionDisplay'] = currentDivisionDisplay;
    final squad = (club['squad'] as List)
        .map((e) => Map<String, dynamic>.from(e as Map))
        .toList();

    // p-1: Starter craque (star player) with complete career, division, red card and injury occurrence data
    squad[0] = {
      ...squad[0],
      'id': 'p-1',
      'name': 'Atleta sintético 1985-23-7',
      'displayName': 'Atleta sintético 1985-23-7*',
      'isStar': true,
      'careerMatches': 128,
      'seasonGoals': 3,
      'cupGoals': 1,
      'seasonAssists': 5,
      'currentDivisionTier': currentDivisionTier,
      'currentDivisionDisplay': currentDivisionDisplay,
      'currentDivisionGoals': currentDivisionTier == 'QuartaDivisao' ? 37 : 15,
      'currentDivisionAssists': currentDivisionTier == 'QuartaDivisao' ? 18 : 8,
      'divisionGoals': {
        'PrimeiraDivisao': 0,
        'SegundaDivisao': 0,
        'TerceiraDivisao': 15,
        'QuartaDivisao': 37,
      },
      'divisionAssists': {
        'PrimeiraDivisao': 0,
        'SegundaDivisao': 0,
        'TerceiraDivisao': 8,
        'QuartaDivisao': 18,
      },
      'careerGoals': 56,
      'careerAssists': 23,
      'careerRedCards': 3,
      'careerInjuries': 2,
      'injuryRoundsRemaining': 0,
      'injuryDaysRemaining': 0,
    };

    // p-2: Starter non-star with identical name to p-3 to verify ID-based lookup
    squad[1] = {
      ...squad[1],
      'id': 'p-2',
      'name': 'Homônimo Clássico',
      'displayName': 'Homônimo Clássico',
      'isStar': false,
      'careerMatches': 40,
      'seasonGoals': 2,
      'cupGoals': 0,
      'seasonAssists': 1,
      'currentDivisionTier': currentDivisionTier,
      'currentDivisionDisplay': currentDivisionDisplay,
      'currentDivisionGoals': 10,
      'currentDivisionAssists': 4,
      'divisionGoals': {'QuartaDivisao': 10, 'TerceiraDivisao': 0},
      'divisionAssists': {'QuartaDivisao': 4, 'TerceiraDivisao': 0},
      'careerGoals': 10,
      'careerAssists': 4,
      'careerRedCards': 1,
      'careerInjuries': 0,
    };

    // p-3: Starter non-star with same name as p-2 but different ID and stats
    squad[2] = {
      ...squad[2],
      'id': 'p-3',
      'name': 'Homônimo Clássico',
      'displayName': 'Homônimo Clássico',
      'isStar': false,
      'careerMatches': 95,
      'seasonGoals': 9,
      'cupGoals': 2,
      'seasonAssists': 7,
      'currentDivisionTier': currentDivisionTier,
      'currentDivisionDisplay': currentDivisionDisplay,
      'currentDivisionGoals': 28,
      'currentDivisionAssists': 14,
      'divisionGoals': {'QuartaDivisao': 28, 'TerceiraDivisao': 6},
      'divisionAssists': {'QuartaDivisao': 14, 'TerceiraDivisao': 3},
      'careerGoals': 36,
      'careerAssists': 17,
      'careerRedCards': 4,
      'careerInjuries': 3,
      'injuryRoundsRemaining': 0,
      'injuryDaysRemaining': 0,
    };
    squad[17] = {
      ...squad[17],
      'id': 'p-18',
      'careerInjuries': 1,
      'injuryRoundsRemaining': 9,
      'injuryDaysRemaining': 21,
    };

    // p-12: Reserve craque with very long name
    squad[11] = {
      ...squad[11],
      'id': 'p-12',
      'name': 'Maximiliano Albuquerque de Bragança Neto 1985-99',
      'displayName': '★ Maximiliano Albuquerque de Bragança Neto 1985-99*',
      'isStar': true,
      'careerMatches': 12,
      'seasonGoals': 1,
      'cupGoals': 0,
      'seasonAssists': 2,
      'careerGoals': 1,
      'careerAssists': 2,
      'careerRedCards': 0,
      'careerInjuries': 1,
    };

    // p-17: Unrelated (out of bench) craque
    squad[16] = {
      ...squad[16],
      'id': 'p-17',
      'name': 'Fora Relacionados Craque 17',
      'displayName': 'Fora Relacionados Craque 17*',
      'isStar': true,
      'careerMatches': 5,
      'seasonGoals': 0,
      'cupGoals': 0,
      'seasonAssists': 0,
      'careerGoals': 0,
      'careerAssists': 0,
      'careerRedCards': 0,
      'careerInjuries': 0,
    };

    club['squad'] = squad;
    return club;
  }

  @override
  Future<Map<String, dynamic>> getPlayerHistory(
    String worldId,
    String playerId,
  ) async {
    requestedPlayerHistoryIds.add(playerId);
    if (pendingHistory.containsKey(playerId)) {
      return pendingHistory[playerId]!.future;
    }
    if (playerId == 'p-1') {
      return {
        'playerId': 'p-1',
        'currentSeason': 1,
        'currentDivisionTier': currentDivisionTier,
        'currentDivisionDisplay': currentDivisionDisplay,
        'careerMatches': 128,
        'seasonGoals': 4,
        'seasonAssists': 5,
        'currentDivisionGoals': currentDivisionTier == 'QuartaDivisao'
            ? 37
            : 15,
        'currentDivisionAssists': currentDivisionTier == 'QuartaDivisao'
            ? 18
            : 8,
        'divisionGoals': {
          'PrimeiraDivisao': 0,
          'SegundaDivisao': 0,
          'TerceiraDivisao': 15,
          'QuartaDivisao': 37,
        },
        'divisionAssists': {
          'PrimeiraDivisao': 0,
          'SegundaDivisao': 0,
          'TerceiraDivisao': 8,
          'QuartaDivisao': 18,
        },
        'careerGoals': 56,
        'careerAssists': 23,
        'careerRedCards': 3,
        'careerInjuries': 2,
        'rows': const [],
      };
    }
    if (playerId == 'p-2') {
      return {
        'playerId': 'p-2',
        'currentSeason': 1,
        'currentDivisionTier': currentDivisionTier,
        'currentDivisionDisplay': currentDivisionDisplay,
        'careerMatches': 40,
        'seasonGoals': 2,
        'seasonAssists': 1,
        'currentDivisionGoals': 10,
        'currentDivisionAssists': 4,
        'divisionGoals': {'QuartaDivisao': 10, 'TerceiraDivisao': 0},
        'divisionAssists': {'QuartaDivisao': 4, 'TerceiraDivisao': 0},
        'careerGoals': 10,
        'careerAssists': 4,
        'careerRedCards': 1,
        'careerInjuries': 0,
        'rows': const [],
      };
    }
    if (playerId == 'p-3') {
      return {
        'playerId': 'p-3',
        'currentSeason': 1,
        'currentDivisionTier': currentDivisionTier,
        'currentDivisionDisplay': currentDivisionDisplay,
        'careerMatches': 95,
        'seasonGoals': 11,
        'seasonAssists': 7,
        'currentDivisionGoals': 28,
        'currentDivisionAssists': 14,
        'divisionGoals': {'QuartaDivisao': 28, 'TerceiraDivisao': 6},
        'divisionAssists': {'QuartaDivisao': 14, 'TerceiraDivisao': 3},
        'careerGoals': 36,
        'careerAssists': 17,
        'careerRedCards': 4,
        'careerInjuries': 3,
        'rows': const [],
      };
    }
    return {'playerId': playerId, 'currentSeason': 1, 'rows': const []};
  }
}

class ClassicBankScreenApi extends GameplayPresentationApi {
  bool loanPaid = false;
  final List<String> repaidLoanIds = [];

  @override
  Future<Map<String, dynamic>> getClubFinances(
    String worldId,
    String clubId,
  ) async => {
    ...await super.getClubFinances(worldId, clubId),
    'availableCash': loanPaid ? 1100000 : 1600000,
    'annualSponsorshipBudget': 72000,
    'sponsorshipInstallments': 21,
    'credit': {
      'activeLoans': loanPaid ? 0 : 1,
      'loanLimit': 2,
      'availableLoans': loanPaid ? 2 : 1,
      'outstandingDebt': loanPaid ? 0 : 500000,
      'interestPerMatch': loanPaid ? 0 : 25000,
      'negativeCashPrincipal': 0,
      'negativeCashEstimatePerGlobalRound': 0,
    },
    'loans': [
      {
        'id': 'loan-classic-1',
        'mode': 'ClassicFixed',
        'principalAmount': 500000,
        'remainingBalance': 500000,
        'outstandingPrincipal': 500000,
        'interestPerMatch': 25000,
        'interestPaid': 0,
        'unpaidInterest': 0,
        'isPaidOff': loanPaid,
      },
    ],
  };

  @override
  Future<void> repayBankLoan(
    String worldId,
    String clubId,
    String coachId,
    String loanId,
  ) async {
    repaidLoanIds.add(loanId);
    loanPaid = true;
  }
}

void main() {
  for (final width in [390.0, 768.0, 1024.0, 1366.0]) {
    testWidgets('Contrato depois do salário para titular e reserva em $width', (
      t,
    ) async {
      t.view.physicalSize = Size(width, 1000);
      t.view.devicePixelRatio = 1;
      addTearDown(t.view.resetPhysicalSize);
      addTearDown(t.view.resetDevicePixelRatio);
      final api = SeasonContractRosterApi();
      await join(t, api);
      await PresentationScope.maybeOf(
        t.element(find.byType(ElifootShellScreen)),
      )!.select(api, PresentationVersion.classic);
      await t.pumpAndSettle();
      RetroTable roster() =>
          t.widget<RetroTable>(find.byKey(const Key('retro-roster')));
      expect(roster().headers.sublist(4), ['SALÁRIO', 'CONTRATO']);
      expect(roster().sectionHeaders.values.first.sublist(4), [
        'SALÁRIO',
        'CONTRATO',
      ]);
      expect(roster().rows.every((r) => r.length == 6), isTrue);
      expect(roster().rows.where((r) => r[5] == 'RENOVADO').length, 2);
      expect(
        roster().rows.firstWhere((r) => r[1].startsWith('Carlos Muralha'))[5],
        'RENOVADO',
      );
      expect(
        roster().rows.firstWhere((r) => r[1] == 'Atleta 12')[5],
        'RENOVADO',
      );
      expect(roster().rows.firstWhere((r) => r[1] == 'Atleta 2')[5], '—');
      if (width >= 768) {
        expect(
          find.text('CONTRATO').first.hitTestable(),
          findsOneWidget,
          reason:
              'Desktop and tablet show the contract column without scrolling',
        );
        expect(
          t.getSize(find.byKey(const Key('retro-roster'))).width,
          lessThanOrEqualTo(width == 1024 ? 610 : width),
        );
      }
      await t.ensureVisible(find.text('CONTRATO').first);
      await t.pumpAndSettle();
      expect(
        t.getCenter(find.text('CONTRATO').first).dx,
        greaterThan(t.getCenter(find.text('SALÁRIO').first).dx),
      );
      expect(find.text('CONTRATO').first.hitTestable(), findsOneWidget);
      // A fresh server response clears the mark; salary alone never means renewal.
      api.renewed = false;
      api.onNotification?.call('Contrato atualizado');
      await t.pumpAndSettle();
      expect(roster().rows.every((r) => r[5] == '—'), isTrue);
      expect(t.takeException(), isNull);
      await t.pumpWidget(const SizedBox());
      api.disconnectRealtime();
    });
  }

  for (final width in [1024.0, 1366.0]) {
    testWidgets('Jogo entre menus e dropdown abaixo do botão em $width', (
      t,
    ) async {
      t.view.physicalSize = Size(width, 1000);
      t.view.devicePixelRatio = 1;
      addTearDown(t.view.resetPhysicalSize);
      addTearDown(t.view.resetDevicePixelRatio);
      final api = GameplayPresentationApi();
      await join(t, api);
      final controller = PresentationScope.maybeOf(
        t.element(find.byType(ElifootShellScreen)),
      )!;
      await controller.select(api, PresentationVersion.classic);
      await t.pumpAndSettle();
      final game = find.text('Jogo').first;
      final tactics = find.text('Táticas').first;
      expect(
        t.getCenter(game).dx,
        greaterThan(t.getCenter(find.text('Configurações')).dx),
      );
      expect(t.getCenter(game).dx, lessThan(t.getCenter(tactics).dx));
      await t.tap(find.text('Banco'));
      await t.pumpAndSettle();
      expect(find.byKey(const Key('classic-bank-screen')), findsOneWidget);
      await t.tap(game);
      await t.pumpAndSettle();
      expect(find.byKey(const Key('retro-roster')), findsOneWidget);
      expect(find.byKey(const Key('classic-bank-screen')), findsNothing);
      final menuBottom = t.getBottomLeft(tactics).dy;
      await t.tap(tactics);
      await t.pumpAndSettle();
      final menuItems = find.byType(PopupMenuItem<int>);
      expect(menuItems, findsWidgets);
      expect(
        t.getTopLeft(menuItems.first).dy,
        greaterThanOrEqualTo(menuBottom),
      );
      expect(find.text('Automático (A)'), findsOneWidget);
      expect(find.text('Melhores (M)'), findsOneWidget);
      await t.tapAt(const Offset(0, 0));
      await t.pumpAndSettle();
      expect(t.takeException(), isNull);
      await t.pumpWidget(const SizedBox());
      api.disconnectRealtime();
    });
  }

  testWidgets(
    'Tela clássica Banco — Empréstimos renderiza Crédito do clube, Resumo financeiro, Empréstimo ativo e QUITAR no desktop e mobile',
    (t) async {
      for (final width in [1366.0, 390.0]) {
        t.view.physicalSize = Size(width, 1000);
        t.view.devicePixelRatio = 1;
        addTearDown(t.view.resetPhysicalSize);
        addTearDown(t.view.resetDevicePixelRatio);

        final api = ClassicBankScreenApi();
        await join(t, api);
        final controller = PresentationScope.maybeOf(
          t.element(find.byType(ElifootShellScreen)),
        )!;
        await controller.select(api, PresentationVersion.classic);
        await t.pumpAndSettle();

        if (width < 1024) {
          await t.tap(find.text('Banco').last);
        } else {
          await t.tap(find.text('Banco'));
        }
        await t.pumpAndSettle();

        expect(find.byKey(const Key('classic-bank-screen')), findsOneWidget);
        expect(find.text('BANCO — EMPRÉSTIMOS'), findsOneWidget);
        expect(
          find.byKey(const Key('classic-bank-credit-section')),
          findsOneWidget,
        );
        expect(find.text('Caixa: R\$ 1.600.000'), findsOneWidget);
        expect(
          find.text(
            'Consulte as condições e os empréstimos em aberto antes de contratar.',
          ),
          findsOneWidget,
        );
        expect(
          find.byKey(const ValueKey('btn-emprestimo-bancario')),
          findsOneWidget,
        );
        expect(
          find.text('EMPRÉSTIMO (+R\$ 500.000; JUROS R\$ 25.000/JOGO)'),
          findsOneWidget,
        );

        // Resumo financeiro do clube (7 linhas)
        expect(
          find.byKey(const Key('classic-bank-summary-section')),
          findsOneWidget,
        );
        for (var i = 0; i < 7; i++) {
          expect(
            find.byKey(ValueKey('classic-bank-summary-row-$i')),
            findsOneWidget,
          );
        }
        expect(find.text('Patrocínio anual:'), findsOneWidget);
        expect(find.text('R\$ 72.000 em 21 parcelas'), findsOneWidget);
        expect(find.text('Folha salarial:'), findsOneWidget);
        expect(find.text('por partida oficial disputada'), findsOneWidget);
        expect(find.text('Saldo disponível:'), findsOneWidget);
        expect(find.text('Crédito individual:'), findsOneWidget);
        expect(find.text('1/2 empréstimos; disponíveis: 1'), findsOneWidget);
        expect(find.text('Dívida bancária:'), findsOneWidget);
        expect(find.text('Juros clássicos por partida:'), findsOneWidget);
        expect(find.text('Caixa negativo:'), findsOneWidget);
        expect(
          find.textContaining(
            'Juros: 2% por rodada global concluída; estimativa:',
          ),
          findsOneWidget,
        );

        // Empréstimo ativo + QUITAR
        expect(
          find.byKey(const Key('classic-bank-active-loan-section')),
          findsOneWidget,
        );
        expect(
          find.byKey(const ValueKey('classic-active-loan-loan-classic-1')),
          findsOneWidget,
        );
        expect(find.textContaining('Empréstimo clássico'), findsOneWidget);
        expect(find.text('Principal contratado:'), findsOneWidget);
        expect(find.text('Principal pendente:'), findsOneWidget);
        expect(find.text('Juros:'), findsOneWidget);
        expect(find.text('R\$ 25.000 por partida'), findsOneWidget);
        expect(find.text('Juros pagos:'), findsOneWidget);
        expect(find.text('Juros pendentes:'), findsOneWidget);

        final quitarFinder = find.byKey(
          const ValueKey('btn-quitar-emprestimo-loan-classic-1'),
        );
        expect(quitarFinder, findsOneWidget);
        await t.ensureVisible(quitarFinder);
        await t.tap(quitarFinder);
        await t.pumpAndSettle();

        expect(api.repaidLoanIds, equals(['loan-classic-1']));
        expect(
          find.byKey(const Key('classic-bank-no-active-loan')),
          findsOneWidget,
        );
        expect(find.text('Nenhum empréstimo ativo em aberto.'), findsOneWidget);

        // Abre Estádio e valida layout web (2 colunas) e mobile (empilhado)
        if (width < 1024) {
          await t.tap(find.text('Estádio').last);
        } else {
          await t.tap(find.text('Estádio'));
        }
        await t.pumpAndSettle();

        expect(find.byKey(const Key('classic-stadium-screen')), findsOneWidget);
        expect(find.byKey(const Key('classic-stadium-header')), findsOneWidget);
        expect(find.text('ESTÁDIO'), findsOneWidget);
        final visualFinder = find.byKey(const Key('classic-stadium-visual'));
        final summaryFinder = find.byKey(
          const Key('classic-stadium-summary-card'),
        );
        expect(visualFinder, findsOneWidget);
        expect(summaryFinder, findsOneWidget);
        expect(find.textContaining('Visão do estádio •'), findsOneWidget);
        expect(
          find.text(
            'Arquibancadas acompanham a capacidade construída. Setores escuros indicam lugares não utilizáveis.',
          ),
          findsOneWidget,
        );
        expect(find.text('Capacidade construída:'), findsOneWidget);
        expect(find.text('Capacidade utilizável:'), findsOneWidget);
        expect(find.text('Condição estrutural:'), findsOneWidget);
        expect(find.text('Último público:'), findsOneWidget);
        expect(find.text('Ingresso:'), findsOneWidget);
        expect(find.text('Manutenção por jogo:'), findsOneWidget);
        expect(find.text('Caixa:'), findsOneWidget);
        expect(
          find.byKey(const ValueKey('btn-reformar-estadio')),
          findsOneWidget,
        );
        expect(
          find.byKey(const ValueKey('btn-ampliar-estadio')),
          findsOneWidget,
        );
        expect(
          find.byKey(const Key('classic-stadium-info-note')),
          findsOneWidget,
        );
        if (width >= 720) {
          expect(
            t.getTopLeft(summaryFinder).dx,
            greaterThan(t.getTopRight(visualFinder).dx),
          );
        } else {
          expect(
            t.getTopLeft(summaryFinder).dy,
            greaterThan(t.getBottomLeft(visualFinder).dy),
          );
        }
        expect(t.takeException(), isNull);

        await t.pumpWidget(const SizedBox());
        api.disconnectRealtime();
      }
    },
  );

  testWidgets(
    'Estrela de craque após o nome e painel Dados do Jogador com 9 estatísticas na ordem oficial (Web e Mobile)',
    (t) async {
      for (final width in [1366.0, 390.0]) {
        t.view.physicalSize = Size(width, 1000);
        t.view.devicePixelRatio = 1;
        addTearDown(t.view.resetPhysicalSize);
        addTearDown(t.view.resetDevicePixelRatio);

        final api = ClassicPlayerStatsPanelApi();
        await join(t, api);
        final controller = PresentationScope.maybeOf(
          t.element(find.byType(ElifootShellScreen)),
        )!;
        await controller.select(api, PresentationVersion.classic);
        await t.pumpAndSettle();

        // 1. Dados do Jogador above Próximo Adversário
        final playerPanelTop = t
            .getTopLeft(find.byKey(const Key('retro-player-data-panel')))
            .dy;
        final nextOpponentTop = t
            .getTopLeft(find.byKey(const Key('retro-next-opponent-panel')))
            .dy;
        expect(playerPanelTop, lessThan(nextOpponentTop));

        // 2. Unselected state shows exact message
        expect(
          find.byKey(const Key('retro-player-data-empty')),
          findsOneWidget,
        );
        expect(
          find.text('Selecione um jogador para ver seus dados.'),
          findsOneWidget,
        );

        // 3. Star ★ appears AFTER name in starters, bench, and unrelated squad players; never before name, never '*'
        final roster = t.widget<RetroTable>(
          find.byKey(const Key('retro-roster')),
        );
        final starterCraqueRow = roster.rows[0][1];
        final commonPlayerRow = roster.rows[1][1];
        final benchCraqueRow = roster.rows.firstWhere(
          (r) => r[1].contains('Maximiliano'),
        )[1];
        final outCraqueRow = roster.rows.firstWhere(
          (r) => r[1].contains('Fora Relacionados'),
        )[1];

        expect(starterCraqueRow, equals('Atleta sintético 1985-23-7 ★'));
        expect(starterCraqueRow.startsWith('★'), isFalse);
        expect(starterCraqueRow.contains('*'), isFalse);
        expect('★'.allMatches(starterCraqueRow).length, equals(1));

        expect(commonPlayerRow, equals('Homônimo Clássico'));
        expect(commonPlayerRow.contains('★'), isFalse);
        expect(commonPlayerRow.contains('*'), isFalse);

        expect(
          benchCraqueRow,
          equals('Maximiliano Albuquerque de Bragança Neto 1985-99 ★'),
        );
        expect(benchCraqueRow.startsWith('★'), isFalse);
        expect(benchCraqueRow.contains('*'), isFalse);
        expect('★'.allMatches(benchCraqueRow).length, equals(1));

        expect(outCraqueRow, equals('Fora Relacionados Craque 17 ★'));
        expect(outCraqueRow.contains('*'), isFalse);

        // Verify softGold (#D9B65B) on ClassicPlayerNameText
        final starNameWidgets = t
            .widgetList<ClassicPlayerNameText>(
              find.byType(ClassicPlayerNameText),
            )
            .toList();
        expect(starNameWidgets, isNotEmpty);
        for (final w in starNameWidgets) {
          expect(w.starColor, equals(const Color(0xFFD9B65B)));
        }

        // 4. Select starter craque (p-1) and verify header + 9 statistics in exact order
        final starterFinder = find.text('Atleta sintético 1985-23-7 ★').first;
        await t.ensureVisible(starterFinder);
        await t.tap(starterFinder);
        await t.pumpAndSettle();

        final headerName = t.widget<ClassicPlayerNameText>(
          find.byKey(const Key('classic-player-data-name')),
        );
        expect(headerName.data, equals('Atleta sintético 1985-23-7 ★'));
        expect(headerName.starColor, equals(const Color(0xFFD9B65B)));

        final expectedStats = [
          ('Jogos (carreira):', '128'),
          ('Gols (temporada):', '4'),
          ('Assistências (temporada):', '5'),
          ('Gols na 4ª Divisão:', '37'),
          ('Assistências na 4ª Divisão:', '18'),
          ('Gols (total):', '56'),
          ('Assistências (total):', '23'),
          ('Cartões vermelhos:', '3'),
          ('Lesões (total):', '2'), // 2 occurrences, NOT 9 missed rounds
        ];
        for (var i = 0; i < expectedStats.length; i++) {
          final label = t.widget<Text>(
            find.byKey(ValueKey('classic-player-stat-label-$i')),
          );
          final value = t.widget<Text>(
            find.byKey(ValueKey('classic-player-stat-value-$i')),
          );
          expect(label.data, equals(expectedStats[i].$1));
          expect(value.data, equals(expectedStats[i].$2));
          expect(value.textAlign, equals(TextAlign.right));
        }

        // 5. Division change preserves division history and returning to previous division restores accumulated totals
        api.currentDivisionTier = 'TerceiraDivisao';
        api.currentDivisionDisplay = '3ª Divisão';
        api.onNotification?.call('Promovido para a 3ª Divisão');
        await t.pumpAndSettle();

        expect(
          t
              .widget<Text>(
                find.byKey(const ValueKey('classic-player-stat-label-3')),
              )
              .data,
          equals('Gols na 3ª Divisão:'),
        );
        expect(
          t
              .widget<Text>(
                find.byKey(const ValueKey('classic-player-stat-value-3')),
              )
              .data,
          equals('15'),
        );
        expect(
          t
              .widget<Text>(
                find.byKey(const ValueKey('classic-player-stat-label-4')),
              )
              .data,
          equals('Assistências na 3ª Divisão:'),
        );
        expect(
          t
              .widget<Text>(
                find.byKey(const ValueKey('classic-player-stat-value-4')),
              )
              .data,
          equals('8'),
        );

        // Return to 4ª Divisão
        api.currentDivisionTier = 'QuartaDivisao';
        api.currentDivisionDisplay = '4ª Divisão';
        api.onNotification?.call('Retorno para a 4ª Divisão');
        await t.pumpAndSettle();

        expect(
          t
              .widget<Text>(
                find.byKey(const ValueKey('classic-player-stat-label-3')),
              )
              .data,
          equals('Gols na 4ª Divisão:'),
        );
        expect(
          t
              .widget<Text>(
                find.byKey(const ValueKey('classic-player-stat-value-3')),
              )
              .data,
          equals('37'),
        );

        // 6. Selecting players with identical names queries by real ID and prevents stale async responses from overwriting
        final slowP2 = Completer<Map<String, dynamic>>();
        api.pendingHistory['p-2'] = slowP2;

        final homonimoRows = find.text('Homônimo Clássico');
        await t.ensureVisible(homonimoRows.first);
        await t.tap(homonimoRows.first); // selects p-2
        await t.pump();

        await t.ensureVisible(homonimoRows.at(1));
        await t.tap(homonimoRows.at(1)); // selects p-3
        await t.pumpAndSettle();

        expect(api.requestedPlayerHistoryIds, containsAll(['p-2', 'p-3']));
        expect(
          t
              .widget<Text>(
                find.byKey(const ValueKey('classic-player-stat-value-0')),
              )
              .data,
          equals('95'),
        );
        expect(
          t
              .widget<Text>(
                find.byKey(const ValueKey('classic-player-stat-value-8')),
              )
              .data,
          equals('3'),
        );

        // Complete stale p-2 response after p-3 is already selected; p-3 data must remain
        slowP2.complete({
          'playerId': 'p-2',
          'currentSeason': 1,
          'careerMatches': 40,
          'seasonGoals': 2,
          'seasonAssists': 1,
          'currentDivisionGoals': 10,
          'currentDivisionAssists': 4,
          'careerGoals': 10,
          'careerAssists': 4,
          'careerRedCards': 1,
          'careerInjuries': 0,
          'rows': const [],
        });
        await t.pumpAndSettle();

        expect(
          t
              .widget<Text>(
                find.byKey(const ValueKey('classic-player-stat-value-0')),
              )
              .data,
          equals('95'),
        );
        expect(t.takeException(), isNull);

        await t.pumpWidget(const SizedBox());
        api.disconnectRealtime();
      }
    },
  );

  testWidgets(
    'Bolinha aceita excesso no rascunho e bloqueia envio até corrigir sem trocar outro atleta',
    (t) async {
      t.view.physicalSize = const Size(1366, 1000);
      t.view.devicePixelRatio = 1;
      addTearDown(t.view.resetPhysicalSize);
      addTearDown(t.view.resetDevicePixelRatio);
      final api = FullBenchApi();
      await join(t, api);
      final controller = PresentationScope.maybeOf(
        t.element(find.byType(ElifootShellScreen)),
      )!;
      await controller.select(api, PresentationVersion.classic);
      await t.pumpAndSettle();
      RetroTable roster() =>
          t.widget<RetroTable>(find.byKey(const Key('retro-roster')));
      bool canSend() =>
          t
              .widget<ElevatedButton>(
                find.byKey(const Key('btn-enviar-tatica')),
              )
              .onPressed !=
          null;
      Future<void> click(String id) async {
        final dot = find.byKey(ValueKey('retro-role-$id'));
        await t.ensureVisible(dot);
        await t.tap(dot);
        await t.pumpAndSettle();
      }

      expect(
        roster().headers,
        equals(const [
          'POS',
          'JOGADOR',
          'FORÇA',
          'ESTILO',
          'SALÁRIO',
          'CONTRATO',
        ]),
      );
      expect(roster().sectionHeaders, isNotEmpty);
      expect(roster().sectionHeaders.values.first[1], startsWith('BANCO ('));
      expect(roster().rows.first.length, 6);
      expect(canSend(), isTrue);
      await click('p-23');
      expect(find.textContaining('Titulares: 12/11'), findsWidgets);
      expect(canSend(), isFalse);
      expect(roster().rows.firstWhere((r) => r[1] == 'Atleta 2').first, '● D');
      expect(find.text('Trocar com titular'), findsNothing);
      expect(find.byType(PopupMenuItem<String>), findsNothing);
      await click('p-2');
      expect(find.textContaining('Banco: 10/9'), findsWidgets);
      expect(canSend(), isFalse);
      await click('p-2');
      expect(roster().rows.firstWhere((r) => r[1] == 'Atleta 2').first, '· D');
      expect(
        roster().headers,
        equals(const [
          'POS',
          'JOGADOR',
          'FORÇA',
          'ESTILO',
          'SALÁRIO',
          'CONTRATO',
        ]),
      );
      expect(roster().sectionHeaders, isNotEmpty);
      expect(roster().sectionHeaders.values.first[1], startsWith('BANCO ('));
      expect(roster().rows.first.length, 6);
      expect(canSend(), isTrue);
      await click('p-23');
      expect(canSend(), isFalse);
      await click('p-23');
      expect(canSend(), isFalse);
      await click('p-2');
      expect(
        roster().headers,
        equals(const [
          'POS',
          'JOGADOR',
          'FORÇA',
          'ESTILO',
          'SALÁRIO',
          'CONTRATO',
        ]),
      );
      expect(roster().sectionHeaders, isNotEmpty);
      expect(roster().sectionHeaders.values.first[1], startsWith('BANCO ('));
      expect(roster().rows.first.length, 6);
      expect(canSend(), isTrue);
      api.ready = 1;
      api.isReady = true;
      api.roundVersion++;
      api.onSnapshotUpdated?.call(await api.getSnapshot('world-011'));
      await t.pumpAndSettle();
      for (final cell in roster().leadingCells.values) {
        expect(((cell as Row).children.first as IconButton).onPressed, isNull);
      }
      expect(t.takeException(), isNull);
      await t.pumpWidget(const SizedBox());
      api.disconnectRealtime();
    },
  );
  for (final width in [360.0, 390.0, 430.0, 768.0, 1366.0, 1920.0]) {
    testWidgets(
      'Bolinha permite retirar e escalar; Banco abre empréstimos em $width px',
      (t) async {
        t.view.physicalSize = Size(width, 1000);
        t.view.devicePixelRatio = 1;
        addTearDown(t.view.resetPhysicalSize);
        addTearDown(t.view.resetDevicePixelRatio);
        final api = GameplayPresentationApi();
        await join(t, api);
        final controller = PresentationScope.maybeOf(
          t.element(find.byType(ElifootShellScreen)),
        )!;
        await controller.select(api, PresentationVersion.classic);
        await t.pumpAndSettle();
        final before = t.widget<RetroTable>(
          find.byKey(const Key('retro-roster')),
        );
        final starter = before.rows.indexWhere((r) => r.first == '● G');
        final name = before.rows[starter][1];
        final lead = before.leadingCells[starter]! as Row;
        final dot = lead.children.first as IconButton;
        final version = api.roundVersion;
        final finder = find.byKey(dot.key!);
        await t.ensureVisible(finder);
        await t.tap(finder);
        await t.pumpAndSettle();
        await t.tap(finder);
        await t.pumpAndSettle();
        var roster = t.widget<RetroTable>(
          find.byKey(const Key('retro-roster')),
        );
        expect(roster.rows.firstWhere((r) => r[1] == name).first, '· G');
        final removedIndex = roster.rows.indexWhere((r) => r[1] == name);
        final roleKey =
            ((roster.leadingCells[removedIndex] as Row).children.first
                    as IconButton)
                .key!;
        await t.tap(find.byKey(roleKey));
        await t.pumpAndSettle();
        roster = t.widget<RetroTable>(find.byKey(const Key('retro-roster')));
        expect(roster.rows.firstWhere((r) => r[1] == name).first, '● G');
        expect(api.roundVersion, version);
        if (width < 1024) {
          await t.tap(find.text('Banco').last);
        } else {
          await t.tap(find.text('Banco'));
        }
        await t.pumpAndSettle();
        expect(find.byKey(const Key('classic-bank-screen')), findsOneWidget);
        expect(
          find.byKey(const ValueKey('btn-emprestimo-bancario')),
          findsOneWidget,
        );
        expect(find.text('BANCO (RESERVAS)'), findsNothing);
        expect(
          find.byKey(const ValueKey('btn-reformar-estadio')),
          findsNothing,
        );
        await t.tap(find.text('Estádio'));
        await t.pumpAndSettle();
        expect(find.byType(ClassicStadiumView), findsOneWidget);
        expect(
          find.byKey(const ValueKey('btn-emprestimo-bancario')),
          findsNothing,
        );
        expect(t.takeException(), isNull);
        await t.pumpWidget(const SizedBox());
        api.disconnectRealtime();
      },
    );
  }

  test(
    'Desenho do estádio muda com ampliação, conservação e obras reais',
    () async {
      Future<List<int>> render({
        required int capacity,
        required int usable,
        required double condition,
        bool expansion = false,
      }) async {
        final recorder = ui.PictureRecorder();
        final canvas = Canvas(recorder);
        ClassicStadiumPainter(
          capacity: capacity,
          usable: usable,
          condition: condition,
          expansion: expansion,
          renovation: false,
        ).paint(canvas, const Size(720, 400));
        final picture = recorder.endRecording();
        final image = await picture.toImage(720, 400);
        final data = await image.toByteData();
        image.dispose();
        picture.dispose();
        return data!.buffer.asUint8List();
      }

      final small = await render(capacity: 8000, usable: 8000, condition: 100);
      final expanded = await render(
        capacity: 40000,
        usable: 40000,
        condition: 100,
      );
      final worn = await render(capacity: 40000, usable: 22000, condition: 60);
      final construction = await render(
        capacity: 40000,
        usable: 22000,
        condition: 60,
        expansion: true,
      );
      expect(listEquals(small, expanded), isFalse);
      expect(listEquals(expanded, worn), isFalse);
      expect(listEquals(worn, construction), isFalse);
    },
  );
}
