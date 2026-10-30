import 'package:flutter/material.dart';
import 'api_client.dart';
import 'catalog_provenance.dart';

class WorldCreatorScreen extends StatefulWidget {
  final ElifootApiClient api;
  final String coachId;
  const WorldCreatorScreen({
    super.key,
    required this.api,
    required this.coachId,
  });
  @override
  State<WorldCreatorScreen> createState() => _WorldCreatorScreenState();
}

class _WorldCreatorScreenState extends State<WorldCreatorScreen> {
  final _name = TextEditingController(text: 'Liga Mundial 98');
  final _season = TextEditingController(text: '2026');
  final _seed = TextEditingController(text: '199801');
  String _gender = 'Male',
      _mode = 'Automatic',
      _district = 'Ranking',
      _search = '',
      _error = '';
  int _step = 0, _humans = 1;
  bool _busy = false,
      _fictional = false,
      _saved = false,
      _fictionalPlayers = false,
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
  Map<String, dynamic>? _last, _preview;
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
    _seed.dispose();
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

  Future<void> _initialize() => _run(() async {
    final options = await _call(
      'countries?season=${Uri.encodeQueryComponent(_season.text)}',
    );
    final last = await _call('last-configuration');
    if (mounted) {
      setState(() {
        _options = _maps(options);
        _last = last == null ? null : Map<String, dynamic>.from(last as Map);
      });
    }
  });
  Map<String, dynamic> _request() => {
    'name': _name.text.trim(),
    'gender': _gender,
    'season': _season.text.trim(),
    'countries': _countries.toList()..sort(),
    'mode': _mode,
    'districtMode': _district,
    'seed': int.tryParse(_seed.text) ?? 199801,
    'maxHumans': _humans,
    'assignments': _assignments.entries
        .where((a) => _mode == 'Custom' || a.value == 5)
        .map((a) => {'clubKey': a.key, 'division': a.value})
        .toList(),
    'fillRemainingDivisions': true,
    'allowFictionalTeams': _fictional,
    'allowFictionalPlayers': _fictionalPlayers,
    'includeHistoricalClubs': _includeHistorical,
    'includeUnverifiedClubs': _includeUnverified,
    'useSavedData': _saved,
  };
  void _useLast() {
    final p = _last!;
    setState(() {
      _name.text = p['name'].toString();
      _gender = p['gender'].toString();
      _season.text = p['season'].toString();
      _seed.text = p['seed'].toString();
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
      _includeHistorical = p['includeHistoricalClubs'] == true;
      _includeUnverified = p['includeUnverifiedClubs'] == true;
      _saved = p['useSavedData'] == true;
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
        final keys = _clubs.map((c) => c['key']).toSet();
        _assignments.removeWhere((key, _) => !keys.contains(key));
        _preview = null;
      });
    }
  }

  Future<void> _makePreview({bool fill = false}) async {
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
  ) => DropdownButtonFormField<String>(
    key: ValueKey('$label:$value'),
    initialValue: value,
    isExpanded: true,
    decoration: InputDecoration(labelText: label),
    items: choices
        .map((c) => DropdownMenuItem(value: c.$1, child: Text(c.$2)))
        .toList(),
    onChanged: _busy
        ? null
        : (v) => setState(() {
            change(v!);
            _preview = null;
          }),
  );
  Widget _searchField() => TextField(
    key: ValueKey('search-$_step'),
    decoration: const InputDecoration(labelText: 'Pesquisar'),
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
                  '${c['name']} • ${c['countryName']} • ranking ${c['rank'] ?? 'ausente'}',
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

  Widget _previewPanel() {
    final p = _preview;
    if (p == null) {
      return const Text('Gere a prévia para conferir clubes e elencos.');
    }
    final clubs = _maps(p['clubs']);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          '${clubs.length}/128 clubes • ${p['pendingSlots']} vagas pendentes',
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
                      '${((c['club'] as Map)['players'] as List).length} atletas',
                    ),
                    children: _maps(
                      (c['club'] as Map)['players'],
                    ).map(catalogPlayerRow).toList(),
                  ),
                )
                .toList(),
          ),
      ],
    );
  }

  Widget _body() {
    switch (_step) {
      case 0:
        return Column(
          children: [
            if (_last != null)
              TextButton(
                onPressed: _busy ? null : _useLast,
                child: const Text('USAR ÚLTIMA CONFIGURAÇÃO'),
              ),
            TextButton(
              onPressed: _busy
                  ? null
                  : () => setState(() {
                      _assignments.clear();
                      _fictional = false;
                      _fictionalPlayers = false;
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
            TextField(
              controller: _seed,
              onChanged: (_) => setState(() => _preview = null),
              keyboardType: TextInputType.number,
              decoration: const InputDecoration(labelText: 'Seed dos sorteios'),
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
        return Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            _searchField(),
            Text('${_countries.length} países selecionados'),
            TextButton(
              onPressed: _busy ? null : _initialize,
              child: const Text('CONSULTAR DISPONIBILIDADE DA TEMPORADA'),
            ),
            for (final c in _options.where(
              (c) => c['name'].toString().toLowerCase().contains(_search),
            ))
              CheckboxListTile(
                contentPadding: EdgeInsets.zero,
                title: Text(c['name'].toString()),
                subtitle: Text(
                  '${c[_gender == 'Male' ? 'maleClubs' : 'femaleClubs']} clubes disponíveis',
                ),
                value: _countries.contains(c['code']),
                onChanged: _busy
                    ? null
                    : (v) => setState(() {
                        if (v!) {
                          _countries.add(c['code'].toString());
                        } else {
                          _countries.remove(c['code']);
                        }
                        _assignments.clear();
                        _preview = null;
                      }),
              ),
            const Text(
              'Zero indica ausência de cobertura válida nesta categoria e temporada.',
            ),
          ],
        );
      case 2:
        return Column(
          children: [
            _choice('Montagem', _mode, [
              ('Automatic', 'AUTOMÁTICO POR RANKING'),
              ('Custom', 'PERSONALIZADO'),
            ], (v) => _mode = v),
            CheckboxListTile(
              contentPadding: EdgeInsets.zero,
              title: const Text('Usar imediatamente os dados salvos'),
              subtitle: const Text(
                'As datas serão exibidas. Desmarque para tentar atualizar fontes autorizadas.',
              ),
              value: _saved,
              onChanged: _busy ? null : (v) => setState(() => _saved = v!),
            ),
            CheckboxListTile(
              contentPadding: EdgeInsets.zero,
              title: const Text(
                'INCLUIR clubes históricos, com referência passada explícita',
              ),
              value: _includeHistorical,
              onChanged: _busy
                  ? null
                  : (v) => setState(() {
                      _includeHistorical = v!;
                      _preview = null;
                      _assignments.clear();
                    }),
            ),
            CheckboxListTile(
              contentPadding: EdgeInsets.zero,
              title: const Text('INCLUIR clubes sem verificação de atividade'),
              subtitle: const Text(
                'Listas antigas não comprovam participação na temporada atual.',
              ),
              value: _includeUnverified,
              onChanged: _busy
                  ? null
                  : (v) => setState(() {
                      _includeUnverified = v!;
                      _preview = null;
                      _assignments.clear();
                    }),
            ),
            const Text(
              '4 divisões de 8 clubes e 96 distritais. Ranking define apenas a distribuição inicial.',
            ),
          ],
        );
      case 3:
        return Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              '${_clubs.where(_clubAllowed).length} clubes disponíveis • ${_countries.join(', ')}',
            ),
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
            CheckboxListTile(
              contentPadding: EdgeInsets.zero,
              title: const Text(
                'AUTORIZO completar vagas com equipes e atletas fictícios',
              ),
              value: _fictional,
              onChanged: _busy
                  ? null
                  : (v) => setState(() {
                      _fictional = v!;
                      _preview = null;
                    }),
            ),
            CheckboxListTile(
              contentPadding: EdgeInsets.zero,
              title: const Text(
                'AUTORIZO atletas fictícios para completar elencos reais insuficientes',
              ),
              subtitle: const Text(
                'Atletas gerados serão identificados e não serão apresentados como importados.',
              ),
              value: _fictionalPlayers,
              onChanged: _busy
                  ? null
                  : (value) => setState(() {
                      _fictionalPlayers = value!;
                      _preview = null;
                    }),
            ),
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
                children: _maps(club['players']).map(catalogPlayerRow).toList(),
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
          ],
        );
      default:
        return Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              '${_name.text} • ${_gender == 'Female' ? 'FEMININO' : 'MASCULINO'} • ${_season.text}',
            ),
            Text('Países: ${_countries.join(', ')} • seed ${_seed.text}'),
            _previewPanel(),
            const Text(
              'A confirmação copia os dados desta prévia para um mundo independente e salva sua configuração.',
            ),
            ElevatedButton(
              onPressed: _busy || !_validPreview
                  ? null
                  : () => _run(() async {
                      await _call('confirm/${_preview!['draftId']}', body: {});
                      if (mounted) Navigator.pop(context, true);
                    }),
              child: const Text('CONFIRMAR CRIAÇÃO'),
            ),
          ],
        );
    }
  }

  bool get _validPreview =>
      _preview != null &&
      (_preview!['errors'] as List).isEmpty &&
      _preview!['pendingSlots'] == 0;
  Future<void> _next() => _run(() async {
    if (_step == 1 && _countries.isEmpty) {
      throw Exception('Selecione pelo menos um país.');
    }
    if (_step == 2) await _catalog();
    if (_step == 5) await _makePreview();
    if (mounted) {
      setState(() {
        _step++;
        _search = '';
      });
    }
  });
  @override
  Widget build(BuildContext context) => Scaffold(
    backgroundColor: const Color(0xFF062E0F),
    appBar: AppBar(
      title: const Text('CRIADOR DE MUNDOS', style: TextStyle(fontSize: 16)),
      backgroundColor: const Color(0xFF0D3B18),
    ),
    body: SafeArea(
      child: Center(
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 980),
          child: Column(
            children: [
              Padding(
                padding: const EdgeInsets.all(12),
                child: Text(
                  '${_step + 1}/8 • ${_steps[_step]}',
                  style: const TextStyle(color: Colors.amber, fontSize: 18),
                ),
              ),
              if (_busy) const LinearProgressIndicator(),
              if (_error.isNotEmpty)
                Padding(
                  padding: const EdgeInsets.all(8),
                  child: Text(
                    _error,
                    maxLines: 6,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(color: Colors.orangeAccent),
                  ),
                ),
              Expanded(
                child: SingleChildScrollView(
                  padding: const EdgeInsets.all(16),
                  child: _body(),
                ),
              ),
              Padding(
                padding: const EdgeInsets.all(10),
                child: Wrap(
                  spacing: 16,
                  children: [
                    TextButton(
                      onPressed: _busy || _step == 0
                          ? null
                          : () => setState(() {
                              _step--;
                              _search = '';
                            }),
                      child: const Text('VOLTAR'),
                    ),
                    if (_step < 7)
                      ElevatedButton(
                        onPressed: _busy || (_step == 6 && !_validPreview)
                            ? null
                            : _next,
                        child: const Text('CONTINUAR'),
                      ),
                  ],
                ),
              ),
            ],
          ),
        ),
      ),
    ),
  );
}
