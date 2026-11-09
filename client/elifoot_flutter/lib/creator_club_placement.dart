/// The ordered club identities of an existing preview. A move exchanges slots;
/// it never changes a club, its catalog variant or its roster.
class CreatorClubPlacement {
  final List<List<String>> divisions;

  CreatorClubPlacement._(this.divisions);

  factory CreatorClubPlacement.fromPreview(List<Map<String, dynamic>> clubs) {
    final divisions = List.generate(5, (_) => <String>[]);
    for (final entry in clubs) {
      final division = entry['division'];
      final key = (entry['club'] as Map?)?['key']?.toString();
      if (division is! int ||
          division < 1 ||
          division > 5 ||
          key == null ||
          key.isEmpty) {
        throw const FormatException(
          'A prévia contém um clube ou uma divisão inválida.',
        );
      }
      divisions[division - 1].add(key);
    }
    final keys = divisions.expand((keys) => keys).toList();
    if (keys.length != 128 ||
        keys.toSet().length != keys.length ||
        divisions.take(4).any((keys) => keys.length != 8) ||
        divisions[4].length != 96) {
      throw const FormatException(
        'A montagem precisa de 4 divisões com 8 clubes e 96 distritais, sem repetições.',
      );
    }
    return CreatorClubPlacement._(divisions);
  }

  CreatorClubPlacement swap(String current, String replacement) {
    final result = divisions.map((keys) => List<String>.of(keys)).toList();
    (int, int)? locate(String key) {
      for (var division = 0; division < result.length; division++) {
        final position = result[division].indexOf(key);
        if (position >= 0) return (division, position);
      }
      return null;
    }

    final from = locate(current), to = locate(replacement);
    if (from == null || to == null || current == replacement) {
      throw const FormatException(
        'Selecione outro clube da montagem para fazer a troca.',
      );
    }
    result[from.$1][from.$2] = replacement;
    result[to.$1][to.$2] = current;
    return CreatorClubPlacement._(result);
  }

  List<Map<String, dynamic>> get assignments => [
    for (final (index, keys) in divisions.indexed)
      for (final key in keys) {'clubKey': key, 'division': index + 1},
  ];
}
