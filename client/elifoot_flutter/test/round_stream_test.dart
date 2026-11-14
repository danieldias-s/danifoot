import 'package:elifoot_flutter/round_stream.dart';
import 'package:flutter_test/flutter_test.dart';

Map<String, dynamic> base({
  String epoch = 'first',
  int sequence = 10,
  int version = 20,
}) => {
  'gameWorldId': 'room',
  'realtimeEpoch': epoch,
  'realtimeSequence': sequence,
  'roundVersion': version,
  'persistedRoundVersion': version,
  'currentMinute': 1,
  'readyCounter': {'roundVersion': version, 'participants': []},
  'matches': [
    {'minute': 1, 'homeGoals': 0, 'events': [], 'isFinished': false},
    {'minute': 0, 'homeGoals': 0, 'events': [], 'isFinished': true},
  ],
};

Map<String, dynamic> patch(int previous, int version, {String room = 'room'}) =>
    {
      'gameWorldId': room,
      'realtimeEpoch': 'first',
      'previousSequence': previous,
      'realtimeSequence': previous + 2,
      'previousVersion': version,
      'roundVersion': version + 1,
      'persistedRoundVersion': 20,
      'roundState': 'RUNNING_FIRST_HALF',
      'currentMinute': 2,
      'activePauseWindow': null,
      'pendingPenalty': null,
      'matches': [
        {
          'index': 0,
          'score': {'homeGoals': 1, 'minute': null},
          'events': [
            {'eventType': 'Goal'},
            {'eventType': 'YellowCard'},
            {'eventType': 'Injury'},
            {'eventType': 'Substitution'},
          ],
          'homeStarters': [
            {'playerId': 'new'},
          ],
        },
      ],
    };

void main() {
  test(
    'aplica alterações e eventos sem modificar a base ou duplicar replay',
    () {
      final initial = base();
      final stream = RoundStream('room', snapshot: initial);
      expect(stream.apply(patch(10, 20)), isTrue);
      final match = stream.snapshot!['matches'][0];
      expect(match['homeGoals'], 1);
      expect(match['minute'], 2);
      expect(match['homeStarters'], [
        {'playerId': 'new'},
      ]);
      expect(stream.snapshot!['matches'][1]['minute'], 0);
      expect(stream.snapshot!['persistedRoundVersion'], 20);
      expect(match['events'], hasLength(4));
      expect(initial['matches'][0]['events'], isEmpty);
      expect(stream.apply(patch(10, 20)), isTrue);
      expect(stream.snapshot!['matches'][0]['events'], hasLength(4));
    },
  );

  test(
    'lacuna e mensagem fora de ordem exigem recuperação sem aplicar estado parcial',
    () {
      final initial = base();
      final stream = RoundStream('room', snapshot: initial);
      expect(stream.apply(patch(12, 21)), isFalse);
      expect(stream.snapshot, same(initial));
      expect(stream.apply(patch(10, 20, room: 'other')), isTrue);
      expect(stream.snapshot, same(initial));
      final recovered = base(sequence: 14, version: 22);
      expect(stream.acceptFull(recovered), isTrue);
      expect(stream.apply(patch(10, 20)), isTrue);
      expect(stream.acceptFull(initial), isFalse);
      expect(stream.snapshot, same(recovered));
    },
  );

  test('reinício aceita versão recuperada mais baixa em uma nova época', () {
    final stream = RoundStream('room', snapshot: base(version: 36));
    final restored = base(epoch: 'second', sequence: 0, version: 30);
    expect(stream.acceptFull(restored), isTrue);
    expect(stream.apply(patch(10, 36)), isFalse);
    expect(stream.snapshot, same(restored));
  });
}
