import 'club_theme.dart';
import 'dart:convert';
import 'dart:math';
import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'api_client.dart';
import 'appearance_settings.dart';
import 'classic_match_board.dart';

/// Edits a private package only. A confirmed world's identifiers are never used.
class CustomAssemblyEditor extends StatefulWidget {
  final ElifootApiClient api;
  final String coachId;
  final Map<String, dynamic> assembly;
  final List<Map<String, dynamic>> countryOptions;
  const CustomAssemblyEditor({
    super.key,
    required this.api,
    required this.coachId,
    required this.assembly,
    required this.countryOptions,
  });
  @override
  State<CustomAssemblyEditor> createState() => _CustomAssemblyEditorState();
}

class _CustomAssemblyEditorState extends State<CustomAssemblyEditor> {
  late Map<String, dynamic> _assembly, _package;
  late final TextEditingController _name;
  final _content = TextEditingController();
  String _search = '', _message = '';
  bool _busy = false, _dirty = false, _personalized = false;
  List<Map<String, dynamic>> _maps(dynamic value) => (value as List? ?? [])
      .map((e) => Map<String, dynamic>.from(e as Map))
      .toList();
  @override
  void initState() {
    super.initState();
    _assembly = Map<String, dynamic>.from(widget.assembly);
    _package =
        jsonDecode(jsonEncode(_assembly['package'])) as Map<String, dynamic>;
    _name = TextEditingController(text: _assembly['name'].toString());
    _personalized = _assembly['allowPersonalizedContent'] == true;
  }

  @override
  void dispose() {
    _name.dispose();
    _content.dispose();
    super.dispose();
  }

  Future<dynamic> _call(String path, {Map<String, dynamic>? body}) =>
      widget.api.worldCreator(widget.coachId, path, body: body);
  Future<void> _run(Future<void> Function() action) async {
    setState(() {
      _busy = true;
      _message = '';
    });
    try {
      await action();
    } catch (e) {
      if (mounted) setState(() => _message = '$e');
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _save({bool preset = false, bool finish = false}) =>
      _run(() async {
        final result = Map<String, dynamic>.from(
          await _call(
                'assemblies/${_assembly['id']}',
                body: {
                  'name': _name.text,
                  'countries': _assembly['countries'],
                  'package': _package,
                  'expectedVersion': _assembly['version'],
                  'savePreset': preset,
                  'allowPersonalizedContent': _personalized,
                },
              )
              as Map,
        );
        if (!mounted) return;
        setState(() {
          _assembly = result;
          _package = Map<String, dynamic>.from(result['package'] as Map);
          _dirty = false;
          _message = preset
              ? 'MONTAGEM SALVA NA SUA CONTA.'
              : 'Rascunho atualizado.';
        });
        if (finish) Navigator.pop(context, result);
      });
  Widget _select(
    String label,
    String value,
    List<(String, String)> options,
    ValueChanged<String> change,
  ) => DropdownButtonFormField<String>(
    key: ValueKey('$label-$value'),
    initialValue: value,
    isExpanded: true,
    decoration: InputDecoration(labelText: label),
    items: options
        .map(
          (o) => DropdownMenuItem(
            value: o.$1,
            child: Text(o.$2, overflow: TextOverflow.ellipsis),
          ),
        )
        .toList(),
    onChanged: _busy
        ? null
        : (v) {
            if (v != null) change(v);
          },
  );
  Future<void> _edit(String collection, Map<String, dynamic> entity) async {
    final player = collection == 'players',
        manager = collection == 'aiManagers';
    final values = Map<String, dynamic>.from(entity);
    final fields = player
        ? [
            'name',
            'strength',
            'salaryPerRound',
            'contractRoundsRemaining',
            'photoUrl',
            'photoLicense',
            'photoCredit',
          ]
        : manager
        ? [
            'name',
            'pragmatism',
            'adaptability',
            'financialAggressiveness',
            'youthPatience',
            'preferredFormations',
          ]
        : [
            'name',
            'shortName',
            'primaryColorHex',
            'secondaryColorHex',
            'stadiumName',
            'builtCapacity',
            'usableCapacity',
            'conditionPercent',
            'reputation',
          ];
    final controls = {
      for (final f in fields)
        f: TextEditingController(
          text: f == 'preferredFormations'
              ? jsonEncode(values[f])
              : '${values[f] ?? ''}',
        ),
    };
    const labels = {
      'name': 'Nome',
      'strength': 'Força (1 a 50)',
      'salaryPerRound': 'Salário contratado por rodada (R\$)',
      'contractRoundsRemaining': 'Contrato inicial (rodadas)',
      'shortName': 'Sigla',
      'primaryColorHex': 'Cor principal (#RRGGBB)',
      'secondaryColorHex': 'Cor secundária (#RRGGBB)',
      'stadiumName': 'Estádio',
      'builtCapacity': 'Capacidade construída',
      'usableCapacity': 'Capacidade utilizável',
      'conditionPercent': 'Condição do estádio (%)',
      'reputation': 'Reputação',
      'photoUrl': 'Foto (URL)',
      'photoLicense': 'Licença da foto',
      'photoCredit': 'Crédito da foto',
      'pragmatism': 'Pragmatismo',
      'adaptability': 'Adaptabilidade',
      'financialAggressiveness': 'Agressividade financeira',
      'youthPatience': 'Paciência com jovens',
      'preferredFormations': 'Táticas preferidas (JSON; pesos somam 100)',
    };
    String? error;
    final accepted = await showDialog<bool>(
      context: context,
      builder: (ctx) => StatefulBuilder(
        builder: (ctx, update) => AlertDialog(
          title: Text(
            player
                ? 'EDITAR JOGADOR'
                : manager
                ? 'EDITAR TREINADOR IA'
                : 'EDITAR CLUBE',
          ),
          content: SizedBox(
            width: 520,
            child: SingleChildScrollView(
              child: Column(
                mainAxisSize: MainAxisSize.min,
                children: [
                  if (!_personalized)
                    const Text(
                      'Autorize conteúdo personalizado para mudar nomes, países ou posições.',
                    ),
                  for (final f in fields)
                    if (f == 'primaryColorHex' || f == 'secondaryColorHex')
                      ColorChoice(
                        label: labels[f] ?? f,
                        value: rgbColor(controls[f]!.text) ?? Colors.black,
                        onChanged: (c) =>
                            update(() => controls[f]!.text = rgbHex(c)),
                      )
                    else
                      TextField(
                        key: ValueKey('assembly-field-$f'),
                        controller: controls[f],
                        decoration: InputDecoration(labelText: labels[f] ?? f),
                      ),
                  if (player) ...[
                    _select(
                      'Posição',
                      '${values['position']}',
                      [
                        for (final p in ['G', 'D', 'M', 'A']) (p, p),
                      ],
                      (v) => update(() => values['position'] = v),
                    ),
                    _select(
                      'Comportamento',
                      '${values['behavior']}',
                      [
                        for (final b in [
                          'FairPlay',
                          'Cordeirinho',
                          'Cavalheiro',
                          'Caneleiro',
                          'Caceteiro',
                          'Sarrafeiro',
                        ])
                          (b, b),
                      ],
                      (v) => update(() => values['behavior'] = v),
                    ),
                    _select(
                      'Clube',
                      '${values['clubId']}',
                      _maps(
                        _package['clubs'],
                      ).map((c) => ('${c['id']}', '${c['name']}')).toList(),
                      (v) => update(() => values['clubId'] = v),
                    ),
                    CheckboxListTile(
                      title: const Text('Estrela *'),
                      value: values['isStar'] == true,
                      onChanged: (v) =>
                          update(() => values['isStar'] = v == true),
                    ),
                  ] else if (!manager) ...[
                    _select(
                      'País',
                      '${values['country']}',
                      {
                        '${values['country']}': '${values['country']}',
                        for (final c in widget.countryOptions.where(
                          (c) => (_assembly['countries'] as List).contains(
                            c['code'],
                          ),
                        ))
                          '${c['name']}': '${c['name']}',
                      }.entries.map((e) => (e.key, e.value)).toList(),
                      (v) => update(() => values['country'] = v),
                    ),
                    _select(
                      'Divisão',
                      '${values['divisionTier']}',
                      [
                        for (final d in [
                          'PrimeiraDivisao',
                          'SegundaDivisao',
                          'TerceiraDivisao',
                          'QuartaDivisao',
                          'Distrital',
                        ])
                          (d, classicDivisionLabel(d)),
                      ],
                      (v) => update(() => values['divisionTier'] = v),
                    ),
                  ],
                  if (error != null)
                    Text(
                      error!,
                      style: const TextStyle(color: Colors.orangeAccent),
                    ),
                ],
              ),
            ),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(ctx, false),
              child: const Text('CANCELAR'),
            ),
            FilledButton(
              onPressed: () {
                try {
                  for (final f in fields) {
                    final text = controls[f]!.text;
                    values[f] = f == 'preferredFormations'
                        ? jsonDecode(text)
                        : [
                            'strength',
                            'contractRoundsRemaining',
                            'builtCapacity',
                            'usableCapacity',
                            'reputation',
                            'pragmatism',
                            'adaptability',
                            'financialAggressiveness',
                            'youthPatience',
                          ].contains(f)
                        ? int.parse(text)
                        : ['salaryPerRound', 'conditionPercent'].contains(f)
                        ? double.parse(text.replaceAll(',', '.'))
                        : text;
                  }
                  if (player &&
                      ((values['strength'] as int) < 1 ||
                          (values['strength'] as int) > 50)) {
                    throw const FormatException(
                      'A força deve estar entre 1 e 50.',
                    );
                  }
                  Navigator.pop(ctx, true);
                } catch (e) {
                  update(() => error = 'Confira os campos: $e');
                }
              },
              child: const Text('APLICAR AO RASCUNHO'),
            ),
          ],
        ),
      ),
    );
    if (accepted == true && mounted) {
      setState(() {
        final entries = _maps(_package[collection]);
        final index = entries.indexWhere((e) => e['id'] == values['id']);
        if (index < 0) {
          entries.add(values);
        } else {
          entries[index] = values;
        }
        _package[collection] = entries;
        _dirty = true;
      });
    }
    await Future<void>.delayed(const Duration(milliseconds: 250));
    for (final c in controls.values) {
      c.dispose();
    }
  }

  String _newId() {
    final r = Random.secure();
    final b = List.generate(16, (_) => r.nextInt(256));
    b[6] = (b[6] & 15) | 64;
    b[8] = (b[8] & 63) | 128;
    final h = b.map((x) => x.toRadixString(16).padLeft(2, '0')).join();
    return '${h.substring(0, 8)}-${h.substring(8, 12)}-${h.substring(12, 16)}-${h.substring(16, 20)}-${h.substring(20)}';
  }

  Future<void> _addPlayer(String clubId) => _edit('players', {
    'id': _newId(),
    'clubId': clubId,
    'name': 'Novo jogador',
    'position': 'M',
    'strength': 20,
    'behavior': 'FairPlay',
    'isStar': false,
    'salaryPerRound': 1000,
    'contractRoundsRemaining': 30,
    'marketPrice': 0,
    'negotiatedContractProtected': true,
  });
  Future<void> _exportCsv() => _run(() async {
    final data = await _call(
      'assemblies/players/export-csv',
      body: {'package': _package},
    );
    if (mounted) {
      setState(() => _content.text = (data as Map)['csv'].toString());
    }
  });
  Future<void> _import(bool csv) => _run(() async {
    if (!_personalized) {
      throw const FormatException(
        'Autorize o conteúdo personalizado antes de importar.',
      );
    }
    if (csv) {
      final result =
          await _call(
                'assemblies/players/parse-csv',
                body: {'csv': _content.text},
              )
              as Map;
      final errors = result['errors'] as List;
      if (errors.isNotEmpty) throw FormatException(errors.join('\n'));
      if (mounted) {
        setState(() {
          _package['players'] = result['players'];
          _dirty = true;
        });
      }
    } else {
      final p = jsonDecode(_content.text) as Map<String, dynamic>;
      if (p['clubs'] is! List ||
          p['players'] is! List ||
          p['aiManagers'] is! List) {
        throw const FormatException('Pacote incompleto.');
      }
      if (mounted) {
        setState(() {
          _package = p;
          _dirty = true;
        });
      }
    }
  });
  @override
  Widget build(BuildContext context) {
    final players = _maps(_package['players']);
    final clubs = _maps(_package['clubs'])
        .where((c) => c['name'].toString().toLowerCase().contains(_search))
        .toList();
    final validation = _assembly['validation'] as Map? ?? {};
    return PopScope(
      canPop: !_dirty,
      onPopInvokedWithResult: (didPop, result) async {
        if (didPop) return;
        final leave = await showDialog<bool>(
          context: context,
          builder: (ctx) => AlertDialog(
            title: const Text('Montagem com alterações'),
            content: const Text(
              'Atualize o rascunho para manter as alterações no banco antes de sair.',
            ),
            actions: [
              TextButton(
                onPressed: () => Navigator.pop(ctx, false),
                child: const Text('CONTINUAR EDITANDO'),
              ),
              TextButton(
                onPressed: () => Navigator.pop(ctx, true),
                child: const Text('DESCARTAR ALTERAÇÕES LOCAIS'),
              ),
            ],
          ),
        );
        if (leave == true && mounted) {
          setState(() => _dirty = false);
          WidgetsBinding.instance.addPostFrameCallback((_) {
            if (mounted) Navigator.pop(context, _assembly);
          });
        }
      },
      child: Scaffold(
        backgroundColor: ClassicTheme.of(context).surface,
        appBar: AppBar(title: const Text('MONTAGEM ANTES DO SAVE')),
        body: SafeArea(
          child: Center(
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 980),
              child: Column(
                children: [
                  if (_busy) const LinearProgressIndicator(),
                  Expanded(
                    child: ListView(
                      padding: const EdgeInsets.all(12),
                      children: [
                        TextField(
                          controller: _name,
                          decoration: const InputDecoration(
                            labelText: 'Nome da montagem',
                          ),
                          onChanged: (_) => setState(() => _dirty = true),
                        ),
                        _select(
                          'Categoria',
                          '${_package['gender']}',
                          const [('Male', 'MASCULINO'), ('Female', 'FEMININO')],
                          (v) => setState(() {
                            _package['gender'] = v;
                            _dirty = true;
                          }),
                        ),
                        TextFormField(
                          initialValue: '${_package['seed']}',
                          decoration: const InputDecoration(labelText: 'Seed'),
                          keyboardType: TextInputType.number,
                          onChanged: (v) {
                            final n = int.tryParse(v);
                            if (n != null) {
                              setState(() {
                                _package['seed'] = n;
                                _dirty = true;
                              });
                            }
                          },
                        ),
                        CheckboxListTile(
                          contentPadding: EdgeInsets.zero,
                          title: const Text(
                            'AUTORIZO CLUBES E JOGADORES PERSONALIZADOS',
                          ),
                          value: _personalized,
                          onChanged: _busy
                              ? null
                              : (v) => setState(() {
                                  _personalized = v == true;
                                  _dirty = true;
                                }),
                        ),
                        Text(
                          '128 clubes • 4 divisões de 8 • 96 distritais • ${players.length} atletas • versão ${_assembly['version']}',
                        ),
                        if (_dirty)
                          const Text(
                            'Alterações locais: atualize para validar e guardar o rascunho.',
                            style: TextStyle(color: Colors.amber),
                          ),
                        if (_message.isNotEmpty)
                          SelectableText(
                            _message,
                            style: const TextStyle(color: Colors.amber),
                          ),
                        for (final e in validation['errors'] as List? ?? [])
                          Text(
                            '$e',
                            style: const TextStyle(color: Colors.orangeAccent),
                          ),
                        TextField(
                          decoration: const InputDecoration(
                            labelText: 'Buscar clube',
                          ),
                          onChanged: (v) =>
                              setState(() => _search = v.toLowerCase()),
                        ),
                        for (final club in clubs)
                          ExpansionTile(
                            key: PageStorageKey('assembly-club-${club['id']}'),
                            title: Text('${club['name']}'),
                            subtitle: Text(
                              '${classicDivisionLabel(club['divisionTier'])} • ${club['country']} • ${players.where((p) => p['clubId'] == club['id']).length} atletas',
                            ),
                            children: [
                              TextButton(
                                onPressed: _busy
                                    ? null
                                    : () => _edit('clubs', club),
                                child: const Text('EDITAR CLUBE / DIVISÃO'),
                              ),
                              for (final p in players.where(
                                (p) => p['clubId'] == club['id'],
                              ))
                                ListTile(
                                  dense: true,
                                  title: Text(
                                    '${p['name']}${p['isStar'] == true ? ' *' : ''} • ${p['position']} • F:${p['strength']}',
                                  ),
                                  subtitle: Text(
                                    'Salário R\$ ${p['salaryPerRound']} • contrato ${p['contractRoundsRemaining']}r',
                                  ),
                                  onTap: _busy
                                      ? null
                                      : () => _edit('players', p),
                                  trailing: IconButton(
                                    tooltip: 'Remover do rascunho',
                                    icon: const Icon(
                                      Icons.person_remove,
                                      size: 18,
                                    ),
                                    onPressed: _busy
                                        ? null
                                        : () => setState(() {
                                            _package['players'] = players
                                                .where(
                                                  (a) => a['id'] != p['id'],
                                                )
                                                .toList();
                                            _dirty = true;
                                          }),
                                  ),
                                ),
                              TextButton(
                                onPressed: _busy
                                    ? null
                                    : () => _addPlayer(club['id'].toString()),
                                child: const Text('ADICIONAR JOGADOR'),
                              ),
                              for (final m in _maps(
                                _package['aiManagers'],
                              ).where((m) => m['assignedClubId'] == club['id']))
                                TextButton(
                                  onPressed: _busy
                                      ? null
                                      : () => _edit('aiManagers', m),
                                  child: Text(
                                    'EDITAR TREINADOR IA: ${m['name']}',
                                  ),
                                ),
                            ],
                          ),
                        ExpansionTile(
                          title: const Text(
                            'IMPORTAR / EXPORTAR PACOTE JSON E JOGADORES CSV',
                          ),
                          children: [
                            Wrap(
                              spacing: 8,
                              children: [
                                TextButton(
                                  onPressed: () => setState(
                                    () => _content.text =
                                        const JsonEncoder.withIndent(
                                          '  ',
                                        ).convert(_package),
                                  ),
                                  child: const Text('EXPORTAR JSON'),
                                ),
                                TextButton(
                                  onPressed: _busy ? null : _exportCsv,
                                  child: const Text('EXPORTAR CSV'),
                                ),
                                TextButton(
                                  onPressed: _busy
                                      ? null
                                      : () => _import(false),
                                  child: const Text('IMPORTAR JSON'),
                                ),
                                TextButton(
                                  onPressed: _busy ? null : () => _import(true),
                                  child: const Text('IMPORTAR CSV'),
                                ),
                                TextButton(
                                  onPressed: () => Clipboard.setData(
                                    ClipboardData(text: _content.text),
                                  ),
                                  child: const Text('COPIAR'),
                                ),
                              ],
                            ),
                            TextField(
                              controller: _content,
                              maxLines: 8,
                              decoration: const InputDecoration(
                                labelText: 'Conteúdo JSON / CSV',
                              ),
                            ),
                          ],
                        ),
                      ],
                    ),
                  ),
                  Padding(
                    padding: const EdgeInsets.all(8),
                    child: Wrap(
                      spacing: 8,
                      runSpacing: 6,
                      children: [
                        OutlinedButton(
                          onPressed: _busy ? null : () => _save(),
                          child: const Text('VALIDAR E GUARDAR RASCUNHO'),
                        ),
                        ElevatedButton(
                          onPressed: _busy ? null : () => _save(preset: true),
                          child: const Text('SALVAR MINHA MONTAGEM'),
                        ),
                        FilledButton(
                          onPressed: _busy ? null : () => _save(finish: true),
                          child: const Text('USAR ESTA MONTAGEM'),
                        ),
                      ],
                    ),
                  ),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}
