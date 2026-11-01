import 'package:elifoot_flutter/api_client.dart';
import 'package:elifoot_flutter/classic_match_board.dart';
import 'package:elifoot_flutter/market_screen.dart';
import 'package:elifoot_flutter/match_router.dart';
import 'package:elifoot_flutter/morale_bar.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'classic_ux_014b_test.dart' show WaitingRoomApi, join;

class Market015Api extends ElifootApiClient {
  int buys = 0, bids = 0, offers = 0;
  double? lastBid;
  @override
  Future<void> buyConfirmedListing(
    String w,
    String c,
    Map<String, dynamic> listing,
  ) async {
    buys++;
  }

  @override
  Future<void> placeAuctionBid(
    String w,
    String a,
    String c,
    double amount,
  ) async {
    bids++;
    lastBid = amount;
  }

  @override
  Future<Map<String, dynamic>> makeTransferOffer(
    String w,
    String c,
    String p,
    double amount,
  ) async {
    offers++;
    return {'status': 'Pending'};
  }
}

final listing015 = <String, dynamic>{
  'playerId': 'p',
  'displayName': 'Atacante real do servidor',
  'position': 'A',
  'strength': 42,
  'sellerClubId': 'seller',
  'sellerClubName': 'Vendedor',
  'askingPrice': 200000,
  'salaryPerRound': 4321,
  'contractRoundsRemaining': 20,
  'listingVersion': 3,
};
final auction015 = <String, dynamic>{
  'auctionId': 'a',
  'playerId': 'p',
  'displayName': 'Atacante em leilão',
  'position': 'A',
  'strength': 42,
  'sellerClubId': 'seller',
  'sellerClubName': 'Vendedor',
  'minimumBid': 500000,
  'currentBid': 550000,
  'highestBidderClubId': 'other',
  'isClosed': false,
  'salaryPerRound': 4321,
  'roundsRemaining': 2,
  'reason': 'Voluntary',
};
Widget market015(Market015Api api, MarketPreferences preferences) =>
    MaterialApp(
      home: Scaffold(
        body: MarketScreen(
          api: api,
          worldId: 'w',
          coachId: 'buyer-coach',
          clubId: 'buyer',
          snapshot: const {
            'roundState': 'PREPARING',
            'roundNumber': 7,
            'seasonNumber': 1,
            'totalRoundsInSeason': 22,
          },
          data: {
            'listings': [listing015],
            'auctions': [auction015],
            'offers': [],
          },
          squad: const [],
          preferences: preferences,
          refresh: () async {},
        ),
      ),
    );
void main() {
  testWidgets('Counteroffer uses actual listing price and contracted salary', (
    tester,
  ) async {
    await tester.binding.setSurfaceSize(const Size(1366, 1000));
    addTearDown(() => tester.binding.setSurfaceSize(null));
    await tester.pumpWidget(
      MaterialApp(
        home: Scaffold(
          body: MarketScreen(
            api: Market015Api(),
            worldId: 'w',
            coachId: 'seller-coach',
            clubId: 'seller',
            snapshot: const {'roundState': 'PREPARING'},
            data: {
              'listings': [listing015],
              'auctions': [],
              'offers': [
                {
                  'offerId': 'o',
                  'playerId': 'p',
                  'playerDisplayName': listing015['displayName'],
                  'position': 'A',
                  'strength': 42,
                  'sellerClubId': 'seller',
                  'sellerClubName': 'Vendedor',
                  'buyerClubId': 'buyer',
                  'buyerClubName': 'Comprador',
                  'status': 'Pending',
                  'offeredAmount': 170000,
                },
              ],
            },
            squad: const [],
            preferences: MarketPreferences()..tab = 1,
            refresh: () async {},
          ),
        ),
      ),
    );
    await tester.pumpAndSettle();
    await tester.ensureVisible(find.text('CONTRAPROPOSTA'));
    await tester.tap(find.text('CONTRAPROPOSTA'));
    await tester.pumpAndSettle();
    expect(find.text('Preço fixo: R\$ 200.000'), findsOneWidget);
    expect(find.text('Salário: R\$ 4.321 / rodada'), findsOneWidget);
    expect(find.text('Preço fixo: R\$ 0'), findsNothing);
    await tester.tap(find.text('CANCELAR'));
    await tester.pumpAndSettle();
    expect(tester.takeException(), isNull);
  });
  for (final width in [390.0, 1366.0]) {
    for (final morale in [(20, 'VERMELHA'), (50, 'AMARELA'), (70, 'VERDE')]) {
      testWidgets(
        'Morale bar only and accessible real value ${morale.$1} at $width',
        (tester) async {
          await tester.binding.setSurfaceSize(Size(width, 900));
          addTearDown(() => tester.binding.setSurfaceSize(null));
          await tester.pumpWidget(
            MaterialApp(
              home: Scaffold(
                body: MoraleBar(value: morale.$1, band: morale.$2),
              ),
            ),
          );
          expect(find.byType(Text), findsNothing);
          expect(
            tester
                .widget<LinearProgressIndicator>(
                  find.byKey(const ValueKey('morale-bar-fill')),
                )
                .value,
            morale.$1 / 100,
          );
          final semantics = tester.widget<Semantics>(
            find
                .byWidgetPredicate(
                  (w) =>
                      w is Semantics &&
                      w.properties.label == 'Moral do treinador',
                )
                .first,
          );
          expect(semantics.properties.value, contains('${morale.$1} de 100'));
          expect(tester.takeException(), isNull);
        },
      );
    }
    testWidgets(
      'Purchase and offer are separate confirmed commands at $width',
      (tester) async {
        await tester.binding.setSurfaceSize(Size(width, 1000));
        addTearDown(() => tester.binding.setSurfaceSize(null));
        final api = Market015Api();
        final pref = MarketPreferences()..tab = 1;
        await tester.pumpWidget(market015(api, pref));
        await tester.pumpAndSettle();
        expect(find.textContaining('Atacante em leilão'), findsNothing);
        final buy = find.byKey(const ValueKey('purchase-p'));
        await tester.ensureVisible(buy);
        await tester.tap(buy);
        await tester.pumpAndSettle();
        expect(api.buys, 0);
        expect(find.text('COMPRAR JOGADOR'), findsWidgets);
        await tester.tap(find.text('CANCELAR'));
        await tester.pumpAndSettle();
        expect(api.buys, 0);
        await tester.tap(buy);
        await tester.pumpAndSettle();
        await tester.tap(find.byKey(const ValueKey('direct-custom-offer')));
        await tester.pumpAndSettle();
        await tester.enterText(
          find.byKey(const ValueKey('market-custom-amount')),
          '170000',
        );
        await tester.tap(find.byKey(const ValueKey('market-confirm-amount')));
        await tester.pumpAndSettle();
        expect(api.offers, 1);
        expect(api.buys, 0);
        expect(find.text('PROPOSTA ENVIADA.'), findsOneWidget);
        await tester.ensureVisible(buy);
        await tester.tap(buy);
        await tester.pumpAndSettle();
        await tester.tap(find.byKey(const ValueKey('direct-confirm-buy')));
        await tester.pumpAndSettle();
        expect(api.buys, 1);
        expect(find.text('COMPRA CONFIRMADA.'), findsOneWidget);
        expect(tester.takeException(), isNull);
      },
    );
    testWidgets('Custom bid validates increment and cancellation at $width', (
      tester,
    ) async {
      await tester.binding.setSurfaceSize(Size(width, 1000));
      addTearDown(() => tester.binding.setSurfaceSize(null));
      final api = Market015Api();
      await tester.pumpWidget(market015(api, MarketPreferences()));
      await tester.pumpAndSettle();
      expect(find.textContaining('Atacante real do servidor'), findsNothing);
      final bid = find.byKey(const ValueKey('bid-a'));
      await tester.ensureVisible(bid);
      await tester.tap(bid);
      await tester.pumpAndSettle();
      expect(api.bids, 0);
      await tester.enterText(
        find.byKey(const ValueKey('market-custom-amount')),
        '550001',
      );
      await tester.tap(find.byKey(const ValueKey('market-confirm-amount')));
      await tester.pumpAndSettle();
      expect(find.textContaining('LANCE INSUFICIENTE'), findsOneWidget);
      expect(api.bids, 0);
      await tester.enterText(
        find.byKey(const ValueKey('market-custom-amount')),
        '600123,45',
      );
      await tester.tap(find.byKey(const ValueKey('market-confirm-amount')));
      await tester.pumpAndSettle();
      expect(api.bids, 1);
      expect(api.lastBid, 600123.45);
      expect(find.text('LANCE REGISTRADO.'), findsOneWidget);
      await tester.ensureVisible(bid);
      await tester.tap(bid);
      await tester.pumpAndSettle();
      await tester.tap(find.text('CANCELAR'));
      await tester.pumpAndSettle();
      expect(api.bids, 1);
      expect(tester.takeException(), isNull);
    });
  }
  for (final order in [
    'PRICE_ASC',
    'PRICE_DESC',
    'STRENGTH_ASC',
    'STRENGTH_DESC',
  ]) {
    for (final auction in [false, true]) {
      test('Numeric stable sort $order auction=$auction', () {
        final rows = [
          {
            'playerId': 'z',
            'auctionId': 'z',
            'position': 0,
            'strength': 10,
            'askingPrice': 100,
            'minimumBid': 999,
            'currentBid': 100,
            'highestBidderClubId': 'leader',
          },
          {
            'playerId': 'a',
            'auctionId': 'a',
            'position': 'A',
            'strength': 20,
            'askingPrice': 20,
            'minimumBid': 20,
          },
          {
            'playerId': 'b',
            'auctionId': 'b',
            'position': 'A',
            'strength': 20,
            'askingPrice': 20,
            'minimumBid': 20,
          },
        ];
        final sorted = orderedMarketPlayers(
          rows,
          auction: auction,
          position: 'ALL',
          order: order,
        );
        expect(
          sorted.map((r) => r['playerId']).toList(),
          order == 'PRICE_DESC' || order == 'STRENGTH_ASC'
              ? ['z', 'a', 'b']
              : ['a', 'b', 'z'],
        );
        for (final position in ['G', 'D', 'M', 'A']) {
          final filtered = orderedMarketPlayers(
            rows,
            auction: auction,
            position: position,
            order: order,
          );
          expect(
            filtered.every((r) => marketPosition(r['position']) == position),
            isTrue,
          );
          expect(
            filtered.length,
            position == 'A'
                ? 2
                : position == 'G'
                ? 1
                : 0,
          );
        }
      });
    }
  }
  for (final n in [2, 4, 6, 8]) {
    testWidgets(
      'Server start locks internal URL back and menus; results release $n humans',
      (tester) async {
        await tester.binding.setSurfaceSize(const Size(1366, 1000));
        addTearDown(() => tester.binding.setSurfaceSize(null));
        final api = WaitingRoomApi(n);
        await join(tester, api);
        final app = tester.widget<MaterialApp>(find.byType(MaterialApp));
        final router = app.routerDelegate! as MatchRouterDelegate;
        await router.setNewRoutePath('/mercado');
        await tester.pumpAndSettle();
        expect(find.byType(MarketScreen), findsOneWidget);
        await api.emit('RUNNING_FIRST_HALF');
        await tester.pumpAndSettle();
        expect(find.byType(ClassicMatchBoard), findsOneWidget);
        expect(
          tester
              .widget<OutlinedButton>(
                find.widgetWithText(OutlinedButton, 'TROCAR SALA'),
              )
              .onPressed,
          isNull,
        );
        for (final path in MatchNavigationController.paths) {
          await router.setNewRoutePath(path);
          await tester.pumpAndSettle();
          expect(find.byType(ClassicMatchBoard), findsOneWidget);
          expect(router.currentConfiguration, '/partida');
        }
        expect(await router.popRoute(), isTrue);
        await tester.pumpAndSettle();
        expect(find.byType(ClassicMatchBoard), findsOneWidget);
        await api.emit('GLOBAL_PAUSED');
        await tester.pumpAndSettle();
        expect(
          find.byKey(const ValueKey('in-match-substitution-panel')),
          findsOneWidget,
        );
        await api.emit('RESULTS');
        await tester.pumpAndSettle();
        expect(router.navigation.locked, isFalse);
        expect(
          tester
              .widget<OutlinedButton>(
                find.widgetWithText(OutlinedButton, 'TROCAR SALA'),
              )
              .onPressed,
          isNotNull,
        );
        await router.setNewRoutePath('/mercado');
        await tester.pumpAndSettle();
        expect(find.byType(MarketScreen), findsOneWidget);
        api.onSnapshotUpdated?.call({
          'gameWorldId': 'world-011',
          'roundVersion': 0,
          'roundState': 'RUNNING_FIRST_HALF',
        });
        await tester.pumpAndSettle();
        expect(find.byType(MarketScreen), findsOneWidget);
        expect(tester.takeException(), isNull);
      },
    );
  }
  test(
    'Only authoritative live phases lock; preparation/results/finished release',
    () {
      final nav = MatchNavigationController();
      for (final stage in [
        'RUNNING_FIRST_HALF',
        'HALFTIME_PAUSED',
        'RUNNING_SECOND_HALF',
        'GLOBAL_PAUSED',
        'EXTRA_TIME_BREAK',
        'RUNNING_EXTRA_TIME',
        'EXTRA_TIME_HALFTIME',
      ]) {
        nav.acceptStage(stage);
        nav.navigate('/mercado');
        expect(nav.locked, isTrue);
        expect(nav.path, '/partida');
      }
      for (final stage in ['PREPARING', 'ALL_READY', 'RESULTS', 'FINISHED']) {
        nav.acceptStage(stage);
        nav.navigate('/mercado');
        expect(nav.locked, isFalse);
        expect(nav.path, '/mercado');
      }
      nav.dispose();
    },
  );
}
