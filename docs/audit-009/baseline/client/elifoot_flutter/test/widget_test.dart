import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:elifoot_flutter/api_client.dart';
import 'package:elifoot_flutter/main.dart';

void main() {
  testWidgets(
      'Elifoot Classic UI E2E flow: Login LocalDev, Lobby, 0/6 Ready Counter, Literal * Star Names, Global Pause & Tabs',
      (WidgetTester tester) async {
    int readyCount = 0;
    bool joined = false;
    String roundState = 'PREPARING';
    int currentMinute = 0;
    int pauseWindowsUsed = 0;
    int morale = 70;
    Map<String, dynamic>? activePauseWindow;

    Map<String, dynamic> buildMockSnapshot() {
      return {
        'gameWorldId': '11111111-1111-1111-1111-111111111111',
        'worldName': 'Campeonato Brasileiro 98',
        'seasonNumber': 1,
        'roundNumber': 1,
        'competitionType': 'League',
        'competitionLabel': 'Campeonato Nacional',
        'roundState': roundState,
        'roundVersion': 1 + readyCount,
        'currentMinute': currentMinute,
        'readyCounter': {
          'gameWorldId': '11111111-1111-1111-1111-111111111111',
          'seasonNumber': 1,
          'roundNumber': 1,
          'roundState': roundState,
          'roundVersion': 1 + readyCount,
          'readyCount': readyCount,
          'totalHumans': 6,
          'counterLabel': 'TÁTICAS ENVIADAS: $readyCount/6',
          'participants': [
            if (joined)
            {
              'coachId': 'c1',
              'nickname': 'Treinador 1',
              'clubId': 'club1',
              'clubName': 'Atlético Paulistano',
              'isUnemployed': false,
              'isReady': readyCount > 0,
              'isConnected': true,
              'isAutoMode': false,
              'morale': morale,
              'moraleBand': morale >= 60 ? 'VERDE' : morale >= 30 ? 'AMARELA' : 'VERMELHA',
              'usedSubstitutions': 0,
              'maxSubstitutions': 5,
              'usedPauseWindows': pauseWindowsUsed,
              'maxPauseWindows': 3,
            },
          ],
        },
        'activePauseWindow': activePauseWindow,
        'matches': [
          {
            'matchId': 'm1',
            'competitionType': 'League',
            'divisionTier': 'PrimeiraDivisao',
            'homeClubId': 'club1',
            'homeClubName': 'Atlético Paulistano',
            'homePrimaryColor': '#C8102E',
            'homeSecondaryColor': '#FFFFFF',
            'awayClubId': 'club2',
            'awayClubName': 'Palestra Verde',
            'awayPrimaryColor': '#006437',
            'awaySecondaryColor': '#FFFFFF',
            'homeGoals': 1,
            'awayGoals': 0,
            'minute': currentMinute,
            'isFinished': false,
            'isExtraTime': false,
            'homeFormation': '4-4-2',
            'awayFormation': '4-3-3',
            'homeSubCount': 0,
            'homeMaxSubs': 5,
            'homePauseWindowsUsed': pauseWindowsUsed,
            'homeMaxPauseWindows': 3,
            'awaySubCount': 0,
            'awayMaxSubs': 5,
            'awayPauseWindowsUsed': 0,
            'awayMaxPauseWindows': 3,
            'attendance': 24000,
            'ticketPrice': 22.0,
            'ticketRevenue': 528000.0,
            'events': [
              {
                'minute': 18,
                'eventType': 'Goal',
                'clubId': 'club1',
                'clubName': 'Atlético Paulistano',
                'description': "18' GOOOL! João Silva* marca para Atlético Paulistano! (1x0)",
              }
            ],
            'homeStarters': [
              {
                'playerId': 'p1',
                'displayName': 'João Silva*',
                'position': 'A',
                'strength': 46,
                'behaviorDisplay': 'Fair Play',
                'isStar': true,
                'yellowCardsInMatch': 0,
                'isSentOff': false,
                'isInjuredInMatch': false,
                'wasSubstitutedOut': false,
                'wasSubstitutedIn': false,
              }
            ],
            'homeBench': [
              {
                'playerId': 'p12',
                'displayName': 'Pedro Rocha',
                'position': 'A',
                'strength': 38,
                'behaviorDisplay': 'Cavalheiro',
                'isStar': false,
                'yellowCardsInMatch': 0,
                'isSentOff': false,
                'isInjuredInMatch': false,
                'wasSubstitutedOut': false,
                'wasSubstitutedIn': false,
              }
            ],
            'awayStarters': [],
            'awayBench': [],
          }
        ],
      };
    }

    final mockHttp = MockClient((http.Request req) async {
      final path = req.url.path;
      if (path.endsWith('/api/auth/local-login')) {
        return http.Response(
          jsonEncode({
            'coachId': 'c1',
            'nickname': 'Treinador 1',
            'deviceId': 'dev-local-01',
            'sessionToken': 'token-1',
            'authModeWarning': 'MODO LOCAL_DEV: Apenas para uso local.',
          }),
          200,
        );
      }
      if (path.endsWith('/api/worlds')) {
        return http.Response(
          jsonEncode([
            {
              'id': '11111111-1111-1111-1111-111111111111',
              'name': 'Campeonato Brasileiro 98',
              'inviteCode': 'ELIF98',
              'seed': 199801,
              'totalClubs': 128,
              'maxHumans': 8,
              'currentHumans': 6,
              'seasonNumber': 1,
              'roundNumber': 1,
              'roundState': roundState,
              'readyCounter': buildMockSnapshot()['readyCounter'],
            }
          ]),
          200,
        );
      }
      if (path.endsWith('/join')) {
        joined = true;
        return http.Response(
          jsonEncode({
            'coachId': 'c1',
            'nickname': 'Treinador 1',
            'clubId': 'club1',
            'isUnemployed': false,
            'isHost': true,
            'snapshot': buildMockSnapshot(),
          }),
          200,
        );
      }
      if (path.endsWith('/snapshot')) {
        return http.Response(jsonEncode(buildMockSnapshot()), 200);
      }
      if (path.endsWith('/clubs/club1')) {
        final squad = List.generate(14, (i) {
          final isGk = i == 0 || i == 11;
          final isStar = i == 1;
          return {
            'id': 'p${i + 1}',
            'clubId': 'club1',
            'clubName': 'Atlético Paulistano',
            'name': isStar ? 'João Silva' : 'Atleta ${i + 1}',
            'displayName': isStar ? 'João Silva*' : 'Atleta ${i + 1}',
            'position': isGk ? 'G' : (i < 6 ? 'D' : (i < 10 ? 'M' : 'A')),
            'strength': 40 - i,
            'behavior': 'FairPlay',
            'behaviorDisplay': 'Fair Play',
            'isStar': isStar,
            'contractRoundsRemaining': 20,
            'salaryPerRound': 8500,
            'marketPrice': 450000,
            'fairValue': 480000,
            'isListedForTransfer': false,
            'injuryRoundsRemaining': 0,
            'suspensionRoundsRemaining': 0,
            'yellowCardsAccumulated': 0,
            'seasonGoals': isStar ? 5 : 0,
            'seasonMatches': 3,
            'careerGoals': 12,
            'careerMatches': 25,
          };
        });
        return http.Response(
          jsonEncode({
            'id': 'club1',
            'name': 'Atlético Paulistano',
            'shortName': 'APA',
            'country': 'Brasil',
            'primaryColorHex': '#C8102E',
            'secondaryColorHex': '#FFFFFF',
            'divisionTier': 'PrimeiraDivisao',
            'divisionDisplay': '1ª Divisão',
            'cashBalance': 4200000,
            'reputation': 88,
            'preferredFormation': '4-4-2',
            'preferredStarterIds': List.generate(11, (i) => 'p${i + 1}'),
            'preferredBenchIds': ['p12', 'p13', 'p14'],
            'stadium': {
              'clubId': 'club1',
              'stadiumName': 'Arena Parque Sul',
              'builtCapacity': 40000,
              'usableCapacity': 38000,
              'conditionPercent': 95.0,
              'maintenanceReservePerSeason': 180000,
              'fixedCostPerMatch': 28000,
              'variableCostPerAttendee': 1.1,
              'lastCalculatedTicketPrice': 24.0,
              'lastAttendance': 25000,
              'expansionTargetSeats': 0,
              'expansionRoundsRemaining': 0,
              'renovationRoundsRemaining': 0,
            },
            'squad': squad,
            'aiSquadPlan': {
              'managerId': 'mgr1',
              'managerName': 'Auxiliar Técnico',
              'tacticalIdentity': 'Ofensivo',
              'preferredFormations': [
                {'formation': '4-3-3', 'weightPercent': 60}
              ],
              'selectedFormationForNextMatch': '4-4-2',
              'selectionReasoning': 'Formação 4-4-2 escolhida: equilíbrio tático.',
              'targetStartersByPosition': {'G': 1, 'D': 4, 'M': 4, 'A': 2},
              'targetReservesByPosition': {'G': 1, 'D': 2, 'M': 2, 'A': 2},
              'priorityGaps': ['Cobertura: reserva na posição A'],
              'maxSingleSigningBudget': 950000,
              'emergencyCashReserve': 450000,
              'surplusPlayerIds': [],
            },
          }),
          200,
        );
      }
      if (path.endsWith('/tactics')) {
        readyCount = 6;
        roundState = 'RUNNING_FIRST_HALF';
        currentMinute = 37;
        final snap = buildMockSnapshot();
        return http.Response(
          jsonEncode({
            'counter': snap['readyCounter'],
            'roundStarted': true,
          }),
          200,
        );
      }
      if (path.endsWith('/pause')) {
        roundState = 'GLOBAL_PAUSED';
        pauseWindowsUsed = 1;
        activePauseWindow = {
          'pauseWindowId': 'pw1',
          'gameWorldId': '11111111-1111-1111-1111-111111111111',
          'minute': 37,
          'pauseType': 'GLOBAL_PAUSED',
          'requestedByCoachId': 'c1',
          'requestedByCoachName': 'Treinador 1',
          'confirmedCount': 0,
          'totalHumans': 6,
          'remainingSeconds': 58,
          'confirmedCoachIds': [],
          'editedCoachIds': ['c1'],
        };
        return http.Response(jsonEncode(activePauseWindow), 200);
      }
      if (path.contains('/standings/')) {
        return http.Response(
          jsonEncode([
            {
              'position': 1,
              'clubId': 'club1',
              'clubName': 'Atlético Paulistano',
              'primaryColorHex': '#C8102E',
              'secondaryColorHex': '#FFFFFF',
              'isHumanControlled': true,
              'coachName': 'Treinador 1 (Humano)',
              'played': 3,
              'wins': 3,
              'draws': 0,
              'losses': 0,
              'goalsFor': 7,
              'goalsAgainst': 1,
              'goalDifference': 6,
              'points': 9,
              'zoneStatus': 'Campeão',
            }
          ]),
          200,
        );
      }
      if (path.endsWith('/clubs')) {
        return http.Response(jsonEncode([{'id':'club1','name':'Atlético Paulistano','divisionTier':'PrimeiraDivisao','divisionDisplay':'1ª Divisão','cashBalance':4200000,'isHumanControlled':false}]),200);
      }
      if (path.endsWith('/cup') || path.endsWith('/top-scorers') || path.endsWith('/calendar')) {
        return http.Response(jsonEncode([]), 200);
      }
      if (path.endsWith('/market')) {
        return http.Response(jsonEncode({'listings': [], 'auctions': [], 'offers': []}), 200);
      }
      if (path.endsWith('/ranking')) {
        return http.Response(jsonEncode({'ranking': [], 'seasonHistory': []}), 200);
      }
      if (path.contains('/finances')) {
        return http.Response(
          jsonEncode({
            'clubId': 'club1',
            'clubName': 'Atlético Paulistano',
            'cashBalance': 4200000,
            'renovationCostEstimate': 45000,
            'expansion5kCostEstimate': 425000,
            'ledger': [],
            'loans': [],
          }),
          200,
        );
      }
      if (path.contains('/invitations/')) {
        return http.Response(jsonEncode({'invitations': [], 'employmentHistory': []}), 200);
      }
      return http.Response(jsonEncode({}), 200);
    });

    final client = ElifootApiClient(httpClient: mockHttp);
    tester.view.physicalSize = const Size(1400, 900);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.resetPhysicalSize);

    await tester.pumpWidget(ElifootClassicApp(apiClient: client));

    // 1. Login LocalDev
    expect(find.textContaining('ELIFOOT CLASSIC MULTIPLAYER'), findsOneWidget);
    await tester.tap(find.text('ENTRAR NO LOBBY (LOCAL DEV)'));
    await tester.pumpAndSettle();

    expect(find.textContaining('MODO LOCAL_DEV'), findsOneWidget);
    expect(find.textContaining('Campeonato Brasileiro 98'), findsOneWidget);

    // 2. Join room with club
    await tester.tap(find.text('ENTRAR COM CLUBE'));
    await tester.pumpAndSettle();
    await tester.tap(find.text('Atlético Paulistano'));
    await tester.pumpAndSettle();

    // Verify 0/6 counter & literal '*' in João Silva*
    expect(find.byKey(const Key('ready-counter-label')), findsOneWidget);
    expect(find.text('TÁTICAS ENVIADAS: 0/6'), findsOneWidget);
    expect(find.text('João Silva*'), findsOneWidget);

    for (final value in [20, 50, 70]) {
      morale = value;
      client.onSnapshotUpdated?.call(buildMockSnapshot());
      await tester.pump();
      final band = value >= 60 ? 'VERDE' : value >= 30 ? 'AMARELA' : 'VERMELHA';
      expect(find.text('MORAL: $value/100 — $band'), findsOneWidget);
    }

    // 3. Click ENVIAR TÁTICA -> transitions to 6/6 and RUNNING_FIRST_HALF at minute 37'
    await tester.tap(find.byKey(const Key('btn-enviar-tatica')));
    await tester.pumpAndSettle();

    expect(find.textContaining("MINUTO 37'"), findsOneWidget);
    expect(find.textContaining('SUBSTITUIÇÕES 0/5'), findsOneWidget);
    expect(find.textContaining('PARADAS 0/3'), findsOneWidget);

    // 4. Request Global Pause (PEDIR PARADA) -> freezes room and shows PARTIDA PAUSADA POR Treinador 1
    await tester.tap(find.byKey(const Key('btn-pedir-parada')));
    await tester.pumpAndSettle();

    expect(find.textContaining('PARTIDA PAUSADA POR Treinador 1'), findsOneWidget);
    expect(find.textContaining('PARADAS 1/3'), findsOneWidget);
    expect(find.byKey(const Key('btn-confirmar-continuar')), findsOneWidget);

    client.disconnectRealtime();
  });

  testWidgets('Phone width: login, room list and create dialog fit 390 pixels', (tester) async {
    tester.view.physicalSize = const Size(390,844);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetPhysicalSize);
    final api = ElifootApiClient(httpClient: MockClient((req) async {
      if (req.url.path.endsWith('local-login')) {
        return http.Response(jsonEncode({'coachId':'phone','authModeWarning':'LOCAL_DEV'}),200);
      }
      return http.Response(jsonEncode([{'id':'phone-world','name':'Sala no telefone',
        'inviteCode':'PHONE','seasonNumber':1,'roundNumber':1,'roundState':'PREPARING','totalClubs':128}]),200);
    }));
    await tester.pumpWidget(ElifootClassicApp(apiClient: api));
    await tester.tap(find.text('ENTRAR NO LOBBY (LOCAL DEV)'));
    await tester.pumpAndSettle();
    expect(tester.takeException(), isNull);
    expect(find.textContaining('Sala no telefone'), findsOneWidget);
    await tester.ensureVisible(find.text('NOVO CAMPEONATO / SALA'));
    await tester.tap(find.text('NOVO CAMPEONATO / SALA'));
    await tester.pumpAndSettle();
    expect(tester.takeException(), isNull);
    expect(find.text('CRIAR SALA'), findsOneWidget);
  });
}
