import 'dart:async';
import 'dart:convert';
import 'package:http/http.dart' as http;
import 'package:web_socket_channel/web_socket_channel.dart';
import 'api_environment.dart';

class ElifootApiClient {
  String baseUrl;
  final http.Client _http;
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

  ElifootApiClient({
    String? baseUrl,
    http.Client? httpClient,
  }) : baseUrl = baseUrl ?? ApiEnvironment.baseUrl,
       _http = httpClient ?? http.Client();

  Future<Map<String, dynamic>> localLogin(String nickname, String deviceId) async {
    final res = await _http.post(
      Uri.parse('$baseUrl/api/auth/local-login'),
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode({'nickname': nickname, 'deviceId': deviceId}),
    );
    return _decodeResponse(res);
  }

  Future<List<dynamic>> listWorlds() async {
    final res = await _http.get(Uri.parse('$baseUrl/api/worlds'));
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
    final uri = Uri.parse('$baseUrl/api/worlds/$worldId/join').replace(queryParameters: q);
    final res = await _http.post(uri);
    return _decodeResponse(res);
  }

  Future<Map<String, dynamic>> joinWorldByCode({
    required String inviteCode,
    required String coachId,
    required String nickname,
    required String deviceId,
  }) async {
    final uri = Uri.parse('$baseUrl/api/worlds/join-by-code').replace(queryParameters: {
      'inviteCode': inviteCode,
      'coachId': coachId,
      'nickname': nickname,
      'deviceId': deviceId,
    });
    final res = await _http.post(uri);
    return _decodeResponse(res);
  }

  Future<Map<String, dynamic>> getSnapshot(String worldId) async {
    final res = await _http.get(Uri.parse('$baseUrl/api/worlds/$worldId/snapshot'));
    return _decodeResponse(res);
  }

  Future<List<dynamic>> getCalendar(String worldId) async {
    final res = await _http.get(Uri.parse('$baseUrl/api/worlds/$worldId/calendar'));
    if (res.statusCode >= 400) throw Exception(res.body);
    return jsonDecode(res.body) as List<dynamic>;
  }

  Future<void> saveEditorEntity(String worldId, String kind, Map<String, dynamic> dto) async {
    final res = await _http.post(Uri.parse('$baseUrl/api/worlds/$worldId/editor/$kind'),
      headers: {'Content-Type': 'application/json'}, body: jsonEncode(dto));
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

  Future<Map<String, dynamic>> getClubDetail(String worldId, String clubId) async {
    final res = await _http.get(Uri.parse('$baseUrl/api/worlds/$worldId/clubs/$clubId'));
    return _decodeResponse(res);
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
        'idempotencyKey': 'tac-$coachId-${DateTime.now().microsecondsSinceEpoch}',
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
        'idempotencyKey': 'unemp-$coachId-${DateTime.now().microsecondsSinceEpoch}',
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
        'idempotencyKey': 'withdraw-$coachId-${DateTime.now().microsecondsSinceEpoch}',
        'expectedRoundVersion': roundVersion,
      }),
    );
    return _decodeResponse(res);
  }

  Future<Map<String, dynamic>> requestGlobalPause(String worldId, String coachId) async {
    final res = await _http.post(
      Uri.parse('$baseUrl/api/worlds/$worldId/pause'),
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode({
        'gameWorldId': worldId,
        'coachId': coachId,
        'idempotencyKey': 'pause-$coachId-${DateTime.now().microsecondsSinceEpoch}',
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
        'idempotencyKey': 'adj-$coachId-${DateTime.now().microsecondsSinceEpoch}',
      }),
    );
    return _decodeResponse(res);
  }

  Future<Map<String, dynamic>> confirmContinue(String worldId, String coachId) async {
    final res = await _http.post(
      Uri.parse('$baseUrl/api/worlds/$worldId/continue'),
      headers: {'Content-Type': 'application/json'},
      body: jsonEncode({
        'gameWorldId': worldId,
        'coachId': coachId,
        'idempotencyKey': 'cont-$coachId-${DateTime.now().microsecondsSinceEpoch}',
      }),
    );
    return _decodeResponse(res);
  }

  Future<Map<String, dynamic>> manualTick(String worldId, {int ticks = 1}) async {
    final res = await _http.post(Uri.parse('$baseUrl/api/worlds/$worldId/tick?ticks=$ticks'));
    return _decodeResponse(res);
  }

  Future<Map<String, dynamic>> advanceToNextRound(String worldId) async {
    final res = await _http.post(Uri.parse('$baseUrl/api/worlds/$worldId/next-round'));
    return _decodeResponse(res);
  }

  Future<List<dynamic>> getStandings(String worldId, String tier) async {
    final res = await _http.get(Uri.parse('$baseUrl/api/worlds/$worldId/standings/$tier'));
    if (res.statusCode >= 400) throw Exception(res.body);
    return jsonDecode(res.body) as List<dynamic>;
  }

  Future<List<dynamic>> getCupFixtures(String worldId) async {
    final res = await _http.get(Uri.parse('$baseUrl/api/worlds/$worldId/cup'));
    if (res.statusCode >= 400) throw Exception(res.body);
    return jsonDecode(res.body) as List<dynamic>;
  }

  Future<List<dynamic>> getTopScorers(String worldId) async {
    final res = await _http.get(Uri.parse('$baseUrl/api/worlds/$worldId/top-scorers'));
    if (res.statusCode >= 400) throw Exception(res.body);
    return jsonDecode(res.body) as List<dynamic>;
  }

  Future<Map<String, dynamic>> getRankingAndHistory(String worldId) async {
    final res = await _http.get(Uri.parse('$baseUrl/api/worlds/$worldId/ranking'));
    return _decodeResponse(res);
  }

  Future<Map<String, dynamic>> getMarket(String worldId) async {
    final res = await _http.get(Uri.parse('$baseUrl/api/worlds/$worldId/market'));
    return _decodeResponse(res);
  }

  Future<void> listPlayerForTransfer(String worldId, String coachId, String playerId, double price) async {
    final uri = Uri.parse('$baseUrl/api/worlds/$worldId/market/list').replace(queryParameters: {
      'coachId': coachId,
      'playerId': playerId,
      'askingPrice': price.round().toString(),
    });
    final res = await _http.post(uri);
    _decodeResponse(res);
  }

  Future<void> buyListedPlayer(String worldId, String coachId, String playerId) async {
    final uri = Uri.parse('$baseUrl/api/worlds/$worldId/market/buy-listed').replace(queryParameters: {
      'coachId': coachId,
      'playerId': playerId,
    });
    final res = await _http.post(uri);
    _decodeResponse(res);
  }

  Future<Map<String, dynamic>> makeTransferOffer(String worldId, String coachId, String playerId, double amount) async {
    final uri = Uri.parse('$baseUrl/api/worlds/$worldId/market/offer').replace(queryParameters: {
      'coachId': coachId,
      'playerId': playerId,
      'offeredAmount': amount.round().toString(),
    });
    final res = await _http.post(uri);
    return _decodeResponse(res);
  }

  Future<void> respondTransferOffer(String worldId, String offerId, String coachId, bool accept) async {
    final uri = Uri.parse('$baseUrl/api/worlds/$worldId/market/offer/$offerId/respond').replace(queryParameters: {
      'coachId': coachId,
      'accept': accept.toString(),
    });
    final res = await _http.post(uri);
    _decodeResponse(res);
  }

  Future<void> placeAuctionBid(String worldId, String auctionId, String coachId, double bidAmount) async {
    final uri = Uri.parse('$baseUrl/api/worlds/$worldId/market/auction/$auctionId/bid').replace(queryParameters: {
      'coachId': coachId,
      'bidAmount': bidAmount.round().toString(),
    });
    final res = await _http.post(uri);
    _decodeResponse(res);
  }

  Future<void> renewPlayerContract(String worldId, String playerId, String coachId, int additionalRounds) async {
    final uri = Uri.parse('$baseUrl/api/worlds/$worldId/players/$playerId/renew').replace(queryParameters: {
      'coachId': coachId,
      'additionalRounds': additionalRounds.toString(),
    });
    final res = await _http.post(uri);
    _decodeResponse(res);
  }

  Future<Map<String, dynamic>> getClubFinances(String worldId, String clubId) async {
    final res = await _http.get(Uri.parse('$baseUrl/api/worlds/$worldId/clubs/$clubId/finances'));
    return _decodeResponse(res);
  }

  Future<void> takeBankLoan(String worldId, String clubId, String coachId, double amount, int rounds) async {
    final uri = Uri.parse('$baseUrl/api/worlds/$worldId/clubs/$clubId/loans').replace(queryParameters: {
      'coachId': coachId,
      'amount': amount.round().toString(),
      'rounds': rounds.toString(),
    });
    final res = await _http.post(uri);
    _decodeResponse(res);
  }

  Future<void> startStadiumRenovation(String worldId, String clubId, String coachId) async {
    final uri = Uri.parse('$baseUrl/api/worlds/$worldId/clubs/$clubId/stadium/renovate').replace(queryParameters: {
      'coachId': coachId,
    });
    final res = await _http.post(uri);
    _decodeResponse(res);
  }

  Future<void> startStadiumExpansion(String worldId, String clubId, String coachId, int seats) async {
    final uri = Uri.parse('$baseUrl/api/worlds/$worldId/clubs/$clubId/stadium/expand').replace(queryParameters: {
      'coachId': coachId,
      'additionalSeats': seats.toString(),
    });
    final res = await _http.post(uri);
    _decodeResponse(res);
  }

  Future<Map<String, dynamic>> getInvitations(String worldId, String coachId) async {
    final res = await _http.get(Uri.parse('$baseUrl/api/worlds/$worldId/invitations/$coachId'));
    return _decodeResponse(res);
  }

  Future<void> acceptInvitation(String worldId, String invitationId, String coachId) async {
    final uri = Uri.parse('$baseUrl/api/worlds/$worldId/invitations/$invitationId/accept').replace(queryParameters: {
      'coachId': coachId,
    });
    final res = await _http.post(uri);
    _decodeResponse(res);
  }

  Future<void> rejectInvitation(String worldId, String invitationId, String coachId) async {
    final uri = Uri.parse('$baseUrl/api/worlds/$worldId/invitations/$invitationId/reject')
        .replace(queryParameters: {'coachId': coachId});
    _decodeResponse(await _http.post(uri));
  }

  Future<String> exportWorldJson(String worldId) async {
    final res = await _http.get(Uri.parse('$baseUrl/api/worlds/$worldId/editor/export-json'));
    if (res.statusCode >= 400) throw Exception(res.body);
    return res.body;
  }

  Future<String> exportPlayersCsv(String worldId) async {
    final res = await _http.get(Uri.parse('$baseUrl/api/worlds/$worldId/editor/export-csv'));
    if (res.statusCode >= 400) throw Exception(res.body);
    return res.body;
  }

  Future<Map<String, dynamic>> validatePackageJson(String worldId, String packageJson) async {
    final res = await _http.post(
      Uri.parse('$baseUrl/api/worlds/$worldId/editor/validate-package'),
      headers: {'Content-Type': 'application/json'},
      body: packageJson,
    );
    return _decodeResponse(res);
  }

  Future<Map<String, dynamic>> importPackageJson(String packageJson, String newWorldName) async {
    final uri = Uri.parse('$baseUrl/api/worlds/editor/import-package')
        .replace(queryParameters: {'newWorldName': newWorldName});
    final res = await _http.post(
      uri,
      headers: {'Content-Type': 'application/json'},
      body: packageJson,
    );
    return _decodeResponse(res);
  }

  Future<Map<String, dynamic>> updatePlayerInEditor(String worldId, Map<String, dynamic> playerDto) async {
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
        if (generation == _connectionGeneration) connectRealtimeRoom(worldId, coachId);
      });
    }
    try {
      final wsBase = baseUrl.replaceFirst('http://', 'ws://').replaceFirst('https://', 'wss://');
      _wsChannel = WebSocketChannel.connect(Uri.parse('$wsBase/hubs/gameroom'));
      // SignalR JSON v1 handshake
      _wsChannel!.sink.add('{"protocol":"json","version":1}\x1e');
      // Invoke JoinRoom(worldId, coachId)
      _wsChannel!.sink.add(
        '${jsonEncode({
          'type': 1,
          'invocationId': 'join-1',
          'target': 'JoinRoom',
          'arguments': [worldId, coachId]
        })}\x1e',
      );

      _wsSub = _wsChannel!.stream.listen((raw) {
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
              } else if ((target == 'RoundStateChanged' || target == 'ClockTick') && args.isNotEmpty) {
                onSnapshotUpdated?.call(args[0] as Map<String, dynamic>);
              } else if (target == 'GlobalPause' && args.isNotEmpty) {
                final payload = args[0] as Map<String, dynamic>;
                if (payload['snapshot'] != null) {
                  onSnapshotUpdated?.call(payload['snapshot'] as Map<String, dynamic>);
                }
              } else if (target == 'MarketUpdated' && args.isNotEmpty) {
                final payload = args[0] as Map<String, dynamic>;
                onNotification?.call(payload['summaryMessage']?.toString() ?? 'Mercado atualizado');
              }
            } else if (type == 3 && msg['result'] is Map<String, dynamic>) {
              _signalRConnected = true;
              onSnapshotUpdated?.call(msg['result'] as Map<String, dynamic>);
            }
          } catch (_) {}
        }
      }, onError: (_) => reconnect(), onDone: reconnect);
      _pingTimer = Timer.periodic(const Duration(seconds: 10), (_) {
        try { _wsChannel?.sink.add('{"type":6}\x1e'); } catch (_) { reconnect(); }
      });
    } catch (_) { reconnect(); }

    // Periodic snapshot reconciliation every 600ms so reconnections or missed frames heal automatically
    _pollTimer = Timer.periodic(const Duration(milliseconds: 600), (_) async {
      if (_polling) return;
      _polling = true;
      try {
        final snap = await getSnapshot(worldId);
        if (generation == _connectionGeneration) onSnapshotUpdated?.call(snap);
      } catch (_) {} finally { _polling = false; }
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
        if (e is Exception && !e.toString().contains('FormatException')) rethrow;
      }
      throw Exception('Erro HTTP ${res.statusCode}: ${res.body}');
    }
    if (res.body.isEmpty) return {};
    return jsonDecode(res.body) as Map<String, dynamic>;
  }
}
