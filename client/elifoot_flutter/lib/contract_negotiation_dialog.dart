import 'package:flutter/material.dart';
import 'auction_config.dart' show parseMarketAmount, formatAuctionMoney;

String _money(dynamic value) => value is num ? formatAuctionMoney(value) : '—';

class ContractNegotiationDialog extends StatefulWidget {
  final Map<String, dynamic> player, initialPreview;
  final Future<Map<String, dynamic>> Function(double) onQuote;
  final Future<void> Function(double, bool, Map<String, dynamic>) onDecision;
  const ContractNegotiationDialog({
    super.key,
    required this.player,
    required this.initialPreview,
    required this.onQuote,
    required this.onDecision,
  });

  @override
  State<ContractNegotiationDialog> createState() =>
      _ContractNegotiationDialogState();
}

class _ContractNegotiationDialogState extends State<ContractNegotiationDialog> {
  final _form = GlobalKey<FormState>();
  late final TextEditingController _salary = TextEditingController(
    text: (widget.initialPreview['currentSalary'] as num)
        .toStringAsFixed(2)
        .replaceAll('.', ','),
  );
  Map<String, dynamic>? _quote;
  double? _offered;
  bool _busy = false;
  String? _error;

  @override
  void dispose() {
    _salary.dispose();
    super.dispose();
  }

  Future<void> _consult() async {
    if (_busy || !_form.currentState!.validate()) return;
    final offered = parseMarketAmount(_salary.text)!;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      final quote = await widget.onQuote(offered);
      if (mounted) {
        setState(() {
          _quote = quote;
          _offered = offered;
        });
      }
    } catch (error) {
      if (mounted) {
        setState(() => _error = '$error'.replaceFirst('Exception: ', ''));
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _decide(bool accept) async {
    if (_busy) return;
    final quote = _quote!;
    final salary = accept && quote['offerAccepted'] != true
        ? (quote['requiredSalary'] as num).toDouble()
        : _offered!;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await widget.onDecision(salary, accept, quote);
      if (mounted) Navigator.pop(context, true);
    } catch (error) {
      if (mounted) {
        setState(() => _error = '$error'.replaceFirst('Exception: ', ''));
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final quote = _quote;
    final accepted = quote?['offerAccepted'] == true;
    return PopScope(
      canPop: !_busy,
      child: AlertDialog(
        scrollable: true,
        title: Text(
          quote == null
              ? 'RENOVAR CONTRATO'
              : accepted
              ? 'PROPOSTA ACEITA'
              : 'CONTRAPROPOSTA DO JOGADOR',
        ),
        content: SizedBox(
          width: 450,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            mainAxisSize: MainAxisSize.min,
            children: [
              Text(
                '${widget.player['displayName'] ?? widget.player['name']}',
                style: const TextStyle(fontWeight: FontWeight.bold),
              ),
              const SizedBox(height: 12),
              Text(
                'Salário atual: ${_money(widget.initialPreview['currentSalary'])} / partida',
              ),
              if (quote == null) ...[
                const SizedBox(height: 12),
                Form(
                  key: _form,
                  child: TextFormField(
                    key: const Key('renewal-salary-offer'),
                    controller: _salary,
                    enabled: !_busy,
                    keyboardType: const TextInputType.numberWithOptions(
                      decimal: true,
                    ),
                    decoration: const InputDecoration(
                      labelText: 'Salário oferecido por partida',
                      prefixText: 'R\$ ',
                    ),
                    validator: (value) => parseMarketAmount(value) == null
                        ? 'Informe um valor válido, com até duas casas decimais.'
                        : null,
                    onFieldSubmitted: (_) => _consult(),
                  ),
                ),
                const SizedBox(height: 12),
                const Text(
                  'Consulte a resposta do jogador antes de confirmar a renovação.',
                ),
              ] else ...[
                Text('Sua oferta: ${_money(_offered)} / partida'),
                const SizedBox(height: 12),
                Text(
                  accepted
                      ? 'O jogador aceita sua oferta.'
                      : 'O jogador recusou sua oferta e pede ${_money(quote['requiredSalary'])} / partida.',
                ),
                Text(
                  'Novo salário: ${_money(accepted ? quote['offeredSalary'] : quote['requiredSalary'])} / partida',
                ),
                Text(
                  'Luvas: ${_money(accepted ? quote['offeredSigningFee'] : quote['signingFee'])}',
                ),
                Text('Contrato até o fim da temporada ${quote['season']}.'),
                const Text('A renovação bloqueia a venda nesta temporada.'),
                if (!accepted) ...[
                  const SizedBox(height: 12),
                  const Text(
                    'Recusar a contraproposta libera o jogador para leilão por 1 rodada, com lance mínimo R\$ 0. O clube não recebe o arremate. O elenco mínimo deve ser respeitado.',
                  ),
                ],
              ],
              if (_error != null)
                Padding(
                  padding: const EdgeInsets.only(top: 12),
                  child: Text(
                    _error!,
                    key: const Key('renewal-negotiation-error'),
                    style: TextStyle(
                      color: Theme.of(context).colorScheme.error,
                    ),
                  ),
                ),
              if (_busy)
                const Padding(
                  padding: EdgeInsets.only(top: 12),
                  child: LinearProgressIndicator(),
                ),
            ],
          ),
        ),
        actions: [
          TextButton(
            onPressed: _busy ? null : () => Navigator.pop(context, false),
            child: const Text('CANCELAR'),
          ),
          if (quote == null)
            ElevatedButton(
              key: const Key('consult-renewal-offer'),
              onPressed: _busy ? null : _consult,
              child: const Text('CONSULTAR JOGADOR'),
            )
          else ...[
            if (!accepted)
              TextButton(
                key: const Key('refuse-renewal-counteroffer'),
                onPressed: _busy ? null : () => _decide(false),
                child: const Text('RECUSAR E ENVIAR A LEILÃO'),
              ),
            ElevatedButton(
              key: const Key('accept-renewal-offer'),
              onPressed: _busy ? null : () => _decide(true),
              child: Text(
                accepted ? 'CONFIRMAR RENOVAÇÃO' : 'ACEITAR E RENOVAR',
              ),
            ),
          ],
        ],
      ),
    );
  }
}
