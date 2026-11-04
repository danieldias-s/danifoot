import 'package:flutter/material.dart';
import 'classic_match_board.dart';
import 'club_crest.dart';
import 'club_theme.dart';
import 'live_match_data.dart';
import 'match_gate.dart';
import 'player_rankings.dart' show formatClassicMoney;

String liveNumber(dynamic value) => value is num
    ? value
          .toStringAsFixed(0)
          .replaceAllMapped(RegExp(r'(\d)(?=(\d{3})+$)'), (m) => '${m[1]}.')
    : '—';
Map<String, dynamic>? livePalette(Map<String, dynamic> match, String side) =>
    match['${side}ColorPalette'] is Map
    ? Map<String, dynamic>.from(match['${side}ColorPalette'] as Map)
    : null;
const liveBlue = Color(0xFF071B80);

class LivePanel extends StatelessWidget {
  final String title;
  final Widget child;
  final Widget? trailing;
  final EdgeInsets padding;
  const LivePanel({
    super.key,
    required this.title,
    required this.child,
    this.trailing,
    this.padding = const EdgeInsets.all(8),
  });
  @override
  Widget build(BuildContext context) {
    final t = ClassicTheme.of(context);
    final header = t.classicPresentation ? t.primary : liveBlue;
    return Container(
      clipBehavior: Clip.antiAlias,
      decoration: BoxDecoration(
        color: t.surface,
        border: Border.all(color: t.border),
        borderRadius: BorderRadius.circular(4),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 6),
            decoration: BoxDecoration(
              color: header,
              border: Border(bottom: BorderSide(color: t.accent)),
            ),
            child: Row(
              children: [
                Expanded(
                  child: Text(
                    title,
                    style: TextStyle(
                      fontSize: 13,
                      fontWeight: FontWeight.w900,
                      color: readableInk(
                        header,
                        t.classicPresentation ? Colors.white : t.accent,
                      ),
                    ),
                  ),
                ),
                ?trailing,
              ],
            ),
          ),
          Padding(padding: padding, child: child),
        ],
      ),
    );
  }
}

class LiveMatchStatus extends StatelessWidget {
  final String status;
  final bool onDark;
  const LiveMatchStatus({super.key, required this.status, this.onDark = false});
  @override
  Widget build(BuildContext context) {
    final indicator = switch (status) {
      'Em andamento' => const Color(0xFF76E36D),
      'Intervalo' || 'Pausada' => const Color(0xFFFFD700),
      _ => const Color(0xFFD2DCD5),
    };
    final t = ClassicTheme.of(context);
    final color = t.classicPresentation && !onDark
        ? readableOn(indicator, t.surface)
        : indicator;
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(Icons.circle, size: 9, color: color),
        const SizedBox(width: 5),
        Text(
          status,
          style: TextStyle(
            color: color,
            fontSize: 11,
            fontWeight: FontWeight.bold,
          ),
        ),
      ],
    );
  }
}

class SelectedMatchScoreboard extends StatelessWidget {
  final Map<String, dynamic> match;
  final String stage, competition;
  final int round;
  const SelectedMatchScoreboard({
    super.key,
    required this.match,
    required this.stage,
    required this.competition,
    required this.round,
  });
  @override
  Widget build(BuildContext context) => LayoutBuilder(
    builder: (context, c) {
      final t = ClassicTheme.of(context), small = c.maxWidth < 600;
      final minute = match['minute'];
      final clockWidth = small ? 72.0 : 94.0;
      final numberStyle = TextStyle(
        fontFamily: t.classicPresentation ? 'ClassicCondensed' : 'Roboto',
        fontSize: small ? 26 : 34,
        fontWeight: FontWeight.w900,
        height: 1,
      );
      Widget club(String side) {
        final colors = matchTeamColors(t, livePalette(match, side));
        return Expanded(
          child: Tooltip(
            message: '${match['${side}ClubName'] ?? '—'}',
            child: Container(
              key: ValueKey('selected-$side-team-colors'),
              padding: const EdgeInsets.all(8),
              decoration: BoxDecoration(
                color: colors.background,
                borderRadius: BorderRadius.circular(3),
              ),
              child: Column(
                children: [
                  ClubCrest(
                    club: '${match['${side}ClubName'] ?? ''}',
                    palette: livePalette(match, side),
                    size: small ? 30 : 42,
                  ),
                  const SizedBox(height: 3),
                  Text(
                    '${match['${side}ClubName'] ?? '—'}',
                    key: ValueKey('selected-$side-name'),
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                    textAlign: TextAlign.center,
                    style: TextStyle(
                      fontFamily: t.classicPresentation
                          ? 'ClassicCondensed'
                          : 'Roboto',
                      fontWeight: FontWeight.w900,
                      color: colors.ink,
                    ).copyWith(fontSize: small ? 14 : 23),
                  ),
                ],
              ),
            ),
          ),
        );
      }

      Widget score(String side) => Container(
        width: small ? 37 : 56,
        height: small ? 36 : 46,
        alignment: Alignment.center,
        decoration: BoxDecoration(
          color: t.classicPresentation ? Colors.black : t.accent,
          borderRadius: BorderRadius.circular(3),
        ),
        padding: const EdgeInsets.all(4),
        child: FittedBox(
          child: Text(
            '${match['${side}Goals'] ?? '—'}',
            style: numberStyle.copyWith(
              color: t.classicPresentation ? Colors.white : t.selectedText,
            ),
          ),
        ),
      );
      return Container(
        key: const Key('selected-match-scoreboard'),
        padding: const EdgeInsets.all(8),
        decoration: BoxDecoration(
          gradient: LinearGradient(
            colors: t.classicPresentation
                ? [t.secondary, const Color(0xFF002E20)]
                : const [liveBlue, Color(0xFF030E49)],
          ),
          border: Border.all(color: t.accent),
          borderRadius: BorderRadius.circular(4),
        ),
        child: Column(
          children: [
            Row(
              children: [
                if (minute != null) SizedBox(width: clockWidth),
                Expanded(
                  child: Text(
                    'RODADA $round • $competition',
                    textAlign: TextAlign.center,
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                      color: Colors.white,
                      fontSize: 11,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                ),
                if (minute != null)
                  SizedBox(
                    width: clockWidth,
                    child: Align(
                      alignment: Alignment.centerRight,
                      child: FittedBox(
                        fit: BoxFit.scaleDown,
                        child: Text(
                          "$minute′",
                          semanticsLabel: 'Minuto $minute',
                          style: numberStyle.copyWith(color: Colors.white),
                        ),
                      ),
                    ),
                  ),
              ],
            ),
            const SizedBox(height: 7),
            Row(
              children: [
                club('home'),
                score('home'),
                const Padding(
                  padding: EdgeInsets.symmetric(horizontal: 6),
                  child: Text(
                    'x',
                    style: TextStyle(
                      color: Colors.white,
                      fontSize: 18,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                ),
                score('away'),
                club('away'),
              ],
            ),
            const SizedBox(height: 6),
            Wrap(
              spacing: 10,
              runSpacing: 4,
              alignment: WrapAlignment.center,
              crossAxisAlignment: WrapCrossAlignment.center,
              children: [
                if (match['isExtraTime'] == true)
                  const Text(
                    'Prorrogação',
                    style: TextStyle(
                      color: Colors.white,
                      fontSize: 12,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                LiveMatchStatus(
                  status: matchStatus(match, stage),
                  onDark: true,
                ),
                if (match['homePenaltyGoals'] != null &&
                    match['awayPenaltyGoals'] != null)
                  Text(
                    'Pênaltis: ${match['homePenaltyGoals']} x ${match['awayPenaltyGoals']}',
                    style: TextStyle(
                      color: t.classicPresentation ? Colors.white : t.accent,
                      fontSize: 11,
                    ),
                  ),
              ],
            ),
            if (match['homePlayersOnPitch'] != null &&
                match['awayPlayersOnPitch'] != null)
              Padding(
                padding: const EdgeInsets.only(top: 4),
                child: Text(
                  '${match['homePlayersOnPitch']} em campo × ${match['awayPlayersOnPitch']} em campo',
                  style: const TextStyle(color: Colors.white70, fontSize: 10),
                ),
              ),
          ],
        ),
      );
    },
  );
}

class SelectedMatchVenue extends StatelessWidget {
  final Map<String, dynamic> match;
  final Map<String, dynamic>? club;
  final String competition;
  final int round;
  final bool compact;
  const SelectedMatchVenue({
    super.key,
    required this.match,
    required this.club,
    required this.competition,
    required this.round,
    this.compact = false,
  });
  @override
  Widget build(BuildContext context) {
    final t = ClassicTheme.of(context);
    final stadium = club?['stadiumName']?.toString() ?? 'Não informado';
    final attendance = officialGateValue(match, 'attendance');
    final revenue = officialGateValue(match, 'ticketRevenue');
    final attendanceLabel = attendance == null
        ? unreportedGateLabel(match)
        : liveNumber(attendance);
    final revenueLabel = revenue == null
        ? unreportedGateLabel(match)
        : formatClassicMoney(revenue);
    if (compact) {
      return Container(
        padding: const EdgeInsets.symmetric(horizontal: 7, vertical: 5),
        decoration: BoxDecoration(
          color: t.surface,
          border: Border.all(color: t.border),
        ),
        child: Wrap(
          spacing: 9,
          runSpacing: 4,
          crossAxisAlignment: WrapCrossAlignment.center,
          children: [
            Text(stadium, style: TextStyle(color: t.textPrimary, fontSize: 11)),
            Text(
              'Público $attendanceLabel',
              style: TextStyle(color: t.textPrimary, fontSize: 11),
            ),
            Text(
              'Renda $revenueLabel',
              style: TextStyle(color: t.accent, fontSize: 11),
            ),
            IconButton(
              key: const Key('live-venue-details'),
              constraints: const BoxConstraints(minWidth: 32, minHeight: 32),
              padding: EdgeInsets.zero,
              tooltip: 'Estádio, capacidade e bilheteria',
              onPressed: () => showModalBottomSheet<void>(
                context: context,
                isScrollControlled: true,
                builder: (context) => SafeArea(
                  child: SingleChildScrollView(
                    child: SelectedMatchVenue(
                      match: match,
                      club: club,
                      competition: competition,
                      round: round,
                    ),
                  ),
                ),
              ),
              icon: Icon(Icons.info_outline, size: 18, color: t.textSecondary),
            ),
          ],
        ),
      );
    }
    final rows = [
      (Icons.stadium_outlined, 'Estádio', stadium),
      (Icons.place_outlined, 'Cidade', 'Não informada'),
      (Icons.groups_outlined, 'Público pagante', attendanceLabel),
      (
        Icons.event_seat_outlined,
        'Capacidade (cadastro atual)',
        liveNumber(club?['builtCapacity']),
      ),
      (Icons.payments_outlined, 'Renda', revenueLabel),
      (Icons.emoji_events_outlined, 'Competição', competition),
      (Icons.calendar_month_outlined, 'Rodada', 'Rodada $round'),
    ];
    return LivePanel(
      key: const Key('selected-match-venue'),
      title: 'PARTIDA SELECIONADA',
      child: Column(
        children: [
          for (final row in rows)
            Container(
              padding: const EdgeInsets.symmetric(vertical: 7),
              decoration: BoxDecoration(
                border: Border(bottom: BorderSide(color: t.border)),
              ),
              child: Row(
                children: [
                  Icon(row.$1, size: 21, color: t.textPrimary),
                  const SizedBox(width: 8),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          row.$2,
                          style: TextStyle(
                            fontSize: 10,
                            color: t.textSecondary,
                          ),
                        ),
                        Text(
                          row.$3,
                          style: TextStyle(
                            fontSize: 12,
                            color: t.textPrimary,
                            fontWeight: FontWeight.bold,
                          ),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            ),
        ],
      ),
    );
  }
}

class MatchEventTimeline extends StatelessWidget {
  final Map<String, dynamic> match;
  final String stage;
  final double maxHeight;
  const MatchEventTimeline({
    super.key,
    required this.match,
    required this.stage,
    this.maxHeight = 330,
  });
  @override
  Widget build(BuildContext context) {
    final t = ClassicTheme.of(context), events = officialMatchEvents(match);
    if (events.isEmpty) {
      return Padding(
        padding: const EdgeInsets.all(8),
        child: Text(
          match['_calendarOnly'] == true
              ? 'Lances não disponíveis na consulta do calendário.'
              : matchStatus(match, stage) == 'Não iniciada'
              ? 'Aguardando início da partida.'
              : 'Nenhum lance registrado até o momento.',
          style: TextStyle(fontSize: 12, color: t.textSecondary),
        ),
      );
    }
    return ConstrainedBox(
      key: const Key('live-match-events'),
      constraints: BoxConstraints(maxHeight: maxHeight),
      child: SingleChildScrollView(
        key: ValueKey('event-scroll-${matchIdentity(match)}'),
        child: Column(
          children: [
            for (int i = 0; i < events.length; i++)
              MatchEventLine(
                key: ValueKey('official-event-${matchIdentity(match)}-$i'),
                event: events[i],
              ),
          ],
        ),
      ),
    );
  }
}

class MatchEventLine extends StatelessWidget {
  final Map<String, dynamic> event;
  const MatchEventLine({super.key, required this.event});
  @override
  Widget build(BuildContext context) {
    final t = ClassicTheme.of(context), type = eventType(event);
    final icon = switch (type) {
      'Goal' || 'PenaltyShootoutGoal' => Icons.sports_soccer,
      'YellowCard' || 'SecondYellowRedCard' || 'DirectRedCard' => Icons.style,
      'Injury' => Icons.medical_services,
      'Substitution' => Icons.swap_horiz,
      'TacticChange' => Icons.account_tree,
      _ => Icons.info_outline,
    };
    final color = switch (type) {
      'YellowCard' => const Color(0xFFFFD700),
      'SecondYellowRedCard' || 'DirectRedCard' => const Color(0xFFFF5B54),
      'Injury' => Colors.white,
      'Substitution' => const Color(0xFF8CEC6C),
      _ => t.textPrimary,
    };
    final rawDescription =
        type == 'Substitution' &&
            event['primaryPlayerDisplayName'] != null &&
            event['secondaryPlayerDisplayName'] != null
        ? '${event['clubName'] ?? ''} — Sai: ${event['secondaryPlayerDisplayName']} • Entra: ${event['primaryPlayerDisplayName']}'
        : event['description']?.toString() ??
              '${event['clubName'] ?? ''} — ${event['primaryPlayerDisplayName'] ?? ''}';
    final prefix = "${event['minute']}' ";
    final description = rawDescription.startsWith(prefix)
        ? rawDescription.substring(prefix.length).trimLeft()
        : rawDescription;
    return Container(
      padding: const EdgeInsets.symmetric(vertical: 5),
      decoration: BoxDecoration(
        border: Border(
          bottom: BorderSide(color: t.border.withValues(alpha: .6)),
        ),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: 30,
            child: Text(
              "${event['minute'] ?? '—'}'",
              style: TextStyle(
                fontSize: 12,
                color: t.accent,
                fontWeight: FontWeight.bold,
              ),
            ),
          ),
          Tooltip(
            message: classicEventBadge(event),
            child: Icon(icon, size: 18, color: color),
          ),
          const SizedBox(width: 7),
          Expanded(
            child: Text(
              description,
              style: TextStyle(fontSize: 12, color: t.textPrimary),
            ),
          ),
        ],
      ),
    );
  }
}

class MatchStatisticsPanel extends StatelessWidget {
  final Map<String, dynamic> match;
  const MatchStatisticsPanel({super.key, required this.match});
  @override
  Widget build(BuildContext context) {
    final t = ClassicTheme.of(context);
    if (!match.containsKey('events') || match['_calendarOnly'] == true) {
      return Text(
        'Estatísticas não disponíveis para este confronto.',
        style: TextStyle(color: t.textSecondary, fontSize: 12),
      );
    }
    final events = officialMatchEvents(match);
    int cards(String side, Set<String> types) => events
        .where(
          (e) =>
              e['clubId'] == match['${side}ClubId'] &&
              types.contains(eventType(e)),
        )
        .length;
    final rows = [
      (
        'Cartões amarelos',
        cards('home', {'YellowCard', 'SecondYellowRedCard'}),
        cards('away', {'YellowCard', 'SecondYellowRedCard'}),
      ),
      (
        'Cartões vermelhos',
        cards('home', {'SecondYellowRedCard', 'DirectRedCard'}),
        cards('away', {'SecondYellowRedCard', 'DirectRedCard'}),
      ),
    ];
    return Column(
      key: const Key('selected-match-statistics'),
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Row(
          children: [
            Expanded(
              child: Text(
                '${match['homeClubName'] ?? ''}',
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(
                  color: t.textPrimary,
                  fontSize: 11,
                  fontWeight: FontWeight.bold,
                ),
              ),
            ),
            const SizedBox(width: 10),
            Expanded(
              child: Text(
                '${match['awayClubName'] ?? ''}',
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
                textAlign: TextAlign.right,
                style: TextStyle(
                  color: t.textPrimary,
                  fontSize: 11,
                  fontWeight: FontWeight.bold,
                ),
              ),
            ),
          ],
        ),
        for (final r in rows)
          Container(
            padding: const EdgeInsets.symmetric(vertical: 7),
            decoration: BoxDecoration(
              border: Border(bottom: BorderSide(color: t.border)),
            ),
            child: Row(
              children: [
                SizedBox(
                  width: 27,
                  child: Text(
                    '${r.$2}',
                    textAlign: TextAlign.center,
                    style: TextStyle(
                      color: t.textPrimary,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                ),
                Expanded(
                  child: Text(
                    r.$1,
                    textAlign: TextAlign.center,
                    style: TextStyle(fontSize: 11, color: t.textSecondary),
                  ),
                ),
                SizedBox(
                  width: 27,
                  child: Text(
                    '${r.$3}',
                    textAlign: TextAlign.center,
                    style: TextStyle(
                      color: t.textPrimary,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                ),
              ],
            ),
          ),
        const SizedBox(height: 8),
        Text(
          'Cartões contabilizados pelos eventos oficiais. Finalizações, posse, escanteios e faltas não são disponibilizados nesta consulta.',
          style: TextStyle(color: t.textSecondary, fontSize: 10),
        ),
      ],
    );
  }
}

class LiveMatchScreen extends StatefulWidget {
  final Map<String, dynamic> snapshot;
  final List<dynamic> calendar;
  final Map<String, Map<String, dynamic>> clubs;
  final String? coachClubId;
  final String coachId, coachName;
  final LiveMatchSelection selection;
  final VoidCallback onSelectionChanged;
  final Widget? coachControls;
  final VoidCallback? onSubstitute;
  const LiveMatchScreen({
    super.key,
    required this.snapshot,
    required this.calendar,
    required this.clubs,
    required this.coachId,
    required this.coachName,
    required this.selection,
    required this.onSelectionChanged,
    this.coachClubId,
    this.coachControls,
    this.onSubstitute,
  });
  @override
  State<LiveMatchScreen> createState() => _LiveMatchScreenState();
}

class _LiveMatchScreenState extends State<LiveMatchScreen> {
  int? _viewedRound;
  String? _filter;
  int _detailTab = 0;
  double? _returnOffset;
  final _scroll = ScrollController();
  @override
  void dispose() {
    _scroll.dispose();
    super.dispose();
  }

  @override
  void didUpdateWidget(covariant LiveMatchScreen old) {
    super.didUpdateWidget(old);
    if (old.snapshot['gameWorldId'] != widget.snapshot['gameWorldId'] ||
        old.snapshot['seasonNumber'] != widget.snapshot['seasonNumber'] ||
        old.snapshot['roundNumber'] != widget.snapshot['roundNumber']) {
      _viewedRound = null;
      _filter = null;
      _detailTab = 0;
      _returnOffset = null;
    }
  }

  void _select(Map<String, dynamic> m) {
    setState(() => widget.selection.select(m));
    widget.onSelectionChanged();
  }

  void _toScoreboard() {
    _returnOffset = _scroll.offset;
    _scroll.animateTo(
      0,
      duration: const Duration(milliseconds: 180),
      curve: Curves.easeOut,
    );
    setState(() {});
  }

  @override
  Widget build(BuildContext context) => LayoutBuilder(
    builder: (context, c) {
      final t = ClassicTheme.of(context), desktop = c.maxWidth >= 1000;
      final snapshot = widget.snapshot,
          current = (snapshot['roundNumber'] as num? ?? 1).toInt(),
          round = _viewedRound ?? current;
      final calendar = widget.calendar
          .whereType<Map>()
          .map((r) => Map<String, dynamic>.from(r))
          .toList();
      final cal = calendar.where((r) => r['roundNumber'] == round).firstOrNull;
      final games = round == current
          ? (snapshot['matches'] as List? ?? [])
                .map(
                  (e) => matchWithClubColors(
                    Map<String, dynamic>.from(e as Map),
                    widget.clubs,
                  ),
                )
                .toList()
          : cal == null
          ? <Map<String, dynamic>>[]
          : calendarRoundMatches(cal, widget.clubs);
      final type =
          '${round == current ? snapshot['competitionType'] : cal?['competition'] ?? 'League'}';
      final label = round == current
          ? '${snapshot['competitionLabel'] ?? ''}'
          : type == 'League'
          ? 'Campeonato Nacional'
          : classicCupLabel(type);
      final stage = round == current
          ? '${snapshot['roundState']}'
          : 'PREPARING';
      widget.selection.accept(
        world: '${snapshot['gameWorldId']}',
        coach: widget.coachId,
        club: widget.coachClubId,
        season: (snapshot['seasonNumber'] as num? ?? 1).toInt(),
        round: round,
        matches: games,
      );
      final selected = games
          .where((m) => matchIdentity(m) == widget.selection.matchId)
          .firstOrNull;
      final mine = games
          .where((m) => isCoachMatch(m, widget.coachClubId))
          .firstOrNull;
      final groups = games
          .map((m) => matchGroup(m, type, label))
          .toSet()
          .toList();
      final filter = groups.contains(_filter) ? _filter : null;
      final filtered = filter == null
          ? games
          : games.where((m) => matchGroup(m, type, label) == filter).toList();
      final myClub = widget.clubs[widget.coachClubId];
      final home = widget.clubs[selected?['homeClubId']];
      final competition = selected == null
          ? label
          : matchGroup(selected, type, label);
      Widget myGame() => OutlinedButton.icon(
        key: const Key('my-live-match'),
        onPressed: mine == null
            ? null
            : () {
                setState(() {
                  _filter = null;
                  widget.selection.select(mine);
                });
                widget.onSelectionChanged();
              },
        icon: const Icon(Icons.my_location, size: 16),
        label: const Text('MEU JOGO'),
      );
      Widget myClubPanel() => LivePanel(
        title: 'MEU CLUBE',
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            if (widget.coachClubId != null)
              Row(
                children: [
                  ClubCrest(
                    club: '${myClub?['name'] ?? ''}',
                    palette: myClub?['colorPalette'] is Map
                        ? Map<String, dynamic>.from(
                            myClub!['colorPalette'] as Map,
                          )
                        : null,
                    size: 36,
                  ),
                  const SizedBox(width: 8),
                  Expanded(
                    child: Text(
                      '${myClub?['name'] ?? 'Clube do treinador'}',
                      style: TextStyle(
                        color: t.textPrimary,
                        fontSize: 13,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                  ),
                ],
              ),
            const SizedBox(height: 7),
            Text(
              widget.coachName,
              style: TextStyle(color: t.textPrimary, fontSize: 12),
            ),
            const SizedBox(height: 7),
            if (mine != null)
              LiveMatchStatus(status: matchStatus(mine, stage))
            else
              Text(
                widget.coachClubId == null
                    ? 'Treinador sem clube.'
                    : 'Seu clube não disputa esta rodada.',
                style: TextStyle(color: t.textSecondary, fontSize: 11),
              ),
            const SizedBox(height: 8),
            myGame(),
            if (widget.onSubstitute != null)
              OutlinedButton.icon(
                key: const Key('live-substitute-desktop'),
                onPressed: widget.onSubstitute,
                icon: const Icon(Icons.swap_horiz, size: 17),
                label: const Text('SUBSTITUIR'),
              ),
          ],
        ),
      );
      Widget results() => LivePanel(
        title: 'RESULTADOS DA RODADA',
        padding: const EdgeInsets.all(4),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            SingleChildScrollView(
              scrollDirection: Axis.horizontal,
              child: Row(
                children: [
                  for (final name in [null, ...groups])
                    Padding(
                      padding: const EdgeInsets.only(right: 4, bottom: 5),
                      child: OutlinedButton(
                        key: ValueKey('live-filter-${name ?? 'ALL'}'),
                        style: OutlinedButton.styleFrom(
                          padding: const EdgeInsets.symmetric(horizontal: 7),
                          minimumSize: const Size(0, 32),
                          backgroundColor: filter == name
                              ? t.accent
                              : t.surface,
                          foregroundColor: filter == name
                              ? readableInk(t.accent, t.selectedText)
                              : t.textPrimary,
                        ),
                        onPressed: () => setState(() => _filter = name),
                        child: Text(
                          name ?? 'TODOS',
                          style: const TextStyle(
                            fontSize: 10,
                            fontWeight: FontWeight.bold,
                          ),
                        ),
                      ),
                    ),
                ],
              ),
            ),
            if (games.isEmpty)
              Text(
                'Nenhum confronto disponível nesta rodada.',
                style: TextStyle(color: t.textSecondary),
              ),
            ClassicMatchBoard(
              matches: filtered,
              competitionType: type,
              competitionLabel: label,
              coachClubId: widget.coachClubId,
              selectedMatchId: widget.selection.matchId,
              onSelected: _select,
              mobile: !desktop,
              selectionScope: widget.selection.scope ?? '',
            ),
            if (!desktop && games.isNotEmpty)
              TextButton.icon(
                key: const Key('view-selected-scoreboard'),
                onPressed: _toScoreboard,
                icon: const Icon(Icons.arrow_upward, size: 16),
                label: const Text(
                  'VER PLACAR SELECIONADO',
                  style: TextStyle(fontSize: 11),
                ),
              ),
          ],
        ),
      );
      Widget timeline() => MatchEventTimeline(match: selected!, stage: stage);
      Widget statistics() => MatchStatisticsPanel(match: selected!);
      final rounds = <int>{
        current,
        ...calendar.map((r) => (r['roundNumber'] as num).toInt()),
      }.toList()..sort();
      void viewRound(int value) {
        setState(() {
          _viewedRound = value;
          _filter = null;
          _returnOffset = null;
        });
      }

      final index = rounds.indexOf(round);
      Widget roundBar() => Container(
        key: const Key('live-round-browser'),
        padding: const EdgeInsets.all(7),
        decoration: BoxDecoration(border: Border.all(color: t.border)),
        child: Wrap(
          spacing: 6,
          runSpacing: 4,
          crossAxisAlignment: WrapCrossAlignment.center,
          children: [
            Text(
              'Resumo: ${games.length} jogos • ${games.fold<int>(0, (n, m) => n + (m['homeGoals'] as num? ?? 0).toInt() + (m['awayGoals'] as num? ?? 0).toInt())} gols',
              style: TextStyle(
                color: t.accent,
                fontWeight: FontWeight.bold,
                fontSize: 11,
              ),
            ),
            IconButton(
              key: const Key('view-previous-round'),
              tooltip: 'Ver rodada anterior',
              onPressed: index > 0 ? () => viewRound(rounds[index - 1]) : null,
              icon: const Icon(Icons.chevron_left),
            ),
            DropdownButton<int>(
              key: const Key('live-view-round'),
              value: round,
              dropdownColor: t.surface,
              style: TextStyle(
                fontFamily: t.classicPresentation
                    ? 'ClassicCondensed'
                    : 'Roboto',
                color: t.textPrimary,
                fontSize: 12,
              ),
              items: [
                for (final r in rounds)
                  DropdownMenuItem(value: r, child: Text('Rodada $r')),
              ],
              onChanged: (value) {
                if (value != null) viewRound(value);
              },
            ),
            IconButton(
              key: const Key('view-next-round'),
              tooltip: 'Ver rodada seguinte',
              onPressed: index < rounds.length - 1
                  ? () => viewRound(rounds[index + 1])
                  : null,
              icon: const Icon(Icons.chevron_right),
            ),
            if (round != current)
              TextButton(
                key: const Key('view-current-round'),
                onPressed: () => viewRound(current),
                child: const Text('RODADA ATUAL'),
              ),
          ],
        ),
      );
      return SingleChildScrollView(
        key: const Key('live-match-screen'),
        controller: _scroll,
        padding: const EdgeInsets.all(8),
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 1680),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                if (desktop)
                  Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      SizedBox(
                        width: 190,
                        child: Column(
                          children: [
                            if (selected != null)
                              SelectedMatchVenue(
                                match: selected,
                                club: home,
                                competition: competition,
                                round: round,
                              ),
                            const SizedBox(height: 8),
                            myClubPanel(),
                          ],
                        ),
                      ),
                      const SizedBox(width: 8),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.stretch,
                          children: [
                            if (selected != null)
                              SelectedMatchScoreboard(
                                match: selected,
                                stage: stage,
                                competition: competition,
                                round: round,
                              )
                            else
                              Text(
                                'Aguardando a rodada confirmada pelo servidor.',
                                style: TextStyle(color: t.textSecondary),
                              ),
                            const SizedBox(height: 8),
                            Row(
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                Expanded(child: results()),
                                const SizedBox(width: 8),
                                SizedBox(
                                  width: c.maxWidth < 1200 ? 270 : 320,
                                  child: Column(
                                    children: [
                                      if (selected != null) ...[
                                        LivePanel(
                                          title: 'LANCES DA PARTIDA',
                                          child: timeline(),
                                        ),
                                        const SizedBox(height: 8),
                                        LivePanel(
                                          title: 'ESTATÍSTICAS DA PARTIDA',
                                          child: statistics(),
                                        ),
                                      ],
                                    ],
                                  ),
                                ),
                              ],
                            ),
                          ],
                        ),
                      ),
                    ],
                  )
                else ...[
                  if (selected != null) ...[
                    SelectedMatchScoreboard(
                      match: selected,
                      stage: stage,
                      competition: competition,
                      round: round,
                    ),
                    SelectedMatchVenue(
                      match: selected,
                      club: home,
                      competition: competition,
                      round: round,
                      compact: true,
                    ),
                  ],
                  if (selected == null)
                    Text(
                      'Aguardando a rodada confirmada pelo servidor.',
                      style: TextStyle(color: t.textSecondary),
                    ),
                  Wrap(
                    spacing: 8,
                    crossAxisAlignment: WrapCrossAlignment.center,
                    children: [
                      myGame(),
                      if (widget.onSubstitute != null)
                        OutlinedButton.icon(
                          key: const Key('live-substitute-mobile'),
                          onPressed: widget.onSubstitute,
                          icon: const Icon(Icons.swap_horiz, size: 17),
                          label: const Text('SUBSTITUIR'),
                        ),
                      if (mine == null)
                        Text(
                          widget.coachClubId == null
                              ? 'Treinador sem clube.'
                              : 'Seu clube não disputa esta rodada.',
                          style: TextStyle(
                            fontSize: 11,
                            color: t.textSecondary,
                          ),
                        ),
                      if (_returnOffset != null)
                        TextButton(
                          key: const Key('return-to-live-list'),
                          onPressed: () => _scroll.animateTo(
                            _returnOffset!.clamp(
                              0,
                              _scroll.position.maxScrollExtent,
                            ),
                            duration: const Duration(milliseconds: 180),
                            curve: Curves.easeOut,
                          ),
                          child: const Text(
                            'VOLTAR À LISTA',
                            style: TextStyle(fontSize: 10),
                          ),
                        ),
                    ],
                  ),
                  if (selected != null) ...[
                    Row(
                      children: [
                        for (final (i, text) in [
                          (0, 'LANCES DA PARTIDA'),
                          (1, 'ESTATÍSTICAS'),
                        ])
                          Expanded(
                            child: Padding(
                              padding: const EdgeInsets.only(right: 4),
                              child: OutlinedButton(
                                key: ValueKey('live-detail-tab-$i'),
                                style: OutlinedButton.styleFrom(
                                  backgroundColor: _detailTab == i
                                      ? t.accent
                                      : t.surface,
                                  foregroundColor: _detailTab == i
                                      ? readableInk(t.accent, t.selectedText)
                                      : t.textPrimary,
                                ),
                                onPressed: () => setState(() => _detailTab = i),
                                child: Text(
                                  text,
                                  style: const TextStyle(
                                    fontSize: 10,
                                    fontWeight: FontWeight.bold,
                                  ),
                                ),
                              ),
                            ),
                          ),
                      ],
                    ),
                    LivePanel(
                      title: _detailTab == 0
                          ? 'LANCES DA PARTIDA'
                          : 'ESTATÍSTICAS DA PARTIDA',
                      child: _detailTab == 0 ? timeline() : statistics(),
                    ),
                    const SizedBox(height: 8),
                  ],
                  results(),
                ],
                if (round == current &&
                    selected != null &&
                    isCoachMatch(selected, widget.coachClubId) &&
                    widget.coachControls != null) ...[
                  const SizedBox(height: 8),
                  widget.coachControls!,
                ],
                const SizedBox(height: 8),
                roundBar(),
              ],
            ),
          ),
        ),
      );
    },
  );
}
