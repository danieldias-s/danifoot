import 'package:flutter/material.dart';
import 'api_client.dart';
import 'visual_components.dart';

class WorldCatalogStatus016B extends StatefulWidget {
  final ElifootApiClient api;
  final String coachId, gender, season;
  const WorldCatalogStatus016B({
    super.key,
    required this.api,
    required this.coachId,
    this.gender = 'Male',
    this.season = '2026',
  });
  @override
  State<WorldCatalogStatus016B> createState() => _WorldCatalogStatus016BState();
}

class _WorldCatalogStatus016BState extends State<WorldCatalogStatus016B> {
  final _token = TextEditingController(), _evidence = TextEditingController();
  late String _gender, _season;
  String _error = '';
  bool _busy = false;
  List<Map<String, dynamic>> _countries = [], _states = [];
  @override
  void initState() {
    super.initState();
    _gender = widget.gender;
    _season = widget.season;
    _load();
  }

  @override
  void dispose() {
    _token.dispose();
    _evidence.dispose();
    super.dispose();
  }

  List<Map<String, dynamic>> _maps(dynamic x) => (x as List? ?? [])
      .map((v) => Map<String, dynamic>.from(v as Map))
      .toList();
  Future<dynamic> _call(
    String path, {
    Map<String, dynamic>? body,
    bool admin = false,
  }) => widget.api.world016b(
    widget.coachId,
    'world-catalog-016b',
    path,
    body: body,
    adminToken: admin ? _token.text.trim() : null,
  );
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
    try {
      final data = Map<String, dynamic>.from(
        await _call(
              'countries?gender=$_gender&season=${Uri.encodeQueryComponent(_season)}',
            )
            as Map,
      );
      if (mounted) {
        setState(() {
          _countries = _maps(data['countries']);
          _states = _maps(data['states']);
        });
      }
    } catch (e) {
      if (mounted) setState(() => _error = e.toString());
    }
  }

  String _date(dynamic value) =>
      value == null || value.toString().startsWith('0001')
      ? 'Sem registro'
      : value.toString().split('T').first;
  Future<void> _report(Map<String, dynamic> country) async {
    final data = Map<String, dynamic>.from(
      await _call(
            'report?country=${country['code']}&gender=$_gender&season=${Uri.encodeQueryComponent(_season)}',
          )
          as Map,
    );
    if (!mounted) return;
    await showDialog<void>(
      context: context,
      builder: (dialog) => AlertDialog(
        title: Text('Relatório • ${country['name']}'),
        content: SizedBox(
          width: 660,
          child: SingleChildScrollView(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(
                  'Fonte: ${(data['provider'] as Map?)?['status'] ?? 'Indisponível'}',
                ),
                for (final c in _maps(data['coverage'])) ...[
                  Text(
                    'Clubes: ${c['clubs']} • Jogadores atuais utilizáveis: ${c['players']}',
                  ),
                  Text(
                    'Elencos completos: ${c['completeSquads']} • Parciais: ${c['partialSquads']}',
                  ),
                  Text(
                    'Documentados: ${c['documentedPlayers']} • Não confirmados: ${c['uncertainPlayers']}',
                  ),
                  for (final note in c['warnings'] as List? ?? [])
                    Text(note.toString()),
                ],
                const Divider(),
                Text(
                  'Históricos: ${data['historicalPlayers'] ?? 'Não avaliado'}',
                ),
                Text(
                  'Clubes com cores verificadas: ${data['colorsCoverage'] ?? 'Não avaliado'} • Com ranking documentado: ${data['rankingCoverage'] ?? 'Não avaliado'}',
                ),
                if (data['positionCoverage'] is Map)
                  Text(
                    'Posições no catálogo: ${(data['positionCoverage'] as Map).entries.map((e) => '${e.key}: ${e.value}').join(' • ')}',
                  ),
                for (final club in _maps(data['clubProfiles']).take(200))
                  ListTile(
                    contentPadding: EdgeInsets.zero,
                    title: Text(club['name'].toString()),
                    subtitle: Text(
                      'Força do clube: ${(club['initialProfile'] as Map?)?['clubInitialPower'] ?? 'Não classificado'}/20 • Jogadores: ${club['players']} • Históricos: ${club['historical']}',
                    ),
                  ),
                const Text('Ligas descobertas • revisão de categoria'),
                TextField(
                  controller: _evidence,
                  decoration: const InputDecoration(
                    labelText: 'Fonte/evidência da categoria da liga',
                  ),
                ),
                for (final league in _maps(data['leagues']))
                  ListTile(
                    contentPadding: EdgeInsets.zero,
                    title: Text(league['name'].toString()),
                    subtitle: Text(
                      '${league['gender'] ?? 'Categoria não revisada'} • ${league['currentSeason'] == true ? 'Temporada atual' : 'Temporada histórica'}',
                    ),
                    trailing: league['gender'] == null
                        ? TextButton(
                            onPressed: _token.text.trim().isEmpty
                                ? null
                                : () async {
                                    try {
                                      await _call(
                                        'admin/review-league',
                                        admin: true,
                                        body: {
                                          'countryCode': country['code'],
                                          'externalId': league['externalId'],
                                          'season': _season,
                                          'gender': _gender,
                                          'evidence': _evidence.text.trim(),
                                        },
                                      );
                                      if (dialog.mounted) Navigator.pop(dialog);
                                      await _load();
                                    } catch (e) {
                                      if (mounted) {
                                        setState(() => _error = e.toString());
                                      }
                                    }
                                  },
                            child: const Text('REVISAR'),
                          )
                        : const Icon(Icons.check),
                  ),
              ],
            ),
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(dialog),
            child: const Text('FECHAR'),
          ),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('BASE MUNDIAL • CLUBES E JOGADORES')),
    body: SingleChildScrollView(
      padding: const EdgeInsets.all(12),
      child: Center(
        child: ConstrainedBox(
          constraints: const BoxConstraints(maxWidth: 1100),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const Text(
                'CATÁLOGO MUNDIAL',
                style: TextStyle(fontSize: 26, fontWeight: FontWeight.bold),
              ),
              const Text(
                'Atualizações afetam novos campeonatos. Seus saves mantêm os dados já congelados.',
              ),
              const SizedBox(height: 12),
              DropdownButtonFormField<String>(
                initialValue: _gender,
                decoration: const InputDecoration(labelText: 'Categoria'),
                items: const [
                  DropdownMenuItem(value: 'Male', child: Text('MASCULINO')),
                  DropdownMenuItem(value: 'Female', child: Text('FEMININO')),
                ],
                onChanged: _busy
                    ? null
                    : (v) {
                        setState(() => _gender = v!);
                        _load();
                      },
              ),
              TextFormField(
                initialValue: _season,
                decoration: const InputDecoration(labelText: 'Temporada'),
                onChanged: (v) => _season = v,
              ),
              const SizedBox(height: 12),
              OutlinedButton.icon(
                onPressed: _busy ? null : () => _run(_load),
                icon: const Icon(Icons.refresh),
                label: const Text('CONSULTAR CACHE E ESTADOS'),
              ),
              ExpansionTile(
                title: const Text('Administração local'),
                children: [
                  TextField(
                    controller: _token,
                    obscureText: true,
                    onChanged: (_) => setState(() {}),
                    decoration: const InputDecoration(
                      labelText: 'Token administrativo local',
                    ),
                  ),
                  const Text(
                    'A chave da API-Football é configurada somente no Windows, no servidor.',
                  ),
                ],
              ),
              for (final country in _countries) ...[
                const SizedBox(height: 12),
                Builder(
                  builder: (context) {
                    final state = _states
                        .where((s) => s['countryCode'] == country['code'])
                        .firstOrNull;
                    return ClassicPaperPanel(
                      title: country['name'].toString(),
                      icon: Icons.public,
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.stretch,
                        children: [
                          Text(
                            'Estado: ${state?['status'] ?? 'NOT_REQUESTED'}',
                          ),
                          Text(
                            'Última atualização: ${_date(state?['lastSuccessfulUpdateAt'])} • Próxima: ${_date(state?['nextEligibleUpdateAt'])}',
                          ),
                          if (state?['lastError'] != null)
                            Text('Pendência: ${state!['lastError']}'),
                          if (state?['totalTeams'] is num &&
                              (state!['totalTeams'] as num) > 0)
                            Text(
                              'Lote: ${state['completedTeams']}/${state['totalTeams']} clubes • ${state['phase']}',
                            ),
                          Wrap(
                            spacing: 12,
                            runSpacing: 8,
                            children: [
                              ElevatedButton(
                                onPressed: _busy
                                    ? null
                                    : () => _run(() async {
                                        await _call(
                                          'refresh',
                                          body: {
                                            'countryCode': country['code'],
                                            'gender': _gender,
                                            'season': _season,
                                          },
                                        );
                                        await _load();
                                      }),
                                child: const Text('ATUALIZAR PAÍS'),
                              ),
                              OutlinedButton(
                                onPressed: _busy
                                    ? null
                                    : () => _run(() => _report(country)),
                                child: const Text('VER RELATÓRIO / PENDÊNCIAS'),
                              ),
                            ],
                          ),
                        ],
                      ),
                    );
                  },
                ),
              ],
              if (_error.isNotEmpty)
                Padding(
                  padding: const EdgeInsets.all(12),
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
