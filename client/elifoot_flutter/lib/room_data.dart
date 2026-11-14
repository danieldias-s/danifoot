// ignore_for_file: invalid_use_of_protected_member
part of 'main.dart';

enum _RoomData {
  squad('Carregando escalação...'),
  standings('Carregando classificação...'),
  cup('Carregando a taça...'),
  scorers('Atualizando artilharia...'),
  calendar('Atualizando calendário...'),
  clubs('Carregando clubes...'),
  market('Carregando mercado e leilões...'),
  finances('Atualizando finanças...'),
  ranking('Carregando ranking...'),
  invites('Carregando carreira...');

  const _RoomData(this.message);
  final String message;
}

class _RoomRead {
  _RoomRead(this.room, this.revision);

  final (String, String?, String?) room;
  final int revision;
  late final Future<void> completion;
  bool pending = true;
  bool failed = false;
  bool delayed = false;
  bool hasData = false;
  List<dynamic>? rows;
}

extension _RoomDataLoading on _ElifootShellScreenState {
  (String, String?, String?)? get _roomIdentity =>
      _worldId == null ? null : (_worldId!, _myClubId, _coachId);

  String? _dataFilter(_RoomData data) =>
      data == _RoomData.standings ? _selectedDivisionTier : null;

  Set<_RoomData> get _visibleRoomData {
    if (_matchLocked || _showMatchView) {
      return {_RoomData.clubs, _RoomData.calendar};
    }
    switch (_classicPage) {
      case 'standings':
        return {_RoomData.standings};
      case 'cup':
        return {_RoomData.cup, _RoomData.clubs};
      case 'calendar':
      case 'results':
        return {_RoomData.calendar, _RoomData.clubs};
      case 'career':
      case 'profile':
        return {_RoomData.invites, _RoomData.ranking};
      case 'coachRanking':
        return {_RoomData.ranking};
      case 'bank':
      case 'stadium':
        return {_RoomData.squad, _RoomData.finances};
    }
    return switch (_activeTabIndex) {
      0 => {
        _RoomData.squad,
        _RoomData.calendar,
        _RoomData.standings,
        if (_isUnemployed) _RoomData.invites,
      },
      1 =>
        _competitionTab == 'cup'
            ? {_RoomData.cup, _RoomData.scorers, _RoomData.clubs}
            : {_RoomData.standings, _RoomData.calendar},
      2 => {_RoomData.market, _RoomData.squad},
      3 => {_RoomData.squad, _RoomData.finances},
      4 => {_RoomData.ranking, _RoomData.invites},
      5 => {_RoomData.calendar, _RoomData.clubs},
      _ => {},
    };
  }

  Future<void> _loadVisibleRoomData({Set<_RoomData> also = const {}}) async {
    await Future.wait({..._visibleRoomData, ...also}.map(_loadRoomData));
  }

  bool _hasCurrentRoomData(_RoomData data) {
    final read = _roomReads[(data, _dataFilter(data))];
    return read != null &&
        read.room == _roomIdentity &&
        read.revision == _dataRevision &&
        !read.pending &&
        !read.failed;
  }

  Future<void> _refreshClubAndSecondaryData() {
    _dataSnapshotVersion = _snapshot?['roundVersion'] as num?;
    _dataRevision++;
    return _loadVisibleRoomData(also: {_RoomData.squad, _RoomData.finances});
  }

  void _applyWorldDataChange(Map<String, dynamic> change) {
    final snapshot = _snapshot;
    if (!mounted || snapshot == null || change['worldId'] != _worldId) return;
    final version = change['version'];
    final areas = change['areas'];
    if (version is! num || areas is! List) return;
    if (change['seasonNumber'] != snapshot['seasonNumber'] ||
        change['roundNumber'] != snapshot['roundNumber']) {
      return;
    }
    final epoch = (
      _worldId,
      snapshot['realtimeEpoch'],
      snapshot['seasonNumber'],
      snapshot['roundNumber'],
    );
    if (_worldDataEpoch != epoch) {
      _worldDataEpoch = epoch;
      _lastAreaVersion.clear();
    }
    final affected = <_RoomData>{};
    for (final data in _RoomData.values) {
      if (areas.contains(data.name) &&
          version > (_lastAreaVersion[data] ?? -1)) {
        _lastAreaVersion[data] = version;
        affected.add(data);
      }
    }
    if (affected.isEmpty) return;
    // A state transition still needs its own full round projection.
    if (change['roundState'] == snapshot['roundState'] &&
        version > (snapshot['roundVersion'] as num? ?? -1)) {
      _dataSnapshotVersion = version;
      _snapshot = {...snapshot, 'roundVersion': version};
    }
    _roomReads.removeWhere((key, _) => affected.contains(key.$1));
    final visible = _visibleRoomData.intersection(affected);
    for (final data in visible) {
      unawaited(_loadRoomData(data));
    }
    setState(() {});
  }

  Future<void> _loadRoomData(_RoomData data, {bool retry = false}) {
    final room = _roomIdentity;
    if (room == null) return Future.value();
    final filter = _dataFilter(data);
    final key = (data, filter);
    final previous = _roomReads[key];
    if (previous != null &&
        previous.room == room &&
        previous.revision == _dataRevision &&
        (!retry || !previous.failed)) {
      if (!previous.pending &&
          !previous.failed &&
          data == _RoomData.standings) {
        setState(() => _standings = previous.rows!);
      }
      return previous.completion;
    }

    final read = _RoomRead(room, _dataRevision);
    read.hasData = previous?.room == room && previous!.hasData;
    _roomReads[key] = read;
    final timer = Timer(const Duration(milliseconds: 180), () {
      if (mounted && _roomReads[key] == read && read.pending) {
        setState(() => read.delayed = true);
      }
    });
    read.completion = () async {
      try {
        final value = await _fetchRoomData(
          data,
          room,
          filter,
        ).timeout(const Duration(seconds: 15));
        if (!_currentRoomRead(key, read)) return;
        setState(() {
          read.pending = false;
          read.hasData = true;
          switch (data) {
            case _RoomData.squad:
              _applySquad(value as Map<String, dynamic>?);
            case _RoomData.standings:
              read.rows = value as List<dynamic>;
              if (filter == _selectedDivisionTier) _standings = read.rows!;
            case _RoomData.cup:
              _cupFixtures = value as List<dynamic>;
            case _RoomData.scorers:
              _topScorers = value as List<dynamic>;
            case _RoomData.calendar:
              _calendar = value as List<dynamic>;
            case _RoomData.clubs:
              _applyClubPalettes(room.$1, value as List<dynamic>);
            case _RoomData.market:
              _marketData = value as Map<String, dynamic>;
            case _RoomData.finances:
              _financesData = value as Map<String, dynamic>?;
            case _RoomData.ranking:
              _rankingData = value as Map<String, dynamic>;
            case _RoomData.invites:
              _invitationsData = value as Map<String, dynamic>?;
          }
        });
      } catch (_) {
        if (_currentRoomRead(key, read)) {
          setState(() {
            read.pending = false;
            read.failed = true;
          });
        }
      } finally {
        timer.cancel();
      }
    }();
    return read.completion;
  }

  bool _currentRoomRead((_RoomData, String?) key, _RoomRead read) =>
      mounted &&
      _roomIdentity == read.room &&
      _dataRevision == read.revision &&
      identical(_roomReads[key], read);

  Future<Object?> _fetchRoomData(
    _RoomData data,
    (String, String?, String?) room,
    String? filter,
  ) async {
    final (world, club, coach) = room;
    final api = widget.apiClient;
    return switch (data) {
      _RoomData.squad => club == null ? null : api.getClubDetail(world, club),
      _RoomData.standings => api.getStandings(world, filter!),
      _RoomData.cup => api.getCupFixtures(world),
      _RoomData.scorers => api.getTopScorers(world),
      _RoomData.calendar => api.getCalendar(world),
      _RoomData.clubs => api.getClubs(world),
      _RoomData.market => api.getMarket(world),
      _RoomData.finances =>
        club == null ? null : api.getClubFinances(world, club),
      _RoomData.ranking => api.getRankingAndHistory(world),
      _RoomData.invites =>
        coach == null ? null : api.getInvitations(world, coach),
    };
  }

  void _applySquad(Map<String, dynamic>? detail) {
    _myClubDetail = detail;
    if (detail == null) return;
    ClubNameDirectory.register(detail['id'], detail['name']);
    final squadIds = (detail['squad'] as List).map((p) => '${p['id']}').toSet();
    if (_selectedStarterIds.isEmpty ||
        !_selectedStarterIds.every(squadIds.contains)) {
      _selectedFormation = detail['preferredFormation']?.toString() ?? '4-4-2';
      _selectedStarterIds = (detail['preferredStarterIds'] as List? ?? [])
          .map((id) => '$id')
          .toList();
      _selectedBenchIds = (detail['preferredBenchIds'] as List? ?? [])
          .map((id) => '$id')
          .toList();
    }
    _selectedBenchIds.removeWhere((id) => !squadIds.contains(id));
    if (_classicSelectedPlayerId != null &&
        !squadIds.contains(_classicSelectedPlayerId)) {
      _classicSelectedPlayerId = null;
      _classicSelectedPlayerHistory = null;
      _classicPlayerStatsRequestSeq++;
    }
    unawaited(
      widget.appearance?.activate(
        widget.apiClient,
        _coachId,
        _worldId,
        _myClubId,
        detail['colorPalette'] is Map
            ? Map<String, dynamic>.from(detail['colorPalette'] as Map)
            : null,
      ),
    );
  }

  void _selectDivision(String tier) {
    if (_selectedDivisionTier == tier) {
      unawaited(_loadRoomData(_RoomData.standings, retry: true));
      return;
    }
    setState(() => _selectedDivisionTier = tier);
    unawaited(_loadRoomData(_RoomData.standings, retry: true));
  }

  Widget _whenRoomData(
    Set<_RoomData> dependencies,
    Widget Function() ready, {
    String? message,
    bool keepContent = false,
  }) {
    final reads = dependencies
        .map((data) => (data, _roomReads[(data, _dataFilter(data))]))
        .where(
          (item) =>
              item.$2?.room != _roomIdentity ||
              item.$2?.revision != _dataRevision ||
              item.$2!.pending ||
              item.$2!.failed,
        )
        .toList();
    if (reads.isEmpty && !keepContent) return ready();
    final failed = reads.where(
      (item) =>
          item.$2?.failed == true &&
          item.$2?.revision == _dataRevision &&
          item.$2?.room == _roomIdentity,
    );
    final delayed = reads.any(
      (item) => item.$2?.delayed == true && item.$2?.revision == _dataRevision,
    );
    final loading = failed.isNotEmpty
        ? _RoomLoading(
            message: 'Não foi possível atualizar os dados.',
            retry: () {
              for (final item in failed) {
                unawaited(_loadRoomData(item.$1, retry: true));
              }
              setState(() {});
            },
          )
        : _RoomLoading(
            message: delayed ? message ?? reads.first.$1.message : null,
          );
    if (keepContent &&
        reads.every(
          (item) => item.$2?.room == _roomIdentity && item.$2?.hasData == true,
        )) {
      return Stack(
        children: [
          ExcludeSemantics(
            excluding: reads.isNotEmpty,
            child: IgnorePointer(ignoring: reads.isNotEmpty, child: ready()),
          ),
          if (failed.isNotEmpty || delayed)
            Align(
              alignment: Alignment.topCenter,
              child: Material(
                color: ClassicTheme.of(context).surface,
                elevation: 2,
                child: Padding(
                  padding: const EdgeInsets.symmetric(horizontal: 20),
                  child: loading,
                ),
              ),
            ),
        ],
      );
    }
    return loading;
  }
}

class _RoomLoading extends StatelessWidget {
  const _RoomLoading({this.message, this.retry});

  final String? message;
  final VoidCallback? retry;

  @override
  Widget build(BuildContext context) {
    return SizedBox(
      height: 104,
      child: Center(
        child: message == null
            ? const SizedBox.shrink()
            : Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  if (retry == null)
                    Padding(
                      padding: const EdgeInsets.only(bottom: 8),
                      child: SizedBox(
                        width: 120,
                        child: LinearProgressIndicator(
                          value: MediaQuery.disableAnimationsOf(context)
                              ? .5
                              : null,
                          minHeight: 3,
                          color: ClassicTheme.of(context).accent,
                          backgroundColor: ClassicTheme.of(
                            context,
                          ).surfaceAlternate,
                        ),
                      ),
                    ),
                  Text(
                    message!,
                    textAlign: TextAlign.center,
                    style: TextStyle(
                      color: ClassicTheme.of(context).accent,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                  if (retry != null)
                    TextButton(
                      onPressed: retry,
                      child: const Text('Tentar novamente'),
                    ),
                ],
              ),
      ),
    );
  }
}
