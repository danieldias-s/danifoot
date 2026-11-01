enum RoundViewDecision { none, match, tactics }

/// Navigation follows authoritative snapshots, never ready counters or HTTP optimism.
class RoundViewCoordinator {
  String? _round, _openedRound, _resultRound, _pause;
  void reset() {
    _round = null;
    _openedRound = null;
    _resultRound = null;
    _pause = null;
  }

  static bool isLiveMatch(String? stage) => const {
    'RUNNING_FIRST_HALF',
    'HALFTIME_PAUSED',
    'RUNNING_SECOND_HALF',
    'GLOBAL_PAUSED',
    'EXTRA_TIME_BREAK',
    'RUNNING_EXTRA_TIME',
    'EXTRA_TIME_HALFTIME',
  }.contains(stage);

  static bool isMatchStage(String? stage) => const {
    'RUNNING_FIRST_HALF',
    'HALFTIME_PAUSED',
    'RUNNING_SECOND_HALF',
    'GLOBAL_PAUSED',
    'EXTRA_TIME_BREAK',
    'RUNNING_EXTRA_TIME',
    'EXTRA_TIME_HALFTIME',
    'RESULTS',
    'FINISHED',
  }.contains(stage);

  RoundViewDecision accept(Map<String, dynamic> snapshot) {
    final key =
        '${snapshot['gameWorldId']}:${snapshot['seasonNumber']}:${snapshot['roundNumber']}';
    final stage = snapshot['roundState']?.toString();
    final changedRound = key != _round;
    if (changedRound) {
      _openedRound = null;
      _resultRound = null;
      _pause = null;
    }
    _round = key;
    if (!isMatchStage(stage)) {
      return stage == 'PREPARING' && changedRound
          ? RoundViewDecision.tactics
          : RoundViewDecision.none;
    }
    final pause = snapshot['activePauseWindow'] as Map?;
    final pauseKey = pause == null ? null : '$key:${pause['pauseWindowId']}';
    if ((stage == 'RESULTS' || stage == 'FINISHED') && _resultRound != key) {
      _resultRound = key;
      _openedRound = key;
      return RoundViewDecision.match;
    }
    if (pauseKey != null && pauseKey != _pause) {
      _pause = pauseKey;
      _openedRound = key;
      return RoundViewDecision.match;
    }
    if (_openedRound != key) {
      _openedRound = key;
      return RoundViewDecision.match;
    }
    return RoundViewDecision.none;
  }
}
