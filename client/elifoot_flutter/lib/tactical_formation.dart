/// Client-side selection and schematic only; nominal positions and strengths stay untouched.
String _position(Map<String, dynamic> player) {
  final raw = player['position'];
  return raw is num
      ? const ['G', 'D', 'M', 'A'][raw.toInt().clamp(0, 3)]
      : raw?.toString() ?? '';
}

List<int> _sectors(String formation) =>
    formation.split('-').map(int.parse).toList();
bool isValidTacticalFormation(String formation) {
  final parts = formation.split('-').map(int.tryParse).toList();
  return parts.length == 3 &&
      parts.every((n) => n != null && n >= 0 && n <= 10) &&
      parts.cast<int>().reduce((a, b) => a + b) == 10 &&
      parts.join('-') == formation;
}

String formationFromPlayers(
  List<Map<String, dynamic>> players,
  String fallback,
) {
  if (players.length != 11 ||
      players.where((p) => _position(p) == 'G').length != 1) {
    return fallback;
  }
  return [
    'D',
    'M',
    'A',
  ].map((pos) => players.where((p) => _position(p) == pos).length).join('-');
}

({List<String> starters, List<String> bench, String formation})?
swapSquadPlayers(
  List<Map<String, dynamic>> squad,
  List<String> starters,
  List<String> bench,
  String playerOut,
  String playerIn,
  String formation,
) {
  if (!starters.contains(playerOut) || !bench.contains(playerIn)) return null;
  final incoming = squad.where((p) => '${p['id']}' == playerIn).firstOrNull;
  if (incoming == null || !_available(incoming)) return null;
  final next = [for (final id in starters) id == playerOut ? playerIn : id];
  final selected = squad.where((p) => next.contains('${p['id']}')).toList();
  final availableGk = squad.any((p) => _position(p) == 'G' && _available(p));
  if (selected.where((p) => _position(p) == 'G').length !=
      (availableGk ? 1 : 0)) {
    return null;
  }
  return (
    starters: next,
    bench: [for (final id in bench) id == playerIn ? playerOut : id],
    formation: formationFromPlayers(selected, formation),
  );
}

bool _available(Map<String, dynamic> player) =>
    (player['injuryDaysRemaining'] as num? ?? 0) == 0 &&
    (player['injuryRoundsRemaining'] as num? ?? 0) == 0 &&
    (player['suspensionRoundsRemaining'] as num? ?? 0) == 0;

/// Keep the coach's eligible starters in each sector before promoting reserves.
/// Only the changes needed by the new formation are made; shortages remain visible.
({List<String> starters, List<String> bench}) lineupForFormation(
  List<Map<String, dynamic>> squad,
  String formation,
  List<String> currentStarters,
  List<String> currentBench,
) {
  final sectors = _sectors(formation);
  final eligible = squad.where(_available).toList();
  final previous = currentStarters.toSet();
  eligible.sort((a, b) {
    final retained = (previous.contains(b['id']) ? 1 : 0).compareTo(
      previous.contains(a['id']) ? 1 : 0,
    );
    if (retained != 0) return retained;
    final strength = (b['strength'] as num? ?? 0).compareTo(
      a['strength'] as num? ?? 0,
    );
    return strength != 0 ? strength : '${a['id']}'.compareTo('${b['id']}');
  });
  final chosen = <Map<String, dynamic>>[];
  for (final sector in [
    ('G', 1),
    ('D', sectors[0]),
    ('M', sectors[1]),
    ('A', sectors[2]),
  ]) {
    chosen.addAll(
      eligible.where((p) => _position(p) == sector.$1).take(sector.$2),
    );
  }
  final ids = chosen.map((p) => '${p['id']}').toSet();
  for (final p in eligible) {
    if (ids.length >= 11) break;
    if (_position(p) != 'G') ids.add('${p['id']}');
  }
  final unused = eligible.where((p) => !ids.contains('${p['id']}')).toList();
  unused.sort((a, b) {
    final ai = currentBench.indexOf('${a['id']}'),
        bi = currentBench.indexOf('${b['id']}');
    if (ai >= 0 || bi >= 0) {
      return (ai < 0 ? 100 : ai).compareTo(bi < 0 ? 100 : bi);
    }
    return '${a['id']}'.compareTo('${b['id']}');
  });
  return (
    starters: ids.toList(),
    bench: unused.take(9).map((p) => '${p['id']}').toList(),
  );
}

({List<String> starters, List<String> bench, String formation}) bestSquadLineup(
  List<Map<String, dynamic>> squad,
  String currentFormation, {
  required bool preferGoals,
}) {
  final eligible = squad.where(_available).toList();
  int compareStrength(Map<String, dynamic> a, Map<String, dynamic> b) {
    final strength = (b['strength'] as num? ?? 0).compareTo(
      a['strength'] as num? ?? 0,
    );
    return strength == 0 ? '${a['id']}'.compareTo('${b['id']}') : strength;
  }

  final keepers = eligible.where((p) => _position(p) == 'G').toList()
    ..sort(compareStrength);
  final outfield = eligible.where((p) => _position(p) != 'G').toList()
    ..sort((a, b) {
      if (preferGoals) {
        final goals = (b['seasonGoals'] as num? ?? 0).compareTo(
          a['seasonGoals'] as num? ?? 0,
        );
        if (goals != 0) return goals;
      }
      return compareStrength(a, b);
    });
  final starters = [...keepers.take(1), ...outfield.take(10)];
  final reserves = [
    ...keepers.skip(1).take(1),
    ...outfield.skip(10),
    ...keepers.skip(2),
  ].take(9);
  return (
    starters: starters.map((p) => '${p['id']}').toList(),
    bench: reserves.map((p) => '${p['id']}').toList(),
    formation: formationFromPlayers(starters, currentFormation),
  );
}

/// Slots follow the chosen shape, including legal out-of-position players.
/// Every selected player remains present once, with their actual nominal position.
List<List<Map<String, dynamic>>> tacticalPitchLines(
  List<Map<String, dynamic>> starters,
  String formation,
) {
  final sectors = _sectors(formation);
  final rows = <List<Map<String, dynamic>>>[[], [], [], []]; // G, D, M, A
  final limits = [1, ...sectors];
  const positions = ['G', 'D', 'M', 'A'];
  final remaining = List<Map<String, dynamic>>.from(starters);
  for (int row = 0; row < rows.length; row++) {
    rows[row].addAll(
      remaining.where((p) => _position(p) == positions[row]).take(limits[row]),
    );
    remaining.removeWhere((p) => rows[row].contains(p));
  }
  for (final p in List<Map<String, dynamic>>.from(remaining)) {
    for (int row = 1; row < rows.length; row++) {
      if (_position(p) != 'G' && rows[row].length < limits[row]) {
        rows[row].add(p);
        remaining.remove(p);
        break;
      }
    }
  }
  // An invalid manual selection is shown, never silently hidden or repaired here.
  for (final p in remaining) {
    rows[_position(p) == 'G' ? 0 : 1].add(p);
  }
  return rows.reversed.toList();
}
