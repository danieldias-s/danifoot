import 'dart:convert';
import 'package:elifoot_flutter/api_client.dart';
import 'package:elifoot_flutter/auction_config.dart';
import 'package:elifoot_flutter/main.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'complete_gameplay_011_test.dart' show FakeGameplay011ApiClient;

class AuctionApi extends FakeGameplay011ApiClient {
  double? minimum;
  int? duration;
  String? auctionPlayer;
  @override
  Future<Map<String, dynamic>> createVoluntaryAuction(
    String w,
    String c,
    String p, {
    double minimumBid = 500000,
    int durationRounds = 2,
  }) async {
    minimum = minimumBid;
    duration = durationRounds;
    auctionPlayer = p;
    return {'success': true, 'auctionId': 'new-auction'};
  }
}

void main() {
  for (final width in [320.0, 390.0, 1366.0]) {
    testWidgets('New voluntary defaults and editable zero/three at $width', (
      tester,
    ) async {
      await tester.binding.setSurfaceSize(Size(width, 1000));
      addTearDown(() => tester.binding.setSurfaceSize(null));
      VoluntaryAuctionConfig? result;
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: Builder(
              builder: (context) => TextButton(
                onPressed: () async {
                  result = await showDialog<VoluntaryAuctionConfig>(
                    context: context,
                    builder: (_) => const AuctionConfigDialog(
                      playerName: 'Jogador de teste',
                      roundNumber: 7,
                      seasonNumber: 1,
                      totalRoundsInSeason: 22,
                    ),
                  );
                },
                child: const Text('ABRIR'),
              ),
            ),
          ),
        ),
      );
      await tester.tap(find.text('ABRIR'));
      await tester.pumpAndSettle();
      expect(
        tester
            .widget<TextFormField>(
              find.byKey(const ValueKey('auction-minimum-bid')),
            )
            .controller!
            .text,
        '500000',
      );
      expect(find.text('2 rodadas'), findsOneWidget);
      expect(
        find.text('Encerramento após conclusão da R8 / T1.'),
        findsOneWidget,
      );
      await tester.enterText(
        find.byKey(const ValueKey('auction-minimum-bid')),
        '-1',
      );
      await tester.tap(find.byKey(const ValueKey('auction-confirm')));
      await tester.pumpAndSettle();
      expect(
        find.text('Informe um valor válido maior ou igual a zero.'),
        findsOneWidget,
      );
      expect(result, isNull);
      await tester.enterText(
        find.byKey(const ValueKey('auction-minimum-bid')),
        '0',
      );
      await tester.tap(find.byKey(const ValueKey('auction-duration')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('3 rodadas').last);
      await tester.pumpAndSettle();
      expect(
        find.text('Encerramento após conclusão da R9 / T1.'),
        findsOneWidget,
      );
      await tester.tap(find.byKey(const ValueKey('auction-confirm')));
      await tester.pumpAndSettle();
      expect(result, isNull);
      expect(find.text('CONFIRMAR LEILÃO?'), findsOneWidget);
      await tester.tap(find.byKey(const ValueKey('auction-publish')));
      await tester.pumpAndSettle();
      expect(result!.minimumBid, 0);
      expect(result!.durationRounds, 3);
      expect(tester.takeException(), isNull);
      // Reopen starts from the specified defaults, not from another player's settings.
      await tester.tap(find.text('ABRIR'));
      await tester.pumpAndSettle();
      expect(find.text('2 rodadas'), findsOneWidget);
      expect(
        tester
            .widget<TextFormField>(
              find.byKey(const ValueKey('auction-minimum-bid')),
            )
            .controller!
            .text,
        '500000',
      );
    });
  }
  testWidgets(
    'Results do not reuse a completed round, and valid cents are accepted',
    (tester) async {
      VoluntaryAuctionConfig? result;
      await tester.pumpWidget(
        MaterialApp(
          home: Builder(
            builder: (context) => Scaffold(
              body: TextButton(
                onPressed: () async {
                  result = await showDialog<VoluntaryAuctionConfig>(
                    context: context,
                    builder: (_) => const AuctionConfigDialog(
                      playerName: 'P',
                      roundNumber: 22,
                      seasonNumber: 1,
                      totalRoundsInSeason: 22,
                      roundAlreadyCompleted: true,
                    ),
                  );
                },
                child: const Text('ABRIR'),
              ),
            ),
          ),
        ),
      );
      await tester.tap(find.text('ABRIR'));
      await tester.pumpAndSettle();
      expect(
        find.text('Encerramento após conclusão da R2 / T2.'),
        findsOneWidget,
      );
      await tester.enterText(
        find.byKey(const ValueKey('auction-minimum-bid')),
        '123,45',
      );
      await tester.tap(find.byKey(const ValueKey('auction-confirm')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const ValueKey('auction-publish')));
      await tester.pumpAndSettle();
      expect(result!.minimumBid, 123.45);
      expect(formatAuctionMoney(123.45), 'R\$ 123,45');
    },
  );
  testWidgets(
    'Squad voluntary action sends default settings without changing fixed-price sell',
    (tester) async {
      final api = AuctionApi();
      await tester.binding.setSurfaceSize(const Size(1366, 1000));
      addTearDown(() => tester.binding.setSurfaceSize(null));
      await tester.pumpWidget(ElifootClassicApp(apiClient: api));
      await tester.tap(find.text('ENTRAR NO LOBBY (LOCAL DEV)'));
      await tester.pumpAndSettle();
      await tester.ensureVisible(find.text('ENTRAR COM CLUBE'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('ENTRAR COM CLUBE'));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('choose-club-club-1')));
      await tester.pumpAndSettle();
      final button = find.byTooltip('Colocar em leilão').first;
      await tester.ensureVisible(button);
      await tester.tap(button);
      await tester.pumpAndSettle();
      expect(find.text('NOVO LEILÃO VOLUNTÁRIO'), findsOneWidget);
      await tester.tap(find.byKey(const ValueKey('auction-confirm')));
      await tester.pumpAndSettle();
      expect(api.minimum, isNull);
      await tester.tap(find.byKey(const ValueKey('auction-publish')));
      await tester.pumpAndSettle();
      expect(api.minimum, 500000);
      expect(api.duration, 2);
      expect(api.auctionPlayer, isNotNull);
      expect(find.byTooltip('Colocar em leilão'), findsWidgets);
      expect(tester.takeException(), isNull);
    },
  );
  test(
    'Auction API defaults, custom cents, and lost-response replay share financial command keys',
    () async {
      final requests = <http.Request>[];
      bool lose = true;
      final api = ElifootApiClient(
        httpClient: MockClient((r) async {
          if (r.method == 'GET') {
            return http.Response('{"roundVersion":7}', 200);
          }
          requests.add(r);
          if (lose) {
            lose = false;
            throw http.ClientException('lost response');
          }
          return http.Response('{"success":true,"auctionId":"a"}', 200);
        }),
      );
      await api.getClubFinances('w', 'club');
      await expectLater(
        api.createVoluntaryAuction('w', 'coach', 'player'),
        throwsA(isA<http.ClientException>()),
      );
      await api.createVoluntaryAuction('w', 'coach', 'player');
      final one = jsonDecode(requests[0].body),
          two = jsonDecode(requests[1].body);
      expect(one['minimumBid'], 500000);
      expect(one['durationRounds'], 2);
      expect(two['expectedRoundVersion'], 7);
      expect(two['idempotencyKey'], one['idempotencyKey']);
      await api.createVoluntaryAuction(
        'w',
        'coach',
        'other',
        minimumBid: 123.45,
        durationRounds: 1,
      );
      expect(jsonDecode(requests[2].body)['minimumBid'], 123.45);
      await api.placeAuctionBid('w', 'a', 'coach', 123.45);
      expect(requests[3].url.queryParameters['bidAmount'], '123.45');
    },
  );
}
