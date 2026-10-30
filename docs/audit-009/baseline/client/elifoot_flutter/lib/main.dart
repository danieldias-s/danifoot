import 'package:flutter/material.dart';
import 'dart:convert';
import 'dart:math';
import 'package:flutter/semantics.dart';
import 'api_client.dart';
import 'session_store.dart';
part 'shell_actions.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await initializeSessionStore();
  SemanticsBinding.instance.ensureSemantics();
  runApp(const ElifootClassicApp());
}

class ElifootClassicApp extends StatelessWidget {
  final ElifootApiClient? apiClient;
  const ElifootClassicApp({super.key, this.apiClient});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'Elifoot Classic Multiplayer',
      debugShowCheckedModeBanner: false,
      theme: ThemeData(
        useMaterial3: false,
        brightness: Brightness.dark,
        scaffoldBackgroundColor: const Color(0xFF0A4D1C),
        primaryColor: const Color(0xFF136F2D),
        colorScheme: const ColorScheme.dark(
          primary: Color(0xFFFFD700),
          secondary: Color(0xFF00E676),
          surface: Color(0xFF0E3B19),
        ),
        fontFamily: 'monospace',
        elevatedButtonTheme: ElevatedButtonThemeData(
          style: ElevatedButton.styleFrom(
            backgroundColor: const Color(0xFFC0C0C0),
            foregroundColor: Colors.black,
            shape: const RoundedRectangleBorder(borderRadius: BorderRadius.zero),
            textStyle: const TextStyle(fontWeight: FontWeight.bold, fontSize: 12),
            padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
          ),
        ),
      ),
      home: ElifootShellScreen(apiClient: apiClient ?? ElifootApiClient()),
    );
  }
}

class ElifootShellScreen extends StatefulWidget {
  final ElifootApiClient apiClient;
  const ElifootShellScreen({super.key, required this.apiClient});

  @override
  State<ElifootShellScreen> createState() => _ElifootShellScreenState();
}

class _ElifootShellScreenState extends State<ElifootShellScreen> {
  // Session & World state
  String _nickname = 'Treinador 1';
  String _deviceId = 'device-${DateTime.now().microsecondsSinceEpoch}-${Random().nextInt(1 << 30)}';
  late final TextEditingController _nicknameController;
  late final TextEditingController _deviceController;
  final _packageController = TextEditingController();
  List<dynamic> _calendar = [];
  List<dynamic> _editorClubs = [];
  List<dynamic> _editorPlayers = [];
  List<dynamic> _editorManagers = [];
  String? _coachId;
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
  List<dynamic> _topScorers = [];
  Map<String, dynamic>? _marketData;
  Map<String, dynamic>? _financesData;
  Map<String, dynamic>? _rankingData;
  Map<String, dynamic>? _invitationsData;

  // Squad & Tactic preparation state
  String _selectedFormation = '4-4-2';
  List<String> _selectedStarterIds = [];
  List<String> _selectedBenchIds = [];

  // In-match substitution selection state
  String? _subPlayerOutId;
  String? _subPlayerInId;
  String? _inMatchFormation;

  // Editor state
  String _editorJsonText = '';
  Map<String, dynamic>? _packageValidationResult;

  int _activeTabIndex = 0;
  String? _statusBanner;
  bool _isLoading = false;
  bool _commandBusy = false;

  static const List<String> availableFormations = [
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

  @override
  void initState() {
    super.initState();
    final saved = readSession();
    Map<String, dynamic>? session;
    try { if (saved != null) session = jsonDecode(saved); } catch (_) {}
    _nickname = session?['nickname'] ?? _nickname;
    _deviceId = session?['deviceId'] ?? _deviceId;
    _nicknameController = TextEditingController(text: _nickname);
    _deviceController = TextEditingController(text: _deviceId);
    widget.apiClient.onSnapshotUpdated = (snap) {
      if (!mounted) return;
      if ((_snapshot?['roundVersion'] as num? ?? 0) > (snap['roundVersion'] as num? ?? 0)) return;
      final prevState = _snapshot?['roundState'];
      final newState = snap['roundState'];
      setState(() {
        _snapshot = _preferLatestSnapshot(snap);
        final people = snap['readyCounter']?['participants'] as List<dynamic>? ?? [];
        final me = people.where((p) => p['coachId'] == _coachId).firstOrNull;
        if (me != null) {
          _myClubId = me['clubId']?.toString();
          _isUnemployed = me['isUnemployed'] == true;
          if (_isUnemployed) _myClubDetail = null;
        }
        if (prevState == 'PREPARING' && newState != 'PREPARING') _activeTabIndex = 1;
      });
      if (prevState != newState && (newState == 'PREPARING' || newState == 'RESULTS')) {
        _refreshClubAndSecondaryData();
      }
    };
    widget.apiClient.onReadyCounterUpdated = (counter) {
      if (!mounted || _snapshot == null) return;
      if ((_snapshot?['roundVersion'] as num? ?? 0) > (counter['roundVersion'] as num? ?? 0)) return;
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
        await _performLocalLoginAndLoadWorlds();
        await _joinSelectedWorld(session!['worldId']);
      });
    }
  }

  @override
  void dispose() {
    _nicknameController.dispose();
    _deviceController.dispose();
    _packageController.dispose();
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
        _coachId = auth['coachId']?.toString();
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

  Future<void> _createNewWorldDialog() async {
    final nameCtrl = TextEditingController(text: 'Liga Clássica 98');
    int clubs = 128;
    int maxHumans = 1;
    final created = await showDialog<bool>(
      context: context,
      builder: (ctx) => StatefulBuilder(
        builder: (ctx, setDialogState) => AlertDialog(
          backgroundColor: const Color(0xFF0D3B18),
          title: const Text('NOVO CAMPEONATO / SALA', style: TextStyle(color: Color(0xFFFFD700))),
          content: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              TextField(
                controller: nameCtrl,
                decoration: const InputDecoration(labelText: 'Nome da Sala / Campeonato'),
              ),
              const SizedBox(height: 10),
              DropdownButtonFormField<int>(
                isExpanded: true,
                initialValue: clubs,
                decoration: const InputDecoration(labelText: 'Total de Clubes (32 Divisão + Distritais)'),
                items: const [
                  DropdownMenuItem(value: 128, child: Text('128 Clubes (32 Div + 96 Distritais)', maxLines: 1, overflow: TextOverflow.ellipsis)),
                  DropdownMenuItem(value: 256, child: Text('256 Clubes (32 Div + 224 Distritais)', maxLines: 1, overflow: TextOverflow.ellipsis)),
                  DropdownMenuItem(value: 512, child: Text('512 Clubes (32 Div + 480 Distritais)', maxLines: 1, overflow: TextOverflow.ellipsis)),
                  DropdownMenuItem(value: 1024, child: Text('1024 Clubes (32 Div + 992 Distritais)', maxLines: 1, overflow: TextOverflow.ellipsis)),
                ],
                onChanged: (v) => setDialogState(() => clubs = v ?? 128),
              ),
              const SizedBox(height: 10),
              DropdownButtonFormField<int>(
                isExpanded: true,
                initialValue: maxHumans,
                decoration: const InputDecoration(labelText: 'Máximo de Treinadores Humanos'),
                items: const [1, 2, 4, 6, 8]
                    .map((n) => DropdownMenuItem(value: n, child: Text('$n Treinadores Humanos')))
                    .toList(),
                onChanged: (v) => setDialogState(() => maxHumans = v ?? 8),
              ),
            ],
          ),
          actions: [
            TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('CANCELAR')),
            ElevatedButton(
              onPressed: () => Navigator.pop(ctx, true),
              child: const Text('CRIAR SALA'),
            ),
          ],
        ),
      ),
    );

    if (created == true) {
      setState(() => _isLoading = true);
      try {
        await widget.apiClient.createWorld(
          worldName: nameCtrl.text,
          maxHumans: maxHumans,
          totalClubs: clubs,
        );
        final worlds = await widget.apiClient.listWorlds();
        setState(() => _worlds = worlds);
      } catch (e) {
        setState(() => _statusBanner = 'Erro ao criar sala: $e');
      } finally {
        setState(() => _isLoading = false);
      }
    }
  }

  Future<void> _joinSelectedWorld(String worldId, {String? preferredClubId, bool joinUnemployed = false}) async {
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
        _worldId = worldId;
        _myClubId = res['clubId']?.toString();
        _isUnemployed = res['isUnemployed'] == true;
        _snapshot = _preferLatestSnapshot(snap);
      });
      widget.apiClient.connectRealtimeRoom(worldId, _coachId!);
      await _saveSession();
      await _refreshClubAndSecondaryData();
    } catch (e) {
      setState(() => _statusBanner = 'Erro ao entrar na sala: $e');
    } finally {
      if (mounted) setState(() => _isLoading = false);
    }
  }

  Future<void> _refreshClubAndSecondaryData() async {
    if (_worldId == null) return;
    try {
      final standings = await widget.apiClient.getStandings(_worldId!, _selectedDivisionTier);
      final cup = await widget.apiClient.getCupFixtures(_worldId!);
      final scorers = await widget.apiClient.getTopScorers(_worldId!);
      final market = await widget.apiClient.getMarket(_worldId!);
      final ranking = await widget.apiClient.getRankingAndHistory(_worldId!);
      final calendar = await widget.apiClient.getCalendar(_worldId!);

      Map<String, dynamic>? clubDetail;
      Map<String, dynamic>? finances;
      if (_myClubId != null && _myClubId!.isNotEmpty) {
        clubDetail = await widget.apiClient.getClubDetail(_worldId!, _myClubId!);
        finances = await widget.apiClient.getClubFinances(_worldId!, _myClubId!);
      }

      Map<String, dynamic>? invites;
      if (_coachId != null) {
        invites = await widget.apiClient.getInvitations(_worldId!, _coachId!);
      }

      if (!mounted) return;
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
          if (_selectedStarterIds.isEmpty || !_selectedStarterIds.every((id) => (clubDetail!['squad'] as List).any((p) => p['id'] == id))) {
          _selectedFormation = clubDetail['preferredFormation']?.toString() ?? '4-4-2';
          _selectedStarterIds = (clubDetail['preferredStarterIds'] as List<dynamic>? ?? [])
              .map((e) => e.toString())
              .toList();
          _selectedBenchIds = (clubDetail['preferredBenchIds'] as List<dynamic>? ?? [])
              .map((e) => e.toString())
              .toList();
          }
        }
        if (finances != null) {
          _financesData = finances;
        }
      });
    } catch (e) {
      if (mounted) setState(() => _statusBanner = 'Aviso: $e');
    }
  }

  Future<void> _handleSendTacticsOrReady() async {
    if (_worldId == null || _coachId == null || _snapshot == null) return;
    final roundVersion = (_snapshot!['roundVersion'] as num?)?.toInt() ?? 1;
    try {
      Map<String, dynamic> res;
      if (_isUnemployed || _myClubId == null) {
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
      final started = res['roundStarted'] == true;
      final snap = await widget.apiClient.getSnapshot(_worldId!);
      setState(() {
        _snapshot = _preferLatestSnapshot(snap);
        if (counter != null) {
          _snapshot!['readyCounter'] = counter;
        }
        _statusBanner = started
            ? 'TODOS PRONTOS! RODADA INICIADA SINCRONIZADAMENTE!'
            : 'Tática enviada! Aguardando demais treinadores humanos...';
        if (started) {
          _activeTabIndex = 1; // Switch to Jogos ao Vivo tab automatically!
        }
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
        _snapshot = _preferLatestSnapshot(snap);
        _statusBanner = 'PARADA GLOBAL ATIVADA! Todas as partidas da sala foram congeladas.';
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
        _snapshot = _preferLatestSnapshot(res['snapshot'] as Map<String, dynamic>?);
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
    if (_coachId == null || _worldId == null) {
      return _buildLobbyAndHomeScreen();
    }

    return Scaffold(
      body: SafeArea(
        child: Column(
          children: [
            ConstrainedBox(
              constraints: BoxConstraints(maxHeight: MediaQuery.sizeOf(context).height * 0.44),
              child: SingleChildScrollView(child: _buildTopSynchronizedHeader()),
            ),
            if (_statusBanner != null)
              Container(
                width: double.infinity,
                color: const Color(0xFF1B5E20),
                padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 4),
                child: Row(
                  children: [
                    const Icon(Icons.info_outline, size: 16, color: Color(0xFFFFD700)),
                    const SizedBox(width: 8),
                    Expanded(
                      child: Text(
                        _statusBanner!,
                        style: const TextStyle(fontSize: 12, color: Colors.white),
                      ),
                    ),
                    IconButton(
                      icon: const Icon(Icons.close, size: 14),
                      padding: EdgeInsets.zero,
                      constraints: const BoxConstraints(),
                      onPressed: () => setState(() => _statusBanner = null),
                    ),
                  ],
                ),
              ),
            _buildClassicNavigationMenu(),
            Expanded(
              child: _buildActiveScreenBody(),
            ),
          ],
        ),
      ),
    );
  }

  /// Screen 1 & 2: Início, Login LocalDev, Novo Campeonato, Entrar em Sala e Seleção de Clube
  Widget _buildLobbyAndHomeScreen() {
    return Scaffold(
      appBar: AppBar(
        backgroundColor: const Color(0xFF073612),
        title: const Text(
          'ELIFOOT CLASSIC MULTIPLAYER — EDIÇÃO 98',
          style: TextStyle(color: Color(0xFFFFD700), fontWeight: FontWeight.bold, fontSize: 16),
        ),
        actions: [
          if (_coachId != null)
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
              child: Chip(
                backgroundColor: const Color(0xFF136F2D),
                label: Text('Técnico: $_nickname', style: const TextStyle(fontSize: 12)),
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
                // Classic Banner
                Container(
                  padding: const EdgeInsets.all(16),
                  decoration: BoxDecoration(
                    color: const Color(0xFF0E3B19),
                    border: Border.all(color: const Color(0xFFFFD700), width: 2),
                  ),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const Text(
                        'GESTÃO CLÁSSICA DE FUTEBOL • MULTIPLAYER ATÉ 8 TÉCNICOS • MOTOR AUTORITATIVO',
                        style: TextStyle(color: Color(0xFFFFD700), fontWeight: FontWeight.bold, fontSize: 14),
                      ),
                      const SizedBox(height: 8),
                      const Text(
                        '• 128 Clubes no Brasil (4 Divisões de 8 + 96 Distritais na Taça)\n'
                        '• Contador Sincronizado (TÁTICAS ENVIADAS: 0/6) e Parada Global compartilhada\n'
                        '• Técnicos de IA com filosofia tática persistente e mercado bilateral racional',
                        style: TextStyle(fontSize: 12, color: Colors.white70),
                      ),
                      const SizedBox(height: 12),
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
                                border: OutlineInputBorder(),
                              ),
                              controller: _nicknameController,
                              onChanged: (v) => _nickname = v,
                            ),
                          ),
                          SizedBox(
                            width: 190,
                            child: TextField(
                              decoration: const InputDecoration(
                                labelText: 'ID Dispositivo (Sessão)',
                                isDense: true,
                                border: OutlineInputBorder(),
                              ),
                              controller: _deviceController,
                              onChanged: (v) => _deviceId = v,
                            ),
                          ),
                          ElevatedButton.icon(
                            onPressed: _isLoading ? null : _performLocalLoginAndLoadWorlds,
                            icon: const Icon(Icons.login, size: 16),
                            label: const Text('ENTRAR NO LOBBY (LOCAL DEV)'),
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
                            style: const TextStyle(color: Colors.amberAccent, fontSize: 11),
                          ),
                        ),
                      ],
                      if (_statusBanner != null) ...[
                        const SizedBox(height: 8),
                        Text(_statusBanner!, style: const TextStyle(color: Colors.redAccent, fontSize: 12)),
                      ],
                    ],
                  ),
                ),
                const SizedBox(height: 16),
                if (_coachId != null) ...[
                  Wrap(
                    spacing: 12,
                    runSpacing: 8,
                    children: [
                      const Text(
                        'SALAS / MUNDOS DISPONÍVEIS',
                        style: TextStyle(color: Color(0xFFFFD700), fontWeight: FontWeight.bold, fontSize: 15),
                      ),
                      Wrap(
                        spacing: 8,
                        runSpacing: 8,
                        children: [
                          ElevatedButton.icon(
                            onPressed: _createNewWorldDialog,
                            icon: const Icon(Icons.add, size: 16),
                            label: const Text('NOVO CAMPEONATO / SALA'),
                          ),
                          const SizedBox(width: 8),
                          OutlinedButton.icon(
                            onPressed: _performLocalLoginAndLoadWorlds,
                            icon: const Icon(Icons.refresh, size: 16),
                            label: const Text('ATUALIZAR'),
                          ),
                          OutlinedButton(onPressed: _joinByCodeDialog, child: const Text('ENTRAR POR CÓDIGO')),
                        ],
                      ),
                    ],
                  ),
                  const SizedBox(height: 8),
                  if (_worlds.isEmpty)
                    const Padding(
                      padding: EdgeInsets.all(24),
                      child: Text('Nenhuma sala encontrada. Clique em NOVO CAMPEONATO / SALA.'),
                    )
                  else
                    ..._worlds.map((w) {
                      final wid = w['id'].toString();
                      final counter = w['readyCounter'] as Map<String, dynamic>?;
                      final counterLabel = counter?['counterLabel']?.toString() ?? 'TÁTICAS ENVIADAS: 0/0';
                      return Container(
                        margin: const EdgeInsets.only(bottom: 10),
                        padding: const EdgeInsets.all(12),
                        decoration: BoxDecoration(
                          color: const Color(0xFF0E3B19),
                          border: Border.all(color: Colors.greenAccent.withValues(alpha: 0.4)),
                        ),
                        child: Wrap(
                          spacing: 8,
                          runSpacing: 8,
                          children: [
                            SizedBox(
                              width: MediaQuery.sizeOf(context).width < 700 ? MediaQuery.sizeOf(context).width - 72 : 420,
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Text(
                                    '${w['name']}  [Código: ${w['inviteCode']}]',
                                    style: const TextStyle(
                                      color: Color(0xFFFFD700),
                                      fontWeight: FontWeight.bold,
                                      fontSize: 14,
                                    ),
                                  ),
                                  const SizedBox(height: 4),
                                  Text(
                                    'Temporada ${w['seasonNumber']} • Rodada ${w['roundNumber']} • Estado: ${w['roundState']} • Clubes: ${w['totalClubs']} • $counterLabel',
                                    style: const TextStyle(fontSize: 12, color: Colors.white70),
                                  ),
                                ],
                              ),
                            ),
                            ElevatedButton(
                              key: Key('join-world-$wid'),
                              onPressed: () => _chooseClub(wid),
                              child: const Text('ENTRAR COM CLUBE'),
                            ),
                            const SizedBox(width: 8),
                            OutlinedButton(
                              onPressed: () => _joinSelectedWorld(wid, joinUnemployed: true),
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

  /// Persistent Top Header required by Section 3.1, 3.3, and 12:
  /// Shows `TÁTICAS ENVIADAS: X/N` + names in preparation, and `MINUTO XX'`, `SUBSTITUIÇÕES X/5`, `PARADAS X/3`, `PEDIR PARADA` in match!
  Widget _buildTopSynchronizedHeader() {
    final snap = _snapshot;
    final roundState = snap?['roundState']?.toString() ?? 'PREPARING';
    final readyCounter = snap?['readyCounter'] as Map<String, dynamic>?;
    final counterLabel = readyCounter?['counterLabel']?.toString() ?? 'TÁTICAS ENVIADAS: 0/1';
    final participants = readyCounter?['participants'] as List<dynamic>? ?? [];
    final currentMinute = (snap?['currentMinute'] as num?)?.toInt() ?? 0;
    final pauseWindow = snap?['activePauseWindow'] as Map<String, dynamic>?;

    // Find current coach's participant info
    Map<String, dynamic>? myPart;
    for (final p in participants) {
      if (p['coachId']?.toString() == _coachId) {
        myPart = p as Map<String, dynamic>;
        break;
      }
    }

    final isReady = myPart?['isReady'] == true;
    final usedSubs = myPart?['usedSubstitutions'] ?? 0;
    final maxSubs = myPart?['maxSubstitutions'] ?? 5;
    final usedWins = myPart?['usedPauseWindows'] ?? 0;
    final maxWins = myPart?['maxPauseWindows'] ?? 3;
    final clubName = _myClubDetail?['name']?.toString() ?? (myPart?['clubName']?.toString() ?? 'Desempregado');
    final cash = (_myClubDetail?['cashBalance'] as num?)?.toDouble() ?? 0.0;

    return Container(
      width: double.infinity,
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
      decoration: const BoxDecoration(
        color: Color(0xFF062E0F),
        border: Border(bottom: BorderSide(color: Color(0xFFFFD700), width: 2)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Wrap(
            spacing: 14,
            runSpacing: 6,
            crossAxisAlignment: WrapCrossAlignment.center,
            children: [
              Text(
                '${snap?['worldName'] ?? 'Sala'} • Temp ${snap?['seasonNumber'] ?? 1} • Rodada ${snap?['roundNumber'] ?? 1} (${snap?['competitionLabel'] ?? ''})',
                style: const TextStyle(color: Color(0xFFFFD700), fontWeight: FontWeight.bold, fontSize: 13),
              ),
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 3),
                color: const Color(0xFF136F2D),
                child: Text(
                  'TÉCNICO: $_nickname ($clubName) • CAIXA: R\$ ${cash.toStringAsFixed(0)}',
                  style: const TextStyle(fontSize: 12, fontWeight: FontWeight.bold),
                ),
              ),
              // Always-visible Ready Counter badge
              _buildCoachMorale(),
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                decoration: BoxDecoration(
                  color: const Color(0xFF1B5E20),
                  border: Border.all(color: const Color(0xFFFFD700)),
                ),
                child: Text(
                  counterLabel,
                  key: const Key('ready-counter-label'),
                  style: const TextStyle(color: Color(0xFFFFD700), fontWeight: FontWeight.bold, fontSize: 13),
                ),
              ),
              if (roundState == 'PREPARING') ...[
                if (!isReady)
                  ElevatedButton.icon(
                    key: const Key('btn-enviar-tatica'),
                    onPressed: _commandBusy || (!_isUnemployed && _selectedStarterIds.length != 11) ? null : () => _runUiCommand(_handleSendTacticsOrReady),
                    icon: const Icon(Icons.check_circle, size: 15),
                    label: Text(_isUnemployed ? 'PRONTO PARA ACOMPANHAR' : 'ENVIAR TÁTICA'),
                  )
                else
                  OutlinedButton.icon(
                    key: const Key('btn-retirar-pronto'),
                    onPressed: _commandBusy ? null : () => _runUiCommand(_handleWithdrawReady),
                    icon: const Icon(Icons.undo, size: 15),
                    label: const Text('RETIRAR PRONTO'),
                  ),
              ],
              if (roundState != 'PREPARING') ...[
                Container(
                  padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                  color: Colors.black54,
                  child: Text(
                    "MINUTO $currentMinute'  |  SUBSTITUIÇÕES $usedSubs/$maxSubs  |  PARADAS $usedWins/$maxWins",
                    key: const Key('live-match-quota-header'),
                    style: const TextStyle(color: Colors.lightGreenAccent, fontWeight: FontWeight.bold, fontSize: 12),
                  ),
                ),
                if (roundState == 'RUNNING_FIRST_HALF' ||
                    roundState == 'RUNNING_SECOND_HALF' ||
                    roundState == 'RUNNING_EXTRA_TIME')
                  ElevatedButton.icon(
                    key: const Key('btn-pedir-parada'),
                    style: ElevatedButton.styleFrom(backgroundColor: Colors.redAccent, foregroundColor: Colors.white),
                    onPressed: _commandBusy ? null : () => _runUiCommand(_handleRequestGlobalPause),
                    icon: const Icon(Icons.pause_circle_filled, size: 15),
                    label: const Text('PEDIR PARADA'),
                  ),
                if (roundState == 'RESULTS')
                  ElevatedButton.icon(
                    key: const Key('btn-proxima-rodada'),
                    onPressed: _commandBusy ? null : () => _runUiCommand(() async {
                      final nextSnap = await widget.apiClient.advanceToNextRound(_worldId!);
                      setState(() {
                        _snapshot = _preferLatestSnapshot(nextSnap);
                        _activeTabIndex = 0;
                        _selectedStarterIds = [];
                      });
                      await _refreshClubAndSecondaryData();
                    }),
                    icon: const Icon(Icons.skip_next, size: 16),
                    label: const Text('AVANÇAR PRÓXIMA RODADA'),
                  ),
              ],
              TextButton.icon(
                onPressed: () async {
                  widget.apiClient.disconnectRealtime();
                  setState(() {
                    _worldId = null;
                    _snapshot = null;
                    _myClubDetail = null;
                    _myClubId = null;
                    _selectedStarterIds = [];
                  });
                  await _saveSession();
                },
                icon: const Icon(Icons.exit_to_app, size: 15),
                label: const Text('TROCAR SALA', style: TextStyle(fontSize: 11)),
              ),
            ],
          ),
          const SizedBox(height: 4),
          // Participant ready pills
          Wrap(
            spacing: 8,
            runSpacing: 4,
            children: participants.map((p) {
              final pName = p['nickname']?.toString() ?? '';
              final pClub = p['clubName']?.toString() ?? '';
              final pReady = p['isReady'] == true;
              return Container(
                padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                decoration: BoxDecoration(
                  color: pReady ? Colors.green.shade800 : Colors.grey.shade900,
                  border: Border.all(color: pReady ? Colors.lightGreenAccent : Colors.white24),
                ),
                child: Text(
                  '${pReady ? "✔" : "⏳"} $pName ($pClub)${p['isConnected'] == false ? " • DESCONECTADO" : ""}',
                  style: TextStyle(
                    fontSize: 11,
                    color: pReady ? Colors.lightGreenAccent : Colors.white70,
                    fontWeight: pReady ? FontWeight.bold : FontWeight.normal,
                  ),
                ),
              );
            }).toList(),
          ),
          // Global Pause / Halftime Banner
          if (pauseWindow != null) ...[
            const SizedBox(height: 6),
            Container(
              width: double.infinity,
              padding: const EdgeInsets.all(8),
              decoration: BoxDecoration(
                color: const Color(0xFF4A1212),
                border: Border.all(color: Colors.amberAccent, width: 2),
              ),
              child: Wrap(
                spacing: 12,
                runSpacing: 6,
                crossAxisAlignment: WrapCrossAlignment.center,
                children: [
                  Text(
                    roundState == 'HALFTIME_PAUSED'
                        ? "INTERVALO DE JOGO (45') — AJUSTES GRATUITOS"
                        : "PARTIDA PAUSADA POR ${pauseWindow['requestedByCoachName'] ?? 'INTERVALO'} (${pauseWindow['minute']}')",
                    style: const TextStyle(color: Color(0xFFFFD700), fontWeight: FontWeight.bold, fontSize: 13),
                  ),
                  Text(
                    "AJUSTES CONFIRMADOS: ${pauseWindow['confirmedCount']}/${pauseWindow['totalHumans']}  |  TEMPO: ${pauseWindow['remainingSeconds']}s",
                    style: const TextStyle(color: Colors.white, fontWeight: FontWeight.bold, fontSize: 12),
                  ),
                  ElevatedButton.icon(
                    key: const Key('btn-confirmar-continuar'),
                    onPressed: _commandBusy ? null : () => _runUiCommand(_handleConfirmContinue),
                    icon: const Icon(Icons.play_arrow, size: 16),
                    label: Text(roundState == 'HALFTIME_PAUSED' ? 'PRONTO PARA O 2º TEMPO' : 'CONTINUAR'),
                  ),
                ],
              ),
            ),
          ],
        ],
      ),
    );
  }

  Widget _buildClassicNavigationMenu() {
    const tabs = [
      (Icons.people, '1. PLANTEL & TÁTICAS'),
      (Icons.sports_soccer, '2. JOGOS AO VIVO'),
      (Icons.leaderboard, '3. DIVISÕES & TAÇA'),
      (Icons.swap_horiz, '4. MERCADO & LEILÃO'),
      (Icons.account_balance, '5. FINANÇAS & ESTÁDIO'),
      (Icons.emoji_events, '6. RANKING & CARREIRA'),
      (Icons.edit_note, '7. EDITOR & PACOTES'),
      (Icons.calendar_month, '8. CALENDÁRIO & RESULTADOS'),
    ];

    return Container(
      width: double.infinity,
      color: const Color(0xFF093915),
      padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
      child: SingleChildScrollView(
        scrollDirection: Axis.horizontal,
        child: Row(
          children: List.generate(tabs.length, (idx) {
            final selected = _activeTabIndex == idx;
            return Padding(
              padding: const EdgeInsets.only(right: 6),
              child: InkWell(
                onTap: () {
                  setState(() => _activeTabIndex = idx);
                  _refreshClubAndSecondaryData();
                },
                child: Container(
                  padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
                  decoration: BoxDecoration(
                    color: selected ? const Color(0xFFFFD700) : const Color(0xFF124E21),
                    border: Border.all(color: Colors.white24),
                  ),
                  child: Row(
                    children: [
                      Icon(tabs[idx].$1, size: 14, color: selected ? Colors.black : Colors.white),
                      const SizedBox(width: 5),
                      Text(
                        tabs[idx].$2,
                        style: TextStyle(
                          fontSize: 11,
                          fontWeight: FontWeight.bold,
                          color: selected ? Colors.black : Colors.white,
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            );
          }),
        ),
      ),
    );
  }

  Widget _buildActiveScreenBody() {
    switch (_activeTabIndex) {
      case 0:
        return _buildSquadAndTacticsScreen();
      case 1:
        return _buildLiveMatchesAndPauseScreen();
      case 2:
        return _buildDivisionsAndCupScreen();
      case 3:
        return _buildMarketAndAuctionsScreen();
      case 4:
        return _buildFinancesAndStadiumScreen();
      case 5:
        return _buildCareerRankingAndInvitationsScreen();
      case 6:
        return _buildEditorAndPackagesScreen();
      case 7:
        return _buildCalendarScreen();
      default:
        return _buildSquadAndTacticsScreen();
    }
  }

  /// Tab 0: Plantel, Escalação (11 titulares, até 9 reservas), Táticas e Plano do Técnico IA
  Widget _buildSquadAndTacticsScreen() {
    if (!_isUnemployed && _myClubId != null && _myClubDetail == null) {
      return const Center(child: Text('CARREGANDO ELENCO...'));
    }
    if (_isUnemployed || _myClubDetail == null) {
      return Center(
        child: Padding(
          padding: const EdgeInsets.all(24),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              const Text(
                'VOCÊ ESTÁ DESEMPREGADO NO MOMENTO',
                style: TextStyle(color: Color(0xFFFFD700), fontWeight: FontWeight.bold, fontSize: 16),
              ),
              const SizedBox(height: 8),
              const Text(
                'Use o botão "PRONTO PARA ACOMPANHAR" no topo para avançar as rodadas com a sala, ou veja a aba "6. RANKING & CARREIRA" para aceitar convites de clubes!',
                textAlign: TextAlign.center,
              ),
              const SizedBox(height: 16),
              ElevatedButton(
                onPressed: () => setState(() => _activeTabIndex = 5),
                child: const Text('VER CONVITES DE EMPREGO'),
              ),
            ],
          ),
        ),
      );
    }

    final squad = _myClubDetail!['squad'] as List<dynamic>? ?? [];
    final aiPlan = _myClubDetail!['aiSquadPlan'] as Map<String, dynamic>?;

    return SingleChildScrollView(
      padding: const EdgeInsets.all(12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          // Tactical controls bar
          Container(
            padding: const EdgeInsets.all(10),
            decoration: BoxDecoration(
              color: const Color(0xFF0E3B19),
              border: Border.all(color: Colors.greenAccent.withValues(alpha: 0.4)),
            ),
            child: Wrap(
              spacing: 16,
              runSpacing: 8,
              crossAxisAlignment: WrapCrossAlignment.center,
              children: [
                Row(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    const Text('ESQUEMA TÁTICO: ', style: TextStyle(fontWeight: FontWeight.bold, fontSize: 12)),
                    DropdownButton<String>(
                      value: availableFormations.contains(_selectedFormation) ? _selectedFormation : '4-4-2',
                      items: availableFormations
                          .map((f) => DropdownMenuItem(value: f, child: Text(f, style: const TextStyle(fontWeight: FontWeight.bold))))
                          .toList(),
                      onChanged: (v) {
                        if (v != null) setState(() => _selectedFormation = v);
                      },
                    ),
                  ],
                ),
                Text(
                  'TITULARES: ${_selectedStarterIds.length}/11  |  BANCO: ${_selectedBenchIds.length}/9  |  ELENCO: ${squad.length}/24',
                  style: TextStyle(
                    fontWeight: FontWeight.bold,
                    fontSize: 12,
                    color: _selectedStarterIds.length == 11 ? Colors.lightGreenAccent : Colors.orangeAccent,
                  ),
                ),
                OutlinedButton.icon(
                  onPressed: () {
                    // Auto-pick best 11 starters + 7 bench
                    final avail = squad.where((p) => (p['injuryRoundsRemaining'] ?? 0) == 0 && (p['suspensionRoundsRemaining'] ?? 0) == 0).toList();
                    final gks = avail.where((p) => p['position'] == 'G' || p['position'] == 0).toList();
                    final outfield = avail.where((p) => p['position'] != 'G' && p['position'] != 0).toList();
                    if (gks.isNotEmpty && outfield.length >= 10) {
                      setState(() {
                        _selectedStarterIds = [
                          gks.first['id'].toString(),
                          ...outfield.take(10).map((p) => p['id'].toString()),
                        ];
                        _selectedBenchIds = [
                          ...gks.skip(1).take(1).map((p) => p['id'].toString()),
                          ...outfield.skip(10).take(8).map((p) => p['id'].toString()),
                        ];
                      });
                    }
                  },
                  icon: const Icon(Icons.auto_fix_high, size: 15),
                  label: const Text('AUTO-ESCALAR 11 + BANCO', style: TextStyle(fontSize: 11)),
                ),
              ],
            ),
          ),
          const SizedBox(height: 10),
          // Dense classic Elifoot player table (preserving literal '*' in DisplayName!)
          SingleChildScrollView(
            scrollDirection: Axis.horizontal,
            child: DataTable(
              headingRowColor: WidgetStateProperty.all(const Color(0xFF062E0F)),
              dataRowMinHeight: 30,
              dataRowMaxHeight: 36,
              columnSpacing: 16,
              columns: const [
                DataColumn(label: Text('STATUS', style: TextStyle(color: Color(0xFFFFD700), fontSize: 11))),
                DataColumn(label: Text('POS', style: TextStyle(color: Color(0xFFFFD700), fontSize: 11))),
                DataColumn(label: Text('JOGADOR (* = CRAQUE)', style: TextStyle(color: Color(0xFFFFD700), fontSize: 11))),
                DataColumn(label: Text('FORÇA', style: TextStyle(color: Color(0xFFFFD700), fontSize: 11))),
                DataColumn(label: Text('COMPORTAMENTO', style: TextStyle(color: Color(0xFFFFD700), fontSize: 11))),
                DataColumn(label: Text('GOLS', style: TextStyle(color: Color(0xFFFFD700), fontSize: 11))),
                DataColumn(label: Text('CONTRATO', style: TextStyle(color: Color(0xFFFFD700), fontSize: 11))),
                DataColumn(label: Text('SALÁRIO', style: TextStyle(color: Color(0xFFFFD700), fontSize: 11))),
                DataColumn(label: Text('VALOR JUSTO', style: TextStyle(color: Color(0xFFFFD700), fontSize: 11))),
                DataColumn(label: Text('AÇÕES', style: TextStyle(color: Color(0xFFFFD700), fontSize: 11))),
              ],
              rows: squad.map((p) {
                final pid = p['id'].toString();
                final displayName = p['displayName']?.toString() ?? p['name'].toString();
                final pos = _formatPosition(p['position']);
                final str = p['strength'] ?? 20;
                final beh = p['behaviorDisplay']?.toString() ?? '';
                final goals = p['seasonGoals'] ?? 0;
                final contract = p['contractRoundsRemaining'] ?? 10;
                final salary = (p['salaryPerRound'] as num?)?.toDouble() ?? 0;
                final fair = (p['fairValue'] as num?)?.toDouble() ?? 0;
                final inj = (p['injuryRoundsRemaining'] as num?)?.toInt() ?? 0;
                final susp = (p['suspensionRoundsRemaining'] as num?)?.toInt() ?? 0;

                final isStarter = _selectedStarterIds.contains(pid);
                final isBench = _selectedBenchIds.contains(pid);

                String roleLabel = 'FORA';
                Color roleColor = Colors.grey;
                if (inj > 0) {
                  roleLabel = 'LES ($inj)';
                  roleColor = Colors.redAccent;
                } else if (susp > 0) {
                  roleLabel = 'SUS ($susp)';
                  roleColor = Colors.orangeAccent;
                } else if (isStarter) {
                  roleLabel = 'TITULAR';
                  roleColor = Colors.lightGreenAccent;
                } else if (isBench) {
                  roleLabel = 'RESERVA';
                  roleColor = Colors.cyanAccent;
                }

                return DataRow(
                  selected: isStarter,
                  cells: [
                    DataCell(
                      InkWell(
                        onTap: (inj > 0 || susp > 0)
                            ? null
                            : () {
                                setState(() {
                                  if (isStarter) {
                                    _selectedStarterIds.remove(pid);
                                    if (_selectedBenchIds.length < 9) _selectedBenchIds.add(pid);
                                  } else if (isBench) {
                                    _selectedBenchIds.remove(pid);
                                  } else {
                                    if (_selectedStarterIds.length < 11) {
                                      _selectedStarterIds.add(pid);
                                    } else if (_selectedBenchIds.length < 9) {
                                      _selectedBenchIds.add(pid);
                                    }
                                  }
                                });
                              },
                        child: Container(
                          padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                          color: roleColor.withValues(alpha: 0.2),
                          child: Text(roleLabel, style: TextStyle(color: roleColor, fontWeight: FontWeight.bold, fontSize: 11)),
                        ),
                      ),
                    ),
                    DataCell(Text(pos, style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 12))),
                    // Literal '*' in player name without replacing with an icon!
                    DataCell(
                      InkWell(
                        onTap: () => _showPlayerDetailDialog(p as Map<String, dynamic>),
                        child: Text(
                          displayName,
                          style: TextStyle(
                            fontWeight: displayName.endsWith('*') ? FontWeight.bold : FontWeight.normal,
                            color: displayName.endsWith('*') ? const Color(0xFFFFD700) : Colors.white,
                            fontSize: 12,
                          ),
                        ),
                      ),
                    ),
                    DataCell(Text('$str', style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 12))),
                    DataCell(Text(beh, style: const TextStyle(fontSize: 11))),
                    DataCell(Text('$goals', style: const TextStyle(fontSize: 11))),
                    DataCell(Text('${contract}r', style: const TextStyle(fontSize: 11))),
                    DataCell(Text('R\$ ${salary.toStringAsFixed(0)}', style: const TextStyle(fontSize: 11))),
                    DataCell(Text('R\$ ${fair.toStringAsFixed(0)}', style: const TextStyle(fontSize: 11))),
                    DataCell(
                      Row(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          TextButton(
                            onPressed: () async {
                              try {
                                await widget.apiClient.renewPlayerContract(_worldId!, pid, _coachId!, 15);
                                await _refreshClubAndSecondaryData();
                                setState(() => _statusBanner = 'Contrato de $displayName renovado (+15 rodadas).');
                              } catch (e) {
                                setState(() => _statusBanner = '$e');
                              }
                            },
                            child: const Text('RENOVAR', style: TextStyle(fontSize: 10)),
                          ),
                          TextButton(
                            onPressed: () async {
                              try {
                                await widget.apiClient.listPlayerForTransfer(_worldId!, _coachId!, pid, fair);
                                await _refreshClubAndSecondaryData();
                                setState(() => _statusBanner = '$displayName anunciado no mercado por R\$ ${fair.toStringAsFixed(0)}.');
                              } catch (e) {
                                setState(() => _statusBanner = '$e');
                              }
                            },
                            child: const Text('VENDER', style: TextStyle(fontSize: 10, color: Colors.amberAccent)),
                          ),
                        ],
                      ),
                    ),
                  ],
                );
              }).toList(),
            ),
          ),
          if (aiPlan != null) ...[
            const SizedBox(height: 16),
            Container(
              padding: const EdgeInsets.all(10),
              decoration: BoxDecoration(
                color: const Color(0xFF062E0F),
                border: Border.all(color: Colors.white24),
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'RELATÓRIO DA COMISSÃO TÉCNICA / IA (${aiPlan['managerName']} — Perfil: ${aiPlan['tacticalIdentity']})',
                    style: const TextStyle(color: Color(0xFFFFD700), fontWeight: FontWeight.bold, fontSize: 12),
                  ),
                  const SizedBox(height: 4),
                  Text('Análise: ${aiPlan['selectionReasoning']}', style: const TextStyle(fontSize: 11)),
                  Text(
                    'Lacunas prioritárias: ${(aiPlan['priorityGaps'] as List<dynamic>? ?? []).join(" | ")}',
                    style: const TextStyle(fontSize: 11, color: Colors.cyanAccent),
                  ),
                ],
              ),
            ),
          ],
        ],
      ),
    );
  }

  void _showPlayerDetailDialog(Map<String, dynamic> player) {
    final displayName = player['displayName']?.toString() ?? '';
    final photoUrl = player['photoUrl']?.toString();
    final photoLicense = player['photoLicense']?.toString() ?? 'Asset interno padrão';
    final photoCredit = player['photoCredit']?.toString() ?? 'Elifoot Classic';

    showDialog(
      context: context,
      builder: (ctx) => AlertDialog(
        backgroundColor: const Color(0xFF0E3B19),
        title: Text(displayName, style: const TextStyle(color: Color(0xFFFFD700))),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            // Image shown ONLY in player detail dialog, with fallback and license credit (Section 11 & 12)
            Center(
              child: Container(
                width: 84,
                height: 84,
                decoration: BoxDecoration(
                  color: const Color(0xFF062E0F),
                  border: Border.all(color: const Color(0xFFFFD700)),
                ),
                child: (photoUrl != null && photoUrl.isNotEmpty)
                    ? Image.network(
                        photoUrl,
                        fit: BoxFit.cover,
                        errorBuilder: (_, _, _) => const Icon(Icons.person, size: 48, color: Colors.white54),
                      )
                    : const Icon(Icons.person, size: 48, color: Colors.white54),
              ),
            ),
            const SizedBox(height: 10),
            Text('Posição: ${_formatPosition(player['position'])}  |  Força: ${player['strength']}'),
            Text('Comportamento: ${player['behaviorDisplay']}'),
            Text('Craque (*): ${player['isStar'] == true ? "Sim (*)" : "Não"}'),
            Text('Gols na Temporada: ${player['seasonGoals']}  |  Carreira: ${player['careerGoals']}'),
            Text('Contrato restante: ${player['contractRoundsRemaining']} rodadas'),
            const Divider(),
            Text('Licença da Imagem: $photoLicense ($photoCredit)', style: const TextStyle(fontSize: 10, color: Colors.white54)),
          ],
        ),
        actions: [
          TextButton(onPressed: () => Navigator.pop(ctx), child: const Text('FECHAR')),
        ],
      ),
    );
  }

  /// Tab 1: Jogos ao Vivo, Placar Simultâneo, Intervalo (45'), Pausa Global e Substituições (5/3 e 6/4)
  Widget _buildLiveMatchesAndPauseScreen() {
    final matches = _snapshot?['matches'] as List<dynamic>? ?? [];
    final roundState = _snapshot?['roundState']?.toString() ?? 'PREPARING';
    final isPausedOrInterval = roundState == 'GLOBAL_PAUSED' ||
        roundState == 'HALFTIME_PAUSED' ||
        roundState == 'EXTRA_TIME_BREAK' ||
        roundState == 'EXTRA_TIME_HALFTIME';

    // Find my club's match if any
    Map<String, dynamic>? myMatch;
    for (final m in matches) {
      if (m['homeClubId']?.toString() == _myClubId || m['awayClubId']?.toString() == _myClubId) {
        myMatch = m as Map<String, dynamic>;
        break;
      }
    }

    return SingleChildScrollView(
      padding: const EdgeInsets.all(12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Wrap(
            spacing: 12,
            runSpacing: 8,
            children: [
              Text(
                'PLACARES SIMULTÂNEOS DA RODADA — ESTADO: $roundState',
                style: const TextStyle(color: Color(0xFFFFD700), fontWeight: FontWeight.bold, fontSize: 14),
              ),
              if (roundState == 'RUNNING_FIRST_HALF' ||
                  roundState == 'RUNNING_SECOND_HALF' ||
                  roundState == 'RUNNING_EXTRA_TIME')
                OutlinedButton.icon(
                  onPressed: () async {
                    final snap = await widget.apiClient.manualTick(_worldId!, ticks: 5);
                    setState(() => _snapshot = snap);
                  },
                  icon: const Icon(Icons.fast_forward, size: 15),
                  label: const Text('+5 MINUTOS (DEV)', style: TextStyle(fontSize: 11)),
                ),
            ],
          ),
          const SizedBox(height: 8),
          // Interactive In-Match Substitution & Tactical Editor when in Pause / Halftime
          if (isPausedOrInterval && myMatch != null) _buildInMatchSubstitutionPanel(myMatch),
          const SizedBox(height: 8),
          // Classic Simultaneous Scoreboard Table
          ...matches.map((m) {
            final isMyMatch = m['homeClubId']?.toString() == _myClubId || m['awayClubId']?.toString() == _myClubId;
            final events = m['events'] as List<dynamic>? ?? [];
            final penH = m['homePenaltyGoals'];
            final penA = m['awayPenaltyGoals'];
            final penText = (penH != null && penA != null) ? ' (Pên: $penH x $penA)' : '';

            return Container(
              margin: const EdgeInsets.only(bottom: 8),
              padding: const EdgeInsets.all(10),
              decoration: BoxDecoration(
                color: isMyMatch ? const Color(0xFF174F26) : const Color(0xFF0E3B19),
                border: Border.all(
                  color: isMyMatch ? const Color(0xFFFFD700) : Colors.white24,
                  width: isMyMatch ? 2 : 1,
                ),
              ),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Row(
                    children: [
                      Container(
                        padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 2),
                        color: Colors.black45,
                        child: Text(
                          "${m['minute']}'",
                          style: const TextStyle(color: Color(0xFFFFD700), fontWeight: FontWeight.bold, fontSize: 12),
                        ),
                      ),
                      const SizedBox(width: 10),
                      Expanded(
                        child: Text(
                          '${m['homeClubName']} (${m['homeFormation']})',
                          textAlign: TextAlign.right,
                          style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 13),
                        ),
                      ),
                      Container(
                        margin: const EdgeInsets.symmetric(horizontal: 12),
                        padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
                        color: Colors.black87,
                        child: Text(
                          '${m['homeGoals']} x ${m['awayGoals']}$penText',
                          style: const TextStyle(color: Color(0xFFFFD700), fontWeight: FontWeight.bold, fontSize: 15),
                        ),
                      ),
                      Expanded(
                        child: Text(
                          '${m['awayClubName']} (${m['awayFormation']})',
                          style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 13),
                        ),
                      ),
                      if (MediaQuery.sizeOf(context).width > 700) Text(
                        'Subs: ${m['homeSubCount']}/${m['homeMaxSubs']} - ${m['awaySubCount']}/${m['awayMaxSubs']} | Paradas: ${m['homePauseWindowsUsed']}/${m['homeMaxPauseWindows']} - ${m['awayPauseWindowsUsed']}/${m['awayMaxPauseWindows']}',
                        style: const TextStyle(fontSize: 10, color: Colors.white70),
                      ),
                    ],
                  ),
                  if (events.isNotEmpty) ...[
                    const SizedBox(height: 6),
                    Wrap(
                      spacing: 10,
                      runSpacing: 4,
                      children: events.reversed.take(5).map((e) {
                        return Text(
                          '• ${e['description']}',
                          style: const TextStyle(fontSize: 11, color: Colors.amberAccent),
                        );
                      }).toList(),
                    ),
                  ],
                ],
              ),
            );
          }),
        ],
      ),
    );
  }

  Widget _buildInMatchSubstitutionPanel(Map<String, dynamic> myMatch) {
    final isHome = myMatch['homeClubId']?.toString() == _myClubId;
    final starters = (isHome ? myMatch['homeStarters'] : myMatch['awayStarters']) as List<dynamic>? ?? [];
    final bench = (isHome ? myMatch['homeBench'] : myMatch['awayBench']) as List<dynamic>? ?? [];
    final currentForm = (isHome ? myMatch['homeFormation'] : myMatch['awayFormation'])?.toString() ?? '4-4-2';
    final usedSubs = isHome ? myMatch['homeSubCount'] : myMatch['awaySubCount'];
    final maxSubs = isHome ? myMatch['homeMaxSubs'] : myMatch['awayMaxSubs'];
    final usedWins = isHome ? myMatch['homePauseWindowsUsed'] : myMatch['awayPauseWindowsUsed'];
    final maxWins = isHome ? myMatch['homeMaxPauseWindows'] : myMatch['awayMaxPauseWindows'];

    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: const Color(0xFF1A3A20),
        border: Border.all(color: const Color(0xFFFFD700), width: 2),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            'PAINEL DE SUBSTITUIÇÕES E TÁTICA EM JOGO — SUBS: $usedSubs/$maxSubs | PARADAS: $usedWins/$maxWins',
            style: const TextStyle(color: Color(0xFFFFD700), fontWeight: FontWeight.bold, fontSize: 13),
          ),
          const SizedBox(height: 8),
          Wrap(
            spacing: 12,
            runSpacing: 8,
            crossAxisAlignment: WrapCrossAlignment.center,
            children: [
              DropdownButton<String>(
                value: availableFormations.contains(_inMatchFormation ?? currentForm)
                    ? (_inMatchFormation ?? currentForm)
                    : '4-4-2',
                items: availableFormations
                    .map((f) => DropdownMenuItem(value: f, child: Text('Formação: $f')))
                    .toList(),
                onChanged: (v) => setState(() => _inMatchFormation = v),
              ),
              DropdownButton<String>(
                hint: const Text('Sai (Em campo)'),
                value: _subPlayerOutId,
                items: starters
                    .where((p) => p['isSentOff'] != true)
                    .map((p) => DropdownMenuItem(
                          value: p['playerId'].toString(),
                          child: Text('Sai: ${p['displayName']} (F:${p['strength']})'),
                        ))
                    .toList(),
                onChanged: (v) => setState(() => _subPlayerOutId = v),
              ),
              DropdownButton<String>(
                hint: const Text('Entra (Banco)'),
                value: _subPlayerInId,
                items: bench
                    .map((p) => DropdownMenuItem(
                          value: p['playerId'].toString(),
                          child: Text('Entra: ${p['displayName']} (F:${p['strength']})'),
                        ))
                    .toList(),
                onChanged: (v) => setState(() => _subPlayerInId = v),
              ),
              ElevatedButton.icon(
                onPressed: () async {
                  final subs = <Map<String, String>>[];
                  if (_subPlayerOutId != null && _subPlayerInId != null) {
                    subs.add({'playerOutId': _subPlayerOutId!, 'playerInId': _subPlayerInId!});
                  }
                  try {
                    final snap = await widget.apiClient.submitInMatchAdjustments(
                      worldId: _worldId!,
                      coachId: _coachId!,
                      matchId: myMatch['matchId'].toString(),
                      newFormation: _inMatchFormation,
                      substitutions: subs,
                    );
                    setState(() {
                      _snapshot = _preferLatestSnapshot(snap);
                      _subPlayerOutId = null;
                      _subPlayerInId = null;
                      _statusBanner = 'Ajustes aplicados com sucesso para o próximo minuto!';
                    });
                  } catch (e) {
                    setState(() => _statusBanner = 'Erro ao aplicar ajuste: $e');
                  }
                },
                icon: const Icon(Icons.swap_calls, size: 15),
                label: const Text('APLICAR AJUSTE NA JANELA'),
              ),
            ],
          ),
        ],
      ),
    );
  }

  /// Tab 2: Divisões (1ª, 2ª, 3ª, 4ª), Taça (Preliminares Distritais + 64 clubes) e Artilharia
  Widget _buildDivisionsAndCupScreen() {
    return SingleChildScrollView(
      padding: const EdgeInsets.all(12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Wrap(
            spacing: 8,
            children: [
              for (final tier in [
                ('PrimeiraDivisao', '1ª DIVISÃO'),
                ('SegundaDivisao', '2ª DIVISÃO'),
                ('TerceiraDivisao', '3ª DIVISÃO'),
                ('QuartaDivisao', '4ª DIVISÃO'),
              ])
                ChoiceChip(
                  label: Text(tier.$2),
                  selected: _selectedDivisionTier == tier.$1,
                  onSelected: (_) async {
                    setState(() => _selectedDivisionTier = tier.$1);
                    final s = await widget.apiClient.getStandings(_worldId!, tier.$1);
                    setState(() => _standings = s);
                  },
                ),
            ],
          ),
          const SizedBox(height: 10),
          // Standings Table
          SingleChildScrollView(
            scrollDirection: Axis.horizontal,
            child: DataTable(
              headingRowColor: WidgetStateProperty.all(const Color(0xFF062E0F)),
              dataRowMinHeight: 28,
              dataRowMaxHeight: 34,
              columns: const [
                DataColumn(label: Text('#')),
                DataColumn(label: Text('CLUBE')),
                DataColumn(label: Text('TÉCNICO')),
                DataColumn(label: Text('PTS')),
                DataColumn(label: Text('J')),
                DataColumn(label: Text('V')),
                DataColumn(label: Text('E')),
                DataColumn(label: Text('D')),
                DataColumn(label: Text('GP')),
                DataColumn(label: Text('GC')),
                DataColumn(label: Text('SG')),
                DataColumn(label: Text('ZONA')),
              ],
              rows: _standings.map((r) {
                return DataRow(
                  cells: [
                    DataCell(Text('${r['position']}')),
                    DataCell(Text('${r['clubName']}', style: const TextStyle(fontWeight: FontWeight.bold))),
                    DataCell(Text('${r['coachName']}', style: const TextStyle(fontSize: 11))),
                    DataCell(Text('${r['points']}', style: const TextStyle(color: Color(0xFFFFD700), fontWeight: FontWeight.bold))),
                    DataCell(Text('${r['played']}')),
                    DataCell(Text('${r['wins']}')),
                    DataCell(Text('${r['draws']}')),
                    DataCell(Text('${r['losses']}')),
                    DataCell(Text('${r['goalsFor']}')),
                    DataCell(Text('${r['goalsAgainst']}')),
                    DataCell(Text('${r['goalDifference']}')),
                    DataCell(Text('${r['zoneStatus']}', style: const TextStyle(fontSize: 11))),
                  ],
                );
              }).toList(),
            ),
          ),
          const SizedBox(height: 20),
          const Text(
            'TAÇA NACIONAL (PRELIMINARES DISTRITAIS 96 -> 32 + MATA-MATA DE 64 CLUBES)',
            style: TextStyle(color: Color(0xFFFFD700), fontWeight: FontWeight.bold, fontSize: 14),
          ),
          const SizedBox(height: 6),
          ..._cupFixtures.take(24).map((f) {
            return Padding(
              padding: const EdgeInsets.symmetric(vertical: 2),
              child: Text(
                '[${f['stage']}] ${f['homeClubName']} (${f['homeDivision']}) ${f['homeGoals']} x ${f['awayGoals']} ${f['awayClubName']} (${f['awayDivision']}) ${f['winnerClubName'] != null ? "-> Vencedor: ${f['winnerClubName']}" : ""}',
                style: const TextStyle(fontSize: 11),
              ),
            );
          }),
          const SizedBox(height: 20),
          const Text(
            'ARTILHARIA DA TEMPORADA',
            style: TextStyle(color: Color(0xFFFFD700), fontWeight: FontWeight.bold, fontSize: 14),
          ),
          const SizedBox(height: 6),
          ..._topScorers.take(15).map((s) {
            return Text(
              '${s['rank']}º ${s['displayName']} (${s['clubName']}) — ${s['seasonGoals'] + s['cupGoals']} gols (Liga: ${s['seasonGoals']}, Taça: ${s['cupGoals']})',
              style: const TextStyle(fontSize: 12),
            );
          }),
        ],
      ),
    );
  }

  /// Tab 3: Mercado Bilateral, Lista de Transferências, Propostas e Leilões
  Widget _buildMarketAndAuctionsScreen() {
    final listings = _marketData?['listings'] as List<dynamic>? ?? [];
    final auctions = _marketData?['auctions'] as List<dynamic>? ?? [];
    final offers = (_marketData?['offers'] as List<dynamic>? ?? [])
        .where((o) => o['buyerClubId'] == _myClubId || o['sellerClubId'] == _myClubId).toList();

    return SingleChildScrollView(
      padding: const EdgeInsets.all(12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
            'LISTA DE TRANSFERÊNCIAS (COMPRA DIRETA OU PROPOSTA BILATERAL)',
            style: TextStyle(color: Color(0xFFFFD700), fontWeight: FontWeight.bold, fontSize: 14),
          ),
          const SizedBox(height: 8),
          SingleChildScrollView(
            scrollDirection: Axis.horizontal,
            child: DataTable(
              headingRowColor: WidgetStateProperty.all(const Color(0xFF062E0F)),
              dataRowMinHeight: 30,
              dataRowMaxHeight: 36,
              columns: const [
                DataColumn(label: Text('POS')),
                DataColumn(label: Text('JOGADOR (*)')),
                DataColumn(label: Text('FORÇA')),
                DataColumn(label: Text('COMPORTAMENTO')),
                DataColumn(label: Text('CLUBE VENDEDOR')),
                DataColumn(label: Text('PREÇO PEDIDO')),
                DataColumn(label: Text('VALOR JUSTO')),
                DataColumn(label: Text('AÇÕES')),
              ],
              rows: listings.map((item) {
                final pid = item['playerId'].toString();
                final ask = (item['askingPrice'] as num).toDouble();
                return DataRow(
                  cells: [
                    DataCell(Text(_formatPosition(item['position']))),
                    DataCell(Text('${item['displayName']}', style: const TextStyle(fontWeight: FontWeight.bold))),
                    DataCell(Text('${item['strength']}')),
                    DataCell(Text('${item['behaviorDisplay']}')),
                    DataCell(Text('${item['sellerClubName']}')),
                    DataCell(Text('R\$ ${ask.toStringAsFixed(0)}', style: const TextStyle(color: Color(0xFFFFD700)))),
                    DataCell(Text('R\$ ${(item['fairValue'] as num).toStringAsFixed(0)}')),
                    DataCell(
                      Row(
                        children: [
                          ElevatedButton(
                            onPressed: () async {
                              try {
                                await widget.apiClient.buyListedPlayer(_worldId!, _coachId!, pid);
                                await _refreshClubAndSecondaryData();
                                setState(() => _statusBanner = 'Jogador contratado com sucesso!');
                              } catch (e) {
                                setState(() => _statusBanner = '$e');
                              }
                            },
                            child: const Text('COMPRAR', style: TextStyle(fontSize: 10)),
                          ),
                          const SizedBox(width: 6),
                          OutlinedButton(
                            onPressed: () async {
                              try {
                                await widget.apiClient.makeTransferOffer(_worldId!, _coachId!, pid, ask * 0.90);
                                await _refreshClubAndSecondaryData();
                                setState(() => _statusBanner = 'Proposta de 90% enviada ao clube vendedor!');
                              } catch (e) {
                                setState(() => _statusBanner = '$e');
                              }
                            },
                            child: const Text('PROPOR 90%', style: TextStyle(fontSize: 10)),
                          ),
                        ],
                      ),
                    ),
                  ],
                );
              }).toList(),
            ),
          ),
          const SizedBox(height: 20),
          const Text(
            'LEILÕES ATIVOS (ATLETAS EM FIM DE CONTRATO / LIVRES)',
            style: TextStyle(color: Color(0xFFFFD700), fontWeight: FontWeight.bold, fontSize: 14),
          ),
          const SizedBox(height: 8),
          ...auctions.map((a) {
            final aid = a['auctionId'].toString();
            final cur = (a['currentBid'] as num).toDouble();
            final minBid = (a['minimumBid'] as num).toDouble();
            final nextBid = cur == 0 ? minBid : cur + 15000;
            final closed = a['isClosed'] == true;
            return Container(
              margin: const EdgeInsets.only(bottom: 6),
              padding: const EdgeInsets.all(8),
              color: const Color(0xFF0E3B19),
              child: Row(
                children: [
                  Expanded(
                    child: Text(
                      '${a['displayName']} (${_formatPosition(a['position'])}, F:${a['strength']}) • Lance Mín: R\$ ${minBid.toStringAsFixed(0)} • Atual: R\$ ${cur.toStringAsFixed(0)} (${a['highestBidderClubName'] ?? "Sem lances"})',
                      style: const TextStyle(fontSize: 12),
                    ),
                  ),
                  if (!closed)
                    ElevatedButton(
                      onPressed: () async {
                        try {
                          await widget.apiClient.placeAuctionBid(_worldId!, aid, _coachId!, nextBid);
                          await _refreshClubAndSecondaryData();
                          setState(() => _statusBanner = 'Lance de R\$ ${nextBid.toStringAsFixed(0)} registrado!');
                        } catch (e) {
                          setState(() => _statusBanner = '$e');
                        }
                      },
                      child: Text('DAR LANCE R\$ ${nextBid.toStringAsFixed(0)}'),
                    )
                  else
                    const Text('ENCERRADO', style: TextStyle(color: Colors.grey)),
                ],
              ),
            );
          }),
          const SizedBox(height: 20),
          const Text(
            'PROPOSTAS E CONTRAPROPOSTAS BILATERAIS',
            style: TextStyle(color: Color(0xFFFFD700), fontWeight: FontWeight.bold, fontSize: 14),
          ),
          const SizedBox(height: 8),
          ...offers.map((o) {
            final oid = o['offerId'].toString();
            final status = o['status'].toString();
            return Container(
              margin: const EdgeInsets.only(bottom: 6),
              padding: const EdgeInsets.all(8),
              color: const Color(0xFF0E3B19),
              child: Row(
                children: [
                  Expanded(
                    child: Text(
                      '${o['playerDisplayName']} (F:${o['strength']}): ${o['buyerClubName']} -> ${o['sellerClubName']} | Oferta: R\$ ${(o['offeredAmount'] as num).toStringAsFixed(0)} ${o['counterAmount'] != null ? "| Contraproposta: R\$ ${(o['counterAmount'] as num).toStringAsFixed(0)}" : ""} | Status: $status',
                      style: const TextStyle(fontSize: 11),
                    ),
                  ),
                  if (status == 'Pending' || status == 'Countered' || status == '0' || status == '3') ...[
                    ElevatedButton(
                      onPressed: () async {
                        try {
                          await widget.apiClient.respondTransferOffer(_worldId!, oid, _coachId!, true);
                          await _refreshClubAndSecondaryData();
                        } catch (e) {
                          setState(() => _statusBanner = '$e');
                        }
                      },
                      child: const Text('ACEITAR', style: TextStyle(fontSize: 10)),
                    ),
                    const SizedBox(width: 6),
                    OutlinedButton(
                      onPressed: () async {
                        try {
                          await widget.apiClient.respondTransferOffer(_worldId!, oid, _coachId!, false);
                          await _refreshClubAndSecondaryData();
                        } catch (e) {
                          setState(() => _statusBanner = '$e');
                        }
                      },
                      child: const Text('RECUSAR', style: TextStyle(fontSize: 10)),
                    ),
                  ],
                ],
              ),
            );
          }),
        ],
      ),
    );
  }

  /// Tab 4: Finanças (Livro-Caixa, Empréstimos) e Estádio (Capacidade Construída vs Utilizável, Desgaste, Reforma e Ampliação)
  Widget _buildFinancesAndStadiumScreen() {
    if (_myClubDetail == null) {
      return const Center(child: Text('Você não controla um clube no momento.'));
    }

    final stadium = _myClubDetail!['stadium'] as Map<String, dynamic>;
    final ledger = _financesData?['ledger'] as List<dynamic>? ?? [];
    final renoCost = (_financesData?['renovationCostEstimate'] as num?)?.toDouble() ?? 50000;
    final expCost = (_financesData?['expansion5kCostEstimate'] as num?)?.toDouble() ?? 425000;

    return SingleChildScrollView(
      padding: const EdgeInsets.all(12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Container(
            padding: const EdgeInsets.all(12),
            decoration: BoxDecoration(
              color: const Color(0xFF0E3B19),
              border: Border.all(color: const Color(0xFFFFD700)),
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  'ESTÁDIO: ${stadium['stadiumName']}',
                  style: const TextStyle(color: Color(0xFFFFD700), fontWeight: FontWeight.bold, fontSize: 15),
                ),
                const SizedBox(height: 6),
                Text(
                  'Capacidade Construída: ${stadium['builtCapacity']} lugares  |  Capacidade Utilizável: ${stadium['usableCapacity']} lugares  |  Condição Estrutural: ${stadium['conditionPercent']}%',
                  style: const TextStyle(fontSize: 12, fontWeight: FontWeight.bold),
                ),
                Text(
                  'Último Ingresso Automático: R\$ ${stadium['lastCalculatedTicketPrice']}  |  Último Público: ${stadium['lastAttendance']}  |  Custo Fixo/Jogo: R\$ ${stadium['fixedCostPerMatch']}',
                  style: const TextStyle(fontSize: 12, color: Colors.white70),
                ),
                const SizedBox(height: 10),
                Wrap(
                  spacing: 10,
                  runSpacing: 8,
                  children: [
                    ElevatedButton.icon(
                      onPressed: () async {
                        try {
                          await widget.apiClient.startStadiumRenovation(_worldId!, _myClubId!, _coachId!);
                          await _refreshClubAndSecondaryData();
                          setState(() => _statusBanner = 'Obra de reforma estrutural iniciada!');
                        } catch (e) {
                          setState(() => _statusBanner = '$e');
                        }
                      },
                      icon: const Icon(Icons.build, size: 15),
                      label: Text('REFORMAR ESTÁDIO (R\$ ${renoCost.toStringAsFixed(0)})'),
                    ),
                    ElevatedButton.icon(
                      onPressed: () async {
                        try {
                          await widget.apiClient.startStadiumExpansion(_worldId!, _myClubId!, _coachId!, 5000);
                          await _refreshClubAndSecondaryData();
                          setState(() => _statusBanner = 'Ampliação de +5.000 lugares iniciada!');
                        } catch (e) {
                          setState(() => _statusBanner = '$e');
                        }
                      },
                      icon: const Icon(Icons.add_business, size: 15),
                      label: Text('AMPLIAR +5.000 LUGARES (R\$ ${expCost.toStringAsFixed(0)})'),
                    ),
                    OutlinedButton.icon(
                      onPressed: () async {
                        try {
                          await widget.apiClient.takeBankLoan(_worldId!, _myClubId!, _coachId!, 300000, 10);
                          await _refreshClubAndSecondaryData();
                          setState(() => _statusBanner = 'Empréstimo bancário de R\$ 300.000 creditado em caixa.');
                        } catch (e) {
                          setState(() => _statusBanner = '$e');
                        }
                      },
                      icon: const Icon(Icons.account_balance_wallet, size: 15),
                      label: const Text('EMPRÉSTIMO BANCÁRIO (+R\$ 300.000)'),
                    ),
                  ],
                ),
              ],
            ),
          ),
          const SizedBox(height: 16),
          const Text(
            'LIVRO-CAIXA AUDITÁVEL (TRANSAÇÕES IDEMPOTENTES POR RODADA)',
            style: TextStyle(color: Color(0xFFFFD700), fontWeight: FontWeight.bold, fontSize: 14),
          ),
          const SizedBox(height: 8),
          ...ledger.map((t) {
            final amt = (t['amount'] as num).toDouble();
            return Container(
              padding: const EdgeInsets.symmetric(vertical: 4, horizontal: 8),
              decoration: const BoxDecoration(border: Border(bottom: BorderSide(color: Colors.white12))),
              child: Row(
                children: [
                  Text('[T${t['seasonNumber']} R${t['roundNumber']}] ', style: const TextStyle(fontSize: 11, color: Colors.white54)),
                  Expanded(child: Text('${t['description']}', style: const TextStyle(fontSize: 12))),
                  Text(
                    '${amt >= 0 ? "+" : ""}R\$ ${amt.toStringAsFixed(0)}',
                    style: TextStyle(
                      fontSize: 12,
                      fontWeight: FontWeight.bold,
                      color: amt >= 0 ? Colors.lightGreenAccent : Colors.redAccent,
                    ),
                  ),
                ],
              ),
            );
          }),
        ],
      ),
    );
  }

  /// Tab 5: Ranking Clássico de Treinadores, Carreira, Histórico de Temporadas e Convites
  Widget _buildCoachMorale() {
    final people = _snapshot?['readyCounter']?['participants'] as List<dynamic>? ?? [];
    final me = people.where((p) => p['coachId'] == _coachId).firstOrNull;
    final value = me?['morale'] ?? _invitationsData?['morale'] ?? 70;
    final band = me?['moraleBand'] ?? _invitationsData?['moraleBand'] ?? 'VERDE';
    final color = band == 'VERMELHA' ? const Color(0xFFEF5350)
        : band == 'AMARELA' ? const Color(0xFFFFD54F) : const Color(0xFF81C784);
    return Semantics(
      label: 'Moral do treinador: $value de 100, $band',
      child: Container(
        key: const ValueKey('coach-morale'),
        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 5),
        decoration: BoxDecoration(color: const Color(0xFF062E0F), border: Border.all(color: color)),
        child: Text('MORAL: $value/100 — $band', style: TextStyle(color: color, fontWeight: FontWeight.bold, fontSize: 12)),
      ),
    );
  }

  Future<void> _respondCareerInvitation(Map<dynamic, dynamic> invitation, bool accept) async {
    if (_worldId == null || _coachId == null) return;
    await _runUiCommand(() async {
      if (accept) {
        await widget.apiClient.acceptInvitation(_worldId!, invitation['invitationId'].toString(), _coachId!);
      } else {
        await widget.apiClient.rejectInvitation(_worldId!, invitation['invitationId'].toString(), _coachId!);
      }
      final snap = await widget.apiClient.getSnapshot(_worldId!);
      final people = snap['readyCounter']?['participants'] as List<dynamic>? ?? [];
      final me = people.where((p) => p['coachId'] == _coachId).firstOrNull;
      if (!mounted) return;
      setState(() {
        _snapshot = _preferLatestSnapshot(snap);
        if (me != null) { _myClubId = me['clubId']?.toString(); _isUnemployed = me['isUnemployed'] == true; }
        _myClubDetail = null; _selectedStarterIds.clear(); _selectedBenchIds.clear();
        _statusBanner = accept ? 'Você assumiu o comando do ${invitation['clubName']}!' : 'Convite recusado. O clube seguirá sua busca.';
      });
      await _saveSession();
      await _refreshClubAndSecondaryData();
    });
  }

  Widget _buildCareerRankingAndInvitationsScreen() {
    final ranking = _rankingData?['ranking'] as List<dynamic>? ?? [];
    final seasonHist = _rankingData?['seasonHistory'] as List<dynamic>? ?? [];
    final invitations = _invitationsData?['invitations'] as List<dynamic>? ?? [];

    return SingleChildScrollView(
      padding: const EdgeInsets.all(12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          _buildCoachMorale(),
          const SizedBox(height: 8),
          const Text('Moral vermelha antes de uma derrota provoca demissão. Campeonatos > Taças > pontos de carreira.'),
          const SizedBox(height: 8),
          ...((_invitationsData?['moraleHistory'] as List<dynamic>? ?? []).reversed.take(5)).map((h) => Text(
            'T${h['season']} R${h['round']}: ${h['before']} → ${h['after']} (${h['matchId'] == null ? 'recuperação sem clube' : 'após partida'})',
            style: const TextStyle(fontSize: 12),
          )),
          const SizedBox(height: 12),
          if (invitations.isNotEmpty) ...[
            const Text(
              'CONVITES DE EMPREGO DISPONÍVEIS PARA VOCÊ',
              style: TextStyle(color: Color(0xFFFFD700), fontWeight: FontWeight.bold, fontSize: 14),
            ),
            const SizedBox(height: 6),
            ...invitations.map((inv) {
              return Container(
                margin: const EdgeInsets.only(bottom: 6),
                padding: const EdgeInsets.all(8),
                color: const Color(0xFF136F2D),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                        '${inv['clubName']} (${inv['divisionDisplay']}) — Caixa: R\$ ${(inv['clubBudget'] as num).toStringAsFixed(0)} | Elenco: ${inv['squadSize']} atletas',
                        style: const TextStyle(fontWeight: FontWeight.bold, fontSize: 12),
                    ),
                    Text('Posição: ${inv['tablePosition'] ?? '-'} • Força: ${inv['squadStrength'] ?? '-'} • Estádio: ${inv['stadiumCapacity'] ?? '-'} lugares', style: const TextStyle(fontSize: 12)),
                    Text('Objetivo: ${inv['objective'] ?? 'Desenvolver o clube'}', style: const TextStyle(fontSize: 12)),
                    Text('Projeto atual: ${(_invitationsData?['currentProjectScore'] as num? ?? 0).toStringAsFixed(1)} • Melhoria proposta: ${(inv['projectImprovement'] as num? ?? 0).toStringAsFixed(1)}', style: const TextStyle(fontSize: 12)),
                    if (inv['isAccepted'] != true)
                      Wrap(spacing: 8, children: [
                        ElevatedButton(onPressed: _commandBusy ? null : () => _respondCareerInvitation(inv as Map, true), child: const Text('ACEITAR')),
                        OutlinedButton(onPressed: _commandBusy ? null : () => _respondCareerInvitation(inv as Map, false), child: const Text('RECUSAR')),
                      ]),
                  ],
                ),
              );
            }),
            const SizedBox(height: 16),
          ],
          const Text(
            'RANKING CLÁSSICO DE TREINADORES (1º CAMPEONATOS, 2º TAÇAS, 3º PONTOS DO TREINADOR)',
            style: TextStyle(color: Color(0xFFFFD700), fontWeight: FontWeight.bold, fontSize: 14),
          ),
          const SizedBox(height: 8),
          ...(_invitationsData?['employmentHistory'] as List<dynamic>? ?? []).reversed.take(8).map((h) => Text(
            '${h['clubName']} — T${h['startSeason']} R${h['startRound']} até ${h['endSeason'] == null ? 'atual' : 'T${h['endSeason']} R${h['endRound']}'}',
            style: const TextStyle(fontSize: 12),
          )),
          const SizedBox(height: 8),
          SingleChildScrollView(
            scrollDirection: Axis.horizontal,
            child: DataTable(
              headingRowColor: WidgetStateProperty.all(const Color(0xFF062E0F)),
              dataRowMinHeight: 28,
              dataRowMaxHeight: 34,
              columns: const [
                DataColumn(label: Text('#')),
                DataColumn(label: Text('TREINADOR')),
                DataColumn(label: Text('TIPO')),
                DataColumn(label: Text('CLUBE ATUAL')),
                DataColumn(label: Text('CAMPEONATOS')),
                DataColumn(label: Text('TAÇAS')),
                DataColumn(label: Text('PTS TREINADOR')),
                DataColumn(label: Text('V / E / D')),
              ],
              rows: ranking.map((r) {
                return DataRow(
                  cells: [
                    DataCell(Text('${r['rank']}')),
                    DataCell(Text('${r['name']}', style: const TextStyle(fontWeight: FontWeight.bold))),
                    DataCell(Text(r['isHuman'] == true ? 'HUMANO' : 'IA')),
                    DataCell(Text('${r['currentClubName']}')),
                    DataCell(Text('${r['leagueTitles']}', style: const TextStyle(color: Color(0xFFFFD700), fontWeight: FontWeight.bold))),
                    DataCell(Text('${r['cupTitles']}', style: const TextStyle(color: Colors.cyanAccent, fontWeight: FontWeight.bold))),
                    DataCell(Text('${r['coachPoints']}', style: const TextStyle(fontWeight: FontWeight.bold))),
                    DataCell(Text('${r['totalWins']}V ${r['totalDraws']}E ${r['totalLosses']}D')),
                  ],
                );
              }).toList(),
            ),
          ),
          if (seasonHist.isNotEmpty) ...[
            const SizedBox(height: 20),
            const Text(
              'GALERIA DE CAMPEÕES POR TEMPORADA',
              style: TextStyle(color: Color(0xFFFFD700), fontWeight: FontWeight.bold, fontSize: 14),
            ),
            const SizedBox(height: 6),
            ...seasonHist.map((h) {
              return Text(
                'Temporada ${h['seasonNumber']}: Campeão 1ª Div: ${h['firstDivisionChampionName']} | Taça: ${h['cupChampionName']} | Artilheiro: ${h['topScorerPlayerName']} (${h['topScorerGoals']} gols)',
                style: const TextStyle(fontSize: 12),
              );
            }),
          ],
        ],
      ),
    );
  }

  /// Tab 6: Editor Integral (Clubes, Jogadores, Técnicos IA e Importação/Exportação JSON/CSV)
  Widget _buildEditorAndPackagesScreen() {
    return SingleChildScrollView(
      padding: const EdgeInsets.all(12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Wrap(spacing: 8, children: [
            ElevatedButton(onPressed: _loadEditor, child: const Text('CARREGAR EDITOR DE DADOS')),
            OutlinedButton(onPressed: () => _editEntity('create-club', {
              'id': '00000000-0000-0000-0000-000000000000', 'name': 'Novo Clube', 'shortName': 'NOV',
              'country': 'Brasil', 'primaryColorHex': '#FFFFFF', 'secondaryColorHex': '#000000',
              'divisionTier': 'Distrital', 'cashBalance': 100000, 'reputation': 20,
              'stadiumName': 'Estádio Novo', 'builtCapacity': 5000, 'usableCapacity': 5000,
              'conditionPercent': 100, 'maintenanceReserve': 0,
            }), child: const Text('CRIAR CLUBE DISTRITAL')),
          ]),
          if (_editorClubs.isNotEmpty) ...[
            _editorPicker('Editar clube', _editorClubs, 'update-club'),
            _editorPicker('Editar jogador: força, comportamento e *', _editorPlayers, 'update-player'),
            _editorPicker('Editar treinador e preferências', _editorManagers, 'update-manager'),
          ],
          const Text(
            'EDITOR INTEGRAL DE MUNDO E PACOTES DE DADOS (JSON / CSV)',
            style: TextStyle(color: Color(0xFFFFD700), fontWeight: FontWeight.bold, fontSize: 14),
          ),
          const SizedBox(height: 8),
          Wrap(
            spacing: 10,
            runSpacing: 8,
            children: [
              ElevatedButton.icon(
                onPressed: () async {
                  final jsonStr = await widget.apiClient.exportWorldJson(_worldId!);
                  setState(() {
                    _editorJsonText = jsonStr;
                    _packageController.text = jsonStr;
                    _statusBanner = 'Pacote JSON exportado do mundo atual (${jsonStr.length} bytes).';
                  });
                },
                icon: const Icon(Icons.download, size: 15),
                label: const Text('EXPORTAR MUNDO PARA JSON'),
              ),
              ElevatedButton.icon(
                onPressed: () async {
                  final csvStr = await widget.apiClient.exportPlayersCsv(_worldId!);
                  setState(() {
                    _editorJsonText = csvStr;
                    _packageController.text = csvStr;
                    _statusBanner = 'Lista de jogadores exportada em formato CSV.';
                  });
                },
                icon: const Icon(Icons.table_chart, size: 15),
                label: const Text('EXPORTAR JOGADORES PARA CSV'),
              ),
              OutlinedButton.icon(
                onPressed: _editorJsonText.isEmpty
                    ? null
                    : () async {
                        try {
                          final val = await widget.apiClient.validatePackageJson(_worldId!, _editorJsonText);
                          setState(() => _packageValidationResult = val);
                        } catch (e) {
                          setState(() => _statusBanner = 'Erro ao validar JSON: $e');
                        }
                      },
                icon: const Icon(Icons.verified, size: 15),
                label: const Text('VALIDAR E PREVIEW DE DIFERENÇAS'),
              ),
              ElevatedButton.icon(
                onPressed: _editorJsonText.isEmpty
                    ? null
                    : () async {
                        try {
                          final res = await widget.apiClient.importPackageJson(
                            _editorJsonText,
                            'Mundo Importado Editado',
                          );
                          setState(() => _statusBanner = 'Novo mundo criado a partir do pacote! Código: ${res['inviteCode']}');
                        } catch (e) {
                          setState(() => _statusBanner = 'Erro na importação: $e');
                        }
                      },
                icon: const Icon(Icons.publish, size: 15),
                label: const Text('CRIAR NOVO MUNDO DO PACOTE'),
              ),
            ],
          ),
          if (_packageValidationResult != null) ...[
            const SizedBox(height: 10),
            Container(
              padding: const EdgeInsets.all(10),
              color: const Color(0xFF062E0F),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'Validação: ${_packageValidationResult!['isValid'] == true ? "VÁLIDO ✔" : "INVÁLIDO ❌"} '
                    '(Clubes: ${_packageValidationResult!['clubCount']}, Atletas: ${_packageValidationResult!['playerCount']}, Técnicos IA: ${_packageValidationResult!['managerCount']})',
                    style: const TextStyle(color: Color(0xFFFFD700), fontWeight: FontWeight.bold),
                  ),
                  for (final err in (_packageValidationResult!['errors'] as List<dynamic>? ?? []))
                    Text('Erro: $err', style: const TextStyle(color: Colors.redAccent, fontSize: 11)),
                  for (final diff in (_packageValidationResult!['diffPreview'] as List<dynamic>? ?? []))
                    Text('Diff: $diff', style: const TextStyle(color: Colors.cyanAccent, fontSize: 11)),
                ],
              ),
            ),
          ],
          const SizedBox(height: 10),
          TextField(
            maxLines: 14,
            style: const TextStyle(fontSize: 11, fontFamily: 'monospace'),
            decoration: const InputDecoration(
              labelText: 'Conteúdo do Pacote JSON / CSV',
              border: OutlineInputBorder(),
            ),
            controller: _packageController,
            onChanged: (v) => setState(() => _editorJsonText = v),
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
