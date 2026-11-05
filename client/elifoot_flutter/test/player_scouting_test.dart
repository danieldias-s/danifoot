import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:elifoot_flutter/api_client.dart';
import 'package:elifoot_flutter/market_screen.dart';
import 'package:elifoot_flutter/player_scouting.dart';

final scoutingRows = [
  {
    'playerId': 'p1',
    'displayName': 'Daniel Dias',
    'position': 'D',
    'strength': 15,
    'clubName': 'Clube Um',
    'sellerClubName': 'Clube Um',
    'sellerClubId': 'other',
    'isListedForTransfer': true,
    'askingPrice': 5000,
  },
  {
    'playerId': 'p2',
    'displayName': 'Danilo Costa',
    'position': 'A',
    'strength': 25,
    'clubName': 'Clube Dois',
    'sellerClubName': 'Clube Dois',
    'sellerClubId': 'other',
    'isListedForTransfer': true,
    'askingPrice': 6000,
  },
  {
    'playerId': 'p3',
    'displayName': 'Daniel Gomes',
    'position': 'M',
    'strength': 35,
    'clubName': 'Clube Três',
    'isListedForTransfer': false,
  },
];

class ScoutingApi extends ElifootApiClient {
  final watched = <String>{};
  final searches = <String>[];
  bool failSave = false;
  @override
  Future<Map<String, dynamic>> getPlayerScouting(
    String worldId,
    String coachId, {
    String name = '',
    bool observedOnly = false,
  }) async {
    searches.add(name);
    return {
      'observedPlayerIds': watched.toList(),
      'players': scoutingRows
          .where(
            (p) =>
                (!observedOnly || watched.contains(p['playerId'])) &&
                (name.length >= 3
                    ? '${p['displayName']}'.toLowerCase().contains(
                        name.toLowerCase(),
                      )
                    : observedOnly && name.isEmpty),
          )
          .toList(),
    };
  }

  @override
  Future<List<String>> setPlayerObservation(
    String worldId,
    String coachId,
    String playerId,
    bool observed,
  ) async {
    if (failSave) throw Exception('offline');
    if (observed) {
      watched.add(playerId);
    } else {
      watched.remove(playerId);
    }
    return watched.toList();
  }
}

Widget scoutingMarket(ScoutingApi api, MarketPreferences pref, bool classic) =>
    MaterialApp(
      home: Scaffold(
        body: MarketScreen(
          api: api,
          worldId: 'world',
          coachId: 'coach',
          clubId: 'mine',
          snapshot: const {'roundState': 'PREPARING'},
          data: {
            'listings': scoutingRows.take(2).toList(),
            'auctions': [
              for (final p in scoutingRows.take(2))
                {
                  ...p,
                  'auctionId': 'a-${p['playerId']}',
                  'minimumBid': 5000,
                  'currentBid': 5000,
                  'isClosed': false,
                },
            ],
          },
          squad: const [],
          preferences: pref,
          refresh: () async {},
          compactClassic: classic,
        ),
      ),
    );

void main() {
  test(
    'Faixas inclusivas, sobrepostas e combinadas com posição, nome e observados',
    () {
      for (final range in scoutingStrengthRanges.skip(1)) {
        final edges = range.split('-').map(int.parse).toList();
        expect(matchesScoutingStrength(edges[0], range), isTrue);
        expect(matchesScoutingStrength(edges[1], range), isTrue);
        expect(matchesScoutingStrength(edges[0] - 1, range), isFalse);
        expect(matchesScoutingStrength(edges[1] + 1, range), isFalse);
      }
      expect(
        orderedMarketPlayers(
          scoutingRows,
          auction: false,
          position: 'D',
          order: 'STRENGTH_DESC',
          name: 'Dan',
          strengthRange: '5-15',
          observedPlayerIds: {'p1'},
        ).single['playerId'],
        'p1',
      );
      expect(
        orderedMarketPlayers(
          scoutingRows,
          auction: true,
          position: 'ALL',
          order: 'STRENGTH_DESC',
          strengthRange: '20-30',
          observedPlayerIds: {'p1'},
        ),
        isEmpty,
      );
    },
  );

  test('Falha ao salvar não inventa observação confirmada', () async {
    final api = ScoutingApi()..failSave = true;
    final observations = PlayerObservationController();
    await observations.load(api, 'world', 'coach');
    await observations.toggle(api, 'world', 'coach', 'p1');
    expect(observations.ids, isEmpty);
    expect(observations.error, isNotNull);
    expect(observations.busyIds, isEmpty);
  });

  for (final classic in [true, false]) {
    for (final width in [390.0, 1366.0]) {
      testWidgets(
        'Mercado e busca observam e restauram jogador classic=$classic width=$width',
        (t) async {
          t.view.physicalSize = Size(width, 1000);
          t.view.devicePixelRatio = 1;
          addTearDown(t.view.resetPhysicalSize);
          addTearDown(t.view.resetDevicePixelRatio);
          final api = ScoutingApi();
          final pref = MarketPreferences()..tab = 1;
          await t.pumpWidget(scoutingMarket(api, pref, classic));
          await t.pumpAndSettle();
          final star = find.byKey(const ValueKey('observe-p1'));
          await t.ensureVisible(star);
          await t.tap(star);
          await t.pumpAndSettle();
          expect(api.watched, {'p1'});
          final filter = find.byKey(const ValueKey('market-observed-only-1'));
          await t.ensureVisible(filter);
          await t.tap(filter);
          await t.pumpAndSettle();
          expect(find.text('Danilo Costa'), findsNothing);
          final search = find.text('PROCURAR JOGADORES');
          await t.ensureVisible(search);
          await t.tap(search);
          await t.pumpAndSettle();
          await t.enterText(find.byKey(const Key('scouting-name')), 'Da');
          await t.pump(const Duration(milliseconds: 400));
          expect(api.searches, isNot(contains('Da')));
          expect(find.text('Daniel Gomes'), findsNothing);
          await t.enterText(find.byKey(const Key('scouting-name')), 'Dan');
          await t.pump(const Duration(milliseconds: 400));
          await t.pumpAndSettle();
          expect(find.text('Daniel Dias'), findsOneWidget);
          expect(find.text('Danilo Costa'), findsOneWidget);
          expect(find.text('Daniel Gomes'), findsOneWidget);
          expect(find.textContaining('Fora do mercado'), findsOneWidget);
          await t.tap(find.byKey(const ValueKey('observe-p3')));
          await t.pumpAndSettle();
          expect(api.watched, {'p1', 'p3'});
          await t.pumpWidget(const SizedBox());
          final restored = MarketPreferences()..discovery = true;
          await t.pumpWidget(scoutingMarket(api, restored, classic));
          await t.pumpAndSettle();
          expect(find.text('JOGADORES OBSERVADOS'), findsOneWidget);
          expect(find.text('Daniel Dias'), findsOneWidget);
          expect(find.text('Daniel Gomes'), findsOneWidget);
          expect(find.text('Danilo Costa'), findsNothing);
          await t.tap(find.byKey(const ValueKey('observe-p1')));
          await t.pumpAndSettle();
          expect(find.text('Daniel Dias'), findsNothing);
          expect(api.watched, {'p3'});
          expect(t.takeException(), isNull);
          await t.pumpWidget(const SizedBox());
        },
      );
    }
  }
}
