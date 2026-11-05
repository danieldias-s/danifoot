import 'dart:async';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:elifoot_flutter/salary_request_modal.dart';
import 'package:elifoot_flutter/main.dart';
import 'package:elifoot_flutter/presentation_version.dart';
import 'package:elifoot_flutter/presentation_widgets.dart';
import 'classic_ux_014b_test.dart' show join;
import 'presentation_017a_test.dart' show GameplayPresentationApi;

class PendingSalaryApi extends GameplayPresentationApi {
  bool pending = false;
  bool resolved = false;
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
    return null;
  }
}

void main() {
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
      if (classic) {
        await PresentationScope.maybeOf(
          tester.element(find.byType(ElifootShellScreen)),
        )!.select(api, PresentationVersion.classic);
        await tester.pumpAndSettle();
      }
      api.pending = true;
      api.onNotification?.call('Atualização salarial');
      await tester.pumpAndSettle();
      expect(find.byType(SalaryRequestModal), findsOneWidget);
      await tester.tap(find.text('ACEITAR'));
      await tester.pumpAndSettle();
      expect(api.resolved, isTrue);
      expect(find.byType(SalaryRequestModal), findsNothing);
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
          expect(find.text('SEM CONTRATO'), findsOneWidget);
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
