// ignore_for_file: invalid_use_of_protected_member
part of 'main.dart';

/// A presentation adapter over the SAME shell state, command methods and models.
/// Selecting a row has no game-side effect. No API mutation is implemented here.
extension _Classic98Shell on _ElifootShellScreenState {
  List<Map<String, dynamic>> get _retroSquad =>
      (_myClubDetail?['squad'] as List? ?? [])
          .whereType<Map>()
          .map((p) => Map<String, dynamic>.from(p))
          .toList()
        ..sort(
          (a, b) => const ['G', 'D', 'M', 'A']
              .indexOf(_formatPosition(a['position']))
              .compareTo(
                const [
                  'G',
                  'D',
                  'M',
                  'A',
                ].indexOf(_formatPosition(b['position'])),
              ),
        );

  Map<String, dynamic>? get _retroSelected => _retroSquad
      .where((p) => '${p['id']}' == _classicSelectedPlayerId)
      .firstOrNull;

  void _retroOpen(String page, [int index = 0]) {
    if (_matchLocked) return;
    _navigateToTab(index);
    setState(() {
      _classicPage = page;
      _mobileTacticsView = page == 'tactics';
    });
  }

  String _retroCondition(Map p) {
    if ((p['injuryDaysRemaining'] as num? ?? 0) > 0 ||
        (p['injuryRoundsRemaining'] as num? ?? 0) > 0) {
      return 'Lesionado';
    }
    if ((p['suspensionRoundsRemaining'] as num? ?? 0) > 0) return 'Suspenso';
    return 'Apto';
  }

  String _retroRole(Map p) => _retroCondition(p) != 'Apto'
      ? '!'
      : _selectedStarterIds.contains('${p['id']}')
      ? '●'
      : _selectedBenchIds.contains('${p['id']}')
      ? '–'
      : '·';

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
          ('Automática', _tacticsLocked ? null : _autoPickBestLineup),
          (
            'Melhores',
            _tacticsLocked ? null : () => _pickBestSquad('strength'),
          ),
          ('Abrir táticas...', nav('tactics')),
        ];
      case 'Equipa':
        return [
          ('Mercado & Leilão...', nav('market', 2)),
          ('Banco (Reservas)...', nav('bench')),
          ('Estádio...', nav('stadium', 3)),
          ('Finanças...', nav('finances', 3)),
          ('História — Em breve', null),
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
          ('Histórico — Em breve', null),
          ('Procurar jogadores...', nav('market', 2)),
        ];
      case 'Campeonato':
        return [
          ('Classificação...', nav('standings', 1)),
          ('Tabela de jogos...', nav('calendar', 5)),
          ('Calendário...', nav('calendar', 5)),
          ('Resultados...', nav('results', 5)),
          ('Taça Nacional...', nav('cup', 1)),
          for (final category in [
            ('scorers', 'Artilheiros...'),
            ('goalkeepers', 'Goleiros...'),
            ('defenders', 'Defensores...'),
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
          ('Jogos ao vivo...', () => _navigation.navigate('/partida')),
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
      title: const Text('Ajuda — VERSÃO CLÁSSICA (ANTIGA)'),
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

  Widget _retroMenuButton(String title) => PopupMenuButton<int>(
    tooltip: '',
    enabled: !_matchLocked || title == 'Campeonato',
    onSelected: (i) => _retroMenu(title)[i].$2?.call(),
    itemBuilder: (_) => [
      for (final (i, entry) in _retroMenu(title).indexed)
        PopupMenuItem(
          value: i,
          enabled: entry.$2 != null,
          height: 38,
          child: Text(entry.$1),
        ),
    ],
    child: Padding(
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 9),
      child: Text(
        title,
        style: const TextStyle(fontSize: 16, fontWeight: FontWeight.bold),
      ),
    ),
  );

  Future<void> _retroMobileMenu(BuildContext context) =>
      showModalBottomSheet<void>(
        context: context,
        isScrollControlled: true,
        backgroundColor: classicButtonColor,
        builder: (ctx) => SafeArea(
          child: SizedBox(
            height: MediaQuery.sizeOf(ctx).height * .8,
            child: ListView(
              children: [
                const RetroTitle(title: 'Menus'),
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
                      style: const TextStyle(fontWeight: FontWeight.bold),
                    ),
                    children: [
                      for (final entry in _retroMenu(title))
                        ListTile(
                          dense: true,
                          enabled: entry.$2 != null,
                          title: Text(entry.$1),
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
      return PopScope(
        canPop: !_matchLocked,
        child: Scaffold(
          backgroundColor: const Color(0xFF004528),
          body: MatchInteractionOverlay(
            interaction: _buildMatchInteraction(),
            child: SafeArea(
              child: Padding(
                padding: EdgeInsets.all(mobile ? 0 : 16),
                child: RetroBevel(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      RetroTitle(
                        title: mobile
                            ? 'LIGA CLÁSSICA 98'
                            : 'LIGA CLÁSSICA 98 — $clubName',
                        trailing: mobile
                            ? IconButton(
                                tooltip: 'Abrir menus',
                                onPressed: _matchLocked
                                    ? null
                                    : () => _retroMobileMenu(ctx),
                                icon: const Icon(
                                  Icons.menu,
                                  color: Colors.white,
                                ),
                              )
                            : const Text(
                                'VERSÃO CLÁSSICA (ANTIGA)',
                                style: TextStyle(
                                  color: Colors.white,
                                  fontSize: 12,
                                ),
                              ),
                      ),
                      if (!mobile)
                        Container(
                          color: classicButtonColor,
                          child: Row(
                            children: [
                              for (final title in [
                                'Configurações',
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
                      if (_showMatchView || _matchLocked)
                        Container(
                          color: classicPaperColor,
                          padding: const EdgeInsets.all(8),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.stretch,
                            children: [
                              Text(
                                _matchVisualStateLabel(
                                  _snapshot?['roundState']?.toString() ??
                                      'PREPARING',
                                  _snapshot?['activePauseWindow'] is Map
                                      ? Map<String, dynamic>.from(
                                          _snapshot!['activePauseWindow'],
                                        )
                                      : null,
                                ),
                              ),
                              _buildRoundPrimaryAction(),
                            ],
                          ),
                        ),
                      if (_statusBanner != null)
                        Container(
                          color: classicSoftGreenColor,
                          padding: const EdgeInsets.symmetric(horizontal: 8),
                          child: Row(
                            children: [
                              Expanded(
                                child: Text(
                                  _statusBanner!,
                                  style: const TextStyle(fontSize: 13),
                                ),
                              ),
                              IconButton(
                                tooltip: 'Fechar aviso',
                                onPressed: () =>
                                    setState(() => _statusBanner = null),
                                icon: const Icon(Icons.close, size: 18),
                              ),
                            ],
                          ),
                        ),
                      Expanded(
                        child: ColoredBox(
                          color: classicPaperColor,
                          child: _retroBody(ctx, mobile),
                        ),
                      ),
                      if (!_matchLocked)
                        Container(
                          color: classicButtonColor,
                          padding: const EdgeInsets.all(3),
                          child: Row(
                            children: [
                              for (final entry
                                  in mobile
                                      ? [
                                          (
                                            'Jogo',
                                            Icons.sports_soccer,
                                            'squad',
                                            0,
                                          ),
                                          (
                                            'Táticas',
                                            Icons.sports_soccer_outlined,
                                            'tactics',
                                            0,
                                          ),
                                          (
                                            'Mercado',
                                            Icons.swap_horiz,
                                            'market',
                                            2,
                                          ),
                                          (
                                            'Campeonato',
                                            Icons.emoji_events_outlined,
                                            'standings',
                                            1,
                                          ),
                                          ('Mais', Icons.more_horiz, 'more', 0),
                                        ]
                                      : [
                                          (
                                            'Jogo',
                                            Icons.sports_soccer,
                                            'squad',
                                            0,
                                          ),
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
                                          ('Banco', Icons.groups, 'bench', 0),
                                          (
                                            'Estádio',
                                            Icons.stadium_outlined,
                                            'stadium',
                                            3,
                                          ),
                                          (
                                            'História — Em breve',
                                            Icons.menu_book,
                                            'history',
                                            0,
                                          ),
                                        ])
                                Expanded(
                                  child: RetroButton(
                                    compact: mobile,
                                    label: entry.$1,
                                    icon: mobile ? null : entry.$2,
                                    onPressed: entry.$3 == 'history'
                                        ? null
                                        : () => entry.$3 == 'more'
                                              ? _retroMobileMenu(ctx)
                                              : _retroOpen(entry.$3, entry.$4),
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
        return _retroHome(ctx, mobile);
      case 'tactics':
        return _retroTactics();
      case 'bench':
        return _retroBench(mobile);
      case 'player':
      case 'statistics':
      case 'contract':
        return _retroPlayer(page);
      case 'standings':
        return _retroStandings(mobile);
      case 'coachRanking':
        return _retroCoaches(mobile);
      case 'profile':
        return _retroProfile();
      case 'career':
        return _retroCareer();
      case 'cup':
        return _retroCup();
      case 'calendar':
      case 'results':
        return _retroCalendar(page == 'results');
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
                      'A versão clássica utiliza a paleta fixa desta interface. As cores registradas dos clubes são preservadas.',
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
    final table = RetroTable(
      dense: !mobile,
      key: const Key('retro-roster'),
      headers: mobile
          ? ['POS', 'JOGADOR', 'FORÇA']
          : ['POS', 'JOGADOR', 'FORÇA', 'VALOR'],
      flex: mobile ? [2, 8, 2] : [2, 9, 2, 4],
      rows: [
        for (final p in squad)
          [
            '${_retroRole(p)} ${_formatPosition(p['position'])}',
            '${p['displayName'] ?? p['name']}${p['isStar'] == true && !'${p['displayName']}'.contains('*') ? '*' : ''}',
            '${p['strength']}',
            if (!mobile) formatClassicMoney(p['fairValue']),
          ],
      ],
      selected: squad.indexWhere(
        (p) => '${p['id']}' == _classicSelectedPlayerId,
      ),
      onSelect: (i) =>
          setState(() => _classicSelectedPlayerId = '${squad[i]['id']}'),
    );
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        if (bounded)
          ConstrainedBox(
            constraints: BoxConstraints(
              maxHeight:
                  MediaQuery.sizeOf(context).height * (mobile ? .40 : .64),
            ),
            child: SingleChildScrollView(child: table),
          )
        else
          table,
        Container(
          color: classicPaperColor,
          padding: const EdgeInsets.all(8),
          child: const Row(
            children: [
              Icon(Icons.circle, size: 9, color: Color(0xFF14851F)),
              SizedBox(width: 5),
              Flexible(
                child: Text(
                  'Titular   – Banco   · Fora do banco   ! Indisponível',
                  style: TextStyle(color: classicInkColor, fontSize: 14),
                ),
              ),
            ],
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
                  label: 'Informações',
                  onPressed: () => _retroOpen('player'),
                ),
                RetroButton(
                  label: 'Mercado...',
                  onPressed:
                      _commandBusy ||
                          (_tacticsLocked &&
                              _snapshot?['roundState'] != 'RESULTS')
                      ? null
                      : () => _announceSquadPlayer(_retroSelected!),
                ),
              ],
            ),
          ),
      ],
    );
  }

  Widget _retroHome(BuildContext ctx, bool mobile) {
    final club = _myClubDetail;
    final header = Row(
      children: [
        if (club != null)
          ClubCrest(
            club: club['name'].toString(),
            palette: clubMatchPalette(club),
            size: 56,
          ),
        const SizedBox(width: 10),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                '${club?['name'] ?? 'Treinador sem clube'}',
                style: const TextStyle(
                  fontSize: 22,
                  fontWeight: FontWeight.bold,
                ),
              ),
              Text('${club?['divisionDisplay'] ?? 'Sem clube'} • $_nickname'),
            ],
          ),
        ),
        if (!mobile)
          Text(
            'Temporada ${_snapshot?['seasonNumber'] ?? 1}\n${formatClassicDate(_snapshot?['gameDate'])}',
            textAlign: TextAlign.right,
          ),
      ],
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
      padding: const EdgeInsets.all(10),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          header,
          const SizedBox(height: 12),
          if (mobile) ...[
            roster,
            const SizedBox(height: 8),
            _retroSummary(),
          ] else
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Expanded(flex: 7, child: roster),
                const SizedBox(width: 16),
                Expanded(flex: 4, child: _retroSummary()),
              ],
            ),
        ],
      ),
    );
  }

  Widget _retroSummary() {
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
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        RetroSection(
          title: 'Próximo adversário',
          child: Container(
            color: classicSoftGreenColor,
            padding: const EdgeInsets.all(12),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  '${opponent ?? 'A definir'}',
                  style: const TextStyle(
                    fontSize: 22,
                    fontWeight: FontWeight.bold,
                  ),
                ),
                Text(
                  '${opponent == null
                      ? 'Local a definir'
                      : home
                      ? 'Casa'
                      : 'Fora'} • Rodada $round',
                ),
              ],
            ),
          ),
        ),
        Text(
          'Divisão: ${club?['divisionDisplay'] ?? 'Sem clube'}',
          style: const TextStyle(fontSize: 18),
        ),
        const SizedBox(height: 12),
        Text(
          'Rodada: $round • ${classicCompetition(_snapshot?['competitionType'])}',
        ),
        const SizedBox(height: 12),
        Text(
          'Caixa: ${club == null ? '—' : formatClassicMoney(club['cashBalance'])}',
          style: const TextStyle(fontSize: 21, fontWeight: FontWeight.bold),
        ),
        const SizedBox(height: 12),
        Text('Moral: ${morale ?? 'A definir'}'),
        const SizedBox(height: 5),
        LinearProgressIndicator(
          value: morale is num ? (morale / 100).clamp(0, 1).toDouble() : 0,
          minHeight: 18,
          color: const Color(0xFF50A550),
          backgroundColor: classicButtonColor,
        ),
        const SizedBox(height: 22),
        KeyedSubtree(
          key: const Key('retro-match-action'),
          child: _buildRoundPrimaryAction(),
        ),
        if (ready && state == 'PREPARING')
          const Text('AGUARDANDO OUTROS TREINADORES.'),
        Text(
          'Sala: ${_snapshot?['worldName'] ?? _worldId}\nRodada compartilhada • ${_snapshot?['roundState'] ?? 'Carregando'}',
          style: const TextStyle(fontSize: 12),
        ),
      ],
    );
  }

  Widget _retroTactics() => SingleChildScrollView(
    padding: const EdgeInsets.all(12),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        const Text(
          'TÁTICAS',
          style: TextStyle(fontWeight: FontWeight.bold, fontSize: 22),
        ),
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
              label: 'Automática',
              onPressed: _tacticsLocked ? null : _autoPickBestLineup,
            ),
            RetroButton(
              label: 'Melhores',
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
                    .where((p) => _selectedStarterIds.contains('${p['id']}'))
                    .map(
                      (p) => {...p, 'position': _formatPosition(p['position'])},
                    )
                    .toList(),
                _selectedFormation,
              ),
              shirt: const Color(0xFFEEEEEE),
              ink: Colors.black,
              onPlayer: (p) =>
                  setState(() => _classicSelectedPlayerId = '${p['id']}'),
            ),
          ),
        ),
        const SizedBox(height: 10),
        Text(
          'Titulares: ${_selectedStarterIds.length}/$_requiredStarterCount • Banco: ${_selectedBenchIds.length}/9',
        ),
        const SizedBox(height: 6),
        _retroRoster(MediaQuery.sizeOf(context).width < 768),
        if (_retroSelected != null)
          RetroButton(
            label: 'Alterar relação do jogador selecionado',
            onPressed: _tacticsLocked
                ? null
                : () => _toggleSquadPlayer(_retroSelected!),
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
        title: const Text('Trocar com titular'),
        children: [
          for (final p in _retroSquad.where(
            (p) => _selectedStarterIds.contains('${p['id']}'),
          ))
            SimpleDialogOption(
              onPressed: () => Navigator.pop(ctx, '${p['id']}'),
              child: Text(
                '${p['displayName']} (${_formatPosition(p['position'])})',
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
          RetroTable(
            headers: const ['POS', 'JOGADOR', 'FORÇA', 'CONDIÇÃO'],
            flex: mobile ? [1, 5, 2, 3] : [1, 5, 1, 2],
            rows: [
              for (final p in bench)
                [
                  _formatPosition(p['position']),
                  '${p['displayName']}',
                  '${p['strength']}',
                  _retroCondition(p),
                ],
            ],
            selected: bench.indexWhere(
              (p) => '${p['id']}' == _classicSelectedPlayerId,
            ),
            onSelect: (i) =>
                setState(() => _classicSelectedPlayerId = '${bench[i]['id']}'),
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
          Text(
            '${p['displayName']}',
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
                Text('Condição: ${_retroCondition(p)}'),
                Text(
                  'Salário por partida: ${formatClassicMoney(p['salaryPerRound'])}',
                ),
                Text('Valor justo: ${formatClassicMoney(p['fairValue'])}'),
                Text(
                  'Contrato: ${p['contractRenewedForSeason'] == true ? 'até o fim da temporada ${p['contractThroughSeason'] ?? '—'}' : '${p['contractRoundsRemaining'] ?? '—'} rodadas'}',
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
                      (_tacticsLocked && _snapshot?['roundState'] != 'RESULTS')
                  ? null
                  : () => _renewSquadPlayer(p),
            ),
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
                label: '${d.$2} Divisão',
                onPressed: () async {
                  setState(() => _selectedDivisionTier = d.$1);
                  final rows = await widget.apiClient.getStandings(
                    _worldId!,
                    d.$1,
                  );
                  if (mounted && _selectedDivisionTier == d.$1) {
                    setState(() => _standings = rows);
                  }
                },
              ),
          ],
        ),
        const SizedBox(height: 10),
        RetroTable(
          onSelect: mobile
              ? (i) => _retroStandingDetails(_standings[i] as Map)
              : null,
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
        if (mobile)
          const Text(
            'Toque no clube para ver vitórias, empates, derrotas e gols.',
          ),
      ],
    ),
  );

  Future<void> _retroStandingDetails(Map row) => showDialog<void>(
    context: context,
    builder: (ctx) => AlertDialog(
      title: Text('${row['clubName']}'),
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
                Text('Aceitar transfere seu comando para este clube.'),
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
          Text(_nickname),
          Text('Clube: ${_myClubDetail?['name'] ?? 'Sem clube'}'),
          const Text('Tipo: HUMANO'),
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

  Widget _retroCalendar(bool results) => SingleChildScrollView(
    padding: const EdgeInsets.all(10),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(
          results ? 'RESULTADOS' : 'CALENDÁRIO',
          style: const TextStyle(fontSize: 22, fontWeight: FontWeight.bold),
        ),
        for (final r in _calendar)
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
                        ),
                      ),
                ],
              ),
            ),
        if (results &&
            !_calendar.any(
              (r) => (r['fixtures'] as List? ?? []).any(
                (f) => f['isFinished'] == true,
              ),
            ))
          const Text('Nenhuma partida concluída.'),
      ],
    ),
  );
}
