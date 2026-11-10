import 'package:flutter/material.dart';
import 'player_rankings.dart' show formatClassicMoney;

/// A voluntary quote. Dismissal never creates a salary request or an auction.
class VoluntaryRenewalDialog extends StatelessWidget {
  final Map<String, dynamic> player, preview;
  const VoluntaryRenewalDialog({
    super.key,
    required this.player,
    required this.preview,
  });

  @override
  Widget build(BuildContext context) => AlertDialog(
    scrollable: true,
    title: const Text('RENOVAÇÃO VOLUNTÁRIA'),
    content: Text(
      '${player['displayName'] ?? player['name']}\n'
      'Salário atual: ${formatClassicMoney(preview['currentSalary'])} / partida\n'
      'Salário exigido: ${formatClassicMoney(preview['requiredSalary'])} / partida\n'
      'Luvas: ${formatClassicMoney(preview['signingFee'])}\n'
      'Validade: fim da temporada ${preview['season']}.\n'
      'Aceitar bloqueia a venda nesta temporada.\n'
      'Desistir mantém jogador e salário, permite venda normal e não gera leilão.',
    ),
    actions: [
      TextButton(
        onPressed: () => Navigator.pop(context, false),
        child: const Text('DESISTIR'),
      ),
      ElevatedButton(
        key: const Key('confirm-season-renewal'),
        onPressed: () => Navigator.pop(context, true),
        child: Text(
          preview['requiresRaise'] == true ? 'ACEITAR EXIGÊNCIA' : 'RENOVAR',
        ),
      ),
    ],
  );
}
