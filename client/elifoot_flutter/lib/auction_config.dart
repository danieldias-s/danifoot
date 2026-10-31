import 'package:flutter/material.dart';
import 'player_rankings.dart';

class VoluntaryAuctionConfig {
  final double minimumBid;
  final int durationRounds;
  const VoluntaryAuctionConfig({
    this.minimumBid = 500000,
    this.durationRounds = 2,
  });
}

/// New voluntary sales always start with these defaults; no global/economic setting is changed.
class AuctionConfigDialog extends StatefulWidget {
  final String playerName;
  final int roundNumber, seasonNumber, totalRoundsInSeason;
  final bool roundAlreadyCompleted;
  const AuctionConfigDialog({
    super.key,
    required this.playerName,
    required this.roundNumber,
    required this.seasonNumber,
    required this.totalRoundsInSeason,
    this.roundAlreadyCompleted = false,
  });
  @override
  State<AuctionConfigDialog> createState() => _AuctionConfigDialogState();
}

class _AuctionConfigDialogState extends State<AuctionConfigDialog> {
  final _form = GlobalKey<FormState>();
  final _minimum = TextEditingController(text: '500000');
  int _duration = 2;
  double? _amount(String? raw) {
    final text = (raw ?? '').trim().replaceAll(',', '.');
    if (!RegExp(r'^\d+(\.\d{1,2})?$').hasMatch(text)) return null;
    final value = double.tryParse(text);
    return value != null && value.isFinite && value >= 0 ? value : null;
  }

  @override
  void dispose() {
    _minimum.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final total = widget.totalRoundsInSeason > 0
        ? widget.totalRoundsInSeason
        : 22;
    final end =
        widget.roundNumber +
        _duration -
        1 +
        (widget.roundAlreadyCompleted ? 1 : 0);
    final season = widget.seasonNumber + (end - 1) ~/ total,
        round = (end - 1) % total + 1;
    return AlertDialog(
      title: const Text('NOVO LEILÃO VOLUNTÁRIO'),
      content: SingleChildScrollView(
        child: SizedBox(
          width: 400,
          child: Form(
            key: _form,
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Text(widget.playerName),
                const SizedBox(height: 12),
                TextFormField(
                  key: const ValueKey('auction-minimum-bid'),
                  controller: _minimum,
                  keyboardType: const TextInputType.numberWithOptions(
                    decimal: true,
                  ),
                  decoration: const InputDecoration(
                    labelText: 'Lance mínimo (R\$)',
                    helperText: 'Valor a partir de zero; até 2 casas decimais.',
                  ),
                  validator: (v) => _amount(v) == null
                      ? 'Informe um valor válido maior ou igual a zero.'
                      : null,
                ),
                const SizedBox(height: 16),
                DropdownButtonFormField<int>(
                  key: const ValueKey('auction-duration'),
                  initialValue: _duration,
                  decoration: const InputDecoration(
                    labelText: 'Duração (rodadas globais)',
                  ),
                  items: [
                    for (final n in [1, 2, 3])
                      DropdownMenuItem(
                        value: n,
                        child: Text('$n ${n == 1 ? 'rodada' : 'rodadas'}'),
                      ),
                  ],
                  onChanged: (n) {
                    if (n != null) setState(() => _duration = n);
                  },
                ),
                const SizedBox(height: 12),
                Text(
                  'Encerramento após conclusão da R$round / T$season.',
                  key: const ValueKey('auction-deadline'),
                ),
                const Text(
                  'Contam apenas rodadas globais oficialmente concluídas.',
                ),
                const Text(
                  'O jogador permanece no clube até uma venda válida; sem comprador, continua no elenco.',
                ),
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
          key: const ValueKey('auction-confirm'),
          onPressed: () {
            if (_form.currentState!.validate()) {
              Navigator.pop(
                context,
                VoluntaryAuctionConfig(
                  minimumBid: _amount(_minimum.text)!,
                  durationRounds: _duration,
                ),
              );
            }
          },
          child: const Text('CRIAR LEILÃO'),
        ),
      ],
    );
  }
}

String formatAuctionMoney(num value) {
  if (value == value.roundToDouble()) return formatClassicMoney(value);
  return '${formatClassicMoney(value.floor())},${((value - value.floor()) * 100).round().toString().padLeft(2, '0')}';
}
