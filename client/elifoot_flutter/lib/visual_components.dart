import 'package:flutter/material.dart';
import 'club_theme.dart';

String classicCompetition(dynamic raw) => switch (raw?.toString()) {
  'CupPreliminary1' => 'Taça - Preliminar 1',
  'CupRoundOf128' => 'Taça - 128 clubes',
  'CupRoundOf256' => 'Taça - 256 clubes',
  'CupRoundOf512' => 'Taça - 512 clubes',
  'CupRoundOf1024' => 'Taça - 1024 clubes',
  'CupPreliminary2' => 'Taça - Preliminar 2',
  'League' => 'Campeonato Nacional',
  'CupRoundOf64' || 'CupRound64' => 'Taça - 64 clubes',
  'CupRoundOf32' || 'CupRound32' => 'Taça - 32 clubes',
  'CupRoundOf16' || 'CupRound16' => 'Taça - Oitavas',
  'CupQuarterFinal' => 'Taça - Quartas',
  'CupSemiFinal' => 'Taça - Semifinal',
  'CupFinal' => 'Taça - Final',
  _ => raw?.toString() ?? '',
};

/// A local surface, not a new saved theme. Custom/dark/contrast choices remain authoritative.
class ClassicPaper extends StatelessWidget {
  final WidgetBuilder builder;
  const ClassicPaper({super.key, required this.builder});
  @override
  Widget build(BuildContext context) => Theme(
    data: ClassicTheme.of(context).paper.material(),
    child: DefaultTextStyle.merge(
      style: TextStyle(
        color: ClassicTheme.of(context).paper.textPrimary,
        fontFamily: 'ClassicCondensed',
        fontSize: 16,
      ),
      child: Builder(builder: builder),
    ),
  );
}

class ClassicPaperPanel extends StatelessWidget {
  final String? title;
  final Widget child;
  final EdgeInsetsGeometry padding;
  final IconData? icon;
  final Color? iconColor;
  const ClassicPaperPanel({
    super.key,
    this.title,
    required this.child,
    this.padding = const EdgeInsets.all(12),
    this.icon,
    this.iconColor,
  });
  @override
  Widget build(BuildContext context) {
    final global = ClassicTheme.of(context);
    return Container(
      decoration: BoxDecoration(
        color: global.paper.surface,
        borderRadius: BorderRadius.circular(8),
        border: Border.all(color: global.paper.border, width: 1.3),
      ),
      padding: const EdgeInsets.all(5),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          if (title != null)
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
              decoration: BoxDecoration(
                gradient: LinearGradient(
                  colors: [global.primary, global.secondary],
                ),
                borderRadius: BorderRadius.circular(4),
              ),
              child: Row(
                children: [
                  if (icon != null) ...[
                    Icon(
                      icon,
                      color:
                          iconColor ??
                          (global.classicPresentation
                              ? Colors.white
                              : global.textPrimary),
                      size: 22,
                    ),
                    const SizedBox(width: 10),
                  ],
                  Expanded(
                    child: Text(
                      title!,
                      style: TextStyle(
                        color: readableInk(global.primary, Colors.white),
                        fontWeight: FontWeight.w700,
                        fontSize: 21,
                        height: 1.05,
                      ),
                    ),
                  ),
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

class ClassicSelectionTabs extends StatelessWidget {
  final List<(String, String)> items;
  final String selected;
  final ValueChanged<String> onSelected;
  final double fontSize;
  final String? keyPrefix;
  const ClassicSelectionTabs({
    super.key,
    required this.items,
    required this.selected,
    required this.onSelected,
    this.fontSize = 17,
    this.keyPrefix,
  });
  @override
  Widget build(BuildContext context) {
    final t = ClassicTheme.of(context);
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        for (final item in items) ...[
          if (item != items.first) const SizedBox(width: 6),
          Expanded(
            child: Semantics(
              selected: selected == item.$1,
              child: OutlinedButton(
                key: keyPrefix == null
                    ? null
                    : ValueKey('$keyPrefix${item.$1}'),
                onPressed: () => onSelected(item.$1),
                style: OutlinedButton.styleFrom(
                  minimumSize: const Size(44, 46),
                  padding: const EdgeInsets.symmetric(
                    horizontal: 5,
                    vertical: 10,
                  ),
                  foregroundColor: selected == item.$1
                      ? t.selectedText
                      : t.textPrimary,
                  backgroundColor: selected == item.$1
                      ? t.selectedBackground
                      : t.primary,
                  side: BorderSide(
                    color: selected == item.$1
                        ? t.selectedBackground
                        : t.border,
                  ),
                  shape: RoundedRectangleBorder(
                    borderRadius: BorderRadius.circular(6),
                  ),
                ),
                child: Text(
                  item.$2,
                  textAlign: TextAlign.center,
                  style: TextStyle(
                    fontSize: fontSize,
                    fontWeight: FontWeight.w700,
                    height: 1.05,
                  ),
                ),
              ),
            ),
          ),
        ],
      ],
    );
  }
}

class ClassicBottomNavigation extends StatelessWidget {
  final bool compactMenu;
  final int selected;
  final bool locked;
  final bool showPlayerRankings;
  final bool showCoachRanking;
  final ValueChanged<int> onSelected;
  const ClassicBottomNavigation({
    super.key,
    this.compactMenu = false,
    required this.selected,
    required this.locked,
    required this.onSelected,
    this.showPlayerRankings = false,
    this.showCoachRanking = false,
  });
  @override
  Widget build(BuildContext context) {
    final t = ClassicTheme.of(context);
    final entries = [
      (Icons.checkroom, 'PLANTEL'),
      (Icons.account_tree, 'TÁTICAS'),
      (Icons.swap_horiz, 'MERCADO'),
      showCoachRanking
          ? (Icons.bar_chart, 'RANKING')
          : showPlayerRankings
          ? (Icons.bar_chart, 'RANKINGS')
          : (Icons.emoji_events, 'COMPETIÇÕES'),
      (Icons.more_horiz, 'MAIS'),
    ];
    return Container(
      key: const Key('classic-bottom-navigation'),
      decoration: BoxDecoration(
        gradient: LinearGradient(colors: [t.primary, t.secondary]),
        border: Border(top: BorderSide(color: t.border)),
      ),
      child: SafeArea(
        top: false,
        child: Row(
          children: [
            for (var i = 0; i < entries.length; i++)
              Expanded(
                child: Semantics(
                  key: compactMenu && i == 4 && !locked
                      ? const Key('open-navigation')
                      : ValueKey('mobile-nav-$i'),
                  selected: selected == i,
                  button: true,
                  enabled: !locked,
                  child: InkWell(
                    key: ValueKey('bottom-nav-$i'),
                    onTap: locked ? null : () => onSelected(i),
                    child: Container(
                      constraints: const BoxConstraints(minHeight: 66),
                      padding: const EdgeInsets.fromLTRB(2, 9, 2, 5),
                      child: Column(
                        mainAxisSize: MainAxisSize.min,
                        children: [
                          Icon(
                            entries[i].$1,
                            size: 25,
                            color: selected == i ? t.accent : t.textPrimary,
                          ),
                          const SizedBox(height: 3),
                          Text(
                            entries[i].$2,
                            maxLines: 1,
                            style: TextStyle(
                              fontSize: 12,
                              fontWeight: FontWeight.w700,
                              color: selected == i ? t.accent : t.textPrimary,
                            ),
                          ),
                          const SizedBox(height: 4),
                          Container(
                            height: 3,
                            width: 48,
                            decoration: BoxDecoration(
                              color: selected == i
                                  ? t.accent
                                  : Colors.transparent,
                              borderRadius: BorderRadius.circular(3),
                            ),
                          ),
                        ],
                      ),
                    ),
                  ),
                ),
              ),
          ],
        ),
      ),
    );
  }
}

class ClassicStandings extends StatefulWidget {
  final List<dynamic> rows;
  final String title;
  final String? clubId;
  const ClassicStandings({
    super.key,
    required this.rows,
    required this.title,
    this.clubId,
  });
  @override
  State<ClassicStandings> createState() => _ClassicStandingsState();
}

class _ClassicStandingsState extends State<ClassicStandings> {
  final Set<String> expanded = {};
  @override
  Widget build(BuildContext context) => ClassicPaperPanel(
    title: widget.title,
    padding: EdgeInsets.zero,
    child: ClassicPaper(
      builder: (context) {
        final t = ClassicTheme.of(context);
        return LayoutBuilder(
          builder: (context, c) {
            final mobile = c.maxWidth < 700;
            final fields = mobile
                ? [
                    'points',
                    'played',
                    'wins',
                    'draws',
                    'losses',
                    'goalDifference',
                  ]
                : [
                    'points',
                    'played',
                    'wins',
                    'draws',
                    'losses',
                    'goalsFor',
                    'goalsAgainst',
                    'goalDifference',
                  ];
            final labels = mobile
                ? ['PTS', 'J', 'V', 'E', 'D', 'SG']
                : ['PTS', 'J', 'V', 'E', 'D', 'GP', 'GC', 'SG'];
            Widget line(List<Widget> cells) => Row(children: cells);
            Widget number(String label) => SizedBox(
              width: mobile ? 23 : 44,
              child: Text(
                label,
                textAlign: TextAlign.center,
                style: TextStyle(
                  fontSize: mobile ? 13 : 15,
                  fontWeight: FontWeight.w700,
                ),
              ),
            );
            return Column(
              key: const ValueKey('standings-table'),
              children: [
                Container(
                  color: t.tableHeader,
                  padding: const EdgeInsets.symmetric(
                    horizontal: 6,
                    vertical: 9,
                  ),
                  child: DefaultTextStyle.merge(
                    style: TextStyle(
                      color: t.classicPresentation ? Colors.white : null,
                    ),
                    child: line([
                      number('#'),
                      Expanded(
                        flex: 4,
                        child: Text(
                          'CLUBE',
                          style: const TextStyle(fontWeight: FontWeight.bold),
                        ),
                      ),
                      if (!mobile)
                        const Expanded(
                          flex: 3,
                          child: Text(
                            'TÉCNICO',
                            style: TextStyle(fontWeight: FontWeight.bold),
                          ),
                        ),
                      for (final label in labels) number(label),
                      if (!mobile)
                        const Expanded(
                          flex: 2,
                          child: Text(
                            'ZONA',
                            textAlign: TextAlign.center,
                            style: TextStyle(fontWeight: FontWeight.bold),
                          ),
                        ),
                    ]),
                  ),
                ),
                if (widget.rows.isEmpty)
                  const Padding(
                    padding: EdgeInsets.all(20),
                    child: Text('Classificação ainda não disponível.'),
                  ),
                for (var i = 0; i < widget.rows.length; i++)
                  Builder(
                    builder: (context) {
                      final r = widget.rows[i];
                      final id = '${r['clubId'] ?? i}';
                      final zone = '${r['zoneStatus'] ?? ''}';
                      final my = widget.clubId != null && id == widget.clubId;
                      final relegated = zone.toUpperCase().contains('REBAIX');
                      return Column(
                        children: [
                          InkWell(
                            onTap: mobile
                                ? () => setState(
                                    () => expanded.contains(id)
                                        ? expanded.remove(id)
                                        : expanded.add(id),
                                  )
                                : null,
                            child: Container(
                              color: my
                                  ? t.surfaceAlternate
                                  : i.isEven
                                  ? t.tableRowAlternate
                                  : t.tableRow,
                              padding: const EdgeInsets.symmetric(
                                horizontal: 6,
                                vertical: 8,
                              ),
                              child: line([
                                number('${r['position'] ?? i + 1}'),
                                Expanded(
                                  flex: 4,
                                  child: Text(
                                    '${r['clubName']}',
                                    style: TextStyle(
                                      fontWeight: FontWeight.w700,
                                      fontSize: mobile ? 14 : 17,
                                    ),
                                  ),
                                ),
                                if (!mobile)
                                  Expanded(
                                    flex: 3,
                                    child: Text(
                                      '${r['coachName'] ?? '—'}',
                                      style: const TextStyle(fontSize: 16),
                                    ),
                                  ),
                                for (final field in fields)
                                  number('${r[field] ?? '—'}'),
                                if (!mobile)
                                  Expanded(
                                    flex: 2,
                                    child: Center(
                                      child: Container(
                                        padding: const EdgeInsets.symmetric(
                                          horizontal: 8,
                                          vertical: 2,
                                        ),
                                        decoration: BoxDecoration(
                                          color: relegated
                                              ? const Color(0xFFFFDCDD)
                                              : const Color(0xFFD2EACD),
                                          borderRadius: BorderRadius.circular(
                                            8,
                                          ),
                                        ),
                                        child: Text(
                                          zone,
                                          style: TextStyle(
                                            color: relegated
                                                ? const Color(0xFF9D1423)
                                                : const Color(0xFF164525),
                                            fontWeight: FontWeight.bold,
                                          ),
                                        ),
                                      ),
                                    ),
                                  ),
                              ]),
                            ),
                          ),
                          if (mobile && expanded.contains(id))
                            Container(
                              width: double.infinity,
                              color: t.surfaceAlternate,
                              padding: const EdgeInsets.all(10),
                              child: Text(
                                'Técnico: ${r['coachName'] ?? '—'}\nGP: ${r['goalsFor']} • GC: ${r['goalsAgainst']} • Situação: $zone',
                              ),
                            ),
                        ],
                      );
                    },
                  ),
                if (mobile)
                  Padding(
                    padding: const EdgeInsets.all(5),
                    child: Text(
                      'Toque no clube para técnico, gols e situação.',
                      style: TextStyle(color: t.textSecondary, fontSize: 12),
                    ),
                  ),
              ],
            );
          },
        );
      },
    ),
  );
}

class ClassicRoundSummary extends StatelessWidget {
  final List<(String, dynamic)> rounds;
  const ClassicRoundSummary({super.key, required this.rounds});
  @override
  Widget build(BuildContext context) => ClassicPaperPanel(
    title: 'RESUMO DE RODADAS (ANTERIOR • ATUAL • PRÓXIMA)',
    child: LayoutBuilder(
      builder: (context, c) {
        final visible = rounds.where((r) => r.$2 != null).toList();
        return Wrap(
          spacing: 10,
          runSpacing: 10,
          children: [
            for (final r in visible)
              SizedBox(
                width: c.maxWidth < 600
                    ? c.maxWidth
                    : (c.maxWidth - 10 * (visible.length - 1)) / visible.length,
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Container(
                      padding: const EdgeInsets.all(8),
                      color: ClassicTheme.of(context).primary,
                      child: Text(
                        '${r.$1}: R${r.$2['roundNumber']} (${classicCompetition(r.$2['competition'])})',
                        style: TextStyle(
                          color: ClassicTheme.of(context).selectedBackground,
                          fontSize: 16,
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                    ),
                    ClassicPaper(
                      builder: (_) => Padding(
                        padding: const EdgeInsets.all(8),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            for (final f
                                in (r.$2['fixtures'] as List? ?? []).take(4))
                              Padding(
                                padding: const EdgeInsets.only(bottom: 3),
                                child: Text(
                                  '• ${f['home']} ${f['isFinished'] == true ? '${f['homeGoals']} x ${f['awayGoals']}' : '×'} ${f['away']}',
                                  style: const TextStyle(fontSize: 16),
                                ),
                              ),
                            if ((r.$2['fixtures'] as List? ?? []).isEmpty)
                              const Text(
                                'A definir',
                                style: TextStyle(fontSize: 16),
                              ),
                          ],
                        ),
                      ),
                    ),
                  ],
                ),
              ),
          ],
        );
      },
    ),
  );
}

class ClassicFixtureList extends StatelessWidget {
  final List<dynamic> fixtures;
  final String tableKey;
  final bool live;
  final Map<String, dynamic>? Function(dynamic id, dynamic name) palette;
  const ClassicFixtureList({
    super.key,
    required this.fixtures,
    required this.tableKey,
    required this.live,
    required this.palette,
  });
  @override
  Widget build(BuildContext context) => ClassicPaper(
    builder: (context) {
      final t = ClassicTheme.of(context);
      return LayoutBuilder(
        builder: (context, c) {
          final mobile = c.maxWidth < 700;
          Widget cell(
            String text, {
            bool heading = false,
            TextAlign align = TextAlign.left,
          }) => Text(
            text,
            textAlign: align,
            style: TextStyle(
              fontSize: mobile ? 14 : 16,
              fontWeight: heading ? FontWeight.bold : FontWeight.w600,
            ),
          );
          return Column(
            key: ValueKey(tableKey),
            children: [
              if (!mobile)
                Container(
                  color: t.tableHeader,
                  padding: const EdgeInsets.symmetric(
                    horizontal: 10,
                    vertical: 7,
                  ),
                  child: DefaultTextStyle.merge(
                    style: TextStyle(
                      color: t.classicPresentation ? Colors.white : null,
                    ),
                    child: Row(
                      children: [
                        SizedBox(width: 35, child: cell('#', heading: true)),
                        Expanded(
                          flex: 4,
                          child: cell('MANDANTE', heading: true),
                        ),
                        Expanded(
                          flex: 4,
                          child: cell('VISITANTE', heading: true),
                        ),
                        SizedBox(
                          width: 90,
                          child: cell(
                            'PLACAR',
                            heading: true,
                            align: TextAlign.center,
                          ),
                        ),
                        SizedBox(
                          width: 150,
                          child: cell(
                            'SITUAÇÃO',
                            heading: true,
                            align: TextAlign.center,
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
              for (var i = 0; i < fixtures.length; i++)
                Builder(
                  builder: (context) {
                    final f = fixtures[i];
                    final finished = f['isFinished'] == true;
                    Widget team(bool home) => TeamColorName(
                      name: '${f[home ? 'home' : 'away']}',
                      palette: palette(
                        f[home ? 'homeClubId' : 'awayClubId'],
                        f[home ? 'home' : 'away'],
                      ),
                      maxLines: mobile ? 3 : 2,
                      fontSize: mobile ? 14 : 17,
                      textAlign: mobile ? TextAlign.center : TextAlign.left,
                    );
                    final score = finished
                        ? '${f['homeGoals']} x ${f['awayGoals']}'
                        : '— x —';
                    final status = finished
                        ? 'FINALIZADO'
                        : live
                        ? 'EM ANDAMENTO'
                        : 'PREPARANDO';
                    return Container(
                      color: i.isEven ? t.tableRow : t.tableRowAlternate,
                      padding: const EdgeInsets.symmetric(
                        horizontal: 6,
                        vertical: 4,
                      ),
                      child: Row(
                        children: [
                          if (!mobile)
                            SizedBox(width: 35, child: cell('${i + 1}')),
                          Expanded(flex: 4, child: team(true)),
                          if (mobile)
                            SizedBox(
                              width: 42,
                              child: cell(score, align: TextAlign.center),
                            ),
                          Expanded(flex: 4, child: team(false)),
                          if (!mobile)
                            SizedBox(
                              width: 90,
                              child: cell(score, align: TextAlign.center),
                            ),
                          SizedBox(
                            width: mobile ? 80 : 150,
                            child: Flex(
                              direction: mobile
                                  ? Axis.vertical
                                  : Axis.horizontal,
                              mainAxisSize: MainAxisSize.min,
                              mainAxisAlignment: MainAxisAlignment.center,
                              children: [
                                Icon(
                                  finished
                                      ? Icons.check_circle_outline
                                      : Icons.schedule,
                                  size: 15,
                                  color: t.accent,
                                ),
                                if (!mobile) const SizedBox(width: 6),
                                if (mobile)
                                  cell(status, align: TextAlign.center)
                                else
                                  Flexible(
                                    child: cell(
                                      status,
                                      align: TextAlign.center,
                                    ),
                                  ),
                              ],
                            ),
                          ),
                        ],
                      ),
                    );
                  },
                ),
            ],
          );
        },
      );
    },
  );
}
