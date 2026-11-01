import 'package:flutter/material.dart';
import 'api_client.dart';

class ImportAdmin016A extends StatefulWidget {
  final ElifootApiClient api;
  const ImportAdmin016A({super.key, required this.api});
  @override
  State<ImportAdmin016A> createState() => _ImportAdmin016AState();
}

class _ImportAdmin016AState extends State<ImportAdmin016A> {
  final token = TextEditingController();
  final Set<String> selected = {
    'flamengo',
    'corinthians',
    'real-madrid',
    'barcelona-male',
    'porto',
    'barcelona-female',
  };
  String provider = 'wikidata', error = '';
  bool busy = false, refresh = false;
  Map<String, dynamic>? coverage, run;
  static const clubs = {
    'flamengo': 'Flamengo · Brasil · Masculino',
    'corinthians': 'Corinthians · Brasil · Masculino',
    'real-madrid': 'Real Madrid · Espanha · Masculino',
    'barcelona-male': 'Barcelona · Espanha · Masculino',
    'porto': 'FC Porto · Portugal · Masculino',
    'barcelona-female': 'Barcelona Feminino · Espanha · Feminino',
  };
  @override
  void dispose() {
    token.dispose();
    super.dispose();
  }

  Future<void> act(Future<void> Function() work) async {
    setState(() {
      busy = true;
      error = '';
    });
    try {
      await work();
    } catch (e) {
      if (mounted) setState(() => error = '$e');
    } finally {
      if (mounted) setState(() => busy = false);
    }
  }

  Future<void> load() async {
    final value = await widget.api.import016a(token.text, 'coverage');
    if (mounted) setState(() => coverage = Map<String, dynamic>.from(value));
  }

  Future<void> dryRun() async {
    final value = await widget.api.import016a(
      token.text,
      'dry-run',
      body: {
        'provider': provider,
        'clubKeys': selected.toList(),
        'refresh': refresh,
      },
    );
    if (mounted) setState(() => run = Map<String, dynamic>.from(value));
    await load();
  }

  Future<void> apply() async {
    final value = await widget.api.import016a(
      token.text,
      'apply/${run!['id']}',
      body: {},
    );
    if (mounted) setState(() => run = Map<String, dynamic>.from(value));
    await load();
  }

  Future<void> mapTeam(Map club) async {
    final team = TextEditingController(),
        league = TextEditingController(),
        name = TextEditingController(),
        evidence = TextEditingController();
    final values = await showDialog<Map<String, dynamic>>(
      context: context,
      builder: (c) => AlertDialog(
        title: Text('Revisar identidade — ${club['name']}'),
        content: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(
                'País ${club['countryCode']} · Categoria ${club['gender']} · Wikidata ${club['wikidataId']}. Confirme a equipe e a liga corretas; nomes semelhantes não bastam.',
              ),
              TextField(
                controller: team,
                decoration: const InputDecoration(
                  labelText: 'ID API-Football da equipe',
                ),
              ),
              TextField(
                controller: league,
                decoration: const InputDecoration(
                  labelText: 'ID API-Football da liga',
                ),
              ),
              TextField(
                controller: name,
                decoration: const InputDecoration(
                  labelText: 'Nome exato da liga',
                ),
              ),
              TextField(
                controller: evidence,
                decoration: const InputDecoration(
                  labelText: 'Evidência da revisão de país e categoria',
                ),
              ),
            ],
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(c),
            child: const Text('CANCELAR'),
          ),
          TextButton(
            onPressed: () => Navigator.pop(c, {
              'clubKey': club['clubKey'],
              'provider': 'api-football',
              'externalTeamId': team.text,
              'externalLeagueId': league.text,
              'leagueName': name.text,
              'gender': club['gender'],
              'countryCode': club['countryCode'],
              'evidence': evidence.text,
            }),
            child: const Text('REGISTRAR REVISÃO'),
          ),
        ],
      ),
    );
    if (values != null) {
      await act(() async {
        await widget.api.import016a(token.text, 'map-team', body: values);
      });
    }
    team.dispose();
    league.dispose();
    name.dispose();
    evidence.dispose();
  }

  Widget reportClub(Map club) => Card(
    child: Padding(
      padding: const EdgeInsets.all(10),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            '${club['name']} · ${club['countryCode']} · ${club['gender']}',
            style: const TextStyle(fontWeight: FontWeight.bold),
          ),
          SelectableText(
            'ID: ${club['externalId'] ?? 'não conectado'} · Wikidata: ${club['wikidataId']} · Fonte: ${club['provider']}',
          ),
          Text(
            'Temporada: ${club['season']} · Consulta: ${club['observedAt']} · ${club['status']}',
          ),
          Text(
            '${club['receivedPlayers']} registros · ${club['usablePlayers']} utilizáveis · ${club['duplicates']} duplicações',
          ),
          Text(
            'Reportados: ${club['currentReported']} · Verificados: ${club['currentVerified']} · Históricos: ${club['historical']}',
          ),
          Text('Posições: ${club['positions']}'),
          for (final issue in club['issues'] as List? ?? []) Text('• $issue'),
          for (final candidate in club['candidates'] as List? ?? [])
            SelectableText(
              'Candidato: ${candidate['externalId']} · ${candidate['name']} · ${candidate['country']}',
            ),
          if (club['provider'] == 'api-football')
            TextButton(
              onPressed: busy ? null : () => mapTeam(club),
              child: const Text('REVISAR IDs / CATEGORIA / LIGA'),
            ),
          ExpansionTile(
            title: const Text('JOGADORES E PROVENIÊNCIA'),
            children: [
              for (final player in club['players'] as List? ?? [])
                ListTile(
                  dense: true,
                  title: Text(
                    '${player['position'] ?? '?'} · ${player['name']}',
                  ),
                  subtitle: Text(
                    '${player['externalId']} · ${player['origin']} · ${player['issues']}',
                  ),
                ),
            ],
          ),
        ],
      ),
    ),
  );
  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('DADOS DE CLUBES E JOGADORES')),
    body: SafeArea(
      child: ListView(
        padding: const EdgeInsets.all(12),
        children: [
          const Text(
            'Administração local antes do save. Atualizar o catálogo não modifica campeonatos existentes.',
          ),
          TextField(
            key: const Key('catalog-admin-token-016a'),
            controller: token,
            obscureText: true,
            enableSuggestions: false,
            autocorrect: false,
            decoration: const InputDecoration(
              labelText: 'Token do administrador local',
              helperText:
                  'Nunca informe a chave da API-Football aqui. O token não é salvo no navegador.',
            ),
          ),
          DropdownButton<String>(
            isExpanded: true,
            value: provider,
            items: const [
              DropdownMenuItem(
                value: 'wikidata',
                child: Text('Wikidata · dados estruturados CC0'),
              ),
              DropdownMenuItem(
                value: 'api-football',
                child: Text('API-Football · conta e direitos necessários'),
              ),
            ],
            onChanged: busy ? null : (v) => setState(() => provider = v!),
          ),
          for (final club in clubs.entries)
            CheckboxListTile(
              dense: true,
              title: Text(club.value),
              value: selected.contains(club.key),
              onChanged: busy
                  ? null
                  : (v) => setState(
                      () => v!
                          ? selected.add(club.key)
                          : selected.remove(club.key),
                    ),
            ),
          CheckboxListTile(
            title: const Text(
              'Atualizar consulta externa, respeitando orçamento',
            ),
            subtitle: const Text(
              'Desmarcado: reutilizar cache válido. Abertura do jogo não busca elencos.',
            ),
            value: refresh,
            onChanged: busy ? null : (v) => setState(() => refresh = v!),
          ),
          Wrap(
            spacing: 8,
            runSpacing: 8,
            children: [
              OutlinedButton(
                onPressed: busy ? null : () => act(load),
                child: const Text('VER COBERTURA'),
              ),
              OutlinedButton(
                onPressed: busy || selected.isEmpty
                    ? null
                    : () => act(() async {
                        final value = await widget.api.import016a(
                          token.text,
                          'dry-run',
                          body: {
                            'provider': provider,
                            'clubKeys': selected.toList(),
                            'refresh': refresh,
                            'includeSquads': false,
                          },
                        );
                        if (mounted) {
                          setState(
                            () => run = Map<String, dynamic>.from(value),
                          );
                        }
                      }),
                child: const Text('VER IDs DOS CLUBES'),
              ),
              ElevatedButton(
                key: const Key('catalog-dry-run-016a'),
                onPressed: busy || selected.isEmpty ? null : () => act(dryRun),
                child: const Text('TESTAR IMPORTAÇÃO — DRY RUN'),
              ),
              ElevatedButton(
                key: const Key('catalog-apply-016a'),
                onPressed:
                    busy ||
                        run == null ||
                        run!['applied'] == true ||
                        run!['status'] == 'NOT_APPLICABLE'
                    ? null
                    : () => act(apply),
                child: const Text('ATUALIZAR CATÁLOGO — APPLY'),
              ),
              OutlinedButton(
                onPressed: busy || run == null
                    ? null
                    : () => act(() async {
                        final value = await widget.api.import016a(
                          token.text,
                          'reports/${run!['id']}',
                        );
                        if (mounted) {
                          setState(
                            () => run = Map<String, dynamic>.from(value),
                          );
                        }
                      }),
                child: const Text('VER RELATÓRIO'),
              ),
            ],
          ),
          if (busy) const LinearProgressIndicator(),
          if (error.isNotEmpty)
            Text(error, style: const TextStyle(color: Colors.red)),
          if (coverage != null) ...[
            Text(
              'Última consulta: ${coverage!['lastUpdate'] ?? 'ainda não executada'}',
            ),
            for (final source in coverage!['providers'] as List)
              Text(
                '${source['provider']}: ${source['status']} · orçamento diário ${source['dailyBudget']} · usadas ${source['requestsUsed']} · restante ${source['providerRemaining'] ?? 'não informado'} · cache ${source['cacheEntries']}',
              ),
            Text(
              'Atletas reais documentados: ${coverage!['realPlayers']} · atuais reportados: ${coverage!['currentReported']} · atuais verificados: ${coverage!['currentVerified']} · registros incompletos: ${coverage!['incompleteRecords']}',
            ),
          ],
          if (run != null) ...[
            if (run!['isFixture'] == true)
              const Text(
                'FIXTURE DE TESTE — NÃO É IMPORTAÇÃO REAL',
                style: TextStyle(fontWeight: FontWeight.bold),
              ),
            SelectableText(
              '${run!['mode']} · ${run!['status']} · ${run!['id']}',
            ),
            for (final club in run!['clubs'] as List) reportClub(club),
          ],
        ],
      ),
    ),
  );
}
