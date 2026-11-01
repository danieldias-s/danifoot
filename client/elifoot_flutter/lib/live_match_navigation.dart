import 'package:flutter/material.dart';
import 'club_theme.dart';

/// Existing screen ids are retained; this toolbar does not bypass the live lock.
class LiveMatchNavigation extends StatelessWidget {
  final bool locked;
  final ValueChanged<int> onSelected;
  final VoidCallback? switchRoom;
  const LiveMatchNavigation({
    super.key,
    required this.locked,
    required this.onSelected,
    this.switchRoom,
  });
  @override
  Widget build(BuildContext context) {
    final t = ClassicTheme.of(context);
    const destinations = [
      (0, '1. PLANTEL & TÁTICAS'),
      (-1, '2. JOGOS AO VIVO'),
      (1, '3. DIVISÕES & TAÇA'),
      (2, '4. MERCADO & LEILÃO'),
      (3, '5. FINANÇAS & ESTÁDIO'),
      (4, '6. RANKING & CARREIRA'),
      (-2, '7. EDITOR & PACOTES'),
      (5, '8. CALENDÁRIO & RESULTADOS'),
      (6, '9. RANKINGS DE JOGADORES'),
    ];
    return Container(
      key: const Key('live-match-navigation'),
      decoration: BoxDecoration(
        color: t.secondary,
        border: Border(bottom: BorderSide(color: t.border)),
      ),
      child: SingleChildScrollView(
        scrollDirection: Axis.horizontal,
        child: Row(
          children: [
            for (final (id, label) in destinations)
              Tooltip(
                message: id == -2
                    ? 'Editor e pacotes disponíveis antes de criar o save, em Novo Campeonato.'
                    : label,
                child: Semantics(
                  button: true,
                  selected: id == -1,
                  enabled: !locked && id != -2,
                  child: InkWell(
                    key: id >= 0
                        ? ValueKey('shell-nav-$id')
                        : ValueKey('live-nav-$id'),
                    onTap: locked || id == -2 ? null : () => onSelected(id),
                    child: Container(
                      margin: const EdgeInsets.all(4),
                      padding: const EdgeInsets.symmetric(
                        horizontal: 8,
                        vertical: 10,
                      ),
                      decoration: BoxDecoration(
                        color: id == -1 ? t.accent : t.surface,
                        border: Border.all(color: t.border),
                        borderRadius: BorderRadius.circular(3),
                      ),
                      child: Text(
                        label,
                        style: TextStyle(
                          fontSize: 10,
                          fontWeight: FontWeight.bold,
                          color: id == -1
                              ? t.selectedText
                              : id == -2
                              ? t.textSecondary
                              : t.textPrimary,
                        ),
                      ),
                    ),
                  ),
                ),
              ),
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 5),
              child: OutlinedButton(
                key: const Key('live-switch-room'),
                onPressed: locked ? null : switchRoom,
                child: const Text(
                  'TROCAR SALA',
                  style: TextStyle(fontSize: 10),
                ),
              ),
            ),
          ],
        ),
      ),
    );
  }
}
