/// Gate figures are populated together by round settlement. The live DTO has no
/// FinancialsSettled flag; its initial zero values must not imply an empty stadium.
bool matchGateReported(Map<String, dynamic> match) =>
    (match['ticketPrice'] as num? ?? 0) > 0 ||
    (match['attendance'] as num? ?? 0) > 0 ||
    (match['ticketRevenue'] as num? ?? 0) > 0;
num? officialGateValue(Map<String, dynamic> match, String key) =>
    matchGateReported(match) ? match[key] as num? : null;
String unreportedGateLabel(Map<String, dynamic> match) =>
    match.containsKey('attendance') || match.containsKey('ticketRevenue')
    ? 'Em apuração'
    : 'Não disponível';
