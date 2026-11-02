import 'dart:async';
import 'package:flutter/material.dart';
import 'api_client.dart';
import 'visual_components.dart';

class WorldInitialLobby016B extends StatefulWidget {
  final ElifootApiClient api;
  final String worldId, coachId;
  final Future<void> Function() onStarted, onLeave;
  const WorldInitialLobby016B({
    super.key,
    required this.api,
    required this.worldId,
    required this.coachId,
    required this.onStarted,
    required this.onLeave,
  });
  @override
  State<WorldInitialLobby016B> createState() => _WorldInitialLobby016BState();
}

class _WorldInitialLobby016BState extends State<WorldInitialLobby016B> {
  Map<String, dynamic>? _view;
  List<Map<String, dynamic>> _clubs = [];
  String _choice = 'UNEMPLOYED', _error = '';
  String? _club;
  bool _busy = false,
      _polling = false,
      _started = false,
      _dirty = false,
      _commandError = false;
  Timer? _timer;
  Future<dynamic> _call(String path, {Map<String, dynamic>? body}) =>
      widget.api.world016b(
        widget.coachId,
        'world-lobby-016b',
        '${widget.worldId}$path',
        body: body,
      );
  @override
  void initState() {
    super.initState();
    _load();
    _timer = Timer.periodic(const Duration(seconds: 2), (_) => _load());
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  List<Map<String, dynamic>> _maps(dynamic v) => (v as List? ?? [])
      .map((x) => Map<String, dynamic>.from(x as Map))
      .toList();
  Future<void> _load() async {
    if (_polling || _busy || _started) return;
    _polling = true;
    try {
      final view = Map<String, dynamic>.from(await _call('') as Map);
      final clubs = _maps(await widget.api.getClubs(widget.worldId));
      if (!mounted) return;
      final me = _maps(
        view['participants'],
      ).where((p) => p['coachId'] == widget.coachId).firstOrNull;
      setState(() {
        _view = view;
        _clubs = clubs;
        if (!_commandError) _error = '';
        if (!_dirty && me != null) {
          _choice = me['choice'] == 'PENDING'
              ? 'UNEMPLOYED'
              : me['choice'].toString();
          _club = me['clubId']?.toString();
        }
      });
      if (view['state'] == 'IN_GAME' && !_started) {
        _started = true;
        _timer?.cancel();
        await widget.onStarted();
      }
    } catch (e) {
      if (mounted) setState(() => _error = e.toString());
    } finally {
      _polling = false;
    }
  }

  Future<void> _act(String path, Map<String, dynamic> body) async {
    setState(() {
      _busy = true;
      _commandError = false;
      _error = '';
    });
    try {
      await _call(path, body: body);
      _dirty = false;
    } catch (e) {
      if (mounted) {
        setState(() {
          _error = e.toString();
          _commandError = true;
        });
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
    await _load();
  }

  static String choiceName(dynamic x) => switch (x) {
    'MANUAL' => 'Assumir clube',
    'FOURTH_DIVISION_DRAW' => 'Sorteio da 4ª divisão',
    'UNEMPLOYED' => 'Desempregado',
    _ => 'Opção pendente',
  };
  @override
  Widget build(BuildContext context) {
    final v = _view;
    final host = v?['hostCoachId'] == widget.coachId;
    final locked = v?['drawCompleted'] == true || _started;
    final me = v == null
        ? null
        : _maps(
            v['participants'],
          ).where((p) => p['coachId'] == widget.coachId).firstOrNull;
    final available = _clubs
        .where(
          (c) =>
              c['divisionTier'] != 'Distrital' &&
              (c['isHumanControlled'] != true || c['id'] == me?['clubId']),
        )
        .toList();
    return Scaffold(
      key: const Key('world-initial-lobby-016b'),
      appBar: AppBar(
        title: const Text('LIGA CLÁSSICA 98 • SALA'),
        leading: IconButton(
          tooltip: 'Voltar às salas',
          icon: const Icon(Icons.arrow_back),
          onPressed: _busy ? null : () => widget.onLeave(),
        ),
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(12),
        child: Center(
          child: ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 1100),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                const Text(
                  'CLÁSSICO SINCRONIZADO',
                  style: TextStyle(fontSize: 25, fontWeight: FontWeight.bold),
                ),
                if (v == null)
                  const Padding(
                    padding: EdgeInsets.all(24),
                    child: Center(child: CircularProgressIndicator()),
                  )
                else ...[
                  Text(
                    'Treinadores: ${v['registered']}/${v['capacity']} • Conectados: ${v['connected']}/${v['capacity']}',
                  ),
                  Text('Estado: ${v['state']}'),
                  const SizedBox(height: 12),
                  ClassicPaperPanel(
                    title: 'PARTICIPANTES',
                    icon: Icons.groups,
                    child: Column(
                      children: [
                        for (final p in _maps(v['participants']))
                          ListTile(
                            contentPadding: EdgeInsets.zero,
                            leading: Icon(
                              p['connected'] == true
                                  ? Icons.person
                                  : Icons.person_outline,
                            ),
                            title: Text(p['nickname'].toString()),
                            subtitle: Text(
                              '${p['connected'] == true ? 'Conectado' : 'Desconectado'} • ${choiceName(p['choice'])}',
                            ),
                            trailing: p['clubName'] == null
                                ? null
                                : SizedBox(
                                    width: 120,
                                    child: Text(
                                      p['clubName'].toString(),
                                      textAlign: TextAlign.end,
                                    ),
                                  ),
                          ),
                      ],
                    ),
                  ),
                  const SizedBox(height: 12),
                  ClassicPaperPanel(
                    title: 'COMO COMEÇAR',
                    icon: Icons.sports_soccer,
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        DropdownButtonFormField<String>(
                          key: const Key('initial-career-choice'),
                          initialValue: _choice,
                          isExpanded: true,
                          decoration: const InputDecoration(
                            labelText: 'Sua opção',
                          ),
                          items: [
                            for (final value in [
                              'MANUAL',
                              'UNEMPLOYED',
                              'FOURTH_DIVISION_DRAW',
                            ])
                              DropdownMenuItem(
                                value: value,
                                child: Text(choiceName(value)),
                              ),
                          ],
                          onChanged: _busy || locked
                              ? null
                              : (value) => setState(() {
                                  _choice = value!;
                                  _dirty = true;
                                }),
                        ),
                        if (_choice == 'MANUAL')
                          DropdownButtonFormField<String>(
                            key: ValueKey(
                              'initial-club-${available.map((c) => c['id']).join()}',
                            ),
                            initialValue: available.any((c) => c['id'] == _club)
                                ? _club
                                : null,
                            isExpanded: true,
                            itemHeight: null,
                            decoration: const InputDecoration(
                              labelText: 'Clube disponível das quatro divisões',
                            ),
                            items: [
                              for (final c in available)
                                DropdownMenuItem(
                                  value: c['id'].toString(),
                                  child: Text(
                                    '${c['name']} • ${c['divisionDisplay']}',
                                  ),
                                ),
                            ],
                            onChanged: _busy || locked
                                ? null
                                : (value) => setState(() {
                                    _club = value;
                                    _dirty = true;
                                  }),
                          ),
                        const SizedBox(height: 10),
                        ElevatedButton.icon(
                          key: const Key('save-initial-choice'),
                          onPressed:
                              _busy ||
                                  locked ||
                                  _choice == 'MANUAL' && _club == null
                              ? null
                              : () => _act('/choice', {
                                  'choice': _choice,
                                  'clubId': _choice == 'MANUAL' ? _club : null,
                                }),
                          icon: const Icon(Icons.check),
                          label: const Text('CONFIRMAR OPÇÃO'),
                        ),
                      ],
                    ),
                  ),
                  const SizedBox(height: 12),
                  for (final issue in v['issues'] as List? ?? [])
                    Text(issue.toString()),
                  if (host) ...[
                    if (!locked)
                      DropdownButtonFormField<int>(
                        key: ValueKey('initial-capacity-${v['capacity']}'),
                        initialValue: (v['capacity'] as num).toInt(),
                        decoration: const InputDecoration(
                          labelText: 'Vagas da sala',
                        ),
                        items: [
                          for (int n = 1; n <= 8; n++)
                            DropdownMenuItem(
                              value: n,
                              child: Text('$n treinador(es)'),
                            ),
                        ],
                        onChanged: _busy
                            ? null
                            : (n) => _act('/capacity', {
                                'maxHumans': n,
                                'idempotencyKey':
                                    'capacity-$n-${DateTime.now().microsecondsSinceEpoch}',
                              }),
                      ),
                    const SizedBox(height: 12),
                    Wrap(
                      spacing: 12,
                      runSpacing: 10,
                      children: [
                        ElevatedButton.icon(
                          key: const Key('initial-draw'),
                          onPressed: _busy || v['canDraw'] != true
                              ? null
                              : () => _act('/draw', {
                                  'idempotencyKey': 'draw-${widget.worldId}',
                                }),
                          icon: const Icon(Icons.shuffle),
                          label: const Text('SORTEAR TIMES DA 4ª DIVISÃO'),
                        ),
                        ElevatedButton.icon(
                          key: const Key('initial-start'),
                          onPressed: _busy || v['canStart'] != true
                              ? null
                              : () => _act('/start', {
                                  'idempotencyKey': 'start-${widget.worldId}',
                                }),
                          icon: const Icon(Icons.play_arrow),
                          label: const Text('INICIAR CAMPEONATO'),
                        ),
                      ],
                    ),
                  ] else
                    const Padding(
                      padding: EdgeInsets.symmetric(vertical: 12),
                      child: Text(
                        'Aguarde o criador concluir o sorteio e iniciar o campeonato.',
                      ),
                    ),
                  const SizedBox(height: 12),
                  const Text(
                    'Depois do início, cada treinador envia sua tática. A rodada começa automaticamente quando todos estiverem prontos.',
                  ),
                ],
                if (_error.isNotEmpty)
                  Padding(
                    padding: const EdgeInsets.all(10),
                    child: Text(
                      _error,
                      style: TextStyle(
                        color: Theme.of(context).colorScheme.error,
                      ),
                    ),
                  ),
              ],
            ),
          ),
        ),
      ),
    );
  }
}
