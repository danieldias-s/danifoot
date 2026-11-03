import 'package:flutter/material.dart';

class LobbyRoomStatus extends StatelessWidget {
  final Map<String, dynamic> room;
  final String? currentCoachId;
  const LobbyRoomStatus({super.key, required this.room, this.currentCoachId});
  @override
  Widget build(BuildContext context) {
    final counter = room['readyCounter'] as Map? ?? {};
    final people = (counter['participants'] as List? ?? [])
        .whereType<Map>()
        .toList();
    final capacity = room['maxHumans'] ?? 8;
    final occupied =
        room['currentHumans'] ?? counter['totalHumans'] ?? people.length;
    final online = room['connectedHumans'];
    final offline = online is num && occupied is num ? occupied - online : null;
    final ready = counter['readyCount'] ?? 0,
        required = counter['totalHumans'] ?? occupied;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(
          'CONECTADOS: ${online ?? '—'}/$capacity',
          key: ValueKey('lobby-connected-${room['id']}'),
        ),
        Text(
          'VAGAS OCUPADAS: $occupied/$capacity',
          key: ValueKey('lobby-occupied-${room['id']}'),
        ),
        Text(
          'TÁTICAS ENVIADAS: $ready/$required',
          key: ValueKey('lobby-ready-${room['id']}'),
        ),
        Text(
          'ESTADO: ${room['roundState'] ?? '—'}',
          key: ValueKey('lobby-state-${room['id']}'),
        ),
        if (occupied is num && capacity is num && occupied >= capacity)
          const Text(
            'SALA LOTADA para novos treinadores. Quem já participa pode retomar sua sessão.',
          ),
        if (offline != null && offline > 0)
          Text(
            '$offline treinador(es) desconectado(s), com vaga e clube preservados.',
          ),
        if (people.isNotEmpty)
          ExpansionTile(
            tilePadding: EdgeInsets.zero,
            title: const Text('TREINADORES DA SALA'),
            children: [
              for (final p in people)
                ListTile(
                  dense: true,
                  selected:
                      currentCoachId != null && p['coachId'] == currentCoachId,
                  leading:
                      currentCoachId != null && p['coachId'] == currentCoachId
                      ? const Icon(Icons.account_circle)
                      : null,
                  title: Text(
                    '${p['nickname']}${currentCoachId != null && p['coachId'] == currentCoachId ? ' • VOCÊ' : ''}',
                  ),
                  subtitle: Text(
                    '${p['clubName'] ?? 'Sem clube'} • ${p['isConnected'] == true ? 'CONECTADO' : 'DESCONECTADO'} • ${p['isReady'] == true ? 'TÁTICA ENVIADA' : 'TÁTICA PENDENTE'}',
                  ),
                ),
            ],
          ),
      ],
    );
  }
}
