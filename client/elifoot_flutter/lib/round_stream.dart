/// Reconstructs the public round projection for a single authorized room.
class RoundStream {
  RoundStream(this.worldId, {this.snapshot});

  final String worldId;
  Map<String, dynamic>? snapshot;
  final _retiredEpochs = <String>{};

  bool acceptFull(Map<String, dynamic> incoming) {
    if (incoming['gameWorldId'] != worldId) return false;
    final current = snapshot;
    final epoch = incoming['realtimeEpoch'] as String?;
    final previousEpoch = current?['realtimeEpoch'] as String?;
    if (_retiredEpochs.contains(epoch)) return false;
    if (current != null &&
        current['realtimeEpoch'] == incoming['realtimeEpoch']) {
      final sequence = current['realtimeSequence'] as num? ?? 0;
      final incomingSequence = incoming['realtimeSequence'] as num? ?? 0;
      final version = current['roundVersion'] as num? ?? 0;
      final incomingVersion = incoming['roundVersion'] as num? ?? 0;
      if (incomingSequence < sequence || incomingVersion < version) {
        return false;
      }
    }
    if (epoch != null && previousEpoch != null && epoch != previousEpoch) {
      _retiredEpochs.add(previousEpoch);
    }
    snapshot = incoming;
    return true;
  }

  /// Returns false when the patch requires a full resynchronization.
  bool apply(Map<String, dynamic> patch) {
    if (patch['gameWorldId'] != worldId) return true;
    final current = snapshot;
    if (current == null || patch['realtimeEpoch'] != current['realtimeEpoch']) {
      return false;
    }
    final sequence = patch['realtimeSequence'] as int;
    if (sequence <= (current['realtimeSequence'] as int)) return true;
    if (patch['previousSequence'] != current['realtimeSequence'] ||
        patch['previousVersion'] != current['roundVersion']) {
      return false;
    }

    final minute = patch['currentMinute'];
    final matches = (current['matches'] as List<dynamic>)
        .cast<Map<String, dynamic>>();
    final changedMatches = <int, Map<String, dynamic>>{};
    for (final change in patch['matches'] as List<dynamic>) {
      final index = change['index'] as int;
      final match = {...matches[index]};
      final score = change['score'] as Map<String, dynamic>?;
      if (score != null) {
        match.addAll(score);
        match['minute'] = score['minute'] ?? minute;
      }
      if (change.containsKey('events')) {
        match['events'] = [
          ...match['events'] as List,
          ...change['events'] as List,
        ];
      }
      for (final side in [
        'homeStarters',
        'homeBench',
        'awayStarters',
        'awayBench',
      ]) {
        if (change.containsKey(side)) match[side] = change[side];
      }
      changedMatches[index] = match;
    }

    snapshot = {
      ...current,
      'realtimeSequence': sequence,
      'roundVersion': patch['roundVersion'],
      'persistedRoundVersion': patch['persistedRoundVersion'],
      'currentMinute': minute,
      'roundState': patch['roundState'],
      'activePauseWindow': patch['activePauseWindow'],
      'pendingPenalty': patch['pendingPenalty'],
      'readyCounter':
          patch['readyCounter'] ??
          {
            ...current['readyCounter'] as Map,
            'roundVersion': patch['roundVersion'],
          },
      'matches': [
        for (int index = 0; index < matches.length; index++)
          if (changedMatches.containsKey(index))
            changedMatches[index]!
          else if (matches[index]['isFinished'] != true &&
              matches[index]['minute'] == current['currentMinute'])
            {...matches[index], 'minute': minute}
          else
            matches[index],
      ],
    };
    return true;
  }
}
