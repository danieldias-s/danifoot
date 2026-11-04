import 'package:flutter/material.dart';
import 'club_theme.dart';

/// Same color source as the current presentation, with the classic layout.
ClassicTheme classic98PresentationPalette(ClassicTheme current) {
  final p = current.paper;
  return ClassicTheme(
    background: p.surface,
    surface: p.surface,
    surfaceAlternate: p.surfaceAlternate,
    primary: current.primary,
    secondary: current.secondary,
    textPrimary: p.textPrimary,
    textSecondary: p.textSecondary,
    accent: p.accent,
    border: p.border,
    buttonBackground: current.buttonBackground,
    buttonText: current.buttonText,
    selectedBackground: p.selectedBackground,
    selectedText: p.selectedText,
    tableHeader: p.tableHeader,
    tableRow: p.tableRow,
    tableRowAlternate: p.tableRowAlternate,
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
      fontFamily: 'ClassicCondensed',
      bodyColor: palette.textPrimary,
      displayColor: palette.textPrimary,
    ),
    scaffoldBackgroundColor: palette.surface,
    elevatedButtonTheme: ElevatedButtonThemeData(
      style: palette.material().elevatedButtonTheme.style?.copyWith(
        shape: const WidgetStatePropertyAll(BeveledRectangleBorder()),
        minimumSize: const WidgetStatePropertyAll(Size(44, 44)),
      ),
    ),
    popupMenuTheme: PopupMenuThemeData(
      color: palette.surface,
      shape: const BeveledRectangleBorder(),
      textStyle: TextStyle(
        fontFamily: 'ClassicCondensed',
        color: palette.textPrimary,
        fontSize: 15,
      ),
    ),
  );
}

class RetroBevel extends StatelessWidget {
  final Widget child;
  final Color? color;
  final EdgeInsetsGeometry padding;
  const RetroBevel({
    super.key,
    required this.child,
    this.color,
    this.padding = const EdgeInsets.all(3),
  });
  @override
  Widget build(BuildContext context) => Container(
    padding: padding,
    decoration: BoxDecoration(
      color: color ?? ClassicTheme.of(context).surface,
      border: const Border(
        top: BorderSide(color: Colors.white, width: 2),
        left: BorderSide(color: Colors.white, width: 2),
        bottom: BorderSide(color: Color(0xFF404040), width: 2),
        right: BorderSide(color: Color(0xFF404040), width: 2),
      ),
    ),
    child: child,
  );
}

class RetroButton extends StatelessWidget {
  final String label;
  final VoidCallback? onPressed;
  final IconData? icon;
  final bool compact;
  const RetroButton({
    super.key,
    required this.label,
    this.onPressed,
    this.icon,
    this.compact = false,
  });
  @override
  Widget build(BuildContext context) => Semantics(
    button: true,
    enabled: onPressed != null,
    child: Material(
      color: ClassicTheme.of(context).buttonBackground,
      child: InkWell(
        onTap: onPressed,
        child: RetroBevel(
          color: ClassicTheme.of(context).buttonBackground,
          padding: EdgeInsets.symmetric(
            horizontal: compact ? 3 : 10,
            vertical: 8,
          ),
          child: ConstrainedBox(
            constraints: const BoxConstraints(minHeight: 28),
            child: Row(
              mainAxisSize: MainAxisSize.min,
              mainAxisAlignment: MainAxisAlignment.center,
              children: [
                if (icon != null) ...[
                  Icon(
                    icon,
                    size: 23,
                    color: onPressed == null
                        ? Colors.grey
                        : ClassicTheme.of(context).buttonText,
                  ),
                  const SizedBox(width: 6),
                ],
                Flexible(
                  child: compact
                      ? FittedBox(
                          fit: BoxFit.scaleDown,
                          child: Text(
                            label,
                            style:
                                const TextStyle(
                                  fontSize: 16,
                                  fontWeight: FontWeight.bold,
                                ).copyWith(
                                  color: ClassicTheme.of(context).buttonText,
                                ),
                          ),
                        )
                      : Text(
                          label,
                          textAlign: TextAlign.center,
                          style: TextStyle(
                            fontSize: 18,
                            fontWeight: FontWeight.bold,
                            color: onPressed == null
                                ? const Color(0xFF666666)
                                : ClassicTheme.of(context).buttonText,
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

class RetroTitle extends StatelessWidget {
  final String title;
  final Widget? trailing;
  const RetroTitle({super.key, required this.title, this.trailing});
  @override
  Widget build(BuildContext context) => Container(
    color: ClassicTheme.of(context).primary,
    padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 7),
    child: Row(
      children: [
        const Icon(Icons.sports_soccer, color: Colors.white, size: 24),
        const SizedBox(width: 7),
        Expanded(
          child: Text(
            title,
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
            style: const TextStyle(
              fontFamily: 'ClassicCondensed',
              fontSize: 18,
              fontWeight: FontWeight.bold,
              color: Colors.white,
            ),
          ),
        ),
        ?trailing,
      ],
    ),
  );
}

class RetroSection extends StatelessWidget {
  final String title;
  final Widget child;
  const RetroSection({super.key, required this.title, required this.child});
  @override
  Widget build(BuildContext context) => Container(
    margin: const EdgeInsets.only(bottom: 10),
    decoration: BoxDecoration(
      color: ClassicTheme.of(context).surface,
      border: Border.all(color: ClassicTheme.of(context).border),
    ),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Container(
          color: ClassicTheme.of(context).primary,
          padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 7),
          child: Text(
            title,
            style: TextStyle(
              color: readableInk(
                ClassicTheme.of(context).primary,
                Colors.white,
              ),
              fontSize: 19,
              fontWeight: FontWeight.bold,
            ),
          ),
        ),
        Padding(padding: const EdgeInsets.all(10), child: child),
      ],
    ),
  );
}

/// Flex columns rather than a fixed-width DataTable: no page horizontal scroll.
class RetroTable extends StatelessWidget {
  final List<String> headers;
  final List<List<String>> rows;
  final List<int>? flex;
  final int? selected;
  final void Function(int)? onSelect;
  final Set<int> cyanRows;
  final bool dense;
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
    this.leadingCells = const {},
  });
  @override
  Widget build(BuildContext context) => Container(
    color: ClassicTheme.of(context).surface,
    padding: const EdgeInsets.all(4),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        _line(context, headers, -1, header: true),
        if (rows.isEmpty)
          Padding(
            padding: const EdgeInsets.all(18),
            child: Text(
              'Nenhum registro disponível.',
              style: TextStyle(color: ClassicTheme.of(context).textPrimary),
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
    final background = header
        ? t.tableHeader
        : selected == index
        ? t.selectedBackground
        : index.isEven
        ? t.tableRow
        : t.tableRowAlternate;
    final ink = header
        ? readableInk(background, t.textPrimary)
        : cyanRows.contains(index)
        ? readableOn(const Color(0xFF00BDCF), background)
        : selected == index
        ? t.selectedText
        : t.textPrimary;
    final content = Container(
      constraints: BoxConstraints(
        minHeight: header
            ? 34
            : dense
            ? 30
            : onSelect != null
            ? 44
            : 32,
      ),
      padding: const EdgeInsets.symmetric(horizontal: 5, vertical: 5),
      decoration: BoxDecoration(
        color: background,
        border: Border(bottom: BorderSide(color: t.border)),
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
                            color: Color(0xFF14851F),
                            size: 9,
                          ),
                          const SizedBox(width: 5),
                          Expanded(
                            child: Text(
                              cells[i].substring(2),
                              style: TextStyle(color: ink, fontSize: 16),
                            ),
                          ),
                        ],
                      )
                    : Text(
                        cells[i],
                        style: TextStyle(
                          color: ink,
                          fontSize: 16,
                          fontWeight: header
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
