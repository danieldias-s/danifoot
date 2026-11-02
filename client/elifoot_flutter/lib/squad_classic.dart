import 'dart:math' as math;
import 'package:flutter/material.dart';
import 'classic_layout.dart';
import 'club_theme.dart';
import 'player_rankings.dart' show formatClassicMoney;
import 'tactical_formation.dart';
import 'visual_components.dart';

String squadPosition(Map<String, dynamic> player) {
  final raw = player['position'];
  return raw is num
      ? const ['G', 'D', 'M', 'A'][raw.toInt().clamp(0, 3)]
      : raw?.toString() ?? '—';
}

String squadPlayerName(Map<String, dynamic> player) =>
    '${player['displayName'] ?? player['name'] ?? '—'}';

String squadCondition(Map<String, dynamic> p) =>
    (p['injuryDaysRemaining'] as num? ?? 0) > 0
    ? 'LES ${p['injuryDaysRemaining']}d'
    : (p['injuryRoundsRemaining'] as num? ?? 0) > 0
    ? 'LES ${p['injuryRoundsRemaining']}r'
    : (p['suspensionRoundsRemaining'] as num? ?? 0) > 0
    ? 'SUS ${p['suspensionRoundsRemaining']}r'
    : 'APTO';

Color squadPositionColor(String position) => switch (position) {
  'G' => const Color(0xFFAF6100),
  'D' => const Color(0xFF087825),
  'M' => const Color(0xFF0858C7),
  'A' => const Color(0xFFB61922),
  _ => const Color(0xFF47604F),
};

/// A paper surface with the same green section headings on both form factors.
class SquadPaperPanel extends StatelessWidget {
  final String title;
  final Widget child;
  final Widget? trailing;
  final EdgeInsetsGeometry padding;
  const SquadPaperPanel({
    super.key,
    required this.title,
    required this.child,
    this.trailing,
    this.padding = const EdgeInsets.all(6),
  });
  @override
  Widget build(BuildContext context) {
    final t = ClassicTheme.of(context);
    return Container(
      padding: const EdgeInsets.all(5),
      decoration: BoxDecoration(
        color: t.paper.surface,
        border: Border.all(color: t.paper.border),
        borderRadius: BorderRadius.circular(5),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Container(
            constraints: const BoxConstraints(minHeight: 40),
            padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
            decoration: BoxDecoration(
              gradient: t.panelGradient,
              borderRadius: BorderRadius.circular(3),
            ),
            child: Row(
              children: [
                Expanded(
                  child: Text(
                    title,
                    style: TextStyle(
                      color: t.textPrimary,
                      fontWeight: FontWeight.w700,
                      fontSize: 20,
                      height: 1.05,
                    ),
                  ),
                ),
                if (trailing != null) ...[
                  const SizedBox(width: 6),
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 7),
                    decoration: BoxDecoration(
                      color: t.paper.surface,
                      borderRadius: BorderRadius.circular(3),
                    ),
                    child: ClassicPaper(builder: (_) => trailing!),
                  ),
                ],
              ],
            ),
          ),
          ClassicPaper(
            builder: (_) => Padding(padding: padding, child: child),
          ),
        ],
      ),
    );
  }
}

/// Real salary and market value are independent server fields.
/// Fixed and flex columns keep the actions usable without an oversized table.
class ClassicSquadRoster extends StatelessWidget {
  final List<Map<String, dynamic>> players;
  final List<String> starters, bench;
  final bool compact, locked, busy, canManage;
  final ValueChanged<Map<String, dynamic>> onToggle,
      onDetail,
      onRenew,
      onAuction;
  const ClassicSquadRoster({
    super.key,
    required this.players,
    required this.starters,
    required this.bench,
    required this.compact,
    required this.locked,
    required this.busy,
    required this.canManage,
    required this.onToggle,
    required this.onDetail,
    required this.onRenew,
    required this.onAuction,
  });
  @override
  Widget build(BuildContext context) => LayoutBuilder(
    builder: (context, c) {
      final t = ClassicTheme.of(context);
      final tight = !compact && c.maxWidth < 640;
      final width = math.max(c.maxWidth, compact ? 330.0 : 520.0);
      final specs = <(String, double?)>[
        (
          'STATUS',
          compact
              ? 44
              : tight
              ? 46
              : 50,
        ),
        ('POS', compact || tight ? 20 : 24),
        ('JOGADOR', null),
        ('FORÇA', compact || tight ? 28 : 34),
        if (!compact) ('COMP.', tight ? 44 : 52),
        ('COND.', compact || tight ? 34 : 44),
        ('SALÁRIO', compact || tight ? 58 : 64),
        if (!compact) ('VALOR JUSTO', tight ? 68 : 74),
        ('AÇÕES', 88),
      ];
      Widget text(String value, {Color? color, bool bold = false}) => Tooltip(
        message: value,
        child: Text(
          value,
          maxLines: 1,
          overflow: TextOverflow.ellipsis,
          style: TextStyle(
            color: color ?? t.textPrimary,
            fontSize: compact ? 13 : 14,
            fontWeight: bold ? FontWeight.w700 : FontWeight.w400,
          ),
        ),
      );
      Widget action({
        required IconData icon,
        required String tooltip,
        required VoidCallback? onPressed,
        Key? key,
        bool sale = false,
      }) => SizedBox(
        width: 28,
        height: 36,
        child: Container(
          margin: const EdgeInsets.symmetric(vertical: 3),
          decoration: BoxDecoration(
            color: sale && onPressed != null
                ? const Color(0xFF087825)
                : t.surfaceAlternate,
            border: Border.all(color: t.border),
            borderRadius: BorderRadius.circular(3),
          ),
          child: IconButton(
            key: key,
            tooltip: tooltip,
            onPressed: onPressed,
            color: sale ? Colors.white : t.textPrimary,
            disabledColor: t.textSecondary,
            padding: EdgeInsets.zero,
            icon: Icon(icon, size: 19),
          ),
        ),
      );
      return SingleChildScrollView(
        scrollDirection: Axis.horizontal,
        child: SizedBox(
          width: width,
          child: DataTable(
            key: const ValueKey('squad-table'),
            showCheckboxColumn: false,
            horizontalMargin: 2,
            columnSpacing: 4,
            headingRowHeight: 32,
            dataRowMinHeight: 42,
            dataRowMaxHeight: 42,
            dividerThickness: .5,
            headingRowColor: WidgetStatePropertyAll(t.surfaceAlternate),
            headingTextStyle: TextStyle(
              color: t.textPrimary,
              fontFamily: 'ClassicCondensed',
              fontSize: compact ? 12 : 13,
              fontWeight: FontWeight.w700,
            ),
            columns: [
              for (final spec in specs)
                DataColumn(
                  columnWidth: spec.$2 == null
                      ? const FlexColumnWidth()
                      : FixedColumnWidth(spec.$2!),
                  label: Expanded(
                    child: Text(
                      spec.$1,
                      maxLines: 1,
                      overflow: TextOverflow.ellipsis,
                    ),
                  ),
                ),
            ],
            rows: [
              for (int i = 0; i < players.length; i++)
                DataRow(
                  color: WidgetStatePropertyAll(
                    i.isEven ? t.surface : t.surfaceAlternate,
                  ),
                  cells: [
                    DataCell(
                      Tooltip(
                        message: 'Alternar titular, reserva ou fora',
                        child: InkWell(
                          key: ValueKey('toggle-role-${players[i]['id']}'),
                          onTap: locked || squadCondition(players[i]) != 'APTO'
                              ? null
                              : () => onToggle(players[i]),
                          child: Container(
                            padding: const EdgeInsets.symmetric(
                              horizontal: 3,
                              vertical: 5,
                            ),
                            decoration: BoxDecoration(
                              color: starters.contains('${players[i]['id']}')
                                  ? const Color(0xFF087825)
                                  : bench.contains('${players[i]['id']}')
                                  ? const Color(0xFF0858C7)
                                  : const Color(0xFF576D5C),
                              borderRadius: BorderRadius.circular(3),
                            ),
                            child: FittedBox(
                              fit: BoxFit.scaleDown,
                              child: Text(
                                starters.contains('${players[i]['id']}')
                                    ? 'TITULAR'
                                    : bench.contains('${players[i]['id']}')
                                    ? 'RESERVA'
                                    : 'FORA',
                                style: const TextStyle(
                                  color: Colors.white,
                                  fontSize: 12,
                                  fontWeight: FontWeight.w700,
                                ),
                              ),
                            ),
                          ),
                        ),
                      ),
                    ),
                    DataCell(text(squadPosition(players[i]), bold: true)),
                    DataCell(
                      InkWell(
                        onTap: () => onDetail(players[i]),
                        child: Tooltip(
                          message: squadPlayerName(players[i]),
                          child: text(
                            compact || tight
                                ? squadPlayerName(
                                    players[i],
                                  ).replaceFirst('Atleta fictício ', '')
                                : squadPlayerName(players[i]),
                            bold: squadPlayerName(players[i]).endsWith('*'),
                          ),
                        ),
                      ),
                    ),
                    DataCell(
                      text('${players[i]['strength'] ?? '—'}', bold: true),
                    ),
                    if (!compact)
                      DataCell(text('${players[i]['behaviorDisplay'] ?? '—'}')),
                    DataCell(
                      text(
                        squadCondition(players[i]),
                        color: squadCondition(players[i]) == 'APTO'
                            ? const Color(0xFF087825)
                            : const Color(0xFFB82F1B),
                        bold: true,
                      ),
                    ),
                    DataCell(
                      text(formatClassicMoney(players[i]['salaryPerRound'])),
                    ),
                    if (!compact)
                      DataCell(
                        text(formatClassicMoney(players[i]['fairValue'])),
                      ),
                    DataCell(
                      Row(
                        children: [
                          action(
                            icon: Icons.edit_outlined,
                            tooltip: 'Renovar contrato',
                            onPressed: canManage && !busy
                                ? () => onRenew(players[i])
                                : null,
                          ),
                          action(
                            icon: Icons.bar_chart,
                            tooltip: 'Ficha e estatísticas',
                            onPressed: () => onDetail(players[i]),
                          ),
                          action(
                            key: ValueKey('create-auction-${players[i]['id']}'),
                            icon: Icons.attach_money,
                            tooltip: 'Colocar em leilão',
                            sale: true,
                            onPressed: canManage && !busy
                                ? () => onAuction(players[i])
                                : null,
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
            ],
          ),
        ),
      );
    },
  );
}

class ClassicSquadTactics extends StatelessWidget {
  final List<Map<String, dynamic>> squad;
  final List<String> starters, bench, formations;
  final String formation;
  final bool locked;
  final int requiredStarters;
  final Color shirt;
  final ValueChanged<String> onFormation;
  final VoidCallback onAutoPick;
  final ValueChanged<Map<String, dynamic>> onPlayer;
  final void Function(String, String) onSwap;
  const ClassicSquadTactics({
    super.key,
    required this.squad,
    required this.starters,
    required this.bench,
    required this.formations,
    required this.formation,
    required this.locked,
    required this.requiredStarters,
    required this.shirt,
    required this.onFormation,
    required this.onAutoPick,
    required this.onPlayer,
    required this.onSwap,
  });
  @override
  Widget build(BuildContext context) {
    final t = ClassicTheme.of(context);
    final selected = squad
        .where((p) => starters.contains('${p['id']}'))
        .toList();
    final reserves = squad
        .where(
          (p) => bench.contains('${p['id']}') && squadCondition(p) == 'APTO',
        )
        .toList();
    final normalized = selected
        .map((p) => {...p, 'position': squadPosition(p)})
        .toList();
    final lines = tacticalPitchLines(normalized, formation);
    final counts = [1, ...formation.split('-').map(int.parse)];
    final sectorsMatch =
        selected.length == requiredStarters &&
        List.generate(
          4,
          (i) =>
              selected
                  .where((p) => squadPosition(p) == ['G', 'D', 'M', 'A'][i])
                  .length ==
              counts[i],
        ).every((v) => v);
    final status = sectorsMatch
        ? 'SETORES COMPATÍVEIS [OK]'
        : selected.length < 11 && selected.length == requiredStarters
        ? 'EM INFERIORIDADE (${selected.length}/11 APTOS)'
        : selected.length < requiredStarters
        ? 'ESCALAÇÃO INCOMPLETA (${selected.length}/11)'
        : 'DESVIO SETORIAL';
    return Column(
      key: const ValueKey('tactical-field-preview'),
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Container(
          padding: const EdgeInsets.all(7),
          decoration: BoxDecoration(border: Border.all(color: t.border)),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const Text(
                'ESQUEMA TÁTICO',
                style: TextStyle(fontWeight: FontWeight.w700, fontSize: 18),
              ),
              const SizedBox(height: 4),
              Wrap(
                spacing: 8,
                runSpacing: 6,
                crossAxisAlignment: WrapCrossAlignment.center,
                children: [
                  Container(
                    width: 128,
                    height: 36,
                    padding: const EdgeInsets.symmetric(horizontal: 8),
                    decoration: BoxDecoration(
                      color: const Color(0xFF003D29),
                      borderRadius: BorderRadius.circular(3),
                    ),
                    child: DropdownButtonHideUnderline(
                      child: DropdownButton<String>(
                        key: const ValueKey('dropdown-formation'),
                        value: formation,
                        selectedItemBuilder: (_) => [
                          for (final f in formations)
                            Align(
                              alignment: Alignment.centerLeft,
                              child: Text(
                                f,
                                style: const TextStyle(
                                  color: Colors.white,
                                  fontFamily: 'ClassicCondensed',
                                  fontSize: 20,
                                  fontWeight: FontWeight.w700,
                                ),
                              ),
                            ),
                        ],
                        disabledHint: Text(
                          formation,
                          style: const TextStyle(
                            color: Colors.white70,
                            fontFamily: 'ClassicCondensed',
                            fontSize: 20,
                            fontWeight: FontWeight.w700,
                          ),
                        ),
                        isExpanded: true,
                        dropdownColor: const Color(0xFF003D29),
                        style: const TextStyle(
                          color: Colors.white,
                          fontFamily: 'ClassicCondensed',
                          fontSize: 20,
                          fontWeight: FontWeight.w700,
                        ),
                        icon: const Icon(
                          Icons.arrow_drop_down,
                          color: Colors.white,
                        ),
                        items: [
                          for (final f in formations)
                            DropdownMenuItem(value: f, child: Text(f)),
                        ],
                        onChanged: locked
                            ? null
                            : (v) {
                                if (v != null) onFormation(v);
                              },
                      ),
                    ),
                  ),
                  Text(
                    'Titulares: ${starters.length}/11  |  Banco: ${bench.length}/9  |  Elenco: ${squad.length}/24',
                    style: const TextStyle(
                      fontWeight: FontWeight.w700,
                      fontSize: 14,
                    ),
                  ),
                  OutlinedButton(
                    key: const ValueKey('btn-auto-escalar'),
                    onPressed: locked ? null : onAutoPick,
                    child: const Text('AUTO-ESCALAR 11 + BANCO'),
                  ),
                ],
              ),
              Text(
                status,
                key: const Key('squad-sector-status'),
                style: TextStyle(
                  fontSize: 12,
                  color: sectorsMatch
                      ? const Color(0xFF087825)
                      : const Color(0xFF9B5010),
                ),
              ),
            ],
          ),
        ),
        const SizedBox(height: 6),
        LayoutBuilder(
          builder: (context, c) {
            final pitch = ClassicTacticalPitch(
              lines: lines,
              shirt: shirt,
              ink: Colors.white,
              onPlayer: onPlayer,
              aspectRatio: 1.15,
            );
            Widget sectorList() => Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                for (int i = 0; i < lines.length; i++) ...[
                  Text(
                    '${['ATAQUE', 'MEIO-CAMPO', 'DEFESA', 'GOLEIRO'][i]} (${lines[i].length}/${counts.reversed.toList()[i]})',
                    style: const TextStyle(
                      fontSize: 14,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                  for (final p in lines[i])
                    InkWell(
                      onTap: () => onPlayer(p),
                      child: Padding(
                        padding: const EdgeInsets.symmetric(vertical: 3),
                        child: Row(
                          children: [
                            Icon(
                              Icons.checkroom,
                              size: 15,
                              color: squadPosition(p) == 'G'
                                  ? const Color(0xFFAF6100)
                                  : shirt,
                            ),
                            const SizedBox(width: 5),
                            Expanded(
                              child: Text(
                                '${squadPlayerName(p)} (F:${p['strength'] ?? '—'})',
                                maxLines: 2,
                                overflow: TextOverflow.ellipsis,
                                style: const TextStyle(fontSize: 13),
                              ),
                            ),
                          ],
                        ),
                      ),
                    ),
                  const SizedBox(height: 8),
                ],
              ],
            );
            if (c.maxWidth < 470) return pitch;
            return Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Expanded(flex: 7, child: pitch),
                const SizedBox(width: 8),
                Expanded(flex: 3, child: sectorList()),
              ],
            );
          },
        ),
        const SizedBox(height: 6),
        ClassicQuickSquadSwap(
          starters: selected,
          reserves: reserves,
          locked: locked,
          onSwap: onSwap,
        ),
      ],
    );
  }
}

class ClassicQuickSquadSwap extends StatefulWidget {
  final List<Map<String, dynamic>> starters, reserves;
  final bool locked;
  final void Function(String, String) onSwap;
  const ClassicQuickSquadSwap({
    super.key,
    required this.starters,
    required this.reserves,
    required this.locked,
    required this.onSwap,
  });
  @override
  State<ClassicQuickSquadSwap> createState() => _ClassicQuickSquadSwapState();
}

class _ClassicQuickSquadSwapState extends State<ClassicQuickSquadSwap> {
  String? _out, _in;
  @override
  Widget build(BuildContext context) {
    if (widget.starters.isEmpty || widget.reserves.isEmpty) {
      return const SizedBox.shrink();
    }
    final out = widget.starters.any((p) => '${p['id']}' == _out)
        ? _out!
        : '${widget.starters.first['id']}';
    final into = widget.reserves.any((p) => '${p['id']}' == _in)
        ? _in!
        : '${widget.reserves.first['id']}';
    Widget select(
      String label,
      String key,
      String selected,
      List<Map<String, dynamic>> players,
      ValueChanged<String?> change,
    ) => Expanded(
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(label, style: const TextStyle(fontSize: 12)),
          DropdownButton<String>(
            key: ValueKey(key),
            value: selected,
            isExpanded: true,
            isDense: true,
            items: [
              for (final p in players)
                DropdownMenuItem(
                  value: '${p['id']}',
                  child: Text(
                    '${squadPlayerName(p)} (${squadPosition(p)}-${p['strength'] ?? '—'})',
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(fontSize: 13),
                  ),
                ),
            ],
            onChanged: widget.locked ? null : change,
          ),
        ],
      ),
    );
    return Container(
      padding: const EdgeInsets.all(6),
      decoration: BoxDecoration(
        border: Border.all(color: ClassicTheme.of(context).border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const Text(
            'TROCA RÁPIDA',
            style: TextStyle(fontSize: 13, fontWeight: FontWeight.w700),
          ),
          Row(
            children: [
              select(
                'Sai titular',
                'dropdown-swap-starter',
                out,
                widget.starters,
                (v) => setState(() => _out = v),
              ),
              const Padding(
                padding: EdgeInsets.symmetric(horizontal: 8),
                child: Icon(Icons.swap_horiz, size: 20),
              ),
              select(
                'Entra reserva',
                'dropdown-swap-reserve',
                into,
                widget.reserves,
                (v) => setState(() => _in = v),
              ),
            ],
          ),
          Align(
            alignment: Alignment.centerRight,
            child: ElevatedButton(
              key: const ValueKey('btn-trocar-titular-reserva'),
              onPressed: widget.locked
                  ? null
                  : () {
                      widget.onSwap(out, into);
                      setState(() {
                        _out = into;
                        _in = out;
                      });
                    },
              child: const Text('TROCAR TITULAR POR RESERVA'),
            ),
          ),
        ],
      ),
    );
  }
}

class ClassicCoachPreparation extends StatelessWidget {
  final Map<String, dynamic> club;
  final String coach, date, opponent, preparation;
  const ClassicCoachPreparation({
    super.key,
    required this.club,
    required this.coach,
    required this.date,
    required this.opponent,
    required this.preparation,
  });
  @override
  Widget build(BuildContext context) {
    final t = ClassicTheme.of(context);
    Widget fact(String label, String value) => Expanded(
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 5),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(label, style: TextStyle(fontSize: 12, color: t.textSecondary)),
            Text(
              value,
              style: const TextStyle(fontSize: 15, fontWeight: FontWeight.w700),
            ),
          ],
        ),
      ),
    );
    return Container(
      key: const ValueKey('coach-employed-panel'),
      decoration: BoxDecoration(border: Border.all(color: t.border)),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Container(
            color: t.surfaceAlternate,
            padding: const EdgeInsets.all(6),
            child: const Text(
              'INFORMAÇÕES DO TREINADOR',
              style: TextStyle(fontSize: 16, fontWeight: FontWeight.w700),
            ),
          ),
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              fact('Clube / Treinador', '${club['name']}\n$coach'),
              fact('Data do jogo', date),
              fact('Próximo adversário', opponent),
              fact('Saldo em caixa', formatClassicMoney(club['cashBalance'])),
            ],
          ),
          Padding(
            padding: const EdgeInsets.fromLTRB(8, 2, 8, 6),
            child: Text(
              '${club['divisionDisplay'] ?? '—'}  •  Teto de evolução: ${club['growthCeiling'] ?? '—'}\nPreparação: $preparation',
              style: const TextStyle(fontSize: 13),
            ),
          ),
        ],
      ),
    );
  }
}
