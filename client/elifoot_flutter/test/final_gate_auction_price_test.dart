import 'package:elifoot_flutter/market_screen.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'match_market_015a_test.dart' show Market015Api, auction015;

void main() {
  for (final width in [390.0, 1024.0, 1360.0]) {
    for (final scenario in [
      (
        name: 'closed salary refusal',
        minimum: 0,
        bid: 45000,
        owner: 'winner',
        closed: true,
        amount: 'R\$ 45.000',
      ),
      (
        name: 'open auction with bid',
        minimum: 500000,
        bid: 550000,
        owner: 'winner',
        closed: false,
        amount: 'R\$ 550.000',
      ),
      (
        name: 'no bids',
        minimum: 500000,
        bid: 0,
        owner: null,
        closed: false,
        amount: 'R\$ 500.000',
      ),
      (
        name: 'valid zero bid',
        minimum: 0,
        bid: 0,
        owner: 'winner',
        closed: true,
        amount: 'R\$ 0',
      ),
    ]) {
      testWidgets('Classic auction price ${scenario.name} at $width', (
        tester,
      ) async {
        tester.view.devicePixelRatio = 1;
        tester.view.physicalSize = Size(width, 900);
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);
        await tester.pumpWidget(
          MaterialApp(
            home: Scaffold(
              body: MarketScreen(
                api: Market015Api(),
                worldId: 'w',
                coachId: 'coach',
                clubId: 'buyer',
                snapshot: const {
                  'roundState': 'PREPARING',
                  'roundNumber': 3,
                  'seasonNumber': 1,
                },
                data: {
                  'listings': [],
                  'offers': [],
                  'auctions': [
                    {
                      ...auction015,
                      'minimumBid': scenario.minimum,
                      'currentBid': scenario.bid,
                      'highestBidderClubId': scenario.owner,
                      'isClosed': scenario.closed,
                    },
                  ],
                },
                squad: const [],
                preferences: MarketPreferences(),
                refresh: () async {},
                compactClassic: true,
              ),
            ),
          ),
        );
        await tester.pumpAndSettle();
        expect(find.textContaining(scenario.amount), findsOneWidget);
        expect(tester.takeException(), isNull);
      });
    }
  }
}
