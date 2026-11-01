import 'package:flutter/material.dart';

String catalogIdentityOrigin(Map<String, dynamic> player) {
  final identity = player['catalogIdentity'];
  final origin =
      player['playerOrigin'] ?? (identity is Map ? identity['origin'] : null);
  return switch (origin) {
    'REAL_CURRENT_VERIFIED' || 'REAL_VERIFIED_CURRENT' =>
      identity is Map
          ? 'REAL — VÍNCULO COMPROVADO NO SNAPSHOT ${identity['asOf'] ?? ''}'
          : 'REAL — VÍNCULO ATUAL COMPROVADO',
    'REAL_CURRENT_PROVIDER_REPORTED' =>
      'REAL — VÍNCULO ATUAL REPORTADO PELO FORNECEDOR, NÃO VERIFICADO',
    'REAL_HISTORICAL' ||
    'REAL_HISTORICAL_REFERENCE' => 'REFERÊNCIA REAL — NÃO É ELENCO ATUAL',
    'FICTIONAL_GENERATED' => 'FICTÍCIO GERADO',
    _ => switch (player['identityOrigin']) {
      'Real' => 'DADO REAL — conferir vínculo',
      'Generated' => 'FICTÍCIO GERADO',
      'Fixture' => 'FIXTURE DE TESTE',
      _ => 'Origem legada / consultar fonte',
    },
  };
}

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
    '${catalogIdentityOrigin(player)} • ${player['source']} • ${player['playerOrigin'] == 'FICTIONAL_GENERATED' ? 'geração determinística; seed salva' : player['updatedAt']}',
  ),
);

Widget catalogRankingLabel(Map<String, dynamic> club) {
  final rank = club['rankingInfo'];
  if (rank is Map && rank['method'] == 'ProvisionalSeededOrder-v1') {
    return const Text(
      'CLASSIFICAÇÃO PROVISÓRIA • baixa confiança • desempate por seed, sem ranking esportivo comprovado.',
    );
  }
  if (rank is Map && rank['method'] == 'Unranked-v2') {
    return const Text(
      'NÃO CLASSIFICADO — sem resultados suficientes; desempate técnico não é ranking.',
    );
  }
  if (rank is! Map) {
    return const Text(
      'Ranking ausente: sem classificação esportiva comprovada.',
    );
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
