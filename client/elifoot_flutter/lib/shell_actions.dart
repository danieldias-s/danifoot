// ignore_for_file: invalid_use_of_protected_member
part of 'main.dart';

class _ShellDropdownArrowPainter extends CustomPainter {
  final Color color;
  const _ShellDropdownArrowPainter(this.color);

  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint()
      ..color = color
      ..style = PaintingStyle.fill;
    final path = Path()
      ..moveTo(0, 0)
      ..lineTo(size.width, 0)
      ..lineTo(size.width / 2, size.height)
      ..close();
    canvas.drawPath(path, paint);
  }

  @override
  bool shouldRepaint(covariant _ShellDropdownArrowPainter oldDelegate) =>
      oldDelegate.color != color;
}

Widget _classicDropdownIcon(BuildContext context) => Padding(
  padding: EdgeInsets.only(right: 4),
  child: CustomPaint(
    size: Size(10, 6),
    painter: _ShellDropdownArrowPainter(ClassicTheme.of(context).accent),
  ),
);

extension _ShellActions on _ElifootShellScreenState {
  Future<void> _runUiCommand(Future<void> Function() action) async {
    if (_commandBusy) return;
    setState(() => _commandBusy = true);
    try {
      await action();
    } catch (e) {
      if (mounted) setState(() => _statusBanner = '$e');
    } finally {
      if (mounted) setState(() => _commandBusy = false);
    }
  }

  Map<String, dynamic>? _preferLatestSnapshot(Map<String, dynamic>? incoming) {
    if (incoming == null) return _snapshot;
    if (_worldId != null &&
        incoming['gameWorldId'] != null &&
        incoming['gameWorldId'] != _worldId) {
      return _snapshot;
    }
    if ((_snapshot?['roundVersion'] as num? ?? 0) >
        (incoming['roundVersion'] as num? ?? 0)) {
      return _snapshot;
    }
    return incoming;
  }

  Future<void> _saveSession() async {
    if (widget.useAccountLogin) {
      await _saveAccountNavigation();
      return;
    }
    final previous = _savedSessions
        .where((p) => p['deviceId'] == _deviceId)
        .firstOrNull;
    final people = _snapshot?['readyCounter']?['participants'] as List? ?? [];
    final me = people
        .whereType<Map>()
        .where((p) => p['coachId'] == _coachId)
        .firstOrNull;
    await writeSession(
      jsonEncode({
        'nickname': _nickname,
        'coachId': _coachId,
        'deviceId': _deviceId,
        'worldId': _worldId,
        'lastWorldName': _snapshot?['worldName'] ?? previous?['lastWorldName'],
        'lastClubName':
            me?['clubName'] ??
            (_isUnemployed && _worldId != null
                ? 'Sem clube'
                : previous?['lastClubName']),
        'liveSelection': _liveSelection.toJson(),
      }),
    );
    if (mounted) setState(() => _savedSessions = readSavedSessions());
  }

  String _localTrainerDetail(String deviceId, {String? coachId}) {
    final profile = _savedSessions
        .where((p) => p['deviceId'] == deviceId)
        .firstOrNull;
    final id = coachId ?? profile?['coachId'];
    if (id != null) {
      for (final world in _worlds) {
        final people = world['readyCounter']?['participants'] as List? ?? [];
        final person = people
            .whereType<Map>()
            .where((p) => p['coachId'] == id)
            .firstOrNull;
        if (person != null) {
          return '${person['clubName'] ?? 'Sem clube'} • ${world['name']}';
        }
      }
    }
    final detail = [
      profile?['lastClubName'],
      profile?['lastWorldName'],
    ].whereType<String>().where((s) => s.isNotEmpty).join(' • ');
    if (detail.isNotEmpty) return detail;
    final ids = _savedSessions.map((p) => p['deviceId'].toString()).toList()
      ..sort();
    final index = ids.indexOf(deviceId);
    return index >= 0
        ? 'Perfil ${index + 1} salvo neste navegador'
        : 'Novo acesso local';
  }

  Future<void> _selectLocalTrainer(String deviceId) async {
    final profile = _savedSessions.firstWhere((p) => p['deviceId'] == deviceId);
    await _activateLocalTrainer(Map<String, dynamic>.from(profile));
  }

  Future<void> _activateLocalTrainer(Map<String, dynamic> profile) async {
    widget.presentation?.clear();
    setState(() {
      _nickname = profile['nickname']?.toString() ?? 'Treinador';
      _deviceId = profile['deviceId'].toString();
      _coachId = profile['coachId']?.toString();
      _authenticatedDeviceId = _coachId == null ? null : _deviceId;
      _worlds = [];
      _statusBanner = null;
      _authWarning = null;
    });
    await _performLocalLoginAndLoadWorlds();
  }

  Future<void> _createLocalTrainer() async {
    var number = 1;
    while (_savedSessions.any((p) => p['nickname'] == 'Treinador $number')) {
      number++;
    }
    final result = await showDialog<Map<String, String>>(
      context: context,
      builder: (_) => TrainerAccessDialog(initialName: 'Treinador $number'),
    );
    if (!mounted || result == null) return;
    await _activateLocalTrainer({
      'nickname': result['nickname'],
      'deviceId':
          'device-${DateTime.now().microsecondsSinceEpoch}-${Random().nextInt(1 << 30)}',
    });
  }

  Future<void> _recoverLocalTrainer() async {
    final result = await showDialog<Map<String, String>>(
      context: context,
      builder: (_) =>
          TrainerAccessDialog(initialName: _nickname, recovery: true),
    );
    if (!mounted || result == null) return;
    final existing = _savedSessions
        .where((p) => p['deviceId'] == result['deviceId'])
        .firstOrNull;
    await _activateLocalTrainer({...?existing, ...result});
  }

  Future<void> _joinByCodeDialog() async {
    final code = TextEditingController();
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        backgroundColor: ClassicTheme.of(context).surface,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.zero,
          side: BorderSide(color: ClassicTheme.of(context).border, width: 1.5),
        ),
        title: Text(
          'ENTRAR POR CÓDIGO',
          style: TextStyle(
            color: ClassicTheme.of(context).accent,
            fontWeight: FontWeight.bold,
            fontSize: 14,
          ),
        ),
        content: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              if (widget.presentation != null)
                PresentationVersionSelector(
                  api: widget.apiClient,
                  controller: widget.presentation!,
                ),
              TextField(
                controller: code,
                decoration: const InputDecoration(
                  labelText: 'Código do convite',
                  border: OutlineInputBorder(borderRadius: BorderRadius.zero),
                ),
              ),
            ],
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx, false),
            child: const Text('CANCELAR'),
          ),
          ElevatedButton(
            onPressed: () => Navigator.pop(ctx, true),
            child: const Text('ENTRAR'),
          ),
        ],
      ),
    );
    if (confirmed == true && _coachId != null) {
      try {
        final result = await widget.apiClient.joinWorldByCode(
          inviteCode: code.text.trim(),
          coachId: _coachId!,
          nickname: _nickname,
          deviceId: _deviceId,
        );
        _coachId = result['coachId']?.toString() ?? _coachId;
        await _joinSelectedWorld(result['worldId']);
      } catch (e) {
        if (mounted) {
          setState(
            () => _statusBanner = e.toString().replaceFirst(
              RegExp(r'^Exception: '),
              '',
            ),
          );
        }
      }
    }
    await Future<void>.delayed(const Duration(milliseconds: 300));
    code.dispose();
  }

  Future<void> _chooseClub(String worldId) async {
    try {
      final membership = _worlds.where((w) => w['id'] == worldId).firstOrNull;
      if (membership?['reconnectCoachId'] != null) {
        _coachId = membership['reconnectCoachId'].toString();
        await _joinSelectedWorld(worldId);
        return;
      }
      final snap = await widget.apiClient.getSnapshot(worldId);
      if (snap['hasInitialLobby'] == true && snap['lobbyStarted'] != true) {
        await _joinSelectedWorld(worldId);
        return;
      }
      if ((snap['readyCounter']['participants'] as List).any(
        (p) => p['coachId'] == _coachId,
      )) {
        await _joinSelectedWorld(worldId);
        return;
      }
      final summary = _worlds.where((w) => w['id'] == worldId).firstOrNull;
      if (summary?['availableSeats'] == 0) {
        await _joinSelectedWorld(worldId);
        return;
      }
      final clubs = await widget.apiClient.getClubs(worldId);
      if (!mounted) return;
      final availableClubs = clubs
          .where(
            (c) =>
                c['divisionTier'] != 'Distrital' &&
                c['divisionTier'] != 5 &&
                c['isHumanControlled'] != true,
          )
          .toList();
      final selected = await showDialog<String>(
        context: context,
        builder: (ctx) => ClubSelectionDialog(
          api: widget.apiClient,
          presentation: widget.presentation,
          clubs: availableClubs
              .map((club) => Map<String, dynamic>.from(club as Map))
              .toList(),
        ),
      );
      if (selected != null) {
        await _joinSelectedWorld(worldId, preferredClubId: selected);
      }
    } catch (e) {
      if (mounted) setState(() => _statusBanner = '$e');
    }
  }

  Color _clubColor(dynamic palette, String field) {
    if (palette is Map &&
        (palette['colorVerified'] == true ||
            palette['isPersonalized'] == true)) {
      return rgbColor(palette[field]) ?? ClassicTheme.of(context).border;
    }
    return ClassicTheme.of(context).surfaceAlternate;
  }

  /// Section 4: Painel Principal do Treinador (Employed & Unemployed views)
  Widget _buildCoachMainPanel() {
    final snap = _snapshot;
    final roundState = snap?['roundState']?.toString() ?? 'PREPARING';
    final roundNumber = (snap?['roundNumber'] as num?)?.toInt() ?? 1;
    final seasonNumber = (snap?['seasonNumber'] as num?)?.toInt() ?? 1;
    final compLabel = snap?['competitionLabel']?.toString() ?? 'Liga';
    final people =
        snap?['readyCounter']?['participants'] as List<dynamic>? ?? [];
    final me = people.where((p) => p['coachId'] == _coachId).firstOrNull;
    final isReady = me?['isReady'] == true;
    final invitations =
        (_invitationsData?['invitations'] as List<dynamic>? ?? [])
            .where((inv) => inv['isAccepted'] != true)
            .toList();
    final ranking = _rankingData?['ranking'] as List<dynamic>? ?? [];
    final myRank = ranking
        .where(
          (r) =>
              r['coachId']?.toString() == _coachId ||
              r['name']?.toString() == _nickname,
        )
        .firstOrNull;

    if (_isUnemployed || _myClubDetail == null) {
      final empHistory =
          _invitationsData?['employmentHistory'] as List<dynamic>? ?? [];
      return Container(
        key: const ValueKey('coach-unemployed-panel'),
        margin: const EdgeInsets.all(12),
        padding: const EdgeInsets.all(14),
        decoration: BoxDecoration(
          color: ClassicTheme.of(context).surface,
          border: Border.all(
            color: ClassicTheme.of(context).selectedBackground,
            width: 1.5,
          ),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            const ClassicSectionTitle(
              title: 'TREINADOR SEM CLUBE',
              subtitle:
                  'VOCÊ ESTÁ DESEMPREGADO NO MOMENTO — Acompanhe a rodada ou aceite um convite para voltar à ativa.',
            ),
            const SizedBox(height: 10),
            _buildCoachMorale(),
            PlayerStatisticRow(
              items: [
                PlayerStatItem('TREINADOR', _nickname, highlight: true),
                const PlayerStatItem('SITUAÇÃO', 'TREINADOR SEM CLUBE'),
                PlayerStatItem(
                  'RANKING',
                  myRank != null
                      ? '${myRank['rank']}º (${myRank['coachPoints']} pts)'
                      : 'Sem posição',
                  highlight: true,
                ),
                PlayerStatItem(
                  'TÍTULOS',
                  myRank != null
                      ? '${myRank['leagueTitles']} Liga / ${myRank['cupTitles']} Taça'
                      : '0 Liga / 0 Taça',
                ),
                PlayerStatItem(
                  'RODADA ATUAL',
                  'T$seasonNumber • R$roundNumber',
                ),
                if (snap?['gameDate'] != null)
                  PlayerStatItem(
                    'DATA DO JOGO',
                    formatClassicDate(snap!['gameDate']),
                  ),
                PlayerStatItem(
                  'CONVITES',
                  '${invitations.length} disponível(is)',
                  highlight: invitations.isNotEmpty,
                ),
              ],
            ),
            const SizedBox(height: 10),
            Text(
              'Use o botão "PRONTO PARA ACOMPANHAR" no topo para avançar as rodadas com a sala, ou veja a aba "5. RANKING & CARREIRA" para aceitar convites de clubes!',
              style: TextStyle(
                fontSize: 11,
                color: ClassicTheme.of(context).accent,
              ),
            ),
            const SizedBox(height: 10),
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: [
                ElevatedButton(
                  key: const ValueKey('btn-ver-convites-desempregado'),
                  onPressed: () => setState(() {
                    _activeTabIndex = 4;
                    _showMatchView = false;
                  }),
                  child: const Text('VER CONVITES DE EMPREGO'),
                ),
                if (roundState == 'PREPARING' && !isReady)
                  ClassicActionButton(
                    buttonKey: const ValueKey('btn-avancar-sem-clube'),
                    label: 'AVANÇAR RODADA (SEM CLUBE)',
                    onPressed: () => _runUiCommand(_handleSendTacticsOrReady),
                  ),
                ClassicActionButton(
                  label: 'CLASSIFICAÇÃO',
                  onPressed: () => setState(() {
                    _activeTabIndex = 1;
                    _showMatchView = false;
                  }),
                ),
                ClassicActionButton(
                  label: 'ACOMPANHAR RODADA',
                  onPressed: () => setState(() {
                    _showMatchView = RoundViewCoordinator.isMatchStage(
                      _snapshot?['roundState']?.toString(),
                    );
                    if (!_showMatchView) _activeTabIndex = 0;
                  }),
                ),
              ],
            ),
            if (empHistory.isNotEmpty) ...[
              const SizedBox(height: 12),
              const ClassicTableHeader(
                title: 'HISTÓRICO DE CLUBES NA CARREIRA',
              ),
              const SizedBox(height: 4),
              for (final h in empHistory.reversed.take(5))
                Padding(
                  padding: const EdgeInsets.symmetric(vertical: 2),
                  child: Text(
                    '• ${h['clubName']} — T${h['startSeason']} R${h['startRound']} até ${h['endSeason'] == null ? 'atual' : 'T${h['endSeason']} R${h['endRound']}'}',
                    style: const TextStyle(fontSize: 11),
                  ),
                ),
            ],
            if (invitations.isNotEmpty) ...[
              const SizedBox(height: 12),
              const ClassicTableHeader(
                title: 'CONVITES DE CLUBES DISPONÍVEIS',
                subtitle: 'Assuma imediatamente o comando técnico de um clube:',
              ),
              const SizedBox(height: 6),
              for (final inv in invitations)
                Container(
                  margin: const EdgeInsets.only(bottom: 6),
                  padding: const EdgeInsets.all(8),
                  decoration: BoxDecoration(
                    color: ClassicTheme.of(context).buttonBackground,
                    border: Border.all(
                      color: ClassicTheme.of(context).selectedBackground,
                    ),
                  ),
                  child: Wrap(
                    spacing: 10,
                    runSpacing: 6,
                    alignment: WrapAlignment.spaceBetween,
                    crossAxisAlignment: WrapCrossAlignment.center,
                    children: [
                      Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            '${inv['clubName']} (${inv['divisionDisplay']}) — Caixa: ${formatClassicMoney(inv['clubBudget'])}',
                            style: TextStyle(
                              color: ClassicTheme.of(context).accent,
                              fontWeight: FontWeight.bold,
                              fontSize: 12,
                            ),
                          ),
                          Text(
                            'Posição: ${inv['tablePosition'] ?? '-'} • Força: ${inv['squadStrength'] ?? '-'} • Objetivo: ${inv['objective'] ?? 'Desenvolver o clube'}',
                            style: const TextStyle(fontSize: 11),
                          ),
                        ],
                      ),
                      Wrap(
                        spacing: 6,
                        children: [
                          ElevatedButton(
                            onPressed: _commandBusy
                                ? null
                                : () => _respondCareerInvitation(
                                    inv as Map,
                                    true,
                                  ),
                            child: const Text('ACEITAR CONVITE'),
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
                  ),
                ),
            ],
          ],
        ),
      );
    }

    // Employed Coach Main Panel
    final clubName = _myClubDetail!['name']?.toString() ?? 'Clube';
    final divisionDisplay =
        _myClubDetail!['divisionDisplay']?.toString() ?? _selectedDivisionTier;
    final cash = (_myClubDetail!['cashBalance'] as num?)?.toDouble() ?? 0.0;

    // Resolve league standing position for coach's club
    final myStanding = _standings
        .where(
          (s) =>
              s['clubId']?.toString() == _myClubId ||
              s['clubName']?.toString() == clubName,
        )
        .firstOrNull;
    final positionText = myStanding != null
        ? '${myStanding['position']}º (${myStanding['points']} pts)'
        : 'Consulte a Divisão';

    // Resolve next / current match and opponent
    String nextOpponentText = 'A definir';
    String nextMatchText = 'Rodada $roundNumber ($compLabel)';
    final liveMatches = snap?['matches'] as List<dynamic>? ?? [];
    final myLiveMatch = liveMatches
        .where(
          (m) =>
              m['homeClubId']?.toString() == _myClubId ||
              m['awayClubId']?.toString() == _myClubId,
        )
        .firstOrNull;
    if (myLiveMatch != null) {
      final isHome = myLiveMatch['homeClubId']?.toString() == _myClubId;
      final oppName = isHome
          ? myLiveMatch['awayClubName']
          : myLiveMatch['homeClubName'];
      nextOpponentText = '$oppName (${isHome ? "CASA" : "FORA"})';
      nextMatchText =
          '${myLiveMatch['homeClubName']} ${myLiveMatch['homeGoals']} x ${myLiveMatch['awayGoals']} ${myLiveMatch['awayClubName']}';
    } else {
      final calRound = _calendar
          .where((r) => r['roundNumber'] == roundNumber)
          .firstOrNull;
      final fixtures = calRound?['fixtures'] as List<dynamic>? ?? [];
      final myCalFix = fixtures
          .where((f) => f['home'] == clubName || f['away'] == clubName)
          .firstOrNull;
      if (myCalFix != null) {
        final isHome = myCalFix['home'] == clubName;
        final oppName = isHome ? myCalFix['away'] : myCalFix['home'];
        nextOpponentText = '$oppName (${isHome ? "CASA" : "FORA"})';
        nextMatchText =
            '${myCalFix['home']} x ${myCalFix['away']} (R$roundNumber)';
      }
    }

    final prepText = roundState == 'PREPARING'
        ? (_isClassicIdleRound
              ? 'SEM PARTIDA — PRONTO PARA ACOMPANHAR'
              : isReady
              ? 'TÁTICA ENVIADA (PRONTO)'
              : (_requiredStarterCount > 0 &&
                        _selectedStarterIds.length == _requiredStarterCount
                    ? 'ESCALADO (FALTA ENVIAR)'
                    : 'ESCALAÇÃO INCOMPLETA (${_selectedStarterIds.length}/11)'))
        : (roundState == 'RESULTS' ? 'RODADA FINALIZADA' : 'PARTIDA EM CURSO');

    return Container(
      key: const ValueKey('coach-main-panel'),
      margin: const EdgeInsets.only(bottom: 10),
      padding: const EdgeInsets.all(10),
      decoration: BoxDecoration(
        color: ClassicTheme.of(context).surface,
        border: Border.all(color: ClassicTheme.of(context).border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          ClassicTableHeader(
            title: 'PAINEL PRINCIPAL DO TREINADOR — $clubName',
            trailing: 'TEMP $seasonNumber • RODADA $roundNumber ($compLabel)',
          ),
          const SizedBox(height: 8),
          PlayerStatisticRow(
            dense: true,
            items: [
              PlayerStatItem('TREINADOR', _nickname),
              PlayerStatItem('CLUBE ATUAL', clubName, highlight: true),
              PlayerStatItem('DIVISÃO', divisionDisplay),
              if (snap?['gameDate'] != null)
                PlayerStatItem(
                  'DATA DO JOGO',
                  formatClassicDate(snap!['gameDate']),
                ),
              if (_myClubDetail?['growthCeiling'] != null)
                PlayerStatItem(
                  'TETO DE EVOLUÇÃO',
                  '${_myClubDetail!['growthCeiling']}',
                ),
              PlayerStatItem('POSIÇÃO', positionText, highlight: true),
              PlayerStatItem('PRÓXIMO ADVERSÁRIO', nextOpponentText),
              PlayerStatItem('PRÓXIMA PARTIDA', nextMatchText),
              PlayerStatItem(
                'SALDO EM CAIXA',
                formatClassicMoney(cash),
                highlight: true,
              ),
              PlayerStatItem('PREPARAÇÃO', prepText),
            ],
          ),
          const SizedBox(height: 6),
          Wrap(
            spacing: 8,
            runSpacing: 6,
            crossAxisAlignment: WrapCrossAlignment.center,
            children: [
              if (invitations.isNotEmpty)
                InkWell(
                  onTap: () => setState(() {
                    _activeTabIndex = 4;
                    _showMatchView = false;
                  }),
                  child: Container(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 8,
                      vertical: 4,
                    ),
                    decoration: BoxDecoration(
                      color: ClassicTheme.of(context).surfaceAlternate,
                      border: Border.all(
                        color: ClassicTheme.of(context).selectedBackground,
                      ),
                    ),
                    child: Text(
                      '[!] ${invitations.length} CONVITE(S) PENDENTE(S) — CLIQUE PARA VER',
                      style: TextStyle(
                        fontSize: 11,
                        fontWeight: FontWeight.bold,
                        color: ClassicTheme.of(context).accent,
                      ),
                    ),
                  ),
                ),
              ClassicActionButton(
                buttonKey: const ValueKey('quick-action-escalacao'),
                label: 'ESCALAÇÃO',
                onPressed: _tacticsLocked ? null : _autoPickBestLineup,
              ),
              ClassicActionButton(
                buttonKey: const ValueKey('quick-action-taticas'),
                label: 'TÁTICAS ($_selectedFormation)',
                onPressed: _tacticsLocked
                    ? null
                    : () {
                        final idx = availableFormations.indexOf(
                          _selectedFormation,
                        );
                        final nextIdx = (idx + 1) % availableFormations.length;
                        _changeFormation(availableFormations[nextIdx]);
                      },
              ),
              ClassicActionButton(
                buttonKey: const ValueKey('quick-action-mercado'),
                label: 'MERCADO',
                onPressed: () => setState(() {
                  _activeTabIndex = 2;
                  _showMatchView = false;
                }),
              ),
              ClassicActionButton(
                buttonKey: const ValueKey('quick-action-classificacao'),
                label: 'CLASSIFICAÇÃO',
                onPressed: () => setState(() {
                  _activeTabIndex = 1;
                  _showMatchView = false;
                }),
              ),
              ClassicActionButton(
                buttonKey: const ValueKey('quick-action-proxima-partida'),
                label: 'PRÓXIMA PARTIDA',
                onPressed: () => setState(() {
                  _showMatchView = RoundViewCoordinator.isMatchStage(
                    _snapshot?['roundState']?.toString(),
                  );
                  if (!_showMatchView) _activeTabIndex = 0;
                }),
              ),
            ],
          ),
        ],
      ),
    );
  }

  void _changeFormation(String formation) {
    if (_tacticsLocked ||
        !isValidTacticalFormation(formation) ||
        formation == _selectedFormation) {
      return;
    }
    final squad = (_myClubDetail?['squad'] as List? ?? [])
        .whereType<Map>()
        .map((p) => Map<String, dynamic>.from(p))
        .toList();
    final lineup = lineupForFormation(
      squad,
      formation,
      _selectedStarterIds,
      _selectedBenchIds,
    );
    setState(() {
      _selectedFormation = formation;
      _selectedStarterIds = lineup.starters;
      _selectedBenchIds = lineup.bench;
      _statusBanner =
          'Formação $formation aplicada. Confira os titulares antes de enviar a tática.';
    });
  }

  Future<void> _autoPickBestLineup() => _selectServerLineup('Automatic');

  /// Section 5 & 6: Visualização tática clássica (Campo + Compatibilidade Setorial + Troca Rápida)
  Widget _buildTacticalFieldPreview(List<dynamic> squad) {
    final parts = _selectedFormation
        .split('-')
        .map((n) => int.tryParse(n) ?? 0)
        .toList();
    final targetD = parts.isNotEmpty ? parts[0] : 4;
    final targetM = parts.length > 1 ? parts[1] : 4;
    final targetA = parts.length > 2 ? parts[2] : 2;

    final starters = squad
        .where((p) => _selectedStarterIds.contains(p['id'].toString()))
        .toList();
    final bench = squad
        .where((p) => _selectedBenchIds.contains(p['id'].toString()))
        .toList();
    final unavailable = squad
        .where(
          (p) =>
              ((p['injuryRoundsRemaining'] as num?)?.toInt() ?? 0) > 0 ||
              ((p['injuryDaysRemaining'] as num?)?.toInt() ?? 0) > 0 ||
              ((p['suspensionRoundsRemaining'] as num?)?.toInt() ?? 0) > 0,
        )
        .toList();

    final gStarters = starters
        .where((p) => _formatPosition(p['position']) == 'G')
        .toList();
    final dStarters = starters
        .where((p) => _formatPosition(p['position']) == 'D')
        .toList();
    final mStarters = starters
        .where((p) => _formatPosition(p['position']) == 'M')
        .toList();
    final aStarters = starters
        .where((p) => _formatPosition(p['position']) == 'A')
        .toList();

    final isComplete =
        _requiredStarterCount > 0 && starters.length == _requiredStarterCount;
    final isSectorMatch =
        isComplete &&
        gStarters.length == 1 &&
        dStarters.length == targetD &&
        mStarters.length == targetM &&
        aStarters.length == targetA;

    final validSwapStarter =
        starters.any((p) => p['id'].toString() == _swapStarterId)
        ? _swapStarterId
        : (starters.isNotEmpty ? starters.first['id'].toString() : null);
    final validSwapReserve =
        bench.any((p) => p['id'].toString() == _swapReserveId)
        ? _swapReserveId
        : (bench.isNotEmpty ? bench.first['id'].toString() : null);

    Widget buildSectorLine(
      String label,
      int targetCount,
      List<dynamic> players,
    ) {
      final countMatch = players.length == targetCount;
      return Container(
        margin: const EdgeInsets.symmetric(vertical: 3),
        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 5),
        decoration: BoxDecoration(
          color: ClassicTheme.of(context).surfaceAlternate,
          border: Border.all(
            color: countMatch
                ? ClassicTheme.of(context).border
                : const Color(0xFFFFB74D),
          ),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Wrap(
              spacing: 8,
              runSpacing: 2,
              alignment: WrapAlignment.spaceBetween,
              children: [
                Text(
                  '$label (${players.length}/$targetCount)',
                  style: TextStyle(
                    fontSize: 10,
                    fontWeight: FontWeight.bold,
                    color: countMatch
                        ? ClassicTheme.of(context).accent
                        : const Color(0xFFFFB74D),
                  ),
                ),
                if (targetCount == 0 && players.isEmpty)
                  Text(
                    'Setor sem atletas neste esquema',
                    style: TextStyle(
                      fontSize: 10,
                      color: ClassicTheme.of(context).accent,
                    ),
                  ),
              ],
            ),
            if (players.isNotEmpty) ...[
              const SizedBox(height: 4),
              Wrap(
                spacing: 6,
                runSpacing: 4,
                children: players.map((p) {
                  final pos = _formatPosition(p['position']);
                  final name =
                      p['displayName']?.toString() ?? p['name'].toString();
                  final str = p['strength'] ?? 20;
                  return Container(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 6,
                      vertical: 2,
                    ),
                    decoration: BoxDecoration(
                      color: ClassicTheme.of(context).surface,
                      border: Border.all(color: Colors.lightGreenAccent),
                    ),
                    child: Text(
                      '[$pos] $name ($str)',
                      style: TextStyle(
                        fontSize: 10,
                        fontWeight: FontWeight.bold,
                        color: ClassicTheme.of(context).textPrimary,
                      ),
                    ),
                  );
                }).toList(),
              ),
            ],
          ],
        ),
      );
    }

    return Container(
      key: const ValueKey('tactical-field-preview'),
      padding: const EdgeInsets.all(10),
      decoration: BoxDecoration(
        color: ClassicTheme.of(context).surface,
        border: Border.all(color: ClassicTheme.of(context).border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          ClassicTableHeader(
            title: 'CAMPO TÁTICO CLÁSSICO — ESQUEMA $_selectedFormation',
            trailing: isSectorMatch
                ? 'SETORES COMPATÍVEIS [OK]'
                : isComplete && starters.length < 11
                ? 'EM INFERIORIDADE (${starters.length}/11 APTOS)'
                : isComplete
                ? 'DESVIO SETORIAL (${gStarters.length}G-${dStarters.length}D-${mStarters.length}M-${aStarters.length}A)'
                : 'ESCALAÇÃO INCOMPLETA (${starters.length}/11)',
          ),
          const SizedBox(height: 6),
          Center(
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 480),
              child: ClassicTacticalPitch(
                lines: tacticalPitchLines(
                  starters
                      .map(
                        (p) =>
                            Map<String, dynamic>.from(p as Map)
                              ..['position'] = _formatPosition(p['position']),
                      )
                      .toList(),
                  _selectedFormation,
                ),
                shirt: _clubColor(
                  _myClubDetail?['colorPalette'],
                  'primaryColor',
                ),
                ink: Colors.white,
                onPlayer: _showPlayerDetailDialog,
              ),
            ),
          ),
          ExpansionTile(
            tilePadding: EdgeInsets.zero,
            title: const Text(
              'TITULARES E POSIÇÕES',
              style: TextStyle(fontSize: 11),
            ),
            children: [
              buildSectorLine('ATAQUE', targetA, aStarters),
              buildSectorLine('MEIO-CAMPO', targetM, mStarters),
              buildSectorLine('DEFESA', targetD, dStarters),
              buildSectorLine('GOLEIRO', 1, gStarters),
            ],
          ),
          if (bench.isNotEmpty)
            ExpansionTile(
              tilePadding: EdgeInsets.zero,
              title: Text(
                'BANCO E RESERVAS (${bench.length})',
                style: const TextStyle(fontSize: 11),
              ),
              children: [
                for (final p in bench)
                  ListTile(
                    dense: true,
                    title: Text(
                      '${p['displayName']}',
                      style: const TextStyle(fontSize: 11),
                    ),
                    subtitle: Text(
                      '${_formatPosition(p['position'])} • Força ${p['strength']}',
                    ),
                    onTap: () => _showPlayerDetailDialog(
                      Map<String, dynamic>.from(p as Map),
                    ),
                  ),
              ],
            ),
          const SizedBox(height: 6),
          // Quick Starter <-> Reserve Swap bar
          if (starters.isNotEmpty && bench.isNotEmpty)
            Container(
              padding: const EdgeInsets.all(8),
              decoration: BoxDecoration(
                color: ClassicTheme.of(context).surface,
                border: Border.all(color: ClassicTheme.of(context).border),
              ),
              child: Wrap(
                spacing: 10,
                runSpacing: 6,
                crossAxisAlignment: WrapCrossAlignment.center,
                children: [
                  Text(
                    'TROCA RÁPIDA:',
                    style: TextStyle(
                      color: ClassicTheme.of(context).accent,
                      fontWeight: FontWeight.bold,
                      fontSize: 11,
                    ),
                  ),
                  ConstrainedBox(
                    constraints: const BoxConstraints(maxWidth: 230),
                    child: DropdownButton<String>(
                      key: const ValueKey('dropdown-swap-starter'),
                      isExpanded: true,
                      value: validSwapStarter,
                      dropdownColor: ClassicTheme.of(context).surface,
                      icon: _classicDropdownIcon(context),
                      items: starters
                          .map(
                            (p) => DropdownMenuItem(
                              value: p['id'].toString(),
                              child: Text(
                                'Sai Titular: [${_formatPosition(p['position'])}] ${p['displayName']} (${p['strength']})',
                                maxLines: 1,
                                overflow: TextOverflow.ellipsis,
                                style: const TextStyle(fontSize: 11),
                              ),
                            ),
                          )
                          .toList(),
                      onChanged: (v) => setState(() => _swapStarterId = v),
                    ),
                  ),
                  ConstrainedBox(
                    constraints: const BoxConstraints(maxWidth: 230),
                    child: DropdownButton<String>(
                      key: const ValueKey('dropdown-swap-reserve'),
                      isExpanded: true,
                      value: validSwapReserve,
                      dropdownColor: ClassicTheme.of(context).surface,
                      icon: _classicDropdownIcon(context),
                      items: bench
                          .map(
                            (p) => DropdownMenuItem(
                              value: p['id'].toString(),
                              child: Text(
                                'Entra Reserva: [${_formatPosition(p['position'])}] ${p['displayName']} (${p['strength']})',
                                maxLines: 1,
                                overflow: TextOverflow.ellipsis,
                                style: const TextStyle(fontSize: 11),
                              ),
                            ),
                          )
                          .toList(),
                      onChanged: (v) => setState(() => _swapReserveId = v),
                    ),
                  ),
                  ClassicActionButton(
                    buttonKey: const ValueKey('btn-trocar-titular-reserva'),
                    label: 'TROCAR TITULAR POR RESERVA',
                    onPressed: () {
                      if (validSwapStarter == null ||
                          validSwapReserve == null) {
                        return;
                      }
                      _swapSquadPlayers(validSwapStarter, validSwapReserve);
                    },
                  ),
                ],
              ),
            ),
          if (unavailable.isNotEmpty) ...[
            const SizedBox(height: 6),
            Text(
              'INDISPONÍVEIS (${unavailable.length}): ${unavailable.map((p) {
                final inj = (p['injuryRoundsRemaining'] as num?)?.toInt() ?? 0;
                final days = (p['injuryDaysRemaining'] as num?)?.toInt() ?? 0;
                final sus = (p['suspensionRoundsRemaining'] as num?)?.toInt() ?? 0;
                return '${p['displayName']} (${days > 0
                    ? "LES $days dias"
                    : inj > 0
                    ? "LES ${inj}r"
                    : "SUS ${sus}r"})';
              }).join(" | ")}',
              style: const TextStyle(fontSize: 11, color: Colors.orangeAccent),
            ),
          ],
        ],
      ),
    );
  }

  void _showCoachCareerDetailDialog(Map<String, dynamic> coach) {
    final isMe =
        coach['coachId']?.toString() == _coachId ||
        coach['name']?.toString() == _nickname;
    final empHist = isMe
        ? (_invitationsData?['employmentHistory'] as List<dynamic>? ?? [])
        : const [];
    final seasonHist = _rankingData?['seasonHistory'] as List<dynamic>? ?? [];
    final wonSeasons = seasonHist
        .where(
          (s) =>
              s['firstDivisionChampionName'] == coach['currentClubName'] ||
              s['cupChampionName'] == coach['currentClubName'],
        )
        .toList();

    showDialog<void>(
      context: context,
      builder: (_) => ClassicDialog(
        dialogKey: const ValueKey('coach-career-detail-dialog'),
        title: 'FICHA DE CARREIRA — ${coach['name']}',
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          mainAxisSize: MainAxisSize.min,
          children: [
            PlayerStatisticRow(
              items: [
                PlayerStatItem('POSIÇÃO', '${coach['rank']}º', highlight: true),
                PlayerStatItem(
                  'PERFIL',
                  coach['isHuman'] == true ? 'HUMANO' : 'IA',
                ),
                PlayerStatItem('CLUBE ATUAL', '${coach['currentClubName']}'),
                PlayerStatItem(
                  'CAMPEONATOS',
                  '${coach['leagueTitles']}',
                  highlight: true,
                ),
                PlayerStatItem(
                  'TAÇAS',
                  '${coach['cupTitles']}',
                  highlight: true,
                ),
                PlayerStatItem('PONTOS', '${coach['coachPoints']}'),
                PlayerStatItem(
                  'RETROSPECTO',
                  '${coach['totalWins']}V ${coach['totalDraws']}E ${coach['totalLosses']}D',
                ),
              ],
            ),
            if (empHist.isNotEmpty) ...[
              const SizedBox(height: 12),
              const ClassicTableHeader(title: 'HISTÓRICO DE CLUBES'),
              const SizedBox(height: 4),
              for (final h in empHist.reversed)
                Padding(
                  padding: const EdgeInsets.symmetric(vertical: 2),
                  child: Text(
                    '• ${h['clubName']}: T${h['startSeason']} R${h['startRound']} até ${h['endSeason'] == null ? 'atual' : 'T${h['endSeason']} R${h['endRound']}'}',
                    style: const TextStyle(fontSize: 12),
                  ),
                ),
            ],
            if (wonSeasons.isNotEmpty) ...[
              const SizedBox(height: 12),
              const ClassicTableHeader(
                title: 'TEMPORADAS COM TÍTULOS DO CLUBE ATUAL',
              ),
              const SizedBox(height: 4),
              for (final s in wonSeasons)
                Text(
                  '• Temporada ${s['seasonNumber']}: 1ª Div: ${s['firstDivisionChampionName']} | Taça: ${s['cupChampionName']}',
                  style: const TextStyle(fontSize: 12),
                ),
            ],
          ],
        ),
      ),
    );
  }

  Widget _buildCalendarScreen() {
    final currentRound = (_snapshot?['roundNumber'] as num?)?.toInt() ?? 1;
    final myClubName = _myClubDetail?['name']?.toString();
    final rounds = _calendar
        .where(
          (r) =>
              _selectedCalendarFilter != 'CURRENT' ||
              r['roundNumber'] == currentRound,
        )
        .toList();
    return ListView.builder(
      key: PageStorageKey('calendar-$_worldId-$_selectedCalendarFilter'),
      padding: const EdgeInsets.all(10),
      itemCount: 1 + (rounds.isEmpty ? 1 : rounds.length),
      itemBuilder: (context, index) {
        if (index == 0) {
          return Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const ClassicPageHeader(
                title: 'CALENDÁRIO DA TEMPORADA E RESULTADOS',
                icon: Icons.calendar_month,
                subtitle:
                    'Veja o calendário completo da temporada e os resultados das partidas já disputadas.',
              ),
              const SizedBox(height: 10),
              ClassicSelectionTabs(
                key: const Key('calendar-filters'),
                selected: _selectedCalendarFilter,
                items: [
                  ('ALL', 'TODAS AS RODADAS'),
                  ('CURRENT', 'RODADA ATUAL (R$currentRound)'),
                  if (myClubName != null) ('MY_CLUB', 'MEU CLUBE'),
                ],
                onSelected: (value) =>
                    setState(() => _selectedCalendarFilter = value),
              ),
              const SizedBox(height: 10),
            ],
          );
        }
        if (rounds.isEmpty) {
          return const ClassicPaperPanel(
            child: Text('Calendário ainda não disponível.'),
          );
        }
        final r = rounds[index - 1];
        final fixtures = (r['fixtures'] as List? ?? [])
            .where(
              (f) =>
                  _selectedCalendarFilter != 'MY_CLUB' ||
                  f['homeClubId'] == _myClubId ||
                  f['awayClubId'] == _myClubId ||
                  f['home'] == myClubName ||
                  f['away'] == myClubName,
            )
            .toList();
        final current = r['roundNumber'] == currentRound;
        return Padding(
          padding: const EdgeInsets.only(bottom: 7),
          child: ClassicPaperPanel(
            padding: EdgeInsets.zero,
            child: ExpansionTile(
              key: PageStorageKey(
                'calendar-$_selectedCalendarFilter-${r['roundNumber']}',
              ),
              initiallyExpanded: current || _selectedCalendarFilter != 'ALL',
              tilePadding: const EdgeInsets.symmetric(horizontal: 10),
              title: Text(
                'Rodada ${r['roundNumber']} • ${classicCompetition(r['competition'])}${current ? ' [ATUAL]' : ''}',
                style: TextStyle(fontSize: 19, fontWeight: FontWeight.bold),
              ),
              backgroundColor: ClassicTheme.of(context).primary,
              collapsedBackgroundColor: ClassicTheme.of(context).paper.surface,
              textColor: current
                  ? ClassicTheme.of(context).selectedBackground
                  : ClassicTheme.of(context).textPrimary,
              collapsedTextColor: ClassicTheme.of(context).paper.textPrimary,
              iconColor: ClassicTheme.of(context).selectedBackground,
              collapsedIconColor: ClassicTheme.of(context).paper.accent,
              children: [
                if (fixtures.isEmpty)
                  ClassicPaper(
                    builder: (_) => Padding(
                      padding: const EdgeInsets.all(14),
                      child: Text(
                        _selectedCalendarFilter == 'MY_CLUB' &&
                                (r['fixtures'] as List? ?? []).isNotEmpty
                            ? 'Seu clube não disputa esta rodada.'
                            : 'Confrontos definidos após a fase anterior da Taça.',
                      ),
                    ),
                  )
                else
                  ConstrainedBox(
                    constraints: BoxConstraints(
                      maxHeight: MediaQuery.sizeOf(context).width < 700
                          ? 280
                          : 440,
                    ),
                    child: SingleChildScrollView(
                      key: PageStorageKey(
                        'calendar-fixtures-$_selectedCalendarFilter-${r['roundNumber']}',
                      ),
                      child: ClassicFixtureList(
                        fixtures: fixtures,
                        tableKey: 'calendar-round-${r['roundNumber']}',
                        live: current && _matchLocked,
                        palette: (id, name) => _teamPalette(id: id, name: name),
                      ),
                    ),
                  ),
              ],
            ),
          ),
        );
      },
    );
  }
}
