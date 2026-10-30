import 'package:flutter/material.dart';

String catalogIdentityOrigin(Map<String, dynamic> player) =>
    switch (player['identityOrigin']) {
      'Real' => 'DADO REAL',
      'Generated' => 'FICTÍCIO GERADO',
      'Fixture' => 'FIXTURE DE TESTE',
      _ => 'Origem legada / consultar fonte',
    };

String catalogStrengthDescription(Map<String, dynamic> player) {
  final estimate = player['strengthEstimate'];
  if (estimate is Map) {
    final method = estimate['method'].toString();
    if (method.endsWith(':pending')) return 'força estimada na prévia';
    return 'força estimada ${player['strength']} • $method';
  }
  return 'força ${player['strength']}';
}

Widget catalogPlayerRow(Map<String, dynamic> player) => ListTile(
  dense: true,
  title: Text(
    '${player['position']} • ${player['playerName']} • ${catalogStrengthDescription(player)}',
  ),
  subtitle: Text(
    '${catalogIdentityOrigin(player)} • ${player['source']} • ${player['updatedAt']}',
  ),
);

Widget catalogRankingLabel(Map<String, dynamic> club) {
  final rank = club['rankingInfo'];
  if (rank is Map && rank['method'] == 'Unranked-v2') {
    return const Text(
      'NÃO CLASSIFICADO — sem resultados suficientes; desempate técnico não é ranking.',
    );
  }
  if (rank is! Map) {
    return const Text('Ranking ausente: ordem provisória por identificador.');
  }
  return Text(
    'Ranking: ${rank['method'] ?? 'posição informada'} • ${rank['source']}\n'
    'Data ${rank['updatedAt']} • score ${rank['score'] ?? '-'} • confiança ${rank['confidence'] ?? '-'}'
    '${rank['origin'] == 'Estimated' ? ' • ESTIMADO' : ''}',
  );
}

Widget catalogClubStatus(Map<String, dynamic> club) {
  final verification = club['verification'];
  if (verification is! Map) return const SizedBox.shrink();
  return Text(
    '${verification['status']} • fonte/temporada ${verification['sourceSeason'] ?? "não informada"}'
    '${verification['currentSeasonConfirmed'] == true ? " • participação datada" : " • atividade atual não certificada"}',
    style: const TextStyle(color: Colors.amber),
  );
}
