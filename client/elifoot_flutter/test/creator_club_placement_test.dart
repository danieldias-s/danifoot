import 'package:elifoot_flutter/creator_club_placement.dart';
import 'package:flutter_test/flutter_test.dart';

List<Map<String, dynamic>> previewClubs() => [
  for (var index = 0; index < 128; index++)
    {
      'division': index < 32 ? index ~/ 8 + 1 : 5,
      'club': {'key': 'PT:${index.isEven ? 1985 : 2025}:club-$index:Male'},
    },
];

void main() {
  test(
    'reciprocal swaps preserve every slot and the full catalog identity',
    () {
      final original = CreatorClubPlacement.fromPreview(previewClubs());
      final first = original.divisions[0][0],
          district = original.divisions[4][95];
      final swapped = original.swap(first, district);
      expect(swapped.divisions[0][0], district);
      expect(swapped.divisions[4][95], first);
      expect(original.divisions[0][0], first);
      expect(swapped.divisions.map((keys) => keys.length), [8, 8, 8, 8, 96]);
      expect(
        swapped.assignments.map((entry) => entry['clubKey']).toSet().length,
        128,
      );
      expect(swapped.swap(district, first).assignments, original.assignments);
    },
  );

  test(
    'same division and cross division exchanges keep the configured order',
    () {
      final original = CreatorClubPlacement.fromPreview(previewClubs());
      final first = original.divisions[1][0], last = original.divisions[1][7];
      final sameDivision = original.swap(first, last);
      expect(sameDivision.divisions[1][0], last);
      expect(sameDivision.divisions[1][7], first);
      final third = original.divisions[2][4];
      final crossDivision = sameDivision.swap(first, third);
      expect(crossDivision.divisions[1][7], third);
      expect(crossDivision.divisions[2][4], first);
    },
  );

  test('incomplete or duplicate lineups cannot become a valid preview', () {
    final missing = previewClubs()..removeLast();
    expect(
      () => CreatorClubPlacement.fromPreview(missing),
      throwsFormatException,
    );
    final duplicate = previewClubs();
    duplicate.last['club'] = duplicate.first['club'];
    expect(
      () => CreatorClubPlacement.fromPreview(duplicate),
      throwsFormatException,
    );
    final invalidDivision = previewClubs();
    invalidDivision.last['division'] = 6;
    expect(
      () => CreatorClubPlacement.fromPreview(invalidDivision),
      throwsFormatException,
    );
  });

  test('swap rejects an unassigned club and identical identities', () {
    final original = CreatorClubPlacement.fromPreview(previewClubs());
    final first = original.divisions[0][0];
    expect(
      () => original.swap(first, 'not-in-this-preview'),
      throwsFormatException,
    );
    expect(() => original.swap(first, first), throwsFormatException);
  });
}
