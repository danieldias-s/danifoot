import 'dart:convert';
import 'package:elifoot_flutter/api_client.dart';
import 'package:elifoot_flutter/voluntary_renewal_dialog.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'classic_ux_014b_test.dart' show join;
import 'presentation_017a_test.dart' show GameplayPresentationApi;

class AnnualRenewalApi extends GameplayPresentationApi {
  int confirmations = 0, quotes = 0;
  String? confirmedToken;
  final renewed = <String>{};
  @override
  Future<Map<String, dynamic>> getClubDetail(String w, String c) async {
    final detail = await super.getClubDetail(w, c);
    return {
      ...detail,
      'squad': [
        for (final raw in detail['squad'] as List)
          {
            ...raw as Map,
            'seasonContractsEnabled': true,
            'salaryPerRound': 100,
            'contractRenewedForSeason': renewed.contains('${raw['id']}'),
            'contractThroughSeason': 1,
          },
      ],
    };
  }

  @override
  Future<Map<String, dynamic>> getRenewalContractPreview(
    String w,
    String p,
    String c,
  ) async {
    quotes++;
    return {
      'currentSalary': 100,
      'requiredSalary': 110,
      'signingFee': 330,
      'season': 1,
      'requiresRaise': true,
      'termsToken': 'authoritative-renewal',
    };
  }

  @override
  Future<void> renewPlayerContract(
    String w,
    String p,
    String c,
    int rounds, {
    String? renewalTermsToken,
  }) async {
    confirmations++;
    confirmedToken = renewalTermsToken;
    renewed.add(p);
  }
}

void main() {
  for (final width in [320.0, 390.0, 1024.0, 1366.0]) {
    for (final raise in [false, true]) {
      testWidgets(
        'Voluntary renewal accepts or abandons without overflow $width raise=$raise',
        (t) async {
          await t.binding.setSurfaceSize(Size(width, 700));
          addTearDown(() => t.binding.setSurfaceSize(null));
          bool? result;
          await t.pumpWidget(
            MaterialApp(
              home: Builder(
                builder: (context) => Scaffold(
                  body: Center(
                    child: ElevatedButton(
                      onPressed: () async {
                        result = await showDialog<bool>(
                          context: context,
                          builder: (_) => VoluntaryRenewalDialog(
                            player: const {'name': 'Jogador do clube'},
                            preview: {
                              'currentSalary': 100,
                              'requiredSalary': raise ? 110 : 100,
                              'signingFee': 300,
                              'season': 1,
                              'requiresRaise': raise,
                            },
                          ),
                        );
                      },
                      child: const Text('ABRIR'),
                    ),
                  ),
                ),
              ),
            ),
          );
          await t.tap(find.text('ABRIR'));
          await t.pumpAndSettle();
          expect(find.textContaining('não gera leilão'), findsOneWidget);
          expect(find.textContaining('Salário exigido'), findsOneWidget);
          await t.tap(find.text('DESISTIR'));
          await t.pumpAndSettle();
          expect(result, isFalse);
          expect(t.takeException(), isNull);
          await t.tap(find.text('ABRIR'));
          await t.pumpAndSettle();
          await t.tap(find.byKey(const Key('confirm-season-renewal')));
          await t.pumpAndSettle();
          expect(result, isTrue);
          expect(t.takeException(), isNull);
        },
      );
    }
  }

  for (final accept in [false, true]) {
    testWidgets(
      'Real squad renewal forwards only explicit consent accept=$accept',
      (t) async {
        await t.binding.setSurfaceSize(const Size(1366, 1000));
        addTearDown(() => t.binding.setSurfaceSize(null));
        final api = AnnualRenewalApi();
        await join(t, api);
        final action = find.byTooltip('Renovar contrato').first;
        await t.ensureVisible(action);
        await t.tap(action);
        await t.pumpAndSettle();
        expect(find.text('RENOVAÇÃO VOLUNTÁRIA'), findsOneWidget);
        expect(find.text('ACEITAR EXIGÊNCIA'), findsOneWidget);
        expect(api.quotes, 1);
        expect(api.confirmations, 0);
        await t.tap(
          accept ? find.text('ACEITAR EXIGÊNCIA') : find.text('DESISTIR'),
        );
        await t.pumpAndSettle();
        expect(api.confirmations, accept ? 1 : 0);
        expect(api.confirmedToken, accept ? 'authoritative-renewal' : isNull);
        expect(find.text('PEDIDO DE AUMENTO SALARIAL'), findsNothing);
        expect(t.takeException(), isNull);
        await t.pumpWidget(const SizedBox());
        api.disconnectRealtime();
      },
    );
  }

  test(
    'Renewal transport uses server terms and preserves the key after save failure',
    () async {
      final requests = <http.Request>[];
      int writes = 0;
      final api = ElifootApiClient(
        baseUrl: 'http://example.test',
        httpClient: MockClient((request) async {
          requests.add(request);
          if (request.method == 'GET') {
            return http.Response(
              jsonEncode({
                'currentSalary': 100,
                'requiredSalary': 110,
                'signingFee': 330,
                'termsToken': 'server-renewal',
                'roundVersion': 9,
              }),
              200,
            );
          }
          writes++;
          return http.Response(
            writes == 1
                ? '{"code":"CONTRACT_PERSISTENCE_FAILED"}'
                : '{"success":true}',
            writes == 1 ? 500 : 200,
          );
        }),
      );
      final quote = await api.getRenewalContractPreview('w', 'p', 'coach');
      expect(requests.length, 1);
      expect(writes, 0);
      await expectLater(
        api.renewPlayerContract(
          'w',
          'p',
          'coach',
          0,
          renewalTermsToken: quote['termsToken'],
        ),
        throwsException,
      );
      await api.renewPlayerContract(
        'w',
        'p',
        'coach',
        0,
        renewalTermsToken: quote['termsToken'],
      );
      final posts = requests.where((r) => r.method == 'POST').toList();
      expect(
        posts[0].url.queryParameters['renewalTermsToken'],
        'server-renewal',
      );
      expect(posts[0].url.queryParameters['expectedRoundVersion'], '9');
      expect(
        posts[1].url.queryParameters['idempotencyKey'],
        posts[0].url.queryParameters['idempotencyKey'],
      );
      expect(posts[0].url.queryParameters.containsKey('salary'), isFalse);
    },
  );
}
