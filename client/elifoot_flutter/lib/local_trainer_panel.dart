import 'package:flutter/material.dart';
import 'club_theme.dart';

class LocalTrainerChoice {
  final String deviceId, nickname, detail;
  const LocalTrainerChoice(this.deviceId, this.nickname, this.detail);
}

/// The device key identifies a saved profile internally; players choose by name.
class LocalTrainerPanel extends StatelessWidget {
  final String nickname, deviceId, detail;
  final bool connected, busy;
  final List<LocalTrainerChoice> profiles;
  final VoidCallback? enter, create, recover;
  final ValueChanged<String>? select;
  const LocalTrainerPanel({
    super.key,
    required this.nickname,
    required this.deviceId,
    required this.detail,
    required this.connected,
    required this.busy,
    required this.profiles,
    this.enter,
    this.create,
    this.recover,
    this.select,
  });

  @override
  Widget build(BuildContext context) {
    final theme = ClassicTheme.of(context);
    final ink = readableInk(theme.primary, Colors.white);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Container(
          key: const Key('current-local-trainer'),
          padding: const EdgeInsets.all(16),
          decoration: BoxDecoration(
            color: theme.primary,
            border: Border.all(color: theme.selectedBackground, width: 2),
            borderRadius: BorderRadius.circular(10),
          ),
          child: Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Icon(Icons.account_circle, size: 42, color: ink),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      connected
                          ? 'VOCÊ ESTÁ USANDO ESTE TREINADOR'
                          : 'SEU TREINADOR',
                      style: TextStyle(color: ink, fontSize: 14),
                    ),
                    Text(
                      nickname,
                      style: TextStyle(
                        color: ink,
                        fontSize: 28,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                    if (detail.isNotEmpty)
                      Text(detail, style: TextStyle(color: ink, fontSize: 16)),
                  ],
                ),
              ),
              if (connected)
                Tooltip(
                  message: 'Treinador selecionado nesta aba',
                  child: Icon(
                    Icons.check_circle,
                    color: theme.selectedBackground,
                  ),
                ),
            ],
          ),
        ),
        const SizedBox(height: 10),
        const Text(
          'Seu acesso é salvo automaticamente neste navegador. Para voltar, escolha seu treinador pelo nome.',
        ),
        if (profiles.isNotEmpty) ...[
          const SizedBox(height: 14),
          DropdownButtonFormField<String>(
            key: ValueKey('saved-trainer-$deviceId'),
            initialValue: profiles.any((p) => p.deviceId == deviceId)
                ? deviceId
                : null,
            isExpanded: true,
            isDense: false,
            itemHeight: 76,
            decoration: const InputDecoration(
              labelText: 'SEUS TREINADORES SALVOS',
              border: OutlineInputBorder(),
            ),
            selectedItemBuilder: (_) => [
              for (final profile in profiles)
                Align(
                  alignment: Alignment.centerLeft,
                  child: Text(
                    '${profile.nickname} • ${profile.detail}',
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                  ),
                ),
            ],
            items: [
              for (final profile in profiles)
                DropdownMenuItem(
                  value: profile.deviceId,
                  child: Row(
                    children: [
                      Expanded(
                        child: Column(
                          mainAxisAlignment: MainAxisAlignment.center,
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Text(
                              profile.nickname,
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                            ),
                            Text(
                              profile.detail,
                              maxLines: 2,
                              overflow: TextOverflow.ellipsis,
                              style: const TextStyle(fontSize: 13),
                            ),
                          ],
                        ),
                      ),
                      if (profile.deviceId == deviceId) ...[
                        const SizedBox(width: 8),
                        const Icon(Icons.check_circle, size: 18),
                        const SizedBox(width: 4),
                        const Text('EM USO', style: TextStyle(fontSize: 12)),
                      ],
                    ],
                  ),
                ),
            ],
            onChanged: busy
                ? null
                : (value) {
                    if (value != null) select?.call(value);
                  },
          ),
        ],
        const SizedBox(height: 12),
        Wrap(
          spacing: 10,
          runSpacing: 10,
          children: [
            ElevatedButton.icon(
              key: const Key('enter-local-lobby'),
              onPressed: busy ? null : enter,
              style: ElevatedButton.styleFrom(minimumSize: const Size(0, 44)),
              icon: const Icon(Icons.login),
              label: const Text('ENTRAR NO LOBBY'),
            ),
            OutlinedButton.icon(
              key: const Key('new-local-trainer'),
              onPressed: busy ? null : create,
              style: OutlinedButton.styleFrom(minimumSize: const Size(0, 44)),
              icon: const Icon(Icons.person_add_alt_1),
              label: const Text('CRIAR NOVO TREINADOR'),
            ),
          ],
        ),
        const SizedBox(height: 6),
        const Text(
          'Criar outro treinador mantém os anteriores na lista de salvos.',
        ),
        ExpansionTile(
          tilePadding: EdgeInsets.zero,
          title: const Text(
            'Recuperação de acesso',
            style: TextStyle(fontSize: 14),
          ),
          children: [
            const Text(
              'Se um treinador antigo não aparece na lista, você pode recuperar usando o nome e o identificador que já utilizava.',
            ),
            TextButton(
              onPressed: busy ? null : recover,
              child: const Text('RECUPERAR TREINADOR ANTIGO'),
            ),
          ],
        ),
      ],
    );
  }
}

class TrainerAccessDialog extends StatefulWidget {
  final String initialName;
  final bool recovery;
  const TrainerAccessDialog({
    super.key,
    required this.initialName,
    this.recovery = false,
  });
  @override
  State<TrainerAccessDialog> createState() => _TrainerAccessDialogState();
}

class _TrainerAccessDialogState extends State<TrainerAccessDialog> {
  final _form = GlobalKey<FormState>();
  late final TextEditingController _name;
  final _identifier = TextEditingController();
  @override
  void initState() {
    super.initState();
    _name = TextEditingController(text: widget.initialName);
    _name.selection = TextSelection(
      baseOffset: 0,
      extentOffset: _name.text.length,
    );
  }

  @override
  void dispose() {
    _name.dispose();
    _identifier.dispose();
    super.dispose();
  }

  void _submit() {
    if (!_form.currentState!.validate()) return;
    Navigator.pop(context, {
      'nickname': _name.text.trim(),
      if (widget.recovery) 'deviceId': _identifier.text.trim(),
    });
  }

  @override
  Widget build(BuildContext context) => AlertDialog(
    title: Text(
      widget.recovery ? 'RECUPERAR TREINADOR' : 'CRIAR NOVO TREINADOR',
    ),
    content: SizedBox(
      width: 400,
      child: SingleChildScrollView(
        child: Form(
          key: _form,
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text(
                widget.recovery
                    ? 'Use os dados do acesso antigo para voltar ao mesmo treinador.'
                    : 'Escolha o nome que você reconhecerá ao voltar. O acesso será salvo automaticamente; seus outros treinadores continuam disponíveis.',
              ),
              const SizedBox(height: 16),
              TextFormField(
                key: const Key('trainer-name'),
                controller: _name,
                autofocus: true,
                decoration: const InputDecoration(
                  labelText: 'Nome do treinador',
                  hintText: 'Ex.: Daniel',
                ),
                textInputAction: widget.recovery
                    ? TextInputAction.next
                    : TextInputAction.done,
                onFieldSubmitted: widget.recovery ? null : (_) => _submit(),
                validator: (value) => value == null || value.trim().isEmpty
                    ? 'Digite o nome do treinador.'
                    : null,
              ),
              if (widget.recovery) ...[
                const SizedBox(height: 12),
                TextFormField(
                  key: const Key('trainer-recovery-id'),
                  controller: _identifier,
                  decoration: const InputDecoration(
                    labelText: 'Identificador do acesso antigo',
                  ),
                  onFieldSubmitted: (_) => _submit(),
                  validator: (value) => value == null || value.trim().isEmpty
                      ? 'Informe o identificador antigo.'
                      : null,
                ),
              ],
            ],
          ),
        ),
      ),
    ),
    actions: [
      TextButton(
        onPressed: () => Navigator.pop(context),
        child: const Text('CANCELAR'),
      ),
      ElevatedButton(
        onPressed: _submit,
        child: Text(widget.recovery ? 'RECUPERAR ACESSO' : 'CRIAR E SALVAR'),
      ),
    ],
  );
}
