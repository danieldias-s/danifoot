import 'dart:math';
import 'dart:convert';
import 'package:flutter/material.dart';
import 'api_client.dart';

/// Separate import path: the old private assembly editor cannot round-trip this schema.
class OfflineCatalogCreator extends StatefulWidget {
  final ElifootApiClient api;
  final String coachId;
  const OfflineCatalogCreator({
    super.key,
    required this.api,
    required this.coachId,
  });

  @override
  State<OfflineCatalogCreator> createState() => _OfflineCatalogCreatorState();
}

class _OfflineCatalogCreatorState extends State<OfflineCatalogCreator> {
  final _name = TextEditingController(text: 'Liga offline 98');
  final _catalogIds = <String>{}, _clubKeys = <String>{};
  final _divisions = <String, int>{};
  final _seed = Random.secure().nextInt(2147483646) + 1;
  List<Map<String, dynamic>> _catalogs = [], _clubs = [];
  Map<String, dynamic>? _preview;
  String? _country, _year;
  String _error = '';
  int _humans = 1;
  bool _busy = false;

  @override
  void initState() {
    super.initState();
    _run(_load);
  }

  @override
  void dispose() {
    _name.dispose();
    super.dispose();
  }

  List<Map<String, dynamic>> _maps(dynamic data) => (data as List? ?? [])
      .map((x) => Map<String, dynamic>.from(x as Map))
      .toList();
  Future<dynamic> _call(String path, {Map<String, dynamic>? body}) =>
      widget.api.worldCreator(widget.coachId, 'offline/$path', body: body);
  Future<void> _run(Future<void> Function() action) async {
    setState(() {
      _busy = true;
      _error = '';
    });
    try {
      await action();
    } catch (e) {
      if (mounted) setState(() => _error = e.toString());
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _load() async {
    final report = Map<String, dynamic>.from(await _call('catalogs') as Map);
    if (!mounted) return;
    setState(() {
      _catalogs = _maps(report['catalogs']);
      _error = _maps(report['issues']).map((i) => i['message']).join('\n');
    });
  }

  bool _applyV16 = true;
  bool _surplus = false;
  bool get _hasV11 => _clubs.any((c) => c['offlineSource']?['v11'] != null);
  int get _realLimit => _surplus ? 127 : 128;

  Map<String, dynamic> _request() => {
    'name': _name.text.trim(),
    'catalogIds': _catalogIds.toList(),
    'seed': _seed,
    'maxHumans': _humans,
    if (_hasV11 && _applyV16) 'applyV16RosterPolicy': true,
    if (_hasV11 && _applyV16 && _surplus) 'includeSurplusClub': true,
    'clubKeys': _clubKeys.toList(),
    'assignments': [
      for (final key in _clubKeys)
        if ((_divisions[key] ?? 0) > 0)
          {'clubKey': key, 'division': _divisions[key]},
    ],
  };

  Future<void> _loadClubs() async {
    final clubs = _maps(await _call('clubs', body: _request()));
    if (!mounted) return;
    setState(() {
      _clubs = clubs;
      _clubKeys.clear();
      if (!_hasV11) _surplus = false;
      _clubKeys.addAll(clubs.take(_realLimit).map((c) => c['key'] as String));
      _divisions.clear();
      _preview = null;
    });
  }

  Future<void> _makePreview() async {
    final preview = Map<String, dynamic>.from(
      await _call('preview', body: _request()) as Map,
    );
    if (mounted) setState(() => _preview = preview);
  }

  Future<void> _confirm() async {
    await _call('confirm/${_preview!['draftId']}', body: {});
    if (mounted) Navigator.pop(context, true);
  }

  void _changeCatalog(String id, bool selected) => setState(() {
    if (selected) {
      _catalogIds.add(id);
    } else {
      _catalogIds.remove(id);
    }
    _clubs = [];
    _surplus = false;
    _clubKeys.clear();
    _divisions.clear();
    _preview = null;
  });

  Map<String, dynamic> _definition(Map<String, dynamic> c) =>
      Map<String, dynamic>.from(
        c['offlineSource']?['definition'] as Map? ?? {},
      );

  Widget _clubDetails(Map<String, dynamic> c) {
    final source = c['offlineSource'] as Map? ?? {};
    final v11 = source['v11'] as Map?;
    final definition = _definition(c);
    final identity = v11?['identity'] as Map? ?? definition;
    final ranking = v11?['historicalRanking'] as Map?;
    final crest = source['crestDataUrl'] as String?;
    final colors = identity['colors'] as List? ?? [];
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Wrap(
          spacing: 6,
          crossAxisAlignment: WrapCrossAlignment.center,
          children: [
            if (crest != null && crest.startsWith('data:image/'))
              Image.memory(
                base64Decode(crest.split(',').last),
                width: 32,
                height: 32,
                errorBuilder: (_, _, _) => const Icon(Icons.shield),
              ),
            for (final hex in colors)
              if (RegExp(r'^#[0-9a-fA-F]{6}$').hasMatch(hex.toString()))
                Container(
                  width: 18,
                  height: 18,
                  color: Color(
                    int.parse('FF${hex.toString().substring(1)}', radix: 16),
                  ),
                ),
          ],
        ),
        Text(
          '${(definition['players'] as List? ?? c['players'] as List? ?? []).length} atletas • ${definition['coach']?['name'] ?? ''}',
        ),
        if (c['referencePlayers'] != null)
          Text(
            'Selecionados: ${(c['players'] as List? ?? []).length} • Candidatos preservados: ${(c['referencePlayers'] as List).length}',
          ),
        for (final warning in (c['coverageWarnings'] as List? ?? []))
          Text(warning.toString()),
        if (v11 != null) ...[
          Text(
            'Força coletiva: ${v11['teamStrength']}/20 • Prioridade: ${v11['tieBreakPriority'] ?? 'não definida'}',
          ),
          Text(
            ranking == null
                ? 'Ranking ausente: exige desempate manual nas vagas.'
                : 'Rating: ${ranking['rating']} • ${ranking['source']} • ${ranking['referenceDate']}',
          ),
        ],
      ],
    );
  }

  @override
  Widget build(BuildContext context) {
    final manifests = _catalogs.where((c) => c['manifest'] != null).toList();
    final countries = <String, String>{};
    for (final c in manifests) {
      final country = c['manifest']['country'];
      if (country != null && country['code'] != null) {
        countries[country['code'].toString()] = country['name'].toString();
      }
    }
    final years =
        manifests
            .where(
              (c) =>
                  _country == null ||
                  c['manifest']['country']?['code'] == _country,
            )
            .map((c) => c['manifest']['year'].toString())
            .toSet()
            .toList()
          ..sort();
    final filtered = manifests
        .where(
          (c) =>
              (_country == null ||
                  c['manifest']['country']?['code'] == _country) &&
              (_year == null || c['manifest']['year'].toString() == _year),
        )
        .toList();
    final previewClubs = _maps(_preview?['clubs']);
    final errors = (_preview?['errors'] as List? ?? [])
        .map((x) => x.toString())
        .toList();
    return Scaffold(
      appBar: AppBar(title: const Text('Catálogo histórico offline')),
      body: Align(
        alignment: Alignment.topCenter,
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 1180),
          child: ListView(
            padding: const EdgeInsets.all(16),
            children: [
              const Text(
                'Arquivos locais • Catálogos 1.0 e 1.1 • Revisão antes de criar',
              ),
              const SizedBox(height: 8),
              const Text(
                'O formato atual exige 128 clubes: quatro divisões de 8 e 96 distritais. Rascunhos não criam mundos. Nenhum elenco será completado automaticamente.',
              ),
              if (_busy) const LinearProgressIndicator(),
              if (_error.isNotEmpty)
                Padding(
                  padding: const EdgeInsets.symmetric(vertical: 12),
                  child: Text(
                    _error,
                    key: const Key('offline-error'),
                    style: TextStyle(
                      color: Theme.of(context).colorScheme.error,
                    ),
                  ),
                ),
              if (_preview == null) ...[
                TextField(
                  controller: _name,
                  enabled: !_busy,
                  maxLength: 160,
                  decoration: const InputDecoration(
                    labelText: 'Nome do novo mundo',
                  ),
                ),
                Wrap(
                  spacing: 16,
                  runSpacing: 12,
                  children: [
                    SizedBox(
                      width: 240,
                      child: DropdownButtonFormField<String>(
                        initialValue: _country,
                        isExpanded: true,
                        decoration: const InputDecoration(labelText: 'País'),
                        items: [
                          const DropdownMenuItem(
                            value: '',
                            child: Text('Todos os países'),
                          ),
                          for (final c in countries.entries)
                            DropdownMenuItem(
                              value: c.key,
                              child: Text(c.value),
                            ),
                        ],
                        onChanged: _busy
                            ? null
                            : (v) => setState(() {
                                _country = v == '' ? null : v;
                                _year = null;
                              }),
                      ),
                    ),
                    SizedBox(
                      width: 160,
                      child: DropdownButtonFormField<String>(
                        key: ValueKey('year-$_country'),
                        initialValue: _year,
                        isExpanded: true,
                        decoration: const InputDecoration(labelText: 'Ano'),
                        items: [
                          const DropdownMenuItem(
                            value: '',
                            child: Text('Todos os anos'),
                          ),
                          for (final y in years)
                            DropdownMenuItem(value: y, child: Text(y)),
                        ],
                        onChanged: _busy
                            ? null
                            : (v) => setState(() => _year = v == '' ? null : v),
                      ),
                    ),
                    SizedBox(
                      width: 180,
                      child: DropdownButtonFormField<int>(
                        initialValue: _humans,
                        decoration: const InputDecoration(
                          labelText: 'Treinadores humanos',
                        ),
                        items: [
                          for (int n = 1; n <= 8; n++)
                            DropdownMenuItem(value: n, child: Text('$n')),
                        ],
                        onChanged: _busy
                            ? null
                            : (v) => setState(() => _humans = v ?? 1),
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 16),
                Text('Catálogos selecionados: ${_catalogIds.length}'),
                if (filtered.isEmpty)
                  const Text('Nenhum catálogo para este país/ano.'),
                for (final c in filtered)
                  CheckboxListTile(
                    contentPadding: EdgeInsets.zero,
                    value: _catalogIds.contains(c['manifest']['catalogId']),
                    title: Text(
                      '${c['manifest']['name']} • ${c['manifest']['year']}',
                    ),
                    subtitle: Text(
                      '${c['ready'] == true ? 'READY' : 'RASCUNHO / INVÁLIDO'} • ${(c['clubs'] as List? ?? []).length} clubes\n${_maps(c['issues']).map((i) => i['message']).join('\n')}',
                    ),
                    onChanged: _busy || c['ready'] != true
                        ? null
                        : (v) => _changeCatalog(
                            c['manifest']['catalogId'].toString(),
                            v ?? false,
                          ),
                  ),
                for (final c in _catalogs.where((c) => c['manifest'] == null))
                  Text(
                    'Arquivo inválido: ${c['path']}\n${_maps(c['issues']).map((i) => i['message']).join('\n')}',
                  ),
                Wrap(
                  spacing: 12,
                  children: [
                    OutlinedButton(
                      onPressed: _busy ? null : () => _run(_load),
                      child: const Text('RELER ARQUIVOS'),
                    ),
                    FilledButton(
                      onPressed: _busy || _catalogIds.isEmpty
                          ? null
                          : () => _run(_loadClubs),
                      child: const Text('CARREGAR CLUBES'),
                    ),
                  ],
                ),
                if (_clubs.isNotEmpty) ...[
                  const Divider(),
                  if (_hasV11) ...[
                    CheckboxListTile(
                      key: const Key('offline-v16-roster'),
                      contentPadding: EdgeInsets.zero,
                      title: const Text(
                        'Selecionar elenco por divisão inicial (V1.6)',
                      ),
                      subtitle: const Text(
                        'Preserva o catálogo completo; cortes e selecionados ficam na prévia.',
                      ),
                      value: _applyV16,
                      onChanged: _busy
                          ? null
                          : (value) => setState(() {
                              _applyV16 = value ?? false;
                              if (!_applyV16) {
                                _surplus = false;
                                _clubKeys.addAll(
                                  _clubs
                                      .take(128)
                                      .map((c) => c['key'] as String),
                                );
                              }
                            }),
                    ),
                    CheckboxListTile(
                      key: const Key('offline-v16-surplus'),
                      contentPadding: EdgeInsets.zero,
                      title: const Text(
                        'Incluir clube fictício de excedentes: 127 reais + 1',
                      ),
                      subtitle: const Text(
                        'Exige 23 atletas cortados, incluindo 3 goleiros reais. Sem candidatos suficientes, a prévia bloqueia a criação.',
                      ),
                      value: _surplus,
                      onChanged: _busy || !_applyV16
                          ? null
                          : (value) => setState(() {
                              _surplus = value ?? false;
                              _clubKeys.clear();
                              _clubKeys.addAll(
                                _clubs
                                    .take(_realLimit)
                                    .map((c) => c['key'] as String),
                              );
                              _divisions.clear();
                            }),
                    ),
                  ],
                  Text('Clubes selecionados: ${_clubKeys.length}/$_realLimit'),
                  Text(
                    'Classificação geral: ${_clubs.length} candidatos • Primeiros ${min(_realLimit, _clubs.length)} • Excedentes: ${max(0, _clubs.length - _realLimit)}',
                  ),
                  Text(
                    'Os primeiros $_realLimit estão pré-selecionados. Todos os excedentes permanecem abaixo para seleção manual. Nenhum clube é apagado.',
                  ),
                  if (_clubs.any((c) => c['offlineSource']?['v11'] != null))
                    const Text(
                      '1.1: força ↓, rating comparável ↓, prioridade ↑. Fontes e datas incompatíveis exigem prioridade manual. A prévia identifica empates entre divisões e gera as forças individuais 1..50.',
                    ),
                  const Text(
                    'Divisão automática respeita a divisão inicial do arquivo. Para substituir, escolha outra divisão. A prévia valida todas as vagas.',
                  ),
                  for (final c in _clubs)
                    Card(
                      child: Padding(
                        padding: const EdgeInsets.all(8),
                        child: LayoutBuilder(
                          builder: (context, constraints) => Wrap(
                            spacing: 16,
                            runSpacing: 8,
                            crossAxisAlignment: WrapCrossAlignment.center,
                            children: [
                              SizedBox(
                                width: min(620, constraints.maxWidth),
                                child: CheckboxListTile(
                                  contentPadding: EdgeInsets.zero,
                                  value: _clubKeys.contains(c['key']),
                                  title: Text(
                                    '${_clubs.indexOf(c) + 1}. ${c['name']} • ${c['countryCode']} ${c['season']}${_clubs.indexOf(c) >= 128 ? ' • EXCEDENTE' : ''}',
                                  ),
                                  subtitle: _clubDetails(c),
                                  onChanged: _busy
                                      ? null
                                      : (v) => setState(() {
                                          if (v == true) {
                                            _clubKeys.add(c['key']);
                                          } else {
                                            _clubKeys.remove(c['key']);
                                          }
                                        }),
                                ),
                              ),
                              SizedBox(
                                width: min(200, constraints.maxWidth),
                                child: DropdownButtonFormField<int>(
                                  initialValue: _divisions[c['key']] ?? 0,
                                  isExpanded: true,
                                  decoration: const InputDecoration(
                                    labelText: 'Divisão',
                                  ),
                                  items: [
                                    const DropdownMenuItem(
                                      value: 0,
                                      child: Text('Automática / arquivo'),
                                    ),
                                    for (int d = 1; d <= 5; d++)
                                      DropdownMenuItem(
                                        value: d,
                                        child: Text(
                                          d == 5 ? 'Distrital' : '$dª Divisão',
                                        ),
                                      ),
                                  ],
                                  onChanged: _busy
                                      ? null
                                      : (v) => setState(
                                          () => _divisions[c['key']] = v ?? 0,
                                        ),
                                ),
                              ),
                            ],
                          ),
                        ),
                      ),
                    ),
                  FilledButton(
                    onPressed: _busy || _clubKeys.length != _realLimit
                        ? null
                        : () => _run(_makePreview),
                    child: const Text('VALIDAR PRÉVIA'),
                  ),
                ],
              ] else ...[
                Text(
                  _preview!['name'].toString(),
                  style: Theme.of(context).textTheme.titleLarge,
                ),
                for (final e in errors)
                  Text(
                    e,
                    style: TextStyle(
                      color: Theme.of(context).colorScheme.error,
                    ),
                  ),
                for (final warning in (_preview?['warnings'] as List? ?? []))
                  Text(warning.toString()),
                for (int d = 1; d <= 5; d++)
                  Text(
                    '${d == 5 ? 'Distritais' : '$dª Divisão'}: ${previewClubs.where((c) => c['division'] == d).length}/${d == 5 ? 96 : 8}',
                  ),
                const Text(
                  'Prévia congelada. 1.0 conserva forças literais; 1.1 gera e congela forças individuais 1..50 na prévia. Identidade, elenco, comportamento, estrela e treinador ficam congelados na prévia.',
                ),
                for (final c in previewClubs)
                  ListTile(
                    dense: true,
                    title: Text(c['club']['name'].toString()),
                    subtitle: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          '${c['club']['countryCode']} ${c['club']['season']} • divisão ${c['division']}',
                        ),
                        _clubDetails(
                          Map<String, dynamic>.from(c['club'] as Map),
                        ),
                      ],
                    ),
                  ),
                Wrap(
                  spacing: 12,
                  children: [
                    OutlinedButton(
                      onPressed: _busy
                          ? null
                          : () => setState(() => _preview = null),
                      child: const Text('VOLTAR À DISTRIBUIÇÃO'),
                    ),
                    FilledButton(
                      onPressed:
                          _busy ||
                              errors.isNotEmpty ||
                              _preview!['pendingSlots'] != 0
                          ? null
                          : () => _run(_confirm),
                      child: const Text('CRIAR NOVO MUNDO'),
                    ),
                  ],
                ),
              ],
            ],
          ),
        ),
      ),
    );
  }
}
