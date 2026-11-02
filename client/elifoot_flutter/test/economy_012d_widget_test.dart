import 'package:elifoot_flutter/main.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'complete_gameplay_011_test.dart' show FakeGameplay011ApiClient;

class EconomyApi extends FakeGameplay011ApiClient {
  bool legacy = false;
  bool paid = false;
  bool accepted = false;
  @override
  Future<Map<String, dynamic>> getClubFinances(String worldId, String clubId) async => {
    ...await super.getClubFinances(worldId, clubId),
    'availableCash': 123456,
    'annualSponsorshipBudget': 185000,
    'sponsorshipInstallments': 22,
    'credit': {'activeLoans': paid ? 0 : 1, 'loanLimit': 2, 'availableLoans': paid ? 2 : 1,
      'outstandingDebt': paid ? 0 : 525000, 'interestPerMatch': paid ? 0 : 25000},
    'loans': [{'id': 'loan', 'mode': legacy ? 'LegacyInstallment' : 'ClassicFixed', 'principalAmount': 500000,
      'interestRatePerRound': 0.015, 'installmentPerRound': 28750,
      'remainingBalance': legacy ? 8654 : 525000, 'outstandingPrincipal': legacy ? null : 500000, 'unpaidInterest': legacy ? 0 : 25000,
      'interestPerMatch': 25000, 'interestPaid': 50000, 'isPaidOff': paid}],
    'salaryRequests': [{'id': 'request', 'playerId': 'p-1', 'status': accepted ? 'ACCEPTED' : 'PENDING',
      'reason': 'FORCE_PROGRESS', 'mandatory': true, 'currentSalary': 1000, 'requestedSalary': 1100,
      'differencePerMatch': 100, 'totalSalaryCommitment': 22000, 'renewalMatches': 20}],
  };
  @override
  Future<void> repayBankLoan(String worldId, String clubId, String coachId, String loanId) async { paid = true; }
  @override
  Future<String?> respondSalaryRequest(String worldId, String clubId, String coachId, String requestId, bool accept) async {
    accepted = accept; return null;
  }
}

void main() {
  testWidgets('012D displays the original legacy interest rate and installment', (tester) async {
    addTearDown(() => tester.binding.setSurfaceSize(null));
    await tester.binding.setSurfaceSize(const Size(1280, 900));
    final api = EconomyApi()..legacy = true..accepted = true;
    await tester.pumpWidget(ElifootClassicApp(apiClient: api));
    await tester.tap(find.text('ENTRAR NO LOBBY (LOCAL DEV)')); await tester.pumpAndSettle();
    await tester.ensureVisible(find.text('ENTRAR COM CLUBE'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('ENTRAR COM CLUBE')); await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('choose-club-club-1'))); await tester.pumpAndSettle();
    await tester.ensureVisible(find.text('4. FINANÇAS & ESTÁDIO'));
    await tester.tap(find.text('4. FINANÇAS & ESTÁDIO')); await tester.pumpAndSettle();
    expect(find.textContaining('Empréstimo legado em prestações'), findsOneWidget);
    expect(find.textContaining('1,50% sobre o saldo; prestação R\$ 28.750'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });
  testWidgets('012D authoritative debt and salary proposals remain usable at 320 and 1280 pixels', (tester) async {
    addTearDown(() => tester.binding.setSurfaceSize(null));
    await tester.binding.setSurfaceSize(const Size(1280, 900));
    final api = EconomyApi();
    await tester.pumpWidget(ElifootClassicApp(apiClient: api));
    await tester.tap(find.text('ENTRAR NO LOBBY (LOCAL DEV)')); await tester.pumpAndSettle();
    await tester.ensureVisible(find.text('ENTRAR COM CLUBE'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('ENTRAR COM CLUBE')); await tester.pumpAndSettle();
    await tester.tap(find.byKey(const Key('choose-club-club-1'))); await tester.pumpAndSettle();
    await tester.ensureVisible(find.text('4. FINANÇAS & ESTÁDIO'));
    await tester.tap(find.text('4. FINANÇAS & ESTÁDIO')); await tester.pumpAndSettle();
    for (final width in [320.0, 1280.0]) {
      await tester.binding.setSurfaceSize(Size(width, 900)); await tester.pumpAndSettle();
      expect(tester.takeException(), isNull);
      expect(find.textContaining('1/2 empréstimos'), findsOneWidget);
      expect(find.text('PATROCÍNIO ANUAL: R\$ 185.000 em 22 parcelas'), findsOneWidget);
      expect(find.text('FOLHA SALARIAL: por partida oficial disputada.'), findsOneWidget);
      expect(find.textContaining('R\$ 525.000'), findsWidgets);
      expect(find.text('RENOVAÇÃO OBRIGATÓRIA: ELENCO MÍNIMO.'), findsOneWidget);
    }
    final refuse = find.widgetWithText(OutlinedButton, 'RECUSAR');
    expect(tester.widget<OutlinedButton>(refuse).onPressed, isNull);
    await tester.ensureVisible(find.text('ACEITAR')); await tester.tap(find.text('ACEITAR')); await tester.pumpAndSettle();
    expect(api.accepted, isTrue); expect(find.text('RENOVAÇÃO OBRIGATÓRIA: ELENCO MÍNIMO.'), findsNothing);
    await tester.ensureVisible(find.text('QUITAR')); await tester.tap(find.text('QUITAR')); await tester.pumpAndSettle();
    expect(api.paid, isTrue); expect(find.text('QUITAR'), findsNothing); expect(tester.takeException(), isNull);
  });
}
