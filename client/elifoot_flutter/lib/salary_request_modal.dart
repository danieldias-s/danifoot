import 'package:flutter/material.dart';
import 'club_crest.dart';
import 'player_rankings.dart' show formatClassicMoney;

/// Shared persisted decision. Presentation never computes a wage or auction price.
class SalaryRequestModal extends StatefulWidget {
  final Map<String, dynamic> request, player, club;
  final bool classic;
  final Future<void> Function(bool accept) onDecision;
  const SalaryRequestModal({
    super.key,
    required this.request,
    required this.player,
    required this.club,
    required this.classic,
    required this.onDecision,
  });
  @override
  State<SalaryRequestModal> createState() => _SalaryRequestModalState();
}

class _SalaryRequestModalState extends State<SalaryRequestModal> {
  bool busy = false;
  String? error;
  Future<void> decide(bool accept) async {
    if (busy) return;
    setState(() {
      busy = true;
      error = null;
    });
    try {
      await widget.onDecision(accept);
    } catch (e) {
      if (mounted) setState(() => error = '$e');
    } finally {
      if (mounted) setState(() => busy = false);
    }
  }

  @override
  Widget build(BuildContext context) {
    final current = widget.request['currentSalary'] as num? ?? 0;
    final requested = widget.request['requestedSalary'] as num? ?? 0;
    final mobile = MediaQuery.sizeOf(context).width < 600;
    Widget button(bool accept) => SizedBox(
      height: 54,
      child: ElevatedButton(
        onPressed: busy ? null : () => decide(accept),
        style: ElevatedButton.styleFrom(
          backgroundColor: accept
              ? const Color(0xFF006620)
              : const Color(0xFFE3E0DB),
          foregroundColor: accept ? Colors.white : const Color(0xFF800000),
          side: BorderSide(
            color: accept ? const Color(0xFF004410) : const Color(0xFF800000),
            width: 2,
          ),
          shape: RoundedRectangleBorder(
            borderRadius: BorderRadius.circular(widget.classic ? 3 : 10),
          ),
        ),
        child: Text(
          accept ? 'ACEITAR' : 'RECUSAR',
          style: const TextStyle(fontSize: 22, fontWeight: FontWeight.bold),
        ),
      ),
    );
    return PopScope(
      canPop: false,
      child: Material(
        color: Colors.black54,
        child: SafeArea(
          child: Center(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: ConstrainedBox(
                constraints: const BoxConstraints(maxWidth: 640),
                child: Material(
                  color: const Color(0xFFF6F4EC),
                  elevation: 18,
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(
                      widget.classic ? 3 : 14,
                    ),
                    side: const BorderSide(color: Colors.white, width: 3),
                  ),
                  clipBehavior: Clip.antiAlias,
                  child: SingleChildScrollView(
                    child: Column(
                      mainAxisSize: MainAxisSize.min,
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        Container(
                          color: widget.classic
                              ? const Color(0xFF000080)
                              : const Color(0xFF003C2B),
                          padding: const EdgeInsets.all(16),
                          child: const Text(
                            'PEDIDO DE AUMENTO SALARIAL',
                            textAlign: TextAlign.center,
                            style: TextStyle(
                              color: Colors.white,
                              fontSize: 22,
                              fontWeight: FontWeight.bold,
                            ),
                          ),
                        ),
                        Padding(
                          padding: EdgeInsets.all(mobile ? 16 : 28),
                          child: DefaultTextStyle(
                            style: const TextStyle(
                              color: Color(0xFF102C1D),
                              fontSize: 19,
                              height: 1.35,
                              fontFamily: 'ClassicCondensed',
                            ),
                            child: Column(
                              crossAxisAlignment: CrossAxisAlignment.stretch,
                              children: [
                                Row(
                                  children: [
                                    ClubCrest(
                                      club: '${widget.club['name'] ?? ''}',
                                      size: 64,
                                      palette:
                                          widget.club['colorPalette'] is Map
                                          ? Map<String, dynamic>.from(
                                              widget.club['colorPalette'],
                                            )
                                          : null,
                                    ),
                                    const SizedBox(width: 18),
                                    Expanded(
                                      child: Column(
                                        crossAxisAlignment:
                                            CrossAxisAlignment.start,
                                        children: [
                                          Text(
                                            '${widget.player['displayName'] ?? widget.player['name']}',
                                            style: const TextStyle(
                                              fontSize: 24,
                                              fontWeight: FontWeight.bold,
                                            ),
                                          ),
                                          Text(
                                            'Posição: ${widget.player['position'] ?? '—'}',
                                          ),
                                          const Chip(
                                            label: Text('SEM CONTRATO'),
                                          ),
                                        ],
                                      ),
                                    ),
                                  ],
                                ),
                                const Divider(height: 28),
                                Text(
                                  'Salário atual: ${formatClassicMoney(current)} / jogo',
                                ),
                                Text(
                                  'Salário solicitado: ${formatClassicMoney(requested)} / jogo',
                                ),
                                Text(
                                  'Aumento: +${formatClassicMoney(requested - current)} / jogo',
                                ),
                                const Divider(height: 28),
                                const Text(
                                  'Aceitar: atualiza o salário e marca RENOVADO nesta temporada.',
                                ),
                                const SizedBox(height: 10),
                                const Text(
                                  'Recusar: o jogador vai automaticamente para leilão.',
                                ),
                                if (error != null)
                                  Padding(
                                    padding: const EdgeInsets.symmetric(
                                      vertical: 10,
                                    ),
                                    child: Text(
                                      error!,
                                      style: const TextStyle(
                                        color: Color(0xFF800000),
                                      ),
                                    ),
                                  ),
                                const SizedBox(height: 24),
                                if (busy) const LinearProgressIndicator(),
                                if (mobile) ...[
                                  button(true),
                                  const SizedBox(height: 12),
                                  button(false),
                                ] else
                                  Row(
                                    children: [
                                      Expanded(child: button(true)),
                                      const SizedBox(width: 16),
                                      Expanded(child: button(false)),
                                    ],
                                  ),
                              ],
                            ),
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
