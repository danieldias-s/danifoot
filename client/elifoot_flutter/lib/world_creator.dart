import 'dart:math';
import 'dart:convert';

import 'club_theme.dart';
import 'package:flutter/material.dart';
import 'api_client.dart';
import 'custom_assembly_editor.dart';
import 'classic_match_board.dart';
import 'creator_divisions.dart';
import 'creator_district_list.dart';
import 'creator_districts.dart';
import 'creator_roster_sources.dart';
import 'creator_visual.dart';
import 'creator_club_placement.dart';
import 'creator_division_picker.dart';
import 'creator_world_review.dart';

class WorldCreatorScreen extends StatefulWidget {
  final ElifootApiClient api;
  final String coachId;
  final bool worldCatalogRules;
  const WorldCreatorScreen({
    super.key,
    required this.api,
    required this.coachId,
    this.worldCatalogRules = false,
  });
  @override
  State<WorldCreatorScreen> createState() => _WorldCreatorScreenState();
}

class _WorldCreatorScreenState extends State<WorldCreatorScreen> {
  final _name = TextEditingController(text: 'Liga Mundial 98');
  final _years = <String>{};
  List<Map<String, dynamic>> _yearOptions = [];
  bool _yearsLoadFailed = false, _countryLoadFailed = false;
  static const _maxCountries = 8;
  static int _drawSeed() => Random.secure().nextInt(2147483647) + 1;
  int _seed = _drawSeed();
  String _gender = 'Male', _district = 'Ranking', _search = '', _error = '';
  bool _generateAllRosterPlayers = false;
  bool _repairUnavailableAssignments = false;
  String? _rosterProblem;
  int? _sourcesReturnStep;
  Map<String, dynamic>? _assembly;
  final _confirmationKey = 'create-${DateTime.now().microsecondsSinceEpoch}';
  int _step = 0, _humans = 1;
  bool _busy = false, _saved = false;
  final _countries = <String>{};
  List<Map<String, dynamic>> _options = [], _clubs = [];
  final _districtClubs = <String>{};
  final _districtView = CreatorDistrictViewState();
  static const _districtCapacity = 96;
  static const _worldCapacity = 128;
  List<String?> _divisionSlots = List.filled(32, null);
  List<String?> _automaticSlots = List.filled(32, null);
  Map<String, dynamic>? _last, _preview;
  Map<String, dynamic>? _reviewDraft;
  String? _reviewScope;
  static const _steps = [
    'Categoria',
    'Países',
    'Escolher anos',
    'Divisões',
    'Distritais',
    'Elencos / fontes',
    'Prévia',
    'Confirmar',
  ];
  static const _categoryStep = 0;
  static const _countriesStep = 1;
  static const _yearsStep = 2;
  static const _divisionsStep = 3;
  static const _districtsStep = 4;
  static const _sourcesStep = 5;
  static const _previewStep = 6;
  static const _confirmStep = 7;
  @override
  void initState() {
    super.initState();
    _initialize();
  }

  @override
  void dispose() {
    _name.dispose();
    _districtView.dispose();
    super.dispose();
  }

  Future<dynamic> _call(String path, {Map<String, dynamic>? body}) =>
      widget.api.worldCreator(widget.coachId, path, body: body);
  List<Map<String, dynamic>> _maps(dynamic data) =>
      (data as List<dynamic>? ?? [])
          .map((x) => Map<String, dynamic>.from(x as Map))
          .toList();
  Future<void> _run(Future<void> Function() action) async {
    setState(() {
      _busy = true;
      _error = '';
    });
    try {
      await action();
    } catch (e) {
      if (mounted) {
        setState(() {
          final message = e.toString();
          _error = message.length > 600 ? message.substring(0, 600) : message;
        });
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  void _clearCatalogDraft({bool keepPlacement = false}) {
    _assembly = null;
    _clubs = [];
    _preview = null;
    _reviewDraft = null;
    _reviewScope = null;
    _rosterProblem = null;
    _repairUnavailableAssignments = keepPlacement;
    if (keepPlacement) return;
    _districtView.reset();
    _districtClubs.clear();
    _divisionSlots = List.filled(32, null);
    _automaticSlots = List.filled(32, null);
  }

  int _clubCount(Map<String, dynamic> country) =>
      ((country['clubCount'] ??
                  country[_gender == 'Male' ? 'maleClubs' : 'femaleClubs'])
              as num?)
          ?.toInt() ??
      0;

  int get _selectedCountryCount => _options.isEmpty ? 0 : _countries.length;

  bool get _canAdvanceFromCountries =>
      !_countryLoadFailed && _selectedCountryCount > 0;

  Future<void> _loadCountries() async {
    final path = Uri(
      path: 'historical-countries',
      queryParameters: {'gender': _gender},
    );
    try {
      final options = _maps(
        await _call(path.toString()),
      ).where((country) => _clubCount(country) > 0).toList();
      if (!mounted) return;
      final available = options.map((country) => country['code']).toSet();
      final validSelection = _countries
          .where(available.contains)
          .take(_maxCountries)
          .toList();
      setState(() {
        _countryLoadFailed = false;
        _options = options;
        if (validSelection.length != _countries.length) {
          _countries
            ..clear()
            ..addAll(validSelection);
          _clearCatalogDraft(keepPlacement: true);
        }
      });
    } catch (_) {
      if (mounted) setState(() => _countryLoadFailed = true);
    }
  }

  List<String> get _orderedYears =>
      _years.toList()..sort((a, b) => int.parse(b).compareTo(int.parse(a)));

  Future<void> _loadYears() async {
    final path = Uri(
      path: 'historical-years',
      queryParameters: {
        'gender': _gender,
        'countries': (_countries.toList()..sort()).join(','),
      },
    );
    try {
      final options =
          _maps(
            await _call(path.toString()),
          ).where((option) => _clubCount(option) > 0).toList()..sort(
            (a, b) => int.parse(
              b['year'].toString(),
            ).compareTo(int.parse(a['year'].toString())),
          );
      if (!mounted) return;
      setState(() {
        _yearsLoadFailed = false;
        _yearOptions = options;
        final available = options
            .map((item) => item['year'].toString())
            .toSet();
        if (!_years.every(available.contains)) {
          _years.removeWhere((year) => !available.contains(year));
          _clearCatalogDraft(keepPlacement: true);
        }
      });
    } catch (_) {
      if (mounted) setState(() => _yearsLoadFailed = true);
    }
  }

  Future<void> _initialize() => _run(() async {
    final last = await _call('last-configuration');
    if (mounted) {
      setState(() {
        _last = last == null ? null : Map<String, dynamic>.from(last as Map);
      });
    }
  });
  bool get _fictionalOnly =>
      widget.worldCatalogRules &&
      _generateAllRosterPlayers &&
      _divisionSlots.contains(null);

  bool get _fillClubVacancies =>
      _generateAllRosterPlayers &&
      (_divisionSlots.contains(null) ||
          (_district != 'Manual' &&
              _districtCandidates.length < _districtCapacity));

  Map<String, dynamic> _request() => {
    'name': _name.text.trim(),
    'gender': _gender,
    'dataMode': !_generateAllRosterPlayers
        ? 'REAL_COMPLETE'
        : _fillClubVacancies
        ? 'FREE_WORLD'
        : 'REAL_CLUBS_SIMULATED',
    'season': _orderedYears.firstOrNull,
    'catalogYears': _orderedYears,
    'countries': _countries.toList()..sort(),
    'mode': !_fictionalOnly && _divisionSlots.any((key) => key != null)
        ? 'Custom'
        : 'Automatic',
    'districtMode': _fictionalOnly ? 'Ranking' : _district,
    'seed': _seed,
    'maxHumans': _humans,
    'assignments': [
      if (!_fictionalOnly) ...[
        for (final (index, key) in _divisionSlots.indexed)
          if (key != null) {'clubKey': key, 'division': index ~/ 8 + 1},
        if (_district == 'Manual')
          for (final key in _districtClubs) {'clubKey': key, 'division': 5},
      ],
    ],
    'fillRemainingDivisions':
        !widget.worldCatalogRules &&
        _generateAllRosterPlayers &&
        _divisionSlots.contains(null),
    'preserveRankedSquadStrengths': true,
    'allowFictionalTeams': _fillClubVacancies,
    'allowFictionalPlayers': _generateAllRosterPlayers,
    if (_generateAllRosterPlayers) 'generateAllRosterPlayers': true,
    'useSavedData': widget.worldCatalogRules || _saved,
    if (widget.worldCatalogRules) ...{
      'useWorldCatalogRules': true,
      'fictionalOnly': _fictionalOnly,
      'requireHostStart': true,
      'gameModeId': 'CLASSIC_SYNC',
    },
  };
  void _useLast() {
    final p = _last!;
    setState(() {
      _clearCatalogDraft();
      _name.text = p['name'].toString();
      _gender = p['gender'].toString();
      final savedYears = p['catalogYears'] as List? ?? [];
      _years
        ..clear()
        ..addAll(
          (savedYears.isNotEmpty ? savedYears : [p['season']])
              .map((year) => year.toString())
              .where((year) => int.tryParse(year) != null),
        );
      _error = '';
      _seed = (p['seed'] as num?)?.toInt() ?? _seed;
      _district = p['districtMode'].toString();
      _humans = (p['maxHumans'] as num?)?.toInt() ?? 1;
      _countries
        ..clear()
        ..addAll((p['countries'] as List).cast<String>());
      final positions = List.filled(4, 0);
      for (final assignment in _maps(p['assignments'])) {
        final division = (assignment['division'] as num).toInt();
        final key = assignment['clubKey'].toString();
        if (division == 5) {
          _districtClubs.add(key);
        } else if (division >= 1 &&
            division <= 4 &&
            positions[division - 1] < 8) {
          _divisionSlots[(division - 1) * 8 + positions[division - 1]++] = key;
        }
      }
      _generateAllRosterPlayers =
          p['generateAllRosterPlayers'] == true || p['fictionalOnly'] == true;
      _saved = p['useSavedData'] == true;
      _repairUnavailableAssignments = true;
      _preview = null;
    });
  }

  Future<void> _catalog() async {
    if (_hasReviewDraft) return;
    final data = Map<String, dynamic>.from(
      await _call(
            'catalog',
            body: {
              ..._request(),
              'dataMode': null,
              'useSavedData': true,
              'generateAllRosterPlayers': false,
            },
          )
          as Map,
    );
    if (mounted) {
      setState(() {
        _clubs = _maps(data['clubs'])..sort(_compareClubs);
        for (final club in _clubs) {
          club['name'] = _catalogClubName(club);
        }
        _reconcileDivisions();
      });
    }
  }

  Future<void> _makePreview() async {
    if (_assembly != null) {
      final data = Map<String, dynamic>.from(
        await _call('assemblies/${_assembly!['id']}') as Map,
      );
      if (mounted) setState(() => _applyAssembly(data));
      return;
    }
    if (_hasReviewDraft) {
      await _saveReviewPlacement(
        _currentReviewPlacement,
        keepDistrictMode: true,
      );
      return;
    }
    final data = Map<String, dynamic>.from(
      await _call('preview', body: _request()) as Map,
    );
    if (mounted) {
      setState(() {
        _preview = data;
        if (_validPreview) _adoptReviewPlacement(data, keepDistrictMode: true);
      });
    }
  }

  // Placement changes use the server's existing snapshot. This includes its
  // generated clubs and strengths; revisiting a step never regenerates them.
  String get _reviewScopeKey => jsonEncode({
    'gender': _gender,
    'countries': _countries.toList()..sort(),
    'years': _orderedYears,
    'seed': _seed,
    'generatedPlayers': _generateAllRosterPlayers,
  });

  bool get _hasReviewDraft =>
      _reviewDraft != null && _reviewScope == _reviewScopeKey;

  List<Map<String, dynamic>> get _reviewClubs => _maps(
    _reviewDraft?['clubs'],
  ).map((entry) => Map<String, dynamic>.from(entry['club'] as Map)).toList();

  CreatorClubPlacement get _currentReviewPlacement {
    final byKey = {for (final club in _reviewClubs) club['key']: club};
    final districts = _hasReviewDraft || _district == 'Manual'
        ? _districtClubs.toList()
        : _reviewClubs
              .where((club) => !_divisionSlots.contains(club['key']))
              .map((club) => club['key'].toString())
              .toList();
    return CreatorClubPlacement.fromPreview([
      for (final (index, key) in _divisionSlots.indexed)
        {'division': index ~/ 8 + 1, 'club': byKey[key]},
      for (final key in districts) {'division': 5, 'club': byKey[key]},
    ]);
  }

  void _adoptReviewPlacement(
    Map<String, dynamic> data, {
    bool keepDistrictMode = false,
  }) {
    final placement = CreatorClubPlacement.fromPreview(_maps(data['clubs']));
    _reviewDraft = data;
    _reviewScope = _reviewScopeKey;
    _divisionSlots = placement.divisions
        .take(4)
        .expand((keys) => keys)
        .toList();
    if (!keepDistrictMode) _district = 'Manual';
    _districtClubs
      ..clear()
      ..addAll(placement.divisions[4]);
    _preview = data;
  }

  Future<void> _saveReviewPlacement(
    CreatorClubPlacement placement, {
    bool keepDistrictMode = false,
    String? districtMode,
  }) async {
    final draft = _reviewDraft ?? _preview;
    if (draft == null) {
      throw Exception('Atualize a prévia antes de trocar clubes.');
    }
    final data = Map<String, dynamic>.from(
      await _call(
            'preview/${draft['draftId']}/reassign',
            body: {
              'assignments': placement.assignments,
              'name': _name.text.trim(),
              'maxHumans': _humans,
              'districtMode': ?districtMode,
            },
          )
          as Map,
    );
    try {
      CreatorClubPlacement.fromPreview(_maps(data['clubs']));
    } on FormatException {
      if (mounted) setState(() => _preview = data);
      rethrow;
    }
    if (mounted) {
      setState(() {
        if (districtMode != null) _district = districtMode;
        _adoptReviewPlacement(data, keepDistrictMode: keepDistrictMode);
      });
    }
  }

  void _chooseDistrictMode(String mode) {
    if (!_hasReviewDraft) {
      setState(() {
        _district = mode;
        _preview = null;
      });
    } else if (mode == 'Manual') {
      setState(() => _district = mode);
    } else {
      _run(
        () => _saveReviewPlacement(
          _currentReviewPlacement,
          districtMode: mode,
          keepDistrictMode: true,
        ),
      );
    }
  }

  Future<void> _pickReviewClub(String clubKey) async {
    if (_busy || _preview == null) return;
    final entries = _maps(_preview!['clubs']);
    final clubs = [
      for (final entry in entries)
        {
          ...Map<String, dynamic>.from(entry['club'] as Map),
          'name': _catalogClubName(entry['club'] as Map),
        },
    ];
    final current = clubs.where((club) => club['key'] == clubKey).firstOrNull;
    if (current == null) return;
    final picked = await showCreatorDivisionPicker(
      context,
      divisions: [
        for (var division = 1; division <= 4; division++)
          [
            for (final entry in entries)
              if (entry['division'] == division)
                clubs.firstWhere(
                  (club) => club['key'] == (entry['club'] as Map)['key'],
                ),
          ],
      ],
      districtClubs: [
        for (final entry in entries)
          if (entry['division'] == 5)
            clubs.firstWhere(
              (club) => club['key'] == (entry['club'] as Map)['key'],
            ),
      ],
      eligibleClubs: clubs,
      currentClub: current,
      assignedOnly: true,
    );
    if (!mounted || _busy || picked == null) return;
    await _run(
      () => _saveReviewPlacement(
        CreatorClubPlacement.fromPreview(entries).swap(clubKey, picked),
      ),
    );
  }

  void _applyAssembly(Map<String, dynamic> data) {
    _assembly = data;
    _reviewDraft = null;
    _reviewScope = null;
    _gender = data['gender'].toString();
    _seed = (data['seed'] as num?)?.toInt() ?? _seed;
    _countries
      ..clear()
      ..addAll((data['countries'] as List).cast<String>());
    _name.text = data['name'].toString();
    _preview = null;
  }

  Future<void> _openAssemblyEditor(Map<String, dynamic> data) async {
    final result = await Navigator.push<Map<String, dynamic>>(
      context,
      MaterialPageRoute(
        builder: (_) => CustomAssemblyEditor(
          api: widget.api,
          coachId: widget.coachId,
          assembly: data,
          countryOptions: _options,
        ),
      ),
    );
    if (mounted) setState(() => _applyAssembly(result ?? data));
  }

  Future<void> _restoreAssembly() => _run(() async {
    final items = _maps(await _call('assemblies'));
    if (!mounted) return;
    if (items.isEmpty) {
      throw Exception('Você ainda não tem montagens salvas nesta conta.');
    }
    setState(() => _busy = false);
    final id = await showDialog<String>(
      context: context,
      builder: (ctx) => SimpleDialog(
        title: const Text('USAR MONTAGEM SALVA'),
        children: items
            .map(
              (a) => SimpleDialogOption(
                onPressed: () => Navigator.pop(ctx, a['id'].toString()),
                child: Text(
                  '${a['name']} • ${a['gender'] == 'Female' ? 'FEMININO' : 'MASCULINO'}',
                ),
              ),
            )
            .toList(),
      ),
    );
    if (id == null) return;
    if (mounted) setState(() => _busy = true);
    final data = Map<String, dynamic>.from(
      await _call('assemblies/$id') as Map,
    );
    if (mounted) {
      setState(() {
        _applyAssembly(data);
        _step = _previewStep;
      });
    }
  });
  Widget _assemblyPanel() {
    final a = _assembly!, p = a['package'] as Map, v = a['validation'] as Map;
    final clubs = _maps(p['clubs']), players = _maps(p['players']);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          '${a['name']} • ${clubs.length}/128 clubes • ${players.length} atletas',
        ),
        Text(
          a['isSaved'] == true
              ? 'MONTAGEM SALVA NA SUA CONTA'
              : 'RASCUNHO PRIVADO',
        ),
        for (final error in v['errors'] as List? ?? [])
          Text('$error', style: const TextStyle(color: Colors.orangeAccent)),
        TextButton(
          onPressed: _busy ? null : () => _run(() => _openAssemblyEditor(a)),
          child: const Text('REVISAR / EDITAR MONTAGEM'),
        ),
        for (final d in [
          'PrimeiraDivisao',
          'SegundaDivisao',
          'TerceiraDivisao',
          'QuartaDivisao',
          'Distrital',
        ])
          ExpansionTile(
            title: Text(
              '${classicDivisionLabel(d)} • ${clubs.where((c) => c['divisionTier'] == d).length}/${d == 'Distrital' ? 96 : 8}',
            ),
            children: clubs
                .where((c) => c['divisionTier'] == d)
                .map(
                  (c) => ListTile(
                    dense: true,
                    title: Text('${c['name']}'),
                    subtitle: Text(
                      '${c['country']} • ${players.where((p) => p['clubId'] == c['id']).length} atletas',
                    ),
                  ),
                )
                .toList(),
          ),
      ],
    );
  }

  bool get _manuallyChanged => _divisionSlots.indexed.any(
    (entry) => entry.$2 != _automaticSlots[entry.$1],
  );

  List<Map<String, dynamic>> get _eligibleClubs => _hasReviewDraft
      ? _reviewClubs
            .map((club) => {...club, 'name': _catalogClubName(club)})
            .toList()
      : _clubs.where(_clubAllowed).toList();

  String _catalogClubName(Map club) {
    final name = club['name'].toString();
    final year = club['season']?.toString();
    if (_years.length < 2 || year == null || name.endsWith(' ($year)')) {
      return name;
    }
    return '$name ($year)';
  }

  int _compareClubs(Map<String, dynamic> a, Map<String, dynamic> b) {
    num rankFor(Map<String, dynamic> club) =>
        (club['rank'] ?? (club['initialProfile'] as Map?)?['worldRank'])
            as num? ??
        double.infinity;
    final rank = rankFor(a).compareTo(rankFor(b));
    if (rank != 0) return rank;
    final score =
        ((b['rankingInfo'] as Map?)?['score'] as num? ??
                double.negativeInfinity)
            .compareTo(
              (a['rankingInfo'] as Map?)?['score'] as num? ??
                  double.negativeInfinity,
            );
    if (score != 0) return score;
    return a['key'].toString().compareTo(b['key'].toString());
  }

  void _reconcileDivisions() {
    final keepPositions = _manuallyChanged;
    final ranked = _eligibleClubs
        .map((club) => club['key'].toString())
        .toList();
    final available = ranked.toSet();
    _automaticSlots = List.generate(
      32,
      (i) => i < ranked.length ? ranked[i] : null,
    );
    if (keepPositions) {
      if (_repairUnavailableAssignments) {
        // Keep compatible manual choices when a country or year changes.
        for (var i = 0; i < _divisionSlots.length; i++) {
          if (!available.contains(_divisionSlots[i])) _divisionSlots[i] = null;
        }
      }
      final retained = _divisionSlots.whereType<String>().toSet();
      final remaining = ranked.where((key) => !retained.contains(key)).iterator;
      for (var i = 0; i < _divisionSlots.length; i++) {
        if (_divisionSlots[i] == null && remaining.moveNext()) {
          _divisionSlots[i] = remaining.current;
        }
      }
    } else {
      _divisionSlots = List.of(_automaticSlots);
    }
    _repairUnavailableAssignments = false;
    _districtClubs.removeWhere(
      (key) => !available.contains(key) || _divisionSlots.contains(key),
    );
    _preview = null;
  }

  void _swapDivisionClub(int division, int position, String key) {
    final target = division * 8 + position;
    if (_hasReviewDraft) {
      final current = _divisionSlots[target];
      if (current != null && !_busy) {
        _run(
          () =>
              _saveReviewPlacement(_currentReviewPlacement.swap(current, key)),
        );
      }
      return;
    }
    final previous = _divisionSlots.indexOf(key);
    setState(() {
      if (previous >= 0) _divisionSlots[previous] = _divisionSlots[target];
      _divisionSlots[target] = key;
      _districtClubs.remove(key);
      _preview = null;
    });
  }

  void _restoreDivisions() => setState(() {
    if (_hasReviewDraft &&
        !_automaticSlots.whereType<String>().every(
          _reviewClubs.map((club) => club['key'].toString()).toSet().contains,
        )) {
      _reviewDraft = null;
      _reviewScope = null;
    }
    _divisionSlots = List.of(_automaticSlots);
    if (_hasReviewDraft) {
      final retained = _divisionSlots.whereType<String>().toSet();
      final remaining = _reviewClubs
          .map((club) => club['key'].toString())
          .where((key) => !retained.contains(key))
          .iterator;
      for (var index = 0; index < _divisionSlots.length; index++) {
        if (_divisionSlots[index] == null && remaining.moveNext()) {
          _divisionSlots[index] = remaining.current;
        }
      }
      _district = 'Manual';
      _districtClubs
        ..clear()
        ..addAll(
          _reviewClubs
              .map((club) => club['key'].toString())
              .where((key) => !_divisionSlots.contains(key)),
        );
    }
    _districtClubs.removeWhere(_divisionSlots.contains);
    _preview = null;
  });

  String? get _divisionProblem {
    if (_assembly != null || _fictionalOnly) return null;
    final clubs = {for (final club in _eligibleClubs) club['key']: club};
    final assigned = _divisionSlots.whereType<String>().toList();
    if (assigned.toSet().length != assigned.length) {
      return 'Há clubes repetidos nas divisões. Restaure a distribuição automática.';
    }
    if (assigned.any((key) => !clubs.containsKey(key))) {
      return 'Alguns clubes da montagem deixaram de estar disponíveis neste catálogo. Revise os países ou restaure a distribuição automática.';
    }
    if (assigned.any(
      (key) => !_countries.contains(clubs[key]!['countryCode']),
    )) {
      return 'Há clubes de fora dos países selecionados. Revise os países ou restaure a distribuição.';
    }
    if (assigned.any(_districtClubs.contains)) {
      return 'Um clube aparece nas divisões e nos distritais. Revise a distribuição.';
    }
    final missing = _divisionSlots.length - assigned.length;
    if (missing > 0 && !_generateAllRosterPlayers) {
      return 'Faltam $missing clubes para preencher as divisões. Selecione mais países ou ajuste as fontes.';
    }
    return null;
  }

  ({int clubs, int divisions, int capacity})? get _divisionSummary {
    if (_step != _divisionsStep || _assembly != null) return null;
    final available = _eligibleClubs.map((club) => club['key']).toSet();
    return (
      clubs: _divisionSlots.where(available.contains).toSet().length,
      divisions: _divisionSlots.length ~/ 8,
      capacity: 8,
    );
  }

  Widget _divisionsPanel() {
    final eligible = _eligibleClubs;
    final byKey = {for (final club in eligible) club['key']: club};
    final problem = _divisionProblem;
    final missingOverall = _worldCapacity - eligible.length;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        if (_fictionalOnly)
          const Text(
            'O mundo totalmente fictício será gerado na prévia. As escolhas abaixo são usadas ao voltar para clubes do catálogo.',
          ),
        CreatorDivisions(
          divisions: [
            for (var d = 0; d < 4; d++)
              [
                for (var slot = 0; slot < 8; slot++)
                  byKey[_divisionSlots[d * 8 + slot]],
              ],
          ],
          eligibleClubs: eligible,
          busy: _busy || _fictionalOnly,
          manuallyChanged: _manuallyChanged,
          onSwap: _swapDivisionClub,
          onRestore: _restoreDivisions,
        ),
        if (problem != null) ...[
          const SizedBox(height: 12),
          Text(problem, style: const TextStyle(fontWeight: FontWeight.bold)),
          Align(
            alignment: Alignment.centerLeft,
            child: TextButton(
              onPressed: _busy ? null : _openSourcesFromSetup,
              child: const Text('AJUSTAR ELENCOS E FONTES'),
            ),
          ),
        ] else if (!_fictionalOnly && missingOverall > 0) ...[
          const SizedBox(height: 12),
          Text(
            'Faltam $missingOverall clubes no catálogo para chegar a 128. Revise a seleção ou as opções em Elencos / fontes.',
          ),
        ],
      ],
    );
  }

  Widget _choice(
    String label,
    String? value,
    List<(String, String)> choices,
    void Function(String) change,
  ) {
    final field = DropdownButtonFormField<String>(
      key: ValueKey('$label:$value'),
      initialValue: value,
      isExpanded: true,
      isDense: true,
      decoration: InputDecoration(labelText: label),
      dropdownColor: ClassicTheme.of(context).paper.surface,
      style: const TextStyle(
        fontFamily: 'ClassicCondensed',
        fontSize: 20,
        fontWeight: FontWeight.w700,
      ),
      selectedItemBuilder: (_) => choices
          .map(
            (choice) => Builder(
              builder: (context) => Align(
                alignment: Alignment.centerLeft,
                child: Text(
                  choice.$2,
                  maxLines: 2,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(
                    color: Theme.of(
                      context,
                    ).inputDecorationTheme.labelStyle?.color,
                  ),
                ),
              ),
            ),
          )
          .toList(),
      items: choices
          .map(
            (c) => DropdownMenuItem(
              value: c.$1,
              child: Text(
                c.$2,
                style: TextStyle(
                  color: ClassicTheme.of(context).paper.textPrimary,
                  fontSize: 18,
                ),
              ),
            ),
          )
          .toList(),
      onChanged: _busy || choices.isEmpty
          ? null
          : (v) => setState(() {
              change(v!);
              if (label == 'Categoria') {
                _generateAllRosterPlayers = false;
                _rosterProblem = null;
              }
              _preview = null;
              if (label != 'Treinadores') _assembly = null;
            }),
    );
    return field;
  }

  Widget _searchField() => TextField(
    key: ValueKey('search-$_step'),
    decoration: InputDecoration(
      labelText: 'Pesquisar país',
      labelStyle: const TextStyle(fontSize: 22),
      prefixIcon: const Icon(Icons.search),
      filled: true,
      fillColor: ClassicTheme.of(context).paper.surfaceAlternate,
      border: OutlineInputBorder(borderRadius: BorderRadius.circular(10)),
      enabledBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(10),
        borderSide: BorderSide(color: ClassicTheme.of(context).paper.border),
      ),
      focusedBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(10),
        borderSide: BorderSide(
          color: ClassicTheme.of(context).paper.accent,
          width: 2,
        ),
      ),
    ),
    onChanged: (v) => setState(() => _search = v.toLowerCase()),
  );
  bool _clubAllowed(Map<String, dynamic> club) {
    return club['isFictional'] != true &&
        (club['verification'] as Map?)?['status'] == 'REAL_CLUB_VERIFIED';
  }

  List<Map<String, dynamic>> get _districtCandidates => _eligibleClubs
      .where(
        (club) =>
            _countries.contains(club['countryCode']) &&
            !_divisionSlots.contains(club['key']),
      )
      .toList();

  String? get _districtProblem {
    if (_assembly != null || _fictionalOnly) return null;
    final divisionProblem = _divisionProblem;
    if (divisionProblem != null) return divisionProblem;

    final candidates = _districtCandidates;
    final eligibleKeys = candidates.map((club) => club['key']).toSet();
    if (eligibleKeys.length != candidates.length) {
      return 'Há clubes repetidos no catálogo dos distritais.';
    }
    if (_district == 'Manual' && !_districtClubs.every(eligibleKeys.contains)) {
      return 'Há distritais selecionados que não pertencem aos países escolhidos ou já estão nas divisões.';
    }
    if (eligibleKeys.length < _districtCapacity &&
        (_district == 'Manual' || !_generateAllRosterPlayers)) {
      return 'Faltam ${_districtCapacity - eligibleKeys.length} clubes distritais. '
          '${_district == 'Manual' ? 'Revise os países ou selecione Ranking.' : 'Revise os países ou escolha elenco fictício.'}';
    }
    if (_district == 'Manual' && _districtClubs.length != _districtCapacity) {
      return 'Selecione $_districtCapacity clubes distritais para continuar. '
          '${_districtClubs.length} selecionados.';
    }
    return null;
  }

  void _selectDistrict(String key, bool selected) => setState(() {
    if (selected) {
      if (_districtClubs.length >= _districtCapacity) {
        _error = 'Selecione no máximo $_districtCapacity distritais.';
        return;
      }
      _districtClubs.add(key);
    } else {
      _districtClubs.remove(key);
    }
    _error = '';
    _preview = null;
  });

  Widget _districtsPanel() {
    final available = _districtCandidates;
    final manual = !_fictionalOnly && _district == 'Manual';
    final previewClubs = _maps(
      _preview?['clubs'],
    ).where((club) => club['division'] == 5).toList();
    final showSelection = !_fictionalOnly && (manual || _preview != null);
    final selectedKeys = manual
        ? _districtClubs
        : {
            for (final entry in previewClubs)
              if (entry['club'] case {'key': final String key}) key,
          };
    final countryNames = {
      for (final code in _countries) code: code,
      for (final country in _options)
        if (_countries.contains(country['code']))
          country['code'].toString(): country['name'].toString(),
    };
    final canFillWithGenerated = !manual && _fillClubVacancies;
    final count = manual
        ? _districtClubs.length
        : _preview != null
        ? previewClubs.length
        : canFillWithGenerated
        ? _districtCapacity
        : min(_districtCapacity, available.length);
    final problem = _districtProblem;
    return CreatorDistricts(
      districtCount: count,
      districtLimit: _districtCapacity,
      districtCountLabel: _preview != null && !manual
          ? 'Distritais na prévia'
          : null,
      availableCount: available.length,
      divisionCount: _divisionSlots.length ~/ 8,
      mode: _fictionalOnly ? 'Ranking' : _district,
      onModeChanged: _busy || _fictionalOnly ? null : _chooseDistrictMode,
      notice: problem != null
          ? Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(problem),
                if (available.length < _districtCapacity || _fictionalOnly)
                  TextButton(
                    onPressed: _busy ? null : _openSourcesFromSetup,
                    child: const Text('AJUSTAR ELENCOS E FONTES'),
                  ),
              ],
            )
          : _fictionalOnly
          ? const Text(
              '96 distritais serão gerados com a autorização do mundo fictício.',
            )
          : canFillWithGenerated && available.length < _districtCapacity
          ? Text(
              'Complemento autorizado para ${_districtCapacity - available.length} vagas distritais.',
            )
          : null,
      filters: CreatorDistrictFilters(
        view: _districtView,
        countries: countryNames,
        clubs: available,
        showSelection: showSelection,
      ),
      clubList: CreatorDistrictList(
        view: _districtView,
        countries: countryNames,
        clubs: available,
        selectedKeys: selectedKeys,
        showSelection: showSelection,
        onSelectionChanged: manual && !_busy
            ? (change) => _selectDistrict(change.$1, change.$2)
            : null,
      ),
    );
  }

  Widget _previewPanel({bool confirmation = false}) {
    if (_assembly != null) return _assemblyPanel();
    final preview = _preview;
    if (preview == null) {
      return const Text('Atualize a prévia para conferir clubes e elencos.');
    }
    return CreatorWorldReview(
      preview: {
        ...preview,
        'errors': {
          ...preview['errors'] as List? ?? [],
          ?_previewStructureProblem,
        }.toList(),
        'clubs': [
          for (final entry in _maps(preview['clubs']))
            {
              ...entry,
              'club': {
                ...Map<String, dynamic>.from(entry['club'] as Map),
                'name': _catalogClubName(entry['club'] as Map),
              },
            },
        ],
      },
      worldName: _name.text.trim(),
      years: _orderedYears,
      countries: {
        for (final option in _options)
          if (_countries.contains(option['code']))
            option['code'].toString(): option['name'].toString(),
      },
      rosterMode: _generateAllRosterPlayers ? 'Fictício' : 'Real',
      maxHumans: _humans,
      synchronized: widget.worldCatalogRules,
      confirmation: confirmation,
      busy: _busy,
      onRefresh: _busy ? null : () => _run(_makePreview),
      onEditDivisions: _busy
          ? null
          : () => setState(() => _step = _divisionsStep),
      onSwapClub: _busy || !confirmation ? null : _pickReviewClub,
    );
  }

  Widget _countryCard(Map<String, dynamic> country) {
    final theme = ClassicTheme.of(context).paper;
    final selected = _countries.contains(country['code']);
    return Container(
      key: ValueKey('country-card-${country['code']}'),
      margin: const EdgeInsets.only(bottom: 10),
      constraints: const BoxConstraints(minHeight: 70),
      decoration: BoxDecoration(
        color: selected
            ? Color.lerp(theme.surface, theme.selectedBackground, .12)
            : theme.surface,
        border: Border.all(
          color: selected ? theme.selectedBackground : theme.border,
          width: selected ? 2 : 1,
        ),
        borderRadius: BorderRadius.circular(11),
      ),
      child: CheckboxListTile(
        contentPadding: const EdgeInsets.symmetric(horizontal: 12, vertical: 7),
        secondary: CreatorCountryFlag(
          code: country['code'].toString(),
          countryName: country['name']?.toString(),
        ),
        title: Row(
          children: [
            Expanded(
              flex: 4,
              child: FittedBox(
                fit: BoxFit.scaleDown,
                alignment: Alignment.centerLeft,
                child: Text(
                  country['name'].toString(),
                  maxLines: 1,
                  style: TextStyle(
                    color: theme.textPrimary,
                    fontWeight: FontWeight.w700,
                    fontSize: 23,
                    height: 1.05,
                  ),
                ),
              ),
            ),
            Container(
              width: 1,
              height: 34,
              margin: const EdgeInsets.symmetric(horizontal: 10),
              color: theme.border,
            ),
            Expanded(
              flex: 5,
              child: Row(
                children: [
                  Icon(Icons.shield_outlined, size: 19, color: theme.accent),
                  const SizedBox(width: 5),
                  Expanded(
                    child: LayoutBuilder(
                      builder: (context, constraints) => FittedBox(
                        fit: BoxFit.scaleDown,
                        alignment: Alignment.centerLeft,
                        child: Text(
                          '${_clubCount(country)} clubes${constraints.maxWidth < 120 ? '\n' : ' '}disponíveis',
                          style: TextStyle(
                            color: theme.textPrimary,
                            fontSize: 17,
                            height: 1.1,
                          ),
                        ),
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ],
        ),
        activeColor: theme.primary,
        checkColor: readableInk(theme.primary),
        checkboxShape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(4),
        ),
        value: selected,
        onChanged: _busy
            ? null
            : (value) => setState(() {
                if (value!) {
                  if (_countries.length >= _maxCountries) {
                    _error = 'Selecione no máximo oito países.';
                    return;
                  }
                  _countries.add(country['code'].toString());
                } else {
                  _countries.remove(country['code']);
                }
                _error = '';
                _clearCatalogDraft(keepPlacement: true);
                _generateAllRosterPlayers = false;
              }),
      ),
    );
  }

  Widget _countriesPanel() {
    final countries =
        _options
            .where(
              (country) =>
                  country['name'].toString().toLowerCase().contains(_search),
            )
            .toList()
          ..sort(
            (a, b) => a['name'].toString().compareTo(b['name'].toString()),
          );
    final theme = ClassicTheme.of(context).paper;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        _searchField(),
        const SizedBox(height: 16),
        Wrap(
          spacing: 14,
          runSpacing: 8,
          crossAxisAlignment: WrapCrossAlignment.center,
          children: [
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 15, vertical: 7),
              decoration: BoxDecoration(
                color: theme.primary,
                borderRadius: BorderRadius.circular(10),
              ),
              child: Text(
                '$_selectedCountryCount países selecionados • máximo $_maxCountries',
                style: TextStyle(
                  color: readableInk(theme.primary, Colors.white),
                  fontSize: 21,
                  fontWeight: FontWeight.w700,
                ),
              ),
            ),
            TextButton.icon(
              onPressed: _busy || _countries.isEmpty
                  ? null
                  : () => setState(() {
                      _countries.clear();
                      _clearCatalogDraft(keepPlacement: true);
                      _error = '';
                    }),
              icon: const Icon(Icons.delete_outline),
              label: const Text('LIMPAR SELEÇÃO'),
            ),
          ],
        ),
        const SizedBox(height: 18),
        LayoutBuilder(
          builder: (context, constraints) {
            if (constraints.maxWidth < 900) {
              return Column(children: countries.map(_countryCard).toList());
            }
            final half = (countries.length / 2).ceil();
            return Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Expanded(
                  child: Column(
                    children: countries.take(half).map(_countryCard).toList(),
                  ),
                ),
                const SizedBox(width: 22),
                Expanded(
                  child: Column(
                    children: countries.skip(half).map(_countryCard).toList(),
                  ),
                ),
              ],
            );
          },
        ),
        const SizedBox(height: 2),
        if (!_busy && _countryLoadFailed) ...[
          const Text('Não foi possível consultar os países do catálogo local.'),
          TextButton(
            onPressed: () => _run(_loadCountries),
            child: const Text('TENTAR NOVAMENTE'),
          ),
        ] else if (!_busy && _options.isEmpty)
          const Text(
            'Nenhum país com clubes disponíveis para o catálogo selecionado.',
          )
        else if (!_busy && countries.isEmpty)
          const Text('Nenhum país corresponde à pesquisa.'),
      ],
    );
  }

  Widget _yearsPanel() {
    final theme = ClassicTheme.of(context).paper;
    final names = {
      for (final country in _options)
        country['code'].toString(): country['name'].toString(),
    };
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        const Text('Selecione os anos que deseja reunir no campeonato.'),
        const SizedBox(height: 12),
        if (!_busy && _yearsLoadFailed) ...[
          const Text('Não foi possível consultar os anos do catálogo local.'),
          TextButton(
            onPressed: () => _run(_loadYears),
            child: const Text('TENTAR NOVAMENTE'),
          ),
        ] else if (!_busy && _yearOptions.isEmpty)
          const Text('Nenhum ano com clubes nos países selecionados.'),
        for (final item in _yearOptions) _yearCard(theme, item, names),
      ],
    );
  }

  Widget _yearCard(
    ClassicTheme theme,
    Map<String, dynamic> item,
    Map<String, String> names,
  ) {
    final year = item['year'].toString();
    final coverage = _maps(item['countries']);
    final covered = coverage.map((country) => country['code']).toSet();
    final missing = _countries.where((country) => !covered.contains(country));
    final count = _clubCount(item);
    return CheckboxListTile(
      key: ValueKey('catalog-year-$year'),
      title: Text('$year • $count ${count == 1 ? 'clube' : 'clubes'}'),
      subtitle: Text(
        [
          for (final country in coverage)
            '${country['name'] ?? names[country['code']]} (${_clubCount(country)})',
          if (missing.isNotEmpty)
            'Sem clubes em ${missing.map((code) => names[code] ?? code).join(', ')}',
        ].join(' • '),
      ),
      value: _years.contains(year),
      activeColor: theme.primary,
      checkColor: readableInk(theme.primary),
      onChanged: _busy || _yearsLoadFailed
          ? null
          : (selected) => setState(() {
              if (selected == true) {
                _years.add(year);
              } else {
                _years.remove(year);
              }
              _clearCatalogDraft(keepPlacement: true);
              _error = '';
            }),
    );
  }

  void _chooseRosterOrigin(bool generated) => setState(() {
    if (generated == _generateAllRosterPlayers) return;
    _generateAllRosterPlayers = generated;
    if (_hasReviewDraft || _reviewDraft != null) {
      _reviewDraft = null;
      _reviewScope = null;
      _repairUnavailableAssignments = true;
      _reconcileDivisions();
    }
    _rosterProblem = null;
    _error = '';
    _preview = null;
  });

  Widget _sourcesPanel() => CreatorRosterSources(
    generatedOnly: _generateAllRosterPlayers,
    fillClubVacancies: _fillClubVacancies,
    fictionalWorld: _fictionalOnly,
    problem: _rosterProblem,
    onChooseGenerated: _busy ? null : _chooseRosterOrigin,
  );

  Widget _body() {
    if (_assembly != null && _step >= _yearsStep && _step <= _sourcesStep) {
      return _assemblyPanel();
    }
    switch (_step) {
      case _categoryStep:
        return Column(
          children: [
            TextButton(
              onPressed: _busy ? null : _restoreAssembly,
              child: const Text('USAR MONTAGEM SALVA'),
            ),
            if (_last != null)
              TextButton(
                onPressed: _busy ? null : _useLast,
                child: const Text('USAR ÚLTIMA CONFIGURAÇÃO'),
              ),
            TextButton(
              onPressed: _busy
                  ? null
                  : () => setState(() {
                      _clearCatalogDraft();
                      _countries.clear();
                      _years.clear();
                      _yearOptions = [];
                      _seed = _drawSeed();
                      _generateAllRosterPlayers = false;
                      _saved = false;
                    }),
              child: const Text('CRIAR NOVA CONFIGURAÇÃO'),
            ),
            TextField(
              controller: _name,
              onChanged: (_) => setState(() => _preview = null),
              decoration: const InputDecoration(labelText: 'Nome do mundo'),
            ),
            _choice(
              'Categoria',
              _gender,
              [('Male', 'MASCULINO'), ('Female', 'FEMININO')],
              (v) {
                if (_gender == v) return;
                _gender = v;
                _years.clear();
                _yearOptions = [];
                _clearCatalogDraft();
              },
            ),
            _choice('Treinadores', '$_humans', [
              for (int i = 1; i <= 8; i++) ('$i', '$i'),
            ], (v) => _humans = int.parse(v)),
            const Text(
              'Força de 1 a 50. A categoria fica fixada ao criar o mundo.',
            ),
          ],
        );
      case _countriesStep:
        return _countriesPanel();
      case _yearsStep:
        return _yearsPanel();
      case _divisionsStep:
        return _divisionsPanel();
      case _districtsStep:
        return _districtsPanel();
      case _sourcesStep:
        return _sourcesPanel();
      case _previewStep:
        return _previewPanel();
      default:
        return _previewPanel(confirmation: true);
    }
  }

  Future<void> _confirm() async {
    if (_busy || !_validPreview) return;
    await _run(() async {
      if (_assembly != null) {
        await _call(
          'assemblies/${_assembly!['id']}/confirm',
          body: {
            'idempotencyKey': _confirmationKey,
            'worldName': _name.text.trim(),
            'maxHumans': _humans,
            if (widget.worldCatalogRules) ...{
              'requireHostStart': true,
              'gameModeId': 'CLASSIC_SYNC',
            },
          },
        );
      } else {
        await _call('confirm/${_preview!['draftId']}', body: {});
      }
      if (mounted) Navigator.pop(context, true);
    });
  }

  bool get _validPreview => _assembly != null
      ? (_assembly!['validation'] as Map)['isValid'] == true
      : _preview != null &&
            (_preview!['errors'] as List?)?.isEmpty == true &&
            _preview!['pendingSlots'] == 0 &&
            _previewStructureProblem == null;

  String? get _previewStructureProblem {
    if (_preview == null) return 'Atualize a prévia antes de continuar.';
    if (_name.text.trim().isEmpty ||
        _name.text.trim().length > 160 ||
        _humans < 1 ||
        _humans > 8 ||
        _countries.isEmpty ||
        _countries.length > _maxCountries ||
        _years.isEmpty) {
      return 'Revise o nome do mundo, os treinadores, os países e os anos selecionados.';
    }
    final entries = _maps(_preview!['clubs']);
    try {
      CreatorClubPlacement.fromPreview(entries);
    } on FormatException catch (error) {
      return error.message;
    }
    for (final entry in entries) {
      final club = entry['club'] as Map;
      if (!_countries.contains(club['countryCode']) ||
          !_years.contains(club['season']?.toString())) {
        return 'Há clubes fora dos países ou anos selecionados. Atualize a prévia.';
      }
      if (!_generateAllRosterPlayers &&
          (club['players'] as List? ?? []).any(
            (player) =>
                player['playerOrigin'] == 'FICTIONAL_GENERATED' ||
                player['identityOrigin'] == 'Generated',
          )) {
        return 'O modo real contém atletas fictícios. Revise a origem dos elencos.';
      }
    }
    return null;
  }

  String _invalidRosterPreview() {
    final count = (_preview?['clubs'] as List?)?.length ?? 0;
    const switchHint =
        'Para jogar agora, selecione USAR ELENCO FICTÍCIO e clique em CONTINUAR.';
    final summary = _preview?['dataSummary'] as Map?;
    final complete = summary?['completeRealSquads'] as int?;
    if (!_generateAllRosterPlayers &&
        complete != null &&
        complete < _worldCapacity) {
      final verified = summary?['verifiedRealClubs'] ?? _eligibleClubs.length;
      final coverage = complete == 0
          ? 'ainda não tem elencos reais completos'
          : 'oferece $complete dos $_worldCapacity elencos reais completos necessários';
      return 'Nos países escolhidos, o catálogo tem $verified clubes reais e $coverage.\n'
          '$switchHint';
    }
    if (count == _worldCapacity) {
      final errors = _preview?['errors'] as List?;
      return errors?.firstOrNull?.toString() ??
          'A montagem tem vagas pendentes. Revise os clubes escolhidos.';
    }
    return _generateAllRosterPlayers
        ? 'A montagem ficou com $count/$_worldCapacity clubes. Revise as divisões e os distritais.'
        : '$count dos $_worldCapacity clubes da montagem passaram na validação dos elencos reais. '
              'Revise os clubes escolhidos.\n$switchHint';
  }

  Future<void> _next() => _run(() async {
    if (_sourcesReturnStep case final returnStep?) {
      _returnToSetup(returnStep);
      return;
    }
    if (_step == _categoryStep) {
      setState(() {
        _step = _countriesStep;
        _search = '';
        _options = [];
        _countryLoadFailed = false;
      });
      await _loadCountries();
      return;
    }
    if (_step == _countriesStep) {
      if (!_canAdvanceFromCountries) return;
      setState(() {
        _step = _yearsStep;
        _search = '';
        _yearOptions = [];
        _yearsLoadFailed = false;
      });
      if (_assembly == null) await _loadYears();
      return;
    }
    if (_step == _yearsStep && _assembly == null) {
      if (_yearsLoadFailed || _years.isEmpty) return;
      await _catalog();
    }
    if (_step == _divisionsStep && _divisionProblem != null) return;
    if (_step == _districtsStep && _districtProblem != null) return;
    if (_step == _previewStep && !_validPreview) {
      throw Exception(
        _previewStructureProblem ?? 'Resolva as pendências da prévia.',
      );
    }
    if (_step == _sourcesStep) {
      final problem = _divisionProblem ?? _districtProblem;
      if (problem != null) {
        setState(() => _rosterProblem = problem);
        return;
      }
      await _makePreview();
      if (!mounted) return;
      if (_assembly == null && !_validPreview) {
        setState(() {
          _rosterProblem = _invalidRosterPreview();
          _preview = null;
        });
        return;
      }
    }
    if (mounted) {
      setState(() {
        _step++;
        _search = '';
      });
    }
  });

  void _openSourcesFromSetup() => setState(() {
    _sourcesReturnStep = _step;
    _rosterProblem = null;
    _step = _sourcesStep;
    _search = '';
  });

  void _returnToSetup(int step) => setState(() {
    _sourcesReturnStep = null;
    _step = step;
    _search = '';
  });

  Future<void> _back() async {
    if (_sourcesReturnStep case final returnStep?) {
      _returnToSetup(returnStep);
      return;
    }
    setState(() {
      _step--;
      _search = '';
      _rosterProblem = null;
    });
    if (_step == _countriesStep && _assembly == null) {
      await _run(_loadCountries);
    }
  }

  @override
  Widget build(BuildContext context) {
    final reviewStage = _step >= _previewStep;
    final frame = CreatorFrame(
      steps: _steps,
      step: _step,
      countryCount: _selectedCountryCount,
      busy: _busy,
      error: _error,
      divisionSummary: _divisionSummary,
      forceStadium: reviewStage || _step == _sourcesStep && _assembly == null,
      stageDescription: _step == _districtsStep && _assembly == null
          ? 'Confira os clubes disponíveis para completar as competições distritais do seu mundo.'
          : null,
      nextLabel: _step == _confirmStep
          ? 'CONFIRMAR CRIAÇÃO'
          : _sourcesReturnStep != null
          ? 'APLICAR OPÇÕES'
          : 'CONTINUAR',
      finalAction: _step == _confirmStep,
      body: _body(),
      back: _busy || _step == _categoryStep ? null : _back,
      next:
          _busy ||
              (_step == _countriesStep && !_canAdvanceFromCountries) ||
              (_step == _yearsStep &&
                  _assembly == null &&
                  (_yearsLoadFailed || _years.isEmpty)) ||
              (_step == _divisionsStep && _divisionProblem != null) ||
              (_step == _districtsStep && _districtProblem != null) ||
              (_step >= _previewStep && !_validPreview)
          ? null
          : _step == _confirmStep
          ? _confirm
          : _next,
    );
    // Review and confirmation belong to the modern creator in both game skins.
    return reviewStage
        ? Theme(data: ClassicTheme.classicGreen().material(), child: frame)
        : frame;
  }
}
