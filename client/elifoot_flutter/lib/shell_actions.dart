// ignore_for_file: invalid_use_of_protected_member
part of 'main.dart';

class _ShellDropdownArrowPainter extends CustomPainter {
  const _ShellDropdownArrowPainter();

  @override
  void paint(Canvas canvas, Size size) {
    final paint = Paint()
      ..color = const Color(0xFFFFD700)
      ..style = PaintingStyle.fill;
    final path = Path()
      ..moveTo(0, 0)
      ..lineTo(size.width, 0)
      ..lineTo(size.width / 2, size.height)
      ..close();
    canvas.drawPath(path, paint);
  }

  @override
  bool shouldRepaint(covariant CustomPainter oldDelegate) => false;
}

Widget _classicDropdownIcon() => const Padding(
  padding: EdgeInsets.only(right: 4),
  child: CustomPaint(
    size: Size(10, 6),
    painter: _ShellDropdownArrowPainter(),
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
    if (_worldId!=null && incoming['gameWorldId']!=null && incoming['gameWorldId']!=_worldId) return _snapshot;
    if ((_snapshot?['roundVersion'] as num? ?? 0) >
        (incoming['roundVersion'] as num? ?? 0)) {
      return _snapshot;
    }
    return incoming;
  }

  Future<void> _saveSession() {
    return writeSession(
      jsonEncode({
        'nickname': _nickname,
        'deviceId': _deviceId,
        'worldId': _worldId,
      }),
    );
  }

  Future<void> _joinByCodeDialog() async {
    final code = TextEditingController();
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        backgroundColor: const Color(0xFF0E3B19),
        shape: const RoundedRectangleBorder(
          borderRadius: BorderRadius.zero,
          side: BorderSide(color: Color(0xFFFFD700), width: 1.5),
        ),
        title: const Text(
          'ENTRAR POR CÓDIGO',
          style: TextStyle(
            color: Color(0xFFFFD700),
            fontWeight: FontWeight.bold,
            fontSize: 14,
          ),
        ),
        content: TextField(
          controller: code,
          decoration: const InputDecoration(
            labelText: 'Código do convite',
            border: OutlineInputBorder(borderRadius: BorderRadius.zero),
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
        await _joinSelectedWorld(result['worldId']);
      } catch (e) {
        if (mounted) setState(() => _statusBanner = '$e');
      }
    }
    await Future<void>.delayed(const Duration(milliseconds: 300));
    code.dispose();
  }

  Future<void> _chooseClub(String worldId) async {
    try {
      final snap = await widget.apiClient.getSnapshot(worldId);
      if ((snap['readyCounter']['participants'] as List).any(
        (p) => p['coachId'] == _coachId,
      )) {
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
        builder: (ctx) {
          final screen = MediaQuery.sizeOf(ctx);
          final isMobile = screen.width < 600;
          return AlertDialog(
            backgroundColor: const Color(0xFF0E3B19),
            insetPadding: EdgeInsets.symmetric(
              horizontal: isMobile ? 8 : 24,
              vertical: isMobile ? 10 : 20,
            ),
            shape: const RoundedRectangleBorder(
              borderRadius: BorderRadius.zero,
              side: BorderSide(color: Color(0xFFFFD700), width: 1.5),
            ),
            title: const Text(
              'SELECIONE SEU CLUBE',
              style: TextStyle(
                color: Color(0xFFFFD700),
                fontWeight: FontWeight.bold,
                fontSize: 14,
              ),
            ),
            content: SizedBox(
              width: isMobile ? screen.width - 24 : 620,
              height: (screen.height * 0.65).clamp(260.0, 460.0),
              child: ListView.separated(
                itemCount: availableClubs.length,
                separatorBuilder: (_, _) =>
                    const Divider(height: 1, color: Colors.white24),
                itemBuilder: (_, idx) {
                  final c = availableClubs[idx];
                  final cashStr = formatClassicMoney(
                    (c['cashBalance'] as num?) ?? 0,
                  );
                  return InkWell(
                    key: Key('choose-club-${c['id']}'),
                    onTap: () => Navigator.pop(ctx, c['id'].toString()),
                    child: Padding(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 8,
                        vertical: 8,
                      ),
                      child: Row(
                        children: [
                          Container(
                            width: 14,
                            height: 24,
                            decoration: BoxDecoration(
                              color: _clubColor(c['primaryColorHex']),
                              border: Border.all(
                                color: _clubColor(c['secondaryColorHex']),
                                width: 2,
                              ),
                            ),
                          ),
                          const SizedBox(width: 10),
                          Expanded(
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Text(
                                  '${c['name']}',
                                  style: const TextStyle(
                                    color: Color(0xFFFFD700),
                                    fontWeight: FontWeight.bold,
                                    fontSize: 12,
                                  ),
                                ),
                                Text(
                                  '${c['divisionDisplay']} • Caixa $cashStr',
                                  style: const TextStyle(
                                    fontSize: 11,
                                    color: Colors.white70,
                                  ),
                                ),
                              ],
                            ),
                          ),
                          Container(
                            padding: const EdgeInsets.symmetric(
                              horizontal: 8,
                              vertical: 4,
                            ),
                            decoration: BoxDecoration(
                              color: const Color(0xFF145224),
                              border: Border.all(
                                color: const Color(0xFFFFD700),
                              ),
                            ),
                            child: const Text(
                              'ASSUMIR',
                              style: TextStyle(
                                color: Color(0xFFFFD700),
                                fontSize: 10,
                                fontWeight: FontWeight.bold,
                              ),
                            ),
                          ),
                        ],
                      ),
                    ),
                  );
                },
              ),
            ),
            actions: [
              TextButton(
                onPressed: () => Navigator.pop(ctx),
                child: const Text('CANCELAR'),
              ),
            ],
          );
        },
      );
      if (selected != null) {
        await _joinSelectedWorld(worldId, preferredClubId: selected);
      }
    } catch (e) {
      if (mounted) setState(() => _statusBanner = '$e');
    }
  }

  Color _clubColor(dynamic hex) => Color(
    int.tryParse(
          'FF${(hex ?? '#FFFFFF').toString().replaceFirst('#', '')}',
          radix: 16,
        ) ??
        0xFFFFFFFF,
  );

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
    final moraleVal = me?['morale'] ?? _invitationsData?['morale'] ?? 70;
    final moraleBand =
        me?['moraleBand'] ?? _invitationsData?['moraleBand'] ?? 'VERDE';
    final moraleColor = moraleBand == 'VERMELHA'
        ? const Color(0xFFEF5350)
        : moraleBand == 'AMARELA'
        ? const Color(0xFFFFD54F)
        : const Color(0xFF81C784);

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
          color: const Color(0xFF0E3B19),
          border: Border.all(color: const Color(0xFFFFD700), width: 1.5),
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
                PlayerStatItem('MORAL', '$moraleVal/100 ($moraleBand)'),
                PlayerStatItem('RODADA ATUAL', 'T$seasonNumber • R$roundNumber'),
                if (snap?['gameDate'] != null)
                  PlayerStatItem('DATA DO JOGO', formatClassicDate(snap!['gameDate'])),
                PlayerStatItem(
                  'CONVITES',
                  '${invitations.length} disponível(is)',
                  highlight: invitations.isNotEmpty,
                ),
              ],
            ),
            const SizedBox(height: 10),
            const Text(
              'Use o botão "PRONTO PARA ACOMPANHAR" no topo para avançar as rodadas com a sala, ou veja a aba "6. RANKING & CARREIRA" para aceitar convites de clubes!',
              style: TextStyle(fontSize: 11, color: Colors.white70),
            ),
            const SizedBox(height: 10),
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: [
                ElevatedButton(
                  key: const ValueKey('btn-ver-convites-desempregado'),
                  onPressed: () => setState(() { _activeTabIndex = 4; _showMatchView = false; }),
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
                  onPressed: () => setState(() { _activeTabIndex = 1; _showMatchView = false; }),
                ),
                ClassicActionButton(
                  label: 'ACOMPANHAR RODADA',
                  onPressed: () => setState(() { _showMatchView = RoundViewCoordinator.isMatchStage(_snapshot?['roundState']?.toString()); if (!_showMatchView) _activeTabIndex = 0; }),
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
                    color: const Color(0xFF136F2D),
                    border: Border.all(color: const Color(0xFFFFD700)),
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
                            style: const TextStyle(
                              color: Color(0xFFFFD700),
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
        nextMatchText = '${myCalFix['home']} x ${myCalFix['away']} (R$roundNumber)';
      }
    }

    final prepText = roundState == 'PREPARING'
        ? (_isClassicIdleRound ? 'SEM PARTIDA — PRONTO PARA ACOMPANHAR' : isReady
              ? 'TÁTICA ENVIADA (PRONTO)'
              : (_requiredStarterCount > 0 && _selectedStarterIds.length == _requiredStarterCount
                    ? 'ESCALADO (FALTA ENVIAR)'
                    : 'ESCALAÇÃO INCOMPLETA (${_selectedStarterIds.length}/11)'))
        : (roundState == 'RESULTS'
              ? 'RODADA FINALIZADA'
              : 'PARTIDA EM CURSO');

    return Container(
      key: const ValueKey('coach-main-panel'),
      margin: const EdgeInsets.only(bottom: 10),
      padding: const EdgeInsets.all(10),
      decoration: BoxDecoration(
        color: const Color(0xFF0E3B19),
        border: Border.all(color: const Color(0xFF3E8450)),
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
            items: [
              PlayerStatItem('TREINADOR', _nickname),
              PlayerStatItem('CLUBE ATUAL', clubName, highlight: true),
              PlayerStatItem('DIVISÃO', divisionDisplay),
              if (snap?['gameDate'] != null)
                PlayerStatItem('DATA DO JOGO', formatClassicDate(snap!['gameDate'])),
              if (_myClubDetail?['growthCeiling'] != null)
                PlayerStatItem('TETO DE EVOLUÇÃO', '${_myClubDetail!['growthCeiling']}'),
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
              Container(
                padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 4),
                decoration: BoxDecoration(
                  color: const Color(0xFF062E0F),
                  border: Border.all(color: moraleColor),
                ),
                child: Text(
                  'MORAL DO CARGO: $moraleVal/100 ($moraleBand)',
                  style: TextStyle(
                    fontSize: 11,
                    fontWeight: FontWeight.bold,
                    color: moraleColor,
                  ),
                ),
              ),
              if (invitations.isNotEmpty)
                InkWell(
                  onTap: () => setState(() { _activeTabIndex = 4; _showMatchView = false; }),
                  child: Container(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 8,
                      vertical: 4,
                    ),
                    decoration: BoxDecoration(
                      color: const Color(0xFF1B5E20),
                      border: Border.all(color: const Color(0xFFFFD700)),
                    ),
                    child: Text(
                      '[!] ${invitations.length} CONVITE(S) PENDENTE(S) — CLIQUE PARA VER',
                      style: const TextStyle(
                        fontSize: 11,
                        fontWeight: FontWeight.bold,
                        color: Color(0xFFFFD700),
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
                onPressed: _tacticsLocked ? null : () {
                  final idx = _ElifootShellScreenState.availableFormations
                      .indexOf(_selectedFormation);
                  final nextIdx =
                      (idx + 1) %
                      _ElifootShellScreenState.availableFormations.length;
                  setState(() {
                    _selectedFormation =
                        _ElifootShellScreenState.availableFormations[nextIdx];
                    _statusBanner =
                        'Esquema tático alterado para $_selectedFormation.';
                  });
                },
              ),
              ClassicActionButton(
                buttonKey: const ValueKey('quick-action-mercado'),
                label: 'MERCADO',
                onPressed: () => setState(() { _activeTabIndex = 2; _showMatchView = false; }),
              ),
              ClassicActionButton(
                buttonKey: const ValueKey('quick-action-classificacao'),
                label: 'CLASSIFICAÇÃO',
                onPressed: () => setState(() { _activeTabIndex = 1; _showMatchView = false; }),
              ),
              ClassicActionButton(
                buttonKey: const ValueKey('quick-action-proxima-partida'),
                label: 'PRÓXIMA PARTIDA',
                onPressed: () => setState(() { _showMatchView = RoundViewCoordinator.isMatchStage(_snapshot?['roundState']?.toString()); if(!_showMatchView) _activeTabIndex=0; }),
              ),
            ],
          ),
        ],
      ),
    );
  }

  void _autoPickBestLineup() {
    if (_tacticsLocked) return;
    final squad = _myClubDetail?['squad'] as List<dynamic>? ?? [];
    final avail = squad
        .where(
          (p) =>
              (p['injuryRoundsRemaining'] ?? 0) == 0 &&
              (p['injuryDaysRemaining'] ?? 0) == 0 &&
              (p['suspensionRoundsRemaining'] ?? 0) == 0,
        )
        .toList();
    avail.sort(
      (a, b) =>
          ((b['strength'] as num?) ?? 0).compareTo((a['strength'] as num?) ?? 0),
    );
    final parts = _selectedFormation
        .split('-')
        .map((n) => int.tryParse(n) ?? 0)
        .toList();
    final targetD = parts.isNotEmpty ? parts[0] : 4;
    final targetM = parts.length > 1 ? parts[1] : 4;
    final targetA = parts.length > 2 ? parts[2] : 2;

    final gks = avail
        .where((p) => _formatPosition(p['position']) == 'G')
        .toList();
    final defs = avail
        .where((p) => _formatPosition(p['position']) == 'D')
        .toList();
    final mids = avail
        .where((p) => _formatPosition(p['position']) == 'M')
        .toList();
    final atts = avail
        .where((p) => _formatPosition(p['position']) == 'A')
        .toList();

    final chosenStarters = <String>[];
    if (gks.isNotEmpty) chosenStarters.add(gks.first['id'].toString());
    for (final p in defs.take(targetD)) {
      chosenStarters.add(p['id'].toString());
    }
    for (final p in mids.take(targetM)) {
      chosenStarters.add(p['id'].toString());
    }
    for (final p in atts.take(targetA)) {
      chosenStarters.add(p['id'].toString());
    }
    // Fill up to 11 with best remaining outfield players if a sector is short
    for (final p in avail) {
      if (chosenStarters.length >= 11) break;
      final id = p['id'].toString();
      if (!chosenStarters.contains(id) &&
          _formatPosition(p['position']) != 'G') {
        chosenStarters.add(id);
      }
    }
    final chosenBench = <String>[];
    for (final p in gks.skip(1).take(1)) {
      final id = p['id'].toString();
      if (!chosenStarters.contains(id)) chosenBench.add(id);
    }
    for (final p in avail) {
      if (chosenBench.length >= 9) break;
      final id = p['id'].toString();
      if (!chosenStarters.contains(id) && !chosenBench.contains(id)) {
        chosenBench.add(id);
      }
    }
    if (chosenStarters.isNotEmpty) {
      setState(() {
        _selectedStarterIds = chosenStarters;
        _selectedBenchIds = chosenBench;
        _statusBanner =
            'Escalação automática aplicada para o esquema $_selectedFormation (${chosenStarters.length} titulares, ${chosenBench.length} reservas).';
      });
    }
  }

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

    final isComplete = _requiredStarterCount > 0 && starters.length == _requiredStarterCount;
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
          color: const Color(0xFF0A4218),
          border: Border.all(
            color: countMatch
                ? const Color(0xFF3E8450)
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
                        ? const Color(0xFFFFD700)
                        : const Color(0xFFFFB74D),
                  ),
                ),
                if (targetCount == 0 && players.isEmpty)
                  const Text(
                    'Setor sem atletas neste esquema',
                    style: TextStyle(fontSize: 10, color: Colors.white54),
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
                      color: const Color(0xFF062E0F),
                      border: Border.all(color: Colors.lightGreenAccent),
                    ),
                    child: Text(
                      '[$pos] $name (F:$str)',
                      style: const TextStyle(
                        fontSize: 10,
                        fontWeight: FontWeight.bold,
                        color: Colors.white,
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
        color: const Color(0xFF062E0F),
        border: Border.all(color: const Color(0xFF3E8450)),
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
          buildSectorLine('ATAQUE', targetA, aStarters),
          buildSectorLine('MEIO-CAMPO', targetM, mStarters),
          buildSectorLine('DEFESA', targetD, dStarters),
          buildSectorLine('GOLEIRO', 1, gStarters),
          const SizedBox(height: 6),
          // Quick Starter <-> Reserve Swap bar
          if (starters.isNotEmpty && bench.isNotEmpty)
            Container(
              padding: const EdgeInsets.all(8),
              decoration: BoxDecoration(
                color: const Color(0xFF0E3B19),
                border: Border.all(color: const Color(0xFF3E8450)),
              ),
              child: Wrap(
                spacing: 10,
                runSpacing: 6,
                crossAxisAlignment: WrapCrossAlignment.center,
                children: [
                  const Text(
                    'TROCA RÁPIDA:',
                    style: TextStyle(
                      color: Color(0xFFFFD700),
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
                      dropdownColor: const Color(0xFF062E0F),
                      icon: _classicDropdownIcon(),
                      items: starters
                          .map(
                            (p) => DropdownMenuItem(
                              value: p['id'].toString(),
                              child: Text(
                                'Sai Titular: [${_formatPosition(p['position'])}] ${p['displayName']} (F:${p['strength']})',
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
                      dropdownColor: const Color(0xFF062E0F),
                      icon: _classicDropdownIcon(),
                      items: bench
                          .map(
                            (p) => DropdownMenuItem(
                              value: p['id'].toString(),
                              child: Text(
                                'Entra Reserva: [${_formatPosition(p['position'])}] ${p['displayName']} (F:${p['strength']})',
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
                      setState(() {
                        final sIdx = _selectedStarterIds.indexOf(
                          validSwapStarter,
                        );
                        final bIdx = _selectedBenchIds.indexOf(
                          validSwapReserve,
                        );
                        if (sIdx >= 0 && bIdx >= 0) {
                          _selectedStarterIds[sIdx] = validSwapReserve;
                          _selectedBenchIds[bIdx] = validSwapStarter;
                          _swapStarterId = validSwapReserve;
                          _swapReserveId = validSwapStarter;
                          _statusBanner =
                              'Troca realizada entre titular e reserva.';
                        }
                      });
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
                return '${p['displayName']} (${days > 0 ? "LES $days dias" : inj > 0 ? "LES ${inj}r" : "SUS ${sus}r"})';
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
    final filteredRounds = _calendar.where((r) {
      if (_selectedCalendarFilter == 'CURRENT') {
        return r['roundNumber'] == currentRound;
      }
      return true;
    }).toList();

    return ListView(
      padding: const EdgeInsets.all(12),
      children: [
        const ClassicSectionTitle(
          title: 'CALENDÁRIO DA TEMPORADA E RESULTADOS',
          subtitle:
              'Consulte os confrontos realizados e os jogos futuros da Liga e da Taça.',
        ),
        const SizedBox(height: 8),
        Wrap(
          spacing: 8,
          runSpacing: 8,
          children: [
            for (final filter in [
              ('ALL', 'TODAS AS RODADAS'),
              ('CURRENT', 'RODADA ATUAL (R$currentRound)'),
              if (myClubName != null) ('MY_CLUB', 'MEU CLUBE ($myClubName)'),
            ])
              ClassicActionButton(
                label: filter.$2,
                onPressed: () =>
                    setState(() => _selectedCalendarFilter = filter.$1),
              ),
          ],
        ),
        const SizedBox(height: 10),
        for (final r in filteredRounds) ...[
          Builder(
            builder: (context) {
              final allFixtures = r['fixtures'] as List<dynamic>? ?? [];
              final fixtures =
                  (_selectedCalendarFilter == 'MY_CLUB' && myClubName != null)
                  ? allFixtures
                        .where(
                          (f) =>
                              f['home'] == myClubName ||
                              f['away'] == myClubName,
                        )
                        .toList()
                  : allFixtures;
              final isCurrent = r['roundNumber'] == currentRound;
              return Container(
                margin: const EdgeInsets.only(bottom: 8),
                decoration: BoxDecoration(
                  color: const Color(0xFF0E3B19),
                  border: Border.all(
                    color: isCurrent
                        ? const Color(0xFFFFD700)
                        : const Color(0xFF3E8450),
                    width: isCurrent ? 1.5 : 1,
                  ),
                ),
                child: ExpansionTile(
                  initiallyExpanded:
                      isCurrent || _selectedCalendarFilter != 'ALL',
                  collapsedIconColor: const Color(0xFFFFD700),
                  iconColor: const Color(0xFFFFD700),
                  title: Text(
                    'Rodada ${r['roundNumber']} • ${r['competition']}${isCurrent ? " [ATUAL]" : ""}',
                    style: TextStyle(
                      color: isCurrent ? const Color(0xFFFFD700) : Colors.white,
                      fontWeight: FontWeight.bold,
                      fontSize: 13,
                    ),
                  ),
                  children: [
                    if (fixtures.isEmpty)
                      const Padding(
                        padding: EdgeInsets.all(10),
                        child: Text(
                          'Confrontos definidos após a fase anterior da Taça.',
                          style: TextStyle(fontSize: 12, color: Colors.white70),
                        ),
                      ),
                    for (final f in fixtures)
                      Container(
                        padding: const EdgeInsets.symmetric(
                          horizontal: 12,
                          vertical: 6,
                        ),
                        decoration: BoxDecoration(
                          color:
                              (myClubName != null &&
                                  (f['home'] == myClubName ||
                                      f['away'] == myClubName))
                              ? const Color(0xFF185728)
                              : Colors.transparent,
                          border: const Border(
                            top: BorderSide(color: Colors.white12),
                          ),
                        ),
                        child: Row(
                          children: [
                            Expanded(
                              child: Text(
                                '${f['home']} ${f['isFinished'] == true ? "${f['homeGoals']} x ${f['awayGoals']}" : "×"} ${f['away']}',
                                style: const TextStyle(
                                  fontSize: 12,
                                  fontWeight: FontWeight.bold,
                                ),
                              ),
                            ),
                            Text(
                              f['isFinished'] == true ? 'FINAL' : 'A JOGAR',
                              style: TextStyle(
                                fontSize: 11,
                                fontWeight: FontWeight.bold,
                                color: f['isFinished'] == true
                                    ? const Color(0xFFFFD700)
                                    : Colors.white70,
                              ),
                            ),
                          ],
                        ),
                      ),
                  ],
                ),
              );
            },
          ),
        ],
      ],
    );
  }

}
