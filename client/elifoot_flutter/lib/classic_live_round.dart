import 'package:flutter/material.dart';
import 'classic98_widgets.dart';
import 'club_theme.dart';
import 'live_match_data.dart';
import 'match_gate.dart';

/// Compact display of official events; never generates commentary or scores.
String classicLiveEventText(Map<String, dynamic>? event) {
  if (event == null) return 'Sem lance registrado';
  final kind = _liveEventKind(event);
  final kick = event['penaltyKick'] as Map?;
  final player =
      (kick?['taker'] as Map?)?['displayName']?.toString() ??
      event['primaryPlayerDisplayName']?.toString();
  final secondary = event['secondaryPlayerDisplayName']?.toString();
  String withPlayer(String label) =>
      player == null || player.isEmpty ? label : '$label $player';
  final description = event['description']?.toString().replaceFirst(
    RegExp(r"^\d+['’′]\s*"),
    '',
  );
  final label = switch (kind) {
    'Goal' => withPlayer('Gol!'),
    'OwnGoal' => withPlayer('Gol contra!'),
    'PenaltyGoal' => withPlayer('Pênalti convertido!'),
    'YellowCard' => player ?? 'Cartão amarelo',
    'DirectRedCard' => player ?? 'Cartão vermelho',
    'SecondYellowRedCard' => player ?? 'Segundo amarelo e expulsão',
    'Injury' => withPlayer('Lesão!'),
    'Substitution' when secondary != null && player != null =>
      'Sai: $secondary → Entra: $player',
    'Substitution' => description ?? 'Substituição',
    'TacticChange' => description ?? 'Alteração tática',
    'PenaltyAwarded' => withPlayer('Pênalti'),
    'PenaltySaved' when secondary != null =>
      "${withPlayer('Pênalti defendido!')} (defesa de $secondary)",
    'PenaltySaved' => withPlayer('Pênalti defendido!'),
    'PenaltyWide' => withPlayer('Pênalti para fora!'),
    'PenaltyMissed' || 'PenaltyShootoutMiss' => withPlayer('Pênalti perdido'),
    'PenaltyShootoutGoal' => description ?? withPlayer('Gol nos pênaltis'),
    'Offside' => withPlayer('Impedimento'),
    'HalftimeReached' => 'Intervalo',
    'ExtraTimeStarted' => 'Prorrogação',
    'GlobalPauseTriggered' => description ?? 'Pausa',
    _ => description ?? withPlayer('Lance registrado'),
  };
  return event['minute'] == null ? label : '$label (${event['minute']}′)';
}

String _liveEventKind(Map<String, dynamic> event) {
  final type = eventType(event);
  final kick = event['penaltyKick'] as Map?;
  final description = (event['description'] as String? ?? '').toUpperCase();
  return switch (type) {
    'Goal' when event['isOwnGoal'] == true => 'OwnGoal',
    'Goal'
        when kick?['outcome'] == 'Goal' ||
            (kick == null && description.contains('(PÊNALTI)')) =>
      'PenaltyGoal',
    'PenaltyMissed'
        when kick?['outcome'] == 'Saved' ||
            (kick == null && description.contains('PÊNALTI DEFENDIDO!')) =>
      'PenaltySaved',
    'PenaltyMissed'
        when kick?['outcome'] == 'Wide' ||
            (kick == null && description.contains('PÊNALTI PARA FORA!')) =>
      'PenaltyWide',
    _ => type,
  };
}

Widget? _liveEventIcon(String kind) {
  const red = Color(0xFFC52025);
  const yellow = Color(0xFFFFD937);
  Widget card(Color color) => Container(
    width: 7,
    height: 10,
    decoration: BoxDecoration(
      color: color,
      border: Border.all(color: const Color(0xFF493F31), width: .6),
    ),
  );
  return switch (kind) {
    'YellowCard' => card(yellow),
    'DirectRedCard' => card(red),
    'SecondYellowRedCard' => SizedBox(
      width: 11,
      height: 13,
      child: Stack(
        children: [
          Positioned(left: 0, top: 0, child: card(yellow)),
          Positioned(right: 0, bottom: 0, child: card(red)),
        ],
      ),
    ),
    'Injury' => const Icon(Icons.add_box_sharp, color: red, size: 14),
    'Goal' ||
    'OwnGoal' ||
    'PenaltyGoal' ||
    'PenaltyAwarded' ||
    'PenaltyShootoutGoal' => const Icon(Icons.sports_soccer, size: 13),
    'PenaltyWide' || 'PenaltyMissed' || 'PenaltyShootoutMiss' => const Icon(
      Icons.close_sharp,
      size: 14,
      color: red,
    ),
    'PenaltySaved' => const Icon(Icons.pan_tool_sharp, size: 13),
    'Offside' => const Icon(Icons.flag_sharp, size: 13),
    _ => null,
  };
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

    final events = officialMatchEvents(match);
    Widget event(String side) {
      final club = match['${side}ClubId'];
      final last = events.reversed
          .where((e) => e['clubId'] == club)
          .firstOrNull;
      final text = classicLiveEventText(last);
      final kind = last == null ? '' : _liveEventKind(last);
      final icon = _liveEventIcon(kind);
      final clubName = _clubName(match, side);
      final style = TextStyle(
        color: Colors.black,
        fontFamily: 'ClassicCondensed',
        fontWeight: FontWeight.w600,
        fontSize: mobile ? 11 : 14,
        height: 1.1,
      );
      final entry = last?['primaryPlayerDisplayName'];
      final exit = last?['secondaryPlayerDisplayName'];
      final substitution =
          kind == 'Substitution' && entry != null && exit != null;
      final detail = switch (kind) {
        'YellowCard' when entry != null => 'Cartão amarelo • $text',
        'DirectRedCard' when entry != null => 'Cartão vermelho • $text',
        'SecondYellowRedCard' when entry != null =>
          'Segundo amarelo e expulsão • $text',
        _ => text,
      };
      return Tooltip(
        key: ValueKey('classic-last-event-$id-$side'),
        message: '$clubName • $detail',
        child: Container(
          constraints: BoxConstraints(minHeight: mobile ? 54 : 36),
          alignment: Alignment.centerLeft,
          padding: EdgeInsets.symmetric(
            horizontal: mobile ? 2 : 5,
            vertical: 3,
          ),
          decoration: BoxDecoration(
            color: mobile ? const Color(0xFFD9F0CF) : cream,
            border: Border.all(color: const Color(0xFF898B7C), width: .6),
          ),
          child: Row(
            children: [
              if (icon != null) ...[icon, const SizedBox(width: 3)],
              Expanded(
                child: substitution
                    ? Text.rich(
                        TextSpan(
                          children: [
                            const TextSpan(
                              text: 'Sai: ',
                              style: TextStyle(color: Color(0xFF9E1C22)),
                            ),
                            TextSpan(text: '$exit → '),
                            const TextSpan(
                              text: 'Entra: ',
                              style: TextStyle(color: Color(0xFF125F34)),
                            ),
                            TextSpan(text: '$entry (${last!['minute']}′)'),
                          ],
                        ),
                        maxLines: mobile ? 3 : 1,
                        overflow: TextOverflow.ellipsis,
                        style: style,
                      )
                    : Text(
                        text,
                        maxLines: mobile ? 3 : 1,
                        overflow: TextOverflow.ellipsis,
                        style: style,
                      ),
              ),
            ],
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
                Expanded(
                  flex: 31,
                  child: Row(
                    children: [
                      Expanded(child: event('home')),
                      Expanded(child: event('away')),
                    ],
                  ),
                ),
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
