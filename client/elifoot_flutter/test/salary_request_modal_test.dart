import 'dart:async';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:elifoot_flutter/salary_request_modal.dart';
import 'package:elifoot_flutter/main.dart';
import 'package:elifoot_flutter/classic98_widgets.dart' show RetroButton;
import 'package:elifoot_flutter/match_router.dart';
import 'package:elifoot_flutter/presentation_version.dart';
import 'package:elifoot_flutter/presentation_widgets.dart';
import 'classic_ux_014b_test.dart' show join;
import 'presentation_017a_test.dart' show GameplayPresentationApi;

class PendingSalaryApi extends GameplayPresentationApi {
  bool pending = false;
  bool resolved = false;
  bool accepted = false;

  @override
  Future<Map<String, dynamic>> getClubDetail(String world, String club) async {
    final detail = await super.getClubDetail(world, club);
    return {
      ...detail,
      'squad': [
        for (final player in detail['squad'] as List)
          {
            ...player as Map,
            if (player['id'] == 'p-1') 'salaryRaiseAcceptedForSeason': accepted,
          },
      ],
    };
  }

  @override
  Future<Map<String, dynamic>> getClubFinances(
    String world,
    String club,
  ) async => {
    ...await super.getClubFinances(world, club),
    'salaryRequests': pending && !resolved
        ? [
            {
              'id': 'request',
              'clubId': club,
              'playerId': 'p-1',
              'status': 'PENDING',
              'currentSalary': 249,
              'requestedSalary': 254,
            },
          ]
        : [],
  };
  @override
  Future<String?> respondSalaryRequest(
    String world,
    String club,
    String coach,
    String request,
    bool accept,
  ) async {
    resolved = true;
    accepted = accept;
    return null;
  }
}

class PostRoundSalaryApi extends PendingSalaryApi {
  int blockedAdvances = 0;
  int successfulAdvances = 0;
  bool otherPending = false;

  @override
  Future<Map<String, dynamic>> getClubFinances(
    String world,
    String club,
  ) async {
    final data = await super.getClubFinances(world, club);
    return {
      ...data,
      'salaryRequests': [
        if (otherPending)
          {
            'id': 'other-request',
            'clubId': 'club-other',
            'playerId': 'p-1',
            'status': 'PENDING',
          },
        ...data['salaryRequests'] as List,
      ],
    };
  }

  @override
  Future<Map<String, dynamic>> advanceToNextRound(
    String worldId, {
    int? expectedSeasonNumber,
    int? expectedRoundNumber,
  }) async {
    if ((pending && !resolved) || otherPending) {
      blockedAdvances++;
      throw Exception('SALARY_PENDING_TRANSITION: Decisão salarial pendente.');
    }
    successfulAdvances++;
    return super.advanceToNextRound(
      worldId,
      expectedSeasonNumber: expectedSeasonNumber,
      expectedRoundNumber: expectedRoundNumber,
    );
  }
}

void main() {
  testWidgets('Other club does not reopen the modal or strand the round', (
    tester,
  ) async {
    tester.view.physicalSize = const Size(1024, 1000);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    addTearDown(tester.view.resetDevicePixelRatio);
    final api = PostRoundSalaryApi();
    await join(tester, api);
    await api.emit('RUNNING_FIRST_HALF');
    await tester.pumpAndSettle();
    api.pending = true;
    api.otherPending = true;
    await api.emit('RESULTS');
    await tester.pumpAndSettle();
    await tester.pump(const Duration(seconds: 5));
    await tester.pumpAndSettle();
    expect(find.byType(SalaryRequestModal), findsOneWidget);

    await tester.tap(find.text('ACEITAR'));
    await tester.pumpAndSettle();
    expect(api.resolved, isTrue);
    expect(api.successfulAdvances, 0);
    expect(find.byType(SalaryRequestModal), findsNothing);

    // The other client committed its decision and disconnected before requesting the next round.
    api.otherPending = false;
    await tester.pump(const Duration(seconds: 5));
    await tester.pumpAndSettle();
    expect(api.successfulAdvances, 1);
    expect(api.currentRound, 2);
    expect(tester.takeException(), isNull);
    await tester.pumpWidget(const SizedBox());
    api.disconnectRealtime();
  });

  for (final classic in [true, false]) {
    for (final completed in ['RESULTS', 'FINISHED']) {
      testWidgets(
        'Salary waits for classification exit and preserves round guard: $classic $completed',
        (tester) async {
          tester.view.physicalSize = const Size(1024, 1000);
          tester.view.devicePixelRatio = 1;
          addTearDown(tester.view.resetPhysicalSize);
          addTearDown(tester.view.resetDevicePixelRatio);
          final api = PostRoundSalaryApi();
          await join(tester, api);
          if (classic) {
            await PresentationScope.maybeOf(
              tester.element(find.byType(ElifootShellScreen)),
            )!.select(api, PresentationVersion.classic);
            await tester.pumpAndSettle();
          }
          final router =
              tester
                      .widget<MaterialApp>(find.byType(MaterialApp))
                      .routerDelegate!
                  as MatchRouterDelegate;
          await api.emit('RUNNING_FIRST_HALF');
          await tester.pumpAndSettle();
          api.pending = true;
          await api.emit(completed);
          api.onNotification?.call('Atualização salarial');
          await tester.pumpAndSettle();
          expect(router.currentConfiguration, '/divisoes');
          expect(find.byType(SalaryRequestModal), findsNothing);
          await tester.pump(const Duration(seconds: 3));
          await tester.pumpAndSettle();
          expect(router.currentConfiguration, '/divisoes');
          expect(find.byType(SalaryRequestModal), findsNothing);
          expect(api.resolved, isFalse);
          await tester.pump(const Duration(seconds: 2));
          await tester.pumpAndSettle();
          expect(router.currentConfiguration, '/plantel');
          expect(find.byType(SalaryRequestModal), findsOneWidget);
          expect(api.blockedAdvances, 1);
          expect(api.successfulAdvances, 0);
          expect(api.resolved, isFalse);
          await tester.tap(find.text('ACEITAR'));
          await tester.pumpAndSettle();
          expect(api.resolved, isTrue);
          expect(api.successfulAdvances, 1);
          expect(api.currentRound, 2);
          expect(router.currentConfiguration, '/plantel');
          expect(find.byType(SalaryRequestModal), findsNothing);
          expect(tester.takeException(), isNull);
          await tester.pumpWidget(const SizedBox());
          api.disconnectRealtime();
        },
      );
    }
  }

  for (final classic in [true, false]) {
    testWidgets('pending request opens at team home only, classic=$classic', (
      tester,
    ) async {
      tester.view.physicalSize = const Size(1366, 1000);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      final api = PendingSalaryApi();
      await join(tester, api);
      final renew = find.byKey(
        Key(classic ? 'retro-renew-contract' : 'renew-contract-p-1'),
      );
      VoidCallback? renewAction() => classic
          ? tester.widget<RetroButton>(renew).onPressed
          : tester.widget<IconButton>(renew).onPressed;
      if (classic) {
        await PresentationScope.maybeOf(
          tester.element(find.byType(ElifootShellScreen)),
        )!.select(api, PresentationVersion.classic);
        await tester.pumpAndSettle();
        final table = find.byKey(const Key('retro-roster'));
        final player = find.descendant(
          of: table,
          matching: find.textContaining('Carlos Muralha'),
        );
        await tester.ensureVisible(player.first);
        await tester.tap(player.first);
        await tester.pumpAndSettle();
      }
      expect(renewAction(), isNotNull);
      api.pending = true;
      api.onNotification?.call('Atualização salarial');
      await tester.pumpAndSettle();
      expect(find.byType(SalaryRequestModal), findsOneWidget);
      expect(find.text('✓'), findsNothing);
      await tester.tap(find.text('ACEITAR'));
      await tester.pumpAndSettle();
      expect(api.resolved, isTrue);
      expect(find.byType(SalaryRequestModal), findsNothing);
      expect(find.text('✓'), findsOneWidget);
      expect(renewAction(), isNull);
      await tester.pumpWidget(const SizedBox());
      api.disconnectRealtime();
    });
  }
  for (final classic in [true, false]) {
    for (final width in [390.0, 1366.0]) {
      testWidgets(
        'salary modal $classic at $width keeps pending and sends one decision',
        (tester) async {
          tester.view.physicalSize = Size(width, 900);
          tester.view.devicePixelRatio = 1;
          addTearDown(tester.view.resetPhysicalSize);
          addTearDown(tester.view.resetDevicePixelRatio);
          final completion = Completer<void>();
          final decisions = <bool>[];
          await tester.pumpWidget(
            MaterialApp(
              home: SalaryRequestModal(
                request: const {'currentSalary': 249, 'requestedSalary': 254},
                player: const {'displayName': 'Daniel Dias', 'position': 'D'},
                club: const {'name': 'Atlético'},
                classic: classic,
                onDecision: (accept) {
                  decisions.add(accept);
                  return completion.future;
                },
              ),
            ),
          );
          expect(find.text('PEDIDO ESPONTÂNEO'), findsOneWidget);
          expect(find.textContaining('Posição: D'), findsOneWidget);
          expect(find.textContaining('R\$ 254'), findsOneWidget);
          await tester.tap(find.text('ACEITAR'));
          await tester.pump();
          await tester.tap(find.text('RECUSAR'));
          expect(decisions, [true]);
          completion.completeError(Exception('Sem rede; pedido preservado'));
          await tester.pumpAndSettle();
          expect(find.textContaining('pedido preservado'), findsOneWidget);
          expect(tester.takeException(), isNull);
        },
      );
    }
  }
}
