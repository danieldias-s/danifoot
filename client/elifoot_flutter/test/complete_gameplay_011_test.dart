import 'package:elifoot_flutter/api_client.dart';
import 'package:elifoot_flutter/main.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

/// Fake API client for comprehensive Task 011 gameplay UI & functional validation.
class FakeGameplay011ApiClient extends ElifootApiClient {
  FakeGameplay011ApiClient() : super(baseUrl: 'http://localhost:5000');

  String currentRoundState = 'PREPARING';
  int currentRound = 1;
  int currentSeason = 1;
  int roundVersion = 1;
  bool isReady = false;
  bool hasJoined = false;
  bool isUnemployed = false;
  String myClubId = 'club-1';
  String myClubName = 'Atlético Verde';
  int morale = 75;
  String moraleBand = 'VERDE';
  int usedSubs = 0;
  int usedPauses = 0;
  int confirmedPauseCount = 0;
  double cashBalance = 1850000;
  int builtCapacity = 20000;
  int usableCapacity = 18000;
  int conditionPercent = 90;

  @override
  void connectRealtimeRoom(String worldId, String coachId) {}

  @override
  void disconnectRealtime() {}

  @override
  Future<Map<String, dynamic>> localLogin(
    String nickname,
    String deviceId,
  ) async {
    return {
      'coachId': 'coach-1',
      'nickname': nickname,
      'deviceId': deviceId,
      'authModeWarning': 'MODO LOCAL_DEV: Autenticação simplificada.',
    };
  }

  @override
  Future<List<dynamic>> listWorlds() async {
    return [
      {
        'id': 'world-011',
        'name': 'Campeonato Oficial 011',
        'inviteCode': 'ELI011',
        'seasonNumber': currentSeason,
        'roundNumber': currentRound,
        'roundState': currentRoundState,
        'totalClubs': 128,
        'readyCounter': _buildReadyCounter(),
      },
    ];
  }

  @override
  Future<List<dynamic>> getClubs(String worldId, {String? tier}) async {
    return [
      {
        'id': 'club-1',
        'name': 'Atlético Verde',
        'divisionTier': 'PrimeiraDivisao',
        'divisionDisplay': '1ª Divisão',
        'cashBalance': cashBalance,
        'primaryColorHex': '#00AA44',
        'secondaryColorHex': '#FFD700',
        'isHumanControlled': false,
      },
      {
        'id': 'club-2',
        'name': 'Real Dourado',
        'divisionTier': 'PrimeiraDivisao',
        'divisionDisplay': '1ª Divisão',
        'cashBalance': 2100000,
        'primaryColorHex': '#FFD700',
        'secondaryColorHex': '#000000',
        'isHumanControlled': false,
      },
    ];
  }

  @override
  Future<Map<String, dynamic>> joinWorld({
    required String worldId,
    required String coachId,
    required String nickname,
    required String deviceId,
    String? preferredClubId,
    bool joinUnemployed = false,
  }) async {
    hasJoined = true;
    isUnemployed = joinUnemployed;
    myClubId = joinUnemployed ? '' : (preferredClubId ?? 'club-1');
    myClubName = joinUnemployed ? 'Sem Clube' : 'Atlético Verde';
    return {
      'worldId': worldId,
      'clubId': isUnemployed ? null : myClubId,
      'isUnemployed': isUnemployed,
      'snapshot': await getSnapshot(worldId),
    };
  }

  Map<String, dynamic> _buildReadyCounter() {
    return {
      'worldId': 'world-011',
      'seasonNumber': currentSeason,
      'roundNumber': currentRound,
      'roundVersion': roundVersion,
      'readyCount': isReady ? 1 : 0,
      'totalHumans': 4,
      'counterLabel': 'TÁTICAS ENVIADAS: ${isReady ? 1 : 0}/4',
      'participants': [
        if (hasJoined)
          {
            'coachId': 'coach-1',
            'nickname': 'Treinador 1',
            'clubId': isUnemployed ? null : myClubId,
            'clubName': isUnemployed ? 'Sem Clube' : myClubName,
            'isUnemployed': isUnemployed,
            'isReady': isReady,
            'isConnected': true,
            'morale': morale,
            'moraleBand': moraleBand,
            'usedSubstitutions': usedSubs,
            'maxSubstitutions': 5,
            'usedPauseWindows': usedPauses,
            'maxPauseWindows': 3,
          },
        {
          'coachId': 'coach-2',
          'nickname': 'Treinador 2',
          'clubId': 'club-2',
          'clubName': 'Real Dourado',
          'isUnemployed': false,
          'isReady': true,
          'isConnected': true,
          'morale': 80,
          'moraleBand': 'VERDE',
          'usedSubstitutions': 1,
          'maxSubstitutions': 5,
          'usedPauseWindows': 0,
          'maxPauseWindows': 3,
        },
      ],
    };
  }

  @override
  Future<Map<String, dynamic>> getSnapshot(String worldId) async {
    return {
      'worldId': worldId,
      'worldName': 'Campeonato Oficial 011',
      'inviteCode': 'ELI011',
      'seasonNumber': currentSeason,
      'roundNumber': currentRound,
      'roundVersion': roundVersion,
      'competitionLabel': 'Liga • 1ª Divisão',
      'roundState': currentRoundState,
      'currentMinute': currentRoundState == 'PREPARING'
          ? 0
          : (currentRoundState == 'RESULTS' ? 90 : 42),
      'readyCounter': _buildReadyCounter(),
      'activePauseWindow':
          (currentRoundState == 'GLOBAL_PAUSED' ||
              currentRoundState == 'HALFTIME_PAUSED')
          ? {
              'requestedByCoachId': 'coach-1',
              'requestedByCoachName': 'Treinador 1',
              'minute': 42,
              'remainingSeconds': 55,
              'confirmedCount': confirmedPauseCount,
              'totalHumans': 4,
            }
          : null,
      'matches': currentRoundState == 'PREPARING'
          ? []
          : [
              {
                'matchId': 'match-1',
                'homeClubId': 'club-1',
                'homeClubName': 'Atlético Verde',
                'awayClubId': 'club-2',
                'awayClubName': 'Real Dourado',
                'homeGoals': 2,
                'awayGoals': 1,
                'minute': currentRoundState == 'RESULTS' ? 90 : 42,
                'isFinished': currentRoundState == 'RESULTS',
                'homeFormation': '4-4-2',
                'awayFormation': '4-3-3',
                'homeSubCount': usedSubs,
                'homeMaxSubs': 5,
                'awaySubCount': 1,
                'awayMaxSubs': 5,
                'homePauseWindowsUsed': usedPauses,
                'homeMaxPauseWindows': 3,
                'awayPauseWindowsUsed': 0,
                'awayMaxPauseWindows': 3,
                'homeStarters': List.generate(
                  11,
                  (i) => {
                    'playerId': 'p-${i + 1}',
                    'displayName': i == 0
                        ? 'Carlos Muralha*'
                        : 'Titular ${i + 1}',
                    'position': i == 0
                        ? 'G'
                        : i <= 4
                        ? 'D'
                        : i <= 8
                        ? 'M'
                        : 'A',
                    'strength': 44 - i,
                    'isSentOff': false,
                  },
                ),
                'homeBench': List.generate(
                  5,
                  (i) => {
                    'playerId': 'p-${i + 12}',
                    'displayName': 'Reserva ${i + 12}',
                    'position': i == 0 ? 'G' : 'M',
                    'strength': 32 - i,
                  },
                ),
                'awayStarters': const [],
                'awayBench': const [],
                'events': [
                  {
                    'minute': 12,
                    'type': 'Goal',
                    'description': "12' GOL de Titular 10 (Atlético Verde)!",
                  },
                  {
                    'minute': 28,
                    'type': 'YellowCard',
                    'description': "28' CARTÃO AMARELO para Titular 3.",
                  },
                  {
                    'minute': 39,
                    'type': 'Goal',
                    'description': "39' GOL de Titular 11 (Atlético Verde)!",
                  },
                ],
              },
            ],
    };
  }

  @override
  Future<Map<String, dynamic>> getClubDetail(
    String worldId,
    String clubId,
  ) async {
    final squad = <Map<String, dynamic>>[];
    final positions = [
      'G',
      'D',
      'D',
      'D',
      'D',
      'M',
      'M',
      'M',
      'M',
      'A',
      'A',
      'G',
      'D',
      'M',
      'M',
      'A',
      'D',
      'M',
    ];
    for (var i = 0; i < positions.length; i++) {
      squad.add({
        'id': 'p-${i + 1}',
        'name': i == 0
            ? 'Carlos Muralha'
            : (i == 9 ? 'João Silva' : 'Atleta ${i + 1}'),
        'displayName': i == 0
            ? 'Carlos Muralha*'
            : (i == 9 ? 'João Silva*' : 'Atleta ${i + 1}'),
        'position': positions[i],
        'strength': 46 - i,
        'isStar': i == 0 || i == 9,
        'behaviorDisplay': i % 2 == 0 ? 'Fair Play' : 'Cavalheiro',
        'seasonGoals': i == 9 ? 14 : (i == 10 ? 7 : 1),
        'careerGoals': i == 9 ? 55 : 10,
        'contractRoundsRemaining': 18,
        'salaryPerRound': 12000 + (i * 500),
        'fairValue': 450000 - (i * 10000),
        'injuryRoundsRemaining': i == 16 ? 3 : 0,
        'suspensionRoundsRemaining': i == 17 ? 1 : 0,
        'photoUrl': null,
      });
    }
    return {
      'id': clubId,
      'name': myClubName,
      'divisionDisplay': '1ª Divisão',
      'cashBalance': cashBalance,
      'preferredFormation': '4-4-2',
      'preferredStarterIds': List.generate(11, (i) => 'p-${i + 1}'),
      'preferredBenchIds': List.generate(5, (i) => 'p-${i + 12}'),
      'stadium': {
        'stadiumName': 'Arena Clássica 98',
        'builtCapacity': builtCapacity,
        'usableCapacity': usableCapacity,
        'conditionPercent': conditionPercent,
        'lastCalculatedTicketPrice': 25,
        'lastAttendance': 16400,
        'fixedCostPerMatch': 18000,
      },
      'squad': squad,
      'aiSquadPlan': {
        'managerName': 'Comissão Técnica Clássica',
        'tacticalIdentity': 'Equilibrado',
        'selectionReasoning': 'Priorizando os 11 atletas de maior força aptos.',
        'priorityGaps': ['Lateral reserva', 'Atacante velocista'],
      },
    };
  }

  @override
  Future<List<dynamic>> getStandings(
    String worldId,
    String divisionTier,
  ) async {
    return List.generate(
      8,
      (i) => {
        'position': i + 1,
        'clubId': i == 0 ? 'club-1' : 'club-${i + 1}',
        'clubName': i == 0
            ? 'Atlético Verde'
            : (i == 1 ? 'Real Dourado' : 'Clube Div ${i + 1}'),
        'coachName': i == 0 ? 'Treinador 1' : 'Técnico IA ${i + 1}',
        'points': 30 - (i * 3),
        'played': 12,
        'wins': 9 - i,
        'draws': 3,
        'losses': i,
        'goalsFor': 24 - i,
        'goalsAgainst': 8 + i,
        'goalDifference': 16 - (i * 2),
        'zoneStatus': i == 0
            ? 'Líder / Campeão'
            : (i == 1
                  ? 'Acesso'
                  : (i >= 6 ? 'Zona de Rebaixamento' : 'Permanência')),
      },
    );
  }

  @override
  Future<List<dynamic>> getCupFixtures(String worldId) async {
    // Generate 30+ cup fixtures across multiple stages to prove the .take(24) truncation bug is fixed!
    final list = <Map<String, dynamic>>[];
    for (var i = 1; i <= 26; i++) {
      list.add({
        'stage': '1ª Eliminatória Distrital',
        'homeClubName': 'Distrital A$i',
        'homeDivision': 'Distrital',
        'awayClubName': 'Distrital B$i',
        'awayDivision': 'Distrital',
        'homeGoals': 2,
        'awayGoals': 1,
        'isFinished': true,
        'winnerClubName': 'Distrital A$i',
      });
    }
    list.addAll([
      {
        'stage': 'Semifinal',
        'homeClubName': 'Atlético Verde',
        'homeDivision': '1ª Divisão',
        'awayClubName': 'Real Dourado',
        'awayDivision': '1ª Divisão',
        'homeGoals': 1,
        'awayGoals': 1,
        'homePenaltyGoals': 5,
        'awayPenaltyGoals': 4,
        'isFinished': true,
        'winnerClubName': 'Atlético Verde',
      },
      {
        'stage': 'Final',
        'homeClubName': 'Atlético Verde',
        'homeDivision': '1ª Divisão',
        'awayClubName': 'União Imperial',
        'awayDivision': '1ª Divisão',
        'homeGoals': 3,
        'awayGoals': 1,
        'isFinished': true,
        'winnerClubName': 'Atlético Verde',
      },
    ]);
    return list;
  }

  @override
  Future<List<dynamic>> getTopScorers(String worldId) async {
    return [
      {
        'rank': 1,
        'displayName': 'João Silva*',
        'clubName': 'Atlético Verde',
        'seasonGoals': 14,
        'cupGoals': 5,
      },
    ];
  }

  @override
  Future<Map<String, dynamic>> getMarket(String worldId) async {
    return {
      'listings': [
        {
          'playerId': 'mkt-1',
          'displayName': 'Rivaldo Craque*',
          'position': 'M',
          'strength': 45,
          'behaviorDisplay': 'Fair Play',
          'sellerClubName': 'Real Dourado',
          'askingPrice': 600000,
          'fairValue': 580000,
          'salaryPerRound': 1234,
        },
        {
          'playerId': 'mkt-2',
          'displayName': 'Zagueiro Firme',
          'position': 'D',
          'strength': 36,
          'behaviorDisplay': 'Cavalheiro',
          'sellerClubName': 'União Imperial',
          'askingPrice': 240000,
          'fairValue': 250000,
          'salaryPerRound': 987,
        },
      ],
      'auctions': [
        {
          'auctionId': 'auc-1',
          'displayName': 'Goleiro Livre',
          'position': 'G',
          'strength': 40,
          'minimumBid': 150000,
          'currentBid': 165000,
          'highestBidderClubName': 'Real Dourado',
          'isClosed': false,
        },
      ],
      'offers': [
        {
          'offerId': 'off-1',
          'playerDisplayName': 'Atleta 5',
          'strength': 38,
          'buyerClubId': 'club-2',
          'buyerClubName': 'Real Dourado',
          'sellerClubId': 'club-1',
          'sellerClubName': 'Atlético Verde',
          'offeredAmount': 350000,
          'counterAmount': 390000,
          'status': 'Countered',
        },
      ],
    };
  }

  @override
  Future<Map<String, dynamic>> getClubFinances(
    String worldId,
    String clubId,
  ) async {
    return {
      'renovationCostEstimate': 65000,
      'expansion5kCostEstimate': 425000,
      'ledger': [
        {
          'seasonNumber': 1,
          'roundNumber': 1,
          'description': 'Bilheteria do jogo em casa',
          'amount': 410000,
        },
        {
          'seasonNumber': 1,
          'roundNumber': 1,
          'description': 'Folha de salários da rodada',
          'amount': -195000,
        },
        {
          'seasonNumber': 1,
          'roundNumber': 1,
          'description': 'Premiação da Taça Nacional',
          'amount': 250000,
        },
      ],
    };
  }

  @override
  Future<Map<String, dynamic>> getRankingAndHistory(String worldId) async {
    return {
      'ranking': [
        {
          'rank': 1,
          'coachId': 'coach-1',
          'name': 'Treinador 1',
          'isHuman': true,
          'currentClubName': myClubName,
          'leagueTitles': 2,
          'cupTitles': 1,
          'coachPoints': 1420,
          'totalWins': 28,
          'totalDraws': 6,
          'totalLosses': 4,
        },
      ],
      'seasonHistory': [
        {
          'seasonNumber': 1,
          'firstDivisionChampionName': 'Atlético Verde',
          'cupChampionName': 'Atlético Verde',
          'topScorerPlayerName': 'João Silva*',
          'topScorerGoals': 19,
        },
      ],
    };
  }

  @override
  Future<Map<String, dynamic>> getInvitations(
    String worldId,
    String coachId,
  ) async {
    return {
      'morale': morale,
      'moraleBand': moraleBand,
      'currentProjectScore': 68.5,
      'moraleHistory': [
        {'season': 1, 'round': 1, 'before': 70, 'after': 75, 'matchId': 'm1'},
      ],
      'employmentHistory': [
        {
          'clubName': 'Guarani da Serra',
          'startSeason': 1,
          'startRound': 1,
          'endSeason': 1,
          'endRound': 6,
        },
        {
          'clubName': 'Atlético Verde',
          'startSeason': 1,
          'startRound': 7,
          'endSeason': null,
          'endRound': null,
        },
      ],
      'invitations': [
        {
          'invitationId': 'inv-1',
          'clubId': 'club-2',
          'clubName': 'Real Dourado',
          'divisionDisplay': '1ª Divisão',
          'clubBudget': 2100000,
          'squadSize': 20,
          'tablePosition': 2,
          'squadStrength': 42,
          'stadiumCapacity': 25000,
          'objective': 'Disputar o título nacional',
          'projectImprovement': 14.0,
          'isAccepted': false,
        },
      ],
    };
  }

  @override
  Future<Map<String, dynamic>> getSeasonTopScorers(
    String worldId, {
    int? season,
  }) async {
    return {
      'season': season ?? 1,
      'awards': const [],
      'clubTotals': const [],
      'coverageWarnings': const [],
    };
  }

  @override
  Future<List<dynamic>> getCalendar(String worldId) async {
    return [
      {
        'roundNumber': 1,
        'competition': 'Liga Nacional',
        'fixtures': [
          {
            'home': 'Atlético Verde',
            'away': 'Real Dourado',
            'homeGoals': 2,
            'awayGoals': 1,
            'isFinished': true,
          },
        ],
      },
      {
        'roundNumber': 2,
        'competition': 'Taça Nacional',
        'fixtures': [
          {
            'home': 'União Imperial',
            'away': 'Atlético Verde',
            'homeGoals': 0,
            'awayGoals': 0,
            'isFinished': false,
          },
        ],
      },
    ];
  }

  @override
  Future<Map<String, dynamic>> submitTactics({
    required String worldId,
    required String coachId,
    required String formation,
    required List<String> starterIds,
    required List<String> benchIds,
    required int roundVersion,
  }) async {
    isReady = true;
    currentRoundState = 'RUNNING_FIRST_HALF';
    this.roundVersion++;
    return {'counter': _buildReadyCounter(), 'roundStarted': true};
  }

  @override
  Future<Map<String, dynamic>> submitUnemployedReady({
    required String worldId,
    required String coachId,
    required int roundVersion,
  }) async {
    isReady = true;
    return {'counter': _buildReadyCounter(), 'roundStarted': false};
  }

  @override
  Future<Map<String, dynamic>> requestGlobalPause(
    String worldId,
    String coachId,
  ) async {
    currentRoundState = 'GLOBAL_PAUSED';
    usedPauses = 1;
    roundVersion++;
    return await getSnapshot(worldId);
  }

  @override
  Future<Map<String, dynamic>> submitInMatchAdjustments({
    required String worldId,
    required String coachId,
    required String matchId,
    String? newFormation,
    required List<Map<String, String>> substitutions,
  }) async {
    usedSubs += substitutions.length;
    roundVersion++;
    return await getSnapshot(worldId);
  }

  @override
  Future<Map<String, dynamic>> confirmContinue(
    String worldId,
    String coachId,
  ) async {
    confirmedPauseCount = 4;
    currentRoundState = 'RESULTS';
    roundVersion++;
    return {'resumed': true, 'snapshot': await getSnapshot(worldId)};
  }

  @override
  Future<Map<String, dynamic>> advanceToNextRound(String worldId) async {
    currentRound++;
    currentRoundState = 'PREPARING';
    isReady = false;
    roundVersion++;
    return await getSnapshot(worldId);
  }

  @override
  Future<void> buyConfirmedListing(
    String worldId,
    String coachId,
    Map<String, dynamic> listing,
  ) async {}

  @override
  Future<void> buyListedPlayer(
    String worldId,
    String coachId,
    String playerId,
  ) async {}

  @override
  Future<Map<String, dynamic>> makeTransferOffer(
    String worldId,
    String coachId,
    String playerId,
    double amount,
  ) async {
    return {'status': 'Pending'};
  }

  @override
  Future<void> placeAuctionBid(
    String worldId,
    String auctionId,
    String coachId,
    double bidAmount,
  ) async {}

  @override
  Future<void> respondTransferOffer(
    String worldId,
    String offerId,
    String coachId,
    bool accept,
  ) async {}

  @override
  Future<void> startStadiumRenovation(
    String worldId,
    String clubId,
    String coachId,
  ) async {}

  @override
  Future<void> startStadiumExpansion(
    String worldId,
    String clubId,
    String coachId,
    int seats,
  ) async {}

  @override
  Future<void> takeBankLoan(
    String worldId,
    String clubId,
    String coachId,
    double amount,
    int rounds,
  ) async {}

  @override
  Future<void> renewPlayerContract(
    String worldId,
    String playerId,
    String coachId,
    int additionalRounds,
  ) async {}

  @override
  Future<void> listPlayerForTransfer(
    String worldId,
    String coachId,
    String playerId,
    double price,
  ) async {}

  @override
  Future<Map<String, dynamic>> acceptInvitation(
    String worldId,
    String invitationId,
    String coachId,
  ) async {
    isUnemployed = false;
    myClubId = 'club-2';
    myClubName = 'Real Dourado';
    roundVersion++;
    return {'accepted': true};
  }
}

void main() {
  testWidgets(
    'Task 011: Coach Main Panel, Squad Filters, Tactical Field, Swap & Live Match Flow',
    (WidgetTester tester) async {
      await tester.binding.setSurfaceSize(const Size(1280, 900));
      addTearDown(() => tester.binding.setSurfaceSize(null));

      final fakeApi = FakeGameplay011ApiClient();
      await tester.pumpWidget(ElifootClassicApp(apiClient: fakeApi));

      // 1. Login and choose club
      await tester.tap(find.text('ENTRAR NO LOBBY (LOCAL DEV)'));
      await tester.pumpAndSettle();

      await tester.tap(find.text('ENTRAR COM CLUBE'));
      await tester.pumpAndSettle();

      expect(find.text('SELECIONE SEU CLUBE'), findsOneWidget);
      expect(find.textContaining('Caixa R\$ 1.850.000'), findsOneWidget);
      await tester.tap(find.byKey(const Key('choose-club-club-1')));
      await tester.pumpAndSettle();

      // 2. Verify Coach Main Panel
      expect(find.byKey(const ValueKey('coach-main-panel')), findsOneWidget);
      expect(
        find.textContaining('PAINEL PRINCIPAL DO TREINADOR — Atlético Verde'),
        findsOneWidget,
      );
      expect(find.text('R\$ 1.850.000'), findsWidgets);
      expect(
        find.byKey(const ValueKey('tactical-field-preview')),
        findsOneWidget,
      );
      expect(find.textContaining('SETORES COMPATÍVEIS [OK]'), findsOneWidget);

      // 3. Test quick starter <-> reserve swap
      await tester.ensureVisible(
        find.byKey(const ValueKey('btn-trocar-titular-reserva')),
      );
      await tester.tap(
        find.byKey(const ValueKey('btn-trocar-titular-reserva')),
      );
      await tester.pumpAndSettle();
      expect(
        find.text('Troca realizada entre titular e reserva.'),
        findsOneWidget,
      );

      // 4. Test squad filters (Position G, then ALL)
      await tester.ensureVisible(
        find.byKey(const ValueKey('filter-squad-pos-G')),
      );
      await tester.tap(find.byKey(const ValueKey('filter-squad-pos-G')));
      await tester.pumpAndSettle();
      expect(find.text('Carlos Muralha*'), findsWidgets);

      await tester.tap(find.byKey(const ValueKey('filter-squad-pos-ALL')));
      await tester.pumpAndSettle();

      // 5. Submit tactics -> starts live match automatically
      await tester.ensureVisible(find.byKey(const Key('btn-enviar-tatica')));
      await tester.tap(find.byKey(const Key('btn-enviar-tatica')));
      await tester.pumpAndSettle();

      // 6. Verify Live Match screen & highlighted match card
      expect(
        find.byKey(const ValueKey('coach-highlighted-match-card')),
        findsOneWidget,
      );
      expect(find.textContaining('PARTIDA EM ANDAMENTO'), findsWidgets);
      await tester.ensureVisible(
        find.byKey(const ValueKey('coach-highlighted-match-card')),
      );
      await tester.tap(
        find.byKey(const ValueKey('coach-highlighted-match-card')),
      );
      await tester.pumpAndSettle();
      expect(find.textContaining('[GOL]'), findsWidgets);

      // 7. Request global pause -> opens interactive substitution panel
      await tester.tap(find.byKey(const Key('btn-pedir-parada')));
      await tester.pumpAndSettle();

      expect(
        find.byKey(const ValueKey('in-match-substitution-panel')),
        findsOneWidget,
      );
      expect(find.textContaining('RESTANTES: 5'), findsOneWidget);

      // Select starter out and reserve in via interactive chips
      await tester.ensureVisible(
        find.byKey(const ValueKey('sub-select-out-p-1')),
      );
      await tester.tap(find.byKey(const ValueKey('sub-select-out-p-1')));
      await tester.pumpAndSettle();
      await tester.ensureVisible(
        find.byKey(const ValueKey('sub-select-in-p-12')),
      );
      await tester.tap(find.byKey(const ValueKey('sub-select-in-p-12')));
      await tester.pumpAndSettle();

      await tester.ensureVisible(
        find.byKey(const ValueKey('btn-aplicar-ajuste-janela')),
      );
      await tester.tap(find.byKey(const ValueKey('btn-aplicar-ajuste-janela')));
      await tester.pumpAndSettle();

      expect(find.textContaining('RESTANTES: 4'), findsOneWidget);

      // 8. Confirm continue -> transitions to RESULTS -> advance round
      await tester.ensureVisible(
        find.byKey(const Key('btn-confirmar-continuar')),
      );
      await tester.tap(find.byKey(const Key('btn-confirmar-continuar')));
      await tester.pumpAndSettle();

      expect(find.textContaining('PARTIDA FINALIZADA'), findsWidgets);
      await tester.tap(find.byKey(const Key('btn-proxima-rodada')));
      await tester.pumpAndSettle();
      expect(find.textContaining('Rodada 2'), findsWidgets);
    },
  );

  testWidgets(
    'Task 011: Divisions, Non-Truncated Cup Stage Navigation, Market, Finances & Unemployed Flow',
    (WidgetTester tester) async {
      await tester.binding.setSurfaceSize(const Size(1280, 900));
      addTearDown(() => tester.binding.setSurfaceSize(null));

      final fakeApi = FakeGameplay011ApiClient();
      await tester.pumpWidget(ElifootClassicApp(apiClient: fakeApi));

      await tester.tap(find.text('ENTRAR NO LOBBY (LOCAL DEV)'));
      await tester.pumpAndSettle();

      // Join unemployed first to verify TREINADOR SEM CLUBE panel
      await tester.tap(find.text('ENTRAR DESEMPREGADO'));
      await tester.pumpAndSettle();

      expect(
        find.byKey(const ValueKey('coach-unemployed-panel')),
        findsOneWidget,
      );
      expect(find.text('TREINADOR SEM CLUBE'), findsWidgets);
      expect(
        find.text(
          'VOCÊ ESTÁ DESEMPREGADO NO MOMENTO — Acompanhe a rodada ou aceite um convite para voltar à ativa.',
        ),
        findsOneWidget,
      );

      // Accept club invitation directly from unemployed panel!
      await tester.ensureVisible(find.text('ACEITAR CONVITE'));
      await tester.tap(find.text('ACEITAR CONVITE'));
      await tester.pumpAndSettle();

      expect(
        find.text('Você assumiu o comando do Real Dourado!'),
        findsOneWidget,
      );

      // Switch to Tab 2: 2. DIVISÕES & TAÇA
      await tester.tap(find.text('2. DIVISÕES & TAÇA'));
      await tester.pumpAndSettle();

      expect(find.byKey(const ValueKey('standings-table')), findsOneWidget);
      await tester.tap(find.text('TAÇA NACIONAL'));
      await tester.pumpAndSettle();
      expect(find.byKey(const ValueKey('cup-champion-banner')), findsOneWidget);
      expect(
        find.text('CAMPEÃO DA TAÇA NACIONAL: Atlético Verde'),
        findsOneWidget,
      );

      // Filter Cup by Final stage (which is fixture #28, proving .take(24) truncation is fixed!)
      await tester.ensureVisible(find.byKey(const ValueKey('cup-stage-Final')));
      await tester.tap(find.byKey(const ValueKey('cup-stage-Final')));
      await tester.pumpAndSettle();
      expect(find.text('União Imperial (1ª Divisão)'), findsOneWidget);

      // Switch to Tab 3: 3. MERCADO & LEILÃO
      await tester.tap(find.text('3. MERCADO & LEILÃO'));
      await tester.pumpAndSettle();
      expect(find.byKey(const ValueKey('market-tab-0')), findsOneWidget);
      expect(find.textContaining('Goleiro Livre'), findsOneWidget);
      expect(find.text('COMPRA DIRETA'), findsWidgets);
      expect(find.textContaining('ABERTO'), findsOneWidget);
      await tester.tap(find.byKey(const ValueKey('market-tab-1')));
      await tester.pumpAndSettle();
      expect(find.textContaining('Rivaldo Craque'), findsOneWidget);
      expect(find.textContaining('CONTRAPROPOSTA'), findsWidgets);

      // Switch to Tab 4: 4. FINANÇAS & ESTÁDIO
      await tester.tap(find.text('4. FINANÇAS & ESTÁDIO'));
      await tester.pumpAndSettle();
      expect(
        find.text('RESUMO FINANCEIRO CONSOLIDADO DO CLUBE'),
        findsOneWidget,
      );
      expect(
        find.byKey(const ValueKey('finances-ledger-table')),
        findsOneWidget,
      );

      // Switch to Tab 5: 5. RANKING & CARREIRA and open Coach Career Detail Dialog
      await tester.ensureVisible(find.text('5. RANKING & CARREIRA'));
      await tester.tap(find.text('5. RANKING & CARREIRA'));
      await tester.pumpAndSettle();
      expect(find.byKey(const ValueKey('coach-ranking-table')), findsOneWidget);
      final coachName = find
          .descendant(
            of: find.byKey(const ValueKey('coach-ranking-table')),
            matching: find.text('Treinador 1'),
          )
          .first;
      await tester.ensureVisible(coachName);
      await tester.tap(coachName);
      await tester.pumpAndSettle();
      expect(
        find.byKey(const ValueKey('coach-career-detail-dialog')),
        findsOneWidget,
      );
      await tester.tap(find.text('FECHAR'));
      await tester.pumpAndSettle();
    },
  );

  testWidgets(
    'Task 011: Responsive Viewport Matrix (320px..1920px) Renders Zero Overflows',
    (WidgetTester tester) async {
      const widths = [
        320.0,
        360.0,
        390.0,
        430.0,
        768.0,
        1024.0,
        1280.0,
        1920.0,
      ];
      final fakeApi = FakeGameplay011ApiClient();

      await tester.binding.setSurfaceSize(const Size(1280, 900));
      await tester.pumpWidget(ElifootClassicApp(apiClient: fakeApi));
      await tester.tap(find.text('ENTRAR NO LOBBY (LOCAL DEV)'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('ENTRAR COM CLUBE'));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('choose-club-club-1')));
      await tester.pumpAndSettle();

      for (final width in widths) {
        final height = width < 600 ? 780.0 : 960.0;
        await tester.binding.setSurfaceSize(Size(width, height));
        await tester.pumpAndSettle();

        for (final tab in [
          '1. PLANTEL & TÁTICAS',
          '2. DIVISÕES & TAÇA',
          '3. MERCADO & LEILÃO',
          '4. FINANÇAS & ESTÁDIO',
          '5. RANKING & CARREIRA',
          '6. CALENDÁRIO & RESULTADOS',
        ]) {
          if (width < 1024) {
            await tester.tap(find.byKey(const Key('open-navigation')));
            await tester.pumpAndSettle();
          }
          await tester.ensureVisible(find.text(tab));
          await tester.tap(find.text(tab));
          await tester.pumpAndSettle();
          expect(
            tester.takeException(),
            isNull,
            reason: 'Overflow at ${width}px on $tab',
          );
        }
      }
      await tester.binding.setSurfaceSize(null);
    },
  );

  testWidgets(
    'Task 011: All 10 Tactical Formations (4-4-2..5-0-5), Market Actions & Stadium/Finance Operations',
    (WidgetTester tester) async {
      await tester.binding.setSurfaceSize(const Size(1280, 900));
      addTearDown(() => tester.binding.setSurfaceSize(null));

      final fakeApi = FakeGameplay011ApiClient();
      await tester.pumpWidget(ElifootClassicApp(apiClient: fakeApi));

      await tester.tap(find.text('ENTRAR NO LOBBY (LOCAL DEV)'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('ENTRAR COM CLUBE'));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('choose-club-club-1')));
      await tester.pumpAndSettle();

      // Cycle through all 10 tactical formations via quick action button
      for (var i = 0; i < 10; i++) {
        await tester.ensureVisible(
          find.byKey(const ValueKey('quick-action-taticas')),
        );
        await tester.tap(find.byKey(const ValueKey('quick-action-taticas')));
        await tester.pumpAndSettle();
      }
      expect(find.textContaining('4-4-2'), findsWidgets);

      // Test Market operations (Buy, Offer 90%, Auction Bid, Respond Offer)
      await tester.tap(find.byKey(const ValueKey('quick-action-mercado')));
      await tester.pumpAndSettle();

      await tester.tap(find.byKey(const ValueKey('market-tab-1')));
      await tester.pumpAndSettle();
      // 012D: the negotiated salary comes from the server.
      expect(find.textContaining('R\$ 1.234'), findsOneWidget);
      expect(find.text('R\$ 20.300'), findsNothing);
      await tester.ensureVisible(find.byKey(const ValueKey('purchase-mkt-1')));
      await tester.tap(find.byKey(const ValueKey('purchase-mkt-1')));
      await tester.pumpAndSettle();
      expect(find.text('COMPRAR JOGADOR'), findsWidgets);
      await tester.tap(find.byKey(const ValueKey('direct-confirm-buy')));
      await tester.pumpAndSettle();
      expect(find.text('COMPRA CONFIRMADA.'), findsOneWidget);
      await tester.ensureVisible(find.byKey(const ValueKey('purchase-mkt-1')));
      await tester.tap(find.byKey(const ValueKey('purchase-mkt-1')));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const ValueKey('direct-custom-offer')));
      await tester.pumpAndSettle();
      await tester.enterText(
        find.byKey(const ValueKey('market-custom-amount')),
        '540000',
      );
      await tester.tap(find.byKey(const ValueKey('market-confirm-amount')));
      await tester.pumpAndSettle();
      expect(find.text('PROPOSTA ENVIADA.'), findsOneWidget);
      await tester.tap(find.byKey(const ValueKey('market-tab-0')));
      await tester.pumpAndSettle();
      await tester.ensureVisible(find.byKey(const ValueKey('bid-auc-1')));
      await tester.tap(find.byKey(const ValueKey('bid-auc-1')));
      await tester.pumpAndSettle();
      await tester.enterText(
        find.byKey(const ValueKey('market-custom-amount')),
        '185000',
      );
      await tester.tap(find.byKey(const ValueKey('market-confirm-amount')));
      await tester.pumpAndSettle();
      expect(find.text('LANCE REGISTRADO.'), findsOneWidget);

      // Test Stadium & Finance operations (Renovate, Expand, Loan)
      await tester.ensureVisible(find.text('4. FINANÇAS & ESTÁDIO'));
      await tester.tap(find.text('4. FINANÇAS & ESTÁDIO'));
      await tester.pumpAndSettle();

      await tester.ensureVisible(
        find.byKey(const ValueKey('btn-reformar-estadio')),
      );
      await tester.tap(find.byKey(const ValueKey('btn-reformar-estadio')));
      await tester.pumpAndSettle();
      expect(find.text('Obra de reforma estrutural iniciada!'), findsOneWidget);

      await tester.ensureVisible(
        find.byKey(const ValueKey('btn-ampliar-estadio')),
      );
      await tester.tap(find.byKey(const ValueKey('btn-ampliar-estadio')));
      await tester.pumpAndSettle();
      expect(
        find.text('Ampliação de +5.000 lugares iniciada!'),
        findsOneWidget,
      );

      await tester.ensureVisible(
        find.byKey(const ValueKey('btn-emprestimo-bancario')),
      );
      await tester.tap(find.byKey(const ValueKey('btn-emprestimo-bancario')));
      await tester.pumpAndSettle();
      expect(
        find.text('Empréstimo bancário de R\$ 500.000 creditado em caixa.'),
        findsOneWidget,
      );
    },
  );
}
