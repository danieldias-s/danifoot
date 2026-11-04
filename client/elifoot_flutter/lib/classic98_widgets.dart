import 'package:flutter/material.dart';
import 'club_theme.dart';

/// Presentation palette only. Never written to club colors or world state.
const classic98Palette = ClassicTheme(
  background: Color(0xFFFFCC00),
  surface: Color(0xFFFFD52A),
  surfaceAlternate: Color(0xFFFFE894),
  primary: Color(0xFFC0C0C0),
  secondary: Color(0xFF004080),
  textPrimary: Colors.black,
  textSecondary: Color(0xFF303030),
  accent: Colors.black,
  border: Color(0xFF77704A),
  buttonBackground: Color(0xFFC0C0C0),
  buttonText: Colors.black,
  selectedBackground: Color(0xFF000080),
  selectedText: Colors.white,
  tableHeader: Colors.black,
  tableRow: Color(0xFFFFE075),
  tableRowAlternate: Color(0xFFFFE894),
);

ThemeData classic98Theme() => classic98Palette.material().copyWith(
  textTheme: ThemeData.light().textTheme.apply(
    fontFamily: 'ClassicCondensed',
    bodyColor: Colors.black,
    displayColor: Colors.black,
  ),
  scaffoldBackgroundColor: const Color(0xFFFFCC00),
  elevatedButtonTheme: ElevatedButtonThemeData(
    style: classic98Palette.material().elevatedButtonTheme.style?.copyWith(
      shape: const WidgetStatePropertyAll(BeveledRectangleBorder()),
      minimumSize: const WidgetStatePropertyAll(Size(44, 44)),
    ),
  ),
  popupMenuTheme: const PopupMenuThemeData(
    color: Color(0xFFC0C0C0),
    shape: BeveledRectangleBorder(),
    textStyle: TextStyle(
      fontFamily: 'ClassicCondensed',
      color: Colors.black,
      fontSize: 15,
    ),
  ),
);

class RetroBevel extends StatelessWidget {
  final Widget child;
  final Color color;
  final EdgeInsetsGeometry padding;
  const RetroBevel({
    super.key,
    required this.child,
    this.color = const Color(0xFFC0C0C0),
    this.padding = const EdgeInsets.all(3),
  });
  @override
  Widget build(BuildContext context) => Container(
    padding: padding,
    decoration: BoxDecoration(
      color: color,
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
      color: const Color(0xFFC0C0C0),
      child: InkWell(
        onTap: onPressed,
        child: RetroBevel(
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
                    color: onPressed == null ? Colors.grey : Colors.black,
                  ),
                  const SizedBox(width: 6),
                ],
                Flexible(
                  child: compact
                      ? FittedBox(
                          fit: BoxFit.scaleDown,
                          child: Text(
                            label,
                            style: const TextStyle(
                              fontSize: 16,
                              fontWeight: FontWeight.bold,
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
                                : Colors.black,
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
    color: const Color(0xFF073B94),
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
    padding: const EdgeInsets.all(10),
    decoration: BoxDecoration(
      border: Border.all(color: const Color(0xFF8B7928)),
    ),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Text(
          title,
          style: const TextStyle(fontSize: 19, fontWeight: FontWeight.bold),
        ),
        const SizedBox(height: 8),
        child,
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
  const RetroTable({
    super.key,
    required this.headers,
    required this.rows,
    this.flex,
    this.selected,
    this.onSelect,
    this.cyanRows = const {},
    this.dense = false,
  });
  @override
  Widget build(BuildContext context) => Container(
    color: Colors.black,
    padding: const EdgeInsets.all(4),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        _line(headers, -1, header: true),
        if (rows.isEmpty)
          const Padding(
            padding: EdgeInsets.all(18),
            child: Text(
              'Nenhum registro disponível.',
              style: TextStyle(color: Color(0xFFFFE13B)),
            ),
          ),
        for (var i = 0; i < rows.length; i++) _line(rows[i], i),
      ],
    ),
  );
  Widget _line(List<String> cells, int index, {bool header = false}) {
    final ink = selected == index && !header
        ? Colors.black
        : cyanRows.contains(index)
        ? const Color(0xFF6CF5FF)
        : const Color(0xFFFFE13B);
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
        color: selected == index && !header
            ? const Color(0xFFFFE13B)
            : Colors.black,
        border: Border(
          bottom: BorderSide(
            color: header ? const Color(0xFFFFE13B) : const Color(0xFF39351C),
          ),
        ),
      ),
      child: Row(
        children: [
          for (var i = 0; i < cells.length; i++)
            Expanded(
              flex: flex?[i] ?? 1,
              child: Padding(
                padding: const EdgeInsets.only(right: 4),
                child: i == 0 && cells[i].startsWith('● ')
                    ? Row(
                        children: [
                          const Icon(
                            Icons.circle,
                            color: Color(0xFF32DD32),
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
