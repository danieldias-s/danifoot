import 'package:flutter/material.dart';
import 'classic98_widgets.dart';
import 'club_theme.dart';
import 'live_match_data.dart';
import 'match_gate.dart';

/// Compact display of official events; never generates commentary or scores.
String classicLiveEventText(Map<String, dynamic>? event) {
  if (event == null) return 'Sem lance registrado';
  final label = switch (eventType(event)) {
    'Goal' => 'Gol!',
    'YellowCard' => 'Cartão amarelo',
    'DirectRedCard' || 'SecondYellowRedCard' => 'Expulsão',
    'Injury' => 'Lesão',
    'Substitution' => 'Substituição',
    'TacticChange' => 'Alteração tática',
    'PenaltyAwarded' => 'Pênalti',
    'PenaltyMissed' => 'Pênalti perdido',
    'PenaltyShootoutGoal' => 'Gol nos pênaltis',
    'PenaltyShootoutMiss' => 'Pênalti perdido',
    'Offside' => 'Impedimento',
    'HalftimeReached' => 'Intervalo',
    'ExtraTimeStarted' => 'Prorrogação',
    'GlobalPauseTriggered' => 'Pausa',
    _ => '${event['description'] ?? 'Lance registrado'}',
  };
  return event['minute'] == null ? label : '$label (${event['minute']}′)';
}

class ClassicReferenceMatchList extends StatelessWidget {
  final List<MapEntry<String, List<Map<String, dynamic>>>> groups;
  final Map<String, Map<String, dynamic>> clubs;
  final String? selectedMatchId, coachClubId;
  final bool mobile;
  final ValueChanged<Map<String, dynamic>> onSelected;
  final void Function(Map<String, dynamic>, String) onClubTap;
  const ClassicReferenceMatchList({
    super.key,
    required this.groups,
    required this.clubs,
    required this.mobile,
    required this.onSelected,
    required this.onClubTap,
    this.selectedMatchId,
    this.coachClubId,
  });

  static const silver = ClassicColors.retroGray,
      navy = ClassicColors.titleBarBlue,
      green = ClassicColors.outerGreen,
      cream = ClassicColors.cream;

  String _clubName(Map<String, dynamic> match, String side) {
    final full = '${match['${side}ClubName'] ?? '—'}';
    final short = clubs['${match['${side}ClubId']}']?['shortName'];
    return mobile || short == null || '$short'.isEmpty ? full : '$short';
  }

  Map<String, dynamic>? _last(Map<String, dynamic> match, [String? side]) {
    final events = officialMatchEvents(match);
    if (side == null) return events.lastOrNull;
    return events
        .where((e) => e['clubId'] == match['${side}ClubId'])
        .lastOrNull;
  }

  @override
  Widget build(BuildContext context) => Container(
    key: const Key('classic-reference-round-list'),
    color: mobile ? silver : green,
    padding: const EdgeInsets.all(5),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        if (groups.isEmpty)
          const Padding(
            padding: EdgeInsets.all(16),
            child: Text('Aguardando confrontos da rodada.'),
          ),
        for (final group in groups)
          Container(
            margin: const EdgeInsets.only(bottom: 7),
            decoration: BoxDecoration(
              border: Border.all(
                color: mobile ? Colors.white : const Color(0xFF72966F),
                width: 2,
              ),
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Container(
                  key: ValueKey('division-heading-${group.key}'),
                  padding: EdgeInsets.symmetric(
                    horizontal: 8,
                    vertical: mobile ? 7 : 4,
                  ),
                  decoration: BoxDecoration(
                    gradient: LinearGradient(
                      colors: mobile
                          ? [const Color(0xFFE8E8EA), silver]
                          : [green, const Color(0xFF002D0C)],
                    ),
                    border: const Border(
                      bottom: BorderSide(color: Colors.white),
                    ),
                  ),
                  child: Text(
                    group.key,
                    style: TextStyle(
                      color: mobile ? navy : const Color(0xFFFFEA00),
                      fontSize: mobile ? 19 : 18,
                      fontWeight: FontWeight.w900,
                    ),
                  ),
                ),
                for (final match in group.value) _row(context, match),
              ],
            ),
          ),
      ],
    ),
  );

  String _audience(num? value) {
    if (value == null) return '—';
    final digits = value.toStringAsFixed(0);
    final result = StringBuffer();
    for (var i = 0; i < digits.length; i++) {
      if (i > 0 && (digits.length - i) % 3 == 0) result.write('.');
      result.write(digits[i]);
    }
    return result.toString();
  }

  Widget _row(BuildContext context, Map<String, dynamic> match) {
    final id = matchIdentity(match),
        selected = matchIdentity(match) == selectedMatchId;
    final t = ClassicTheme.of(context);
    final mine = isCoachMatch(match, coachClubId);
    Widget team(String side) {
      final colors = matchTeamColors(t, match['${side}ColorPalette'] as Map?);
      final full = '${match['${side}ClubName'] ?? '—'}';
      return Semantics(
        button: true,
        label: 'Abrir equipe $full',
        child: Tooltip(
          message: '$full — tática e jogadores da partida',
          child: Material(
            key: ValueKey('classic-team-color-$id-$side'),
            color: colors.background,
            child: InkWell(
              key: ValueKey('live-club-$id-$side'),
              onTap: () => onClubTap(match, side),
              child: Container(
                alignment: Alignment.center,
                constraints: BoxConstraints(minHeight: mobile ? 54 : 36),
                padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 3),
                decoration: BoxDecoration(
                  border: Border.all(color: Colors.white, width: .7),
                ),
                child: Text(
                  _clubName(match, side),
                  key: ValueKey('classic-team-name-$id-$side'),
                  maxLines: mobile ? 3 : 2,
                  overflow: TextOverflow.ellipsis,
                  textAlign: TextAlign.center,
                  style: TextStyle(
                    color: colors.ink,
                    fontFamily: 'ClassicCondensed',
                    fontSize: mobile ? 13 : 15,
                    fontWeight: FontWeight.bold,
                    height: 1.05,
                  ),
                ),
              ),
            ),
          ),
        ),
      );
    }

    Widget event([String? side]) {
      final last = _last(match, side);
      var text = classicLiveEventText(last);
      if (mobile && last != null) {
        final club = clubs['${last['clubId']}'];
        final name = club?['shortName'] ?? last['clubName'];
        if (name != null && '$name'.isNotEmpty) text = '$name: $text';
      }
      return Tooltip(
        message: '${last?['description'] ?? text}',
        child: Container(
          constraints: BoxConstraints(minHeight: mobile ? 54 : 36),
          alignment: Alignment.centerLeft,
          padding: const EdgeInsets.symmetric(horizontal: 5, vertical: 3),
          decoration: BoxDecoration(
            color: mobile ? const Color(0xFFD9F0CF) : cream,
            border: Border.all(color: const Color(0xFF898B7C), width: .6),
          ),
          child: Text(
            text,
            maxLines: mobile ? 3 : 2,
            overflow: TextOverflow.ellipsis,
            style: TextStyle(
              color: Colors.black,
              fontWeight: FontWeight.w600,
              fontSize: mobile ? 11 : 14,
              height: 1.1,
            ),
          ),
        ),
      );
    }

    final score = Material(
      color: Colors.black,
      child: InkWell(
        key: ValueKey('select-match-$id'),
        onTap: () => onSelected(match),
        child: Container(
          alignment: Alignment.center,
          constraints: BoxConstraints(minHeight: mobile ? 54 : 36),
          decoration: BoxDecoration(
            border: Border.all(color: Colors.white, width: .7),
          ),
          child: Text(
            '${match['homeGoals'] ?? '—'} x ${match['awayGoals'] ?? '—'}',
            maxLines: 1,
            style: TextStyle(
              color: Colors.white,
              fontSize: mobile ? 15 : 17,
              fontWeight: FontWeight.w900,
            ),
          ),
        ),
      ),
    );
    final attendance = officialGateValue(match, 'attendance');
    final audience = _audience(attendance);
    return Container(
      key: mine
          ? const ValueKey('coach-highlighted-match-card')
          : ValueKey('observed-match-$id'),
      margin: const EdgeInsets.all(2),
      decoration: BoxDecoration(
        border: Border.all(
          color: selected ? const Color(0xFFFFDF39) : Colors.transparent,
          width: 1.4,
        ),
      ),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.center,
        children: mobile
            ? [
                Expanded(flex: 28, child: team('home')),
                Expanded(flex: 13, child: score),
                Expanded(flex: 28, child: team('away')),
                Expanded(flex: 31, child: event()),
              ]
            : [
                SizedBox(
                  width: 66,
                  child: Tooltip(
                    message: attendance == null
                        ? 'Público em apuração'
                        : 'Público pagante: $audience',
                    child: Text(
                      audience,
                      textAlign: TextAlign.center,
                      style: const TextStyle(
                        color: Colors.white,
                        fontWeight: FontWeight.bold,
                        fontSize: 15,
                      ),
                    ),
                  ),
                ),
                Expanded(flex: 2, child: team('home')),
                Expanded(flex: 3, child: event('home')),
                SizedBox(width: 76, child: score),
                Expanded(flex: 3, child: event('away')),
                Expanded(flex: 2, child: team('away')),
              ],
      ),
    );
  }
}
