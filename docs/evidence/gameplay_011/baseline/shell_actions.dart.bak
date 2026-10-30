// ignore_for_file: invalid_use_of_protected_member
part of 'main.dart';

extension _ShellActions on _ElifootShellScreenState {
  Future<void> _runUiCommand(Future<void> Function() action) async {
    if (_commandBusy) return;
    setState(() => _commandBusy = true);
    try { await action(); } catch (e) { if (mounted) setState(() => _statusBanner = '$e'); }
    finally { if (mounted) setState(() => _commandBusy = false); }
  }

  Map<String,dynamic>? _preferLatestSnapshot(Map<String,dynamic>? incoming) {
    if (incoming == null) return _snapshot;
    if ((_snapshot?['roundVersion'] as num? ?? 0) > (incoming['roundVersion'] as num? ?? 0)) return _snapshot;
    return incoming;
  }
  Future<void> _saveSession() {
    return writeSession(jsonEncode({'nickname': _nickname, 'deviceId': _deviceId, 'worldId': _worldId}));
  }

  Future<void> _joinByCodeDialog() async {
    final code = TextEditingController();
    final confirmed = await showDialog<bool>(context: context, builder: (ctx) => AlertDialog(
      title: const Text('ENTRAR POR CÓDIGO'),
      content: TextField(controller: code, decoration: const InputDecoration(labelText: 'Código do convite')),
      actions: [TextButton(onPressed: () => Navigator.pop(ctx, false), child: const Text('CANCELAR')),
        ElevatedButton(onPressed: () => Navigator.pop(ctx, true), child: const Text('ENTRAR'))],
    ));
    if (confirmed == true && _coachId != null) {
      try {
        final result = await widget.apiClient.joinWorldByCode(inviteCode: code.text.trim(), coachId: _coachId!, nickname: _nickname, deviceId: _deviceId);
        await _joinSelectedWorld(result['worldId']);
      } catch (e) { if (mounted) setState(() => _statusBanner = '$e'); }
    }
    await Future<void>.delayed(const Duration(milliseconds: 300)); code.dispose();
  }

  Future<void> _chooseClub(String worldId) async {
    try {
      final snap = await widget.apiClient.getSnapshot(worldId);
      if ((snap['readyCounter']['participants'] as List).any((p) => p['coachId'] == _coachId)) {
        await _joinSelectedWorld(worldId); return;
      }
      final clubs = await widget.apiClient.getClubs(worldId);
      if (!mounted) return;
      final selected = await showDialog<String>(context: context, builder: (ctx) => AlertDialog(
        title: const Text('SELECIONE SEU CLUBE'),
        content: SizedBox(width: 600, height: 450, child: ListView(children: clubs
          .where((c) => c['divisionTier'] != 'Distrital' && c['divisionTier'] != 5 && c['isHumanControlled'] != true)
          .map<Widget>((c) => ListTile(
            key: Key('choose-club-${c['id']}'),
            leading: Container(width: 18, height: 28, color: _clubColor(c['primaryColorHex'])),
            title: Text(c['name']), subtitle: Text('${c['divisionDisplay']} • Caixa R\$ ${c['cashBalance']}'),
            onTap: () => Navigator.pop(ctx, c['id'].toString()),
          )).toList())),
        actions: [TextButton(onPressed: () => Navigator.pop(ctx), child: const Text('CANCELAR'))],
      ));
      if (selected != null) await _joinSelectedWorld(worldId, preferredClubId: selected);
    } catch (e) { if (mounted) setState(() => _statusBanner = '$e'); }
  }

  Color _clubColor(dynamic hex) => Color(int.tryParse('FF${(hex ?? '#FFFFFF').toString().replaceFirst('#', '')}', radix: 16) ?? 0xFFFFFFFF);

  Widget _buildCalendarScreen() => ListView(padding: const EdgeInsets.all(12), children: [
    const Text('CALENDÁRIO DA TEMPORADA E RESULTADOS', style: TextStyle(color: Color(0xFFFFD700), fontWeight: FontWeight.bold)),
    for (final r in _calendar) ExpansionTile(
      title: Text('Rodada ${r['roundNumber']} • ${r['competition']}'),
      initiallyExpanded: r['roundNumber'] == _snapshot?['roundNumber'],
      children: [
        if ((r['fixtures'] as List).isEmpty) const ListTile(title: Text('Confrontos definidos após a fase anterior da Taça.')),
        for (final f in r['fixtures']) ListTile(dense: true,
          title: Text('${f['home']} ${f['isFinished'] == true ? "${f['homeGoals']} x ${f['awayGoals']}" : "×"} ${f['away']}'),
          trailing: Text(f['isFinished'] == true ? 'FINAL' : 'A JOGAR'),
        ),
      ],
    ),
  ]);

  Future<void> _loadEditor() async {
    try {
      final data = jsonDecode(await widget.apiClient.exportWorldJson(_worldId!));
      if (!mounted) return;
      setState(() {
        _editorClubs = data['clubs']; _editorPlayers = data['players']; _editorManagers = data['aiManagers'];
        _statusBanner = 'Editor carregado. Escolha um clube, jogador ou treinador.';
      });
    } catch (e) { if (mounted) setState(() => _statusBanner = '$e'); }
  }

  Widget _editorPicker(String label, List<dynamic> entries, String kind) => Padding(
    padding: const EdgeInsets.symmetric(vertical: 6),
    child: DropdownButtonFormField<String>(isExpanded: true,
      decoration: InputDecoration(labelText: label),
      items: entries.map((e) => DropdownMenuItem<String>(value: e['id'], child: Text('${e['name']}'))).toList(),
      onChanged: (id) => _editEntity(kind, Map<String,dynamic>.from(entries.firstWhere((e) => e['id'] == id))),
    ),
  );

  Future<void> _editEntity(String kind, Map<String,dynamic> data) async {
    final isPlayer = kind == 'update-player';
    final isManager = kind == 'update-manager';
    final name = TextEditingController(text: data['name']);
    final short = TextEditingController(text: data['shortName'] ?? '');
    final primary = TextEditingController(text: data['primaryColorHex'] ?? '');
    final secondary = TextEditingController(text: data['secondaryColorHex'] ?? '');
    final stadium = TextEditingController(text: data['stadiumName'] ?? '');
    final strength = TextEditingController(text: '${data['strength'] ?? 20}');
    final prefs = TextEditingController(text: jsonEncode(data['preferredFormations'] ?? []));
    String position = '${data['position'] ?? 'M'}';
    String behavior = '${data['behavior'] ?? 'FairPlay'}';
    String identity = '${data['identity'] ?? 'Equilibrado'}';
    bool star = data['isStar'] == true;
    String? error;
    await showDialog<void>(context: context, builder: (ctx) => StatefulBuilder(builder: (ctx, update) => AlertDialog(
      title: Text(isPlayer ? 'EDITAR JOGADOR' : isManager ? 'EDITAR TREINADOR' : 'EDITAR CLUBE'),
      content: SizedBox(width: 520, child: SingleChildScrollView(child: Column(mainAxisSize: MainAxisSize.min, children: [
        TextField(controller: name, decoration: const InputDecoration(labelText: 'Nome')),
        if (isPlayer) ...[
          TextField(controller: strength, keyboardType: TextInputType.number, decoration: const InputDecoration(labelText: 'Força (1 a 50)')),
          DropdownButtonFormField<String>(initialValue: position, decoration: const InputDecoration(labelText: 'Posição'),
            items: ['G','D','M','A'].map((v) => DropdownMenuItem(value: v, child: Text(v))).toList(), onChanged: (v) => position = v!),
          DropdownButtonFormField<String>(initialValue: behavior, decoration: const InputDecoration(labelText: 'Comportamento'),
            items: ['FairPlay','Cordeirinho','Cavalheiro','Caneleiro','Caceteiro','Sarrafeiro'].map((v) => DropdownMenuItem(value: v, child: Text(v == 'FairPlay' ? 'Fair Play' : v))).toList(), onChanged: (v) => behavior = v!),
          CheckboxListTile(title: const Text('Craque (*)'), value: star, onChanged: (v) => update(() => star = v!)),
        ] else if (isManager) ...[
          DropdownButtonFormField<String>(initialValue: identity, decoration: const InputDecoration(labelText: 'Personalidade'),
            items: ['Ofensivo','Equilibrado','Defensivo'].map((v) => DropdownMenuItem(value: v, child: Text(v))).toList(), onChanged: (v) => identity = v!),
          TextField(controller: prefs, maxLines: 5, decoration: const InputDecoration(labelText: 'Formações e pesos (JSON; total 100%)')),
        ] else ...[
          TextField(controller: short, decoration: const InputDecoration(labelText: 'Sigla')),
          TextField(controller: primary, decoration: const InputDecoration(labelText: 'Cor principal (#RRGGBB)')),
          TextField(controller: secondary, decoration: const InputDecoration(labelText: 'Cor secundária (#RRGGBB)')),
          TextField(controller: stadium, decoration: const InputDecoration(labelText: 'Nome do estádio')),
        ],
        if (error != null) Text(error!, style: const TextStyle(color: Colors.redAccent)),
      ]))),
      actions: [
        TextButton(onPressed: () => Navigator.pop(ctx), child: const Text('CANCELAR')),
        ElevatedButton(onPressed: () async {
          try {
            if (name.text.trim().isEmpty) throw Exception('Informe um nome.');
            data['name'] = name.text.trim();
            if (isPlayer) {
              final value = int.tryParse(strength.text);
              if (value == null || value < 1 || value > 50) throw Exception('Força deve estar entre 1 e 50.');
              data.addAll({'strength': value, 'position': position, 'behavior': behavior, 'isStar': star});
            } else if (isManager) {
              data.addAll({'identity': identity, 'preferredFormations': jsonDecode(prefs.text)});
            } else {
              data.addAll({'shortName': short.text, 'primaryColorHex': primary.text, 'secondaryColorHex': secondary.text, 'stadiumName': stadium.text});
            }
            await widget.apiClient.saveEditorEntity(_worldId!, kind, data);
            if (!ctx.mounted) return;
            Navigator.pop(ctx);
            await _loadEditor(); await _refreshClubAndSecondaryData();
            if (mounted) setState(() => _statusBanner = 'Alterações salvas no servidor.');
          } catch (e) { if (ctx.mounted) update(() => error = '$e'); }
        }, child: const Text('SALVAR ALTERAÇÕES')),
      ],
    )));
    await Future<void>.delayed(const Duration(milliseconds: 300));
    for (final c in [name,short,primary,secondary,stadium,strength,prefs]) { c.dispose(); }
  }
}
