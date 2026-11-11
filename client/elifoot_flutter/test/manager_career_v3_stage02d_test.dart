import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:elifoot_flutter/api_client.dart';
import 'package:elifoot_flutter/coach_finance_screens.dart';
import 'package:elifoot_flutter/club_theme.dart';

void main() {
  test(
    'Stage 02D API client attaches idempotencyKey and expectedRoundVersion on accept/reject invitation and retains key across 500 retry',
    () async {
      final requests = <http.Request>[];
      int call = 0;
      final api = ElifootApiClient(
        httpClient: MockClient((request) async {
          requests.add(request);
          if (request.url.path.endsWith('/invitations/coach-01')) {
            return http.Response(jsonEncode({'roundVersion': 14, 'invitations': []}), 200);
          }
          call++;
          if (call == 1) {
            return http.Response('{"error":"Transient persistence failure"}', 500);
          }
          return http.Response('{"success":true}', 200);
        }),
      );

      await api.getInvitations('w-02d', 'coach-01');
      await expectLater(
        () => api.acceptInvitation('w-02d', 'inv-accept-1', 'coach-01'),
        throwsException,
      );
      await api.acceptInvitation('w-02d', 'inv-accept-1', 'coach-01');
      await api.rejectInvitation('w-02d', 'inv-reject-2', 'coach-01');

      expect(requests, hasLength(4));
      expect(
        requests[1].url.path,
        '/api/worlds/w-02d/invitations/inv-accept-1/accept',
      );
      expect(requests[1].url.queryParameters['coachId'], 'coach-01');
      expect(requests[1].url.queryParameters['expectedRoundVersion'], '14');
      expect(requests[1].url.queryParameters['idempotencyKey'], isNotEmpty);
      expect(
        requests[2].url.queryParameters['idempotencyKey'],
        requests[1].url.queryParameters['idempotencyKey'],
      );

      expect(
        requests[3].url.path,
        '/api/worlds/w-02d/invitations/inv-reject-2/reject',
      );
      expect(requests[3].url.queryParameters['coachId'], 'coach-01');
      expect(requests[3].url.queryParameters['expectedRoundVersion'], '14');
      expect(requests[3].url.queryParameters['idempotencyKey'], isNotEmpty);
    },
  );

  test(
    'Stage 02D typed CoachCareerSnapshotDto payload decodes employmentHistory, moraleHistory, titles, and invitations',
    () async {
      final api = ElifootApiClient(
        httpClient: MockClient(
          (request) async => http.Response(
            jsonEncode({
              'invitations': [
                {
                  'invitationId': 'inv-1',
                  'coachId': 'c-1',
                  'clubId': 'club-2',
                  'clubName': 'Joinville Tricolor',
                  'divisionTier': 'SegundaDivisao',
                  'divisionDisplay': '2ª Divisão',
                  'clubBudget': 8500000,
                  'squadSize': 18,
                  'createdAtSeason': 2,
                  'createdAtRound': 4,
                  'isAccepted': false,
                  'isRejected': false,
                  'tablePosition': 3,
                  'squadStrength': 34.5,
                  'stadiumCapacity': 25000,
                  'objective': 'Buscar acesso e estabilidade',
                  'projectImprovement': 11.4,
                },
              ],
              'employmentHistory': [
                {
                  'clubId': 'club-1',
                  'clubName': 'Ferroviária',
                  'startSeason': 1,
                  'startRound': 1,
                  'endSeason': 2,
                  'endRound': 3,
                  'reasonForLeaving': 'RED_MORALE_LOSS',
                },
              ],
              'morale': 62,
              'moraleBand': 'VERDE',
              'moraleHistory': [
                {
                  'season': 2,
                  'round': 4,
                  'matchId': null,
                  'before': 56,
                  'after': 62,
                  'reason': 'UNEMPLOYED_RECOVERY',
                },
              ],
              'currentClubId': null,
              'currentProjectScore': 0,
              'careerRules': {
                'greenMinimum': 60,
                'yellowMinimum': 30,
                'unemployedRecoveryPerRound': 6,
              },
              'titles': [
                {
                  'season': 1,
                  'division': 'QuartaDivisao',
                  'clubId': 'club-1',
                  'coachId': 'c-1',
                  'titleType': 'LEAGUE',
                },
              ],
              'roundVersion': 27,
            }),
            200,
          ),
        ),
      );

      final snapshot = await api.getInvitations('w-02d', 'c-1');
      expect(snapshot['morale'], 62);
      expect(snapshot['moraleBand'], 'VERDE');
      expect(snapshot['roundVersion'], 27);
      expect((snapshot['invitations'] as List).single['clubName'], 'Joinville Tricolor');
      expect((snapshot['employmentHistory'] as List).single['reasonForLeaving'], 'RED_MORALE_LOSS');
      expect((snapshot['titles'] as List).single['titleType'], 'LEAGUE');
    },
  );

  testWidgets(
    'Stage 02D ClassicCoachScreen switches between Ranking and Career tabs cleanly on 1024px desktop layout',
    (WidgetTester tester) async {
      await tester.binding.setSurfaceSize(const Size(1280, 900));
      addTearDown(() => tester.binding.setSurfaceSize(null));

      Map<String, dynamic>? selectedCoach;
      final ranking = [
        {
          'rank': 1,
          'coachId': 'h-1',
          'name': 'Tecnico Campeao',
          'isHuman': true,
          'currentClubName': 'Palestra Verde',
          'leagueTitles': 2,
          'cupTitles': 1,
          'coachPoints': 84.5,
          'totalWins': 22,
          'totalDraws': 6,
          'totalLosses': 4,
        },
        {
          'rank': 2,
          'coachId': 'ai-1',
          'name': 'Caio Mendes',
          'isHuman': false,
          'currentClubName': 'Sem clube',
          'leagueTitles': 1,
          'cupTitles': 0,
          'coachPoints': 41.0,
          'totalWins': 12,
          'totalDraws': 5,
          'totalLosses': 8,
        },
      ];

      await tester.pumpWidget(
        MaterialApp(
          theme: ClassicTheme.derive().material(),
          home: Scaffold(
            body: ClassicCoachScreen(
              ranking: ranking,
              onCoach: (c) => selectedCoach = c,
              career: const Text('DETALHE DA CARREIRA E PROPOSTAS 02D'),
            ),
          ),
        ),
      );

      expect(find.text('Tecnico Campeao'), findsOneWidget);
      expect(find.text('84.5'), findsOneWidget);

      await tester.tap(find.text('Tecnico Campeao'));
      await tester.pump();
      expect(selectedCoach?['coachId'], 'h-1');

      await tester.tap(find.byKey(const Key('coach-tab-career')));
      await tester.pump();
      expect(find.text('DETALHE DA CARREIRA E PROPOSTAS 02D'), findsOneWidget);
    },
  );
}
