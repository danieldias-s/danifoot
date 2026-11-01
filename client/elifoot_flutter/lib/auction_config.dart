import 'package:flutter/material.dart';
import 'player_rankings.dart';

class VoluntaryAuctionConfig {
  final double minimumBid;
  final int durationRounds;
  final bool isDirect;
  const VoluntaryAuctionConfig({
    this.minimumBid = 500000,
    this.durationRounds = 2,
    this.isDirect = false,
  });
}

/// Explicit preview and confirmation for both voluntary listing types.
class AuctionConfigDialog extends StatefulWidget {
  final String playerName;
  final String? position;
  final num? strength, salary;
  final int roundNumber, seasonNumber, totalRoundsInSeason;
  final bool roundAlreadyCompleted, allowDirect, initialDirect;
  final num directPrice;
  const AuctionConfigDialog({
    super.key,
    required this.playerName,
    required this.roundNumber,
    required this.seasonNumber,
    required this.totalRoundsInSeason,
    this.roundAlreadyCompleted = false,
    this.position,
    this.strength,
    this.salary,
    this.allowDirect = false,
    this.initialDirect = false,
    this.directPrice = 500000,
  });
  @override
  State<AuctionConfigDialog> createState() => _AuctionConfigDialogState();
}

/// Values are numeric; Brazilian decimal input and server precision are preserved.
double? parseMarketAmount(String? raw, {bool allowZero = true}) {
  final text = (raw ?? '').trim().replaceAll(',', '.');
  if (!RegExp(r'^\d+(\.\d{1,2})?$').hasMatch(text)) return null;
  final value = double.tryParse(text);
  return value != null && value.isFinite && (allowZero ? value >= 0 : value > 0)
      ? value
      : null;
}

class _AuctionConfigDialogState extends State<AuctionConfigDialog> {
  final _form = GlobalKey<FormState>();
  late final TextEditingController _minimum = TextEditingController(
    text: widget.initialDirect ? widget.directPrice.toString() : '500000',
  );
  late bool _direct = widget.initialDirect;
  int _duration = 2;
  bool _review = false;
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
    final start = widget.roundNumber + (widget.roundAlreadyCompleted ? 1 : 0);
    final end = start + _duration - 1;
    final season = widget.seasonNumber + (end - 1) ~/ total,
        round = (end - 1) % total + 1;
    final amount = parseMarketAmount(_minimum.text, allowZero: !_direct);
    return AlertDialog(
      insetPadding: const EdgeInsets.symmetric(horizontal: 12, vertical: 16),
      title: Text(
        _review
            ? (_direct
                  ? 'CONFIRMAR VENDA POR PREÇO FIXO?'
                  : 'CONFIRMAR LEILÃO?')
            : (_direct ? 'VENDA POR PREÇO FIXO' : 'NOVO LEILÃO VOLUNTÁRIO'),
      ),
      content: SingleChildScrollView(
        child: SizedBox(
          width: 400,
          child: Form(
            key: _form,
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Text(
                  widget.playerName,
                  style: const TextStyle(fontWeight: FontWeight.bold),
                ),
                if (widget.position != null)
                  Text(
                    'Posição: ${widget.position} • Força: ${widget.strength ?? "—"}',
                  ),
                if (widget.salary != null)
                  Text(
                    'Salário: ${formatAuctionMoney(widget.salary!)} / rodada',
                  ),
                const SizedBox(height: 12),
                if (!_review && widget.allowDirect)
                  DropdownButtonFormField<bool>(
                    key: const ValueKey('listing-type'),
                    initialValue: _direct,
                    decoration: const InputDecoration(
                      labelText: 'Tipo de anúncio',
                    ),
                    items: const [
                      DropdownMenuItem(value: false, child: Text('LEILÃO')),
                      DropdownMenuItem(
                        value: true,
                        child: Text('COMPRA DIRETA'),
                      ),
                    ],
                    onChanged: (value) {
                      if (value != null) {
                        setState(() {
                          _direct = value;
                          _minimum.text = value
                              ? widget.directPrice.toString()
                              : '500000';
                        });
                      }
                    },
                  ),
                if (!_review) ...[
                  const SizedBox(height: 12),
                  TextFormField(
                    key: ValueKey(
                      _direct ? 'direct-fixed-price' : 'auction-minimum-bid',
                    ),
                    controller: _minimum,
                    keyboardType: const TextInputType.numberWithOptions(
                      decimal: true,
                    ),
                    decoration: InputDecoration(
                      labelText: _direct
                          ? 'Preço fixo de venda (R\$)'
                          : 'Lance mínimo (R\$)',
                      helperText: _direct
                          ? 'Preço positivo; até 2 casas decimais.'
                          : 'Valor a partir de zero; até 2 casas decimais.',
                    ),
                    validator: (v) =>
                        parseMarketAmount(v, allowZero: !_direct) == null
                        ? 'Informe um valor válido${_direct ? " maior que zero" : " maior ou igual a zero"}.'
                        : null,
                  ),
                  if (!_direct) ...[
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
                            child: Text('$n ${n == 1 ? "rodada" : "rodadas"}'),
                          ),
                      ],
                      onChanged: (n) {
                        if (n != null) setState(() => _duration = n);
                      },
                    ),
                  ],
                ] else ...[
                  Text(
                    '${_direct ? "Preço fixo" : "Lance mínimo"}: ${formatAuctionMoney(amount!)}',
                  ),
                  if (!_direct)
                    Text(
                      'Duração: $_duration ${_duration == 1 ? "rodada" : "rodadas"}',
                    ),
                ],
                const SizedBox(height: 12),
                if (!_direct) ...[
                  if (_review)
                    Text(
                      'Rodada inicial: R${(start - 1) % total + 1} / T${widget.seasonNumber + (start - 1) ~/ total}',
                    ),
                  Text(
                    'Encerramento após conclusão da R$round / T$season.',
                    key: const ValueKey('auction-deadline'),
                  ),
                  const Text(
                    'Contam apenas rodadas globais oficialmente concluídas.',
                  ),
                ],
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
        if (_review)
          TextButton(
            onPressed: () => setState(() => _review = false),
            child: const Text('EDITAR'),
          ),
        ElevatedButton(
          key: ValueKey(_review ? 'auction-publish' : 'auction-confirm'),
          onPressed: () {
            if (!_review) {
              if (_form.currentState!.validate()) {
                setState(() => _review = true);
              }
            } else {
              Navigator.pop(
                context,
                VoluntaryAuctionConfig(
                  minimumBid: amount!,
                  durationRounds: _duration,
                  isDirect: _direct,
                ),
              );
            }
          },
          child: Text(_review ? 'CONFIRMAR' : 'REVISAR ANÚNCIO'),
        ),
      ],
    );
  }
}

String formatAuctionMoney(num value) {
  if (value == value.roundToDouble()) return formatClassicMoney(value);
  return '${formatClassicMoney(value.floor())},${((value - value.floor()) * 100).round().toString().padLeft(2, '0')}';
}
