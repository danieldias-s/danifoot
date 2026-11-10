import 'package:elifoot_flutter/api_client.dart';
import 'package:elifoot_flutter/market_screen.dart';
import 'package:elifoot_flutter/salary_request_modal.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'dart:convert';
import 'match_market_015a_test.dart' show Market015Api, market015;

class TermsApi extends Market015Api {
  String? confirmedToken;
  @override
  Future<void> buyConfirmedListing(
    String w,
    String c,
    Map<String, dynamic> listing,
  ) async {
    confirmedToken = listing['salaryTermsToken'] as String?;
    await super.buyConfirmedListing(w, c, listing);
  }
}

class WithdrawnTermsApi extends TermsApi {
  @override
  Future<Map<String, dynamic>> getTransferContractPreview(
    String w,
    String c,
    String p, {
    String? offerId,
    String? auctionId,
  }) async => {
    ...await super.getTransferContractPreview(
      w,
      c,
      p,
      offerId: offerId,
      auctionId: auctionId,
    ),
    'askingPrice': null,
  };
}

void main() {
  testWidgets(
    'Withdrawn listing cannot open a price-less purchase confirmation',
    (tester) async {
      final api = WithdrawnTermsApi();
      await tester.pumpWidget(market015(api, MarketPreferences()..tab = 1));
      await tester.pumpAndSettle();
      final buy = find.byKey(const ValueKey('purchase-p'));
      await tester.ensureVisible(buy);
      await tester.tap(buy);
      await tester.pumpAndSettle();
      expect(find.byKey(const ValueKey('direct-confirm-buy')), findsNothing);
      expect(api.buys, 0);
      expect(find.textContaining('ANÚNCIO ALTERADO'), findsOneWidget);
      expect(tester.takeException(), isNull);
    },
  );
  for (final width in [320.0, 390.0, 1024.0, 1360.0]) {
    testWidgets('Server wage before purchase and safe controls at $width', (
      tester,
    ) async {
      await tester.binding.setSurfaceSize(Size(width, 900));
      addTearDown(() => tester.binding.setSurfaceSize(null));
      final api = TermsApi();
      await tester.pumpWidget(market015(api, MarketPreferences()..tab = 1));
      await tester.pumpAndSettle();
      final buy = find.byKey(const ValueKey('purchase-p'));
      await tester.ensureVisible(buy);
      await tester.tap(buy);
      await tester.pumpAndSettle();
      expect(find.text('Salário atual: R\$ 4.321 / rodada'), findsOneWidget);
      expect(
        find.text('Salário após contratação: R\$ 5.000 / rodada'),
        findsOneWidget,
      );
      expect(find.text('Comprador: Comprador'), findsOneWidget);
      expect(api.buys, 0);
      final confirm = find.byKey(const ValueKey('direct-confirm-buy'));
      await tester.ensureVisible(confirm);
      await tester.tap(confirm);
      await tester.pumpAndSettle();
      expect(api.buys, 1);
      expect(api.confirmedToken, 'server-terms');
      expect(tester.takeException(), isNull);
    });
  }

  testWidgets('Auction salary is clearly an estimate', (tester) async {
    await tester.binding.setSurfaceSize(const Size(1024, 900));
    addTearDown(() => tester.binding.setSurfaceSize(null));
    await tester.pumpWidget(market015(Market015Api(), MarketPreferences()));
    await tester.pumpAndSettle();
    final bid = find.byKey(const ValueKey('bid-a'));
    await tester.ensureVisible(bid);
    await tester.tap(bid);
    await tester.pumpAndSettle();
    expect(find.text('Prévia de salário: R\$ 5.000 / rodada'), findsOneWidget);
    expect(
      find.text('Estimativa atual; salário revalidado na liquidação.'),
      findsOneWidget,
    );
    expect(tester.takeException(), isNull);
  });

  testWidgets('Spontaneous request requires decision and cannot be dismissed', (
    tester,
  ) async {
    int decisions = 0;
    await tester.pumpWidget(
      MaterialApp(
        home: SalaryRequestModal(
          request: const {
            'currentSalary': 100,
            'requestedSalary': 110,
            'mandatory': true,
          },
          player: const {'name': 'Atleta'},
          club: const {'name': 'Clube'},
          classic: true,
          onDecision: (_) async {
            decisions++;
          },
        ),
      ),
    );
    expect(find.textContaining('recomponha o elenco'), findsOneWidget);
    expect(find.text('VOLTAR AO CLUBE'), findsNothing);
    expect(find.text('DESISTIR'), findsNothing);
    await tester.binding.handlePopRoute();
    await tester.pump();
    expect(find.byType(SalaryRequestModal), findsOneWidget);
    expect(decisions, 0);
  });

  test(
    'API sends the server token and price, preserving retry identity after 500',
    () async {
      final requests = <http.Request>[];
      int calls = 0;
      final api = ElifootApiClient(
        httpClient: MockClient((request) async {
          requests.add(request);
          if (request.method == 'GET') {
            return http.Response(
              jsonEncode({
                'playerId': 'p',
                'askingPrice': 100000,
                'salaryTermsToken': 'server-token',
                'signingSalaryPerRound': 222,
                'roundVersion': 7,
              }),
              200,
            );
          }
          calls++;
          return http.Response(
            calls == 1 ? '{}' : '{"success":true}',
            calls == 1 ? 500 : 200,
          );
        }),
      );
      final preview = await api.getTransferContractPreview('w', 'coach', 'p');
      await expectLater(
        api.buyConfirmedListing('w', 'coach', preview),
        throwsException,
      );
      await api.buyConfirmedListing('w', 'coach', preview);
      final writes = requests.where((r) => r.method == 'POST').toList();
      expect(writes[0].url.queryParameters['salaryTermsToken'], 'server-token');
      expect(writes[0].url.queryParameters['askingPrice'], '100000.00');
      expect(writes[0].url.queryParameters['expectedRoundVersion'], '7');
      expect(
        writes[1].url.queryParameters['idempotencyKey'],
        writes[0].url.queryParameters['idempotencyKey'],
      );
    },
  );
}
