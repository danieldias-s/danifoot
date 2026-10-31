import 'package:flutter/material.dart';

const _gold = Color(0xFFFFD700);
const _green = Color(0xFF062E0F);
String classicDivisionLabel(dynamic tier) => switch (tier?.toString()) {
  'PrimeiraDivisao' || '1' => '1ª DIVISÃO',
  'SegundaDivisao' || '2' => '2ª DIVISÃO',
  'TerceiraDivisao' || '3' => '3ª DIVISÃO',
  'QuartaDivisao' || '4' => '4ª DIVISÃO',
  'Distrital' || '5' => 'DISTRITAIS',
  _ => 'CAMPEONATO — DIVISÃO NÃO INFORMADA',
};
String classicCupLabel(String type) => switch (type) {
  'CupPreliminary1' => 'TAÇA - PRELIMINAR 1',
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
    _ => '[LANCE]',
  };
}

class ClassicMatchBoard extends StatelessWidget {
  final List<Map<String, dynamic>> matches;
  final String competitionType, competitionLabel;
  final String? coachClubId;
  final Widget Function(Map<String, dynamic>)? coachDetails;
  const ClassicMatchBoard({
    super.key,
    required this.matches,
    required this.competitionType,
    required this.competitionLabel,
    this.coachClubId,
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
                    color: _green,
                    border: Border.all(color: const Color(0xFF3E8450)),
                  ),
                  child: group.value.length > 8
                      ? ExpansionTile(
                          key: PageStorageKey('match-group-${group.key}'),
                          initiallyExpanded: group.value.any(_isCoachMatch),
                          title: Text(
                            '${group.key} • ${group.value.length} JOGOS',
                            style: const TextStyle(
                              color: _gold,
                              fontWeight: FontWeight.bold,
                              fontSize: 13,
                            ),
                          ),
                          children: _rows(group.value),
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
                                style: const TextStyle(
                                  color: _gold,
                                  fontSize: 14,
                                  fontWeight: FontWeight.bold,
                                ),
                              ),
                            ),
                            ..._rows(group.value),
                          ],
                        ),
                ),
              ),
          ],
        ),
      ),
    );
  }

  bool _isCoachMatch(Map<String, dynamic> match) =>
      coachClubId != null &&
      (match['homeClubId']?.toString() == coachClubId ||
          match['awayClubId']?.toString() == coachClubId);
  List<Widget> _rows(List<Map<String, dynamic>> games) => [
    for (int i = 0; i < games.length; i++) _row(games[i], i),
  ];
  Widget _row(Map<String, dynamic> match, int index) {
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
          color: mine ? const Color(0xFF174F26) : null,
          border: Border(
            top: BorderSide(color: mine ? _gold : const Color(0xFF205B30)),
            left: BorderSide(
              color: mine ? _gold : Colors.transparent,
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
                  style: const TextStyle(color: _gold, fontSize: 10),
                ),
              ),
              Expanded(
                child: Tooltip(
                  message: home,
                  child: Text(
                    home,
                    textAlign: TextAlign.right,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: TextStyle(
                      fontSize: 12,
                      fontWeight: mine ? FontWeight.bold : FontWeight.normal,
                    ),
                  ),
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
                      style: const TextStyle(
                        color: _gold,
                        fontSize: 14,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                    if (pens != null)
                      Text(
                        pens,
                        textAlign: TextAlign.center,
                        style: const TextStyle(
                          fontSize: 9,
                          color: Colors.white70,
                        ),
                      ),
                  ],
                ),
              ),
              Expanded(
                child: Tooltip(
                  message: away,
                  child: Text(
                    away,
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: TextStyle(
                      fontSize: 12,
                      fontWeight: mine ? FontWeight.bold : FontWeight.normal,
                    ),
                  ),
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
              const Text('PRORROGAÇÃO', style: TextStyle(color: _gold)),
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
