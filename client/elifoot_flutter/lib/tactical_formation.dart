/// Client-side selection and schematic only; nominal positions and strengths stay untouched.
String _position(Map<String, dynamic> player) {
  final raw = player['position'];
  return raw is num
      ? const ['G', 'D', 'M', 'A'][raw.toInt().clamp(0, 3)]
      : raw?.toString() ?? '';
}

List<int> _sectors(String formation) =>
    formation.split('-').map(int.parse).toList();
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
