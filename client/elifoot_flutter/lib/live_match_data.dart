import 'dart:convert';
import 'classic_match_board.dart' show classicDivisionLabel, classicCupLabel;

String matchIdentity(Map<String, dynamic> match) =>
    '${match['matchId'] ?? match['id'] ?? '${match['homeClubId']}:${match['awayClubId']}'}';
bool isCoachMatch(Map<String, dynamic> match, String? club) =>
    club != null &&
    (match['homeClubId'] == club || match['awayClubId'] == club);
String matchGroup(
  Map<String, dynamic> match,
  String fallbackType,
  String fallbackLabel,
) {
  final type = '${match['competitionType'] ?? fallbackType}';
  return type == 'League' || type == '0'
      ? classicDivisionLabel(match['divisionTier'])
      : type == fallbackType && fallbackLabel.isNotEmpty
      ? fallbackLabel.toUpperCase()
      : classicCupLabel(type);
}

String matchStatus(Map<String, dynamic> match, String stage) {
  if (match['isFinished'] == true) return 'Encerrada';
  if (match['_calendarOnly'] == true) return 'Não iniciada';
  if (stage == 'HALFTIME_PAUSED' ||
      stage == 'EXTRA_TIME_HALFTIME' ||
      stage == 'EXTRA_TIME_BREAK') {
    return 'Intervalo';
  }
  if (stage == 'GLOBAL_PAUSED') return 'Pausada';
  if ((match['minute'] as num? ?? 0) > 0 || stage.startsWith('RUNNING_')) {
    return 'Em andamento';
  }
  return 'Não iniciada';
}

String eventType(Map<String, dynamic> e) =>
    switch ('${e['eventType'] ?? e['type']}') {
      '0' => 'Goal',
      '1' => 'YellowCard',
      '2' => 'SecondYellowRedCard',
      '3' => 'DirectRedCard',
      '4' => 'Injury',
      '5' => 'Substitution',
      '6' => 'TacticChange',
      '7' => 'PenaltyMissed',
      '8' => 'Offside',
      '9' => 'PenaltyShootoutGoal',
      '10' => 'PenaltyShootoutMiss',
      '11' => 'GlobalPauseTriggered',
      '12' => 'HalftimeReached',
      '13' => 'ExtraTimeStarted',
      '15' => 'PenaltyAwarded',
      final String value => value,
    };

/// Every update is a full official snapshot, never an appended notification feed.
/// Preserve same-minute order and even identical official records: this DTO has no event id.
List<Map<String, dynamic>> officialMatchEvents(Map<String, dynamic> match) {
  final indexed = (match['events'] as List? ?? []).asMap().entries.toList()
    ..sort((a, b) {
      final minute = (a.value['minute'] as num? ?? 0).compareTo(
        b.value['minute'] as num? ?? 0,
      );
      return minute == 0 ? a.key.compareTo(b.key) : minute;
    });
  return indexed.map((e) => Map<String, dynamic>.from(e.value as Map)).toList();
}

/// Local selection is scoped to coach, room, season, round and club, not room commands.
class LiveMatchSelection {
  String? scope, matchId;
  void restore(Map<String, dynamic>? value) {
    scope = value?['scope']?.toString();
    matchId = value?['matchId']?.toString();
  }

  Map<String, dynamic> toJson() => {'scope': scope, 'matchId': matchId};
  void accept({
    required String world,
    required String coach,
    required String? club,
    required int season,
    required int round,
    required List<Map<String, dynamic>> matches,
  }) {
    final next = jsonEncode([world, coach, club, season, round]);
    if (scope != next || !matches.any((m) => matchIdentity(m) == matchId)) {
      scope = next;
      matchId = matches
          .where((m) => isCoachMatch(m, club))
          .firstOrNull?['matchId']
          ?.toString();
      matchId ??= matches.isEmpty ? null : matchIdentity(matches.first);
    }
  }

  void select(Map<String, dynamic> match) => matchId = matchIdentity(match);
}

/// Calendar exposes scores and club ids only. Do not copy live events, attendance or
/// revenue into other rounds, or pretend current stadium capacity is a historic value.
List<Map<String, dynamic>> calendarRoundMatches(
  Map<String, dynamic> round,
  Map<String, Map<String, dynamic>> clubs,
) => (round['fixtures'] as List? ?? []).map((item) {
  final f = Map<String, dynamic>.from(item as Map);
  final h = clubs['${f['homeClubId']}'], a = clubs['${f['awayClubId']}'];
  return <String, dynamic>{
    ...f,
    'matchId': f['id'],
    'homeClubName': f['home'],
    'awayClubName': f['away'],
    'competitionType': round['competition'],
    'divisionTier': h?['divisionTier'],
    'homeColorPalette': h?['colorPalette'],
    'awayColorPalette': a?['colorPalette'],
    '_calendarOnly': true,
  };
}).toList();
