import 'dart:async';
import 'dart:convert';
import 'package:http/http.dart' as http;
import 'package:web_socket_channel/web_socket_channel.dart';
import 'api_environment.dart';
import 'account_http_client.dart';
import 'account_transport.dart';

class ElifootApiClient {
  Future<Map<String, dynamic>> getClubHistory(
    String world,
    String club, {
    int? season,
  }) async => _decodeResponse(
    await _http.get(
      Uri.parse(
        '$baseUrl/api/worlds/$world/clubs/$club/history',
      ).replace(queryParameters: season == null ? null : {'season': '$season'}),
    ),
  );
  String baseUrl;
  String? _lobbyCoachId, _lobbyDeviceId;
  final AccountHttpClient _http;
  WebSocketChannel? _wsChannel;
  StreamSubscription? _wsSub;
  Timer? _pollTimer;
  Timer? _pingTimer;
  Timer? _reconnectTimer;
  int _connectionGeneration = 0;
  bool _polling = false;
  bool _signalRConnected = false;
  int _signalRMessagesReceived = 0;

  bool get signalRConnected => _signalRConnected;
  int get signalRMessagesReceived => _signalRMessagesReceived;

  void Function(Map<String, dynamic> snapshot)? onSnapshotUpdated;
  void Function(Map<String, dynamic> counter)? onReadyCounterUpdated;
  void Function(String message)? onNotification;

  ElifootApiClient({String? baseUrl, http.Client? httpClient})
    : baseUrl = baseUrl ?? ApiEnvironment.baseUrl,
      _http = AccountHttpClient(httpClient ?? createAccountTransport());

  void enableAccountSession({void Function()? onExpired}) {
    _http.enabled = true;
    _http.onExpired = onExpired;
  }

  Future<Map<String, dynamic>?> accountSession() async {
    final response = await _http.get(Uri.parse('$baseUrl/api/auth/session'));
    if (response.statusCode == 401) return null;
    final account = _decodeResponse(response);
    _lobbyCoachId = account['coachId']?.toString();
    _lobbyDeviceId = null;
    return account;
  }

  Future<Map<String, dynamic>> signInAccount(
    String username,
    String password, {
    String? displayName,
  }) async {
    final response = await _http.post(
      Uri.parse(
        '$baseUrl/api/auth/${displayName == null ? 'login' : 'register'}',
      ),
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode({
        'username': username,
        'password': password,
        'displayName': ?displayName,
      }),
    );
    final account = _decodeResponse(response);
    _lobbyCoachId = account['coachId']?.toString();
    _lobbyDeviceId = null;
    return account;
  }

  Future<void> signOutAccount() async {
    _decodeResponse(await _http.post(Uri.parse('$baseUrl/api/auth/logout')));
    disconnectRealtime();
    _lobbyCoachId = null;
    _lobbyDeviceId = null;
  }

  Future<Map<String, dynamic>> selectAccountProfile(String coachId) async {
    final response = await _http.post(
      Uri.parse('$baseUrl/api/auth/profile'),
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode({'coachId': coachId}),
    );
    final account = _decodeResponse(response);
    _lobbyCoachId = account['coachId']?.toString();
    return account;
  }

  Future<Map<String, dynamic>> linkLegacyAccount(String code) async =>
      _decodeResponse(
        await _http.post(
          Uri.parse('$baseUrl/api/accounts/legacy-link'),
          headers: {'Content-Type': 'application/json'},
          body: jsonEncode({'code': code}),
        ),
      );

  Future<dynamic> import016a(
    String adminToken,
    String path, {
    Map<String, dynamic>? body,
  }) async {
    final uri = Uri.parse('$baseUrl/api/catalog-016a/$path');
    final headers = {
      'Content-Type': 'application/json',
      'X-Catalog-Admin-Token': adminToken,
    };
    final response = body == null
        ? await _http.get(uri, headers: headers)
        : await _http.post(uri, headers: headers, body: jsonEncode(body));
    if (response.statusCode != 200) {
      if (response.statusCode == 404) {
        throw Exception(
          'Acesso administrativo indisponível. Confira o token local e a configuração 016A.',
        );
      }
      final value = jsonDecode(response.body);
      throw Exception(
        value is Map
            ? value['error'] ?? 'Importação indisponível'
            : 'Importação indisponível',
      );
    }
    return jsonDecode(response.body);
  }

  Future<dynamic> worldCreator(
    String coachId,
    String path, {
    Map<String, dynamic>? body,
  }) async {
    final uri = Uri.parse('$baseUrl/api/world-creator/$path');
    final headers = {
      'Content-Type': 'application/json',
      'Authorization': 'Bearer localdev-${coachId.replaceAll('-', '')}',
    };
    final res = body == null
        ? await _http.get(uri, headers: headers)
        : await _http.post(uri, headers: headers, body: jsonEncode(body));
    if (res.statusCode >= 400) {
      throw Exception(
        res.statusCode == 404
            ? (path.startsWith('assemblies')
                  ? 'Montagem não encontrada para este usuário.'
                  : 'Catálogo indisponível: ative o perfil 013A.')
            : res.body,
      );
    }
    return res.body.isEmpty ? null : jsonDecode(res.body);
  }

  Future<dynamic> world016b(
    String owner,
    String area,
    String path, {
    Map<String, dynamic>? body,
    String? adminToken,
  }) async {
    if (!['world-catalog-016b', 'world-lobby-016b'].contains(area)) {
      throw ArgumentError('Área inválida');
    }
    final uri = Uri.parse('$baseUrl/api/$area/$path');
    final headers = {
      'Content-Type': 'application/json',
      'Authorization': 'Bearer localdev-${owner.replaceAll('-', '')}',
      'X-Catalog-Admin-Token': ?adminToken,
    };
    final response = body == null
        ? await _http.get(uri, headers: headers)
        : await _http.post(uri, headers: headers, body: jsonEncode(body));
    final value = response.body.isEmpty ? null : jsonDecode(response.body);
    if (response.statusCode >= 400) {
      throw Exception(
        value is Map
            ? value['error'] ?? 'Serviço 016B indisponível'
            : 'Serviço 016B indisponível',
      );
    }
    return value;
  }

  Future<Map<String, dynamic>> getAppearance(
    String owner,
    String world,
    String club,
  ) async => Map<String, dynamic>.from(
    await worldCreator(owner, 'appearance/$world/$club') as Map,
  );

  Future<String> profileAppearance(String owner, {String? version}) async {
    final uri = Uri.parse('$baseUrl/api/profile/appearance');
    final headers = {
      'Authorization': 'Bearer localdev-${owner.replaceAll('-', '')}',
      'Content-Type': 'application/json',
    };
    final response = version == null
        ? await _http.get(uri, headers: headers)
        : await _http.put(
            uri,
            headers: headers,
            body: jsonEncode({'mode': version}),
          );
    if (response.statusCode != 200) {
      throw Exception(
        'Preferência visual indisponível (${response.statusCode}).',
      );
    }
    return jsonDecode(response.body)['mode'] as String;
  }

  Future<Map<String, dynamic>> saveAppearance(
    String owner,
    String world,
    String club,
    Map<String, dynamic> preferences,
    int version,
  ) async => Map<String, dynamic>.from(
    await worldCreator(
          owner,
          'appearance/$world/$club',
          body: {'preferences': preferences, 'expectedVersion': version},
        )
        as Map,
  );
  Future<Map<String, dynamic>> localLogin(
    String nickname,
    String deviceId,
  ) async {
    final res = await _http.post(
      Uri.parse('$baseUrl/api/auth/local-login'),
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode({'nickname': nickname, 'deviceId': deviceId}),
    );
    final auth = _decodeResponse(res);
    _lobbyCoachId = auth['coachId']?.toString();
    _lobbyDeviceId = auth['deviceId']?.toString();
    return auth;
  }

  Future<List<dynamic>> listWorlds() async {
    final query = <String, String>{};
    if (_lobbyCoachId != null) {
      query['coachId'] = _lobbyCoachId!;
    }
    if (_lobbyDeviceId != null) {
      query['deviceId'] = _lobbyDeviceId!;
    }
    final uri = Uri.parse(
      '$baseUrl/api/worlds',
    ).replace(queryParameters: query);
    final res = await _http.get(uri);
    if (res.statusCode >= 400) throw Exception(res.body);
    return jsonDecode(res.body) as List<dynamic>;
  }

  Future<Map<String, dynamic>> createWorld({
    required String worldName,
    int maxHumans = 8,
    int totalClubs = 128,
    int seed = 199801,
    int tickIntervalMs = 220,
    int pauseTimeoutSeconds = 60,
    bool allowClassic505 = true,
    String? inviteCode,
  }) async {
    final res = await _http.post(
      Uri.parse('$baseUrl/api/worlds'),
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode({
        'worldName': worldName,
        'maxHumans': maxHumans,
        'totalClubs': totalClubs,
        'seed': seed,
        'tickIntervalMs': tickIntervalMs,
        'pauseTimeoutSeconds': pauseTimeoutSeconds,
        'allowClassic505': allowClassic505,
        'inviteCode': inviteCode,
      }),
    );
    return _decodeResponse(res);
  }

  Future<Map<String, dynamic>> joinWorld({
    required String worldId,
    required String coachId,
    required String nickname,
    required String deviceId,
    String? preferredClubId,
    bool joinUnemployed = false,
  }) async {
    final q = <String, String>{
      'coachId': coachId,
      'nickname': nickname,
      'deviceId': deviceId,
      'joinUnemployed': joinUnemployed.toString(),
    };
    if (preferredClubId != null && preferredClubId.isNotEmpty) {
      q['preferredClubId'] = preferredClubId;
    }
    final uri = Uri.parse(
      '$baseUrl/api/worlds/$worldId/join',
    ).replace(queryParameters: q);
    final res = await _http.post(uri);
    return _decodeResponse(res);
  }

  Future<Map<String, dynamic>> joinWorldByCode({
    required String inviteCode,
    required String coachId,
    required String nickname,
    required String deviceId,
  }) async {
    final uri = Uri.parse('$baseUrl/api/worlds/join-by-code').replace(
      queryParameters: {
        'inviteCode': inviteCode,
        'coachId': coachId,
        'nickname': nickname,
        'deviceId': deviceId,
      },
    );
    final res = await _http.post(uri);
    return _decodeResponse(res);
  }

  Future<Map<String, dynamic>> getSnapshot(String worldId) async {
    final res = await _http.get(
      Uri.parse('$baseUrl/api/worlds/$worldId/snapshot'),
    );
    return _decodeResponse(res);
  }

  Future<List<dynamic>> getCalendar(String worldId) async {
    final res = await _http.get(
      Uri.parse('$baseUrl/api/worlds/$worldId/calendar'),
    );
    if (res.statusCode >= 400) throw Exception(res.body);
    return jsonDecode(res.body) as List<dynamic>;
  }

  Future<void> saveEditorEntity(
    String worldId,
    String kind,
    Map<String, dynamic> dto,
  ) async {
    final res = await _http.post(
      Uri.parse('$baseUrl/api/worlds/$worldId/editor/$kind'),
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode(dto),
    );
    _decodeResponse(res);
  }

  Future<List<dynamic>> getClubs(String worldId, {String? tier}) async {
    final uri = tier == null
        ? Uri.parse('$baseUrl/api/worlds/$worldId/clubs')
        : Uri.parse('$baseUrl/api/worlds/$worldId/clubs?tier=$tier');
    final res = await _http.get(uri);
    if (res.statusCode >= 400) throw Exception(res.body);
    return jsonDecode(res.body) as List<dynamic>;
  }

  Future<Map<String, dynamic>> getClubDetail(
    String worldId,
    String clubId,
  ) async {
    final res = await _http.get(
      Uri.parse('$baseUrl/api/worlds/$worldId/clubs/$clubId'),
    );
    return _decodeResponse(res);
  }

  Future<Map<String, dynamic>> selectLineup({
    required String worldId,
    required String coachId,
    required String formation,
    required String mode,
    required List<String> currentStarterIds,
    required int roundVersion,
    String? idempotencyKey,
  }) async {
    final response = await _http.post(
      Uri.parse('$baseUrl/api/worlds/$worldId/lineup-selection'),
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode({
        'coachId': coachId,
        'formation': formation,
        'mode': mode,
        'currentStarterPlayerIds': currentStarterIds,
        'expectedRoundVersion': roundVersion,
        'idempotencyKey':
            idempotencyKey ??
            'lineup-$coachId-${DateTime.now().microsecondsSinceEpoch}',
      }),
    );
    return _decodeResponse(response);
  }

  Future<Map<String, dynamic>> submitTactics({
    required String worldId,
    required String coachId,
    required String formation,
    required List<String> starterIds,
    required List<String> benchIds,
    required int roundVersion,
  }) async {
    final res = await _http.post(
      Uri.parse('$baseUrl/api/worlds/$worldId/tactics'),
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode({
        'gameWorldId': worldId,
        'coachId': coachId,
        'formation': formation,
        'starterPlayerIds': starterIds,
        'benchPlayerIds': benchIds,
        'idempotencyKey':
            'tac-$coachId-${DateTime.now().microsecondsSinceEpoch}',
        'expectedRoundVersion': roundVersion,
        'saveAsPreferred': true,
      }),
    );
    return _decodeResponse(res);
  }

  Future<Map<String, dynamic>> submitUnemployedReady({
    required String worldId,
    required String coachId,
    required int roundVersion,
  }) async {
    final res = await _http.post(
      Uri.parse('$baseUrl/api/worlds/$worldId/ready-unemployed'),
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode({
        'gameWorldId': worldId,
        'coachId': coachId,
        'idempotencyKey':
            'unemp-$coachId-${DateTime.now().microsecondsSinceEpoch}',
        'expectedRoundVersion': roundVersion,
      }),
    );
    return _decodeResponse(res);
  }

  Future<Map<String, dynamic>> withdrawReady({
    required String worldId,
    required String coachId,
    required int roundVersion,
  }) async {
    final res = await _http.post(
      Uri.parse('$baseUrl/api/worlds/$worldId/withdraw-ready'),
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode({
        'gameWorldId': worldId,
        'coachId': coachId,
        'idempotencyKey':
            'withdraw-$coachId-${DateTime.now().microsecondsSinceEpoch}',
        'expectedRoundVersion': roundVersion,
      }),
    );
    return _decodeResponse(res);
  }

  Future<Map<String, dynamic>> requestGlobalPause(
    String worldId,
    String coachId,
  ) async {
    final res = await _http.post(
      Uri.parse('$baseUrl/api/worlds/$worldId/pause'),
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode({
        'gameWorldId': worldId,
        'coachId': coachId,
        'idempotencyKey':
            'pause-$coachId-${DateTime.now().microsecondsSinceEpoch}',
      }),
    );
    return _decodeResponse(res);
  }

  Future<Map<String, dynamic>> submitInMatchAdjustments({
    required String worldId,
    required String coachId,
    required String matchId,
    String? newFormation,
    required List<Map<String, String>> substitutions,
  }) async {
    final res = await _http.post(
      Uri.parse('$baseUrl/api/worlds/$worldId/in-match-adjust'),
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode({
        'gameWorldId': worldId,
        'coachId': coachId,
        'matchId': matchId,
        'newFormation': newFormation,
        'substitutions': substitutions,
        'idempotencyKey':
            'adj-$coachId-${DateTime.now().microsecondsSinceEpoch}',
      }),
    );
    return _decodeResponse(res);
  }

  Future<Map<String, dynamic>> choosePenaltyTaker(
    String worldId,
    String coachId,
    String decisionId,
    String playerId,
  ) async {
    final response = await _http.post(
      Uri.parse('$baseUrl/api/worlds/$worldId/penalty-taker'),
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode({
        'gameWorldId': worldId,
        'coachId': coachId,
        'decisionId': decisionId,
        'playerId': playerId,
        'idempotencyKey': 'penalty-$coachId-$decisionId-$playerId',
      }),
    );
    return _decodeResponse(response);
  }

  Future<Map<String, dynamic>> confirmContinue(
    String worldId,
    String coachId,
  ) async {
    final res = await _http.post(
      Uri.parse('$baseUrl/api/worlds/$worldId/continue'),
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode({
        'gameWorldId': worldId,
        'coachId': coachId,
        'idempotencyKey':
            'cont-$coachId-${DateTime.now().microsecondsSinceEpoch}',
      }),
    );
    return _decodeResponse(res);
  }

  Future<Map<String, dynamic>> manualTick(
    String worldId, {
    int ticks = 1,
  }) async {
    final res = await _http.post(
      Uri.parse('$baseUrl/api/worlds/$worldId/tick?ticks=$ticks'),
    );
    return _decodeResponse(res);
  }

  Future<Map<String, dynamic>> advanceToNextRound(
    String worldId, {
    int? expectedSeasonNumber,
    int? expectedRoundNumber,
  }) async {
    final res = await _http.post(
      Uri.parse('$baseUrl/api/worlds/$worldId/next-round').replace(
        queryParameters: {
          if (expectedSeasonNumber != null)
            'expectedSeasonNumber': '$expectedSeasonNumber',
          if (expectedRoundNumber != null)
            'expectedRoundNumber': '$expectedRoundNumber',
        },
      ),
    );
    return _decodeResponse(res);
  }

  Future<List<dynamic>> getStandings(String worldId, String tier) async {
    final res = await _http.get(
      Uri.parse('$baseUrl/api/worlds/$worldId/standings/$tier'),
    );
    if (res.statusCode >= 400) throw Exception(res.body);
    return jsonDecode(res.body) as List<dynamic>;
  }

  Future<List<dynamic>> getCupFixtures(String worldId) async {
    final res = await _http.get(Uri.parse('$baseUrl/api/worlds/$worldId/cup'));
    if (res.statusCode >= 400) throw Exception(res.body);
    return jsonDecode(res.body) as List<dynamic>;
  }

  Future<List<dynamic>> getTopScorers(String worldId) async {
    final res = await _http.get(
      Uri.parse('$baseUrl/api/worlds/$worldId/top-scorers'),
    );
    if (res.statusCode >= 400) throw Exception(res.body);
    return jsonDecode(res.body) as List<dynamic>;
  }

  Future<Map<String, dynamic>> getRankingAndHistory(String worldId) async {
    final res = await _http.get(
      Uri.parse('$baseUrl/api/worlds/$worldId/ranking'),
    );
    return _decodeResponse(res);
  }

  Future<Map<String, dynamic>> getPlayerRankings(
    String worldId, {
    String category = 'scorers',
    String scope = 'general',
    String period = 'career',
    int? season,
  }) async {
    if (period == 'season' && season == null) {
      throw ArgumentError('Informe a temporada selecionada.');
    }
    final uri = Uri.parse('$baseUrl/api/worlds/$worldId/player-rankings')
        .replace(
          queryParameters: {
            'category': category,
            'scope': scope,
            'period': period,
            if (period == 'season') 'season': season.toString(),
          },
        );
    return _decodeResponse(await _http.get(uri));
  }

  Future<Map<String, dynamic>> getPlayerHistory(
    String worldId,
    String playerId,
  ) async {
    return _decodeResponse(
      await _http.get(
        Uri.parse('$baseUrl/api/worlds/$worldId/players/$playerId/history'),
      ),
    );
  }

  Future<Map<String, dynamic>> getSeasonTopScorers(
    String worldId, {
    int? season,
  }) async {
    final uri = Uri.parse(
      '$baseUrl/api/worlds/$worldId/season-top-scorers',
    ).replace(queryParameters: {if (season != null) 'season': '$season'});
    return _decodeResponse(await _http.get(uri));
  }

  Future<Map<String, dynamic>> getMarket(String worldId) async {
    final res = await _http.get(
      Uri.parse('$baseUrl/api/worlds/$worldId/market'),
    );
    final data = _decodeResponse(res);
    if (data['roundVersion'] is num) {
      _financeVersions[worldId] = (data['roundVersion'] as num).toInt();
    }
    return data;
  }

  Future<Map<String, dynamic>> getPlayerScouting(
    String worldId,
    String coachId, {
    String name = '',
    bool observedOnly = false,
  }) async => _decodeResponse(
    await _http.get(
      Uri.parse('$baseUrl/api/worlds/$worldId/players/scouting').replace(
        queryParameters: {
          'coachId': coachId,
          'name': name,
          'observedOnly': '$observedOnly',
        },
      ),
    ),
  );

  Future<List<String>> setPlayerObservation(
    String worldId,
    String coachId,
    String playerId,
    bool observed,
  ) async {
    final data = _decodeResponse(
      await _http.put(
        Uri.parse('$baseUrl/api/worlds/$worldId/players/$playerId/observation'),
        headers: {'Content-Type': 'application/json'},
        body: jsonEncode({'coachId': coachId, 'isObserved': observed}),
      ),
    );
    return (data['observedPlayerIds'] as List).map((id) => '$id').toList();
  }

  Future<void> listPlayerForTransfer(
    String worldId,
    String coachId,
    String playerId,
    double price,
  ) async {
    final uri = Uri.parse('$baseUrl/api/worlds/$worldId/market/list').replace(
      queryParameters: {
        'coachId': coachId,
        'playerId': playerId,
        'askingPrice': price.toStringAsFixed(2),
      },
    );
    final res = await _postFinance(uri);
    _decodeResponse(res);
  }

  Future<void> buyListedPlayer(
    String worldId,
    String coachId,
    String playerId,
  ) async {
    final uri = Uri.parse(
      '$baseUrl/api/worlds/$worldId/market/buy-listed',
    ).replace(queryParameters: {'coachId': coachId, 'playerId': playerId});
    final res = await _postFinance(uri);
    _decodeResponse(res);
  }

  Future<void> withdrawTransferListing(
    String worldId,
    String coachId,
    String playerId,
  ) async {
    final uri = Uri.parse(
      '$baseUrl/api/worlds/$worldId/market/unlist',
    ).replace(queryParameters: {'coachId': coachId, 'playerId': playerId});
    _decodeResponse(await _postFinance(uri));
  }

  Future<void> buyConfirmedListing(
    String worldId,
    String coachId,
    Map<String, dynamic> listing,
  ) async {
    final uri = Uri.parse('$baseUrl/api/worlds/$worldId/market/buy-listed')
        .replace(
          queryParameters: {
            'coachId': coachId,
            'playerId': listing['playerId'].toString(),
            if (listing['listingVersion'] != null)
              'listingVersion': listing['listingVersion'].toString(),
            if (listing['sellerClubId'] != null)
              'sellerClubId': listing['sellerClubId'].toString(),
            'askingPrice': (listing['askingPrice'] as num).toStringAsFixed(2),
          },
        );
    _decodeResponse(await _postFinance(uri));
  }

  Future<void> counterTransferOffer(
    String worldId,
    String offerId,
    String coachId,
    double amount,
  ) async {
    final uri =
        Uri.parse(
          '$baseUrl/api/worlds/$worldId/market/offer/$offerId/counter',
        ).replace(
          queryParameters: {
            'coachId': coachId,
            'amount': amount.toStringAsFixed(2),
          },
        );
    _decodeResponse(await _postFinance(uri));
  }

  Future<Map<String, dynamic>> makeTransferOffer(
    String worldId,
    String coachId,
    String playerId,
    double amount,
  ) async {
    final uri = Uri.parse('$baseUrl/api/worlds/$worldId/market/offer').replace(
      queryParameters: {
        'coachId': coachId,
        'playerId': playerId,
        'offeredAmount': amount.toStringAsFixed(2),
      },
    );
    final res = await _postFinance(uri);
    return _decodeResponse(res);
  }

  Future<void> respondTransferOffer(
    String worldId,
    String offerId,
    String coachId,
    bool accept,
  ) async {
    final uri =
        Uri.parse(
          '$baseUrl/api/worlds/$worldId/market/offer/$offerId/respond',
        ).replace(
          queryParameters: {'coachId': coachId, 'accept': accept.toString()},
        );
    final res = await _postFinance(uri);
    _decodeResponse(res);
  }

  Future<Map<String, dynamic>> createVoluntaryAuction(
    String worldId,
    String coachId,
    String playerId, {
    double minimumBid = 500000,
    int durationRounds = 2,
  }) async {
    final uri = Uri.parse('$baseUrl/api/worlds/$worldId/market/auction');
    return _decodeResponse(
      await _postFinance(
        uri,
        body: {
          'coachId': coachId,
          'playerId': playerId,
          'minimumBid': minimumBid,
          'durationRounds': durationRounds,
        },
      ),
    );
  }

  Future<void> placeAuctionBid(
    String worldId,
    String auctionId,
    String coachId,
    double bidAmount,
  ) async {
    final uri =
        Uri.parse(
          '$baseUrl/api/worlds/$worldId/market/auction/$auctionId/bid',
        ).replace(
          queryParameters: {
            'coachId': coachId,
            'bidAmount': bidAmount.toStringAsFixed(2),
          },
        );
    final res = await _postFinance(uri);
    _decodeResponse(res);
  }

  Future<void> renewPlayerContract(
    String worldId,
    String playerId,
    String coachId,
    int additionalRounds,
  ) async {
    final uri =
        Uri.parse(
          '$baseUrl/api/worlds/$worldId/players/$playerId/renew',
        ).replace(
          queryParameters: {
            'coachId': coachId,
            'additionalRounds': additionalRounds.toString(),
          },
        );
    final res = await _postFinance(uri);
    _decodeResponse(res);
  }

  Future<Map<String, dynamic>> getClubFinances(
    String worldId,
    String clubId,
  ) async {
    final res = await _http.get(
      Uri.parse('$baseUrl/api/worlds/$worldId/clubs/$clubId/finances'),
    );
    final data = _decodeResponse(res);
    _financeVersions[worldId] = data['roundVersion'] as int?;
    return data;
  }

  Future<void> takeBankLoan(
    String worldId,
    String clubId,
    String coachId,
    double amount,
    int rounds,
  ) async {
    final uri = Uri.parse('$baseUrl/api/worlds/$worldId/clubs/$clubId/loans')
        .replace(
          queryParameters: {
            'coachId': coachId,
            'amount': amount.round().toString(),
            'rounds': rounds.toString(),
          },
        );
    final res = await _postFinance(uri);
    _decodeResponse(res);
  }

  Future<void> startStadiumRenovation(
    String worldId,
    String clubId,
    String coachId,
  ) async {
    final uri = Uri.parse(
      '$baseUrl/api/worlds/$worldId/clubs/$clubId/stadium/renovate',
    ).replace(queryParameters: {'coachId': coachId});
    final res = await _postFinance(uri);
    _decodeResponse(res);
  }

  Future<void> startStadiumExpansion(
    String worldId,
    String clubId,
    String coachId,
    int seats,
  ) async {
    final uri =
        Uri.parse(
          '$baseUrl/api/worlds/$worldId/clubs/$clubId/stadium/expand',
        ).replace(
          queryParameters: {
            'coachId': coachId,
            'additionalSeats': seats.toString(),
          },
        );
    final res = await _postFinance(uri);
    _decodeResponse(res);
  }

  final Map<String, String> _financeKeys = {};
  final Map<String, int?> _financeVersions = {};
  int _financeSequence = 0;

  Future<http.Response> _postFinance(
    Uri uri, {
    Map<String, dynamic>? body,
  }) async {
    final identity = '$uri:${body == null ? "" : jsonEncode(body)}';
    final key = _financeKeys.putIfAbsent(
      identity,
      () => '${DateTime.now().microsecondsSinceEpoch}-${++_financeSequence}',
    );
    final segments = uri.pathSegments;
    final worldId = segments.length > 2 ? segments[2] : '';
    final version = _financeVersions[worldId];
    final response = body == null
        ? await _http.post(
            uri.replace(
              queryParameters: {
                ...uri.queryParameters,
                'idempotencyKey': key,
                if (version != null) 'expectedRoundVersion': '$version',
              },
            ),
          )
        : await _http.post(
            uri,
            headers: {'Content-Type': 'application/json'},
            body: jsonEncode({
              ...body,
              'idempotencyKey': key,
              'expectedRoundVersion': ?version,
            }),
          );
    // A transport/server failure may happen after the server committed the operation.
    if (response.statusCode < 500) _financeKeys.remove(identity);
    return response;
  }

  Future<void> repayBankLoan(
    String worldId,
    String clubId,
    String coachId,
    String loanId,
  ) async {
    final response = await _postFinance(
      Uri.parse(
        '$baseUrl/api/worlds/$worldId/clubs/$clubId/loans/$loanId/repay',
      ),
      body: {'coachId': coachId},
    );
    _decodeResponse(response);
  }

  Future<String?> respondSalaryRequest(
    String worldId,
    String clubId,
    String coachId,
    String requestId,
    bool accept,
  ) async {
    final response = await _postFinance(
      Uri.parse(
        '$baseUrl/api/worlds/$worldId/clubs/$clubId/salary-requests/$requestId/respond',
      ),
      body: {'coachId': coachId, 'accept': accept},
    );
    return _decodeResponse(response)['message'] as String?;
  }

  Future<Map<String, dynamic>> getInvitations(
    String worldId,
    String coachId,
  ) async {
    final res = await _http.get(
      Uri.parse('$baseUrl/api/worlds/$worldId/invitations/$coachId'),
    );
    return _decodeResponse(res);
  }

  Future<void> acceptInvitation(
    String worldId,
    String invitationId,
    String coachId,
  ) async {
    final uri = Uri.parse(
      '$baseUrl/api/worlds/$worldId/invitations/$invitationId/accept',
    ).replace(queryParameters: {'coachId': coachId});
    final res = await _http.post(uri);
    _decodeResponse(res);
  }

  Future<void> rejectInvitation(
    String worldId,
    String invitationId,
    String coachId,
  ) async {
    final uri = Uri.parse(
      '$baseUrl/api/worlds/$worldId/invitations/$invitationId/reject',
    ).replace(queryParameters: {'coachId': coachId});
    _decodeResponse(await _http.post(uri));
  }

  Future<String> exportWorldJson(String worldId) async {
    final res = await _http.get(
      Uri.parse('$baseUrl/api/worlds/$worldId/editor/export-json'),
    );
    if (res.statusCode >= 400) throw Exception(res.body);
    return res.body;
  }

  Future<String> exportPlayersCsv(String worldId) async {
    final res = await _http.get(
      Uri.parse('$baseUrl/api/worlds/$worldId/editor/export-csv'),
    );
    if (res.statusCode >= 400) throw Exception(res.body);
    return res.body;
  }

  Future<Map<String, dynamic>> validatePackageJson(
    String worldId,
    String packageJson,
  ) async {
    final res = await _http.post(
      Uri.parse('$baseUrl/api/worlds/$worldId/editor/validate-package'),
      headers: {'Content-Type': 'application/json'},
      body: packageJson,
    );
    return _decodeResponse(res);
  }

  Future<Map<String, dynamic>> importPackageJson(
    String packageJson,
    String newWorldName,
  ) async {
    final uri = Uri.parse(
      '$baseUrl/api/worlds/editor/import-package',
    ).replace(queryParameters: {'newWorldName': newWorldName});
    final res = await _http.post(
      uri,
      headers: {'Content-Type': 'application/json'},
      body: packageJson,
    );
    return _decodeResponse(res);
  }

  Future<Map<String, dynamic>> updatePlayerInEditor(
    String worldId,
    Map<String, dynamic> playerDto,
  ) async {
    final res = await _http.post(
      Uri.parse('$baseUrl/api/worlds/$worldId/editor/update-player'),
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode(playerDto),
    );
    return _decodeResponse(res);
  }

  /// Connects to the SignalR Hub via WebSocket JSON protocol (`\x1e` delimiter)
  /// with automatic snapshot reconciliation.
  void connectRealtimeRoom(String worldId, String coachId) {
    disconnectRealtime();
    final generation = _connectionGeneration;
    void reconnect() {
      if (generation != _connectionGeneration) return;
      _signalRConnected = false;
      _reconnectTimer?.cancel();
      _reconnectTimer = Timer(const Duration(seconds: 2), () {
        if (generation == _connectionGeneration) {
          connectRealtimeRoom(worldId, coachId);
        }
      });
    }

    try {
      final wsBase = baseUrl
          .replaceFirst('http://', 'ws://')
          .replaceFirst('https://', 'wss://');
      final socketUri = Uri.parse('$wsBase/hubs/gameroom');
      _wsChannel = _http.enabled
          ? createAccountSocket(socketUri, _http.inner)
          : WebSocketChannel.connect(socketUri);
      // SignalR JSON v1 handshake
      _wsChannel!.sink.add('{"protocol":"json","version":1}\x1e');
      // Invoke JoinRoom(worldId, coachId)
      _wsChannel!.sink.add(
        '${jsonEncode({
          'type': 1,
          'invocationId': 'join-1',
          'target': 'JoinRoom',
          'arguments': [worldId, coachId],
        })}\x1e',
      );

      _wsSub = _wsChannel!.stream.listen(
        (raw) {
          if (raw is! String) return;
          final frames = raw.split('\x1e');
          for (final frame in frames) {
            if (frame.trim().isEmpty) continue;
            try {
              final msg = jsonDecode(frame) as Map<String, dynamic>;
              final type = msg['type'];
              if (type == 1) {
                _signalRMessagesReceived++;
                final target = msg['target'] as String?;
                final args = msg['arguments'] as List<dynamic>? ?? const [];
                if (target == 'ReadyCounterUpdated' && args.isNotEmpty) {
                  onReadyCounterUpdated?.call(args[0] as Map<String, dynamic>);
                } else if ((target == 'RoundStateChanged' ||
                        target == 'ClockTick') &&
                    args.isNotEmpty) {
                  onSnapshotUpdated?.call(args[0] as Map<String, dynamic>);
                } else if (target == 'GlobalPause' && args.isNotEmpty) {
                  final payload = args[0] as Map<String, dynamic>;
                  if (payload['snapshot'] != null) {
                    onSnapshotUpdated?.call(
                      payload['snapshot'] as Map<String, dynamic>,
                    );
                  }
                } else if (target == 'MarketUpdated' && args.isNotEmpty) {
                  final payload = args[0] as Map<String, dynamic>;
                  onNotification?.call(
                    payload['summaryMessage']?.toString() ??
                        'Mercado atualizado',
                  );
                }
              } else if (type == 3 && msg['result'] is Map<String, dynamic>) {
                _signalRConnected = true;
                onSnapshotUpdated?.call(msg['result'] as Map<String, dynamic>);
              }
            } catch (_) {}
          }
        },
        onError: (_) => reconnect(),
        onDone: reconnect,
      );
      _pingTimer = Timer.periodic(const Duration(seconds: 10), (_) {
        try {
          _wsChannel?.sink.add('{"type":6}\x1e');
        } catch (_) {
          reconnect();
        }
      });
    } catch (_) {
      reconnect();
    }

    // Periodic snapshot reconciliation every 600ms so reconnections or missed frames heal automatically
    _pollTimer = Timer.periodic(const Duration(milliseconds: 600), (_) async {
      if (_polling) return;
      _polling = true;
      try {
        final snap = await getSnapshot(worldId);
        if (generation == _connectionGeneration) onSnapshotUpdated?.call(snap);
      } catch (_) {
      } finally {
        _polling = false;
      }
    });
  }

  void disconnectRealtime() {
    _signalRConnected = false;
    _connectionGeneration++;
    _pingTimer?.cancel();
    _reconnectTimer?.cancel();
    _pollTimer?.cancel();
    _pollTimer = null;
    _wsSub?.cancel();
    _wsSub = null;
    _wsChannel?.sink.close();
    _wsChannel = null;
  }

  Map<String, dynamic> _decodeResponse(http.Response res) {
    if (res.statusCode >= 400) {
      try {
        final err = jsonDecode(res.body);
        if (err is Map && err['error'] != null) {
          throw Exception(err['error'].toString());
        }
      } catch (e) {
        if (e is Exception && !e.toString().contains('FormatException')) {
          rethrow;
        }
      }
      throw Exception('Erro HTTP ${res.statusCode}: ${res.body}');
    }
    if (res.body.isEmpty) return {};
    return jsonDecode(res.body) as Map<String, dynamic>;
  }
}
