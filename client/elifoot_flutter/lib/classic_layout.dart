import 'package:flutter/material.dart';
import 'club_theme.dart';

/// Layout tokens share the existing appearance settings and contrast safeguards.
extension ClassicLayoutTokens on ClassicTheme {
  Color get appBackground => background;
  Color get sidebarBackground => secondary;
  Color get headerBackground => primary;
  Color get panelBackground => surface;
  Color get panelHeader => primary;
  Color get panelBorder => border;
  Color get activeNavigation => selectedBackground;
  Color get activeTab => selectedBackground;
  Color get primaryText => textPrimary;
  Color get secondaryText => textSecondary;
  Color get accentYellow => accent;
  Color get actionGreen => buttonBackground;
  Color get tableRowOdd => tableRow;
  Color get tableRowEven => tableRowAlternate;
  Color get buttonHover => surfaceAlternate;
  LinearGradient get panelGradient =>
      LinearGradient(colors: [primary, secondary]);
}

const classicNavigation = [
  ('1. PLANTEL & TÁTICAS', Icons.checkroom),
  ('2. DIVISÕES & TAÇA', Icons.emoji_events),
  ('3. MERCADO & LEILÃO', Icons.swap_horiz),
  ('4. FINANÇAS & ESTÁDIO', Icons.account_balance),
  ('5. RANKING & CARREIRA', Icons.bar_chart),
  ('6. CALENDÁRIO & RESULTADOS', Icons.calendar_month_outlined),
  ('7. RANKINGS DE JOGADORES', Icons.groups_outlined),
];

class AppHeader extends StatelessWidget {
  final String round, date, world;
  final Widget action;
  final VoidCallback? openMenu;
  final bool live;
  const AppHeader({
    super.key,
    required this.round,
    required this.date,
    required this.world,
    required this.action,
    this.openMenu,
    this.live = false,
  });
  @override
  Widget build(BuildContext context) {
    final t = ClassicTheme.of(context);
    return LayoutBuilder(
      builder: (context, c) {
        final compact = c.maxWidth < 1024;
        final brand = Row(
          mainAxisSize: MainAxisSize.min,
          children: [
            if (openMenu != null)
              IconButton(
                key: const Key('open-navigation'),
                tooltip: 'ABRIR MENU',
                onPressed: openMenu,
                icon: const Icon(Icons.menu),
                color: t.textPrimary,
              ),
            Icon(
              Icons.sports_soccer,
              color: t.textPrimary,
              size: compact
                  ? 29
                  : c.maxWidth < 1200
                  ? 40
                  : 46,
            ),
            const SizedBox(width: 8),
            Text(
              live ? 'ELIFOOT ' : 'LIGA CLÁSSICA ',
              style: TextStyle(
                color: t.textPrimary,
                fontSize: compact
                    ? 22
                    : c.maxWidth < 1200
                    ? 26
                    : 36,
                fontWeight: FontWeight.w900,
                fontStyle: FontStyle.italic,
                letterSpacing: -.8,
              ),
            ),
            Text(
              live ? '98 ONLINE' : '98',
              style: TextStyle(
                color: t.accent,
                fontSize: compact
                    ? 28
                    : c.maxWidth < 1200
                    ? 32
                    : 42,
                fontWeight: FontWeight.w900,
                fontStyle: FontStyle.italic,
              ),
            ),
          ],
        );
        if (c.maxWidth < 768) {
          return Container(
            key: const Key('classic-app-header'),
            padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 6),
            decoration: BoxDecoration(
              gradient: t.panelGradient,
              border: Border(bottom: BorderSide(color: t.accent)),
            ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Row(
                  children: [
                    Expanded(
                      child: FittedBox(
                        fit: BoxFit.scaleDown,
                        alignment: Alignment.centerLeft,
                        child: brand,
                      ),
                    ),
                    const SizedBox(width: 6),
                    Container(
                      padding: const EdgeInsets.symmetric(
                        horizontal: 5,
                        vertical: 10,
                      ),
                      decoration: BoxDecoration(
                        border: Border.all(color: t.border),
                        borderRadius: BorderRadius.circular(3),
                      ),
                      child: Text(
                        date,
                        style: TextStyle(
                          color: t.textPrimary,
                          fontSize: 10,
                          fontWeight: FontWeight.bold,
                        ),
                      ),
                    ),
                    const SizedBox(width: 5),
                    ConstrainedBox(
                      constraints: const BoxConstraints(maxWidth: 116),
                      child: DefaultTextStyle.merge(
                        style: const TextStyle(fontSize: 10),
                        child: action,
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 4),
                Text(
                  round,
                  key: const Key('classic-header-round'),
                  maxLines: 2,
                  style: TextStyle(
                    color: t.textPrimary,
                    fontSize: 11,
                    fontWeight: FontWeight.w600,
                  ),
                ),
                if (live)
                  Padding(
                    padding: const EdgeInsets.only(top: 3),
                    child: Text(
                      world,
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                      style: TextStyle(color: t.textSecondary, fontSize: 10),
                    ),
                  ),
              ],
            ),
          );
        }
        final info = Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              round,
              key: const Key('classic-header-round'),
              style: TextStyle(
                color: t.textPrimary,
                fontWeight: FontWeight.bold,
                fontSize: compact ? 14 : 18,
              ),
              maxLines: 2,
            ),
            if (!compact)
              Text(
                world,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(color: t.textSecondary, fontSize: 14),
              ),
          ],
        );
        final calendar = Container(
          padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 9),
          decoration: BoxDecoration(
            border: Border.all(color: t.border),
            borderRadius: BorderRadius.circular(3),
          ),
          child: Row(
            mainAxisSize: MainAxisSize.min,
            children: [
              Icon(
                Icons.calendar_today_outlined,
                size: 15,
                color: t.textSecondary,
              ),
              const SizedBox(width: 7),
              Text(
                date,
                style: TextStyle(
                  color: t.textPrimary,
                  fontSize: c.maxWidth < 1200 ? 14 : 16,
                ),
              ),
            ],
          ),
        );
        return Container(
          key: const Key('classic-app-header'),
          padding: EdgeInsets.symmetric(
            horizontal: compact ? 4 : 16,
            vertical: 8,
          ),
          decoration: BoxDecoration(
            gradient: t.panelGradient,
            border: Border(bottom: BorderSide(color: t.accent, width: 1.5)),
          ),
          child: compact
              ? Column(
                  children: [
                    Row(
                      children: [
                        Expanded(
                          child: FittedBox(
                            fit: BoxFit.scaleDown,
                            alignment: Alignment.centerLeft,
                            child: brand,
                          ),
                        ),
                        const SizedBox(width: 4),
                        ConstrainedBox(
                          constraints: const BoxConstraints(maxWidth: 150),
                          child: action,
                        ),
                      ],
                    ),
                    Padding(
                      padding: const EdgeInsets.fromLTRB(12, 5, 10, 0),
                      child: Row(
                        children: [
                          Expanded(child: info),
                          const SizedBox(width: 8),
                          Text(
                            date,
                            style: TextStyle(
                              fontSize: 14,
                              color: t.textPrimary,
                            ),
                          ),
                        ],
                      ),
                    ),
                  ],
                )
              : Row(
                  children: [
                    brand,
                    const SizedBox(width: 24),
                    Expanded(child: info),
                    const SizedBox(width: 10),
                    calendar,
                    const SizedBox(width: 12),
                    ConstrainedBox(
                      constraints: const BoxConstraints(maxWidth: 240),
                      child: action,
                    ),
                  ],
                ),
        );
      },
    );
  }
}

class AppSidebar extends StatelessWidget {
  final int selected;
  final bool locked, showAppearance, appearanceSelected;
  final ValueChanged<int> onSelected;
  final VoidCallback? appearance, switchRoom, liveMatches;
  const AppSidebar({
    super.key,
    required this.selected,
    required this.locked,
    required this.onSelected,
    this.appearance,
    this.switchRoom,
    this.liveMatches,
    this.showAppearance = true,
    this.appearanceSelected = false,
  });
  @override
  Widget build(BuildContext context) {
    final t = ClassicTheme.of(context);
    return Container(
      key: const Key('classic-sidebar'),
      width: 216,
      decoration: BoxDecoration(
        gradient: LinearGradient(
          begin: Alignment.topLeft,
          end: Alignment.bottomRight,
          colors: [t.sidebarBackground, t.headerBackground],
        ),
        border: Border(right: BorderSide(color: t.border)),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Padding(
            padding: const EdgeInsets.fromLTRB(14, 20, 12, 14),
            child: Text(
              locked ? 'PARTIDA EM ANDAMENTO' : 'CENTRAL DO TREINADOR',
              style: TextStyle(
                color: t.textSecondary,
                fontSize: 10,
                fontWeight: FontWeight.bold,
                letterSpacing: 1,
              ),
            ),
          ),
          Expanded(
            child: SingleChildScrollView(
              child: Column(
                children: [
                  if (liveMatches != null)
                    InkWell(
                      key: const Key('open-live-matches'),
                      onTap: locked ? null : liveMatches,
                      child: Padding(
                        padding: const EdgeInsets.symmetric(
                          horizontal: 12,
                          vertical: 14,
                        ),
                        child: Row(
                          children: [
                            Icon(
                              Icons.sports_soccer,
                              color: t.textPrimary,
                              size: 23,
                            ),
                            const SizedBox(width: 10),
                            Text(
                              'JOGOS AO VIVO',
                              style: TextStyle(
                                color: t.textPrimary,
                                fontWeight: FontWeight.bold,
                                fontSize: 11,
                              ),
                            ),
                          ],
                        ),
                      ),
                    ),
                  for (int i = 0; i < classicNavigation.length; i++)
                    Semantics(
                      button: true,
                      selected: selected == i,
                      enabled: !locked,
                      child: InkWell(
                        key: ValueKey('shell-nav-$i'),
                        onTap: locked ? null : () => onSelected(i),
                        hoverColor: t.buttonHover,
                        child: Container(
                          constraints: const BoxConstraints(minHeight: 57),
                          padding: const EdgeInsets.symmetric(horizontal: 12),
                          decoration: BoxDecoration(
                            color: selected == i && !locked
                                ? t.activeNavigation
                                : null,
                            border: Border(
                              bottom: BorderSide(
                                color: t.border.withValues(alpha: .4),
                              ),
                            ),
                          ),
                          child: Row(
                            children: [
                              Icon(
                                classicNavigation[i].$2,
                                size: 23,
                                color: selected == i && !locked
                                    ? t.selectedText
                                    : locked
                                    ? t.textSecondary
                                    : t.textPrimary,
                              ),
                              const SizedBox(width: 10),
                              Expanded(
                                child: Text(
                                  classicNavigation[i].$1,
                                  style: TextStyle(
                                    color: selected == i && !locked
                                        ? t.selectedText
                                        : locked
                                        ? t.textSecondary
                                        : t.textPrimary,
                                    fontSize: 16,
                                    fontWeight: FontWeight.w700,
                                  ),
                                ),
                              ),
                            ],
                          ),
                        ),
                      ),
                    ),
                ],
              ),
            ),
          ),
          Padding(
            padding: const EdgeInsets.all(10),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                if (showAppearance)
                  OutlinedButton.icon(
                    key: const ValueKey('appearance-menu'),
                    style: OutlinedButton.styleFrom(
                      backgroundColor: appearanceSelected
                          ? t.selectedBackground
                          : null,
                      foregroundColor: appearanceSelected
                          ? t.selectedText
                          : t.accent,
                    ),
                    onPressed: locked ? null : appearance,
                    icon: const Icon(Icons.settings_outlined, size: 16),
                    label: const Text('APARÊNCIA'),
                  ),
                OutlinedButton.icon(
                  key: const Key('switch-room'),
                  onPressed: locked ? null : switchRoom,
                  icon: const Icon(Icons.logout, size: 16),
                  label: const Text('TROCAR SALA'),
                ),
                const SizedBox(height: 4),
                Text(
                  'LIGA CLÁSSICA 98',
                  textAlign: TextAlign.center,
                  style: TextStyle(
                    color: t.textSecondary,
                    fontSize: 9,
                    letterSpacing: 1,
                  ),
                ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class ClassicPanel extends StatelessWidget {
  final String? title;
  final Widget child;
  final EdgeInsetsGeometry padding;
  const ClassicPanel({
    super.key,
    this.title,
    required this.child,
    this.padding = const EdgeInsets.all(10),
  });
  @override
  Widget build(BuildContext context) {
    final t = ClassicTheme.of(context);
    return Container(
      decoration: BoxDecoration(
        color: t.panelBackground,
        border: Border.all(color: t.panelBorder),
        borderRadius: BorderRadius.circular(4),
      ),
      clipBehavior: Clip.antiAlias,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          if (title != null)
            Container(
              padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 9),
              decoration: BoxDecoration(
                gradient: t.panelGradient,
                border: Border(bottom: BorderSide(color: t.border)),
              ),
              child: Text(
                title!,
                style: TextStyle(
                  color: t.accent,
                  fontSize: 13,
                  fontWeight: FontWeight.w800,
                ),
              ),
            ),
          Padding(padding: padding, child: child),
        ],
      ),
    );
  }
}

class ClassicTabs extends StatelessWidget {
  final List<(String, String)> items;
  final String selected;
  final ValueChanged<String> onSelected;
  const ClassicTabs({
    super.key,
    required this.items,
    required this.selected,
    required this.onSelected,
  });
  @override
  Widget build(BuildContext context) {
    final t = ClassicTheme.of(context);
    return SingleChildScrollView(
      scrollDirection: Axis.horizontal,
      child: Row(
        children: [
          for (final item in items)
            Padding(
              padding: const EdgeInsets.only(right: 4),
              child: Semantics(
                selected: selected == item.$1,
                child: OutlinedButton(
                  onPressed: () => onSelected(item.$1),
                  style: OutlinedButton.styleFrom(
                    backgroundColor: selected == item.$1
                        ? t.activeTab
                        : t.surface,
                    foregroundColor: selected == item.$1
                        ? t.selectedText
                        : t.textPrimary,
                    side: BorderSide(
                      color: selected == item.$1 ? t.accent : t.border,
                    ),
                    minimumSize: const Size(60, 38),
                  ),
                  child: Text(
                    item.$2,
                    style: const TextStyle(
                      fontSize: 11,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                ),
              ),
            ),
        ],
      ),
    );
  }
}

class ClassicFilterBar extends StatelessWidget {
  final List<Widget> children;
  const ClassicFilterBar({super.key, required this.children});
  @override
  Widget build(BuildContext context) => ClassicPanel(
    child: Wrap(
      spacing: 8,
      runSpacing: 8,
      crossAxisAlignment: WrapCrossAlignment.center,
      children: children,
    ),
  );
}

class ClassicStatusIndicator extends StatelessWidget {
  final String label;
  final bool completed;
  const ClassicStatusIndicator({
    super.key,
    required this.label,
    this.completed = false,
  });
  @override
  Widget build(BuildContext context) {
    final t = ClassicTheme.of(context);
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(
          completed ? Icons.check_circle_outline : Icons.schedule,
          size: 13,
          color: t.accent,
        ),
        const SizedBox(width: 4),
        Text(
          label,
          style: TextStyle(
            fontSize: 10,
            color: t.textPrimary,
            fontWeight: FontWeight.bold,
          ),
        ),
      ],
    );
  }
}

class ClassicSummaryCard extends StatelessWidget {
  final IconData icon;
  final Widget? leading;
  final String? title, subtitle;
  final Widget? content;
  const ClassicSummaryCard({
    super.key,
    required this.icon,
    this.leading,
    this.title,
    this.subtitle,
    this.content,
  });
  @override
  Widget build(BuildContext context) {
    final t = ClassicTheme.of(context).paper;
    return LayoutBuilder(
      builder: (context, c) {
        final compact = c.maxWidth < 145;
        final text = Column(
          crossAxisAlignment: compact
              ? CrossAxisAlignment.center
              : CrossAxisAlignment.start,
          mainAxisAlignment: MainAxisAlignment.center,
          mainAxisSize: MainAxisSize.min,
          children: [
            if (title != null)
              Text(
                title!,
                textAlign: compact ? TextAlign.center : TextAlign.left,
                style: TextStyle(
                  color: t.textPrimary,
                  fontSize: compact ? 13 : 18,
                  fontWeight: FontWeight.w700,
                  height: 1.04,
                ),
              ),
            if (subtitle != null)
              Text(
                subtitle!,
                textAlign: compact ? TextAlign.center : TextAlign.left,
                style: TextStyle(
                  color: t.textSecondary,
                  fontSize: compact ? 12 : 15,
                  height: 1.12,
                ),
              ),
            if (content != null) Theme(data: t.material(), child: content!),
          ],
        );
        return Container(
          constraints: BoxConstraints(minHeight: compact ? 86 : 68),
          padding: EdgeInsets.symmetric(
            horizontal: compact ? 3 : 12,
            vertical: 8,
          ),
          decoration: BoxDecoration(
            gradient: LinearGradient(colors: [t.surface, t.surfaceAlternate]),
            border: Border.all(color: t.border),
            borderRadius: BorderRadius.circular(5),
          ),
          child: compact
              ? Column(
                  mainAxisSize: MainAxisSize.min,
                  children: [
                    leading ?? Icon(icon, size: 22, color: t.accent),
                    const SizedBox(height: 4),
                    text,
                  ],
                )
              : Row(
                  children: [
                    leading ?? Icon(icon, size: 32, color: t.accent),
                    const SizedBox(width: 10),
                    Expanded(child: text),
                  ],
                ),
        );
      },
    );
  }
}

/// A schematic built from the selected real squad; it never supplies players.
class ClassicTacticalPitch extends StatelessWidget {
  final List<List<Map<String, dynamic>>> lines;
  final Color shirt, ink;
  final ValueChanged<Map<String, dynamic>> onPlayer;
  final double aspectRatio;
  const ClassicTacticalPitch({
    super.key,
    required this.lines,
    required this.shirt,
    required this.ink,
    required this.onPlayer,
    this.aspectRatio = 1.22,
  });
  @override
  Widget build(BuildContext context) => AspectRatio(
    aspectRatio: aspectRatio,
    child: CustomPaint(
      painter: const _PitchPainter(),
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 9),
        child: Column(
          mainAxisAlignment: MainAxisAlignment.spaceAround,
          children: [
            for (final line in lines)
              Expanded(
                child: Row(
                  mainAxisAlignment: MainAxisAlignment.spaceEvenly,
                  children: [
                    for (final p in line)
                      Expanded(
                        child: Tooltip(
                          message: '${p['displayName'] ?? p['name']}',
                          child: InkWell(
                            onTap: () => onPlayer(p),
                            child: Column(
                              mainAxisAlignment: MainAxisAlignment.center,
                              children: [
                                SizedBox(
                                  width: 38,
                                  height: 32,
                                  child: CustomPaint(
                                    painter: _JerseyPainter(
                                      p['position'] == 'G'
                                          ? const Color(0xFFFF981B)
                                          : shirt,
                                    ),
                                    child: Center(
                                      child: Text(
                                        '${p['strength'] ?? '—'}',
                                        style: TextStyle(
                                          color: readableInk(
                                            p['position'] == 'G'
                                                ? const Color(0xFFFF981B)
                                                : shirt,
                                            Colors.white,
                                          ),
                                          fontSize: 13,
                                          height: 1,
                                          fontWeight: FontWeight.w700,
                                        ),
                                      ),
                                    ),
                                  ),
                                ),
                                Container(
                                  padding: const EdgeInsets.symmetric(
                                    horizontal: 2,
                                    vertical: 1,
                                  ),
                                  color: const Color(0xD9002416),
                                  child: Text(
                                    '${p['displayName'] ?? p['name']}'
                                        .replaceFirst('Atleta fictício ', ''),
                                    textAlign: TextAlign.center,
                                    maxLines: 1,
                                    overflow: TextOverflow.ellipsis,
                                    style: const TextStyle(
                                      color: Colors.white,
                                      fontSize: 11,
                                      fontWeight: FontWeight.bold,
                                    ),
                                  ),
                                ),
                              ],
                            ),
                          ),
                        ),
                      ),
                  ],
                ),
              ),
          ],
        ),
      ),
    ),
  );
}

class _PitchPainter extends CustomPainter {
  const _PitchPainter();
  @override
  void paint(Canvas canvas, Size size) {
    final p = Paint();
    for (int i = 0; i < 8; i++) {
      p.color = i.isEven ? const Color(0xFF26783A) : const Color(0xFF2B8240);
      canvas.drawRect(
        Rect.fromLTWH(0, size.height * i / 8, size.width, size.height / 8),
        p,
      );
    }
    p
      ..color = const Color(0xB3F5F7F4)
      ..style = PaintingStyle.stroke
      ..strokeWidth = 1;
    final field = Rect.fromLTWH(8, 6, size.width - 16, size.height - 12);
    canvas.drawRect(field, p);
    canvas.drawLine(
      Offset(8, size.height / 2),
      Offset(size.width - 8, size.height / 2),
      p,
    );
    canvas.drawCircle(
      Offset(size.width / 2, size.height / 2),
      size.height * .14,
      p,
    );
    canvas.drawRect(
      Rect.fromLTWH(size.width * .25, 6, size.width * .5, size.height * .17),
      p,
    );
    canvas.drawRect(
      Rect.fromLTWH(
        size.width * .25,
        size.height * .83 - 6,
        size.width * .5,
        size.height * .17,
      ),
      p,
    );
  }

  @override
  bool shouldRepaint(covariant _PitchPainter oldDelegate) => false;
}

class ClassicPageHeader extends StatelessWidget {
  final String title;
  final String? subtitle;
  final IconData? icon;
  const ClassicPageHeader({
    super.key,
    required this.title,
    this.subtitle,
    this.icon,
  });
  @override
  Widget build(BuildContext context) {
    final t = ClassicTheme.of(context);
    return Container(
      width: double.infinity,
      padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 10),
      decoration: BoxDecoration(
        gradient: t.panelGradient,
        borderRadius: BorderRadius.circular(4),
        border: Border(bottom: BorderSide(color: t.accent, width: 2)),
      ),
      child: Row(
        children: [
          if (icon != null) ...[
            Icon(icon, color: t.selectedBackground, size: 30),
            const SizedBox(width: 12),
          ],
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  title,
                  style: TextStyle(
                    color: t.selectedBackground,
                    fontSize: 24,
                    fontWeight: FontWeight.w800,
                  ),
                ),
                if (subtitle != null)
                  Padding(
                    padding: const EdgeInsets.only(top: 3),
                    child: Text(
                      subtitle!,
                      style: TextStyle(
                        color: readableInk(t.primary, Colors.white),
                        fontSize: 16,
                      ),
                    ),
                  ),
              ],
            ),
          ),
        ],
      ),
    );
  }
}

class _JerseyPainter extends CustomPainter {
  final Color color;
  const _JerseyPainter(this.color);
  @override
  void paint(Canvas canvas, Size size) {
    final shape = Path()
      ..moveTo(8, 1)
      ..lineTo(2, 4)
      ..lineTo(0, 11)
      ..lineTo(6, 13)
      ..lineTo(7, 9)
      ..lineTo(7, 23)
      ..lineTo(21, 23)
      ..lineTo(21, 9)
      ..lineTo(22, 13)
      ..lineTo(28, 11)
      ..lineTo(26, 4)
      ..lineTo(20, 1)
      ..quadraticBezierTo(14, 7, 8, 1)
      ..close();
    canvas.drawPath(shape, Paint()..color = color);
    canvas.drawPath(
      shape,
      Paint()
        ..color = Colors.white
        ..style = PaintingStyle.stroke
        ..strokeWidth = .8,
    );
  }

  @override
  bool shouldRepaint(covariant _JerseyPainter oldDelegate) =>
      oldDelegate.color != color;
}
