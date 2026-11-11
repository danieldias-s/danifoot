import 'package:elifoot_flutter/market_screen.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'match_market_015a_test.dart' show Market015Api, market015;

class ClassicalEconomyPreviewApi extends Market015Api {
  String? confirmedToken;
  Map<String, dynamic>? lastPreviewReturned;

  @override
  Future<Map<String, dynamic>> getTransferContractPreview(
    String w,
    String c,
    String p, {
    String? offerId,
    String? auctionId,
  }) async {
    final basePreview = await super.getTransferContractPreview(
      w,
      c,
      p,
      offerId: offerId,
      auctionId: auctionId,
    );
    final enriched = <String, dynamic>{
      ...basePreview,
      'currentSalaryPerRound': 3100,
      'signingSalaryPerRound': 4300,
      'calculatedBaseSalary': 4300,
      'marketValue': 185440,
      'candidateSigningPrice': 344704,
      'nationalityClass': 'SAME_COUNTRY',
      'aiHiringInterestFactor': 1.65,
      'inflationIndex': 2.0,
      'inflationDisplay': 20,
      'salaryTermsToken': 'classical-02c-token',
    };
    lastPreviewReturned = enriched;
    return enriched;
  }

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

void main() {
  testWidgets(
    'Classical economy preview displays server-calculated signing salary and confirms with server token',
    (tester) async {
      await tester.binding.setSurfaceSize(const Size(1024, 900));
      addTearDown(() => tester.binding.setSurfaceSize(null));

      final api = ClassicalEconomyPreviewApi();
      await tester.pumpWidget(market015(api, MarketPreferences()..tab = 1));
      await tester.pumpAndSettle();

      final buy = find.byKey(const ValueKey('purchase-p'));
      await tester.ensureVisible(buy);
      await tester.tap(buy);
      await tester.pumpAndSettle();

      expect(find.text('Salário atual: R\$ 3.100 / rodada'), findsOneWidget);
      expect(
        find.text('Salário após contratação: R\$ 4.300 / rodada'),
        findsOneWidget,
      );
      expect(api.lastPreviewReturned?['calculatedBaseSalary'], 4300);
      expect(api.lastPreviewReturned?['marketValue'], 185440);
      expect(api.lastPreviewReturned?['candidateSigningPrice'], 344704);
      expect(api.lastPreviewReturned?['nationalityClass'], 'SAME_COUNTRY');
      expect(api.lastPreviewReturned?['aiHiringInterestFactor'], 1.65);
      expect(api.lastPreviewReturned?['inflationIndex'], 2.0);
      expect(api.lastPreviewReturned?['inflationDisplay'], 20);

      final confirm = find.byKey(const ValueKey('direct-confirm-buy'));
      await tester.ensureVisible(confirm);
      await tester.tap(confirm);
      await tester.pumpAndSettle();

      expect(api.buys, 1);
      expect(api.confirmedToken, 'classical-02c-token');
      expect(tester.takeException(), isNull);
    },
  );
}
