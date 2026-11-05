import 'dart:math';

import 'world_catalog_status_016b.dart';
import 'club_theme.dart';
import 'package:flutter/material.dart';
import 'api_client.dart';
import 'custom_assembly_editor.dart';
import 'classic_match_board.dart';
import 'catalog_provenance.dart';
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
  final _season = TextEditingController(text: '2026');
  static int _drawSeed() => Random.secure().nextInt(2147483647) + 1;
  int _seed = _drawSeed();
  String? _countriesSeason;
  String _gender = 'Male',
      _dataMode = 'REAL_COMPLETE',
      _mode = 'Automatic',
      _district = 'Ranking',
      _search = '',
      _error = '';
  String _creation = 'Automatic';
  bool _customConsent = false, _fictionalOnly = false;
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
  final Map<String, int> _assignments = {};
  Map<String, dynamic>? _last, _preview, _dataSummary;
  static const _steps = [
    'Categoria',
    'Países',
    'Modo',
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
    _season.dispose();
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

  Future<void> _loadCountries() async {
    final season = _season.text.trim();
    final options = _maps(
      await _call('countries?season=${Uri.encodeQueryComponent(season)}'),
    );
    if (!mounted) return;
    final available = options.map((country) => country['code']).toSet();
    setState(() {
      _options = options;
      _countriesSeason = season;
      _countries.retainWhere(available.contains);
    });
  }

  Future<void> _initialize() => _run(() async {
    await _loadCountries();
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
    'season': _season.text.trim(),
    'countries': _countries.toList()..sort(),
    'mode': _mode,
    'districtMode': _district,
    'seed': _seed,
    'maxHumans': _humans,
    'assignments': _assignments.entries
        .where((a) => _mode == 'Custom' || a.value == 5)
        .map((a) => {'clubKey': a.key, 'division': a.value})
        .toList(),
    'fillRemainingDivisions': true,
    'allowFictionalTeams': _fictional,
    'allowFictionalPlayers': _fictionalPlayers,
    'allowProviderReportedPlayers':
        _providerReported && _dataMode != 'REAL_COMPLETE',
    'includeHistoricalClubs': _includeHistorical,
    'includeUnverifiedClubs': _includeUnverified,
    'useSavedData': _saved,
    if (widget.worldCatalogRules) ...{
      'useWorldCatalogRules': true,
      'fictionalOnly': _fictionalOnly,
      'requireHostStart': true,
      'gameModeId': 'CLASSIC_SYNC',
    },
  };
  void _useLast() {
    _assembly = null;
    final p = _last!;
    setState(() {
      _name.text = p['name'].toString();
      _gender = p['gender'].toString();
      _dataMode =
          p['dataMode']?.toString() ??
          (p['allowFictionalTeams'] == true
              ? 'FREE_WORLD'
              : p['allowFictionalPlayers'] == true
              ? 'REAL_CLUBS_SIMULATED'
              : 'REAL_COMPLETE');
      _season.text = p['season'].toString();
      _seed = (p['seed'] as num?)?.toInt() ?? _seed;
      _mode = p['mode'].toString();
      _district = p['districtMode'].toString();
      _humans = (p['maxHumans'] as num?)?.toInt() ?? 1;
      _countries
        ..clear()
        ..addAll((p['countries'] as List).cast<String>());
      _assignments
        ..clear()
        ..addEntries(
          _maps(p['assignments']).map(
            (a) => MapEntry(
              a['clubKey'].toString(),
              (a['division'] as num).toInt(),
            ),
          ),
        );
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
      await _call('catalog', body: _request()) as Map,
    );
    if (mounted) {
      setState(() {
        _clubs = _maps(data['clubs']);
        _references = _maps(data['references']);
        _warnings = data['warnings'] as List? ?? [];
        _dataSummary = data['dataSummary'] is Map
            ? Map<String, dynamic>.from(data['dataSummary'] as Map)
            : null;
        final keys = _clubs.map((c) => c['key']).toSet();
        _assignments.removeWhere((key, _) => !keys.contains(key));
        _preview = null;
      });
    }
  }

  Future<void> _makePreview({bool fill = false}) async {
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
    if (mounted) {
      setState(() {
        _preview = data;
        if (fill) {
          for (final item in _maps(data['clubs'])) {
            final c = Map<String, dynamic>.from(item['club'] as Map);
            if (c['isFictional'] != true && item['division'] != 5) {
              _assignments[c['key'].toString()] = (item['division'] as num)
                  .toInt();
            }
          }
        }
      });
    }
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

  Future<void> _editPreview() => _run(() async {
    if (_assembly == null) {
      if (!_validPreview) {
        throw Exception('Gere uma prévia válida antes de editar.');
      }
      final data = Map<String, dynamic>.from(
        await _call(
              'assemblies',
              body: {
                'name': _name.text,
                'gender': _gender,
                'countries': _countries.toList(),
                'seed': _seed,
                'sourcePreviewId': _preview!['draftId'],
              },
            )
            as Map,
      );
      if (!mounted) return;
      setState(() => _applyAssembly(data));
    }
    await _openAssemblyEditor(_assembly!);
  });
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
        _step = 6;
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
          onPressed: _busy ? null : _editPreview,
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

  Future<void> _assign(Map<String, dynamic> club, int? target) async {
    final key = club['key'].toString(), previous = _assignments[club['key']];
    String? swap;
    if (target == previous) return;
    if (target != null &&
        _assignments.values.where((d) => d == target).length >=
            (target == 5 ? 96 : 8)) {
      swap = await showDialog<String>(
        context: context,
        builder: (ctx) => SimpleDialog(
          title: const Text('Escolha o clube para trocar'),
          children: _clubs
              .where((c) => _assignments[c['key']] == target)
              .map(
                (c) => SimpleDialogOption(
                  onPressed: () => Navigator.pop(ctx, c['key'].toString()),
                  child: Text(c['name'].toString()),
                ),
              )
              .toList(),
        ),
      );
      if (swap == null) return;
    }
    if (mounted) {
      setState(() {
        _assignments.remove(key);
        if (swap != null) {
          _assignments.remove(swap);
          if (previous != null) _assignments[swap] = previous;
        }
        if (target != null) _assignments[key] = target;
        _preview = null;
      });
    }
  }

  Widget _choice(
    String label,
    String value,
    List<(String, String)> choices,
    void Function(String) change,
  ) {
    final field = DropdownButtonFormField<String>(
      key: ValueKey('$label:$value'),
      initialValue: value,
      isExpanded: true,
      isDense: _step != 2,
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
      onChanged: _busy
          ? null
          : (v) => setState(() {
              change(v!);
              if (label == 'Categoria' || label == 'Dados do mundo') {
                _fictional = false;
                _fictionalPlayers = false;
                _includeHistorical = false;
                _includeUnverified = false;
                _assignments.clear();
                _clubs = [];
                _dataSummary = null;
              }
              _preview = null;
              if (label != 'Treinadores') _assembly = null;
            }),
    );
    if (_step != 2) return field;
    return CreatorField(
      icon: switch (label) {
        'Como montar' => Icons.layers_outlined,
        'Modo de jogo' => Icons.sports_esports_outlined,
        'Dados do mundo' => Icons.storage_outlined,
        _ => Icons.settings_outlined,
      },
      prominent: label == 'Dados do mundo' || label == 'Montagem',
      child: field,
    );
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
    final verification = club['verification'];
    if (verification is! Map) return true;
    return (verification['status'] != 'REAL_CLUB_HISTORICAL' ||
            _includeHistorical) &&
        (verification['status'] != 'REAL_CLUB_UNVERIFIED' ||
            _includeUnverified);
  }

  Widget _clubList() {
    final ranked = _clubs.where(_clubAllowed).toList()
      ..sort((a, b) {
        if (widget.worldCatalogRules) {
          final ap = a['initialProfile'] as Map? ?? {},
              bp = b['initialProfile'] as Map? ?? {};
          int power = ((bp['clubInitialPower'] as num?) ?? 1).compareTo(
            (ap['clubInitialPower'] as num?) ?? 1,
          );
          if (power != 0) return power;
          int score = ((bp['worldRankingScore'] as num?) ?? -1e12).compareTo(
            (ap['worldRankingScore'] as num?) ?? -1e12,
          );
          if (score != 0) return score;
          int rank = ((ap['worldRank'] as num?) ?? 2147483647).compareTo(
            (bp['worldRank'] as num?) ?? 2147483647,
          );
          if (rank != 0) return rank;
          return a['key'].toString().compareTo(b['key'].toString());
        }
        final compare = ((a['rank'] as num?)?.toInt() ?? 2147483647).compareTo(
          (b['rank'] as num?)?.toInt() ?? 2147483647,
        );
        return compare == 0
            ? a['key'].toString().compareTo(b['key'].toString())
            : compare;
      });
    final reserved = ranked.take(32).map((c) => c['key']).toSet();
    final clubs = _clubs
        .where(_clubAllowed)
        .where(
          (c) =>
              _mode != 'Automatic' ||
              _step != 4 ||
              !reserved.contains(c['key']),
        )
        .where(
          (c) => '${c['name']} ${c['countryName']}'.toLowerCase().contains(
            _search,
          ),
        )
        .toList();
    return SizedBox(
      height: 340,
      child: ListView.builder(
        itemCount: clubs.length,
        itemBuilder: (ctx, i) {
          final c = clubs[i], key = clubs[i]['key'].toString();
          final value = _assignments[key] ?? 0;
          return Padding(
            padding: const EdgeInsets.symmetric(vertical: 6),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  '${c['name']} • ${c['countryName']} • ranking ${c['rank'] ?? 'ausente'}${c['initialProfile'] is Map ? ' • Força clube ${c['initialProfile']['clubInitialPower']}/20' : ''}',
                ),
                DropdownButtonFormField<int>(
                  key: ValueKey('$key:$value'),
                  initialValue: value,
                  isExpanded: true,
                  items: [
                    const DropdownMenuItem(
                      value: 0,
                      child: Text('Fora do mundo'),
                    ),
                    for (int d = 1; d <= 5; d++)
                      if (_mode == "Custom" || _step != 4 || d == 5)
                        DropdownMenuItem(
                          value: d,
                          child: Text(d == 5 ? 'Distritais' : 'DIV$d'),
                        ),
                  ],
                  onChanged: _busy || (_mode == 'Automatic' && _step == 3)
                      ? null
                      : (v) => _assign(c, v == 0 ? null : v),
                ),
              ],
            ),
          );
        },
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
                _fictionalPlayers = value;
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
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          '${clubs.length}/128 clubes • ${p['pendingSlots']} vagas pendentes',
        ),
        _dataCounts(summary),
        if (p['dataAsOf'] != null)
          Text('Vínculos conferidos em ${p['dataAsOf']}'),
        for (final reference in _maps(p['references']))
          Text(
            '${(reference['scope'] as Map)['provider']} • referência ${reference['updatedAt']} • versão ${reference['version']}\n'
            '${reference['rights'] is Map ? (reference['rights'] as Map)['attribution'] ?? (reference['rights'] as Map)['license'] : ''}',
          ),
        if (summary != null)
          Text(
            '${summary['selectedRealClubs']} clubes reais selecionados • ${summary['selectedFictionalClubs']} fictícios • ${summary['realCurrentPlayers']} atletas reais confirmados • ${summary['generatedPlayers']} fictícios gerados',
          ),
        for (final e in p['errors'] as List? ?? [])
          Text(
            e.toString(),
            style: const TextStyle(color: Colors.orangeAccent),
          ),
        for (final w in p['warnings'] as List? ?? [])
          Text(w.toString(), style: const TextStyle(color: Colors.amber)),
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
        secondary: CreatorCountryFlag(code: country['code'].toString()),
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
                          '${country[_gender == 'Male' ? 'maleClubs' : 'femaleClubs']} clubes${constraints.maxWidth < 120 ? '\n' : ' '}disponíveis',
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
                  if (widget.worldCatalogRules && _countries.length >= 8) {
                    _error = 'Selecione no máximo oito países.';
                    return;
                  }
                  _countries.add(country['code'].toString());
                } else {
                  _countries.remove(country['code']);
                }
                _assembly = null;
                _assignments.clear();
                _fictional = false;
                _fictionalPlayers = false;
                _dataSummary = null;
                _preview = null;
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
                '${_countries.length} países selecionados${widget.worldCatalogRules ? ' • máximo 8' : ''}',
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
                        _assignments.clear();
                        _preview = null;
                      }),
                icon: const Icon(Icons.delete_outline),
                label: const Text('LIMPAR SELEÇÃO'),
              ),
              TextButton.icon(
                onPressed: _busy
                    ? null
                    : () => Navigator.push(
                        context,
                        MaterialPageRoute(
                          builder: (_) => WorldCatalogStatus016B(
                            api: widget.api,
                            coachId: widget.coachId,
                            gender: _gender,
                            season: _season.text.trim(),
                          ),
                        ),
                      ),
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
        if (_options.isEmpty && !_busy)
          const Text(
            'A lista de países não está disponível. Consulte a disponibilidade da temporada para tentar novamente.',
          )
        else if (countries.isEmpty && !_busy)
          const Text('Nenhum país corresponde à pesquisa.')
        else
          const Text(
            'Zero indica ausência de cobertura válida nesta categoria e temporada.',
            style: TextStyle(fontSize: 15),
          ),
      ],
    );
  }

  Widget _modePanel() => Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      LayoutBuilder(
        builder: (context, constraints) {
          final assembly = _choice(
            'Como montar',
            _creation,
            [
              ('Automatic', 'GERAR MUNDO AUTOMÁTICO'),
              ('Choose', 'ESCOLHER CLUBES E DIVISÕES'),
              ('FullCustom', 'CRIAR MUNDO PERSONALIZADO'),
            ],
            (value) {
              _creation = value;
              _mode = value == 'Choose' ? 'Custom' : 'Automatic';
              _assembly = null;
            },
          );
          if (_creation == 'FullCustom' || !widget.worldCatalogRules) {
            return assembly;
          }
          final gameplay = _choice('Modo de jogo', 'CLASSIC_SYNC', [
            ('CLASSIC_SYNC', 'CLÁSSICO SINCRONIZADO'),
          ], (_) {});
          return constraints.maxWidth < 700
              ? Column(children: [assembly, gameplay])
              : Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Expanded(child: assembly),
                    const SizedBox(width: 20),
                    Expanded(child: gameplay),
                  ],
                );
        },
      ),
      if (_creation == 'FullCustom')
        CreatorConsent(
          icon: Icons.edit_note,
          title: 'AUTORIZO CLUBES E JOGADORES PERSONALIZADOS',
          description:
              'Começar com 128 clubes fictícios editáveis, usando os países e a categoria selecionados.',
          value: _customConsent,
          onChanged: _busy
              ? null
              : (value) => setState(() => _customConsent = value == true),
        ),
      if (_creation != 'FullCustom') ...[
        if (widget.worldCatalogRules)
          Container(
            margin: const EdgeInsets.only(bottom: 18),
            decoration: BoxDecoration(
              border: Border.all(color: ClassicTheme.of(context).paper.border),
              borderRadius: BorderRadius.circular(8),
            ),
            child: SwitchListTile.adaptive(
              dense: true,
              visualDensity: const VisualDensity(vertical: -2),
              key: const Key('fictional-world-016b'),
              secondary: const Icon(Icons.language, size: 28),
              title: const Text(
                'MUNDO TOTALMENTE FICTÍCIO • AUTORIZO 128 CLUBES E ATLETAS GERADOS',
                style: TextStyle(
                  fontSize: 20,
                  fontWeight: FontWeight.bold,
                  height: 1.1,
                ),
              ),
              subtitle: const Text(
                'Classificação dos clubes: 1–20. Elencos iniciais: ciclo de 23 a 17 atletas; clubes de elite podem iniciar com 23. A força individual continua 1–50.',
                style: TextStyle(fontSize: 15, height: 1.15),
              ),
              value: _fictionalOnly,
              onChanged: _busy
                  ? null
                  : (value) => setState(() {
                      _fictionalOnly = value;
                      _preview = null;
                      _assignments.clear();
                      if (_fictionalOnly) {
                        _dataMode = 'FREE_WORLD';
                        _fictional = true;
                        _fictionalPlayers = true;
                      }
                    }),
            ),
          ),
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
            if (value != 'FREE_WORLD') _fictionalOnly = false;
          },
        ),
        Builder(
          builder: (context) {
            final theme = ClassicTheme.of(context);
            final background = Color.lerp(
              theme.surface,
              theme.selectedBackground,
              .22,
            )!;
            return Container(
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(
                color: background,
                borderRadius: BorderRadius.circular(5),
              ),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Icon(
                    Icons.info_outline,
                    color: readableInk(background),
                    size: 23,
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Text(
                      'O modo somente real bloqueia a criação se faltarem elencos atuais comprovados. Complementação depende de autorização explícita.',
                      style: TextStyle(
                        color: readableInk(background),
                        fontSize: 17,
                        fontWeight: FontWeight.w700,
                        height: 1.1,
                      ),
                    ),
                  ),
                ],
              ),
            );
          },
        ),
        _dataConsentChoices(),
        const SizedBox(height: 18),
        _choice('Montagem', _mode, [
          ('Automatic', 'AUTOMÁTICO POR RANKING'),
          ('Custom', 'ESCOLHER CLUBES E DIVISÕES'),
        ], (value) => _mode = value),
        CreatorConsent(
          icon: Icons.save_outlined,
          title: 'Usar imediatamente os dados salvos',
          description:
              'As datas serão exibidas. Desmarque para tentar atualizar fontes autorizadas.',
          value: _saved,
          onChanged: _busy ? null : (value) => setState(() => _saved = value!),
        ),
        CreatorConsent(
          icon: Icons.history,
          title: 'INCLUIR clubes históricos, com referência passada explícita',
          value: _includeHistorical,
          onChanged: _busy || _dataMode != 'FREE_WORLD'
              ? null
              : (value) => setState(() {
                  _includeHistorical = value!;
                  _preview = null;
                  _assignments.clear();
                }),
        ),
        CreatorConsent(
          icon: Icons.fact_check_outlined,
          title: 'INCLUIR clubes sem verificação de atividade',
          description:
              'Listas antigas não comprovam participação na temporada atual.',
          value: _includeUnverified,
          onChanged: _busy || _dataMode != 'FREE_WORLD'
              ? null
              : (value) => setState(() {
                  _includeUnverified = value!;
                  _preview = null;
                  _assignments.clear();
                }),
        ),
        const Padding(
          padding: EdgeInsets.only(top: 12),
          child: Text(
            '4 divisões de 8 clubes e 96 distritais. Ranking define apenas a distribuição inicial.',
            style: TextStyle(fontSize: 15),
          ),
        ),
      ],
    ],
  );

  Widget _body() {
    if (_assembly != null && _step >= 3 && _step <= 5) return _assemblyPanel();
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
                      _assembly = null;
                      _seed = _drawSeed();
                      _assignments.clear();
                      _dataMode = 'REAL_COMPLETE';
                      _dataSummary = null;
                      _fictional = false;
                      _fictionalPlayers = false;
                      _fictionalOnly = false;
                      _includeHistorical = false;
                      _includeUnverified = false;
                      _preview = null;
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
                _gender = v;
                _assignments.clear();
                _clubs = [];
              },
            ),
            TextField(
              controller: _season,
              onChanged: (_) => setState(() {
                _preview = null;
                _assignments.clear();
              }),
              decoration: const InputDecoration(
                labelText: 'Temporada do catálogo',
              ),
            ),
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
        return _modePanel();
      case 3:
        return Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              '${_clubs.where(_clubAllowed).length} clubes disponíveis • ${_countries.join(', ')}',
            ),
            _dataCounts(_dataSummary),
            Wrap(
              spacing: 12,
              children: [
                for (int d = 1; d <= 4; d++)
                  Text(
                    'DIV$d: ${_assignments.values.where((v) => v == d).length}/8',
                  ),
              ],
            ),
            TextButton(
              onPressed: _busy
                  ? null
                  : () => _run(() => _makePreview(fill: true)),
              child: const Text('PREENCHER VAGAS POR RANKING'),
            ),
            const Text(
              'No modo personalizado, escolha uma divisão. Divisões cheias permitem trocar clubes.',
            ),
            _searchField(),
            _clubList(),
          ],
        );
      case 4:
        return Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            _choice('Distritais', _district, [
              ('Ranking', 'Ranking dos clubes restantes'),
              ('Random', 'Sorteio com seed salva'),
              ('Manual', 'Escolher os 96 manualmente'),
            ], (v) => _district = v),
            _dataConsentChoices(),
            Text(
              '${_assignments.values.where((v) => v == 5).length}/96 distritais escolhidos',
            ),
            if (_district == 'Manual') ...[_searchField(), _clubList()],
            const Text(
              'Os clubes das quatro divisões são excluídos do preenchimento distrital.',
            ),
          ],
        );
      case 5:
        return Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            for (final warning in _warnings)
              Text(
                warning.toString(),
                style: const TextStyle(color: Colors.amber),
              ),
            for (final reference in _references)
              Padding(
                padding: const EdgeInsets.symmetric(vertical: 5),
                child: Text(
                  '${(reference['scope'] as Map)['countryCode']} • ${(reference['scope'] as Map)['dataType']} • ${(reference['scope'] as Map)['provider']}\n'
                  '${reference['status']} • referência ${reference['updatedAt']} • versão ${reference['version']}${reference['isFixture'] == true ? ' • FICTÍCIO' : ''}\n'
                  '${reference['rights'] is Map ? (reference['rights'] as Map)['attribution'] ?? (reference['rights'] as Map)['license'] : ''}',
                ),
              ),
            const Text(
              'Forças estimadas são calculadas na prévia e não são notas individuais oficiais. Confira o elenco final antes de confirmar.',
            ),
            for (final club in _clubs) ...[
              catalogClubStatus(club),
              catalogRankingLabel(club),
              for (final issue in club['coverageWarnings'] as List? ?? [])
                Text(
                  issue.toString(),
                  style: const TextStyle(color: Colors.amber),
                ),
              ExpansionTile(
                title: Text(
                  '${club['name']} • ranking ${club['rank'] ?? 'ausente'}',
                ),
                subtitle: Text(
                  '${(club['players'] as List).length} atuais utilizáveis • ${club['documentedPlayers'] ?? (club['players'] as List).length} documentados • ${club['uncertainPlayers'] ?? 0} vínculos não confirmados',
                ),
                children: [
                  ..._maps(club['players']).map(catalogPlayerRow),
                  if (_maps(club['referencePlayers']).isNotEmpty)
                    ExpansionTile(
                      title: Text(
                        '${_maps(club['referencePlayers']).length} referências documentais — fora do elenco atual',
                      ),
                      children: _maps(
                        club['referencePlayers'],
                      ).map(catalogPlayerRow).toList(),
                    ),
                ],
              ),
            ],
          ],
        );
      case 6:
        return Column(
          children: [
            TextButton(
              onPressed: _busy ? null : () => _run(() => _makePreview()),
              child: const Text('ATUALIZAR PRÉVIA'),
            ),
            _previewPanel(),
            if (_assembly == null)
              TextButton(
                onPressed: _busy || !_validPreview ? null : _editPreview,
                child: const Text('REVISAR / EDITAR MONTAGEM'),
              ),
          ],
        );
      default:
        return Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              '${_name.text} • ${_gender == 'Female' ? 'FEMININO' : 'MASCULINO'} • ${_season.text}',
            ),
            Text('Países: ${_countries.join(', ')} • modo $_dataMode'),
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
    if (_step == 0 && _countriesSeason != _season.text.trim()) {
      setState(() {
        _step = 1;
        _search = '';
        _options = [];
      });
      await _loadCountries();
      return;
    }
    if (_step == 1 &&
        (_countries.isEmpty ||
            widget.worldCatalogRules && _countries.length > 8)) {
      throw Exception('Selecione pelo menos um país.');
    }
    if (_step == 2) {
      if (_creation == 'FullCustom') {
        if (!_customConsent) {
          throw Exception(
            'Autorize explicitamente clubes e jogadores personalizados.',
          );
        }
        if (_assembly == null) {
          final data = Map<String, dynamic>.from(
            await _call(
                  'assemblies',
                  body: {
                    'name': _name.text,
                    'gender': _gender,
                    'countries': _countries.toList(),
                    'seed': _seed,
                    'allowPersonalizedContent': true,
                    if (widget.worldCatalogRules) 'useWorldCatalogRules': true,
                  },
                )
                as Map,
          );
          if (mounted) setState(() => _applyAssembly(data));
        }
      } else {
        await _catalog();
      }
    }
    if (_step == 5) await _makePreview();
    if (mounted) {
      setState(() {
        _step++;
        _search = '';
      });
    }
  });
  @override
  Widget build(BuildContext context) => CreatorFrame(
    steps: _steps,
    step: _step,
    countryCount: _countries.length,
    busy: _busy,
    error: _error,
    body: _body(),
    back: _busy || _step == 0
        ? null
        : () => setState(() {
            _step--;
            _search = '';
          }),
    next:
        _busy ||
            (_step == 1 && _options.isEmpty) ||
            (_step == 6 && !_validPreview)
        ? null
        : _next,
  );
}
