import 'club_theme.dart';
import 'match_gate.dart';
import 'package:flutter/material.dart';

String classicDivisionLabel(dynamic tier) => switch (tier?.toString()) {
  'PrimeiraDivisao' || '1' => '1ª DIVISÃO',
  'SegundaDivisao' || '2' => '2ª DIVISÃO',
  'TerceiraDivisao' || '3' => '3ª DIVISÃO',
  'QuartaDivisao' || '4' => '4ª DIVISÃO',
  'Distrital' || '5' => 'DISTRITAIS',
  _ => _additionalDivisionLabel(tier),
};
String _additionalDivisionLabel(dynamic tier) {
  final raw = tier?.toString();
  final number = int.tryParse(raw ?? '');
  if (number != null && number > 5) return '$numberª DIVISÃO';
  return raw == null || raw.isEmpty
      ? 'CAMPEONATO — DIVISÃO NÃO INFORMADA'
      : raw.toUpperCase();
}

String classicCupLabel(String type) => switch (type) {
  'CupPreliminary1' => 'TAÇA - PRELIMINAR 1',
  'CupRoundOf128' => 'TAÇA - 128 CLUBES',
  'CupRoundOf256' => 'TAÇA - 256 CLUBES',
  'CupRoundOf512' => 'TAÇA - 512 CLUBES',
  'CupRoundOf1024' => 'TAÇA - 1024 CLUBES',
  'CupPreliminary2' => 'TAÇA - PRELIMINAR 2',
  'CupRoundOf64' => 'TAÇA - 1ª ELIMINATÓRIA',
  'CupRoundOf32' => 'TAÇA - 2ª ELIMINATÓRIA',
  'CupRoundOf16' => 'TAÇA - OITAVAS',
  'CupQuarterFinal' => 'TAÇA - QUARTAS',
  'CupSemiFinal' => 'TAÇA - SEMIFINAL',
  'CupFinal' => 'TAÇA - FINAL',
  _ => 'TAÇA',
};

/// Server eventType is authoritative; club names must never determine an event label.
String classicEventBadge(Map<String, dynamic> event) {
  final type = (event['eventType'] ?? event['type'])?.toString();
  return switch (type) {
    'Goal' || '0' => '[GOL]',
    'YellowCard' || '1' => '[CARTÃO]',
    'SecondYellowRedCard' || 'DirectRedCard' || '2' || '3' => '[EXPULSÃO]',
    'Injury' || '4' => '[LESÃO]',
    'Substitution' || '5' => '[SUB]',
    'TacticChange' || '6' => '[TÁTICA]',
    'PenaltyMissed' || '7' => '[PÊNALTI PERDIDO]',
    'Offside' || '8' => '[IMPEDIMENTO]',
    'PenaltyShootoutGoal' || '9' => '[GOL NOS PÊNALTIS]',
    'PenaltyShootoutMiss' || '10' => '[PÊNALTI PERDIDO]',
    'GlobalPauseTriggered' || '11' => '[PAUSA]',
    'HalftimeReached' || '12' => '[INTERVALO]',
    'ExtraTimeStarted' || '13' => '[PRORROGAÇÃO]',
    'PenaltyAwarded' || '15' => '[PÊNALTI]',
    _ => '[LANCE]',
  };
}

class ClassicMatchBoard extends StatelessWidget {
  final List<Map<String, dynamic>> matches;
  final String competitionType, competitionLabel;
  final String? coachClubId;
  final String? selectedMatchId;
  final ValueChanged<Map<String, dynamic>>? onSelected;
  final bool mobile;
  final String selectionScope;
  final Widget Function(Map<String, dynamic>)? coachDetails;
  const ClassicMatchBoard({
    super.key,
    required this.matches,
    required this.competitionType,
    required this.competitionLabel,
    this.coachClubId,
    this.selectedMatchId,
    this.onSelected,
    this.mobile = false,
    this.selectionScope = '',
    this.coachDetails,
  });

  @override
  Widget build(BuildContext context) {
    final groups = <String, List<Map<String, dynamic>>>{};
    for (final match in matches) {
      final type = match['competitionType']?.toString() ?? competitionType;
      final league = type == 'League' || type == '0';
      final label = league
          ? classicDivisionLabel(match['divisionTier'])
          : type == competitionType && competitionLabel.isNotEmpty
          ? competitionLabel.toUpperCase()
          : classicCupLabel(type);
      groups.putIfAbsent(label, () => []).add(match);
    }
    final entries = groups.entries.toList()
      ..sort((a, b) {
        final order = [
          '1ª DIVISÃO',
          '2ª DIVISÃO',
          '3ª DIVISÃO',
          '4ª DIVISÃO',
          'DISTRITAIS',
        ];
        final ai = order.indexOf(a.key), bi = order.indexOf(b.key);
        return (ai < 0 ? 99 : ai).compareTo(bi < 0 ? 99 : bi);
      });
    if (onSelected != null) {
      return Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          for (final group in entries)
            Container(
              margin: const EdgeInsets.only(bottom: 5),
              decoration: BoxDecoration(
                color: ClassicTheme.of(context).surface,
                border: Border.all(color: ClassicTheme.of(context).border),
              ),
              child: mobile
                  ? ExpansionTile(
                      key: PageStorageKey(
                        'live-group-$selectionScope-${group.key}',
                      ),
                      initiallyExpanded:
                          group.value.any(
                            (m) => '${m['matchId']}' == selectedMatchId,
                          ) ||
                          group == entries.first,
                      dense: true,
                      tilePadding: const EdgeInsets.symmetric(horizontal: 8),
                      title: Text(
                        group.key,
                        style: TextStyle(
                          color: ClassicTheme.of(context).accent,
                          fontWeight: FontWeight.bold,
                          fontSize: 13,
                        ),
                      ),
                      children: [
                        for (final match in group.value)
                          _selectionRow(context, match),
                      ],
                    )
                  : Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        Padding(
                          padding: const EdgeInsets.symmetric(
                            horizontal: 8,
                            vertical: 5,
                          ),
                          child: Text(
                            group.key,
                            key: ValueKey('division-heading-${group.key}'),
                            style: TextStyle(
                              color: ClassicTheme.of(context).accent,
                              fontSize: 14,
                              fontWeight: FontWeight.bold,
                            ),
                          ),
                        ),
                        for (final match in group.value)
                          _selectionRow(context, match),
                      ],
                    ),
            ),
        ],
      );
    }
    return Center(
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: 940),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            for (final group in entries)
              Padding(
                padding: const EdgeInsets.only(bottom: 10),
                child: DecoratedBox(
                  decoration: BoxDecoration(
                    color: ClassicTheme.of(context).surface,
                    border: Border.all(color: ClassicTheme.of(context).border),
                  ),
                  child: group.value.length > 8
                      ? ExpansionTile(
                          key: PageStorageKey('match-group-${group.key}'),
                          initiallyExpanded: group.value.any(_isCoachMatch),
                          title: Text(
                            '${group.key} • ${group.value.length} JOGOS',
                            style: TextStyle(
                              color: ClassicTheme.of(context).accent,
                              fontWeight: FontWeight.bold,
                              fontSize: 13,
                            ),
                          ),
                          children: _rows(context, group.value),
                        )
                      : Column(
                          crossAxisAlignment: CrossAxisAlignment.stretch,
                          children: [
                            Padding(
                              padding: const EdgeInsets.symmetric(
                                horizontal: 10,
                                vertical: 7,
                              ),
                              child: Text(
                                group.key,
                                key: ValueKey('division-heading-${group.key}'),
                                style: TextStyle(
                                  color: ClassicTheme.of(context).accent,
                                  fontSize: 14,
                                  fontWeight: FontWeight.bold,
                                ),
                              ),
                            ),
                            ..._rows(context, group.value),
                          ],
                        ),
                ),
              ),
          ],
        ),
      ),
    );
  }

  Widget _selectionRow(BuildContext context, Map<String, dynamic> match) {
    final id = '${match['matchId']}',
        mine = _isCoachMatch(match),
        selected = id == selectedMatchId;
    final t = ClassicTheme.of(context);
    final attendance = officialGateValue(match, 'attendance');
    final audience = attendance == null
        ? '—'
        : attendance
              .toStringAsFixed(0)
              .replaceAllMapped(
                RegExp(r'(\d)(?=(\d{3})+$)'),
                (m) => '${m[1]}.',
              );
    Widget club(String side) => Expanded(
      child: Tooltip(
        message: '${match['${side}ClubName'] ?? ''}',
        child: TeamColorName(
          name: '${match['${side}ClubName'] ?? ''}',
          palette: match['${side}ColorPalette'] is Map
              ? Map<String, dynamic>.from(match['${side}ColorPalette'] as Map)
              : null,
          highlight: mine,
        ),
      ),
    );
    return KeyedSubtree(
      key: mine
          ? const ValueKey('coach-highlighted-match-card')
          : ValueKey('observed-match-$id'),
      child: Semantics(
        button: true,
        selected: selected,
        label: mine ? 'Partida do seu clube' : null,
        child: InkWell(
          key: ValueKey('select-match-$id'),
          onTap: () => onSelected!(match),
          child: Container(
            key: ValueKey('classic-match-$id'),
            constraints: BoxConstraints(minHeight: mobile ? 36 : 30),
            margin: const EdgeInsets.symmetric(horizontal: 3, vertical: 1),
            decoration: BoxDecoration(
              border: Border.all(
                color: selected ? t.accent : Colors.transparent,
                width: 2,
              ),
              borderRadius: BorderRadius.circular(3),
            ),
            padding: const EdgeInsets.symmetric(horizontal: 3, vertical: 2),
            child: Row(
              children: [
                SizedBox(
                  width: mobile ? 12 : 16,
                  child: mine
                      ? Icon(
                          Icons.sports_esports,
                          size: mobile ? 12 : 15,
                          color: t.accent,
                        )
                      : null,
                ),
                Tooltip(
                  message: attendance == null
                      ? 'Público: ${unreportedGateLabel(match)}'
                      : 'Público pagante: $audience',
                  child: SizedBox(
                    width: mobile ? 40 : 53,
                    child: Text(
                      audience,
                      textAlign: TextAlign.right,
                      style: TextStyle(
                        color: t.textPrimary,
                        fontSize: mobile ? 10 : 11,
                      ),
                    ),
                  ),
                ),
                const SizedBox(width: 4),
                club('home'),
                Container(
                  key: ValueKey('score-$id'),
                  width: mobile ? 58 : 66,
                  alignment: Alignment.center,
                  padding: const EdgeInsets.symmetric(vertical: 3),
                  color: t.accent,
                  child: Text(
                    '${match['homeGoals'] ?? '—'} x ${match['awayGoals'] ?? '—'}',
                    style: TextStyle(
                      color: t.selectedText,
                      fontWeight: FontWeight.w900,
                      fontSize: mobile ? 12 : 14,
                    ),
                  ),
                ),
                club('away'),
              ],
            ),
          ),
        ),
      ),
    );
  }

  bool _isCoachMatch(Map<String, dynamic> match) =>
      coachClubId != null &&
      (match['homeClubId']?.toString() == coachClubId ||
          match['awayClubId']?.toString() == coachClubId);
  List<Widget> _rows(BuildContext context, List<Map<String, dynamic>> games) =>
      [for (int i = 0; i < games.length; i++) _row(context, games[i], i)];
  Widget _row(BuildContext context, Map<String, dynamic> match, int index) {
    final mine = _isCoachMatch(match);
    final id =
        match['matchId']?.toString() ??
        '${match['homeClubId']}:${match['awayClubId']}:$index';
    final home = match['homeClubName']?.toString() ?? '',
        away = match['awayClubName']?.toString() ?? '';
    final pens =
        match['homePenaltyGoals'] != null && match['awayPenaltyGoals'] != null
        ? '${match['homePenaltyGoals']}–${match['awayPenaltyGoals']} pên.'
        : null;
    return KeyedSubtree(
      key: PageStorageKey('match-expansion-$id'),
      child: DecoratedBox(
        key: ValueKey('classic-match-$id'),
        decoration: BoxDecoration(
          color: mine ? ClassicTheme.of(context).surfaceAlternate : null,
          border: Border(
            top: BorderSide(
              color: mine
                  ? ClassicTheme.of(context).border
                  : ClassicTheme.of(context).border,
            ),
            left: BorderSide(
              color: mine
                  ? ClassicTheme.of(context).border
                  : Colors.transparent,
              width: 3,
            ),
          ),
        ),
        child: ExpansionTile(
          key: mine
              ? const ValueKey('coach-highlighted-match-card')
              : ValueKey('match-details-$id'),
          subtitle:
              mine &&
                  match['homePlayersOnPitch'] != null &&
                  match['awayPlayersOnPitch'] != null
              ? Text(
                  '${match['homePlayersOnPitch']} em campo × ${match['awayPlayersOnPitch']} em campo',
                  textAlign: TextAlign.center,
                  style: const TextStyle(fontSize: 10),
                )
              : null,
          tilePadding: const EdgeInsets.symmetric(horizontal: 6),
          minTileHeight: 38,
          dense: true,
          childrenPadding: const EdgeInsets.all(8),
          title: Row(
            children: [
              SizedBox(
                width: 30,
                child: Text(
                  match['isFinished'] == true
                      ? 'FIM'
                      : "${match['minute'] ?? 0}'",
                  style: TextStyle(
                    color: ClassicTheme.of(context).accent,
                    fontSize: 10,
                  ),
                ),
              ),
              Expanded(
                child: TeamColorName(
                  name: home,
                  palette: match['homeColorPalette'] is Map
                      ? Map<String, dynamic>.from(
                          match['homeColorPalette'] as Map,
                        )
                      : null,
                  textAlign: TextAlign.right,
                  highlight: mine,
                ),
              ),
              SizedBox(
                width: 74,
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    Text(
                      '${match['homeGoals'] ?? 0} - ${match['awayGoals'] ?? 0}',
                      key: ValueKey('score-$id'),
                      textAlign: TextAlign.center,
                      style: TextStyle(
                        color: ClassicTheme.of(context).accent,
                        fontSize: 14,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                    if (pens != null)
                      Text(
                        pens,
                        textAlign: TextAlign.center,
                        style: TextStyle(
                          fontSize: 9,
                          color: ClassicTheme.of(context).textSecondary,
                        ),
                      ),
                  ],
                ),
              ),
              Expanded(
                child: TeamColorName(
                  name: away,
                  palette: match['awayColorPalette'] is Map
                      ? Map<String, dynamic>.from(
                          match['awayColorPalette'] as Map,
                        )
                      : null,
                  highlight: mine,
                ),
              ),
            ],
          ),
          children: [
            Text(
              '${match['homeFormation'] ?? ''} × ${match['awayFormation'] ?? ''} • SUBSTITUIÇÕES ${match['homeSubCount'] ?? 0}/${match['homeMaxSubs'] ?? 5} — ${match['awaySubCount'] ?? 0}/${match['awayMaxSubs'] ?? 5}',
              style: const TextStyle(fontSize: 11),
            ),
            if (match['isExtraTime'] == true)
              Text(
                'PRORROGAÇÃO',
                style: TextStyle(color: ClassicTheme.of(context).accent),
              ),
            if (mine && coachDetails != null)
              coachDetails!(match)
            else
              for (final event in match['events'] as List? ?? [])
                Text(
                  "${event['minute']}' • ${classicEventBadge(Map<String, dynamic>.from(event as Map))} ${event['description']}",
                  style: const TextStyle(fontSize: 11),
                ),
          ],
        ),
      ),
    );
  }
}
