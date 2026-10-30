import 'dart:convert';
import 'package:elifoot_flutter/api_client.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

void main() {
  test('012D preserves the command key when a server error follows a possible commit', () async {
    final calls = <http.Request>[];
    final api = ElifootApiClient(httpClient: MockClient((request) async {
      calls.add(request);
      return calls.length == 1 ? http.Response('{"error":"response failed"}', 500)
          : http.Response('{"success":true}', 200);
    }));
    await expectLater(api.takeBankLoan('w', 'c', 'h', 500000, 0), throwsException);
    await api.takeBankLoan('w', 'c', 'h', 500000, 0);
    expect(calls[0].url.queryParameters['idempotencyKey'], calls[1].url.queryParameters['idempotencyKey']);
  });
  test('012D reuses loan command key after a lost response and sends room version', () async {
    final calls = <http.Request>[];
    var loseResponse = true;
    final api = ElifootApiClient(httpClient: MockClient((request) async {
      if (request.method == 'GET') return http.Response('{"roundVersion":42}', 200);
      calls.add(request);
      if (loseResponse) { loseResponse = false; throw http.ClientException('lost response'); }
      return http.Response('{"success":true}', 200);
    }));
    await api.getClubFinances('w', 'c');
    await expectLater(api.takeBankLoan('w', 'c', 'h', 500000, 0), throwsA(isA<http.ClientException>()));
    await api.takeBankLoan('w', 'c', 'h', 500000, 0);
    expect(calls[0].url.queryParameters['idempotencyKey'], calls[1].url.queryParameters['idempotencyKey']);
    expect(calls[1].url.queryParameters['expectedRoundVersion'], '42');
    expect(calls[1].url.queryParameters['amount'], '500000');
    await api.takeBankLoan('w', 'c', 'h', 500000, 0);
    expect(calls[2].url.queryParameters['idempotencyKey'], isNot(calls[1].url.queryParameters['idempotencyKey']));
  });
  test('012D salary decisions and debt repayments preserve authoritative command bodies', () async {
    final calls = <http.Request>[];
    final api = ElifootApiClient(httpClient: MockClient((r) async {
      calls.add(r); return http.Response('{"success":true,"message":"RENOVAÇÃO OBRIGATÓRIA: ELENCO MÍNIMO."}', 200);
    }));
    final message = await api.respondSalaryRequest('w', 'c', 'h', 'request', false);
    expect(message, contains('OBRIGATÓRIA'));
    final decision = jsonDecode(calls[0].body) as Map<String, dynamic>;
    expect(decision['accept'], false); expect(decision['coachId'], 'h'); expect(decision['idempotencyKey'], isNotEmpty);
    await api.repayBankLoan('w', 'c', 'h', 'loan');
    expect(calls[1].url.path, '/api/worlds/w/clubs/c/loans/loan/repay');
    expect(jsonDecode(calls[1].body)['coachId'], 'h');
  });
}
