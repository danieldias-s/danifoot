import 'dart:async';

import 'package:flutter/material.dart';

import 'classic98_widgets.dart';
import 'squad_classic.dart' show squadPosition;

const _navy = Color(0xFF0D2460);
const _green = Color(0xFF064631);
const _paper = Color(0xFFF1E8CE);

class ClassicPenaltyDialog extends StatefulWidget {
  final Map<String, dynamic> penalty;
  final bool choosing, busy;
  final String? notice;
  final ValueChanged<String> onShoot;

  const ClassicPenaltyDialog({
    super.key,
    required this.penalty,
    required this.choosing,
    required this.busy,
    this.notice,
    required this.onShoot,
  });

  @override
  State<ClassicPenaltyDialog> createState() => _ClassicPenaltyDialogState();
}

class _ClassicPenaltyDialogState extends State<ClassicPenaltyDialog> {
  String? _selected;
  bool _announced = false;

  @override
  Widget build(BuildContext context) {
    final players = (widget.penalty['eligiblePlayers'] as List? ?? [])
        .whereType<Map>();
    final selected = players
        .where((p) => p['playerId'] == _selected)
        .firstOrNull;
    final announced = _announced && selected != null;
    final canChoose = widget.choosing;
    return _PenaltyWindow(
      key: Key(
        canChoose
            ? announced
                  ? 'penalty-taker-dialog'
                  : 'penalty-choice-dialog'
            : 'penalty-waiting-dialog',
      ),
      caption: _caption(widget.penalty),
      title: canChoose ? 'PÊNALTI' : 'AGUARDE',
      message: announced
          ? '${selected['displayName']} vai cobrar.'
          : canChoose
          ? '${widget.penalty['coachName']}, escolha o jogador para cobrar.'
          : '${widget.penalty['coachName']} está escolhendo o cobrador de ${widget.penalty['clubName']}.',
      children: [
        if (canChoose)
          _PenaltyPanel(
            title: announced
                ? 'COBRADOR ESCOLHIDO'
                : 'EM CAMPO (${players.length})',
            child: Column(
              children: [
                for (final player in announced ? [selected] : players)
                  _PlayerLine(
                    player: player,
                    selected: !announced && player == selected,
                    onTap: announced || widget.busy
                        ? null
                        : () => setState(
                            () => _selected = player['playerId'].toString(),
                          ),
                  ),
              ],
            ),
          ),
        if (widget.notice != null)
          Padding(
            padding: const EdgeInsets.only(top: 10),
            child: Text(widget.notice!),
          ),
        const Padding(
          padding: EdgeInsets.only(top: 12),
          child: Text(
            'O tempo está parado. Somente jogadores em campo podem cobrar.',
          ),
        ),
        if (canChoose)
          _Controls(
            children: [
              if (announced)
                _button(
                  'VOLTAR',
                  widget.busy ? null : () => setState(() => _announced = false),
                ),
              _button(
                announced ? 'CHUTAR' : 'ESCOLHER',
                widget.busy || selected == null
                    ? null
                    : () {
                        if (announced) {
                          widget.onShoot(_selected!);
                        } else {
                          setState(() => _announced = true);
                        }
                      },
                key: Key(
                  announced
                      ? 'confirm-penalty-taker'
                      : 'announce-penalty-taker',
                ),
              ),
            ],
          ),
      ],
    );
  }
}

class ClassicPenaltyResult extends StatefulWidget {
  final Map<String, dynamic> event, match;
  final bool isOwnKick;
  final VoidCallback onDone;

  const ClassicPenaltyResult({
    super.key,
    required this.event,
    required this.match,
    required this.isOwnKick,
    required this.onDone,
  });

  @override
  State<ClassicPenaltyResult> createState() => _ClassicPenaltyResultState();
}

class _ClassicPenaltyResultState extends State<ClassicPenaltyResult> {
  Timer? _timer;
  late int _stage = widget.isOwnKick
      ? 2
      : widget.event['penaltyKick']['decisionId'] == null
      ? 0
      : 1;

  @override
  void initState() {
    super.initState();
    _advance();
  }

  void _advance() {
    if (_stage == 2) return;
    _timer = Timer(Duration(milliseconds: _stage == 0 ? 700 : 1450), () {
      setState(() => _stage++);
      _advance();
    });
  }

  @override
  void dispose() {
    _timer?.cancel();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final kick = widget.event['penaltyKick'] as Map;
    final taker = kick['taker'] as Map;
    final name = taker['displayName'];
    final outcome = kick['outcome'];
    final goal = outcome == 'Goal';
    final saved = outcome == 'Saved';
    final keeper = widget.event['secondaryPlayerDisplayName'] ?? 'O goleiro';
    return _PenaltyWindow(
      key: Key(switch (_stage) {
        0 => 'penalty-waiting-dialog',
        1 => 'penalty-taker-dialog',
        _ => 'penalty-result-dialog',
      }),
      caption: _caption(widget.event),
      title: switch (_stage) {
        0 => 'AGUARDE',
        1 => 'PÊNALTI',
        _ =>
          goal
              ? 'GOL!'
              : saved
              ? 'DEFENDEU!'
              : 'PRA FORA!',
      },
      message: switch (_stage) {
        0 => 'A máquina vai cobrar o pênalti de ${widget.event['clubName']}.',
        1 => '$name vai cobrar.',
        _ =>
          goal
              ? '$name converteu o pênalti.'
              : saved
              ? '$keeper defendeu a cobrança de $name.'
              : '$name mandou para fora.',
      },
      children: [
        if (_stage == 1)
          _PenaltyPanel(
            title: 'COBRADOR ESCOLHIDO',
            child: _PlayerLine(player: taker),
          ),
        if (_stage == 2) ...[
          if (!goal)
            Center(
              child: SizedBox(
                width: 230,
                height: 125,
                child: CustomPaint(painter: _PenaltyScene(saved)),
              ),
            ),
          const SizedBox(height: 10),
          _PenaltyPanel(
            title: 'PLACAR',
            child: Row(
              children: [
                Expanded(child: _teamName(widget.match['homeClubName'])),
                Container(
                  color: _green,
                  padding: const EdgeInsets.symmetric(
                    horizontal: 14,
                    vertical: 8,
                  ),
                  child: Text(
                    '${kick['homeGoals']} x ${kick['awayGoals']}',
                    style: const TextStyle(
                      color: _paper,
                      fontSize: 26,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                ),
                Expanded(child: _teamName(widget.match['awayClubName'])),
              ],
            ),
          ),
          _Controls(
            children: [
              _button(
                'OK',
                widget.onDone,
                key: const Key('acknowledge-penalty-result'),
              ),
            ],
          ),
        ],
      ],
    );
  }
}

Widget _teamName(dynamic name) => Padding(
  padding: const EdgeInsets.all(6),
  child: Text(
    '${name ?? '—'}',
    textAlign: TextAlign.center,
    style: const TextStyle(fontWeight: FontWeight.bold),
  ),
);

String _caption(Map data) => '${data['clubName']} • ${data['minute']}′';

Widget _button(String label, VoidCallback? action, {Key? key}) => RetroButton(
  key: key,
  label: label,
  onPressed: action,
  icon: label == 'VOLTAR' ? Icons.arrow_back : Icons.sports_soccer,
  backgroundColor: ClassicColors.retroGray,
  foregroundColor: _navy,
);

class _Controls extends StatelessWidget {
  final List<Widget> children;
  const _Controls({required this.children});

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.only(top: 12),
    child: Align(
      alignment: Alignment.centerRight,
      child: Wrap(spacing: 8, runSpacing: 8, children: children),
    ),
  );
}

class _PenaltyWindow extends StatelessWidget {
  final String caption, title, message;
  final List<Widget> children;
  const _PenaltyWindow({
    super.key,
    required this.caption,
    required this.title,
    required this.message,
    required this.children,
  });

  @override
  Widget build(BuildContext context) => Dialog(
    insetPadding: const EdgeInsets.all(12),
    backgroundColor: _paper,
    shape: const RoundedRectangleBorder(),
    child: ConstrainedBox(
      constraints: const BoxConstraints(maxWidth: 670),
      child: RetroBevel(
        color: _paper,
        padding: const EdgeInsets.all(4),
        child: DefaultTextStyle(
          style: const TextStyle(
            color: _navy,
            fontFamily: 'ClassicCondensed',
            fontSize: 17,
          ),
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              RetroBevel(
                color: _navy,
                padding: const EdgeInsets.symmetric(
                  horizontal: 10,
                  vertical: 4,
                ),
                child: Text(
                  caption,
                  style: const TextStyle(
                    color: Colors.white,
                    fontSize: 20,
                    fontWeight: FontWeight.bold,
                  ),
                ),
              ),
              Flexible(
                child: SingleChildScrollView(
                  child: Padding(
                    padding: const EdgeInsets.all(14),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        Row(
                          children: [
                            const Expanded(child: _TitleDecoration()),
                            const SizedBox(width: 8),
                            Flexible(
                              flex: 5,
                              child: FittedBox(
                                fit: BoxFit.scaleDown,
                                child: Text(
                                  title,
                                  style: const TextStyle(
                                    fontSize: 54,
                                    letterSpacing: 2,
                                    fontWeight: FontWeight.w900,
                                    height: 1,
                                  ),
                                ),
                              ),
                            ),
                            const SizedBox(width: 8),
                            const Expanded(child: _TitleDecoration()),
                          ],
                        ),
                        const SizedBox(height: 8),
                        Text(
                          message,
                          textAlign: TextAlign.center,
                          style: const TextStyle(
                            fontSize: 21,
                            fontWeight: FontWeight.bold,
                          ),
                        ),
                        const SizedBox(height: 12),
                        ...children,
                      ],
                    ),
                  ),
                ),
              ),
            ],
          ),
        ),
      ),
    ),
  );
}

class _TitleDecoration extends StatelessWidget {
  const _TitleDecoration();

  @override
  Widget build(BuildContext context) => Row(
    children: [
      const Expanded(child: Divider(color: _green, thickness: 3)),
      Icon(
        Icons.sports_soccer,
        color: _green,
        size: MediaQuery.sizeOf(context).width < 420 ? 16 : 27,
      ),
      const Expanded(child: Divider(color: _green, thickness: 3)),
    ],
  );
}

class _PenaltyPanel extends StatelessWidget {
  final String title;
  final Widget child;
  const _PenaltyPanel({required this.title, required this.child});

  @override
  Widget build(BuildContext context) => RetroBevel(
    color: _paper,
    sunken: true,
    padding: const EdgeInsets.all(2),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Container(
          color: _green,
          padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 4),
          child: Text(
            title,
            style: const TextStyle(
              color: _paper,
              fontSize: 21,
              fontWeight: FontWeight.bold,
            ),
          ),
        ),
        child,
      ],
    ),
  );
}

class _PlayerLine extends StatelessWidget {
  final Map player;
  final bool selected;
  final VoidCallback? onTap;
  const _PlayerLine({required this.player, this.selected = false, this.onTap});

  @override
  Widget build(BuildContext context) {
    final ink = selected ? Colors.white : _navy;
    return Material(
      color: selected ? _navy : _paper,
      child: InkWell(
        key: onTap == null
            ? null
            : ValueKey('penalty-taker-${player['playerId']}'),
        onTap: onTap,
        child: Semantics(
          selected: selected,
          child: Container(
            padding: const EdgeInsets.symmetric(horizontal: 7, vertical: 4),
            decoration: const BoxDecoration(
              border: Border(bottom: BorderSide(color: ClassicColors.border)),
            ),
            child: DefaultTextStyle(
              style: TextStyle(
                color: ink,
                fontSize: 20,
                fontWeight: FontWeight.bold,
                fontFamily: 'ClassicCondensed',
              ),
              child: Row(
                children: [
                  if (selected)
                    const Icon(
                      Icons.arrow_right,
                      color: Color(0xFFFF7474),
                      size: 16,
                    ),
                  SizedBox(
                    width: selected ? 22 : 38,
                    child: Text(
                      squadPosition(Map<String, dynamic>.from(player)),
                      textAlign: TextAlign.center,
                    ),
                  ),
                  const SizedBox(width: 8),
                  Expanded(child: Text(formatClassicPlayerName(player))),
                  const SizedBox(width: 8),
                  Text('${player['strength']}'),
                ],
              ),
            ),
          ),
        ),
      ),
    );
  }
}

class _PenaltyScene extends CustomPainter {
  final bool saved;
  const _PenaltyScene(this.saved);

  @override
  void paint(Canvas canvas, Size size) {
    canvas.scale(size.width / 230, size.height / 125);
    canvas.drawRect(
      const Rect.fromLTWH(0, 0, 230, 125),
      Paint()..color = _green,
    );
    canvas.drawRect(
      const Rect.fromLTWH(0, 105, 230, 20),
      Paint()..color = const Color(0xFF51763C),
    );
    final net = Paint()..color = const Color(0x887CD29A);
    final line = Paint()
      ..color = _paper
      ..strokeWidth = 3;
    final right = saved ? 214.0 : 131.0;
    for (double x = 14; x < right; x += 10) {
      canvas.drawLine(Offset(x, 17), Offset(x, 108), net);
    }
    for (double y = 17; y < 109; y += 9) {
      canvas.drawLine(Offset(14, y), Offset(right, y), net);
    }
    canvas.drawLine(const Offset(14, 108), const Offset(14, 17), line);
    canvas.drawLine(const Offset(14, 17), Offset(right, 17), line);
    canvas.drawLine(Offset(right, 17), Offset(right, 108), line);
    final ball = saved ? const Offset(181, 43) : const Offset(210, 38);
    if (saved) {
      final kit = Paint()
        ..color = const Color(0xFFB6D88C)
        ..strokeWidth = 8;
      final shorts = Paint()
        ..color = _navy
        ..strokeWidth = 8;
      canvas.drawLine(const Offset(108, 88), const Offset(143, 62), kit);
      canvas.drawLine(const Offset(143, 62), const Offset(174, 45), kit);
      canvas.drawLine(const Offset(113, 84), const Offset(78, 103), shorts);
      canvas.drawLine(const Offset(111, 88), const Offset(86, 115), shorts);
      canvas.drawCircle(const Offset(149, 54), 7, Paint()..color = _paper);
    } else {
      for (var i = 0; i < 8; i++) {
        canvas.drawRect(Rect.fromLTWH(137 + i * 9, 77 - i * 5, 4, 4), line);
      }
    }
    canvas.drawCircle(ball, 10, Paint()..color = _paper);
    canvas.drawCircle(ball, 4, Paint()..color = _navy);
  }

  @override
  bool shouldRepaint(covariant _PenaltyScene oldDelegate) =>
      saved != oldDelegate.saved;
}
