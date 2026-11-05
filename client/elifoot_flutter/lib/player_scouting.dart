import 'dart:async';
import 'package:flutter/material.dart';
import 'api_client.dart';

const scoutingStrengthRanges = [
  'ALL',
  '1-10',
  '5-15',
  '10-20',
  '20-30',
  '30-40',
  '40-50',
];
bool matchesScoutingStrength(dynamic strength, String range) {
  if (range == 'ALL') return true;
  final bounds = range.split('-').map(int.parse).toList();
  return strength is num && strength >= bounds[0] && strength <= bounds[1];
}

class PlayerObservationController extends ChangeNotifier {
  String? _identity;
  final ids = <String>{}, busyIds = <String>{};
  String? error;
  bool loading = false;
  Future<void> load(ElifootApiClient api, String world, String coach) async {
    final identity = '$world/$coach';
    if (_identity != identity) {
      ids.clear();
      busyIds.clear();
      _identity = identity;
    }
    loading = true;
    error = null;
    try {
      final data = await api.getPlayerScouting(world, coach);
      if (_identity != identity) return;
      ids
        ..clear()
        ..addAll((data['observedPlayerIds'] as List? ?? []).map((id) => '$id'));
    } catch (_) {
      if (_identity == identity) {
        error = 'Não foi possível carregar os observados. Tente atualizar.';
      }
    } finally {
      if (_identity == identity) {
        loading = false;
        notifyListeners();
      }
    }
  }

  Future<void> toggle(
    ElifootApiClient api,
    String world,
    String coach,
    String player,
  ) async {
    if (busyIds.contains(player)) return;
    final identity = '$world/$coach';
    _identity = identity;
    busyIds.add(player);
    error = null;
    notifyListeners();
    try {
      final saved = await api.setPlayerObservation(
        world,
        coach,
        player,
        !ids.contains(player),
      );
      if (_identity == identity) {
        ids
          ..clear()
          ..addAll(saved);
      }
    } catch (_) {
      if (_identity == identity) {
        error = 'Não foi possível salvar a observação. Tente novamente.';
      }
    } finally {
      if (_identity == identity) {
        busyIds.remove(player);
        notifyListeners();
      }
    }
  }
}

class ObservePlayerButton extends StatelessWidget {
  final PlayerObservationController controller;
  final ElifootApiClient api;
  final String world, coach, player, name;
  const ObservePlayerButton({
    super.key,
    required this.controller,
    required this.api,
    required this.world,
    required this.coach,
    required this.player,
    required this.name,
  });
  @override
  Widget build(BuildContext context) => ListenableBuilder(
    listenable: controller,
    builder: (context, _) => IconButton(
      key: ValueKey('observe-$player'),
      tooltip:
          '${controller.ids.contains(player) ? 'Deixar de observar' : 'Observar'} $name',
      onPressed: controller.loading || controller.busyIds.contains(player)
          ? null
          : () => controller.toggle(api, world, coach, player),
      icon: Icon(
        controller.ids.contains(player) ? Icons.star : Icons.star_border,
      ),
    ),
  );
}

class ScoutingStrengthFilter extends StatelessWidget {
  final String value;
  final ValueChanged<String> onChanged;
  const ScoutingStrengthFilter({
    super.key,
    required this.value,
    required this.onChanged,
  });
  @override
  Widget build(BuildContext context) => DropdownButtonFormField<String>(
    initialValue: value,
    isExpanded: true,
    decoration: const InputDecoration(labelText: 'Força'),
    items: [
      for (final range in scoutingStrengthRanges)
        DropdownMenuItem(
          value: range,
          child: Text(range == 'ALL' ? 'Todas' : range.replaceAll('-', ' a ')),
        ),
    ],
    onChanged: (v) {
      if (v != null) onChanged(v);
    },
  );
}

class PlayerScoutingScreen extends StatefulWidget {
  final ElifootApiClient api;
  final String world, coach;
  final PlayerObservationController observations;
  final bool observedOnly;
  final VoidCallback onClose;
  const PlayerScoutingScreen({
    super.key,
    required this.api,
    required this.world,
    required this.coach,
    required this.observations,
    required this.observedOnly,
    required this.onClose,
  });
  @override
  State<PlayerScoutingScreen> createState() => _PlayerScoutingScreenState();
}

class _PlayerScoutingScreenState extends State<PlayerScoutingScreen> {
  final _name = TextEditingController();
  Timer? _debounce;
  int _request = 0;
  bool _loading = false;
  String? _error;
  String _position = 'ALL', _strength = 'ALL';
  List<Map<String, dynamic>> _players = [];
  @override
  void initState() {
    super.initState();
    widget.observations.addListener(_changed);
    widget.observations.load(widget.api, widget.world, widget.coach);
    if (widget.observedOnly) _search();
  }

  void _changed() {
    if (mounted) setState(() {});
  }

  @override
  void dispose() {
    _debounce?.cancel();
    _name.dispose();
    widget.observations.removeListener(_changed);
    super.dispose();
  }

  void _onName(String value) {
    _debounce?.cancel();
    _request++;
    setState(() {
      _players = [];
      _error = null;
      _loading = false;
    });
    if (value.trim().length >= 3 ||
        widget.observedOnly && value.trim().isEmpty) {
      _debounce = Timer(const Duration(milliseconds: 300), _search);
    }
  }

  Future<void> _search() async {
    final request = ++_request;
    setState(() {
      _loading = true;
      _error = null;
    });
    try {
      final data = await widget.api.getPlayerScouting(
        widget.world,
        widget.coach,
        name: _name.text.trim(),
        observedOnly: widget.observedOnly,
      );
      if (mounted && request == _request) {
        setState(
          () => _players = (data['players'] as List)
              .whereType<Map>()
              .map((p) => Map<String, dynamic>.from(p))
              .toList(),
        );
      }
    } catch (_) {
      if (mounted && request == _request) {
        setState(
          () =>
              _error = 'Não foi possível procurar jogadores. Tente novamente.',
        );
      }
    } finally {
      if (mounted && request == _request) setState(() => _loading = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    String pos(dynamic v) =>
        v is num ? const ['G', 'D', 'M', 'A'][v.toInt().clamp(0, 3)] : '$v';
    final rows = _players
        .where(
          (p) =>
              (_position == 'ALL' || pos(p['position']) == _position) &&
              matchesScoutingStrength(p['strength'], _strength) &&
              (!widget.observedOnly ||
                  widget.observations.ids.contains('${p['playerId']}')),
        )
        .toList();
    return Column(
      children: [
        Row(
          children: [
            Expanded(
              child: Padding(
                padding: const EdgeInsets.all(12),
                child: Text(
                  widget.observedOnly
                      ? 'JOGADORES OBSERVADOS'
                      : 'PROCURAR JOGADORES',
                  style: const TextStyle(
                    fontSize: 22,
                    fontWeight: FontWeight.bold,
                  ),
                ),
              ),
            ),
            IconButton(
              tooltip: 'Voltar',
              onPressed: widget.onClose,
              icon: const Icon(Icons.close),
            ),
          ],
        ),
        Expanded(
          child: ListView(
            padding: const EdgeInsets.all(12),
            children: [
              TextField(
                key: const Key('scouting-name'),
                controller: _name,
                decoration: const InputDecoration(
                  labelText: 'Nome do jogador',
                  hintText: 'Ex.: Daniel Dias ou Dan',
                ),
                onChanged: _onName,
              ),
              const Text(
                'Digite pelo menos 3 letras. A busca inclui jogadores que não estão à venda.',
              ),
              const SizedBox(height: 8),
              Row(
                children: [
                  Expanded(
                    child: DropdownButtonFormField<String>(
                      initialValue: _position,
                      isExpanded: true,
                      decoration: const InputDecoration(labelText: 'Posição'),
                      items: [
                        for (final p in ['ALL', 'G', 'D', 'M', 'A'])
                          DropdownMenuItem(
                            value: p,
                            child: Text(p == 'ALL' ? 'Todos' : p),
                          ),
                      ],
                      onChanged: (v) => setState(() => _position = v ?? 'ALL'),
                    ),
                  ),
                  const SizedBox(width: 10),
                  Expanded(
                    child: ScoutingStrengthFilter(
                      value: _strength,
                      onChanged: (v) => setState(() => _strength = v),
                    ),
                  ),
                ],
              ),
              if (_loading) const LinearProgressIndicator(),
              if (_error != null) Text(_error!),
              if (widget.observations.error != null)
                Text(widget.observations.error!),
              if (_error != null || widget.observations.error != null)
                TextButton(
                  onPressed: () {
                    widget.observations.load(
                      widget.api,
                      widget.world,
                      widget.coach,
                    );
                    _search();
                  },
                  child: const Text('TENTAR NOVAMENTE'),
                ),
              if (!_loading &&
                  _error == null &&
                  rows.isEmpty &&
                  (widget.observedOnly || _name.text.trim().length >= 3))
                const Padding(
                  padding: EdgeInsets.all(12),
                  child: Text('Nenhum jogador corresponde aos filtros.'),
                ),
              for (final p in rows)
                ListTile(
                  key: ValueKey('scouting-player-${p['playerId']}'),
                  title: Text('${p['displayName']}'),
                  subtitle: Text(
                    '${pos(p['position'])} • Força ${p['strength']} • ${p['clubName']}\n${p['isInAuction'] == true
                        ? 'Em leilão'
                        : p['isListedForTransfer'] == true
                        ? 'À venda'
                        : 'Fora do mercado'}',
                  ),
                  trailing: ObservePlayerButton(
                    controller: widget.observations,
                    api: widget.api,
                    world: widget.world,
                    coach: widget.coach,
                    player: '${p['playerId']}',
                    name: '${p['displayName']}',
                  ),
                ),
            ],
          ),
        ),
      ],
    );
  }
}
