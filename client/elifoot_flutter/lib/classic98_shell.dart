// ignore_for_file: invalid_use_of_protected_member
part of 'main.dart';

/// A presentation adapter over the SAME shell state, command methods and models.
/// Selecting a row has no game-side effect. No API mutation is implemented here.
extension _Classic98Shell on _ElifootShellScreenState {
  void _openClubHistory(String id) {
    if (_matchLocked || _worldId == null) return;
    showClubHistoryDialog(
      context: context,
      api: widget.apiClient,
      worldId: _worldId!,
      clubId: id,
    );
  }

  List<Map<String, dynamic>> get _retroSquad {
    final list = (_myClubDetail?['squad'] as List? ?? [])
        .whereType<Map>()
        .map((p) => Map<String, dynamic>.from(p))
        .toList();
    int roleRank(Map<String, dynamic> p) {
      final id = '${p['id']}';
      if (_selectedStarterIds.contains(id)) return 0;
      if (_selectedBenchIds.contains(id)) return 1;
      return 2;
    }

    int posRank(Map<String, dynamic> p) =>
        const ['G', 'D', 'M', 'A'].indexOf(_formatPosition(p['position']));
    list.sort((a, b) {
      final r = roleRank(a).compareTo(roleRank(b));
      if (r != 0) return r;
      return posRank(a).compareTo(posRank(b));
    });
    return list;
  }

  Map<String, dynamic>? get _retroSelected => _retroSquad
      .where((p) => '${p['id']}' == _classicSelectedPlayerId)
      .firstOrNull;

  void _selectClassicPlayer(String? playerId) {
    final normalized = playerId?.trim();
    if (normalized == null || normalized.isEmpty) {
      setState(() {
        _classicSelectedPlayerId = null;
        _classicSelectedPlayerHistory = null;
        _classicPlayerStatsRequestSeq++;
      });
      return;
    }
    setState(() {
      _classicSelectedPlayerId = normalized;
      final cachedId =
          '${_classicSelectedPlayerHistory?['playerId'] ?? (_classicSelectedPlayerHistory?['career'] as Map?)?['playerId'] ?? ''}';
      if (cachedId != normalized) {
        _classicSelectedPlayerHistory = null;
      }
    });
    unawaited(_loadClassicSelectedPlayerHistory(normalized));
  }

  Future<void> _loadClassicSelectedPlayerHistory(String playerId) async {
    final worldId = _worldId;
    if (worldId == null || playerId.isEmpty) return;
    final requestSeq = ++_classicPlayerStatsRequestSeq;
    try {
      final history = await widget.apiClient.getPlayerHistory(
        worldId,
        playerId,
      );
      if (!mounted ||
          _worldId != worldId ||
          _classicSelectedPlayerId != playerId ||
          _classicPlayerStatsRequestSeq != requestSeq) {
        return;
      }
      setState(() {
        _classicSelectedPlayerHistory = history;
      });
    } catch (_) {
      // Fallback to PlayerDto statistics if history endpoint is unavailable.
    }
  }

  String _normalizeClassicDivisionTier(dynamic raw) {
    final v = '${raw ?? ''}'.trim();
    return switch (v) {
      'PrimeiraDivisao' ||
      '1' ||
      '1ª Divisão' ||
      '1a Divisão' ||
      '1ª Divisao' => 'PrimeiraDivisao',
      'SegundaDivisao' ||
      '2' ||
      '2ª Divisão' ||
      '2a Divisão' ||
      '2ª Divisao' => 'SegundaDivisao',
      'TerceiraDivisao' ||
      '3' ||
      '3ª Divisão' ||
      '3a Divisão' ||
      '3ª Divisao' => 'TerceiraDivisao',
      'QuartaDivisao' ||
      '4' ||
      '4ª Divisão' ||
      '4a Divisão' ||
      '4ª Divisao' => 'QuartaDivisao',
      'Distrital' || '5' || 'Divisão Distrital' => 'Distrital',
      _ => '',
    };
  }

  String _classicDivisionDisplayFromTier(String tier, [String? fallback]) {
    return switch (tier) {
      'PrimeiraDivisao' => '1ª Divisão',
      'SegundaDivisao' => '2ª Divisão',
      'TerceiraDivisao' => '3ª Divisão',
      'QuartaDivisao' => '4ª Divisão',
      'Distrital' => 'Divisão Distrital',
      _ =>
        (fallback != null && fallback.trim().isNotEmpty)
            ? fallback.trim()
            : '4ª Divisão',
    };
  }

  bool _isClassicLeagueCompetition(dynamic raw) {
    if (raw == null) return true;
    final v = '$raw'.trim();
    return v == 'League' || v == '0' || v == 'Campeonato Nacional';
  }

  List<(String, int)> _retroPlayerPanelStats(Map<String, dynamic> player) {
    final id = '${player['id']}';
    final h = _classicSelectedPlayerHistory;
    final historyPlayerId =
        '${h?['playerId'] ?? (h?['career'] as Map?)?['playerId'] ?? ''}';
    final validHistory = h != null && historyPlayerId == id ? h : null;
    final careerRow = validHistory?['career'] is Map
        ? Map<String, dynamic>.from(validHistory!['career'] as Map)
        : null;
    final rows = (validHistory?['rows'] as List? ?? const [])
        .whereType<Map>()
        .map((r) => Map<String, dynamic>.from(r))
        .toList();

    final clubDivRaw =
        _myClubDetail?['divisionTier'] ??
        _myClubDetail?['divisionDisplay'] ??
        validHistory?['currentDivisionTier'] ??
        validHistory?['currentDivisionDisplay'] ??
        player['currentDivisionTier'] ??
        player['currentDivisionDisplay'];
    final divisionTier = _normalizeClassicDivisionTier(clubDivRaw);
    final divisionDisplay = _classicDivisionDisplayFromTier(
      divisionTier,
      (_myClubDetail?['divisionDisplay'] ??
              validHistory?['currentDivisionDisplay'] ??
              player['currentDivisionDisplay'])
          ?.toString(),
    );

    int asInt(dynamic v) => v is num ? v.toInt() : int.tryParse('$v') ?? 0;
    int maxOf(Iterable<int> values) =>
        values.fold<int>(0, (acc, v) => v > acc ? v : acc);

    final currentSeason =
        (_snapshot?['seasonNumber'] as num?)?.toInt() ??
        (validHistory?['currentSeason'] as num?)?.toInt() ??
        1;

    final rowsMatches = rows.fold<int>(
      0,
      (sum, r) => sum + asInt(r['matchesPlayed']),
    );
    final careerMatches = maxOf([
      asInt(validHistory?['careerMatches']),
      asInt(player['careerMatches']),
      asInt(careerRow?['matchesPlayed']),
      rowsMatches,
    ]);

    final seasonRows = rows
        .where((r) => asInt(r['season']) == currentSeason)
        .toList();
    final rowsSeasonGoals = seasonRows.fold<int>(
      0,
      (sum, r) => sum + asInt(r['goalsScored']),
    );
    // Player seasonGoals already includes the cup subset.
    final dtoSeasonGoals = maxOf([
      asInt(player['seasonGoals']),
      asInt(player['cupGoals']),
    ]);
    final seasonGoals = maxOf([
      asInt(validHistory?['seasonGoals']),
      dtoSeasonGoals,
      rowsSeasonGoals,
    ]);

    final rowsSeasonAssists = seasonRows.fold<int>(
      0,
      (sum, r) => sum + asInt(r['assists']),
    );
    final seasonAssists = maxOf([
      asInt(validHistory?['seasonAssists']),
      asInt(player['seasonAssists']),
      rowsSeasonAssists,
    ]);

    int mapDivisionStat(dynamic rawMap, String tier) {
      if (rawMap is! Map || tier.isEmpty) return 0;
      for (final entry in rawMap.entries) {
        if (_normalizeClassicDivisionTier(entry.key) == tier) {
          return asInt(entry.value);
        }
      }
      return 0;
    }

    final divisionRows = divisionTier.isEmpty
        ? const <Map<String, dynamic>>[]
        : rows
              .where(
                (r) =>
                    _isClassicLeagueCompetition(r['competitionType']) &&
                    _normalizeClassicDivisionTier(r['division']) ==
                        divisionTier,
              )
              .toList();
    final rowsDivisionGoals = divisionRows.fold<int>(
      0,
      (sum, r) => sum + asInt(r['goalsScored']),
    );
    final rowsDivisionAssists = divisionRows.fold<int>(
      0,
      (sum, r) => sum + asInt(r['assists']),
    );

    final playerTier = _normalizeClassicDivisionTier(
      player['currentDivisionTier'] ?? player['currentDivisionDisplay'],
    );
    final historyTier = _normalizeClassicDivisionTier(
      validHistory?['currentDivisionTier'] ??
          validHistory?['currentDivisionDisplay'],
    );

    final currentDivisionGoals = maxOf([
      mapDivisionStat(validHistory?['divisionGoals'], divisionTier),
      mapDivisionStat(player['divisionGoals'], divisionTier),
      if (historyTier == divisionTier)
        asInt(validHistory?['currentDivisionGoals']),
      if (playerTier == divisionTier) asInt(player['currentDivisionGoals']),
      rowsDivisionGoals,
    ]);

    final currentDivisionAssists = maxOf([
      mapDivisionStat(validHistory?['divisionAssists'], divisionTier),
      mapDivisionStat(player['divisionAssists'], divisionTier),
      if (historyTier == divisionTier)
        asInt(validHistory?['currentDivisionAssists']),
      if (playerTier == divisionTier) asInt(player['currentDivisionAssists']),
      rowsDivisionAssists,
    ]);

    final rowsCareerGoals = rows.fold<int>(
      0,
      (sum, r) => sum + asInt(r['goalsScored']),
    );
    final careerGoals = maxOf([
      asInt(validHistory?['careerGoals']),
      asInt(player['careerGoals']),
      asInt(careerRow?['goalsScored']),
      rowsCareerGoals,
    ]);

    final rowsCareerAssists = rows.fold<int>(
      0,
      (sum, r) => sum + asInt(r['assists']),
    );
    final careerAssists = maxOf([
      asInt(validHistory?['careerAssists']),
      asInt(player['careerAssists']),
      asInt(careerRow?['assists']),
      rowsCareerAssists,
    ]);

    final rowsRedCards = rows.fold<int>(
      0,
      (sum, r) => sum + asInt(r['redCards']),
    );
    final cardHistoryReds =
        (validHistory?['cardHistory'] as List? ??
                player['cardHistory'] as List? ??
                const [])
            .whereType<Map>()
            .where((c) {
              final card = '${c['card'] ?? c['eventType'] ?? ''}';
              return card == 'DirectRedCard' ||
                  card == 'SecondYellowRedCard' ||
                  card == '2' ||
                  card == '3';
            })
            .length;
    final disciplineReds =
        asInt(player['totalDirectRedCards']) +
        asInt(player['totalSecondYellowDismissals']) +
        asInt(player['unclassifiedHistoricalRedCards']);
    final careerRedCards = maxOf([
      asInt(validHistory?['careerRedCards']),
      asInt(player['careerRedCards']),
      asInt(careerRow?['redCards']),
      rowsRedCards,
      cardHistoryReds,
      disciplineReds,
      asInt(player['seasonReds']),
    ]);

    final rowsInjuries = rows.fold<int>(
      0,
      (sum, r) => sum + asInt(r['injuries']),
    );
    final forceInjuriesCount = (player['forceInjuries'] as List?)?.length ?? 0;
    final careerInjuries = maxOf([
      asInt(validHistory?['careerInjuries']),
      asInt(player['careerInjuries']),
      rowsInjuries,
      forceInjuriesCount,
    ]);

    return [
      ('Jogos (carreira):', careerMatches),
      ('Gols (temporada):', seasonGoals),
      ('Assistências (temporada):', seasonAssists),
      ('Gols na $divisionDisplay:', currentDivisionGoals),
      ('Assistências na $divisionDisplay:', currentDivisionAssists),
      ('Gols (total):', careerGoals),
      ('Assistências (total):', careerAssists),
      ('Cartões vermelhos:', careerRedCards),
      ('Lesões (total):', careerInjuries),
    ];
  }

  Widget _retroPlayerDataPanel({bool mobile = false}) {
    final player = _retroSelected;
    return RetroSection(
      key: const Key('retro-player-data-panel'),
      title: 'Dados do Jogador',
      padding: EdgeInsets.all(mobile ? 7 : 8),
      child: player == null
          ? Container(
              key: const Key('retro-player-data-empty'),
              padding: EdgeInsets.symmetric(
                horizontal: mobile ? 8 : 10,
                vertical: mobile ? 10 : 12,
              ),
              decoration: BoxDecoration(
                color: ClassicColors.creamBox,
                border: Border.all(color: ClassicColors.border, width: 1.2),
              ),
              child: Text(
                'Selecione um jogador para ver seus dados.',
                style: TextStyle(
                  fontSize: mobile ? 14 : 15,
                  fontWeight: FontWeight.w600,
                  color: ClassicColors.rosterBlack,
                ),
              ),
            )
          : Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Container(
                  padding: EdgeInsets.symmetric(
                    horizontal: mobile ? 8 : 9,
                    vertical: mobile ? 5 : 6,
                  ),
                  decoration: BoxDecoration(
                    color: ClassicColors.creamBox,
                    border: Border.all(color: ClassicColors.border, width: 1.2),
                  ),
                  child: ClassicPlayerNameText(
                    formatClassicPlayerName(player),
                    key: const Key('classic-player-data-name'),
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: TextStyle(
                      fontSize: mobile ? 16.5 : 18.5,
                      fontWeight: FontWeight.w900,
                      color: ClassicColors.rosterBlack,
                      height: 1.1,
                    ),
                  ),
                ),
                SizedBox(height: mobile ? 3 : 4),
                for (final (idx, stat) in _retroPlayerPanelStats(
                  player,
                ).indexed)
                  Container(
                    key: ValueKey('classic-player-stat-row-$idx'),
                    padding: EdgeInsets.symmetric(
                      horizontal: 2,
                      vertical: mobile ? 2.5 : 2.5,
                    ),
                    decoration: const BoxDecoration(
                      border: Border(
                        bottom: BorderSide(
                          color: ClassicColors.border,
                          width: 0.8,
                        ),
                      ),
                    ),
                    child: Row(
                      children: [
                        Expanded(
                          child: Text(
                            stat.$1,
                            key: ValueKey('classic-player-stat-label-$idx'),
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                            style: TextStyle(
                              fontSize: mobile ? 13.5 : 14.5,
                              fontWeight: FontWeight.w700,
                              color: ClassicColors.rosterBlack,
                              height: 1.15,
                            ),
                          ),
                        ),
                        const SizedBox(width: 8),
                        Text(
                          '${stat.$2}',
                          key: ValueKey('classic-player-stat-value-$idx'),
                          textAlign: TextAlign.right,
                          style: TextStyle(
                            fontSize: mobile ? 14 : 15,
                            fontWeight: FontWeight.w900,
                            color: ClassicColors.rosterBlack,
                            height: 1.15,
                          ),
                        ),
                      ],
                    ),
                  ),
              ],
            ),
    );
  }

  void _retroOpen(String page, [int index = 0]) {
    if (_matchLocked) return;
    if (page == 'bank' || page == 'stadium') _statusBanner = null;
    if (page == 'market') _marketPreferences.discovery = null;
    _navigateToTab(index, classicPage: page);
  }

  String _retroCondition(Map p) {
    if ((p['injuryDaysRemaining'] as num? ?? 0) > 0 ||
        (p['injuryRoundsRemaining'] as num? ?? 0) > 0) {
      return 'Lesionado';
    }
    if ((p['suspensionRoundsRemaining'] as num? ?? 0) > 0) return 'Suspenso';
    return 'Apto';
  }

  String _retroBehavior(Map p) {
    final display = p['behaviorDisplay']?.toString().trim();
    if (display != null && display.isNotEmpty) return display;
    return switch (p['behavior']?.toString()) {
      'FairPlay' || '0' => 'Fair Play',
      'Cordeirinho' || '1' => 'Cordeirinho',
      'Cavalheiro' || '2' => 'Cavalheiro',
      'Caneleiro' || '3' => 'Caneleiro',
      'Caceteiro' || '4' => 'Caceteiro',
      'Sarrafeiro' || '5' => 'Sarrafeiro',
      _ => 'Cavalheiro',
    };
  }

  String _retroRole(Map p) => _retroCondition(p) != 'Apto'
      ? '!'
      : _selectedStarterIds.contains('${p['id']}')
      ? '●'
      : _selectedBenchIds.contains('${p['id']}')
      ? '–'
      : '·';

  Widget _retroRoleControl(Map<String, dynamic> player, {bool mobile = false}) {
    final id = '${player['id']}';
    final starter = _selectedStarterIds.contains(id),
        reserve = _selectedBenchIds.contains(id);
    final available = _retroCondition(player) == 'Apto';
    final isSelected = id == _classicSelectedPlayerId;
    final role = !available
        ? _retroCondition(player)
        : starter
        ? 'Titular'
        : reserve
        ? 'Reserva'
        : 'Fora dos relacionados';
    return Row(
      children: [
        IconButton(
          key: ValueKey('retro-role-$id'),
          tooltip:
              '${formatClassicPlayerName(player)}: $role — clique para ${starter
                  ? 'Reserva'
                  : reserve
                  ? 'Fora dos relacionados'
                  : 'Titular'}',
          onPressed: !_tacticsLocked && !_commandBusy && available
              ? () => _retroCycleRole(player)
              : null,
          padding: EdgeInsets.zero,
          constraints: BoxConstraints(
            minWidth: mobile ? 26 : 34,
            minHeight: mobile ? 28 : 34,
          ),
          icon: SizedBox(
            width: mobile ? 26 : 34,
            height: mobile ? 28 : 34,
            child: Icon(
              !available
                  ? Icons.block
                  : starter
                  ? Icons.circle
                  : reserve
                  ? Icons.remove_circle_outline
                  : Icons.radio_button_unchecked,
              size: mobile ? 12 : 14,
              color: !available
                  ? const Color(0xFFFF5252)
                  : starter
                  ? ClassicColors.starterGreen
                  : reserve
                  ? ClassicColors.softGold
                  : const Color(0xFF9AA8A1),
            ),
          ),
        ),
        Flexible(
          child: Text(
            _formatPosition(player['position']),
            overflow: TextOverflow.clip,
            style: TextStyle(
              color: isSelected
                  ? ClassicColors.softGold
                  : ClassicColors.rosterText,
              fontWeight: FontWeight.bold,
              fontSize: mobile ? 14 : 15.5,
            ),
          ),
        ),
      ],
    );
  }

  void _retroCycleRole(Map<String, dynamic> player) {
    if (_tacticsLocked || _commandBusy || _retroCondition(player) != 'Apto') {
      return;
    }
    final id = '${player['id']}';
    final starter = _selectedStarterIds.contains(id),
        reserve = _selectedBenchIds.contains(id);
    final role = starter
        ? 'reserve'
        : reserve
        ? 'out'
        : 'starter';
    setState(() {
      _selectedStarterIds.remove(id);
      _selectedBenchIds.remove(id);
      if (role == 'starter') _selectedStarterIds.add(id);
      if (role == 'reserve') _selectedBenchIds.add(id);
      _classicSelectedPlayerId = id;
      final formation = formationFromPlayers(
        _retroSquad
            .where((p) => _selectedStarterIds.contains('${p['id']}'))
            .toList(),
        _selectedFormation,
      );
      if (formation != '5-0-5' || _myClubDetail?['allowClassic505'] != false) {
        _selectedFormation = formation;
      }
      _statusBanner =
          '${formatClassicPlayerName(player)}: ${role == 'starter'
              ? 'titular'
              : role == 'reserve'
              ? 'reserva'
              : 'fora dos relacionados'}. ${_tacticsSelectionError ?? 'Escalação pronta para enviar.'}';
    });
    _selectClassicPlayer(id);
  }

  List<(String, VoidCallback?)> _retroMenu(String menu) {
    final locked = _matchLocked;
    final player = _retroSelected;
    final canManage =
        !_commandBusy &&
        (!_tacticsLocked || _snapshot?['roundState'] == 'RESULTS');
    VoidCallback? nav(String page, [int index = 0]) =>
        locked ? null : () => _retroOpen(page, index);
    switch (menu) {
      case 'Configurações':
        return [
          (
            'Aparência...',
            locked
                ? null
                : () => setState(() {
                    _classicPage = null;
                    _appearanceOpen = true;
                  }),
          ),
          ('Trocar sala...', locked ? null : _switchRoom),
          ('Voltar ao lobby...', locked ? null : _switchRoom),
          ('Ajuda...', locked ? null : () => _retroHelp()),
        ];
      case 'Táticas':
        return [
          for (final shape in availableFormations)
            (shape, _tacticsLocked ? null : () => _changeFormation(shape)),
          ('Automático (A)', _tacticsLocked ? null : _autoPickBestLineup),
          (
            'Melhores (M)',
            _tacticsLocked ? null : () => _pickBestSquad('strength'),
          ),
          ('Abrir táticas...', nav('tactics')),
        ];
      case 'Equipa':
        return [
          ('Finanças...', nav('finances', 3)),
          (
            'História do clube...',
            locked || _myClubId == null
                ? null
                : () => _openClubHistory(_myClubId!),
          ),
        ];
      case 'Jogador':
        return [
          ('Informações...', player == null ? null : nav('player')),
          ('Estatísticas...', player == null ? null : nav('statistics')),
          (
            'Mercado...',
            locked || player == null || !canManage
                ? null
                : () => _announceSquadPlayer(player),
          ),
          ('Contrato...', player == null ? null : nav('contract')),
          (
            'VER HISTÓRICO',
            locked || player == null
                ? null
                : () => showPlayerHistoryDialog(
                    context: context,
                    apiClient: widget.apiClient,
                    worldId: _worldId!,
                    playerId: '${player['id']}',
                  ),
          ),
          (
            'Procurar jogadores...',
            locked
                ? null
                : () {
                    _retroOpen('market', 2);
                    setState(() => _marketPreferences.discovery = false);
                  },
          ),
          (
            'Jogadores observados...',
            locked
                ? null
                : () {
                    _retroOpen('market', 2);
                    setState(() => _marketPreferences.discovery = true);
                  },
          ),
        ];
      case 'Campeonato':
        return [
          ('Classificação...', nav('standings', 1)),
          ('Calendário...', nav('calendar', 5)),
          ('Resultados...', nav('results', 5)),
          ('Taça Nacional...', nav('cup', 1)),
          for (final category in [
            ('scorers', 'Artilheiros...'),
            ('assists', 'Assistências...'),
            ('goalkeepers', 'Goleiros...'),
          ])
            (
              category.$2,
              locked
                  ? null
                  : () {
                      setState(() => _classicRankingCategory = category.$1);
                      _retroOpen('playerRankings', 6);
                    },
            ),
          ('Rankings de Jogadores...', nav('playerRankings', 6)),
        ];
      case 'Treinador':
        return [
          ('História...', nav('career', 4)),
          ('Ranking...', nav('coachRanking', 4)),
          ('Perfil...', nav('profile', 4)),
          ('Gestão de treinadores — Em breve', null),
        ];
      default:
        return [];
    }
  }

  Future<void> _retroHelp() => showDialog<void>(
    context: context,
    builder: (ctx) => AlertDialog(
      backgroundColor: ClassicColors.cream,
      titlePadding: EdgeInsets.zero,
      title: const RetroTitle(title: 'Ajuda — VERSÃO CLÁSSICA (ANTIGA)'),
      content: const Text(
        'Selecione um atleta para usar o menu Jogador. ● indica titular; – indica banco; · indica fora dos relacionados; ! indica indisponível.\n\nTáticas e mercado usam as mesmas regras da VERSÃO ATUAL. A rodada é sincronizada pela sala. Altere o visual em Configurações > Aparência.',
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.pop(ctx),
          child: const Text('FECHAR'),
        ),
      ],
    ),
  );

  Widget _retroMenuButton(
    String title, {
    String? displayLabel,
    bool richLabel = false,
    bool compact = false,
  }) {
    final button = Container(
      padding: EdgeInsets.symmetric(
        horizontal: compact ? 8 : 14,
        vertical: compact ? 6 : 7,
      ),
      decoration: const BoxDecoration(
        color: ClassicColors.retroGray,
        border: Border(
          top: BorderSide(color: ClassicColors.bevelLight, width: 1.5),
          left: BorderSide(color: ClassicColors.bevelLight, width: 1.5),
          bottom: BorderSide(color: ClassicColors.bevelDark, width: 1.5),
          right: BorderSide(color: ClassicColors.bevelDark, width: 1.5),
        ),
      ),
      child: richLabel
          ? RichText(
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              text: TextSpan(
                text: displayLabel ?? title,
                style: ClassicTypography.menuLabel.copyWith(
                  fontSize: compact ? 13.5 : 16,
                ),
              ),
            )
          : Text(
              displayLabel ?? title,
              maxLines: 1,
              overflow: TextOverflow.ellipsis,
              style: ClassicTypography.menuLabel.copyWith(
                fontSize: compact ? 13.5 : 16,
              ),
            ),
    );
    if (title == 'Jogo') {
      return InkWell(
        onTap: _matchLocked ? null : () => _retroOpen('squad'),
        child: button,
      );
    }
    return PopupMenuButton<int>(
      tooltip: '',
      position: PopupMenuPosition.under,
      menuPadding: EdgeInsets.zero,
      enabled: !_matchLocked || title == 'Campeonato',
      onSelected: (i) => _retroMenu(title)[i].$2?.call(),
      itemBuilder: (_) => [
        for (final (i, entry) in _retroMenu(title).indexed)
          PopupMenuItem(
            value: i,
            enabled: entry.$2 != null,
            height: 36,
            child: Text(entry.$1),
          ),
      ],
      child: button,
    );
  }

  Future<void> _retroMobileMenu(BuildContext context) =>
      showModalBottomSheet<void>(
        context: context,
        isScrollControlled: true,
        backgroundColor: ClassicColors.cream,
        builder: (ctx) => SafeArea(
          child: SizedBox(
            height: MediaQuery.sizeOf(ctx).height * .8,
            child: ListView(
              children: [
                const RetroTitle(title: 'Menus — Liga Clássica 98'),
                for (final title in [
                  'Configurações',
                  'Táticas',
                  'Equipa',
                  'Jogador',
                  'Campeonato',
                  'Treinador',
                ])
                  ExpansionTile(
                    title: Text(
                      title,
                      style: const TextStyle(
                        fontWeight: FontWeight.bold,
                        color: ClassicColors.rosterBlack,
                      ),
                    ),
                    children: [
                      for (final entry in _retroMenu(title))
                        ListTile(
                          dense: true,
                          enabled: entry.$2 != null,
                          title: Text(
                            entry.$1,
                            style: TextStyle(
                              color: entry.$2 != null
                                  ? ClassicColors.rosterBlack
                                  : const Color(0xFF777777),
                            ),
                          ),
                          onTap: entry.$2 == null
                              ? null
                              : () {
                                  Navigator.pop(ctx);
                                  entry.$2!();
                                },
                        ),
                    ],
                  ),
              ],
            ),
          ),
        ),
      );

  Widget _buildClassic98Shell(BuildContext retroContext) => LayoutBuilder(
    builder: (ctx, size) {
      final mobile = size.maxWidth < 1024;
      final clubName =
          _myClubDetail?['name']?.toString() ??
          (_myClubId == null ? 'Treinador sem clube' : 'Carregando clube...');
      final windowContent = RetroBevel(
        color: ClassicColors.retroGray,
        padding: const EdgeInsets.all(3),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            RetroTitle(
              backgroundColor: _matchLocked
                  ? ClassicColors.titleBarBlue
                  : ClassicColors.titleBarBlue,
              title: mobile
                  ? 'LIGA CLÁSSICA 98'
                  : 'LIGA CLÁSSICA 98 — $clubName',
              subtitle: mobile ? clubName : null,
              showWindowControls: !mobile,
              trailing: Text(
                mobile
                    ? 'VERSÃO\nCLÁSSICA\n(ANTIGA)'
                    : 'VERSÃO CLÁSSICA (ANTIGA)',
                textAlign: TextAlign.right,
                style: TextStyle(
                  color: Colors.white,
                  fontSize: mobile ? 10 : 13,
                  height: 1.05,
                  fontWeight: FontWeight.bold,
                ),
              ),
            ),
            if (!mobile && !_matchLocked)
              Container(
                color: ClassicColors.retroGray,
                padding: const EdgeInsets.fromLTRB(2, 2, 2, 3),
                child: Wrap(
                  children: [
                    for (final title in [
                      'Configurações',
                      'Jogo',
                      'Táticas',
                      'Equipa',
                      'Jogador',
                      'Campeonato',
                      'Treinador',
                    ])
                      _retroMenuButton(title),
                  ],
                ),
              ),
            if (mobile && !_matchLocked)
              Container(
                color: ClassicColors.retroGray,
                padding: const EdgeInsets.fromLTRB(2, 2, 2, 3),
                child: Row(
                  children: [
                    Container(
                      decoration: const BoxDecoration(
                        color: ClassicColors.retroGray,
                        border: Border(
                          top: BorderSide(
                            color: ClassicColors.bevelLight,
                            width: 1.5,
                          ),
                          left: BorderSide(
                            color: ClassicColors.bevelLight,
                            width: 1.5,
                          ),
                          bottom: BorderSide(
                            color: ClassicColors.bevelDark,
                            width: 1.5,
                          ),
                          right: BorderSide(
                            color: ClassicColors.bevelDark,
                            width: 1.5,
                          ),
                        ),
                      ),
                      child: IconButton(
                        tooltip: 'Abrir menus',
                        constraints: const BoxConstraints(
                          minWidth: 34,
                          minHeight: 30,
                        ),
                        padding: const EdgeInsets.symmetric(
                          horizontal: 6,
                          vertical: 4,
                        ),
                        onPressed: _matchLocked
                            ? null
                            : () => _retroMobileMenu(ctx),
                        icon: const Icon(
                          Icons.menu,
                          size: 18,
                          color: ClassicColors.rosterBlack,
                        ),
                      ),
                    ),
                    for (final item in const [
                      ('Táticas', 'Táticas'),
                      ('Equipa', 'Equipa'),
                      ('Jogador', 'Jogador'),
                      ('Campeonato', 'Camp.'),
                      ('Treinador', 'Trein.'),
                    ])
                      Expanded(
                        child: _retroMenuButton(
                          item.$1,
                          displayLabel: item.$2,
                          richLabel: true,
                          compact: true,
                        ),
                      ),
                  ],
                ),
              ),
            if (_waitingForRound) _buildReadyWaitingPanel(),
            if (!_matchLocked &&
                (_showMatchView || _snapshot?['roundState'] == 'RESULTS'))
              Container(
                color: ClassicColors.creamBox,
                padding: const EdgeInsets.all(8),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Text(
                      _matchVisualStateLabel(
                        _snapshot?['roundState']?.toString() ?? 'PREPARING',
                        _snapshot?['activePauseWindow'] is Map
                            ? Map<String, dynamic>.from(
                                _snapshot!['activePauseWindow'],
                              )
                            : null,
                      ),
                      style: const TextStyle(
                        fontWeight: FontWeight.bold,
                        color: ClassicColors.rosterBlack,
                      ),
                    ),
                    _buildRoundPrimaryAction(),
                  ],
                ),
              ),
            if (_statusBanner != null)
              Container(
                color: ClassicColors.creamBox,
                padding: const EdgeInsets.symmetric(horizontal: 8),
                child: Row(
                  children: [
                    Expanded(
                      child: Text(
                        _statusBanner!,
                        style: const TextStyle(
                          fontSize: 13,
                          color: ClassicColors.rosterBlack,
                        ),
                      ),
                    ),
                    IconButton(
                      tooltip: 'Fechar aviso',
                      onPressed: () => setState(() => _statusBanner = null),
                      icon: const Icon(
                        Icons.close,
                        size: 18,
                        color: ClassicColors.rosterBlack,
                      ),
                    ),
                  ],
                ),
              ),
            Expanded(
              child: ColoredBox(
                color: ClassicColors.cream,
                child: _retroBody(ctx, mobile),
              ),
            ),
            if (!_matchLocked)
              Container(
                decoration: const BoxDecoration(
                  color: ClassicColors.retroGray,
                  border: Border(
                    top: BorderSide(color: ClassicColors.border, width: 1.2),
                  ),
                ),
                padding: const EdgeInsets.all(3),
                child: Row(
                  children: [
                    for (final (idx, entry)
                        in (mobile
                                ? [
                                    ('Jogo', Icons.sports_soccer, 'squad', 0),
                                    (
                                      'Táticas',
                                      Icons.sports_soccer_outlined,
                                      'tactics',
                                      0,
                                    ),
                                    ('Mercado', Icons.swap_horiz, 'market', 2),
                                    ('Banco', Icons.account_balance, 'bank', 3),
                                    (
                                      'Estádio',
                                      Icons.stadium_outlined,
                                      'stadium',
                                      3,
                                    ),
                                    ('História', Icons.menu_book, 'history', 0),
                                  ]
                                : [
                                    ('Jogo', Icons.sports_soccer, 'squad', 0),
                                    (
                                      'Táticas',
                                      Icons.sports_soccer_outlined,
                                      'tactics',
                                      0,
                                    ),
                                    (
                                      'Mercado & Leilão',
                                      Icons.swap_horiz,
                                      'market',
                                      2,
                                    ),
                                    ('Banco', Icons.account_balance, 'bank', 3),
                                    (
                                      'Estádio',
                                      Icons.stadium_outlined,
                                      'stadium',
                                      3,
                                    ),
                                    ('História', Icons.menu_book, 'history', 0),
                                  ])
                            .indexed) ...[
                      if (idx > 0) const SizedBox(width: 3),
                      Expanded(
                        child: RetroButton(
                          compact: mobile,
                          stackIconOnCompact: mobile,
                          label: entry.$1,
                          icon: entry.$2,
                          onPressed: entry.$3 == 'history'
                              ? (_myClubId == null || _matchLocked
                                    ? null
                                    : () => _openClubHistory(_myClubId!))
                              : () => _retroOpen(entry.$3, entry.$4),
                        ),
                      ),
                    ],
                  ],
                ),
              ),
          ],
        ),
      );
      return PopScope(
        canPop: !_matchLocked,
        child: Scaffold(
          backgroundColor: ClassicColors.outerGreen,
          body: MatchInteractionOverlay(
            interaction: _buildMatchInteraction(),
            child: SafeArea(
              child: Padding(
                padding: EdgeInsets.symmetric(
                  horizontal: mobile ? 4 : 20,
                  vertical: mobile ? 4 : 16,
                ),
                child: mobile
                    ? windowContent
                    : Center(
                        child: ConstrainedBox(
                          constraints: const BoxConstraints(maxWidth: 1180),
                          child: windowContent,
                        ),
                      ),
              ),
            ),
          ),
        ),
      );
    },
  );

  Widget _retroBody(BuildContext ctx, bool mobile) {
    if (_appearanceOpen && !_matchLocked) {
      return _retroAppearance();
    }
    if (_matchLocked || _showMatchView) {
      return _buildSharedScreenBody();
    }
    final page =
        _classicPage ??
        switch (_activeTabIndex) {
          0 => _mobileTacticsView ? 'tactics' : 'squad',
          1 => 'standings',
          2 => 'market',
          3 => 'stadium',
          4 => 'career',
          5 => 'calendar',
          _ => 'playerRankings',
        };
    switch (page) {
      case 'squad':
        return _whenRoomData({_RoomData.squad}, () => _retroHome(ctx, mobile));
      case 'tactics':
        return _whenRoomData({_RoomData.squad}, _retroTactics);
      case 'bench':
        return _whenRoomData({_RoomData.squad}, () => _retroBench(mobile));
      case 'bank':
        return _whenRoomData({
          _RoomData.finances,
          _RoomData.squad,
        }, _buildFinancesAndStadiumScreen);
      case 'player':
      case 'statistics':
      case 'contract':
        return _retroPlayer(page);
      case 'standings':
        return _retroStandings(mobile);
      case 'coachRanking':
        return _whenRoomData({_RoomData.ranking}, () => _retroCoaches(mobile));
      case 'profile':
        return _retroProfile();
      case 'career':
        return _whenRoomData({_RoomData.invites}, _retroCareer);
      case 'cup':
        return _whenRoomData({_RoomData.cup}, _retroCup);
      case 'calendar':
      case 'results':
        return _whenRoomData({
          _RoomData.calendar,
        }, () => _retroCalendar(page == 'results'));
      default:
        return _buildSharedScreenBody();
    }
  }

  Widget _retroAppearance() => SingleChildScrollView(
    padding: const EdgeInsets.all(12),
    child: Center(
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 540),
        child: RetroBevel(
          color: ClassicColors.cream,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const RetroTitle(title: 'Aparência'),
              Padding(
                padding: const EdgeInsets.all(12),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Row(
                      children: [
                        BackButton(
                          onPressed: () =>
                              setState(() => _appearanceOpen = false),
                        ),
                        const Expanded(
                          child: Text(
                            'APARÊNCIA',
                            style: TextStyle(
                              fontSize: 22,
                              fontWeight: FontWeight.bold,
                            ),
                          ),
                        ),
                      ],
                    ),
                    if (widget.presentation != null)
                      PresentationVersionSelector(
                        api: widget.apiClient,
                        controller: widget.presentation!,
                        title: 'ESCOLHA A APARÊNCIA:',
                      ),
                    const Text(
                      'A versão clássica aplica o tema oficial retrô em todas as telas da sessão. As cores dos clubes continuam preservadas nas partidas.',
                    ),
                    const SizedBox(height: 12),
                    RetroButton(
                      label: 'Voltar ao jogo',
                      onPressed: () => setState(() => _appearanceOpen = false),
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

  Widget _retroRoster(
    bool mobile, {
    List<Map<String, dynamic>>? players,
    bool bounded = false,
  }) {
    final squad = players ?? _retroSquad;
    final benchStartIndex = squad.indexWhere(
      (p) => !_selectedStarterIds.contains('${p['id']}'),
    );
    final sectionRow = benchStartIndex >= 0 ? benchStartIndex : squad.length;
    final table = RetroTable(
      darkRoster: true,
      dense: true,
      key: const Key('retro-roster'),
      headers: const [
        'POS',
        'JOGADOR',
        'FORÇA',
        'ESTILO',
        'SALÁRIO',
        'CONTRATO',
      ],
      sectionHeaders: {
        sectionRow: [
          'POS',
          'BANCO (${_selectedBenchIds.length}/9)',
          'FORÇA',
          'ESTILO',
          'SALÁRIO',
          'CONTRATO',
        ],
      },
      emptySectionMessage: 'Sem jogadores reservas no banco.',
      flex: mobile ? const [3, 7, 2, 4, 4, 4] : const [2, 6, 2, 3, 3, 3],
      centerAlignedColumns: const {2, 3, 5},
      rightAlignedColumns: const {4},
      leadingCells: {
        for (final (i, p) in squad.indexed)
          i: _retroRoleControl(p, mobile: mobile),
      },
      rows: [
        for (final p in squad)
          [
            '${_retroRole(p)} ${_formatPosition(p['position'])}',
            formatClassicPlayerName(p),
            '${p['strength']}',
            _retroBehavior(p),
            formatClassicMoney(p['salaryPerRound']),
            squadContractMark(p),
          ],
      ],
      selected: squad.indexWhere(
        (p) => '${p['id']}' == _classicSelectedPlayerId,
      ),
      onSelect: (i) => _selectClassicPlayer('${squad[i]['id']}'),
    );
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        _scrollRetroRoster(table, mobile: mobile),
        Container(
          color: ClassicColors.cream,
          padding: const EdgeInsets.fromLTRB(4, 6, 4, 2),
          child: Row(
            children: [
              const Icon(
                Icons.circle,
                size: 10,
                color: ClassicColors.starterGreen,
              ),
              const SizedBox(width: 5),
              Flexible(
                child: Text(
                  'Titular   – Banco   · Fora dos relacionados   ! Indisponível',
                  style: TextStyle(
                    color: ClassicColors.rosterBlack,
                    fontSize: mobile ? 12.5 : 14.5,
                    fontWeight: FontWeight.w600,
                  ),
                ),
              ),
            ],
          ),
        ),
        Padding(
          padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 2),
          child: Text(
            'Titulares: ${_selectedStarterIds.length}/$_requiredStarterCount • Banco: ${_selectedBenchIds.length}/9',
            key: const Key('retro-roster-counts'),
            style: TextStyle(
              color: ClassicColors.rosterBlack,
              fontSize: mobile ? 13 : 15,
              fontWeight: FontWeight.w600,
            ),
          ),
        ),
        if (_retroSelected != null)
          Padding(
            padding: const EdgeInsets.symmetric(vertical: 4),
            child: Wrap(
              spacing: 5,
              runSpacing: 4,
              children: [
                RetroButton(
                  label: 'Listar Jogador',
                  onPressed:
                      _commandBusy ||
                          (_tacticsLocked &&
                              _snapshot?['roundState'] != 'RESULTS')
                      ? null
                      : () => _announceSquadPlayer(_retroSelected!),
                ),
                RetroButton(
                  key: const Key('retro-renew-contract'),
                  label: 'Renovar contrato',
                  onPressed:
                      _contractNegotiationLocked ||
                          squadContractConfirmed(_retroSelected!)
                      ? null
                      : () => _negotiateSquadContract(_retroSelected!),
                ),
              ],
            ),
          ),
      ],
    );
  }

  Widget _scrollRetroRoster(Widget table, {required bool mobile}) =>
      LayoutBuilder(
        builder: (context, constraints) =>
            constraints.maxWidth >= (mobile ? 640 : 560)
            ? table
            : SingleChildScrollView(
                scrollDirection: Axis.horizontal,
                child: SizedBox(width: mobile ? 640 : 560, child: table),
              ),
      );

  Widget _retroHome(BuildContext ctx, bool mobile) {
    final club = _myClubDetail;
    final header = Container(
      padding: const EdgeInsets.only(bottom: 8),
      decoration: const BoxDecoration(
        border: Border(
          bottom: BorderSide(color: ClassicColors.border, width: 1),
        ),
      ),
      child: Row(
        children: [
          if (club != null)
            ClubCrest(
              club: club['name'].toString(),
              palette: clubMatchPalette(club),
              size: mobile ? 44 : 56,
            ),
          const SizedBox(width: 10),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  '${club?['name'] ?? 'Treinador sem clube'}',
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(
                    fontSize: mobile ? 20 : 28,
                    fontWeight: FontWeight.w900,
                    color: ClassicColors.rosterBlack,
                    height: 1.05,
                  ),
                ),
                Text(
                  '${club?['divisionDisplay'] ?? 'Sem clube'} • $_nickname',
                  style: TextStyle(
                    fontSize: mobile ? 13 : 16,
                    fontWeight: FontWeight.w600,
                    color: ClassicColors.rosterBlack,
                  ),
                ),
              ],
            ),
          ),
          Text(
            mobile
                ? 'Temp. ${_snapshot?['seasonNumber'] ?? 1}\n${formatClassicDate(_snapshot?['gameDate'])}'
                : 'Temporada ${_snapshot?['seasonNumber'] ?? 1}\n${formatClassicDate(_snapshot?['gameDate'])}',
            textAlign: TextAlign.right,
            style: TextStyle(
              fontSize: mobile ? 13 : 16,
              fontWeight: FontWeight.w600,
              color: ClassicColors.rosterBlack,
            ),
          ),
        ],
      ),
    );
    final roster = club == null
        ? const Padding(
            padding: EdgeInsets.all(16),
            child: Text(
              'Você está sem clube. Consulte a carreira para ver os convites disponíveis.',
            ),
          )
        : _retroRoster(mobile, bounded: true);
    return SingleChildScrollView(
      padding: EdgeInsets.all(mobile ? 8 : 12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          header,
          const SizedBox(height: 10),
          if (mobile) ...[
            roster,
            const SizedBox(height: 6),
            _retroPlayerDataPanel(mobile: true),
            _retroSummary(mobile: true),
          ] else
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Expanded(flex: 7, child: roster),
                const SizedBox(width: 14),
                Expanded(
                  flex: 4,
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [_retroPlayerDataPanel(), _retroSummary()],
                  ),
                ),
              ],
            ),
        ],
      ),
    );
  }

  Widget _retroSummary({bool mobile = false}) {
    final round = _snapshot?['roundNumber'] ?? 1;
    final club = _myClubDetail;
    final matches = (_snapshot?['matches'] as List? ?? []).whereType<Map>();
    final match = _myClubId == null
        ? null
        : matches
              .where(
                (m) =>
                    m['homeClubId'] == _myClubId ||
                    m['awayClubId'] == _myClubId,
              )
              .firstOrNull;
    final fixture =
        (_calendar
                        .where((r) => r['roundNumber'] == round)
                        .firstOrNull?['fixtures']
                    as List? ??
                [])
            .whereType<Map>()
            .where(
              (f) =>
                  club != null &&
                  (f['home'] == club['name'] || f['away'] == club['name']),
            )
            .firstOrNull;
    final home = match != null
        ? match['homeClubId'] == _myClubId
        : fixture != null && fixture['home'] == club?['name'];
    final opponent = match != null
        ? match[home ? 'awayClubName' : 'homeClubName']
        : fixture?[home ? 'away' : 'home'];
    final me = (_snapshot?['readyCounter']?['participants'] as List? ?? [])
        .whereType<Map>()
        .where((p) => p['coachId'] == _coachId)
        .firstOrNull;
    final ready = me?['isReady'] == true;
    final state = _snapshot?['roundState'];
    final morale = me?['morale'] ?? _invitationsData?['morale'];
    final cash = club?['cashBalance'];
    return RetroSection(
      key: const Key('retro-next-opponent-panel'),
      title: 'Próximo adversário',
      padding: EdgeInsets.all(mobile ? 8 : 8),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Container(
            padding: EdgeInsets.all(mobile ? 7 : 8),
            decoration: BoxDecoration(
              color: ClassicColors.creamBox,
              border: Border.all(color: ClassicColors.border, width: 1.2),
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  '${opponent ?? 'A definir'}',
                  style: TextStyle(
                    fontSize: mobile ? 18 : 20,
                    fontWeight: FontWeight.w900,
                    color: ClassicColors.rosterBlack,
                  ),
                ),
                Text(
                  '${opponent == null
                      ? 'Local a definir'
                      : home
                      ? 'Casa'
                      : 'Fora'} • Rodada $round',
                  style: TextStyle(
                    fontSize: mobile ? 13.5 : 14.5,
                    fontWeight: FontWeight.w600,
                    color: ClassicColors.rosterBlack,
                  ),
                ),
              ],
            ),
          ),
          SizedBox(height: mobile ? 5 : 6),
          Text(
            'Divisão: ${club?['divisionDisplay'] ?? 'Sem clube'}',
            style: TextStyle(
              fontSize: mobile ? 14.5 : 16,
              fontWeight: FontWeight.w600,
            ),
          ),
          SizedBox(height: mobile ? 2 : 3),
          Text(
            'Rodada: $round • ${classicCompetition(_snapshot?['competitionType'])}',
            style: TextStyle(
              fontSize: mobile ? 14 : 15,
              fontWeight: FontWeight.w600,
            ),
          ),
          SizedBox(height: mobile ? 4 : 5),
          Text(
            'Caixa: ${club == null ? '—' : formatClassicMoney(club['cashBalance'])}',
            style: TextStyle(
              fontSize: mobile ? 18 : 20,
              fontWeight: FontWeight.w900,
              color: cash is num
                  ? cash < 0
                        ? const Color(0xFFB61922)
                        : ClassicColors.actionGreen
                  : ClassicColors.rosterBlack,
            ),
          ),
          SizedBox(height: mobile ? 4 : 5),
          Container(
            decoration: BoxDecoration(
              color: ClassicColors.creamBox,
              border: Border.all(color: ClassicColors.bevelDark, width: 1.3),
            ),
            child: LinearProgressIndicator(
              value: morale is num ? (morale / 100).clamp(0, 1).toDouble() : 0,
              minHeight: mobile ? 12 : 14,
              color: ClassicColors.moraleGreen,
              backgroundColor: ClassicColors.creamBox,
            ),
          ),
          SizedBox(height: mobile ? 8 : 8),
          if (state != 'RESULTS')
            KeyedSubtree(
              key: const Key('retro-match-action'),
              child: RetroBevel(
                color: ClassicColors.actionGreen,
                padding: const EdgeInsets.all(2),
                child: SizedBox(
                  width: double.infinity,
                  child: _buildRoundPrimaryAction(),
                ),
              ),
            ),
          if (ready && state == 'PREPARING')
            const Padding(
              padding: EdgeInsets.only(top: 6),
              child: Text(
                'AGUARDANDO OUTROS TREINADORES.',
                style: TextStyle(fontWeight: FontWeight.bold),
              ),
            ),
          const SizedBox(height: 6),
          Text(
            'Sala: ${_snapshot?['worldName'] ?? _worldId} • ${_snapshot?['roundState'] ?? 'Carregando'}',
            style: const TextStyle(fontSize: 12, color: Color(0xFF42504A)),
          ),
        ],
      ),
    );
  }

  Widget _retroTactics() => SingleChildScrollView(
    padding: const EdgeInsets.all(12),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        RetroSection(
          title: 'TÁTICAS E FORMAÇÃO',
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              DropdownButtonFormField<String>(
                initialValue: _selectedFormation,
                decoration: const InputDecoration(labelText: 'Formação'),
                items: [
                  for (final f in availableFormations)
                    DropdownMenuItem(value: f, child: Text(f)),
                ],
                onChanged: _tacticsLocked
                    ? null
                    : (f) {
                        if (f != null) _changeFormation(f);
                      },
              ),
              const SizedBox(height: 10),
              Wrap(
                spacing: 6,
                runSpacing: 6,
                children: [
                  RetroButton(
                    label: 'Automático (A)',
                    onPressed: _tacticsLocked ? null : _autoPickBestLineup,
                  ),
                  RetroButton(
                    label: 'Melhores (M)',
                    onPressed: _tacticsLocked
                        ? null
                        : () => _pickBestSquad('strength'),
                  ),
                ],
              ),
              const SizedBox(height: 10),
              Center(
                child: ConstrainedBox(
                  constraints: const BoxConstraints(maxWidth: 420),
                  child: ClassicTacticalPitch(
                    lines: tacticalPitchLines(
                      _retroSquad
                          .where(
                            (p) => _selectedStarterIds.contains('${p['id']}'),
                          )
                          .map(
                            (p) => {
                              ...p,
                              'name': cleanClassicPlayerName(p),
                              'displayName': formatClassicPlayerName(p),
                              'position': _formatPosition(p['position']),
                            },
                          )
                          .toList(),
                      _selectedFormation,
                    ),
                    shirt: const Color(0xFFEEEEEE),
                    ink: Colors.black,
                    onPlayer: (p) => _selectClassicPlayer('${p['id']}'),
                  ),
                ),
              ),
            ],
          ),
        ),
        Text(
          'Titulares: ${_selectedStarterIds.length}/$_requiredStarterCount • Banco: ${_selectedBenchIds.length}/9',
          style: const TextStyle(fontWeight: FontWeight.bold),
        ),
        const SizedBox(height: 6),
        _retroRoster(MediaQuery.sizeOf(context).width < 768),
        if (_retroSelected != null)
          RetroButton(
            label: 'Alterar relação do jogador selecionado',
            onPressed: _tacticsLocked
                ? null
                : () => _retroCycleRole(_retroSelected!),
          ),
        const SizedBox(height: 8),
        RetroButton(
          label: 'Voltar ao jogo',
          onPressed: () => _retroOpen('squad'),
        ),
      ],
    ),
  );

  Future<void> _retroSwapReserve(Map<String, dynamic> reserve) async {
    if (_tacticsLocked) return;
    final starter = await showDialog<String>(
      context: context,
      builder: (ctx) => SimpleDialog(
        backgroundColor: ClassicColors.cream,
        title: const RetroTitle(title: 'Trocar com titular'),
        children: [
          for (final p in _retroSquad.where(
            (p) =>
                _selectedStarterIds.contains('${p['id']}') &&
                swapSquadPlayers(
                      _retroSquad,
                      _selectedStarterIds,
                      _selectedBenchIds,
                      '${p['id']}',
                      '${reserve['id']}',
                      _selectedFormation,
                    ) !=
                    null,
          ))
            SimpleDialogOption(
              onPressed: () => Navigator.pop(ctx, '${p['id']}'),
              child: Text(
                '${formatClassicPlayerName(p)} (${_formatPosition(p['position'])})',
              ),
            ),
        ],
      ),
    );
    if (mounted && starter != null && !_tacticsLocked) {
      _swapSquadPlayers(starter, '${reserve['id']}');
    }
  }

  Widget _retroBench(bool mobile) {
    final bench = _retroSquad
        .where((p) => _selectedBenchIds.contains('${p['id']}'))
        .toList();
    return SingleChildScrollView(
      padding: const EdgeInsets.all(12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const Text(
            'BANCO (RESERVAS)',
            style: TextStyle(fontSize: 22, fontWeight: FontWeight.bold),
          ),
          Text('${bench.length}/9 relacionados • selecione um reserva'),
          const SizedBox(height: 10),
          _scrollRetroRoster(
            RetroTable(
              darkRoster: true,
              headers: const [
                'POS',
                'JOGADOR',
                'FORÇA',
                'ESTILO',
                'SALÁRIO',
                'CONTRATO',
              ],
              flex: mobile
                  ? const [2, 6, 2, 4, 4, 4]
                  : const [1, 6, 2, 3, 3, 3],
              centerAlignedColumns: const {2, 3, 5},
              rightAlignedColumns: const {4},
              rows: [
                for (final p in bench)
                  [
                    _formatPosition(p['position']),
                    formatClassicPlayerName(p),
                    '${p['strength']}',
                    _retroBehavior(p),
                    formatClassicMoney(p['salaryPerRound']),
                    squadContractMark(p),
                  ],
              ],
              selected: bench.indexWhere(
                (p) => '${p['id']}' == _classicSelectedPlayerId,
              ),
              onSelect: (i) => _selectClassicPlayer('${bench[i]['id']}'),
            ),
            mobile: mobile,
          ),
          const SizedBox(height: 10),
          if (_retroSelected != null &&
              _selectedBenchIds.contains(_classicSelectedPlayerId))
            Wrap(
              spacing: 8,
              runSpacing: 8,
              children: [
                RetroButton(
                  label: 'Promover / trocar com titular',
                  onPressed:
                      _tacticsLocked ||
                          _retroCondition(_retroSelected!) != 'Apto'
                      ? null
                      : () => _retroSwapReserve(_retroSelected!),
                ),
                RetroButton(
                  label: 'Retirar do banco',
                  onPressed: _tacticsLocked
                      ? null
                      : () => _toggleSquadPlayer(_retroSelected!),
                ),
              ],
            ),
          const SizedBox(height: 8),
          RetroButton(label: 'Voltar', onPressed: () => _retroOpen('squad')),
        ],
      ),
    );
  }

  Widget _retroPlayer(String page) {
    final p = _retroSelected;
    if (p == null) {
      return const Center(child: Text('Selecione um jogador no plantel.'));
    }
    return SingleChildScrollView(
      padding: const EdgeInsets.all(12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          ClassicPlayerNameText(
            formatClassicPlayerName(p),
            style: const TextStyle(fontSize: 23, fontWeight: FontWeight.bold),
          ),
          const SizedBox(height: 10),
          Wrap(
            spacing: 5,
            runSpacing: 5,
            children: [
              for (final tab in [
                ('player', 'Informações'),
                ('statistics', 'Estatísticas'),
                ('contract', 'Contrato'),
              ])
                RetroButton(label: tab.$2, onPressed: () => _retroOpen(tab.$1)),
            ],
          ),
          const SizedBox(height: 12),
          RetroSection(
            title: page == 'statistics'
                ? 'Estatísticas disponíveis'
                : page == 'contract'
                ? 'Contrato'
                : 'Informações do jogador',
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text('Clube: ${_myClubDetail?['name']}'),
                Text('Posição: ${_formatPosition(p['position'])}'),
                Text('Força: ${p['strength']}'),
                Text('Estilo: ${_retroBehavior(p)}'),
                Text('Condição: ${_retroCondition(p)}'),
                Text(
                  'Salário por partida: ${formatClassicMoney(p['salaryPerRound'])}',
                ),
                Text('Valor justo: ${formatClassicMoney(p['fairValue'])}'),
                Text(
                  'Contrato: ${p['contractRenewedForSeason'] == true
                      ? 'até o fim da temporada ${p['contractThroughSeason'] ?? '—'}'
                      : p['salaryRaiseAcceptedForSeason'] == true
                      ? '✓ Aumento salarial aceito nesta temporada'
                      : '${p['contractRoundsRemaining'] ?? '—'} rodadas'}',
                ),
                if (page == 'statistics') ...[
                  Text('Gols na temporada: ${p['seasonGoals'] ?? '—'}'),
                  Text('Gols na carreira: ${p['careerGoals'] ?? '—'}'),
                  Text('Partidas na temporada: ${p['seasonMatches'] ?? '—'}'),
                  Text('Partidas na carreira: ${p['careerMatches'] ?? '—'}'),
                ],
              ],
            ),
          ),
          if (page == 'contract')
            RetroButton(
              label: 'Consultar renovação...',
              onPressed:
                  _commandBusy ||
                      (_tacticsLocked &&
                          _snapshot?['roundState'] != 'RESULTS') ||
                      squadContractConfirmed(p)
                  ? null
                  : () => _renewSquadPlayer(p),
            ),
          const SizedBox(height: 6),
          RetroButton(
            label: 'Mercado / anunciar...',
            onPressed:
                _commandBusy ||
                    (_tacticsLocked && _snapshot?['roundState'] != 'RESULTS')
                ? null
                : () => _announceSquadPlayer(p),
          ),
          const SizedBox(height: 8),
          RetroButton(label: 'Voltar', onPressed: () => _retroOpen('squad')),
        ],
      ),
    );
  }

  Widget _retroStandings(bool mobile) => SingleChildScrollView(
    padding: const EdgeInsets.all(10),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        const Text(
          'CLASSIFICAÇÃO',
          style: TextStyle(fontSize: 22, fontWeight: FontWeight.bold),
        ),
        const SizedBox(height: 6),
        Wrap(
          spacing: 4,
          runSpacing: 4,
          children: [
            for (final d in [
              ('PrimeiraDivisao', '1ª'),
              ('SegundaDivisao', '2ª'),
              ('TerceiraDivisao', '3ª'),
              ('QuartaDivisao', '4ª'),
            ])
              RetroButton(
                backgroundColor: _selectedDivisionTier == d.$1
                    ? ClassicColors.titleBarBlue
                    : ClassicColors.actionGreen,
                label: '${d.$2} Divisão',
                onPressed: () => _selectDivision(d.$1),
              ),
          ],
        ),
        const SizedBox(height: 10),
        _whenRoomData(
          {_RoomData.standings},
          () => RetroTable(
            selected: _standings.indexWhere(
              (r) => r is Map && r['clubId'] == _myClubId,
            ),
            onSelect: _matchLocked
                ? null
                : (i) => _retroStandingDetails(_standings[i] as Map),
            headers: mobile
                ? ['#', 'CLUBE', 'PTS', 'J', 'SG']
                : ['#', 'CLUBE', 'PTS', 'J', 'V', 'E', 'D', 'GP', 'GC', 'SG'],
            flex: mobile ? [1, 6, 1, 1, 1] : [1, 8, 1, 1, 1, 1, 1, 1, 1, 1],
            rows: [
              for (final r in _standings)
                [
                  '${r['position']}',
                  '${r['clubName']}',
                  '${r['points']}',
                  '${r['played'] ?? r['playedMatches'] ?? '—'}',
                  if (!mobile) ...[
                    '${r['wins']}',
                    '${r['draws']}',
                    '${r['losses']}',
                    '${r['goalsFor']}',
                    '${r['goalsAgainst']}',
                  ],
                  '${r['goalDifference']}',
                ],
            ],
          ),
          message: _holdingClassification ? 'Atualizando resultados...' : null,
        ),
        if (mobile)
          const Padding(
            padding: EdgeInsets.only(top: 6),
            child: Text(
              'Toque no clube para ver vitórias, empates, derrotas e gols.',
            ),
          ),
      ],
    ),
  );

  Future<void> _retroStandingDetails(Map row) => showDialog<void>(
    context: context,
    builder: (ctx) => AlertDialog(
      backgroundColor: ClassicColors.cream,
      titlePadding: EdgeInsets.zero,
      title: RetroTitle(title: '${row['clubName']}'),
      content: Column(
        mainAxisSize: MainAxisSize.min,
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          for (final field in [
            ('points', 'Pontos'),
            ('played', 'Jogos'),
            ('wins', 'Vitórias'),
            ('draws', 'Empates'),
            ('losses', 'Derrotas'),
            ('goalsFor', 'Gols pró'),
            ('goalsAgainst', 'Gols contra'),
            ('goalDifference', 'Saldo'),
          ])
            Text('${field.$2}: ${row[field.$1] ?? '—'}'),
        ],
      ),
      actions: [
        if (row['clubId'] != null)
          TextButton(
            onPressed: _matchLocked
                ? null
                : () {
                    Navigator.pop(ctx);
                    _openClubHistory('${row['clubId']}');
                  },
            child: const Text('HISTÓRIA DO CLUBE'),
          ),
        TextButton(
          onPressed: () => Navigator.pop(ctx),
          child: const Text('FECHAR'),
        ),
      ],
    ),
  );

  Widget _retroCareer() => SingleChildScrollView(
    padding: const EdgeInsets.all(12),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        const Text(
          'HISTÓRIA DO TREINADOR',
          style: TextStyle(fontSize: 22, fontWeight: FontWeight.bold),
        ),
        Text('$_nickname • ${_myClubDetail?['name'] ?? 'Sem clube'}'),
        const SizedBox(height: 12),
        RetroSection(
          title: 'Histórico de clubes',
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              if ((_invitationsData?['employmentHistory'] as List? ?? [])
                  .isEmpty)
                const Text('Histórico ainda não disponível.'),
              for (final h
                  in _invitationsData?['employmentHistory'] as List? ?? [])
                Text(
                  '${h['clubName']} • T${h['startSeason']} R${h['startRound']} — ${h['endSeason'] == null ? 'Atual' : 'T${h['endSeason']} R${h['endRound']}'}',
                ),
            ],
          ),
        ),
        RetroSection(
          title: 'Convites',
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              if ((_invitationsData?['invitations'] as List? ?? []).isEmpty)
                const Text('Nenhum convite disponível.'),
              for (final inv
                  in _invitationsData?['invitations'] as List? ?? []) ...[
                Text('${inv['clubName']} • ${inv['divisionDisplay']}'),
                const Text('Aceitar transfere seu comando para este clube.'),
                if (inv['isAccepted'] != true)
                  Wrap(
                    spacing: 8,
                    children: [
                      RetroButton(
                        label: 'Aceitar',
                        onPressed: _commandBusy
                            ? null
                            : () => _respondCareerInvitation(inv as Map, true),
                      ),
                      RetroButton(
                        label: 'Recusar',
                        onPressed: _commandBusy
                            ? null
                            : () => _respondCareerInvitation(inv as Map, false),
                      ),
                    ],
                  ),
              ],
            ],
          ),
        ),
      ],
    ),
  );

  Widget _retroCoaches(bool mobile) {
    final rows = (_rankingData?['ranking'] as List? ?? [])
        .whereType<Map>()
        .toList();
    return SingleChildScrollView(
      padding: const EdgeInsets.all(10),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const Text(
            'RANKING DE TREINADORES',
            style: TextStyle(fontSize: 22, fontWeight: FontWeight.bold),
          ),
          const Text(
            'Classificação oficial: campeonatos, taças e pontos de treinador.',
          ),
          const SizedBox(height: 10),
          RetroTable(
            headers: mobile
                ? ['#', 'TREINADOR / CLUBE', 'C / T', 'PTS']
                : ['#', 'TREINADOR', 'CLUBE', 'TIPO', 'CAMP.', 'TAÇAS', 'PTS'],
            flex: mobile ? [1, 6, 2, 2] : [1, 4, 5, 2, 2, 2, 2],
            cyanRows: {
              for (var i = 0; i < rows.length; i++)
                if (rows[i]['isHuman'] == true) i,
            },
            rows: [
              for (final r in rows)
                [
                  '${r['rank'] ?? '—'}',
                  '${r['coachName'] ?? r['name'] ?? '—'}${mobile ? '\n${r['currentClubName'] ?? 'Sem clube'}${r['isHuman'] == true ? ' • HUMANO' : ' • IA'}' : ''}',
                  if (!mobile) ...[
                    '${r['currentClubName'] ?? 'Sem clube'}',
                    r['isHuman'] == true ? 'HUMANO' : 'IA',
                  ],
                  if (mobile)
                    '${r['leagueTitles'] ?? 0} / ${r['cupTitles'] ?? 0}'
                  else ...[
                    '${r['leagueTitles'] ?? 0}',
                    '${r['cupTitles'] ?? 0}',
                  ],
                  '${r['coachPoints'] ?? 0}',
                ],
            ],
            onSelect: (i) => _showCoachCareerDetailDialog(
              Map<String, dynamic>.from(rows[i]),
            ),
          ),
        ],
      ),
    );
  }

  Widget _retroProfile() => SingleChildScrollView(
    padding: const EdgeInsets.all(12),
    child: RetroSection(
      title: 'Perfil do treinador',
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(_nickname, style: const TextStyle(fontWeight: FontWeight.bold)),
          Text('Clube: ${_myClubDetail?['name'] ?? 'Sem clube'}'),
          const Text('Tipo: HUMANO'),
          const SizedBox(height: 8),
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: [
              RetroButton(
                label: 'História / carreira',
                onPressed: () => _retroOpen('career', 4),
              ),
              RetroButton(
                label: 'Ranking',
                onPressed: () => _retroOpen('coachRanking', 4),
              ),
            ],
          ),
        ],
      ),
    ),
  );

  Widget _retroCup() => SingleChildScrollView(
    padding: const EdgeInsets.all(10),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        const Text(
          'TAÇA NACIONAL',
          style: TextStyle(fontSize: 22, fontWeight: FontWeight.bold),
        ),
        const SizedBox(height: 8),
        RetroTable(
          headers: const ['FASE', 'CONFRONTO', 'RESULTADO'],
          flex: const [2, 6, 2],
          rows: [
            for (final f in _cupFixtures)
              [
                '${f['stage'] ?? '—'}',
                '${f['homeClubName'] ?? f['home'] ?? 'A definir'}\nx ${f['awayClubName'] ?? f['away'] ?? 'A definir'}',
                f['isFinished'] == true || f['winnerClubName'] != null
                    ? '${f['homeGoals'] ?? '—'} : ${f['awayGoals'] ?? '—'}'
                    : 'A jogar',
              ],
          ],
        ),
      ],
    ),
  );

  Widget _retroCalendar(bool results) {
    bool belongsToClub(Map fixture) =>
        _myClubId != null &&
        (fixture['homeClubId'] == _myClubId ||
            fixture['awayClubId'] == _myClubId ||
            fixture['homeClubId'] == null &&
                fixture['awayClubId'] == null &&
                _myClubDetail != null &&
                (fixture['home'] == _myClubDetail!['name'] ||
                    fixture['away'] == _myClubDetail!['name']));
    final rounds = _calendar
        .map(
          (r) => {
            ...r as Map,
            'fixtures': (r['fixtures'] as List? ?? [])
                .where(
                  (f) => results
                      ? f['isFinished'] == true
                      : belongsToClub(f as Map),
                )
                .toList(),
          },
        )
        .where((r) => (r['fixtures'] as List).isNotEmpty)
        .toList();
    return SingleChildScrollView(
      padding: const EdgeInsets.all(10),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(
            results ? 'RESULTADOS' : 'CALENDÁRIO — MEU CLUBE',
            style: const TextStyle(fontSize: 22, fontWeight: FontWeight.bold),
          ),
          const SizedBox(height: 8),
          for (final r in rounds)
            if (!results ||
                (r['fixtures'] as List? ?? []).any(
                  (f) => f['isFinished'] == true,
                ))
              RetroSection(
                title:
                    'Rodada ${r['roundNumber']} • ${classicCompetition(r['competition'])}',
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    for (final f in r['fixtures'] as List? ?? [])
                      if (!results || f['isFinished'] == true)
                        Padding(
                          padding: const EdgeInsets.symmetric(vertical: 6),
                          child: Text(
                            '${f['home']}  ${f['isFinished'] == true ? '${f['homeGoals']} x ${f['awayGoals']}' : '×'}  ${f['away']}',
                            style: const TextStyle(fontWeight: FontWeight.w600),
                          ),
                        ),
                  ],
                ),
              ),
          if (results &&
              !rounds.any(
                (r) => (r['fixtures'] as List? ?? []).any(
                  (f) => f['isFinished'] == true,
                ),
              ))
            const Text('Nenhuma partida concluída.'),
          if (!results && rounds.isEmpty)
            const Text(
              'Nenhum jogo definido para a sua equipe. Os confrontos da taça aparecerão aqui quando forem definidos.',
            ),
        ],
      ),
    );
  }
}
