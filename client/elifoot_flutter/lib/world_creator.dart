import 'dart:math';

import 'world_catalog_status_016b.dart';
import 'club_theme.dart';
import 'package:flutter/material.dart';
import 'api_client.dart';
import 'custom_assembly_editor.dart';
import 'classic_match_board.dart';
import 'catalog_provenance.dart';
import 'creator_divisions.dart';
import 'creator_district_list.dart';
import 'creator_districts.dart';
import 'creator_visual.dart';

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
  String? _season;
  List<String> _seasons = [];
  bool _seasonLoadFailed = false, _countryLoadFailed = false;
  static const _maxCountries = 8;
  static int _drawSeed() => Random.secure().nextInt(2147483647) + 1;
  int _seed = _drawSeed();
  String _gender = 'Male',
      _dataMode = 'REAL_COMPLETE',
      _district = 'Ranking',
      _search = '',
      _error = '';
  bool _fictionalOnly = false;
  int? _sourcesReturnStep;
  Map<String, dynamic>? _assembly;
  final _confirmationKey = 'create-${DateTime.now().microsecondsSinceEpoch}';
  int _step = 0, _humans = 1;
  bool _busy = false,
      _fictional = false,
      _saved = false,
      _fictionalPlayers = false,
      _providerReported = false,
      _includeHistorical = false,
      _includeUnverified = false;
  final Set<String> _countries = {
    'BR',
    'AR',
    'GB',
    'ES',
    'DE',
    'IT',
    'FR',
    'PT',
  };
  List<Map<String, dynamic>> _options = [], _clubs = [], _references = [];
  List<dynamic> _warnings = [];
  final _districtClubs = <String>{};
  final _districtView = CreatorDistrictViewState();
  static const _districtCapacity = 96;
  List<String?> _divisionSlots = List.filled(32, null);
  List<String?> _automaticSlots = List.filled(32, null);
  Map<String, dynamic>? _last, _preview;
  static const _steps = [
    'Categoria',
    'Países',
    'Divisões',
    'Distritais',
    'Elencos / fontes',
    'Prévia',
    'Confirmar',
  ];
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

  Future<void> _loadSeasons() async {
    try {
      final seasons = List<String>.from(await _call('seasons') as List);
      if (!mounted) return;
      setState(() {
        _seasonLoadFailed = false;
        final firstAvailableCatalog = _seasons.isEmpty;
        _seasons = seasons;
        if (firstAvailableCatalog) {
          _selectSeason(seasons.firstOrNull);
        } else if (_season != null && !seasons.contains(_season)) {
          _selectSeason(null);
          if (seasons.isNotEmpty) {
            _error =
                'O ano selecionado não está mais disponível. Escolha outro ano do catálogo.';
          }
        }
      });
    } catch (_) {
      if (mounted) setState(() => _seasonLoadFailed = true);
    }
  }

  void _selectSeason(String? season) {
    if (_season == season) return;
    _season = season;
    _clearCatalogDraft();
  }

  void _clearCatalogDraft({bool keepPlacement = false}) {
    _assembly = null;
    _clubs = [];
    _references = [];
    _warnings = [];
    _preview = null;
    if (keepPlacement) return;
    _districtView.reset();
    _districtClubs.clear();
    _divisionSlots = List.filled(32, null);
    _automaticSlots = List.filled(32, null);
  }

  int _clubCount(Map<String, dynamic> country) =>
      (country[_gender == 'Male' ? 'maleClubs' : 'femaleClubs'] as num?)
          ?.toInt() ??
      0;

  int get _selectedCountryCount => _options.isEmpty ? 0 : _countries.length;

  bool get _canAdvanceFromCountries =>
      !_countryLoadFailed && _selectedCountryCount > 0;

  Future<void> _loadCountries() async {
    final season = _season;
    if (season == null) return;
    final path = Uri(
      path: 'countries',
      queryParameters: {'season': season, 'gender': _gender},
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

  Future<void> _initialize() => _run(() async {
    await _loadSeasons();
    if (!mounted || _seasonLoadFailed) return;
    final last = await _call('last-configuration');
    if (mounted) {
      setState(() {
        _last = last == null ? null : Map<String, dynamic>.from(last as Map);
      });
    }
  });
  Map<String, dynamic> _request() => {
    'name': _name.text.trim(),
    'gender': _gender,
    'dataMode': _dataMode,
    'season': _season,
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
    'fillRemainingDivisions': false,
    'preserveRankedSquadStrengths': true,
    'allowFictionalTeams': _fictional,
    'allowFictionalPlayers': _fictionalPlayers,
    'allowProviderReportedPlayers':
        _providerReported && _dataMode != 'REAL_COMPLETE',
    'includeHistoricalClubs': _includeHistorical,
    'includeUnverifiedClubs': _includeUnverified,
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
      _dataMode =
          p['dataMode']?.toString() ??
          (p['allowFictionalTeams'] == true
              ? 'FREE_WORLD'
              : p['allowFictionalPlayers'] == true
              ? 'REAL_CLUBS_SIMULATED'
              : 'REAL_COMPLETE');
      final presetSeason = p['season']?.toString();
      _selectSeason(_seasons.contains(presetSeason) ? presetSeason : null);
      _error = _season == null
          ? 'O ano da última configuração não está disponível. Escolha um ano do catálogo.'
          : '';
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
      _fictional = p['allowFictionalTeams'] == true;
      _fictionalPlayers = p['allowFictionalPlayers'] == true;
      _providerReported = p['allowProviderReportedPlayers'] == true;
      _includeHistorical = p['includeHistoricalClubs'] == true;
      _includeUnverified = p['includeUnverifiedClubs'] == true;
      _saved = p['useSavedData'] == true;
      _fictionalOnly = p['fictionalOnly'] == true;
      _preview = null;
    });
  }

  Future<void> _catalog() async {
    final data = Map<String, dynamic>.from(
      await _call(
            'catalog',
            body: {..._request(), 'dataMode': null, 'useSavedData': true},
          )
          as Map,
    );
    if (mounted) {
      setState(() {
        _clubs = _maps(data['clubs'])..sort(_compareClubs);
        _references = _maps(data['references']);
        _warnings = data['warnings'] as List? ?? [];
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
    final data = Map<String, dynamic>.from(
      await _call('preview', body: _request()) as Map,
    );
    if (mounted) setState(() => _preview = data);
  }

  Future<void> _previewWithSimulatedSquads() async {
    setState(() {
      _dataMode = 'REAL_CLUBS_SIMULATED';
      _fictionalPlayers = true;
      _fictional = false;
      _fictionalOnly = false;
      _preview = null;
    });
    if (mounted) await _makePreview();
  }

  void _applyAssembly(Map<String, dynamic> data) {
    _assembly = data;
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
        _step = 5;
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

  List<Map<String, dynamic>> get _eligibleClubs =>
      _clubs.where(_clubAllowed).toList();

  int _compareClubs(Map<String, dynamic> a, Map<String, dynamic> b) {
    if (widget.worldCatalogRules) {
      final first = a['initialProfile'] as Map? ?? {};
      final second = b['initialProfile'] as Map? ?? {};
      final power = (second['clubInitialPower'] as num? ?? 1).compareTo(
        first['clubInitialPower'] as num? ?? 1,
      );
      if (power != 0) return power;
      final score =
          (second['worldRankingScore'] as num? ?? double.negativeInfinity)
              .compareTo(
                first['worldRankingScore'] as num? ?? double.negativeInfinity,
              );
      if (score != 0) return score;
      final rank = (first['worldRank'] as num? ?? 2147483647).compareTo(
        second['worldRank'] as num? ?? 2147483647,
      );
      if (rank != 0) return rank;
    } else {
      final rank = (a['rank'] as num? ?? 2147483647).compareTo(
        b['rank'] as num? ?? 2147483647,
      );
      if (rank != 0) return rank;
    }
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
      // Preserve explicit choices even if a catalog change invalidates them.
      // The editor shows the affected slots and lets the coach restore the ranking.
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
    _districtClubs.removeWhere(
      (key) => !available.contains(key) || _divisionSlots.contains(key),
    );
    _preview = null;
  }

  void _swapDivisionClub(int division, int position, String key) {
    final target = division * 8 + position;
    final previous = _divisionSlots.indexOf(key);
    setState(() {
      if (previous >= 0) _divisionSlots[previous] = _divisionSlots[target];
      _divisionSlots[target] = key;
      _districtClubs.remove(key);
      _preview = null;
    });
  }

  void _restoreDivisions() => setState(() {
    _divisionSlots = List.of(_automaticSlots);
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
    if (missing > 0) {
      return 'Faltam $missing clubes para preencher as divisões. Selecione mais países ou ajuste as fontes.';
    }
    return null;
  }

  ({int clubs, int divisions, int capacity})? get _divisionSummary {
    if (_step != 2 || _assembly != null) return null;
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
    final missingOverall = 128 - eligible.length;
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
                _fictional = false;
                _fictionalPlayers = false;
                _fictionalOnly = false;
                _providerReported = false;
                _includeHistorical = false;
                _includeUnverified = false;
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
      labelText: _step == 1 ? 'Pesquisar país' : 'Pesquisar',
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
    if (club['isFictional'] == true) return false;
    return switch ((club['verification'] as Map?)?['status']) {
      'REAL_CLUB_VERIFIED' => true,
      'REAL_CLUB_HISTORICAL' => _includeHistorical,
      'REAL_CLUB_UNVERIFIED' => _includeUnverified,
      _ => false,
    };
  }

  List<Map<String, dynamic>> get _districtCandidates => _eligibleClubs
      .where(
        (club) =>
            _countries.contains(club['countryCode']) &&
            !_divisionSlots.contains(club['key']),
      )
      .toList();

  bool get _canGenerateDistricts =>
      _dataMode == 'FREE_WORLD' && _fictional && _fictionalPlayers;

  String? get _districtProblem {
    if (_assembly != null) return null;
    if (_fictionalOnly) {
      return _canGenerateDistricts
          ? null
          : 'Autorize clubes e atletas fictícios em Elencos / fontes.';
    }
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
        (_district == 'Manual' || !_canGenerateDistricts)) {
      return 'Faltam ${_districtCapacity - eligibleKeys.length} clubes distritais. '
          'Revise os países ou ajuste as fontes.';
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
    final canFillWithGenerated = !manual && _canGenerateDistricts;
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
      onModeChanged: _busy || _fictionalOnly
          ? null
          : (mode) => setState(() {
              _district = mode;
              _preview = null;
              _error = '';
            }),
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

  Widget _dataCounts(Map<String, dynamic>? summary) {
    if (summary == null) return const SizedBox.shrink();
    return Text(
      '${summary['verifiedRealClubs']} clubes reais verificados • ${summary['currentSeasonConfirmedClubs']} com temporada atual comprovada\n'
      '${summary['completeRealSquads']} elencos reais completos • ${summary['incompleteRealSquads']} incompletos • ${summary['missingClubSlots']} vagas a completar no catálogo',
    );
  }

  String _rosterLabel(Map club) {
    final generated = (club['players'] as List? ?? [])
        .where(
          (p) =>
              p['playerOrigin'] == 'FICTIONAL_GENERATED' ||
              p['identityOrigin'] == 'Generated',
        )
        .length;
    final verified =
        (club['verification'] as Map?)?['status'] == 'REAL_CLUB_VERIFIED';
    return verified && generated > 0
        ? 'CLUBE REAL — ELENCO SIMULADO'
        : club['isFictional'] == true
        ? 'CLUBE FICTÍCIO — ELENCO SIMULADO'
        : 'Confira a origem de cada atleta';
  }

  Widget _dataConsentChoices() => Column(
    children: [
      CreatorConsent(
        key: const Key('allow-provider-reported-016a'),
        icon: Icons.link_outlined,
        title: 'USAR elencos atuais reportados pelo fornecedor',
        description:
            'Vínculo reportado não é verificação independente. A origem e a data ficam no snapshot. Em SOMENTE DADOS REAIS COMPLETOS, apenas vínculos verificados são aceitos.',
        value: _providerReported && _dataMode != 'REAL_COMPLETE',
        onChanged: _busy || _dataMode == 'REAL_COMPLETE'
            ? null
            : (v) => setState(() {
                _providerReported = v!;
                _preview = null;
              }),
      ),
      CreatorConsent(
        icon: Icons.edit_outlined,
        title:
            'AUTORIZO atletas fictícios para completar elencos reais insuficientes',
        description:
            'A origem fictícia e a força estimada ficam identificadas no mundo.',
        value: _fictionalPlayers,
        onChanged: _busy || _dataMode == 'REAL_COMPLETE'
            ? null
            : (value) => setState(() {
                _fictionalPlayers = value!;
                if (!_fictionalPlayers) {
                  _fictional = false;
                  _fictionalOnly = false;
                }
                _preview = null;
              }),
      ),
      CreatorConsent(
        icon: Icons.groups_outlined,
        title: 'AUTORIZO completar vagas com equipes e atletas fictícios',
        value: _fictional,
        onChanged: _busy || _dataMode != 'FREE_WORLD'
            ? null
            : (value) => setState(() {
                _fictional = value!;
                if (value) _fictionalPlayers = true;
                if (!value) _fictionalOnly = false;
                _preview = null;
              }),
      ),
    ],
  );
  Widget _previewPanel() {
    if (_assembly != null) return _assemblyPanel();
    final p = _preview;
    if (p == null) {
      return const Text('Gere a prévia para conferir clubes e elencos.');
    }
    final clubs = _maps(p['clubs']);
    final summary = p['dataSummary'] is Map
        ? Map<String, dynamic>.from(p['dataSummary'] as Map)
        : null;
    final clubCount = Text(
      '${clubs.length}/128 clubes • ${p['pendingSlots']} vagas pendentes',
    );
    final names = {
      for (final country in _options) country['code']: country['name'],
    };
    final contextSummary = Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text('${_name.text} • Ano do catálogo: ${_season ?? '—'}'),
        Text(
          'Países: ${_countries.map((code) => names[code] ?? code).join(', ')}',
        ),
        Text(
          _fictionalOnly
              ? 'Mundo totalmente fictício autorizado.'
              : _fictional
              ? 'Complementos autorizados: clubes e atletas fictícios.'
              : _fictionalPlayers
              ? 'Complemento autorizado: atletas fictícios.'
              : 'Complementos fictícios sem autorização.',
        ),
      ],
    );
    final details = [
      for (final e in p['errors'] as List? ?? [])
        Text(e.toString(), style: const TextStyle(color: Colors.orangeAccent)),
      for (final w in p['warnings'] as List? ?? [])
        Text(w.toString(), style: const TextStyle(color: Colors.amber)),
    ];
    final verifiedClubs = summary?['verifiedRealClubs'] as num? ?? 0;
    if (_step == 5 &&
        _dataMode == 'REAL_COMPLETE' &&
        clubs.isEmpty &&
        verifiedClubs >= 128 &&
        summary?['completeRealSquads'] == 0) {
      return Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          contextSummary,
          clubCount,
          Text(
            'Há $verifiedClubs clubes reais verificados nesta seleção, mas nenhum tem '
            'elenco real completo na temporada. Para jogar com esses clubes, você pode '
            'autorizar atletas simulados para completar os elencos.',
          ),
          const SizedBox(height: 12),
          ElevatedButton(
            onPressed: _busy ? null : () => _run(_previewWithSimulatedSquads),
            child: const Text('AUTORIZAR ATLETAS SIMULADOS E GERAR PRÉVIA'),
          ),
          if (details.isNotEmpty)
            ExpansionTile(
              title: const Text('Detalhes da prévia'),
              children: details,
            ),
        ],
      );
    }
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        contextSummary,
        clubCount,
        _dataCounts(summary),
        if (p['dataAsOf'] != null)
          Text('Vínculos conferidos em ${p['dataAsOf']}'),
        if (summary != null)
          Text(
            '${summary['selectedRealClubs']} clubes reais selecionados • ${summary['selectedFictionalClubs']} fictícios • ${summary['realCurrentPlayers']} atletas reais confirmados • ${summary['generatedPlayers']} fictícios gerados',
          ),
        if (details.isNotEmpty)
          ExpansionTile(
            initiallyExpanded: (p['errors'] as List? ?? []).isNotEmpty,
            title: const Text('Avisos e pendências da prévia'),
            children: details,
          ),
        for (int d = 1; d <= 5; d++)
          ExpansionTile(
            title: Text(
              '${d == 5 ? 'DISTRITAIS' : 'DIV$d'} • ${clubs.where((c) => c['division'] == d).length}/${d == 5 ? 96 : 8}',
            ),
            children: clubs
                .where((c) => c['division'] == d)
                .map(
                  (c) => ExpansionTile(
                    title: Text((c['club'] as Map)['name'].toString()),
                    subtitle: Text(
                      '${_rosterLabel(c['club'] as Map)} • ${((c['club'] as Map)['players'] as List).length} atletas',
                    ),
                    children: [
                      catalogClubStatus(
                        Map<String, dynamic>.from(c['club'] as Map),
                      ),
                      catalogRankingLabel(
                        Map<String, dynamic>.from(c['club'] as Map),
                      ),
                      ..._maps(
                        (c['club'] as Map)['players'],
                      ).map(catalogPlayerRow),
                    ],
                  ),
                )
                .toList(),
          ),
      ],
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
                _fictionalOnly = false;
                _fictional = false;
                _fictionalPlayers = false;
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
            if (widget.worldCatalogRules) ...[
              TextButton.icon(
                onPressed: _busy
                    ? null
                    : () => setState(() {
                        _countries.clear();
                        _clearCatalogDraft(keepPlacement: true);
                        _error = '';
                      }),
                icon: const Icon(Icons.delete_outline),
                label: const Text('LIMPAR SELEÇÃO'),
              ),
              TextButton.icon(
                onPressed: _busy ? null : _openCatalogStatus,
                icon: const Icon(Icons.sync),
                label: const Text('CACHE, ATUALIZAÇÕES E COBERTURA'),
              ),
            ],
            TextButton.icon(
              onPressed: _busy ? null : () => _run(_loadCountries),
              icon: const Icon(Icons.calendar_month_outlined),
              label: const Text('CONSULTAR DISPONIBILIDADE DA TEMPORADA'),
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
        if (!_busy && _countryLoadFailed)
          const Text(
            'A lista de países não está disponível. Consulte a disponibilidade da temporada para tentar novamente.',
          )
        else if (!_busy && _options.isEmpty)
          const Text(
            'Nenhum país com clubes disponíveis para o catálogo selecionado.',
          )
        else if (!_busy && countries.isEmpty)
          const Text('Nenhum país corresponde à pesquisa.'),
      ],
    );
  }

  Future<void> _openCatalogStatus() async {
    final season = _season;
    if (season == null) return;
    await Navigator.push<void>(
      context,
      MaterialPageRoute(
        builder: (_) => WorldCatalogStatus016B(
          api: widget.api,
          coachId: widget.coachId,
          gender: _gender,
          season: season,
        ),
      ),
    );
    if (mounted) await _run(_loadCountries);
  }

  Widget _sourcesPanel() => Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      _choice(
        'Dados do mundo',
        _dataMode,
        [
          ('REAL_COMPLETE', 'SOMENTE DADOS REAIS COMPLETOS'),
          ('REAL_CLUBS_SIMULATED', 'CLUBES REAIS + ELENCOS SIMULADOS'),
          ('FREE_WORLD', 'MUNDO LIVRE'),
        ],
        (value) {
          _dataMode = value;
          if (value != 'FREE_WORLD') {
            _fictionalOnly = false;
            _fictional = false;
            _includeHistorical = false;
            _includeUnverified = false;
          }
          if (value == 'REAL_COMPLETE') {
            _fictionalPlayers = false;
            _providerReported = false;
          }
          _reconcileDivisions();
        },
      ),
      const SizedBox(height: 12),
      Text(switch (_dataMode) {
        'REAL_COMPLETE' =>
          'Exige elencos reais verificados. Para usar elencos do fornecedor ou autorizar complementos, escolha outro modo de dados.',
        'REAL_CLUBS_SIMULATED' =>
          'Mantém clubes reais verificados. Você pode autorizar atletas simulados para completar elencos.',
        _ =>
          'Permite ampliar o catálogo e completar vagas conforme as autorizações abaixo.',
      }),
      const SizedBox(height: 12),
      Text('ELENCOS', style: Theme.of(context).textTheme.titleMedium),
      _dataConsentChoices(),
      const SizedBox(height: 12),
      Text(
        'CLUBES DO CATÁLOGO',
        style: Theme.of(context).textTheme.titleMedium,
      ),
      CreatorConsent(
        icon: Icons.history,
        title: 'INCLUIR clubes históricos, com referência passada explícita',
        description:
            'Disponível em Mundo livre. Os clubes mantêm a identificação histórica.',
        value: _includeHistorical,
        onChanged: _busy || _dataMode != 'FREE_WORLD'
            ? null
            : (value) => setState(() {
                _includeHistorical = value!;
                _reconcileDivisions();
              }),
      ),
      CreatorConsent(
        icon: Icons.fact_check_outlined,
        title: 'INCLUIR clubes sem verificação de atividade',
        description:
            'Disponível em Mundo livre. A atividade na temporada permanece sem verificação.',
        value: _includeUnverified,
        onChanged: _busy || _dataMode != 'FREE_WORLD'
            ? null
            : (value) => setState(() {
                _includeUnverified = value!;
                _reconcileDivisions();
              }),
      ),
      if (widget.worldCatalogRules)
        SwitchListTile.adaptive(
          key: const Key('fictional-world-016b'),
          secondary: const Icon(Icons.language),
          title: const Text(
            'MUNDO TOTALMENTE FICTÍCIO • AUTORIZO 128 CLUBES E ATLETAS GERADOS',
          ),
          subtitle: const Text(
            'Gera uma montagem separada das escolhas do catálogo.',
          ),
          value: _fictionalOnly,
          onChanged: _busy
              ? null
              : (value) => setState(() {
                  _fictionalOnly = value;
                  _fictional = value;
                  _fictionalPlayers = value;
                  if (value) _dataMode = 'FREE_WORLD';
                  _preview = null;
                }),
        ),
      if (!widget.worldCatalogRules)
        CreatorConsent(
          icon: Icons.save_outlined,
          title: 'Usar imediatamente os dados salvos',
          description:
              'Desmarque para permitir atualizar as fontes autorizadas ao gerar a prévia.',
          value: _saved,
          onChanged: _busy ? null : (value) => setState(() => _saved = value!),
        ),
      ExpansionTile(
        title: const Text('Fontes e avisos do catálogo'),
        children: [
          for (final warning in _warnings)
            ListTile(dense: true, title: Text(warning.toString())),
          for (final reference in _references)
            ListTile(
              dense: true,
              title: Text(
                '${(reference['scope'] as Map)['countryCode']} • ${(reference['scope'] as Map)['dataType']} • ${(reference['scope'] as Map)['provider']}',
              ),
              subtitle: Text(
                '${reference['status']} • referência ${reference['updatedAt']}${reference['isFixture'] == true ? ' • FICTÍCIO' : ''}'
                '\n${reference['rights'] is Map ? (reference['rights'] as Map)['attribution'] ?? (reference['rights'] as Map)['license'] : ''}',
              ),
            ),
        ],
      ),
    ],
  );

  Widget _body() {
    if (_assembly != null && _step >= 2 && _step <= 4) return _assemblyPanel();
    switch (_step) {
      case 0:
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
                      _seed = _drawSeed();
                      _dataMode = 'REAL_COMPLETE';
                      _fictional = false;
                      _fictionalPlayers = false;
                      _fictionalOnly = false;
                      _includeHistorical = false;
                      _includeUnverified = false;
                      _providerReported = false;
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
                _clearCatalogDraft();
              },
            ),
            _choice(
              'ANO DO CATÁLOGO',
              _season,
              [for (final year in _seasons) (year, year)],
              (year) {
                _selectSeason(year);
                _error = '';
              },
            ),
            if (!_busy && (_seasons.isEmpty || _seasonLoadFailed)) ...[
              Text(
                _seasonLoadFailed
                    ? 'Não foi possível consultar os anos do catálogo.'
                    : 'Nenhum catálogo disponível.',
              ),
              TextButton(
                onPressed: _initialize,
                child: const Text('TENTAR NOVAMENTE'),
              ),
            ],
            _choice('Treinadores', '$_humans', [
              for (int i = 1; i <= 8; i++) ('$i', '$i'),
            ], (v) => _humans = int.parse(v)),
            const Text(
              'Força de 1 a 50. A categoria fica fixada ao criar o mundo.',
            ),
          ],
        );
      case 1:
        return _countriesPanel();
      case 2:
        return _divisionsPanel();
      case 3:
        return _districtsPanel();
      case 4:
        return _sourcesPanel();
      case 5:
        return Column(
          children: [
            Wrap(
              alignment: WrapAlignment.center,
              spacing: 12,
              children: [
                TextButton(
                  onPressed: _busy ? null : () => _run(_makePreview),
                  child: const Text('ATUALIZAR PRÉVIA'),
                ),
                if (_assembly == null)
                  TextButton.icon(
                    onPressed: _busy ? null : () => setState(() => _step = 2),
                    icon: const Icon(Icons.edit_outlined),
                    label: const Text('EDITAR DIVISÕES'),
                  ),
              ],
            ),
            _previewPanel(),
          ],
        );
      default:
        return Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            _previewPanel(),
            const Text(
              'A confirmação copia os dados desta prévia para um mundo independente e salva sua configuração.',
            ),
            ElevatedButton(
              onPressed: _busy || !_validPreview
                  ? null
                  : () => _run(() async {
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
                        await _call(
                          'confirm/${_preview!['draftId']}',
                          body: {},
                        );
                      }
                      if (mounted) Navigator.pop(context, true);
                    }),
              child: const Text('CONFIRMAR CRIAÇÃO'),
            ),
          ],
        );
    }
  }

  bool get _validPreview => _assembly != null
      ? (_assembly!['validation'] as Map)['isValid'] == true
      : _preview != null &&
            (_preview!['errors'] as List).isEmpty &&
            _preview!['pendingSlots'] == 0;
  Future<void> _next() => _run(() async {
    if (_sourcesReturnStep case final returnStep?) {
      _returnToSetup(returnStep);
      return;
    }
    if (_step == 0) {
      await _loadSeasons();
      if (!mounted || _seasonLoadFailed || _season == null) return;
      setState(() {
        _step = 1;
        _search = '';
        _options = [];
        _countryLoadFailed = false;
      });
      await _loadCountries();
      return;
    }
    if (_step == 1) {
      if (!_canAdvanceFromCountries) return;
      if (_assembly == null) await _catalog();
    }
    if (_step == 2 && _divisionProblem != null) return;
    if (_step == 3 && _districtProblem != null) return;
    if (_step == 4) {
      if (_divisionProblem != null) {
        _returnToSetup(2);
        return;
      }
      if (_districtProblem != null) {
        _returnToSetup(3);
        return;
      }
      await _makePreview();
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
    _step = 4;
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
    });
    if (_step == 0) await _run(_loadSeasons);
    if (_step == 1) await _run(_loadCountries);
  }

  @override
  Widget build(BuildContext context) => CreatorFrame(
    steps: _steps,
    step: _step,
    countryCount: _selectedCountryCount,
    busy: _busy,
    error: _error,
    divisionSummary: _divisionSummary,
    stageDescription: _step == 3 && _assembly == null
        ? 'Confira os clubes disponíveis para completar as competições distritais do seu mundo.'
        : null,
    nextLabel: _sourcesReturnStep != null ? 'APLICAR OPÇÕES' : 'CONTINUAR',
    body: _body(),
    back: _busy || _step == 0 ? null : _back,
    next:
        _busy ||
            (_step == 0 && (_season == null || _seasonLoadFailed)) ||
            (_step == 1 && !_canAdvanceFromCountries) ||
            (_step == 2 && _divisionProblem != null) ||
            (_step == 3 && _districtProblem != null) ||
            (_step == 5 && !_validPreview)
        ? null
        : _next,
  );
}
