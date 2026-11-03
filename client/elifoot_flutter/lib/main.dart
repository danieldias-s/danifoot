import 'world_initial_lobby_016b.dart';
import 'world_catalog_status_016b.dart';
import 'import_admin_016a.dart';
import 'dart:convert';
import 'dart:math';

import 'package:flutter/material.dart';
import 'package:flutter/semantics.dart';

import 'api_client.dart';
import 'auction_config.dart';
import 'market_screen.dart';
import 'match_router.dart';
import 'morale_bar.dart';
import 'world_creator.dart';
import 'club_theme.dart';
import 'club_crest.dart';
import 'club_selection_dialog.dart';
import 'classic_layout.dart';
import 'visual_components.dart';
import 'coach_finance_screens.dart';
import 'squad_classic.dart';
import 'mobile_classic.dart';
import 'live_match_data.dart';
import 'live_match_view.dart';
import 'live_match_navigation.dart';
import 'match_interaction_dialog.dart';
import 'tactical_formation.dart';
import 'appearance_settings.dart';
import 'catalog_provenance.dart';
import 'round_navigation.dart';
import 'player_rankings.dart';
import 'session_store.dart';
import 'lobby_room_status.dart';

part 'shell_actions.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await initializeSessionStore();
  SemanticsBinding.instance.ensureSemantics();
  runApp(const ElifootClassicApp());
}

class _PlayerSilhouettePainter extends CustomPainter {
  final Color color;
  const _PlayerSilhouettePainter(this.color);

  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint()
      ..color = color
      ..style = PaintingStyle.fill;
    final cx = size.width / 2;
    canvas.drawCircle(Offset(cx, size.height * 0.34), size.width * 0.18, paint);
    final body = Path()
      ..moveTo(size.width * 0.2, size.height * 0.85)
      ..quadraticBezierTo(
        cx,
        size.height * 0.52,
        size.width * 0.8,
        size.height * 0.85,
      )
      ..close();
    canvas.drawPath(body, paint);
  }

  @override
  bool shouldRepaint(covariant _PlayerSilhouettePainter oldDelegate) =>
      oldDelegate.color != color;
}

class ElifootClassicApp extends StatefulWidget {
  final ElifootApiClient? apiClient;
  const ElifootClassicApp({super.key, this.apiClient});
  @override
  State<ElifootClassicApp> createState() => _ElifootClassicAppState();
}

class _ElifootClassicAppState extends State<ElifootClassicApp> {
  final appearance = ClubThemeController();
  late final api = widget.apiClient ?? ElifootApiClient();
  final navigation = MatchNavigationController();
  late final router = MatchRouterDelegate(
    navigation,
    ElifootShellScreen(
      apiClient: api,
      appearance: appearance,
      navigation: navigation,
    ),
  );
  @override
  void dispose() {
    router.dispose();
    navigation.dispose();
    appearance.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => ListenableBuilder(
    listenable: appearance,
    builder: (context, _) => MaterialApp.router(
      title: 'Liga Clássica 98 — 015B',
      debugShowCheckedModeBanner: false,
      theme: appearance.theme.material(),
      routerDelegate: router,
      routeInformationParser: const MatchRouteParser(),
    ),
  );
}

class ElifootShellScreen extends StatefulWidget {
  final ElifootApiClient apiClient;
  final ClubThemeController? appearance;
  final MatchNavigationController? navigation;
  const ElifootShellScreen({
    super.key,
    required this.apiClient,
    this.appearance,
    this.navigation,
  });

  @override
  State<ElifootShellScreen> createState() => _ElifootShellScreenState();
}

class _ElifootShellScreenState extends State<ElifootShellScreen> {
  // Session & World state
  String _nickname = 'Treinador 1';
  String _deviceId =
      'device-${DateTime.now().microsecondsSinceEpoch}-${Random().nextInt(1 << 30)}';
  late final TextEditingController _nicknameController;
  late final TextEditingController _deviceController;
  List<dynamic> _calendar = [];
  String? _coachId;
  String? _authenticatedDeviceId;
  List<Map<String, dynamic>> _savedSessions = [];
  String? _authWarning;
  String? _worldId;
  String? _myClubId;
  bool _isUnemployed = false;

  List<dynamic> _worlds = [];
  Map<String, dynamic>? _snapshot;
  Map<String, dynamic>? _myClubDetail;
  List<dynamic> _standings = [];
  String _selectedDivisionTier = 'PrimeiraDivisao';
  List<dynamic> _cupFixtures = [];

  /// MATCH TEAM COLORS directory (014C rebalance): registered club palettes by
  /// club id and by name, used to paint team blocks in cup/calendar lists.
  /// Refreshed with the secondary queries; failures never mix rooms.
  final Map<String, Map<String, dynamic>> _clubPaletteById = {};
  final Map<String, Map<String, dynamic>> _clubPaletteByName = {};
  String? _clubPaletteWorld;
  final Map<String, Map<String, dynamic>> _liveClubs = {};
  final _liveSelection = LiveMatchSelection();

  Map<String, dynamic>? _teamPalette({dynamic id, dynamic name}) {
    if (_clubPaletteWorld != _worldId) return null;
    // An explicit fixture ID is authoritative; never substitute a namesake.
    if (id != null) return _clubPaletteById[id.toString()];
    return _clubPaletteByName[name?.toString()];
  }

  Future<void> _loadClubPalettes(String worldId) async {
    // Stadium capacity and club metadata can change between rounds.
    try {
      final clubs = await widget.apiClient.getClubs(worldId);
      if (!mounted || _worldId != worldId) return;
      setState(() {
        _clubPaletteWorld = worldId;
        _clubPaletteById.clear();
        _clubPaletteByName.clear();
        _liveClubs.clear();
        final duplicateNames = <String>{};
        final seenNames = <String>{};
        for (final c in clubs) {
          if (c is Map &&
              c['name'] != null &&
              !seenNames.add(c['name'].toString())) {
            duplicateNames.add(c['name'].toString());
          }
        }
        for (final c in clubs) {
          if (c is Map && c['id'] != null) {
            _liveClubs['${c['id']}'] = Map<String, dynamic>.from(c);
          }
          if (c is! Map || c['colorPalette'] is! Map) continue;
          final p = Map<String, dynamic>.from(c['colorPalette'] as Map);
          _clubPaletteById[c['id'].toString()] = p;
          if (c['name'] != null &&
              !duplicateNames.contains(c['name'].toString())) {
            _clubPaletteByName[c['name'].toString()] = p;
          }
        }
      });
    } catch (_) {
      // Team blocks fall back to neutral; gameplay data is unaffected.
    }
  }

  List<dynamic> _topScorers = [];
  Map<String, dynamic>? _marketData;
  Map<String, dynamic>? _financesData;
  Map<String, dynamic>? _rankingData;
  Map<String, dynamic>? _invitationsData;

  // Squad & Tactic preparation state
  String _selectedFormation = '4-4-2';
  List<String> _selectedStarterIds = [];
  List<String> _selectedBenchIds = [];
  String _squadPositionFilter = 'ALL';
  String _squadStatusFilter = 'ALL';
  String _squadSortBy = 'DEFAULT';
  String? _swapStarterId;
  String? _swapReserveId;

  // Competition & Market filters
  String _selectedCupStage = 'ALL';
  String _selectedCalendarFilter = 'ALL';
  final _marketPreferences = MarketPreferences();

  // In-match substitution selection state
  String? _matchInteractionNotice;
  String? _lastInteractionId;

  // Editor state

  final _shellScaffoldKey = GlobalKey<ScaffoldState>();
  bool _mobileTacticsView = false;
  int _activeTabIndex = 0;
  bool _showMatchView = false;
  bool _appearanceOpen = false;
  String _competitionTab = 'standings';
  final _tacticsPanelKey = GlobalKey();
  final _roundView = RoundViewCoordinator();
  late final _navigation = widget.navigation ?? MatchNavigationController();
  bool _restoringSession = false;
  bool get _matchLocked =>
      RoundViewCoordinator.isLiveMatch(_snapshot?['roundState']?.toString());
  void _onNavigationRequest() {
    if (!mounted) return;
    setState(() {
      _showMatchView = _matchLocked || _navigation.path == '/partida';
      final index = MatchNavigationController.paths.indexOf(_navigation.path);
      if (!_matchLocked && index >= 0) _activeTabIndex = index;
    });
  }

  void _navigateToTab(int index) {
    if (_matchLocked) {
      _navigation.navigate('/partida');
      return;
    }
    setState(() {
      _appearanceOpen = false;
    });
    _navigation.navigate(MatchNavigationController.paths[index]);
    _refreshClubAndSecondaryData();
  }

  String? _statusBanner;
  bool _isLoading = false;
  bool _commandBusy = false;
  final Set<String> _shownSeasonAwards = {};

  String? _swapPickedPlayerId;
  static const List<String> standardFormations = [
    '4-4-2',
    '4-3-3',
    '3-4-3',
    '5-3-2',
    '5-4-1',
    '4-5-1',
    '3-5-2',
    '4-2-4',
    '3-6-1',
    '5-0-5',
  ];
  List<String> get availableFormations => [
    ...standardFormations.where(
      (shape) => shape != '5-0-5' || _myClubDetail?['allowClassic505'] != false,
    ),
    if (!standardFormations.contains(_selectedFormation) &&
        isValidTacticalFormation(_selectedFormation))
      _selectedFormation,
  ];

  @override
  void initState() {
    super.initState();
    _navigation.addListener(_onNavigationRequest);
    final saved = readSession();
    Map<String, dynamic>? session;
    try {
      if (saved != null) session = jsonDecode(saved);
    } catch (_) {}
    if (session?['liveSelection'] is Map) {
      _liveSelection.restore(
        Map<String, dynamic>.from(session!['liveSelection'] as Map),
      );
    }
    _restoringSession = session?['worldId'] != null;
    _nickname = session?['nickname'] ?? _nickname;
    _deviceId = session?['deviceId'] ?? _deviceId;
    _coachId = session?['coachId'];
    _authenticatedDeviceId = session?['deviceId'];
    _savedSessions = readSavedSessions();
    _nicknameController = TextEditingController(text: _nickname);
    _deviceController = TextEditingController(text: _deviceId);
    widget.apiClient.onSnapshotUpdated = (snap) {
      if (!mounted) return;
      if (_worldId != null &&
          snap['gameWorldId'] != null &&
          snap['gameWorldId'] != _worldId) {
        return;
      }
      if ((_snapshot?['roundVersion'] as num? ?? 0) >
          (snap['roundVersion'] as num? ?? 0)) {
        return;
      }
      final previousClub = _myClubId;
      final prevState = _snapshot?['roundState'];
      final newState = snap['roundState'];
      setState(() {
        _snapshot = _acceptSnapshot(snap);
        final people =
            snap['readyCounter']?['participants'] as List<dynamic>? ?? [];
        final me = people.where((p) => p['coachId'] == _coachId).firstOrNull;
        if (me != null) {
          _myClubId = me['clubId']?.toString();
          _isUnemployed = me['isUnemployed'] == true;
          if (_isUnemployed) _myClubDetail = null;
        }
      });
      if (previousClub != _myClubId) {
        widget.appearance?.activate(
          widget.apiClient,
          _coachId,
          _worldId,
          _myClubId,
          null,
        );
        _refreshClubAndSecondaryData();
      }
      if (prevState != newState &&
          (newState == 'PREPARING' || newState == 'RESULTS')) {
        _refreshClubAndSecondaryData();
      }
    };
    widget.apiClient.onReadyCounterUpdated = (counter) {
      if (!mounted || _snapshot == null) return;
      if (counter['gameWorldId'] != null &&
          counter['gameWorldId'] != _worldId) {
        return;
      }
      if ((_snapshot?['roundVersion'] as num? ?? 0) >
          (counter['roundVersion'] as num? ?? 0)) {
        return;
      }
      setState(() {
        _snapshot = {..._snapshot!, 'readyCounter': counter};
      });
    };
    widget.apiClient.onNotification = (msg) {
      if (!mounted) return;
      setState(() => _statusBanner = msg);
      _refreshClubAndSecondaryData();
    };
    if (session?['worldId'] != null) {
      WidgetsBinding.instance.addPostFrameCallback((_) async {
        try {
          await _performLocalLoginAndLoadWorlds();
          await _joinSelectedWorld(session!['worldId']);
        } finally {
          if (mounted) setState(() => _restoringSession = false);
        }
      });
    }
  }

  @override
  void dispose() {
    _navigation.removeListener(_onNavigationRequest);
    if (widget.navigation == null) _navigation.dispose();
    _nicknameController.dispose();
    _deviceController.dispose();
    widget.apiClient.disconnectRealtime();
    super.dispose();
  }

  Future<void> _performLocalLoginAndLoadWorlds() async {
    setState(() {
      _isLoading = true;
      _statusBanner = null;
    });
    try {
      final auth = await widget.apiClient.localLogin(_nickname, _deviceId);
      final worlds = await widget.apiClient.listWorlds();
      setState(() {
        _coachId = _authenticatedDeviceId == _deviceId && _coachId != null
            ? _coachId
            : auth['coachId']?.toString();
        _deviceId = auth['deviceId']?.toString() ?? _deviceId.trim();
        _authenticatedDeviceId = _deviceId;
        _deviceController.text = _deviceId;
        _authWarning = auth['authModeWarning']?.toString();
        _worlds = worlds;
      });
      await _saveSession();
    } catch (e) {
      setState(() => _statusBanner = 'Erro ao conectar na API: $e');
    } finally {
      if (mounted) setState(() => _isLoading = false);
    }
  }

  Future<void> _openCatalogCreator() async {
    if (_coachId == null || _matchLocked) return;
    final created = await Navigator.push<bool>(
      context,
      MaterialPageRoute(
        builder: (_) => WorldCreatorScreen(
          api: widget.apiClient,
          coachId: _coachId!,
          worldCatalogRules: true,
        ),
      ),
    );
    if (created == true) {
      final worlds = await widget.apiClient.listWorlds();
      if (mounted) setState(() => _worlds = worlds);
    }
  }

  Future<void> _joinSelectedWorld(
    String worldId, {
    String? preferredClubId,
    bool joinUnemployed = false,
  }) async {
    if (_coachId == null) return;
    setState(() => _isLoading = true);
    try {
      final res = await widget.apiClient.joinWorld(
        worldId: worldId,
        coachId: _coachId!,
        nickname: _nickname,
        deviceId: _deviceId,
        preferredClubId: preferredClubId,
        joinUnemployed: joinUnemployed,
      );
      final snap = res['snapshot'] as Map<String, dynamic>;
      setState(() {
        if (_worldId != worldId) {
          _snapshot = null;
          _roundView.reset();
        }
        _worldId = worldId;
        _coachId = res['coachId']?.toString() ?? _coachId;
        _nickname = res['nickname']?.toString() ?? _nickname;
        _nicknameController.text = _nickname;
        _myClubId = res['clubId']?.toString();
        _isUnemployed = res['isUnemployed'] == true;
        _snapshot = _acceptSnapshot(snap);
      });
      widget.apiClient.connectRealtimeRoom(worldId, _coachId!);
      await _saveSession();
      await _refreshClubAndSecondaryData();
    } catch (e) {
      setState(
        () => _statusBanner =
            'Não foi possível entrar: ${e.toString().replaceFirst(RegExp(r'^Exception: '), '')}',
      );
    } finally {
      if (mounted) {
        setState(() {
          _isLoading = false;
          _restoringSession = false;
        });
      }
    }
  }

  Future<void> _refreshClubAndSecondaryData() async {
    if (_worldId == null) return;
    final appearanceWorld = _worldId, appearanceClub = _myClubId;
    try {
      final standings = await widget.apiClient.getStandings(
        _worldId!,
        _selectedDivisionTier,
      );
      final cup = await widget.apiClient.getCupFixtures(_worldId!);
      final scorers = await widget.apiClient.getTopScorers(_worldId!);
      final market = await widget.apiClient.getMarket(_worldId!);
      final ranking = await widget.apiClient.getRankingAndHistory(_worldId!);
      final calendar = await widget.apiClient.getCalendar(_worldId!);
      await _loadClubPalettes(_worldId!);

      Map<String, dynamic>? clubDetail;
      Map<String, dynamic>? finances;
      if (_myClubId != null && _myClubId!.isNotEmpty) {
        clubDetail = await widget.apiClient.getClubDetail(
          _worldId!,
          _myClubId!,
        );
        finances = await widget.apiClient.getClubFinances(
          _worldId!,
          _myClubId!,
        );
      }

      Map<String, dynamic>? invites;
      if (_coachId != null) {
        invites = await widget.apiClient.getInvitations(_worldId!, _coachId!);
      }

      if (!mounted ||
          _worldId != appearanceWorld ||
          _myClubId != appearanceClub) {
        return;
      }
      setState(() {
        _standings = standings;
        _cupFixtures = cup;
        _topScorers = scorers;
        _marketData = market;
        _rankingData = ranking;
        _invitationsData = invites;
        _calendar = calendar;
        if (clubDetail != null) {
          _myClubDetail = clubDetail;
          ClubNameDirectory.register(clubDetail['id'], clubDetail['name']);
          if (_selectedStarterIds.isEmpty ||
              !_selectedStarterIds.every(
                (id) =>
                    (clubDetail!['squad'] as List).any((p) => p['id'] == id),
              )) {
            _selectedFormation =
                clubDetail['preferredFormation']?.toString() ?? '4-4-2';
            _selectedStarterIds =
                (clubDetail['preferredStarterIds'] as List<dynamic>? ?? [])
                    .map((e) => e.toString())
                    .toList();
            _selectedBenchIds =
                (clubDetail['preferredBenchIds'] as List<dynamic>? ?? [])
                    .map((e) => e.toString())
                    .toList();
          }
        }
        if (finances != null) {
          _financesData = finances;
        }
      });
      await widget.appearance?.activate(
        widget.apiClient,
        _coachId,
        _worldId,
        _myClubId,
        clubDetail?['colorPalette'] is Map
            ? Map<String, dynamic>.from(clubDetail!['colorPalette'] as Map)
            : null,
      );
    } catch (e) {
      if (mounted) setState(() => _statusBanner = 'Aviso: $e');
    }
  }

  Map<String, dynamic>? _acceptSnapshot(Map<String, dynamic>? incoming) {
    final next = _preferLatestSnapshot(incoming);
    if (next != null) {
      final interactionId =
          (next['pendingPenalty']?['decisionId'] ??
                  next['activePauseWindow']?['pauseWindowId'])
              ?.toString();
      if (interactionId != _lastInteractionId) {
        _lastInteractionId = interactionId;
        _matchInteractionNotice = null;
        if (interactionId != null) {
          WidgetsBinding.instance.addPostFrameCallback((_) {
            if (mounted && _lastInteractionId == interactionId) {
              _shellScaffoldKey.currentState?.closeDrawer();
              Navigator.of(context).popUntil((route) => route.isFirst);
            }
          });
        }
      }
      final wasLocked = _navigation.locked;
      _navigation.acceptStage(next['roundState']?.toString());
      if (_navigation.locked && !wasLocked) {
        WidgetsBinding.instance.addPostFrameCallback((_) {
          if (mounted && _navigation.locked) {
            _shellScaffoldKey.currentState?.closeDrawer();
            Navigator.of(context).popUntil((route) => route.isFirst);
          }
        });
      }
      switch (_roundView.accept(next)) {
        case RoundViewDecision.match:
          _showMatchView = true;
          _navigation.navigate('/partida');
        case RoundViewDecision.tactics:
          _showMatchView = false;
          _activeTabIndex = 0;
          _navigation.navigate('/plantel');
        case RoundViewDecision.none:
          break;
      }
    }
    final previousSeason = (_snapshot?['seasonNumber'] as num?)?.toInt();
    final nextSeason = (next?['seasonNumber'] as num?)?.toInt();
    final worldId = _worldId;
    if (previousSeason != null &&
        nextSeason != null &&
        nextSeason > previousSeason) {
      _clubPaletteWorld = null;
    }
    if (worldId != null &&
        previousSeason != null &&
        nextSeason != null &&
        nextSeason > previousSeason &&
        _shownSeasonAwards.add('$worldId:${nextSeason - 1}')) {
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (!mounted || _worldId != worldId) return;
        showSeasonTopScorersDialog(
          context: context,
          apiClient: widget.apiClient,
          worldId: worldId,
          season: nextSeason - 1,
        );
      });
    }
    return next;
  }

  int get _requiredStarterCount =>
      (_myClubDetail?['requiredStarterCount'] as num?)?.toInt() ?? 11;

  bool get _tacticsLocked =>
      _snapshot?['roundState'] != 'PREPARING' ||
      (_snapshot?['readyCounter']?['participants'] as List? ?? []).any(
        (p) => p['coachId'] == _coachId && p['isReady'] == true,
      );

  bool get _isClassicIdleRound =>
      _snapshot?['forceRulesVersion'] == 1 &&
      _myClubId != null &&
      !((_snapshot?['matches'] as List<dynamic>?) ?? []).any(
        (m) =>
            m['homeClubId']?.toString() == _myClubId ||
            m['awayClubId']?.toString() == _myClubId,
      );

  Future<void> _handleSendTacticsOrReady() async {
    if (_worldId == null || _coachId == null || _snapshot == null) return;
    final roundVersion = (_snapshot!['roundVersion'] as num?)?.toInt() ?? 1;
    try {
      Map<String, dynamic> res;
      if (_isUnemployed || _myClubId == null || _isClassicIdleRound) {
        res = await widget.apiClient.submitUnemployedReady(
          worldId: _worldId!,
          coachId: _coachId!,
          roundVersion: roundVersion,
        );
      } else {
        res = await widget.apiClient.submitTactics(
          worldId: _worldId!,
          coachId: _coachId!,
          formation: _selectedFormation,
          starterIds: _selectedStarterIds,
          benchIds: _selectedBenchIds,
          roundVersion: roundVersion,
        );
      }
      final counter = res['counter'] as Map<String, dynamic>?;

      final snap = await widget.apiClient.getSnapshot(_worldId!);
      setState(() {
        _snapshot = _acceptSnapshot(snap);
        if (counter != null &&
            (counter['roundVersion'] as num? ?? 0) >=
                (_snapshot?['roundVersion'] as num? ?? 0)) {
          _snapshot!['readyCounter'] = counter;
        }
        _statusBanner =
            RoundViewCoordinator.isMatchStage(
              _snapshot?['roundState']?.toString(),
            )
            ? 'RODADA INICIADA PELO SERVIDOR'
            : 'TÁTICA ENVIADA. AGUARDANDO OUTROS TREINADORES.';
      });
    } catch (e) {
      setState(() => _statusBanner = 'Erro ao enviar tática: $e');
    }
  }

  Future<void> _handleWithdrawReady() async {
    if (_worldId == null || _coachId == null || _snapshot == null) return;
    final roundVersion = (_snapshot!['roundVersion'] as num?)?.toInt() ?? 1;
    try {
      final res = await widget.apiClient.withdrawReady(
        worldId: _worldId!,
        coachId: _coachId!,
        roundVersion: roundVersion,
      );
      setState(() {
        _snapshot = {..._snapshot!, 'readyCounter': res['counter']};
        _statusBanner = 'Pronto retirado. Você pode alterar a escalação.';
      });
    } catch (e) {
      setState(() => _statusBanner = 'Erro ao retirar pronto: $e');
    }
  }

  Future<void> _handleRequestGlobalPause() async {
    if (_worldId == null || _coachId == null) return;
    try {
      await widget.apiClient.requestGlobalPause(_worldId!, _coachId!);
      final snap = await widget.apiClient.getSnapshot(_worldId!);
      setState(() {
        _snapshot = _acceptSnapshot(snap);
        _statusBanner =
            'PARADA GLOBAL ATIVADA! Todas as partidas da sala foram congeladas.';
      });
    } catch (e) {
      setState(() => _statusBanner = 'Parada recusada: $e');
    }
  }

  Future<void> _handleConfirmContinue() async {
    if (_worldId == null || _coachId == null) return;
    try {
      final res = await widget.apiClient.confirmContinue(_worldId!, _coachId!);
      setState(() {
        _snapshot = _acceptSnapshot(res['snapshot'] as Map<String, dynamic>?);
        _statusBanner = res['resumed'] == true
            ? 'Partidas retomadas!'
            : 'Confirmação registrada. Aguardando demais técnicos...';
      });
    } catch (e) {
      setState(() => _statusBanner = 'Erro ao continuar: $e');
    }
  }

  @override
  Widget build(BuildContext context) {
    if (_restoringSession) {
      return const Scaffold(body: Center(child: CircularProgressIndicator()));
    }
    if (_coachId == null || _worldId == null) {
      return _buildLobbyAndHomeScreen();
    }

    if (_snapshot?['hasInitialLobby'] == true &&
        _snapshot?['lobbyStarted'] != true) {
      return WorldInitialLobby016B(
        api: widget.apiClient,
        worldId: _worldId!,
        coachId: _coachId!,
        onLeave: () async {
          widget.apiClient.disconnectRealtime();
          setState(() {
            _worldId = null;
            _snapshot = null;
            _myClubId = null;
            _myClubDetail = null;
          });
          await _saveSession();
        },
        onStarted: () async {
          final snapshot = await widget.apiClient.getSnapshot(_worldId!);
          if (!mounted) return;
          final participants =
              snapshot['readyCounter']?['participants'] as List? ?? [];
          final me = participants
              .where((p) => p['coachId'] == _coachId)
              .firstOrNull;
          setState(() {
            _snapshot = _acceptSnapshot(snapshot);
            _myClubId = me?['clubId']?.toString();
            _isUnemployed = me?['isUnemployed'] == true;
          });
          await _refreshClubAndSecondaryData();
        },
      );
    }

    return LayoutBuilder(
      builder: (context, viewport) {
        final desktop = viewport.maxWidth >= 1024;
        return PopScope(
          canPop: !_matchLocked,
          child: Scaffold(
            key: _shellScaffoldKey,
            drawerEnableOpenDragGesture: !_matchLocked,
            bottomNavigationBar: desktop
                ? null
                : _buildIntegratedBottomNavigation(viewport.maxWidth),
            drawer: desktop
                ? null
                : Drawer(
                    width: 250,
                    child: SafeArea(child: _buildClassicNavigationMenu()),
                  ),
            body: MatchInteractionOverlay(
              interaction: _buildMatchInteraction(),
              child: SafeArea(
                child: Column(
                  children: [
                    ConstrainedBox(
                      constraints: BoxConstraints(
                        maxHeight: MediaQuery.sizeOf(context).height * 0.42,
                      ),
                      child: SingleChildScrollView(
                        child: _buildTopSynchronizedHeader(viewport.maxWidth),
                      ),
                    ),
                    if (_statusBanner != null)
                      Container(
                        width: double.infinity,
                        color: ClassicTheme.of(context).surfaceAlternate,
                        padding: const EdgeInsets.symmetric(
                          horizontal: 12,
                          vertical: 4,
                        ),
                        child: Row(
                          children: [
                            Text(
                              '[!]',
                              style: TextStyle(
                                fontSize: 12,
                                fontWeight: FontWeight.bold,
                                color: ClassicTheme.of(context).accent,
                              ),
                            ),
                            const SizedBox(width: 8),
                            Expanded(
                              child: Text(
                                _statusBanner!,
                                style: TextStyle(
                                  fontSize: 12,
                                  color: ClassicTheme.of(context).textPrimary,
                                ),
                              ),
                            ),
                            InkWell(
                              onTap: () => setState(() => _statusBanner = null),
                              child: Padding(
                                padding: EdgeInsets.symmetric(
                                  horizontal: 6,
                                  vertical: 2,
                                ),
                                child: Text(
                                  '[X]',
                                  style: TextStyle(
                                    fontSize: 11,
                                    fontWeight: FontWeight.bold,
                                    color: ClassicTheme.of(context).accent,
                                  ),
                                ),
                              ),
                            ),
                          ],
                        ),
                      ),
                    if ((_snapshot?['roundState'] == 'PREPARING' ||
                            _snapshot?['roundState'] == 'ALL_READY') &&
                        ((_snapshot?['readyCounter']?['participants']
                                    as List? ??
                                [])
                            .any(
                              (p) =>
                                  p['coachId'] == _coachId &&
                                  p['isReady'] == true,
                            )))
                      Container(
                        key: const Key('ready-waiting-panel'),
                        width: double.infinity,
                        padding: const EdgeInsets.all(8),
                        color: ClassicTheme.of(context).surfaceAlternate,
                        child: Text(
                          _isUnemployed
                              ? 'PRONTO PARA ACOMPANHAR. AGUARDANDO OUTROS TREINADORES.'
                              : 'TÁTICA ENVIADA. AGUARDANDO OUTROS TREINADORES.',
                          style: TextStyle(
                            color: ClassicTheme.of(context).accent,
                            fontWeight: FontWeight.bold,
                          ),
                        ),
                      ),
                    if ((_showMatchView || _matchLocked) &&
                        viewport.maxWidth >= 768)
                      LiveMatchNavigation(
                        locked: _matchLocked,
                        onSelected: (idx) {
                          if (idx == -1) {
                            _navigation.navigate('/partida');
                          } else {
                            _navigateToTab(idx);
                          }
                        },
                        switchRoom: _matchLocked ? null : _switchRoom,
                      ),
                    Expanded(
                      child: Row(
                        crossAxisAlignment: CrossAxisAlignment.stretch,
                        children: [
                          if (desktop && !_showMatchView && !_matchLocked)
                            _buildClassicNavigationMenu(),
                          Expanded(
                            child: Column(
                              children: [
                                if (!_matchLocked &&
                                    !_showMatchView &&
                                    !_appearanceOpen)
                                  _buildClubSummaryStrip(viewport.maxWidth),
                                Expanded(child: _buildActiveScreenBody()),
                              ],
                            ),
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),
        );
      },
    );
  }

  /// Screen 1 & 2: Início, Login LocalDev, Novo Campeonato, Entrar em Sala e Seleção de Clube
  Widget _buildLobbyAndHomeScreen() {
    return Scaffold(
      appBar: AppBar(
        backgroundColor: ClassicTheme.of(context).surface,
        title: Text(
          'ELIFOOT CLASSIC MULTIPLAYER — EDIÇÃO 98',
          style: TextStyle(
            color: ClassicTheme.of(context).accent,
            fontWeight: FontWeight.bold,
            fontSize: 15,
          ),
        ),
        actions: [
          if (_coachId != null)
            Center(
              child: Container(
                margin: const EdgeInsets.symmetric(horizontal: 12),
                padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                decoration: BoxDecoration(
                  color: ClassicTheme.of(context).buttonBackground,
                  border: Border.all(
                    color: ClassicTheme.of(context).selectedBackground,
                  ),
                ),
                child: Text(
                  'Técnico: $_nickname',
                  style: const TextStyle(
                    fontSize: 11,
                    fontWeight: FontWeight.bold,
                  ),
                ),
              ),
            ),
        ],
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(16),
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 960),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Container(
                  padding: const EdgeInsets.all(16),
                  decoration: BoxDecoration(
                    color: ClassicTheme.of(context).surface,
                    border: Border.all(
                      color: ClassicTheme.of(context).selectedBackground,
                      width: 2,
                    ),
                  ),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'GESTÃO CLÁSSICA DE FUTEBOL • MULTIPLAYER ATÉ 8 TÉCNICOS • MOTOR AUTORITATIVO',
                        style: TextStyle(
                          color: ClassicTheme.of(context).accent,
                          fontWeight: FontWeight.bold,
                          fontSize: 14,
                        ),
                      ),
                      const SizedBox(height: 8),
                      Text(
                        '• 128 Clubes no Brasil (4 Divisões de 8 + 96 Distritais na Taça)\n'
                        '• Contador Sincronizado (TÁTICAS ENVIADAS: 0/6) e Parada Global compartilhada\n'
                        '• Técnicos de IA com filosofia tática persistente e mercado bilateral racional',
                        style: TextStyle(
                          fontSize: 12,
                          color: ClassicTheme.of(context).textSecondary,
                        ),
                      ),
                      const SizedBox(height: 12),
                      if (_savedSessions.isNotEmpty) ...[
                        DropdownButtonFormField<String>(
                          key: ValueKey('saved-trainer-$_deviceId'),
                          initialValue:
                              _savedSessions.any(
                                (p) => p['deviceId'] == _deviceId,
                              )
                              ? _deviceId
                              : null,
                          isExpanded: true,
                          decoration: const InputDecoration(
                            labelText: 'RETOMAR TREINADOR SALVO',
                          ),
                          items: [
                            for (final p in _savedSessions)
                              DropdownMenuItem(
                                value: p['deviceId'].toString(),
                                child: Text(
                                  '${p['nickname']} • ${p['deviceId']}',
                                  overflow: TextOverflow.ellipsis,
                                ),
                              ),
                          ],
                          onChanged: _isLoading
                              ? null
                              : (device) async {
                                  final p = _savedSessions.firstWhere(
                                    (p) => p['deviceId'] == device,
                                  );
                                  setState(() {
                                    _nickname =
                                        p['nickname']?.toString() ??
                                        'Treinador';
                                    _deviceId = device!;
                                    _coachId = p['coachId']?.toString();
                                    _authenticatedDeviceId = device;
                                    _nicknameController.text = _nickname;
                                    _deviceController.text = _deviceId;
                                    _statusBanner = null;
                                    _worlds = [];
                                  });
                                  await _performLocalLoginAndLoadWorlds();
                                },
                        ),
                        const Text(
                          'Use a mesma identidade para retornar. Desconectar preserva sua vaga e seu clube.',
                        ),
                        const SizedBox(height: 8),
                      ],
                      Wrap(
                        spacing: 12,
                        runSpacing: 12,
                        crossAxisAlignment: WrapCrossAlignment.center,
                        children: [
                          SizedBox(
                            width: 220,
                            child: TextField(
                              decoration: const InputDecoration(
                                labelText: 'Apelido do Treinador (Nickname)',
                                isDense: true,
                                border: OutlineInputBorder(
                                  borderRadius: BorderRadius.zero,
                                ),
                              ),
                              controller: _nicknameController,
                              readOnly: _isLoading,
                              onChanged: (v) => _nickname = v,
                            ),
                          ),
                          SizedBox(
                            width: 190,
                            child: TextField(
                              decoration: const InputDecoration(
                                labelText: 'ID Dispositivo (Sessão)',
                                isDense: true,
                                border: OutlineInputBorder(
                                  borderRadius: BorderRadius.zero,
                                ),
                              ),
                              controller: _deviceController,
                              readOnly: _isLoading,
                              onChanged: (v) => _deviceId = v,
                            ),
                          ),
                          ElevatedButton(
                            onPressed: _isLoading
                                ? null
                                : _performLocalLoginAndLoadWorlds,
                            child: const Text('ENTRAR NO LOBBY (LOCAL DEV)'),
                          ),
                          OutlinedButton(
                            key: const Key('new-local-trainer'),
                            onPressed: _isLoading
                                ? null
                                : () {
                                    setState(() {
                                      _coachId = null;
                                      _authenticatedDeviceId = null;
                                      _deviceId =
                                          'device-${DateTime.now().microsecondsSinceEpoch}-${Random().nextInt(1 << 30)}';
                                      _nickname = 'Treinador 1';
                                      _nicknameController.text = _nickname;
                                      _deviceController.text = _deviceId;
                                      _worlds = [];
                                      _authWarning = null;
                                    });
                                  },
                            child: const Text('NOVO TREINADOR'),
                          ),
                        ],
                      ),
                      if (_authWarning != null) ...[
                        const SizedBox(height: 8),
                        Container(
                          padding: const EdgeInsets.all(6),
                          color: Colors.amber.withValues(alpha: 0.15),
                          child: Text(
                            _authWarning!,
                            style: const TextStyle(
                              color: Colors.amberAccent,
                              fontSize: 11,
                            ),
                          ),
                        ),
                      ],
                      if (_statusBanner != null) ...[
                        const SizedBox(height: 8),
                        Text(
                          _statusBanner!,
                          style: const TextStyle(
                            color: Colors.redAccent,
                            fontSize: 12,
                          ),
                        ),
                      ],
                    ],
                  ),
                ),
                const SizedBox(height: 16),
                if (_coachId != null) ...[
                  Wrap(
                    spacing: 12,
                    runSpacing: 8,
                    crossAxisAlignment: WrapCrossAlignment.center,
                    children: [
                      Text(
                        'SALAS / MUNDOS DISPONÍVEIS',
                        style: TextStyle(
                          color: ClassicTheme.of(context).accent,
                          fontWeight: FontWeight.bold,
                          fontSize: 15,
                        ),
                      ),
                      Wrap(
                        spacing: 8,
                        runSpacing: 8,
                        children: [
                          ElevatedButton(
                            onPressed: _openCatalogCreator,
                            child: const Text('NOVO CAMPEONATO / SALA'),
                          ),
                          OutlinedButton(
                            onPressed: _performLocalLoginAndLoadWorlds,
                            child: const Text('ATUALIZAR'),
                          ),
                          OutlinedButton(
                            onPressed: _joinByCodeDialog,
                            child: const Text('ENTRAR POR CÓDIGO'),
                          ),
                          OutlinedButton(
                            onPressed: () => Navigator.of(context).push(
                              MaterialPageRoute(
                                builder: (_) =>
                                    ImportAdmin016A(api: widget.apiClient),
                              ),
                            ),
                            child: const Text('DADOS DE CLUBES E JOGADORES'),
                          ),
                          OutlinedButton(
                            onPressed: () => Navigator.push(
                              context,
                              MaterialPageRoute(
                                builder: (_) => WorldCatalogStatus016B(
                                  api: widget.apiClient,
                                  coachId: _coachId!,
                                ),
                              ),
                            ),
                            child: const Text(
                              'CATÁLOGO MUNDIAL • ESTADO POR PAÍS',
                            ),
                          ),
                        ],
                      ),
                    ],
                  ),
                  const SizedBox(height: 8),
                  if (_worlds.isEmpty)
                    const Padding(
                      padding: EdgeInsets.all(24),
                      child: Text(
                        'Nenhuma sala encontrada. Clique em NOVO CAMPEONATO / SALA.',
                      ),
                    )
                  else
                    ..._worlds.map((w) {
                      final wid = w['id'].toString();
                      return Container(
                        margin: const EdgeInsets.only(bottom: 10),
                        padding: const EdgeInsets.all(12),
                        decoration: BoxDecoration(
                          color: ClassicTheme.of(context).surface,
                          border: Border.all(
                            color: ClassicTheme.of(context).border,
                          ),
                        ),
                        child: Wrap(
                          spacing: 8,
                          runSpacing: 8,
                          crossAxisAlignment: WrapCrossAlignment.center,
                          children: [
                            SizedBox(
                              width: MediaQuery.sizeOf(context).width < 700
                                  ? MediaQuery.sizeOf(context).width - 64
                                  : 440,
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Text(
                                    '${w['name']}  [Código: ${w['inviteCode']}]',
                                    style: TextStyle(
                                      color: ClassicTheme.of(context).accent,
                                      fontWeight: FontWeight.bold,
                                      fontSize: 14,
                                    ),
                                  ),
                                  const SizedBox(height: 4),
                                  Text(
                                    'Temporada ${w['seasonNumber']} • Rodada ${w['roundNumber']} • Clubes: ${w['totalClubs']}',
                                  ),
                                  LobbyRoomStatus(
                                    room: Map<String, dynamic>.from(w),
                                  ),
                                ],
                              ),
                            ),
                            ElevatedButton(
                              key: Key('join-world-$wid'),
                              onPressed:
                                  _isLoading ||
                                      _authenticatedDeviceId != _deviceId.trim()
                                  ? null
                                  : () => _chooseClub(wid),
                              child: Text(
                                w['reconnectCoachId'] != null
                                    ? 'RETOMAR MINHA SESSÃO'
                                    : 'ENTRAR COM CLUBE',
                              ),
                            ),
                            OutlinedButton(
                              onPressed:
                                  _isLoading ||
                                      _authenticatedDeviceId != _deviceId.trim()
                                  ? null
                                  : () => _joinSelectedWorld(
                                      wid,
                                      joinUnemployed: true,
                                    ),
                              child: const Text('ENTRAR DESEMPREGADO'),
                            ),
                          ],
                        ),
                      );
                    }),
                ],
              ],
            ),
          ),
        ),
      ),
    );
  }

  /// Persistent Top Header:
  /// Shows `TÁTICAS ENVIADAS: X/N` + names in preparation, and `MINUTO XX'`, `SUBSTITUIÇÕES X/5`, `PARADAS X/3`, `PEDIR PARADA` in match!
  Widget _buildTopSynchronizedHeader(double viewportWidth) {
    final snap = _snapshot;
    final state = snap?['roundState']?.toString() ?? 'PREPARING';
    final people = snap?['readyCounter']?['participants'] as List? ?? [];
    final me = people.where((p) => p['coachId'] == _coachId).firstOrNull;
    final ready = me?['isReady'] == true;
    final pause = snap?['activePauseWindow'] as Map?;
    final t = ClassicTheme.of(context);
    Widget action;
    if (state == 'PREPARING') {
      action = ready
          ? OutlinedButton(
              key: const Key('btn-retirar-pronto'),
              onPressed: _commandBusy
                  ? null
                  : () => _runUiCommand(_handleWithdrawReady),
              child: const Text('RETIRAR PRONTO'),
            )
          : ElevatedButton(
              key: const Key('btn-enviar-tatica'),
              onPressed:
                  _commandBusy ||
                      (!_isUnemployed &&
                          !_isClassicIdleRound &&
                          (_requiredStarterCount < 1 ||
                              _selectedStarterIds.length !=
                                  _requiredStarterCount))
                  ? null
                  : () => _runUiCommand(_handleSendTacticsOrReady),
              child: Text(
                _isUnemployed || _isClassicIdleRound
                    ? 'PRONTO PARA ACOMPANHAR'
                    : 'ENVIAR TÁTICA',
              ),
            );
    } else if (state == 'RESULTS') {
      action = ElevatedButton(
        key: const Key('btn-proxima-rodada'),
        onPressed: _commandBusy
            ? null
            : () => _runUiCommand(() async {
                final next = await widget.apiClient.advanceToNextRound(
                  _worldId!,
                );
                setState(() {
                  _snapshot = _acceptSnapshot(next);
                  _activeTabIndex = 0;
                  _selectedStarterIds = [];
                });
                await _refreshClubAndSecondaryData();
              }),
        child: const Text('AVANÇAR PRÓXIMA RODADA'),
      );
    } else if (pause != null) {
      action = ElevatedButton(
        key: const Key('btn-header-confirmar-continuar'),
        onPressed: _commandBusy
            ? null
            : () => _runUiCommand(_handleConfirmContinue),
        child: Text(
          state == 'HALFTIME_PAUSED' ? 'PRONTO PARA O 2º TEMPO' : 'CONTINUAR',
        ),
      );
    } else if (state.startsWith('RUNNING_')) {
      action = ElevatedButton(
        key: const Key('btn-pedir-parada'),
        onPressed: _commandBusy
            ? null
            : () => _runUiCommand(_handleRequestGlobalPause),
        child: const Text('SUBSTITUIR'),
      );
    } else {
      action = const Text('TEMPORADA ENCERRADA');
    }
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        AppHeader(
          round:
              'Temporada ${snap?['seasonNumber'] ?? '—'} | Rodada ${snap?['roundNumber'] ?? '—'} (${snap?['competitionLabel'] ?? ''})',
          date: formatClassicDate(snap?['gameDate']),
          live: _showMatchView || _matchLocked,
          world: (_showMatchView || _matchLocked)
              ? '${snap?['worldName'] ?? ''} • ${snap?['readyCounter']?['totalHumans'] ?? people.length} treinadores • ${_myClubDetail?['name'] ?? me?['clubName'] ?? 'Sem clube'}'
              : snap?['worldName']?.toString() ?? '',
          action: action,
          openMenu:
              viewportWidth >= 768 && viewportWidth < 1024 && !_matchLocked
              ? () => _shellScaffoldKey.currentState?.openDrawer()
              : null,
        ),
        Container(
          color: t.surface,
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
          child: SingleChildScrollView(
            scrollDirection: Axis.horizontal,
            child: Row(
              children: [
                Text(
                  snap?['readyCounter']?['counterLabel']?.toString() ??
                      'TÁTICAS ENVIADAS: —',
                  key: const Key('ready-counter-label'),
                  style: TextStyle(
                    color: t.accent,
                    fontSize: 11,
                    fontWeight: FontWeight.bold,
                  ),
                ),
                const SizedBox(width: 16),
                for (final person in people)
                  Padding(
                    padding: const EdgeInsets.only(right: 12),
                    child: Text(
                      '${person['isReady'] == true ? '[OK]' : '[..]'} ${person['nickname']} (${person['clubName'] ?? 'Sem clube'})${person['isConnected'] == false ? ' • DESCONECTADO' : ''}',
                      style: TextStyle(color: t.textSecondary, fontSize: 10),
                    ),
                  ),
                if (!_showMatchView && RoundViewCoordinator.isMatchStage(state))
                  TextButton(
                    key: const Key('return-to-match'),
                    onPressed: () => setState(() => _showMatchView = true),
                    child: const Text('VOLTAR À PARTIDA'),
                  ),
              ],
            ),
          ),
        ),
        if (state != 'PREPARING')
          Container(
            color: t.secondary,
            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
            child: Text(
              "MINUTO ${snap?['currentMinute'] ?? 0}'  |  SUBSTITUIÇÕES ${me?['usedSubstitutions'] ?? 0}/${me?['maxSubstitutions'] ?? 5}  |  PARADAS ${me?['usedPauseWindows'] ?? 0}/${me?['maxPauseWindows'] ?? 3}",
              key: const Key('live-match-quota-header'),
              style: TextStyle(fontSize: 11, color: t.textSecondary),
            ),
          ),
        if (pause != null)
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
            color: t.surfaceAlternate,
            child: Wrap(
              spacing: 12,
              runSpacing: 4,
              children: [
                Text(
                  state == 'HALFTIME_PAUSED'
                      ? "INTERVALO DE JOGO (45') — AJUSTES GRATUITOS"
                      : "PARTIDA PAUSADA POR ${pause['requestedByCoachName'] ?? 'INTERVALO'} (${pause['minute']}')",
                  style: TextStyle(
                    color: t.accent,
                    fontSize: 12,
                    fontWeight: FontWeight.bold,
                  ),
                ),
                Text(
                  "AJUSTES CONFIRMADOS: ${pause['confirmedCount']}/${pause['totalHumans']}  |  TEMPO: ${pause['remainingSeconds']}s",
                  style: TextStyle(color: t.textPrimary, fontSize: 11),
                ),
              ],
            ),
          ),
      ],
    );
  }

  Widget _buildClassicNavigationMenu() => AppSidebar(
    selected: _showMatchView || _appearanceOpen ? -1 : _activeTabIndex,
    appearanceSelected: _appearanceOpen,
    locked: _matchLocked,
    onSelected: (idx) {
      _navigateToTab(idx);
      _shellScaffoldKey.currentState?.closeDrawer();
    },
    showAppearance: widget.appearance != null,
    appearance: _matchLocked
        ? null
        : () {
            _shellScaffoldKey.currentState?.closeDrawer();
            setState(() => _appearanceOpen = true);
          },
    switchRoom: _matchLocked ? null : _switchRoom,
    liveMatches: () {
      _navigation.navigate('/partida');
      _shellScaffoldKey.currentState?.closeDrawer();
    },
  );

  Future<void> _switchRoom() async {
    if (_matchLocked) return;
    _shellScaffoldKey.currentState?.closeDrawer();
    widget.apiClient.disconnectRealtime();
    setState(() {
      _worldId = null;
      widget.appearance?.activate(widget.apiClient, null, null, null, null);
      _snapshot = null;
      _myClubDetail = null;
      _myClubId = null;
      _selectedStarterIds = [];
    });
    await _saveSession();
  }

  void _navigateMobile(int destination) {
    if (_matchLocked) return;
    if (_showMatchView) {
      if (destination == 4) {
        _shellScaffoldKey.currentState?.openDrawer();
        return;
      }
      if (destination == 1) {
        _navigation.navigate('/partida');
        return;
      }
      _navigateToTab(
        destination == 0
            ? 0
            : destination == 2
            ? 1
            : 2,
      );
      return;
    }
    if (destination == 4) {
      _shellScaffoldKey.currentState?.openDrawer();
      return;
    }
    if (destination == 0 || destination == 1) {
      setState(() => _mobileTacticsView = destination == 1);
      _navigateToTab(0);
    } else {
      _navigateToTab(destination == 2 ? 2 : 1);
    }
  }

  void _toggleSquadPlayer(Map<String, dynamic> player) {
    final pid = '${player['id']}';
    if (_tacticsLocked ||
        (player['injuryDaysRemaining'] as num? ?? 0) > 0 ||
        (player['injuryRoundsRemaining'] as num? ?? 0) > 0 ||
        (player['suspensionRoundsRemaining'] as num? ?? 0) > 0) {
      return;
    }
    setState(() {
      if (_selectedStarterIds.contains(pid)) {
        _selectedStarterIds.remove(pid);
        if (_selectedBenchIds.length < 9) _selectedBenchIds.add(pid);
      } else if (_selectedBenchIds.contains(pid)) {
        _selectedBenchIds.remove(pid);
      } else if (_selectedStarterIds.length < 11) {
        _selectedStarterIds.add(pid);
      } else if (_selectedBenchIds.length < 9) {
        _selectedBenchIds.add(pid);
      }
    });
  }

  void _swapSquadPlayers(String out, String into) {
    if (_tacticsLocked) return;
    final squad = (_myClubDetail?['squad'] as List? ?? [])
        .map((p) => Map<String, dynamic>.from(p as Map))
        .toList();
    final result = swapSquadPlayers(
      squad,
      _selectedStarterIds,
      _selectedBenchIds,
      out,
      into,
      _selectedFormation,
    );
    setState(() {
      _swapPickedPlayerId = null;
      if (result == null) {
        _statusBanner =
            'Troca inválida: escolha um reserva apto e preserve o goleiro.';
        return;
      }
      _selectedStarterIds = result.starters;
      _selectedBenchIds = result.bench;
      if (result.formation != '5-0-5' ||
          _myClubDetail?['allowClassic505'] != false) {
        _selectedFormation = result.formation;
      }
      _statusBanner = 'Troca realizada entre titular e reserva.';
    });
  }

  void _pickBestSquad(String mode) {
    if (_tacticsLocked) return;
    final squad = (_myClubDetail?['squad'] as List? ?? [])
        .map((p) => Map<String, dynamic>.from(p as Map))
        .toList();
    final result = bestSquadLineup(
      squad,
      _selectedFormation,
      preferGoals: mode == 'goals',
    );
    setState(() {
      _selectedStarterIds = result.starters;
      _selectedBenchIds = result.bench;
      if (result.formation != '5-0-5' ||
          _myClubDetail?['allowClassic505'] != false) {
        _selectedFormation = result.formation;
      }
      _swapPickedPlayerId = null;
      _statusBanner =
          'Melhores por ${mode == 'goals' ? 'gols na temporada' : 'força'} escalados. Formação $_selectedFormation.';
    });
  }

  void _pickSquadSwapPlayer(Map<String, dynamic> player) {
    if (_tacticsLocked) return;
    final id = '${player['id']}';
    final wasStarter = _selectedStarterIds.contains(_swapPickedPlayerId);
    final wasReserve = _selectedBenchIds.contains(_swapPickedPlayerId);
    final isStarter = _selectedStarterIds.contains(id),
        isReserve = _selectedBenchIds.contains(id);
    if (_swapPickedPlayerId != id &&
        ((wasStarter && isReserve) || (wasReserve && isStarter))) {
      _swapSquadPlayers(
        wasStarter ? _swapPickedPlayerId! : id,
        wasStarter ? id : _swapPickedPlayerId!,
      );
      return;
    }
    setState(() {
      _swapPickedPlayerId = _swapPickedPlayerId == id ? null : id;
      _statusBanner = _swapPickedPlayerId == null
          ? 'Seleção de troca cancelada.'
          : '${player['displayName'] ?? player['name']} (${player['strength']}) selecionado. Escolha ${isStarter ? 'um reserva' : 'um titular'} para trocar.';
    });
  }

  Future<void> _renewSquadPlayer(Map<String, dynamic> player) async {
    final world = _worldId, coach = _coachId, club = _myClubId;
    if (_commandBusy || _matchLocked || world == null || coach == null) return;
    if (player['contractRenewedForSeason'] == true) return;
    final season = (_snapshot?['seasonNumber'] as num?)?.toInt() ?? 1;
    final annual = player['seasonContractsEnabled'] == true;
    if (annual) {
      final confirmed = await showDialog<bool>(
        context: context,
        builder: (ctx) => AlertDialog(
          title: const Text('RENOVAR ATÉ O FIM DA TEMPORADA'),
          content: Text(
            '${player['displayName'] ?? player['name']}\n'
            'Salário por partida: ${formatClassicMoney(player['salaryPerRound'])}\n'
            'Luvas: ${formatClassicMoney(player['seasonRenewalFee'])}\n'
            'Validade: fim da temporada $season.\n'
            'O atleta não poderá ser vendido ou leiloado, inclusive por dívida, nesta temporada.',
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(ctx, false),
              child: const Text('CANCELAR'),
            ),
            ElevatedButton(
              key: const Key('confirm-season-renewal'),
              onPressed: () => Navigator.pop(ctx, true),
              child: const Text('RENOVAR'),
            ),
          ],
        ),
      );
      if (confirmed != true ||
          !mounted ||
          _matchLocked ||
          world != _worldId ||
          coach != _coachId ||
          club != _myClubId ||
          season != _snapshot?['seasonNumber']) {
        return;
      }
    }
    await _runUiCommand(() async {
      await widget.apiClient.renewPlayerContract(
        world,
        '${player['id']}',
        coach,
        annual ? 0 : 15,
      );
      await _refreshClubAndSecondaryData();
      if (mounted) {
        setState(
          () => _statusBanner = annual
              ? '✓ ${player['displayName']} renovado até o fim da temporada $season.'
              : 'Contrato de ${player['displayName']} renovado (+15 rodadas).',
        );
      }
    });
  }

  Widget _buildIntegratedBottomNavigation(double width) {
    if (_showMatchView || _matchLocked) {
      return MobileClassicNavigation(
        liveMode: true,
        selected: 1,
        locked: _matchLocked,
        onSelected: _navigateMobile,
      );
    }
    final selected = _appearanceOpen
        ? 4
        : (_activeTabIndex == 6 || _activeTabIndex == 4)
        ? 3
        : _activeTabIndex == 2
        ? 2
        : (_activeTabIndex == 1 || _activeTabIndex == 5)
        ? 3
        : _activeTabIndex == 0
        ? (_mobileTacticsView ? 1 : 0)
        : 4;
    final navigation = ClassicBottomNavigation(
      compactMenu: width < 768,
      showCoachRanking: _activeTabIndex == 4 && !_appearanceOpen,
      showPlayerRankings: _activeTabIndex == 6 && !_appearanceOpen,
      selected: selected,
      locked: _matchLocked,
      onSelected: (index) {
        if (index == 4) {
          _shellScaffoldKey.currentState?.openDrawer();
          return;
        }
        if (index == 0 || index == 1) {
          setState(() {
            _mobileTacticsView = index == 1;
          });
        }
        _navigateToTab(
          index == 2
              ? 2
              : index == 3
              ? (_activeTabIndex == 6
                    ? 6
                    : _activeTabIndex == 4
                    ? 4
                    : 1)
              : 0,
        );
      },
    );
    return width < 768
        ? MobileClassicNavigation(
            selected: selected,
            locked: _matchLocked,
            onSelected: _navigateMobile,
            child: navigation,
          )
        : navigation;
  }

  Widget _buildClubSummaryStrip(double viewportWidth) {
    final club = _myClubDetail;
    final round = (_snapshot?['roundNumber'] as num?)?.toInt() ?? 1;
    final next = _calendar
        .where(
          (r) =>
              (r['roundNumber'] as num? ?? 0) >=
              (round + (_snapshot?['roundState'] == 'RESULTS' ? 1 : 0)),
        )
        .firstOrNull;
    final fixture = (next?['fixtures'] as List? ?? [])
        .where((f) => f['home'] == club?['name'] || f['away'] == club?['name'])
        .firstOrNull;
    if (viewportWidth < 768 && _activeTabIndex != 6) {
      final people = _snapshot?['readyCounter']?['participants'] as List? ?? [];
      final me = people.where((p) => p['coachId'] == _coachId).firstOrNull;
      return KeyedSubtree(
        key: const Key('classic-club-summary'),
        child: MobileClubOverview(
          club: club?['name']?.toString() ?? 'TREINADOR SEM CLUBE',
          coach: _nickname,
          division: club?['divisionDisplay']?.toString() ?? 'Sem divisão',
          standing:
              'Posição: ${_standings.where((s) => s['clubId'] == _myClubId).firstOrNull?['position'] ?? '—'}',
          cash: club?['cashBalance'] is num
              ? formatClassicMoney(club!['cashBalance'])
              : '—',
          opponent: fixture == null
              ? 'A definir'
              : '${fixture['home'] == club?['name'] ? fixture['away'] : fixture['home']} • R${next?['roundNumber']}',
          morale: (me?['morale'] ?? _invitationsData?['morale'] ?? 70) as num,
          moraleBand:
              (me?['moraleBand'] ?? _invitationsData?['moraleBand'] ?? 'VERDE')
                  .toString(),
          primary: _clubColor(club?['colorPalette'], 'primaryColor'),
          secondary: _clubColor(club?['colorPalette'], 'secondaryColor'),
        ),
      );
    }
    final cards = <Widget>[
      ClassicSummaryCard(
        icon: Icons.shield_outlined,
        leading: ClubCrest(
          club: club?['name']?.toString() ?? 'TREINADOR SEM CLUBE',
          palette: club?['colorPalette'] is Map
              ? Map<String, dynamic>.from(club!['colorPalette'])
              : null,
          size: 52,
        ),
        title: club?['name']?.toString() ?? 'TREINADOR SEM CLUBE',
        subtitle: _nickname,
      ),
      ClassicSummaryCard(
        icon: Icons.emoji_events_outlined,
        title: club?['divisionDisplay']?.toString() ?? 'Sem divisão',
        subtitle:
            'Posição: ${_standings.where((s) => s['clubId'] == _myClubId).firstOrNull?['position'] ?? '—'}º',
      ),
      ClassicSummaryCard(
        icon: Icons.sentiment_satisfied_alt,
        title: 'Moral',
        content: _buildCoachMorale(),
      ),
      ClassicSummaryCard(
        icon: Icons.account_balance_wallet_outlined,
        title: 'Caixa',
        subtitle: club?['cashBalance'] is num
            ? formatClassicMoney(club!['cashBalance'])
            : '—',
      ),
      ClassicSummaryCard(
        icon: Icons.sports_soccer,
        title: 'Próximo jogo',
        subtitle: fixture == null
            ? 'A definir'
            : '${fixture['home'] == club?['name'] ? fixture['away'] : fixture['home']} • R${next?['roundNumber']}',
      ),
    ];
    return Padding(
      padding: const EdgeInsets.fromLTRB(10, 9, 10, 0),
      child: LayoutBuilder(
        builder: (context, c) => Container(
          key: const Key('classic-club-summary'),
          padding: const EdgeInsets.all(4),
          decoration: BoxDecoration(
            color: ClassicTheme.of(context).paper.surface,
            border: Border.all(
              color: ClassicTheme.of(context).paper.border,
              width: 1.5,
            ),
            borderRadius: BorderRadius.circular(8),
          ),
          child: c.maxWidth >= 900
              ? Row(
                  children: [
                    for (var i = 0; i < cards.length; i++) ...[
                      if (i > 0) const SizedBox(width: 5),
                      Expanded(flex: i == 0 ? 6 : 4, child: cards[i]),
                    ],
                  ],
                )
              : Column(
                  children: [
                    SizedBox(width: double.infinity, child: cards[0]),
                    const SizedBox(height: 4),
                    if (_activeTabIndex == 6 && !_appearanceOpen) ...[
                      for (var i = 1; i < cards.length; i += 2) ...[
                        if (i > 1) const SizedBox(height: 5),
                        Row(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Expanded(child: cards[i]),
                            const SizedBox(width: 5),
                            Expanded(child: cards[i + 1]),
                          ],
                        ),
                      ],
                    ] else
                      Row(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          for (var i = 1; i < cards.length; i++) ...[
                            if (i > 1) const SizedBox(width: 3),
                            Expanded(child: cards[i]),
                          ],
                        ],
                      ),
                  ],
                ),
        ),
      ),
    );
  }

  Widget _buildActiveScreenBody() {
    if (_appearanceOpen && !_matchLocked && widget.appearance != null) {
      return AppearanceSettings(
        api: widget.apiClient,
        controller: widget.appearance!,
        embedded: true,
        onClose: () => setState(() => _appearanceOpen = false),
      );
    }
    if (_matchLocked || _showMatchView) {
      return _buildLiveMatchesAndPauseScreen();
    }
    switch (_activeTabIndex) {
      case 0:
        return _buildSquadAndTacticsScreen();
      case 1:
        return _buildDivisionsAndCupScreen();
      case 2:
        return _buildMarketAndAuctionsScreen();
      case 3:
        return _buildFinancesAndStadiumScreen();
      case 4:
        return _buildCareerRankingAndInvitationsScreen();
      case 5:
        return _buildCalendarScreen();
      case 6:
        return PlayerRankingsScreen(
          key: ValueKey('player-rankings-$_worldId'),
          apiClient: widget.apiClient,
          worldId: _worldId!,
          currentSeason: (_snapshot?['seasonNumber'] as num?)?.toInt() ?? 1,
          revision: (_snapshot?['roundVersion'] as num?)?.toInt() ?? 0,
        );
      default:
        return _buildSquadAndTacticsScreen();
    }
  }

  Future<void> _announceSquadPlayer(
    Map<String, dynamic> player, {
    bool direct = false,
  }) async {
    if (_matchLocked || _worldId == null || _coachId == null) return;
    if (player['contractRenewedForSeason'] == true) {
      setState(
        () => _statusBanner =
            'Jogador renovado: venda e leilão bloqueados até o fim da temporada.',
      );
      return;
    }
    final world = _worldId, coach = _coachId, club = _myClubId;
    final config = await showDialog<VoluntaryAuctionConfig>(
      context: context,
      builder: (_) => AuctionConfigDialog(
        playerName: player['displayName'] ?? player['name'] ?? '',
        position: marketPosition(player['position']),
        strength: player['strength'] as num?,
        salary: player['salaryPerRound'] as num?,
        roundNumber: (_snapshot?['roundNumber'] as num?)?.toInt() ?? 1,
        seasonNumber: (_snapshot?['seasonNumber'] as num?)?.toInt() ?? 1,
        totalRoundsInSeason:
            (_snapshot?['totalRoundsInSeason'] as num?)?.toInt() ?? 22,
        roundAlreadyCompleted: _snapshot?['roundState'] == 'RESULTS',
        allowDirect: true,
        initialDirect: direct,
        directPrice: player['fairValue'] as num? ?? 500000,
      ),
    );
    if (!mounted ||
        config == null ||
        _matchLocked ||
        world != _worldId ||
        coach != _coachId ||
        club != _myClubId) {
      return;
    }
    await _runUiCommand(() async {
      if (config.isDirect) {
        await widget.apiClient.listPlayerForTransfer(
          world!,
          coach!,
          player['id'].toString(),
          config.minimumBid,
        );
      } else {
        await widget.apiClient.createVoluntaryAuction(
          world!,
          coach!,
          player['id'].toString(),
          minimumBid: config.minimumBid,
          durationRounds: config.durationRounds,
        );
      }
      await _refreshClubAndSecondaryData();
      if (mounted) setState(() => _statusBanner = 'ANÚNCIO PUBLICADO.');
    });
  }

  /// Tab 0: Painel Principal do Treinador + Plantel, Escalação (11 titulares, até 9 reservas), Táticas e Plano do Técnico IA
  Widget _buildSquadAndTacticsScreen() {
    if (!_isUnemployed && _myClubId != null && _myClubDetail == null) {
      return const Center(child: Text('CARREGANDO ELENCO...'));
    }
    if (_isUnemployed || _myClubDetail == null) {
      return SingleChildScrollView(child: _buildCoachMainPanel());
    }

    final squad = _myClubDetail!['squad'] as List<dynamic>? ?? [];

    // Apply squad filters and sorting
    final filteredSquad = squad.where((p) {
      final pid = p['id'].toString();
      final pos = _formatPosition(p['position']);
      final inj =
          ((p['injuryDaysRemaining'] as num?)?.toInt() ?? 0) +
          ((p['injuryRoundsRemaining'] as num?)?.toInt() ?? 0);
      final susp = (p['suspensionRoundsRemaining'] as num?)?.toInt() ?? 0;
      final isStarter = _selectedStarterIds.contains(pid);
      final isBench = _selectedBenchIds.contains(pid);

      if (_squadPositionFilter != 'ALL' && pos != _squadPositionFilter) {
        return false;
      }
      if (_squadStatusFilter == 'TITULAR' && !isStarter) return false;
      if (_squadStatusFilter == 'RESERVA' && !isBench) return false;
      if (_squadStatusFilter == 'APTO' && (inj > 0 || susp > 0)) return false;
      if (_squadStatusFilter == 'INDISPONIVEL' && inj == 0 && susp == 0) {
        return false;
      }
      return true;
    }).toList();

    if (_squadSortBy == 'STRENGTH_DESC') {
      filteredSquad.sort(
        (a, b) => ((b['strength'] as num?) ?? 0).compareTo(
          (a['strength'] as num?) ?? 0,
        ),
      );
    } else if (_squadSortBy == 'GOALS_DESC') {
      filteredSquad.sort(
        (a, b) => ((b['seasonGoals'] as num?) ?? 0).compareTo(
          (a['seasonGoals'] as num?) ?? 0,
        ),
      );
    } else if (_squadSortBy == 'VALUE_DESC') {
      filteredSquad.sort(
        (a, b) => ((b['fairValue'] as num?) ?? 0).compareTo(
          (a['fairValue'] as num?) ?? 0,
        ),
      );
    }

    final squadFilters = Wrap(
      spacing: 8,
      runSpacing: 6,
      crossAxisAlignment: WrapCrossAlignment.center,
      children: [
        Text(
          'FILTRAR POS:',
          style: TextStyle(
            fontSize: 11,
            fontWeight: FontWeight.bold,
            color: ClassicTheme.of(context).accent,
          ),
        ),
        for (final pos in ['ALL', 'G', 'D', 'M', 'A'])
          InkWell(
            key: ValueKey('filter-squad-pos-$pos'),
            onTap: () => setState(() => _squadPositionFilter = pos),
            child: Container(
              padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
              decoration: BoxDecoration(
                color: _squadPositionFilter == pos
                    ? ClassicTheme.of(context).selectedBackground
                    : ClassicTheme.of(context).surface,
                border: Border.all(color: ClassicTheme.of(context).border),
              ),
              child: Text(
                pos == 'ALL' ? 'TODAS' : pos,
                style: TextStyle(
                  fontSize: 10,
                  fontWeight: FontWeight.bold,
                  color: _squadPositionFilter == pos
                      ? ClassicTheme.of(context).selectedText
                      : ClassicTheme.of(context).textPrimary,
                ),
              ),
            ),
          ),
        const SizedBox(width: 8),
        Text(
          'STATUS:',
          style: TextStyle(
            fontSize: 11,
            fontWeight: FontWeight.bold,
            color: ClassicTheme.of(context).accent,
          ),
        ),
        for (final st in [
          ('ALL', 'TODOS'),
          ('TITULAR', 'TITULARES'),
          ('RESERVA', 'RESERVAS'),
          ('APTO', 'APTOS'),
          ('INDISPONIVEL', 'INDISPONÍVEIS'),
        ])
          InkWell(
            key: ValueKey('filter-squad-status-${st.$1}'),
            onTap: () => setState(() => _squadStatusFilter = st.$1),
            child: Container(
              padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
              decoration: BoxDecoration(
                color: _squadStatusFilter == st.$1
                    ? ClassicTheme.of(context).selectedBackground
                    : ClassicTheme.of(context).surface,
                border: Border.all(color: ClassicTheme.of(context).border),
              ),
              child: Text(
                st.$2,
                style: TextStyle(
                  fontSize: 10,
                  fontWeight: FontWeight.bold,
                  color: _squadStatusFilter == st.$1
                      ? ClassicTheme.of(context).selectedText
                      : ClassicTheme.of(context).textPrimary,
                ),
              ),
            ),
          ),
        const SizedBox(width: 8),
        Text(
          'ORDENAR:',
          style: TextStyle(
            fontSize: 11,
            fontWeight: FontWeight.bold,
            color: ClassicTheme.of(context).accent,
          ),
        ),
        for (final srt in [
          ('DEFAULT', 'PADRÃO'),
          ('STRENGTH_DESC', 'FORÇA'),
          ('GOALS_DESC', 'GOLS'),
          ('VALUE_DESC', 'VALOR'),
        ])
          InkWell(
            key: ValueKey('sort-squad-${srt.$1}'),
            onTap: () => setState(() => _squadSortBy = srt.$1),
            child: Container(
              padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
              decoration: BoxDecoration(
                color: _squadSortBy == srt.$1
                    ? ClassicTheme.of(context).selectedBackground
                    : ClassicTheme.of(context).surface,
                border: Border.all(color: ClassicTheme.of(context).border),
              ),
              child: Text(
                srt.$2,
                style: TextStyle(
                  fontSize: 10,
                  fontWeight: FontWeight.bold,
                  color: _squadSortBy == srt.$1
                      ? ClassicTheme.of(context).selectedText
                      : ClassicTheme.of(context).textPrimary,
                ),
              ),
            ),
          ),
      ],
    );
    Widget rosterFilter() => SizedBox(
      width: 100,
      child: DropdownButtonHideUnderline(
        child: DropdownButton<String>(
          key: const Key('squad-position-dropdown'),
          icon: Icon(
            Icons.arrow_drop_down,
            color: ClassicTheme.of(context).paper.textPrimary,
          ),
          isExpanded: true,
          value: _squadPositionFilter,
          style: TextStyle(
            color: ClassicTheme.of(context).paper.textPrimary,
            fontFamily: 'ClassicCondensed',
            fontSize: 15,
          ),
          dropdownColor: ClassicTheme.of(context).paper.surface,
          items: [
            for (final p in ['ALL', 'G', 'D', 'M', 'A'])
              DropdownMenuItem(value: p, child: Text(p == 'ALL' ? 'Todos' : p)),
          ],
          onChanged: (v) {
            if (v != null) setState(() => _squadPositionFilter = v);
          },
        ),
      ),
    );
    final typedSquad = squad
        .map((p) => Map<String, dynamic>.from(p as Map))
        .toList();
    final typedFiltered = filteredSquad
        .map((p) => Map<String, dynamic>.from(p as Map))
        .toList();
    final canManage =
        !_matchLocked &&
        (!_tacticsLocked || _snapshot?['roundState'] == 'RESULTS');
    return LayoutBuilder(
      builder: (context, viewport) {
        if (viewport.maxWidth < 768) {
          return MobileSquadScreen(
            squad: typedSquad,
            filteredSquad: typedFiltered,
            starters: _selectedStarterIds,
            bench: _selectedBenchIds,
            formations: availableFormations,
            formation: _selectedFormation,
            requiredStarterCount: _requiredStarterCount,
            locked: _tacticsLocked,
            busy: _commandBusy,
            tactics: _mobileTacticsView,
            canManage: canManage,
            onFormation: _changeFormation,
            swapPickedId: _swapPickedPlayerId,
            onSwapPick: _pickSquadSwapPlayer,
            onBest: _pickBestSquad,
            onDetail: _showPlayerDetailDialog,
            onToggle: _toggleSquadPlayer,
            onRenew: _renewSquadPlayer,
            onAnnounce: (p) => _announceSquadPlayer(p, direct: true),
            onAuction: _announceSquadPlayer,
            onTab: _navigateMobile,
            field: AbsorbPointer(
              absorbing: _tacticsLocked,
              child: _buildTacticalFieldPreview(squad),
            ),
            filters: squadFilters,
            rosterFilter: rosterFilter(),
            coachPanel: _buildCoachMainPanel(),
          );
        }
        final round = (_snapshot?['roundNumber'] as num?)?.toInt() ?? 1;
        final nextFixtureRound =
            round + (_snapshot?['roundState'] == 'RESULTS' ? 1 : 0);
        final fixture =
            (_calendar
                            .where((r) => r['roundNumber'] == nextFixtureRound)
                            .firstOrNull?['fixtures']
                        as List? ??
                    [])
                .where(
                  (f) =>
                      f['home'] == _myClubDetail?['name'] ||
                      f['away'] == _myClubDetail?['name'],
                )
                .firstOrNull;
        final opponent = fixture == null
            ? 'A definir'
            : '${fixture['home'] == _myClubDetail?['name'] ? fixture['away'] : fixture['home']}';
        final people =
            _snapshot?['readyCounter']?['participants'] as List? ?? [];
        final ready =
            people
                .where((p) => p['coachId'] == _coachId)
                .firstOrNull?['isReady'] ==
            true;
        final preparation = _isClassicIdleRound
            ? 'Sem partida — pronto para acompanhar'
            : ready
            ? 'Tática enviada'
            : '${_selectedStarterIds.length}/$_requiredStarterCount atletas selecionados';
        Widget shortcuts() => Wrap(
          spacing: 4,
          runSpacing: 4,
          children: [
            for (final entry in [
              ('plantel', 'PLANTEL'),
              ('taticas', 'TÁTICAS ($_selectedFormation)'),
              ('escalacao', 'ESCALAÇÃO'),
              ('mercado', 'MERCADO'),
              ('classificacao', 'CLASSIFICAÇÃO'),
              ('proxima-partida', 'PRÓXIMA PARTIDA'),
            ])
              OutlinedButton(
                key: ValueKey('quick-action-${entry.$1}'),
                style: OutlinedButton.styleFrom(
                  minimumSize: const Size(0, 32),
                  padding: const EdgeInsets.symmetric(horizontal: 7),
                  textStyle: const TextStyle(
                    fontFamily: 'ClassicCondensed',
                    fontSize: 13,
                    fontWeight: FontWeight.w700,
                  ),
                  backgroundColor: entry.$1 == 'plantel'
                      ? ClassicTheme.of(context).accent
                      : ClassicTheme.of(context).primary,
                  foregroundColor: entry.$1 == 'plantel'
                      ? ClassicTheme.of(context).selectedText
                      : ClassicTheme.of(context).textPrimary,
                  disabledForegroundColor: Colors.white60,
                ),
                onPressed:
                    _tacticsLocked &&
                        (entry.$1 == 'taticas' || entry.$1 == 'escalacao')
                    ? null
                    : () {
                        switch (entry.$1) {
                          case 'taticas':
                            _changeFormation(
                              availableFormations[(availableFormations.indexOf(
                                        _selectedFormation,
                                      ) +
                                      1) %
                                  availableFormations.length],
                            );
                          case 'escalacao':
                            _autoPickBestLineup();
                          case 'mercado':
                            _navigateToTab(2);
                          case 'classificacao':
                            _navigateToTab(1);
                          case 'proxima-partida':
                            if (RoundViewCoordinator.isMatchStage(
                              _snapshot?['roundState']?.toString(),
                            )) {
                              _navigation.navigate('/partida');
                            } else {
                              _navigateToTab(5);
                            }
                          default:
                            break;
                        }
                      },
                child: Text(entry.$2),
              ),
          ],
        );
        final tactical = SquadPaperPanel(
          title: 'PLANTEL & TÁTICAS',
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              shortcuts(),
              const SizedBox(height: 7),
              KeyedSubtree(
                key: const ValueKey('coach-main-panel'),
                child: ClassicCoachPreparation(
                  club: _myClubDetail!,
                  coach: _nickname,
                  date: formatClassicDate(_snapshot?['gameDate']),
                  opponent: opponent,
                  preparation: preparation,
                ),
              ),
              const SizedBox(height: 7),
              ClassicSquadTactics(
                key: _tacticsPanelKey,
                squad: typedSquad,
                starters: _selectedStarterIds,
                bench: _selectedBenchIds,
                formations: availableFormations,
                formation: _selectedFormation,
                locked: _tacticsLocked,
                requiredStarters: _requiredStarterCount,
                shirt: _clubColor(
                  _myClubDetail?['colorPalette'],
                  'primaryColor',
                ),
                onFormation: _changeFormation,
                onPlayer: _showPlayerDetailDialog,
                onSwap: _swapSquadPlayers,
                swapPickedId: _swapPickedPlayerId,
                onSwapPick: _pickSquadSwapPlayer,
                onBest: _pickBestSquad,
              ),
            ],
          ),
        );
        final roster = SquadPaperPanel(
          title: 'PLANTEL DE JOGADORES (* = CRAQUE)',
          trailing: rosterFilter(),
          padding: const EdgeInsets.only(top: 4),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              ClassicSquadRoster(
                players: typedFiltered,
                starters: _selectedStarterIds,
                bench: _selectedBenchIds,
                compact: false,
                locked: _tacticsLocked,
                busy: _commandBusy,
                canManage: canManage,
                onToggle: _toggleSquadPlayer,
                onDetail: _showPlayerDetailDialog,
                onRenew: _renewSquadPlayer,
                onAuction: _announceSquadPlayer,
              ),
              ExpansionTile(
                key: const Key('desktop-squad-filters'),
                dense: true,
                title: const Text(
                  'FILTROS E ORDENAÇÃO',
                  style: TextStyle(fontSize: 13),
                ),
                children: [squadFilters],
              ),
            ],
          ),
        );
        return SingleChildScrollView(
          key: const Key('desktop-squad-scroll'),
          padding: const EdgeInsets.all(10),
          child: viewport.maxWidth >= 1000
              ? Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Expanded(
                      key: const Key('desktop-tactics-column'),
                      flex: 1,
                      child: tactical,
                    ),
                    const SizedBox(width: 8),
                    Expanded(flex: 1, child: roster),
                  ],
                )
              : Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [tactical, const SizedBox(height: 8), roster],
                ),
        );
      },
    );
  }

  void _showPlayerDetailDialog(Map<String, dynamic> player) {
    final displayName = player['displayName']?.toString() ?? '';
    final photoUrl = player['photoUrl']?.toString();
    final photoLicense =
        player['photoLicense']?.toString() ?? 'Asset interno padrão';
    final photoCredit = player['photoCredit']?.toString() ?? 'Elifoot Classic';

    showDialog(
      context: context,
      builder: (ctx) => AlertDialog(
        backgroundColor: ClassicTheme.of(context).surface,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.zero,
          side: BorderSide(color: ClassicTheme.of(context).border, width: 1.5),
        ),
        title: Text(
          displayName,
          style: TextStyle(
            color: ClassicTheme.of(context).accent,
            fontWeight: FontWeight.bold,
          ),
        ),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Center(
              child: Container(
                width: 84,
                height: 84,
                decoration: BoxDecoration(
                  color: ClassicTheme.of(context).surface,
                  border: Border.all(
                    color: ClassicTheme.of(context).selectedBackground,
                  ),
                ),
                child: (photoUrl != null && photoUrl.isNotEmpty)
                    ? Image.network(
                        photoUrl,
                        fit: BoxFit.cover,
                        errorBuilder: (_, _, _) => CustomPaint(
                          size: Size(64, 64),
                          painter: _PlayerSilhouettePainter(
                            ClassicTheme.of(context).selectedBackground,
                          ),
                        ),
                      )
                    : CustomPaint(
                        size: Size(64, 64),
                        painter: _PlayerSilhouettePainter(
                          ClassicTheme.of(context).selectedBackground,
                        ),
                      ),
              ),
            ),
            const SizedBox(height: 10),
            Text(
              'Posição: ${_formatPosition(player['position'])}  |  Força: ${player['strength']}',
            ),
            Text('Comportamento: ${player['behaviorDisplay']}'),
            if (player['catalogIdentity'] != null)
              Text(catalogIdentityOrigin(player)),
            Text('Craque (*): ${player['isStar'] == true ? "Sim (*)" : "Não"}'),
            Text(
              'Gols na Temporada: ${player['seasonGoals']}  |  Carreira: ${player['careerGoals']}',
            ),
            Text(
              player['seasonContractsEnabled'] == true
                  ? player['contractRenewedForSeason'] == true
                        ? '✓ Contrato renovado até o fim da temporada ${player['contractThroughSeason']}.'
                        : 'SEM CONTRATO DA TEMPORADA. O vínculo com o clube é mantido.'
                  : 'Contrato restante: ${player['contractRoundsRemaining']} rodadas',
            ),
            const Divider(),
            Text(
              'Licença da Imagem: $photoLicense ($photoCredit)',
              style: TextStyle(
                fontSize: 10,
                color: ClassicTheme.of(context).textSecondary,
              ),
            ),
          ],
        ),
        actions: [
          if (_worldId != null && (player['id'] ?? player['playerId']) != null)
            TextButton(
              onPressed: () {
                Navigator.pop(ctx);
                showPlayerHistoryDialog(
                  context: context,
                  apiClient: widget.apiClient,
                  worldId: _worldId!,
                  playerId: (player['id'] ?? player['playerId']).toString(),
                );
              },
              child: const Text('HISTÓRICO & ARTILHARIAS'),
            ),
          TextButton(
            onPressed: () => Navigator.pop(ctx),
            child: const Text('FECHAR'),
          ),
        ],
      ),
    );
  }

  String _matchVisualStateLabel(
    String roundState,
    Map<String, dynamic>? pauseWindow,
  ) {
    if (_snapshot?['pendingPenalty'] != null) {
      return 'PÊNALTI • RELÓGIO PARADO';
    }
    if (roundState == 'RUNNING_FIRST_HALF' ||
        roundState == 'RUNNING_SECOND_HALF' ||
        roundState == 'RUNNING_EXTRA_TIME') {
      return 'PARTIDA EM ANDAMENTO';
    }
    if (roundState == 'GLOBAL_PAUSED' ||
        roundState == 'HALFTIME_PAUSED' ||
        roundState == 'EXTRA_TIME_BREAK' ||
        roundState == 'EXTRA_TIME_HALFTIME') {
      final confirmed = (pauseWindow?['confirmedCount'] as num?)?.toInt() ?? 0;
      if (confirmed > 0) {
        return 'PARTIDA PAUSADA • AGUARDANDO RETOMADA';
      }
      if (roundState == 'GLOBAL_PAUSED') {
        return 'PAUSA SOLICITADA • PARTIDA PAUSADA';
      }
      return 'PARTIDA PAUSADA (INTERVALO)';
    }
    if (roundState == 'RESULTS') {
      return 'PARTIDA FINALIZADA';
    }
    return 'PREPARAÇÃO DA RODADA';
  }

  Widget _buildLiveMatchesAndPauseScreen() {
    final snap = _snapshot ?? <String, dynamic>{};
    final matches = (snap['matches'] as List? ?? []).whereType<Map>();
    final mine = matches
        .where(
          (m) => m['homeClubId'] == _myClubId || m['awayClubId'] == _myClubId,
        )
        .firstOrNull;
    final pause = snap['activePauseWindow'] as Map<String, dynamic>?;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
          child: Text(
            'PLACARES DA RODADA • ${_matchVisualStateLabel(snap['roundState']?.toString() ?? 'PREPARING', pause)}',
            key: const ValueKey('live-match-state-banner'),
            style: TextStyle(
              color: ClassicTheme.of(context).accent,
              fontSize: 11,
              fontWeight: FontWeight.bold,
            ),
          ),
        ),
        Expanded(
          child: LiveMatchScreen(
            key: ValueKey('live-view-$_worldId-$_coachId'),
            snapshot: snap,
            calendar: _calendar,
            clubs: _clubPaletteWorld == _worldId ? _liveClubs : {},
            coachId: _coachId ?? '',
            coachName: _nickname,
            coachClubId: _myClubId,
            selection: _liveSelection,
            onSelectionChanged: _saveSession,
            onSubstitute:
                mine != null &&
                    mine['isFinished'] != true &&
                    snap['pendingPenalty'] == null &&
                    !_commandBusy &&
                    '${snap['roundState']}'.startsWith('RUNNING_')
                ? () => _runUiCommand(_handleRequestGlobalPause)
                : null,
          ),
        ),
      ],
    );
  }

  Widget? _buildMatchInteraction() {
    final penalty = _snapshot?['pendingPenalty'];
    if (penalty is Map) {
      final value = Map<String, dynamic>.from(penalty);
      return PenaltyChoiceDialog(
        key: ValueKey('penalty-${value['decisionId']}'),
        penalty: value,
        choosing: value['coachId'] == _coachId,
        busy: _commandBusy,
        notice: _matchInteractionNotice,
        onShoot: (player) => _runUiCommand(() async {
          try {
            final snap = await widget.apiClient.choosePenaltyTaker(
              _worldId!,
              _coachId!,
              '${value['decisionId']}',
              player,
            );
            if (mounted) setState(() => _snapshot = _acceptSnapshot(snap));
          } catch (e) {
            if (mounted) setState(() => _matchInteractionNotice = '$e');
            rethrow;
          }
        }),
      );
    }
    final pauseRaw = _snapshot?['activePauseWindow'];
    if (pauseRaw is! Map) return null;
    final pause = Map<String, dynamic>.from(pauseRaw);
    final mine = (_snapshot?['matches'] as List? ?? [])
        .whereType<Map>()
        .where(
          (m) => m['homeClubId'] == _myClubId || m['awayClubId'] == _myClubId,
        )
        .firstOrNull;
    final confirmed = (pause['confirmedCoachIds'] as List? ?? []).contains(
      _coachId,
    );
    final child = mine == null || mine['isFinished'] == true
        ? PauseWaitingContent(
            pause: pause,
            confirmed: confirmed,
            busy: _commandBusy,
            onContinue: () => _runUiCommand(_handleConfirmContinue),
          )
        : SubstitutionChoicePanel(
            key: ValueKey(
              'substitution-${pause['pauseWindowId'] ?? '${_snapshot?['roundNumber']}-${pause['minute']}-${_snapshot?['roundState']}'}',
            ),
            match: Map<String, dynamic>.from(mine),
            pause: pause,
            coachId: _coachId!,
            isHome: mine['homeClubId'] == _myClubId,
            busy: _commandBusy,
            confirmed: confirmed,
            notice: _matchInteractionNotice,
            formations: availableFormations,
            onContinue: () => _runUiCommand(_handleConfirmContinue),
            onApply: (formation, subs) => _runUiCommand(() async {
              try {
                final snap = await widget.apiClient.submitInMatchAdjustments(
                  worldId: _worldId!,
                  coachId: _coachId!,
                  matchId: '${mine['matchId']}',
                  newFormation: formation,
                  substitutions: subs,
                );
                if (mounted) {
                  setState(() {
                    _snapshot = _acceptSnapshot(snap);
                    _matchInteractionNotice =
                        'Alterações registradas. Clique em Continuar quando terminar.';
                  });
                }
              } catch (e) {
                if (mounted) setState(() => _matchInteractionNotice = '$e');
                rethrow;
              }
            }),
          );
    return ClassicMatchActionDialog(
      key: const Key('shared-substitution-dialog'),
      caption: pause['pauseType'] == 'GLOBAL_PAUSED'
          ? 'SUBSTITUIÇÕES • ${pause['minute'] ?? _snapshot?['currentMinute']}\''
          : 'INTERVALO — SUBSTITUIÇÕES',
      child: child,
    );
  }

  /// Tab 2: Divisões (1ª, 2ª, 3ª, 4ª), Taça (Preliminares Distritais + 64 clubes sem truncamento) e Artilharia
  Widget _buildDivisionsAndCupScreen() {
    final currentRound = (_snapshot?['roundNumber'] as num?)?.toInt() ?? 1;
    final prevRound = _calendar
        .where((r) => r['roundNumber'] == currentRound - 1)
        .firstOrNull;
    final curRoundCal = _calendar
        .where((r) => r['roundNumber'] == currentRound)
        .firstOrNull;
    final nextRound = _calendar
        .where((r) => r['roundNumber'] == currentRound + 1)
        .firstOrNull;

    // Extract distinct Cup stages in chronological order
    final cupStages = <String>[];
    for (final f in _cupFixtures) {
      final st = f['stage']?.toString() ?? '';
      if (st.isNotEmpty && !cupStages.contains(st)) {
        cupStages.add(st);
      }
    }
    final activeCupStage =
        (_selectedCupStage != 'ALL' && cupStages.contains(_selectedCupStage))
        ? _selectedCupStage
        : 'ALL';
    final visibleCupFixtures = activeCupStage == 'ALL'
        ? _cupFixtures
        : _cupFixtures.where((f) => f['stage'] == activeCupStage).toList();

    final finalFixture = _cupFixtures
        .where(
          (f) =>
              f['stage']?.toString().toUpperCase().contains('FINAL') == true &&
              !f['stage']!.toString().toUpperCase().contains('SEMI') &&
              !f['stage']!.toString().toUpperCase().contains('QUARTA') &&
              !f['stage']!.toString().toUpperCase().contains('OITAVA') &&
              !f['stage']!.toString().toUpperCase().contains('AVOS'),
        )
        .lastOrNull;
    final cupChampion = finalFixture?['winnerClubName']?.toString();

    return SingleChildScrollView(
      padding: const EdgeInsets.all(10),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const ClassicPageHeader(
            title: 'DIVISÕES & TAÇA',
            icon: Icons.emoji_events,
          ),
          const SizedBox(height: 8),
          ClassicSelectionTabs(
            selected: _competitionTab,
            items: const [
              ('standings', 'CLASSIFICAÇÃO'),
              ('cup', 'TAÇA NACIONAL'),
            ],
            onSelected: (value) => setState(() => _competitionTab = value),
          ),
          const SizedBox(height: 10),
          if (_competitionTab == 'standings') ...[
            ClassicSelectionTabs(
              selected: _selectedDivisionTier,
              keyPrefix: 'btn-division-',
              fontSize: 16,
              items: const [
                ('PrimeiraDivisao', '1ª DIVISÃO'),
                ('SegundaDivisao', '2ª DIVISÃO'),
                ('TerceiraDivisao', '3ª DIVISÃO'),
                ('QuartaDivisao', '4ª DIVISÃO'),
              ],
              onSelected: (value) async {
                setState(() => _selectedDivisionTier = value);
                final rows = await widget.apiClient.getStandings(
                  _worldId!,
                  value,
                );
                if (mounted) setState(() => _standings = rows);
              },
            ),
            const SizedBox(height: 10),
            ClassicStandings(
              rows: _standings,
              clubId: _myClubId,
              title:
                  '${switch (_selectedDivisionTier) {
                    'SegundaDivisao' => '2ª',
                    'TerceiraDivisao' => '3ª',
                    'QuartaDivisao' => '4ª',
                    _ => '1ª',
                  }} DIVISÃO — CLASSIFICAÇÃO',
            ),
            const SizedBox(height: 12),
            ClassicRoundSummary(
              rounds: [
                ('RODADA ANTERIOR', prevRound),
                ('RODADA ATUAL', curRoundCal),
                ('PRÓXIMA RODADA', nextRound),
              ],
            ),
          ],
          if (_competitionTab == 'cup')
            ClassicPaperPanel(
              child: ClassicPaper(
                builder: (context) => Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Text(
                      'TAÇA NACIONAL (PRELIMINARES DISTRITAIS 96 -> 32 + MATA-MATA DE 64 CLUBES)',
                      style: TextStyle(
                        color: ClassicTheme.of(context).accent,
                        fontWeight: FontWeight.bold,
                        fontSize: 14,
                      ),
                    ),
                    const SizedBox(height: 6),
                    if (cupChampion != null && cupChampion.isNotEmpty) ...[
                      Container(
                        width: double.infinity,
                        margin: const EdgeInsets.only(bottom: 8),
                        padding: const EdgeInsets.all(10),
                        decoration: BoxDecoration(
                          color: ClassicTheme.of(context).surfaceAlternate,
                          border: Border.all(
                            color: ClassicTheme.of(context).selectedBackground,
                            width: 1.5,
                          ),
                        ),
                        child: Text(
                          'CAMPEÃO DA TAÇA NACIONAL: $cupChampion',
                          key: const ValueKey('cup-champion-banner'),
                          style: TextStyle(
                            color: ClassicTheme.of(context).accent,
                            fontWeight: FontWeight.bold,
                            fontSize: 13,
                          ),
                        ),
                      ),
                    ],
                    if (cupStages.isNotEmpty) ...[
                      Wrap(
                        spacing: 6,
                        runSpacing: 6,
                        children: [
                          InkWell(
                            key: const ValueKey('cup-stage-ALL'),
                            onTap: () =>
                                setState(() => _selectedCupStage = 'ALL'),
                            child: Container(
                              padding: const EdgeInsets.symmetric(
                                horizontal: 8,
                                vertical: 5,
                              ),
                              decoration: BoxDecoration(
                                color: activeCupStage == 'ALL'
                                    ? ClassicTheme.of(
                                        context,
                                      ).selectedBackground
                                    : ClassicTheme.of(context).surface,
                                border: Border.all(
                                  color: ClassicTheme.of(context).border,
                                ),
                              ),
                              child: Text(
                                'TODAS AS FASES (${_cupFixtures.length})',
                                style: TextStyle(
                                  fontSize: 10.5,
                                  fontWeight: FontWeight.bold,
                                  color: activeCupStage == 'ALL'
                                      ? ClassicTheme.of(context).selectedText
                                      : ClassicTheme.of(context).textPrimary,
                                ),
                              ),
                            ),
                          ),
                          for (final st in cupStages)
                            InkWell(
                              key: ValueKey('cup-stage-$st'),
                              onTap: () =>
                                  setState(() => _selectedCupStage = st),
                              child: Container(
                                padding: const EdgeInsets.symmetric(
                                  horizontal: 8,
                                  vertical: 5,
                                ),
                                decoration: BoxDecoration(
                                  color: activeCupStage == st
                                      ? ClassicTheme.of(
                                          context,
                                        ).selectedBackground
                                      : ClassicTheme.of(context).surface,
                                  border: Border.all(
                                    color: ClassicTheme.of(context).border,
                                  ),
                                ),
                                child: Text(
                                  st,
                                  style: TextStyle(
                                    fontSize: 10.5,
                                    fontWeight: FontWeight.bold,
                                    color: activeCupStage == st
                                        ? ClassicTheme.of(context).selectedText
                                        : ClassicTheme.of(context).textPrimary,
                                  ),
                                ),
                              ),
                            ),
                        ],
                      ),
                      const SizedBox(height: 8),
                    ],
                    if (visibleCupFixtures.isEmpty)
                      const Text(
                        'Nenhuma partida da Taça registrada nesta fase ainda.',
                        style: TextStyle(fontSize: 12),
                      )
                    else
                      ClassicTable(
                        tableKey: 'cup-fixtures-table',
                        columns: const [
                          'FASE',
                          'MANDANTE',
                          'PLACAR',
                          'VISITANTE',
                          'CLASSIFICADO / SITUAÇÃO',
                        ],
                        rows: visibleCupFixtures.asMap().entries.map((entry) {
                          final i = entry.key;
                          final f = entry.value;
                          final penH = f['homePenaltyGoals'];
                          final penA = f['awayPenaltyGoals'];
                          final penStr = (penH != null && penA != null)
                              ? ' (Pên: $penH x $penA)'
                              : '';
                          final winner = f['winnerClubName']?.toString();
                          final scoreStr =
                              f['isFinished'] == true || winner != null
                              ? '${f['homeGoals']} x ${f['awayGoals']}$penStr'
                              : 'vs';
                          return DataRow(
                            color: WidgetStateProperty.all(
                              i.isEven
                                  ? ClassicTheme.of(context).tableRow
                                  : ClassicTheme.of(context).tableRowAlternate,
                            ),
                            cells: [
                              DataCell(
                                Text(
                                  '${f['stage']}',
                                  style: TextStyle(
                                    fontSize: 11,
                                    color: ClassicTheme.of(context).accent,
                                  ),
                                ),
                              ),
                              DataCell(
                                TeamColorName(
                                  name:
                                      '${f['homeClubName']} (${f['homeDivision']})',
                                  palette: _teamPalette(
                                    id: f['homeClubId'],
                                    name: f['homeClubName'],
                                  ),
                                  textAlign: TextAlign.right,
                                ),
                              ),
                              DataCell(
                                Text(
                                  scoreStr,
                                  style: TextStyle(
                                    fontSize: 11,
                                    fontWeight: FontWeight.bold,
                                    color: ClassicTheme.of(context).accent,
                                  ),
                                ),
                              ),
                              DataCell(
                                TeamColorName(
                                  name:
                                      '${f['awayClubName']} (${f['awayDivision']})',
                                  palette: _teamPalette(
                                    id: f['awayClubId'],
                                    name: f['awayClubName'],
                                  ),
                                ),
                              ),
                              DataCell(
                                Text(
                                  winner != null
                                      ? 'Vencedor: $winner'
                                      : 'A disputar',
                                  style: TextStyle(
                                    fontSize: 11,
                                    color: winner != null
                                        ? readableOn(
                                            Colors.lightGreenAccent,
                                            ClassicTheme.of(context).tableRow,
                                          )
                                        : ClassicTheme.of(
                                            context,
                                          ).textSecondary,
                                  ),
                                ),
                              ),
                            ],
                          );
                        }).toList(),
                      ),
                    const SizedBox(height: 20),
                    Text(
                      'ARTILHARIA DA TEMPORADA',
                      style: TextStyle(
                        color: ClassicTheme.of(context).accent,
                        fontWeight: FontWeight.bold,
                        fontSize: 14,
                      ),
                    ),
                    const SizedBox(height: 6),
                    ..._topScorers.take(15).map((s) {
                      return Padding(
                        padding: const EdgeInsets.symmetric(vertical: 2),
                        child: Text(
                          '${s['rank']}º ${s['displayName']} (${s['clubName']}) — ${(s['seasonGoals'] as num? ?? 0) + (s['cupGoals'] as num? ?? 0)} gols (Liga: ${s['seasonGoals']}, Taça: ${s['cupGoals']})',
                          style: const TextStyle(fontSize: 12),
                        ),
                      );
                    }),
                  ],
                ),
              ),
            ),
        ],
      ),
    );
  }

  Widget _buildMarketAndAuctionsScreen() => MarketScreen(
    key: ValueKey('market-$_worldId'),
    api: widget.apiClient,
    worldId: _worldId!,
    coachId: _coachId!,
    clubId: _myClubId,
    snapshot: _snapshot ?? {},
    data: _marketData ?? {},
    squad: _myClubDetail?['squad'] as List? ?? [],
    preferences: _marketPreferences,
    refresh: _refreshClubAndSecondaryData,
    locked: _matchLocked,
  );

  /// Finanças e estádio mantêm os comandos econômicos 012D.
  Widget _buildFinancesAndStadiumScreen() {
    if (_myClubDetail == null) {
      return const Center(
        child: Text('Você não controla um clube no momento.'),
      );
    }

    final loans = _financesData?['loans'] as List<dynamic>? ?? [];
    final credit = _financesData?['credit'] as Map<String, dynamic>?;
    final salaryRequests =
        (_financesData?['salaryRequests'] as List<dynamic>? ?? [])
            .where((r) => r['status'] == 'PENDING')
            .toList();
    String loanInterest(dynamic loan) {
      if (loan['mode'] == 'ClassicFixed') {
        return '${formatClassicMoney(loan['interestPerMatch'] ?? 0)} por partida';
      }
      final rate = ((loan['interestRatePerRound'] as num?) ?? 0) * 100;
      return '${rate.toStringAsFixed(2).replaceAll('.', ',')}% sobre o saldo; prestação ${formatClassicMoney(loan['installmentPerRound'] ?? 0)}';
    }

    final renoCost =
        (_financesData?['renovationCostEstimate'] as num?)?.toDouble() ?? 50000;
    final expCost =
        (_financesData?['expansion5kCostEstimate'] as num?)?.toDouble() ??
        425000;
    final cash = (_myClubDetail!['cashBalance'] as num?)?.toDouble() ?? 0.0;

    return ClassicFinanceView(
      club: _myClubDetail!,
      data: _financesData ?? {},
      season: (_snapshot?['seasonNumber'] as num? ?? 1).toInt(),
      round: (_snapshot?['roundNumber'] as num? ?? 1).toInt(),
      actions: [
        ElevatedButton.icon(
          key: const ValueKey('btn-reformar-estadio'),
          icon: const Icon(Icons.build, size: 18),
          onPressed: _commandBusy
              ? null
              : () => _runUiCommand(() async {
                  await widget.apiClient.startStadiumRenovation(
                    _worldId!,
                    _myClubId!,
                    _coachId!,
                  );
                  await _refreshClubAndSecondaryData();
                  setState(
                    () =>
                        _statusBanner = 'Obra de reforma estrutural iniciada!',
                  );
                }),
          label: Text('REFORMAR ESTÁDIO (${formatClassicMoney(renoCost)})'),
        ),
        ElevatedButton.icon(
          key: const ValueKey('btn-ampliar-estadio'),
          icon: const Icon(Icons.stadium, size: 18),
          onPressed: _commandBusy
              ? null
              : () => _runUiCommand(() async {
                  await widget.apiClient.startStadiumExpansion(
                    _worldId!,
                    _myClubId!,
                    _coachId!,
                    5000,
                  );
                  await _refreshClubAndSecondaryData();
                  setState(
                    () =>
                        _statusBanner = 'Ampliação de +5.000 lugares iniciada!',
                  );
                }),
          label: Text(
            'AMPLIAR +5.000 LUGARES (${formatClassicMoney(expCost)})',
          ),
        ),
        OutlinedButton.icon(
          key: const ValueKey('btn-emprestimo-bancario'),
          icon: const Icon(Icons.toll, size: 18),
          onPressed: _commandBusy
              ? null
              : () => _runUiCommand(() async {
                  await widget.apiClient.takeBankLoan(
                    _worldId!,
                    _myClubId!,
                    _coachId!,
                    500000,
                    0,
                  );
                  await _refreshClubAndSecondaryData();
                  setState(
                    () => _statusBanner =
                        'Empréstimo bancário de R\$ 500.000 creditado em caixa.',
                  );
                }),
          label: const Text('EMPRÉSTIMO (+R\$ 500.000; JUROS R\$ 25.000/JOGO)'),
        ),
      ],
      obligations: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          if (credit != null) ...[
            if (_financesData?['annualSponsorshipBudget'] != null)
              Text(
                'PATROCÍNIO ANUAL: ${formatClassicMoney(_financesData!['annualSponsorshipBudget'])} em ${_financesData!['sponsorshipInstallments']} parcelas',
              ),
            const Text('FOLHA SALARIAL: por partida oficial disputada.'),
            Text(
              'SALDO DISPONÍVEL: ${formatClassicMoney(_financesData?['availableCash'] ?? cash)}',
            ),
            Text(
              'CRÉDITO INDIVIDUAL: ${credit['activeLoans']}/${credit['loanLimit']} empréstimos; disponíveis: ${credit['availableLoans']}',
            ),
            Text(
              'DÍVIDA BANCÁRIA: ${formatClassicMoney(credit['outstandingDebt'] ?? 0)} | JUROS CLÁSSICOS POR PARTIDA: ${formatClassicMoney(credit['interestPerMatch'] ?? 0)}',
            ),
            if (_financesData?['negativeCash'] is Map)
              Text(
                'CAIXA NEGATIVO: ${formatClassicMoney(_financesData!['negativeCash']['principal'])} | '
                'JUROS: 2% por rodada global concluída; estimativa: ${formatClassicMoney(_financesData!['negativeCash']['estimatedNextRoundInterest'])}. '
                'Separados dos juros do empréstimo.',
              ),
          ],
          ...loans
              .where((l) => l['isPaidOff'] != true)
              .map(
                (l) => ListTile(
                  title: Text(
                    '${l['mode'] == "ClassicFixed" ? "Empréstimo clássico" : "Empréstimo legado em prestações"} — dívida ${formatClassicMoney(l['remainingBalance'] ?? 0)}',
                  ),
                  subtitle: Text(
                    'Principal contratado: ${formatClassicMoney(l['principalAmount'] ?? 0)} | Principal pendente: ${l['outstandingPrincipal'] == null ? "composição anterior indisponível" : formatClassicMoney(l['outstandingPrincipal'])} | Juros: ${loanInterest(l)} | Juros pagos: ${formatClassicMoney(l['interestPaid'] ?? 0)} | Juros pendentes: ${formatClassicMoney(l['unpaidInterest'] ?? 0)}',
                  ),
                  trailing: OutlinedButton(
                    onPressed: _commandBusy
                        ? null
                        : () => _runUiCommand(() async {
                            await widget.apiClient.repayBankLoan(
                              _worldId!,
                              _myClubId!,
                              _coachId!,
                              l['id'] as String,
                            );
                            await _refreshClubAndSecondaryData();
                          }),
                    child: const Text('QUITAR'),
                  ),
                ),
              ),
          ...salaryRequests.map(
            (r) => Card(
              child: Padding(
                padding: const EdgeInsets.all(12),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'PEDIDO SALARIAL — ${r['reason'] == "DIVISION_PROMOTION" ? "ACESSO DE DIVISÃO" : "EVOLUÇÃO DE FORÇA"}',
                    ),
                    Text(
                      'Jogador: ${(_myClubDetail!['squad'] as List<dynamic>).where((p) => p['id'] == r['playerId']).map((p) => p['displayName']).firstOrNull ?? r['playerId']}',
                    ),
                    Text(
                      '${formatClassicMoney(r['currentSalary'])} → ${formatClassicMoney(r['requestedSalary'])} por partida; ${r['renewalMatches']} partidas de contrato',
                    ),
                    Text(
                      'Acréscimo por partida: ${formatClassicMoney(r['differencePerMatch'] ?? 0)} | Compromisso salarial: ${formatClassicMoney(r['totalSalaryCommitment'] ?? 0)}',
                    ),
                    if (r['mandatory'] == true)
                      const Text('RENOVAÇÃO OBRIGATÓRIA: ELENCO MÍNIMO.'),
                    Wrap(
                      spacing: 8,
                      children: [
                        for (final accept in [true, false])
                          OutlinedButton(
                            onPressed:
                                _commandBusy ||
                                    !accept && r['mandatory'] == true
                                ? null
                                : () => _runUiCommand(() async {
                                    final message = await widget.apiClient
                                        .respondSalaryRequest(
                                          _worldId!,
                                          _myClubId!,
                                          _coachId!,
                                          r['id'] as String,
                                          accept,
                                        );
                                    await _refreshClubAndSecondaryData();
                                    if (message != null) {
                                      setState(() => _statusBanner = message);
                                    }
                                  }),
                            child: Text(accept ? 'ACEITAR' : 'RECUSAR'),
                          ),
                      ],
                    ),
                  ],
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }

  /// Tab 5: Ranking Clássico de Treinadores, Carreira, Histórico de Temporadas e Convites
  Widget _buildCoachMorale() {
    final people =
        _snapshot?['readyCounter']?['participants'] as List<dynamic>? ?? [];
    final me = people.where((p) => p['coachId'] == _coachId).firstOrNull;
    final value = me?['morale'] ?? _invitationsData?['morale'] ?? 70;
    final band =
        me?['moraleBand'] ?? _invitationsData?['moraleBand'] ?? 'VERDE';
    return MoraleBar(
      key: const ValueKey('coach-morale'),
      value: value as num,
      band: band.toString(),
    );
  }

  Future<void> _respondCareerInvitation(
    Map<dynamic, dynamic> invitation,
    bool accept,
  ) async {
    if (_worldId == null || _coachId == null) return;
    await _runUiCommand(() async {
      if (accept) {
        await widget.apiClient.acceptInvitation(
          _worldId!,
          invitation['invitationId'].toString(),
          _coachId!,
        );
      } else {
        await widget.apiClient.rejectInvitation(
          _worldId!,
          invitation['invitationId'].toString(),
          _coachId!,
        );
      }
      final snap = await widget.apiClient.getSnapshot(_worldId!);
      final people =
          snap['readyCounter']?['participants'] as List<dynamic>? ?? [];
      final me = people.where((p) => p['coachId'] == _coachId).firstOrNull;
      if (!mounted) return;
      setState(() {
        _snapshot = _acceptSnapshot(snap);
        if (me != null) {
          _myClubId = me['clubId']?.toString();
          _isUnemployed = me['isUnemployed'] == true;
        }
        _myClubDetail = null;
        _selectedStarterIds.clear();
        _selectedBenchIds.clear();
        _statusBanner = accept
            ? 'Você assumiu o comando do ${invitation['clubName']}!'
            : 'Convite recusado. O clube seguirá sua busca.';
      });
      await _saveSession();
      await _refreshClubAndSecondaryData();
    });
  }

  Widget _buildCareerRankingAndInvitationsScreen() {
    final ranking = _rankingData?['ranking'] as List<dynamic>? ?? [];
    final seasonHist = _rankingData?['seasonHistory'] as List<dynamic>? ?? [];
    final invitations =
        _invitationsData?['invitations'] as List<dynamic>? ?? [];

    return ClassicCoachScreen(
      ranking: ranking,
      onCoach: _showCoachCareerDetailDialog,
      career: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          _buildCoachMorale(),
          const SizedBox(height: 8),
          const Text(
            'Moral vermelha antes de uma derrota provoca demissão. Campeonatos > Taças > pontos de carreira.',
          ),
          const SizedBox(height: 8),
          ...((_invitationsData?['moraleHistory'] as List<dynamic>? ?? [])
                  .reversed
                  .take(5))
              .map(
                (h) => Text(
                  'T${h['season']} R${h['round']}: ${h['before']} → ${h['after']} (${h['matchId'] == null ? 'recuperação sem clube' : 'após partida'})',
                  style: const TextStyle(fontSize: 12),
                ),
              ),
          const SizedBox(height: 12),
          if (invitations.isNotEmpty) ...[
            Text(
              'CONVITES DE EMPREGO DISPONÍVEIS PARA VOCÊ',
              style: TextStyle(
                color: ClassicTheme.of(context).accent,
                fontWeight: FontWeight.bold,
                fontSize: 14,
              ),
            ),
            const SizedBox(height: 6),
            ...invitations.map((inv) {
              return Container(
                margin: const EdgeInsets.only(bottom: 6),
                padding: const EdgeInsets.all(10),
                decoration: BoxDecoration(
                  color: ClassicTheme.of(context).buttonBackground,
                  border: Border.all(
                    color: ClassicTheme.of(context).selectedBackground,
                  ),
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      '${inv['clubName']} (${inv['divisionDisplay']}) — Caixa: ${formatClassicMoney(inv['clubBudget'])} | Elenco: ${inv['squadSize']} atletas',
                      style: TextStyle(
                        fontWeight: FontWeight.bold,
                        fontSize: 12,
                        color: ClassicTheme.of(context).accent,
                      ),
                    ),
                    Text(
                      'Posição: ${inv['tablePosition'] ?? '-'} • Força: ${inv['squadStrength'] ?? '-'} • Estádio: ${inv['stadiumCapacity'] ?? '-'} lugares',
                      style: const TextStyle(fontSize: 12),
                    ),
                    Text(
                      'Objetivo: ${inv['objective'] ?? 'Desenvolver o clube'}',
                      style: const TextStyle(fontSize: 12),
                    ),
                    Text(
                      'Projeto atual: ${(_invitationsData?['currentProjectScore'] as num? ?? 0).toStringAsFixed(1)} • Melhoria proposta: ${(inv['projectImprovement'] as num? ?? 0).toStringAsFixed(1)}',
                      style: const TextStyle(fontSize: 12),
                    ),
                    const SizedBox(height: 4),
                    Text(
                      _isUnemployed
                          ? 'Consequência: Você assumirá imediatamente o comando técnico do ${inv['clubName']}.'
                          : 'Consequência: Aceitar encerrará seu contrato atual com ${_myClubDetail?['name'] ?? "seu clube"} para assumir o ${inv['clubName']}.',
                      style: const TextStyle(
                        fontSize: 11,
                        color: Colors.amberAccent,
                      ),
                    ),
                    if (inv['isAccepted'] != true) ...[
                      const SizedBox(height: 6),
                      Wrap(
                        spacing: 8,
                        children: [
                          ElevatedButton(
                            onPressed: _commandBusy
                                ? null
                                : () => _respondCareerInvitation(
                                    inv as Map,
                                    true,
                                  ),
                            child: const Text('ACEITAR'),
                          ),
                          OutlinedButton(
                            onPressed: _commandBusy
                                ? null
                                : () => _respondCareerInvitation(
                                    inv as Map,
                                    false,
                                  ),
                            child: const Text('RECUSAR'),
                          ),
                        ],
                      ),
                    ],
                  ],
                ),
              );
            }),
            const SizedBox(height: 16),
          ],
          ...(_invitationsData?['employmentHistory'] as List<dynamic>? ?? [])
              .reversed
              .take(8)
              .map(
                (h) => Text(
                  '${h['clubName']} — T${h['startSeason']} R${h['startRound']} até ${h['endSeason'] == null ? 'atual' : 'T${h['endSeason']} R${h['endRound']}'}',
                  style: const TextStyle(fontSize: 12),
                ),
              ),
          const SizedBox(height: 8),
          if (seasonHist.isNotEmpty) ...[
            const SizedBox(height: 20),
            Text(
              'GALERIA DE CAMPEÕES POR TEMPORADA',
              style: TextStyle(
                color: ClassicTheme.of(context).accent,
                fontWeight: FontWeight.bold,
                fontSize: 14,
              ),
            ),
            const SizedBox(height: 6),
            ...seasonHist.map((h) {
              return Text(
                'Temporada ${h['seasonNumber']}: Campeão 1ª Div: ${h['firstDivisionChampionName']} | Taça: ${h['cupChampionName']} | Artilheiro: ${h['topScorerPlayerName']} (${h['topScorerGoals']} gols)',
                style: const TextStyle(fontSize: 12),
              );
            }),
          ],
          const SizedBox(height: 20),
          SeasonTopScorersPanel(
            apiClient: widget.apiClient,
            worldId: _worldId!,
            currentSeason: (_snapshot?['seasonNumber'] as num?)?.toInt(),
            revision: (_snapshot?['roundVersion'] as num?)?.toInt() ?? 0,
          ),
        ],
      ),
    );
  }

  String _formatPosition(dynamic raw) {
    if (raw is String) return raw;
    if (raw is int) {
      return switch (raw) {
        0 => 'G',
        1 => 'D',
        2 => 'M',
        3 => 'A',
        _ => 'M',
      };
    }
    return raw?.toString() ?? 'M';
  }
}
