import 'dart:async';
import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:elifoot_flutter/api_client.dart';
import 'package:elifoot_flutter/classic98_widgets.dart';
import 'package:elifoot_flutter/contract_negotiation_dialog.dart';
import 'package:elifoot_flutter/main.dart';
import 'package:elifoot_flutter/presentation_version.dart';
import 'package:elifoot_flutter/presentation_widgets.dart';
import 'classic_ux_014b_test.dart' show join;
import 'salary_rules_correction_test.dart' show AnnualRenewalApi;

class NegotiationApi extends AnnualRenewalApi {
  final decisions = <(double, bool, String)>[];
  String? released;
  double salary = 100;
  @override
  Future<Map<String, dynamic>> getClubDetail(String w, String c) async {
    final club = await super.getClubDetail(w, c);
    (club['squad'] as List).removeWhere((p) => p['id'] == released);
    for (final p in club['squad'] as List) {
      if (renewed.contains(p['id'])) p['salaryPerRound'] = salary;
    }
    return club;
  }

  @override
  Future<Map<String, dynamic>> getRenewalOfferPreview(
    String w,
    String p,
    String c,
    double offeredSalary,
  ) async => {
    ...await getRenewalContractPreview(w, p, c),
    'offeredSalary': offeredSalary,
    'offeredSigningFee': offeredSalary * 3,
    'offerAccepted': offeredSalary >= 110,
  };
  @override
  Future<void> decideRenewalOffer(
    String w,
    String p,
    String c,
    double offeredSalary,
    bool accept,
    String token,
  ) async {
    decisions.add((offeredSalary, accept, token));
    if (accept) {
      salary = offeredSalary;
      renewed.add(p);
    } else {
      released = p;
    }
  }
}

void main() {
  for (final width in [390.0, 768.0, 1024.0, 1366.0]) {
    for (final outcome in ['offer', 'counter', 'refuse', 'cancel']) {
      testWidgets('Classic selected player negotiation $outcome at $width', (
        t,
      ) async {
        t.view.physicalSize = Size(width, 1000);
        t.view.devicePixelRatio = 1;
        addTearDown(t.view.resetPhysicalSize);
        addTearDown(t.view.resetDevicePixelRatio);
        final api = NegotiationApi();
        await join(t, api);
        await PresentationScope.maybeOf(
          t.element(find.byType(ElifootShellScreen)),
        )!.select(api, PresentationVersion.classic);
        await t.pumpAndSettle();
        final action = find.byKey(const Key('retro-renew-contract'));
        expect(action, findsNothing);
        final roster = t.widget<RetroTable>(
          find.byKey(const Key('retro-roster')),
        );
        roster.onSelect!(
          roster.rows.indexWhere((r) => r[1].startsWith('Carlos Muralha')),
        );
        await t.pumpAndSettle();
        await t.ensureVisible(action);
        await t.tap(action);
        await t.pumpAndSettle();
        expect(find.byType(ContractNegotiationDialog), findsOneWidget);
        final input = find.byKey(const Key('renewal-salary-offer'));
        expect(t.widget<TextFormField>(input).controller!.text, '100,00');
        if (outcome == 'offer') await t.enterText(input, '150,50');
        await t.tap(find.byKey(const Key('consult-renewal-offer')));
        await t.pumpAndSettle();
        expect(api.decisions, isEmpty);
        if (outcome == 'offer') {
          expect(find.text('PROPOSTA ACEITA'), findsOneWidget);
          expect(
            find.text('Novo salário: R\$ 150,50 / partida'),
            findsOneWidget,
          );
          expect(find.text('Luvas: R\$ 451,50'), findsOneWidget);
        } else {
          expect(find.text('CONTRAPROPOSTA DO JOGADOR'), findsOneWidget);
          expect(find.textContaining('R\$ 110'), findsWidgets);
        }
        await t.tap(
          outcome == 'cancel'
              ? find.text('CANCELAR')
              : find.byKey(
                  Key(
                    outcome == 'refuse'
                        ? 'refuse-renewal-counteroffer'
                        : 'accept-renewal-offer',
                  ),
                ),
        );
        await t.pumpAndSettle();
        expect(find.byType(ContractNegotiationDialog), findsNothing);
        if (outcome == 'cancel') {
          expect(api.decisions, isEmpty);
          expect(api.renewed, isEmpty);
        } else if (outcome == 'refuse') {
          expect(api.decisions, [(100.0, false, 'authoritative-renewal')]);
          expect(api.released, 'p-1');
          expect(action, findsNothing);
        } else {
          expect(api.decisions, [
            (
              outcome == 'offer' ? 150.50 : 110.0,
              true,
              'authoritative-renewal',
            ),
          ]);
          expect(api.renewed, contains('p-1'));
          expect(t.widget<RetroButton>(action).onPressed, isNull);
          final updated = t.widget<RetroTable>(
            find.byKey(const Key('retro-roster')),
          );
          expect(
            updated.rows.firstWhere(
              (r) => r[1].startsWith('Carlos Muralha'),
            )[5],
            'RENOVADO',
          );
        }
        expect(t.takeException(), isNull);
        await t.pumpWidget(const SizedBox());
        api.disconnectRealtime();
      });
    }
  }

  testWidgets(
    'Invalid money and repeated clicks cannot submit a decision twice',
    (t) async {
      final pending = Completer<void>();
      int decisions = 0;
      await t.pumpWidget(
        MaterialApp(
          home: Builder(
            builder: (ctx) => Scaffold(
              body: TextButton(
                onPressed: () => showDialog<void>(
                  context: ctx,
                  builder: (_) => ContractNegotiationDialog(
                    player: const {'name': 'Jogador'},
                    initialPreview: const {'currentSalary': 100},
                    onQuote: (_) async => {
                      'offerAccepted': false,
                      'requiredSalary': 110,
                      'signingFee': 330,
                      'season': 1,
                    },
                    onDecision: (_, _, _) {
                      decisions++;
                      return pending.future;
                    },
                  ),
                ),
                child: const Text('ABRIR'),
              ),
            ),
          ),
        ),
      );
      await t.tap(find.text('ABRIR'));
      await t.pumpAndSettle();
      final input = find.byKey(const Key('renewal-salary-offer'));
      await t.enterText(input, '-1');
      await t.tap(find.byKey(const Key('consult-renewal-offer')));
      await t.pumpAndSettle();
      expect(find.textContaining('Informe um valor válido'), findsOneWidget);
      await t.enterText(input, '100');
      await t.tap(find.byKey(const Key('consult-renewal-offer')));
      await t.pumpAndSettle();
      await t.tap(find.byKey(const Key('accept-renewal-offer')));
      await t.pump();
      await t.tap(find.byKey(const Key('refuse-renewal-counteroffer')));
      expect(decisions, 1);
      pending.completeError(Exception('Elenco mínimo: pedido preservado.'));
      await t.pumpAndSettle();
      expect(find.textContaining('Elenco mínimo'), findsOneWidget);
      expect(find.byType(ContractNegotiationDialog), findsOneWidget);
      expect(t.takeException(), isNull);
    },
  );

  test(
    'New transport binds amount, quote and decision and retains identity after 500',
    () async {
      final writes = <http.Request>[];
      final api = ElifootApiClient(
        baseUrl: 'http://example.test',
        httpClient: MockClient((r) async {
          if (r.method == 'GET') {
            expect(r.url.queryParameters['offeredSalary'], '150.50');
            return http.Response(
              jsonEncode({'roundVersion': 7, 'termsToken': 'quote'}),
              200,
            );
          }
          writes.add(r);
          return http.Response(
            writes.length == 1
                ? '{"error":"CONTRACT_PERSISTENCE_FAILED"}'
                : '{}',
            writes.length == 1 ? 500 : 200,
          );
        }),
      );
      await api.getRenewalOfferPreview('w', 'p', 'coach', 150.50);
      await expectLater(
        api.decideRenewalOffer('w', 'p', 'coach', 150.50, true, 'quote'),
        throwsException,
      );
      await api.decideRenewalOffer('w', 'p', 'coach', 150.50, true, 'quote');
      final first = jsonDecode(writes[0].body) as Map;
      final second = jsonDecode(writes[1].body) as Map;
      expect(first['idempotencyKey'], second['idempotencyKey']);
      expect(first['offeredSalary'], 150.50);
      expect(first['accept'], true);
      expect(first['termsToken'], 'quote');
      expect(first['expectedRoundVersion'], 7);
    },
  );
}
