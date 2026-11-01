import 'package:flutter/material.dart';

const _paper = Color(0xFFF2F2EF),
    _ink = Color(0xFF294E7F),
    _line = Color(0xFFA5A9AC),
    _bar = Color(0xFFA5BFD6);
String _pos(dynamic value) => value is num
    ? const ['G', 'D', 'M', 'A'][value.toInt().clamp(0, 3)]
    : '${value ?? ''}';

class MatchInteractionOverlay extends StatelessWidget {
  final Widget child;
  final Widget? interaction;
  const MatchInteractionOverlay({
    super.key,
    required this.child,
    this.interaction,
  });
  @override
  Widget build(BuildContext context) => Stack(
    fit: StackFit.expand,
    children: [
      ExcludeFocus(excluding: interaction != null, child: child),
      if (interaction != null) ...[
        const Positioned.fill(
          child: ModalBarrier(dismissible: false, color: Color(0x8800160E)),
        ),
        Positioned.fill(
          child: BlockSemantics(
            child: SafeArea(child: Center(child: interaction!)),
          ),
        ),
      ],
    ],
  );
}

class ClassicMatchActionDialog extends StatelessWidget {
  final String caption;
  final Widget child;
  final double maxWidth;
  const ClassicMatchActionDialog({
    super.key,
    required this.caption,
    required this.child,
    this.maxWidth = 720,
  });
  @override
  Widget build(BuildContext context) => Dialog(
    insetPadding: const EdgeInsets.all(12),
    backgroundColor: _paper,
    shape: const RoundedRectangleBorder(),
    child: ConstrainedBox(
      constraints: BoxConstraints(
        maxWidth: maxWidth,
        maxHeight: MediaQuery.sizeOf(context).height * .90,
      ),
      child: DefaultTextStyle(
        style: const TextStyle(fontFamily: 'Roboto', fontSize: 12, color: _ink),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Container(
              color: _bar,
              padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 6),
              child: Text(
                caption,
                maxLines: 2,
                overflow: TextOverflow.ellipsis,
                style: const TextStyle(
                  color: Color(0xFF15334E),
                  fontSize: 12,
                  fontWeight: FontWeight.bold,
                ),
              ),
            ),
            Flexible(
              child: SingleChildScrollView(
                child: Padding(padding: const EdgeInsets.all(12), child: child),
              ),
            ),
          ],
        ),
      ),
    ),
  );
}

Widget _actionButton({
  Key? key,
  required String label,
  required IconData icon,
  required VoidCallback? onPressed,
}) => OutlinedButton.icon(
  key: key,
  onPressed: onPressed,
  icon: Icon(icon, size: 19),
  label: Text(
    label,
    style: const TextStyle(
      fontFamily: 'Roboto',
      fontSize: 11,
      fontWeight: FontWeight.bold,
    ),
  ),
  style: OutlinedButton.styleFrom(
    backgroundColor: Colors.white,
    foregroundColor: _ink,
    disabledForegroundColor: Colors.grey,
    side: const BorderSide(color: _line),
    padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 11),
  ),
);

class PenaltyChoiceDialog extends StatefulWidget {
  final Map<String, dynamic> penalty;
  final bool choosing, busy;
  final String? notice;
  final ValueChanged<String> onShoot;
  const PenaltyChoiceDialog({
    super.key,
    required this.penalty,
    required this.choosing,
    required this.busy,
    required this.onShoot,
    this.notice,
  });
  @override
  State<PenaltyChoiceDialog> createState() => _PenaltyChoiceDialogState();
}

class _PenaltyChoiceDialogState extends State<PenaltyChoiceDialog> {
  String? selected;
  @override
  Widget build(BuildContext context) {
    final players = (widget.penalty['eligiblePlayers'] as List? ?? [])
        .whereType<Map>()
        .toList();
    if (!widget.choosing) {
      return ClassicMatchActionDialog(
        key: const Key('penalty-waiting-dialog'),
        caption:
            '${widget.penalty['clubName']} • ${widget.penalty['minute']}\'',
        maxWidth: 450,
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const Icon(Icons.sports_soccer, size: 38, color: _ink),
            const SizedBox(height: 10),
            const Text(
              'AGUARDE',
              style: TextStyle(fontSize: 25, fontWeight: FontWeight.w900),
            ),
            const SizedBox(height: 10),
            Text(
              '${widget.penalty['coachName']} está escolhendo o cobrador do pênalti de ${widget.penalty['clubName']}.',
              textAlign: TextAlign.center,
            ),
            const SizedBox(height: 10),
            const Text(
              'O tempo de todas as partidas está parado até a escolha.',
              textAlign: TextAlign.center,
            ),
          ],
        ),
      );
    }
    final valid = players.any((p) => '${p['playerId']}' == selected);
    return ClassicMatchActionDialog(
      key: const Key('penalty-choice-dialog'),
      caption: '${widget.penalty['clubName']} • ${widget.penalty['minute']}\'',
      maxWidth: 530,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const Text(
            'PÊNALTI',
            textAlign: TextAlign.center,
            style: TextStyle(
              fontFamily: 'Roboto',
              fontSize: 36,
              letterSpacing: 5,
              fontWeight: FontWeight.w900,
            ),
          ),
          const SizedBox(height: 10),
          Text(
            '${widget.penalty['coachName']}, escolha o jogador para cobrar.',
            textAlign: TextAlign.center,
            style: const TextStyle(fontWeight: FontWeight.bold),
          ),
          const SizedBox(height: 12),
          _MatchPlayerList(
            title: 'EM CAMPO',
            players: players,
            selected: selected,
            playerKeyPrefix: 'penalty-taker',
            enabled: !widget.busy,
            onSelect: (id) => setState(() => selected = id),
          ),
          const SizedBox(height: 10),
          if (widget.notice != null)
            Text(widget.notice!, textAlign: TextAlign.center),
          Align(
            alignment: Alignment.centerRight,
            child: _actionButton(
              key: const Key('confirm-penalty-taker'),
              label: widget.busy ? 'AGUARDE' : 'CHUTAR',
              icon: Icons.sports_soccer,
              onPressed: !widget.busy && valid
                  ? () => widget.onShoot(selected!)
                  : null,
            ),
          ),
          const Text(
            'O tempo está parado. Somente jogadores em campo podem cobrar.',
            textAlign: TextAlign.center,
            style: TextStyle(fontSize: 10),
          ),
        ],
      ),
    );
  }
}

class _MatchPlayerList extends StatelessWidget {
  final String title, playerKeyPrefix;
  final List<Map> players;
  final String? selected;
  final bool enabled;
  final ValueChanged<String> onSelect;
  const _MatchPlayerList({
    required this.title,
    required this.players,
    required this.selected,
    required this.playerKeyPrefix,
    required this.enabled,
    required this.onSelect,
  });
  @override
  Widget build(BuildContext context) => Container(
    decoration: BoxDecoration(
      color: Colors.white,
      border: Border.all(color: _line),
    ),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Container(
          color: const Color(0xFFE1E7EC),
          padding: const EdgeInsets.all(6),
          child: Text(
            '$title (${players.length})',
            style: const TextStyle(fontSize: 11, fontWeight: FontWeight.bold),
          ),
        ),
        SizedBox(
          height: players.length.clamp(1, 9) * 31.0,
          child: ListView(
            children: [
              for (final p in players)
                Builder(
                  builder: (context) {
                    final id = '${p['playerId']}',
                        unavailable =
                            p['isSentOff'] == true ||
                            p['wasSubstitutedOut'] == true;
                    final injured = p['isInjuredInMatch'] == true;
                    return Semantics(
                      button: true,
                      selected: id == selected,
                      enabled: enabled && !unavailable,
                      child: InkWell(
                        key: ValueKey('$playerKeyPrefix-$id'),
                        onTap: enabled && !unavailable
                            ? () => onSelect(id)
                            : null,
                        child: Container(
                          height: 31,
                          padding: const EdgeInsets.symmetric(horizontal: 5),
                          color: id == selected
                              ? const Color(0xFFFFE16B)
                              : Colors.white,
                          child: Row(
                            children: [
                              SizedBox(
                                width: 20,
                                child: Text(
                                  _pos(p['position']),
                                  style: TextStyle(
                                    color: unavailable ? Colors.grey : _ink,
                                    fontWeight: FontWeight.bold,
                                  ),
                                ),
                              ),
                              Expanded(
                                child: Tooltip(
                                  message: '${p['displayName']}',
                                  child: Text(
                                    '${p['displayName']}',
                                    maxLines: 1,
                                    overflow: TextOverflow.ellipsis,
                                    style: TextStyle(
                                      color: unavailable ? Colors.grey : _ink,
                                      fontSize: 11,
                                      fontWeight: FontWeight.w600,
                                    ),
                                  ),
                                ),
                              ),
                              if (unavailable)
                                const Icon(
                                  Icons.block,
                                  size: 13,
                                  color: Colors.red,
                                )
                              else if (injured)
                                const Icon(
                                  Icons.medical_services,
                                  size: 13,
                                  color: Colors.red,
                                ),
                              SizedBox(
                                width: 28,
                                child: Text(
                                  '${p['strength'] ?? '—'}',
                                  textAlign: TextAlign.right,
                                  style: TextStyle(
                                    color: unavailable ? Colors.grey : _ink,
                                    fontWeight: FontWeight.bold,
                                  ),
                                ),
                              ),
                            ],
                          ),
                        ),
                      ),
                    );
                  },
                ),
            ],
          ),
        ),
      ],
    ),
  );
}

class SubstitutionChoicePanel extends StatefulWidget {
  final Map<String, dynamic> match, pause;
  final String coachId;
  final bool isHome, busy, confirmed;
  final String? notice;
  final List<String> formations;
  final Future<void> Function(String?, List<Map<String, String>>) onApply;
  final VoidCallback onContinue;
  const SubstitutionChoicePanel({
    super.key,
    required this.match,
    required this.pause,
    required this.coachId,
    required this.isHome,
    required this.busy,
    required this.confirmed,
    required this.formations,
    required this.onApply,
    required this.onContinue,
    this.notice,
  });
  @override
  State<SubstitutionChoicePanel> createState() =>
      _SubstitutionChoicePanelState();
}

class _SubstitutionChoicePanelState extends State<SubstitutionChoicePanel> {
  String? outId, inId, formation;
  String get current =>
      '${widget.match[widget.isHome ? 'homeFormation' : 'awayFormation'] ?? '4-4-2'}';
  @override
  void initState() {
    super.initState();
    formation = current;
  }

  @override
  void didUpdateWidget(covariant SubstitutionChoicePanel old) {
    super.didUpdateWidget(old);
    if (old.match[old.isHome ? 'homeFormation' : 'awayFormation'] != current) {
      formation = current;
    }
  }

  @override
  Widget build(BuildContext context) {
    final side = widget.isHome ? 'home' : 'away';
    final starters = (widget.match['${side}Starters'] as List? ?? [])
            .whereType<Map>()
            .toList(),
        bench = (widget.match['${side}Bench'] as List? ?? [])
            .whereType<Map>()
            .toList();
    final used = (widget.match['${side}SubCount'] as num? ?? 0).toInt(),
        max = (widget.match['${side}MaxSubs'] as num? ?? 5).toInt();
    final wins = widget.match['${side}PauseWindowsUsed'] ?? 0,
        maxWins = widget.match['${side}MaxPauseWindows'] ?? 3;
    final free = widget.pause['pauseType'] != 'GLOBAL_PAUSED';
    final hasWindow =
        free ||
        (wins as num) < (maxWins as num) ||
        ((widget.pause['editedCoachIds'] as List? ?? []).contains(
          widget.coachId,
        ));
    final active = !widget.busy && !widget.confirmed;
    final validOut =
        starters.any(
          (p) =>
              '${p['playerId']}' == outId &&
              p['isSentOff'] != true &&
              p['wasSubstitutedOut'] != true,
        )
        ? outId
        : null;
    final validIn =
        bench.any(
          (p) =>
              '${p['playerId']}' == inId &&
              p['isSentOff'] != true &&
              p['isInjuredInMatch'] != true,
        )
        ? inId
        : null;
    final pair = validOut != null && validIn != null,
        partial = (validOut != null) != (validIn != null),
        changed = formation != current;
    return Column(
      key: const ValueKey('in-match-substitution-panel'),
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(
          '${widget.match['homeClubName']} ${widget.match['homeGoals']} x ${widget.match['awayGoals']} ${widget.match['awayClubName']}',
          style: const TextStyle(fontSize: 15, fontWeight: FontWeight.bold),
        ),
        const SizedBox(height: 7),
        Wrap(
          spacing: 12,
          runSpacing: 4,
          children: [
            Text(
              'SUBSTITUIÇÕES $used/$max (RESTANTES: ${max - used})',
              style: const TextStyle(fontWeight: FontWeight.bold),
            ),
            Text(
              'PARADAS $wins/$maxWins',
              style: const TextStyle(fontWeight: FontWeight.bold),
            ),
          ],
        ),
        const SizedBox(height: 5),
        Text(
          free
              ? 'INTERVALO — alterações não gastam parada.'
              : 'Abrir ou observar não gasta parada. Só uma alteração confirmada gasta uma parada nesta janela.',
          style: const TextStyle(fontSize: 11),
        ),
        if (!hasWindow)
          const Text(
            'Limite de paradas atingido. Você pode observar e continuar.',
            style: TextStyle(color: Colors.red, fontSize: 11),
          ),
        const SizedBox(height: 8),
        LayoutBuilder(
          builder: (context, c) {
            final left = _MatchPlayerList(
              title: 'EM CAMPO — SAI',
              players: starters,
              selected: validOut,
              playerKeyPrefix: 'sub-select-out',
              enabled: active && hasWindow && used < max,
              onSelect: (id) => setState(() => outId = id),
            );
            final right = _MatchPlayerList(
              title: 'BANCO — ENTRA',
              players: bench,
              selected: validIn,
              playerKeyPrefix: 'sub-select-in',
              enabled: active && hasWindow && used < max,
              onSelect: (id) => setState(() => inId = id),
            );
            return c.maxWidth >= 500
                ? Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Expanded(child: left),
                      const SizedBox(width: 10),
                      Expanded(child: right),
                    ],
                  )
                : Column(children: [left, const SizedBox(height: 8), right]);
          },
        ),
        const SizedBox(height: 7),
        Wrap(
          spacing: 12,
          runSpacing: 7,
          crossAxisAlignment: WrapCrossAlignment.center,
          children: [
            DropdownButton<String>(
              key: const ValueKey('in-match-formation-dropdown'),
              value: widget.formations.contains(formation)
                  ? formation
                  : '4-4-2',
              dropdownColor: Colors.white,
              style: const TextStyle(
                fontFamily: 'Roboto',
                color: _ink,
                fontSize: 12,
              ),
              iconEnabledColor: _ink,
              items: widget.formations
                  .map(
                    (f) =>
                        DropdownMenuItem(value: f, child: Text('Formação $f')),
                  )
                  .toList(),
              onChanged: active && hasWindow
                  ? (f) => setState(() => formation = f)
                  : null,
            ),
            _actionButton(
              key: const ValueKey('btn-aplicar-ajuste-janela'),
              label: pair ? 'SUBSTITUIR' : 'APLICAR TÁTICA',
              icon: Icons.swap_horiz,
              onPressed:
                  active &&
                      hasWindow &&
                      !partial &&
                      (pair || changed) &&
                      (!pair || used < max)
                  ? () async {
                      await widget.onApply(
                        changed ? formation : null,
                        pair
                            ? [
                                {
                                  'playerOutId': validOut,
                                  'playerInId': validIn,
                                },
                              ]
                            : [],
                      );
                      if (mounted) {
                        setState(() {
                          outId = null;
                          inId = null;
                        });
                      }
                    }
                  : null,
            ),
            _actionButton(
              key: const Key('btn-confirmar-continuar'),
              label: widget.confirmed ? 'AGUARDANDO' : 'CONTINUAR',
              icon: Icons.check,
              onPressed: active ? widget.onContinue : null,
            ),
          ],
        ),
        if (widget.notice != null)
          Padding(
            padding: const EdgeInsets.only(top: 7),
            child: Text(widget.notice!, style: const TextStyle(fontSize: 11)),
          ),
        if (widget.confirmed)
          const Padding(
            padding: EdgeInsets.only(top: 7),
            child: Text(
              'Aguarde os outros treinadores confirmarem para retomar o jogo.',
              style: TextStyle(fontWeight: FontWeight.bold),
            ),
          ),
      ],
    );
  }
}

class PauseWaitingContent extends StatelessWidget {
  final Map<String, dynamic> pause;
  final bool confirmed, busy;
  final VoidCallback onContinue;
  const PauseWaitingContent({
    super.key,
    required this.pause,
    required this.confirmed,
    required this.busy,
    required this.onContinue,
  });
  @override
  Widget build(BuildContext context) => Column(
    mainAxisSize: MainAxisSize.min,
    children: [
      const Text(
        'AGUARDE',
        style: TextStyle(fontWeight: FontWeight.bold, fontSize: 24),
      ),
      const SizedBox(height: 10),
      Text(
        'Janela compartilhada de substituições. ${pause['requestedByCoachName'] ?? 'Os treinadores'} estão preparando alterações.',
        textAlign: TextAlign.center,
      ),
      const SizedBox(height: 10),
      _actionButton(
        key: const Key('btn-confirmar-continuar'),
        label: confirmed ? 'AGUARDANDO' : 'CONTINUAR',
        icon: Icons.check,
        onPressed: confirmed || busy ? null : onContinue,
      ),
    ],
  );
}
