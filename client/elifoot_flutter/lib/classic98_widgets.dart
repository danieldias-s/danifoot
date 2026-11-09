import 'package:flutter/material.dart';
import 'club_theme.dart';

/// Official global visual tokens for VERSÃO CLÁSSICA (ANTIGA).
abstract final class ClassicColors {
  static const Color outerGreen = Color(0xFF0D3B2E);
  static const Color titleBarBlue = Color(0xFF123A87);
  static const Color titleBarBlueLight = Color(0xFF1E4CA1);
  static const Color classicBlue = Color(0xFF244264);
  static const Color cream = Color(0xFFF0E8D8);
  static const Color creamLight = Color(0xFFF8F3E9);
  static const Color creamBox = Color(0xFFE4DEC9);
  static const Color creamRowAlt = Color(0xFFEDE4D1);
  static const Color retroGray = Color(0xFFD6D4CA);
  static const Color rosterBlack = Color(0xFF111A17);
  static const Color rosterRowAlt = Color(0xFF172420);
  static const Color rosterBorder = Color(0xFF2B3A34);
  static const Color rosterText = Color(0xFFF8F3E9);
  static const Color softGold = Color(0xFFD9B65B);
  static const Color actionGreen = Color(0xFF006B38);
  static const Color starterGreen = Color(0xFF24B52E);
  static const Color moraleGreen = Color(0xFF2E9B33);
  static const Color border = Color(0xFF8C8574);
  static const Color bevelLight = Color(0xFFFFFFFF);
  static const Color bevelDark = Color(0xFF404040);
}

/// Official typography tokens for VERSÃO CLÁSSICA (ANTIGA).
abstract final class ClassicTypography {
  static const String fontFamily = 'ClassicCondensed';

  static const TextStyle windowTitle = TextStyle(
    fontFamily: fontFamily,
    fontSize: 18,
    fontWeight: FontWeight.bold,
    color: Colors.white,
    height: 1.1,
  );

  static const TextStyle sectionTitle = TextStyle(
    fontFamily: fontFamily,
    fontSize: 19,
    fontWeight: FontWeight.bold,
    color: Colors.white,
  );

  static const TextStyle menuLabel = TextStyle(
    fontFamily: fontFamily,
    fontSize: 16,
    fontWeight: FontWeight.bold,
    color: ClassicColors.rosterBlack,
  );
}

/// Standardized Classic 98 presentation palette (cream window, blue headers,
/// dark-green outer backdrop, soft-gold table headers, and green actions).
ClassicTheme classic98PresentationPalette(ClassicTheme current) {
  return ClassicTheme(
    background: ClassicColors.cream,
    surface: ClassicColors.creamLight,
    surfaceAlternate: ClassicColors.creamBox,
    primary: ClassicColors.titleBarBlue,
    secondary: ClassicColors.outerGreen,
    textPrimary: ClassicColors.rosterBlack,
    textSecondary: const Color(0xFF35423C),
    accent: ClassicColors.titleBarBlue,
    border: ClassicColors.border,
    buttonBackground: ClassicColors.actionGreen,
    buttonText: Colors.white,
    selectedBackground: ClassicColors.softGold,
    selectedText: ClassicColors.rosterBlack,
    tableHeader: ClassicColors.softGold,
    tableRow: ClassicColors.creamLight,
    tableRowAlternate: ClassicColors.creamRowAlt,
    strongTeamColors: current.strongTeamColors,
    classicPresentation: true,
  );
}

final classic98Palette = classic98PresentationPalette(ClassicTheme.derive());

ThemeData classic98Theme({ClassicTheme? current}) {
  final palette = current == null
      ? classic98Palette
      : classic98PresentationPalette(current);
  return palette.material().copyWith(
    textTheme: ThemeData.light().textTheme.apply(
      fontFamily: ClassicTypography.fontFamily,
      bodyColor: palette.textPrimary,
      displayColor: palette.textPrimary,
    ),
    scaffoldBackgroundColor: palette.background,
    canvasColor: palette.surface,
    cardColor: palette.surface,
    dividerColor: palette.border,
  );
}

class RetroBevel extends StatelessWidget {
  final Widget child;
  final Color? color;
  final EdgeInsetsGeometry padding;
  final bool sunken;
  const RetroBevel({
    super.key,
    required this.child,
    this.color,
    this.padding = const EdgeInsets.all(3),
    this.sunken = false,
  });
  @override
  Widget build(BuildContext context) {
    final topLeft = sunken
        ? ClassicColors.bevelDark
        : ClassicColors.bevelLight;
    final bottomRight = sunken
        ? ClassicColors.bevelLight
        : ClassicColors.bevelDark;
    return Container(
      padding: padding,
      decoration: BoxDecoration(
        color: color ?? ClassicTheme.of(context).background,
        border: Border(
          top: BorderSide(color: topLeft, width: 2),
          left: BorderSide(color: topLeft, width: 2),
          bottom: BorderSide(color: bottomRight, width: 2),
          right: BorderSide(color: bottomRight, width: 2),
        ),
      ),
      child: child,
    );
  }
}

class RetroButton extends StatelessWidget {
  final String label;
  final VoidCallback? onPressed;
  final IconData? icon;
  final bool compact;
  final bool stackIconOnCompact;
  final Color? backgroundColor;
  final Color? foregroundColor;
  const RetroButton({
    super.key,
    required this.label,
    this.onPressed,
    this.icon,
    this.compact = false,
    this.stackIconOnCompact = false,
    this.backgroundColor,
    this.foregroundColor,
  });
  @override
  Widget build(BuildContext context) {
    final bg = backgroundColor ?? ClassicTheme.of(context).buttonBackground;
    final fg = foregroundColor ?? ClassicTheme.of(context).buttonText;
    final disabledFg = const Color(0xFF9E9E9E);
    return Semantics(
      button: true,
      enabled: onPressed != null,
      child: Material(
        color: bg,
        child: InkWell(
          onTap: onPressed,
          child: RetroBevel(
            color: bg,
            padding: EdgeInsets.symmetric(
              horizontal: compact ? 3 : 10,
              vertical: compact && stackIconOnCompact ? 5 : 8,
            ),
            child: ConstrainedBox(
              constraints: BoxConstraints(
                minHeight: compact && stackIconOnCompact ? 42 : 28,
              ),
              child: compact && stackIconOnCompact && icon != null
                  ? Column(
                      mainAxisSize: MainAxisSize.min,
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        Icon(
                          icon,
                          size: 18,
                          color: onPressed == null ? disabledFg : fg,
                        ),
                        const SizedBox(height: 2),
                        FittedBox(
                          fit: BoxFit.scaleDown,
                          child: Text(
                            label,
                            textAlign: TextAlign.center,
                            style: TextStyle(
                              fontFamily: ClassicTypography.fontFamily,
                              fontSize: 13,
                              fontWeight: FontWeight.bold,
                              color: onPressed == null ? disabledFg : fg,
                            ),
                          ),
                        ),
                      ],
                    )
                  : Row(
                      mainAxisSize: MainAxisSize.min,
                      mainAxisAlignment: MainAxisAlignment.center,
                      children: [
                        if (icon != null) ...[
                          Icon(
                            icon,
                            size: compact ? 18 : 21,
                            color: onPressed == null ? disabledFg : fg,
                          ),
                          const SizedBox(width: 6),
                        ],
                        Flexible(
                          child: compact
                              ? FittedBox(
                                  fit: BoxFit.scaleDown,
                                  child: Text(
                                    label,
                                    style: TextStyle(
                                      fontFamily: ClassicTypography.fontFamily,
                                      fontSize: 15,
                                      fontWeight: FontWeight.bold,
                                      color: onPressed == null
                                          ? disabledFg
                                          : fg,
                                    ),
                                  ),
                                )
                              : Text(
                                  label,
                                  textAlign: TextAlign.center,
                                  style: TextStyle(
                                    fontFamily: ClassicTypography.fontFamily,
                                    fontSize: 17,
                                    fontWeight: FontWeight.bold,
                                    color: onPressed == null ? disabledFg : fg,
                                  ),
                                ),
                        ),
                      ],
                    ),
            ),
          ),
        ),
      ),
    );
  }
}

class RetroWindowControlBox extends StatelessWidget {
  final String symbol;
  const RetroWindowControlBox({super.key, required this.symbol});

  @override
  Widget build(BuildContext context) => Container(
    width: 22,
    height: 20,
    margin: const EdgeInsets.only(left: 3),
    alignment: Alignment.center,
    decoration: const BoxDecoration(
      color: ClassicColors.retroGray,
      border: Border(
        top: BorderSide(color: ClassicColors.bevelLight, width: 1.5),
        left: BorderSide(color: ClassicColors.bevelLight, width: 1.5),
        bottom: BorderSide(color: ClassicColors.bevelDark, width: 1.5),
        right: BorderSide(color: ClassicColors.bevelDark, width: 1.5),
      ),
    ),
    child: Text(
      symbol,
      style: const TextStyle(
        fontFamily: ClassicTypography.fontFamily,
        fontSize: 12,
        height: 1,
        fontWeight: FontWeight.w900,
        color: ClassicColors.rosterBlack,
      ),
    ),
  );
}

class RetroTitle extends StatelessWidget {
  final String title;
  final String? subtitle;
  final Widget? trailing;
  final Color? backgroundColor;
  final bool showWindowControls;
  const RetroTitle({
    super.key,
    required this.title,
    this.subtitle,
    this.trailing,
    this.backgroundColor,
    this.showWindowControls = false,
  });
  @override
  Widget build(BuildContext context) {
    final baseColor = backgroundColor ?? ClassicTheme.of(context).primary;
    return Container(
      decoration: BoxDecoration(
        gradient: LinearGradient(
          colors: [
            baseColor,
            Color.lerp(baseColor, ClassicColors.titleBarBlueLight, 0.55)!,
          ],
        ),
      ),
      padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
      child: Row(
        children: [
          const Icon(Icons.sports_soccer, color: Colors.white, size: 22),
          const SizedBox(width: 8),
          Expanded(
            child: subtitle == null
                ? Text(
                    title,
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                    style: ClassicTypography.windowTitle,
                  )
                : Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Text(
                        title,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: ClassicTypography.windowTitle.copyWith(
                          fontSize: 16,
                        ),
                      ),
                      Text(
                        subtitle!,
                        maxLines: 1,
                        overflow: TextOverflow.ellipsis,
                        style: const TextStyle(
                          fontFamily: ClassicTypography.fontFamily,
                          fontSize: 13,
                          fontWeight: FontWeight.w600,
                          color: ClassicColors.creamLight,
                          height: 1.05,
                        ),
                      ),
                    ],
                  ),
          ),
          ?trailing,
          if (showWindowControls) ...[
            const SizedBox(width: 8),
            const RetroWindowControlBox(symbol: '—'),
            const RetroWindowControlBox(symbol: '□'),
            const RetroWindowControlBox(symbol: '×'),
          ],
        ],
      ),
    );
  }
}

class RetroSection extends StatelessWidget {
  final String title;
  final Widget child;
  final EdgeInsetsGeometry padding;
  const RetroSection({
    super.key,
    required this.title,
    required this.child,
    this.padding = const EdgeInsets.all(10),
  });
  @override
  Widget build(BuildContext context) => Container(
    margin: const EdgeInsets.only(bottom: 10),
    decoration: BoxDecoration(
      color: ClassicTheme.of(context).surface,
      border: Border.all(color: ClassicTheme.of(context).border, width: 1.2),
    ),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Container(
          decoration: BoxDecoration(
            gradient: LinearGradient(
              colors: [
                ClassicTheme.of(context).primary,
                ClassicColors.classicBlue,
              ],
            ),
          ),
          padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 6),
          child: Text(
            title,
            style: TextStyle(
              color: readableInk(
                ClassicTheme.of(context).primary,
                Colors.white,
              ),
              fontSize: 18,
              fontWeight: FontWeight.bold,
            ),
          ),
        ),
        Padding(padding: padding, child: child),
      ],
    ),
  );
}

/// Flex columns rather than a fixed-width DataTable: no page horizontal scroll.
/// Supports `darkRoster: true` for the signature greenish-black player list
/// with soft-gold header and cream/white rows.
class RetroTable extends StatelessWidget {
  final List<String> headers;
  final List<List<String>> rows;
  final List<int>? flex;
  final int? selected;
  final void Function(int)? onSelect;
  final Set<int> cyanRows;
  final bool dense;
  final bool darkRoster;
  final Set<int> rightAlignedColumns;
  final Set<int> centerAlignedColumns;
  final Map<int, Widget> leadingCells;
  const RetroTable({
    super.key,
    required this.headers,
    required this.rows,
    this.flex,
    this.selected,
    this.onSelect,
    this.cyanRows = const {},
    this.dense = false,
    this.darkRoster = false,
    this.rightAlignedColumns = const {},
    this.centerAlignedColumns = const {},
    this.leadingCells = const {},
  });
  @override
  Widget build(BuildContext context) => Container(
    decoration: BoxDecoration(
      color: darkRoster
          ? ClassicColors.rosterBlack
          : ClassicTheme.of(context).surface,
      border: Border.all(color: ClassicColors.border, width: 1.5),
    ),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        _line(context, headers, -1, header: true),
        if (rows.isEmpty)
          Padding(
            padding: const EdgeInsets.all(18),
            child: Text(
              'Nenhum registro disponível.',
              style: TextStyle(
                color: darkRoster
                    ? ClassicColors.rosterText
                    : ClassicTheme.of(context).textPrimary,
              ),
            ),
          ),
        for (var i = 0; i < rows.length; i++) _line(context, rows[i], i),
      ],
    ),
  );
  Widget _line(
    BuildContext context,
    List<String> cells,
    int index, {
    bool header = false,
  }) {
    final t = ClassicTheme.of(context);
    final isSelected = !header && selected == index;
    final background = header
        ? ClassicColors.softGold
        : isSelected
        ? (darkRoster ? const Color(0xFF284237) : t.selectedBackground)
        : darkRoster
        ? (index.isEven
              ? ClassicColors.rosterBlack
              : ClassicColors.rosterRowAlt)
        : (index.isEven ? t.tableRow : t.tableRowAlternate);
    final ink = header
        ? ClassicColors.rosterBlack
        : cyanRows.contains(index)
        ? readableOn(
            darkRoster ? ClassicColors.softGold : ClassicColors.titleBarBlue,
            background,
          )
        : darkRoster
        ? (isSelected ? ClassicColors.softGold : ClassicColors.rosterText)
        : (isSelected ? t.selectedText : t.textPrimary);
    final dividerColor = darkRoster
        ? (isSelected ? ClassicColors.softGold : ClassicColors.rosterBorder)
        : t.border;
    final content = Container(
      constraints: BoxConstraints(
        minHeight: header
            ? 32
            : dense
            ? 30
            : onSelect != null
            ? (darkRoster ? 34 : 42)
            : 32,
      ),
      padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 3),
      decoration: BoxDecoration(
        color: background,
        border: Border(
          bottom: BorderSide(color: dividerColor, width: isSelected ? 1.4 : 1),
        ),
      ),
      child: Row(
        children: [
          for (var i = 0; i < cells.length; i++)
            Expanded(
              flex: flex?[i] ?? 1,
              child: Padding(
                padding: const EdgeInsets.only(right: 4),
                child: !header && i == 0 && leadingCells.containsKey(index)
                    ? leadingCells[index]!
                    : i == 0 && cells[i].startsWith('● ')
                    ? Row(
                        children: [
                          const Icon(
                            Icons.circle,
                            color: ClassicColors.starterGreen,
                            size: 10,
                          ),
                          const SizedBox(width: 5),
                          Expanded(
                            child: Text(
                              cells[i].substring(2),
                              style: TextStyle(color: ink, fontSize: 15),
                            ),
                          ),
                        ],
                      )
                    : Text(
                        cells[i],
                        maxLines: darkRoster ? 1 : 2,
                        overflow: TextOverflow.ellipsis,
                        textAlign: rightAlignedColumns.contains(i)
                            ? TextAlign.right
                            : centerAlignedColumns.contains(i)
                            ? TextAlign.center
                            : TextAlign.left,
                        style: TextStyle(
                          color: ink,
                          fontSize: header ? 15 : 15.5,
                          fontWeight: header || isSelected
                              ? FontWeight.bold
                              : FontWeight.w500,
                        ),
                      ),
              ),
            ),
        ],
      ),
    );
    return onSelect != null && !header
        ? Semantics(
            selected: selected == index,
            button: true,
            child: InkWell(onTap: () => onSelect!(index), child: content),
          )
        : content;
  }
}
