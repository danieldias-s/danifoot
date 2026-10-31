import 'package:flutter/material.dart';
import 'api_client.dart';

Color? rgbColor(dynamic raw) =>
    raw is String && RegExp(r'^#[0-9a-fA-F]{6}$').hasMatch(raw)
    ? Color(int.parse('FF${raw.substring(1)}', radix: 16))
    : null;
String rgbHex(Color color) =>
    '#${color.toARGB32().toRadixString(16).substring(2).toUpperCase()}';
double colorContrast(Color a, Color b) {
  final x = a.computeLuminance(), y = b.computeLuminance();
  return ((x > y ? x : y) + .05) / ((x < y ? x : y) + .05);
}

Color readableInk(Color background, [Color? preferred]) {
  if (preferred != null && colorContrast(background, preferred) >= 4.5) {
    return preferred;
  }
  return colorContrast(background, Colors.black) >=
          colorContrast(background, Colors.white)
      ? Colors.black
      : Colors.white;
}

Color readableOn(Color requested, Color background) {
  if (colorContrast(requested, background) >= 4.5) return requested;
  final target = readableInk(background);
  for (int i = 1; i <= 100; i++) {
    final color = Color.lerp(requested, target, i / 100)!;
    if (colorContrast(color, background) >= 4.5) return color;
  }
  return target;
}

Color readableSurface(Color requested, Color text) {
  if (colorContrast(requested, text) >= 4.5) return requested;
  final target =
      colorContrast(text, Colors.black) >= colorContrast(text, Colors.white)
      ? Colors.black
      : Colors.white;
  for (int i = 1; i <= 100; i++) {
    final c = Color.lerp(requested, target, i / 100)!;
    if (colorContrast(c, text) >= 4.5) return c;
  }
  return target;
}

@immutable
class ClassicTheme extends ThemeExtension<ClassicTheme> {
  final Color background,
      surface,
      surfaceAlternate,
      primary,
      secondary,
      textPrimary,
      textSecondary,
      accent,
      border,
      buttonBackground,
      buttonText,
      selectedBackground,
      selectedText,
      tableHeader,
      tableRow,
      tableRowAlternate;
  const ClassicTheme({
    required this.background,
    required this.surface,
    required this.surfaceAlternate,
    required this.primary,
    required this.secondary,
    required this.textPrimary,
    required this.textSecondary,
    required this.accent,
    required this.border,
    required this.buttonBackground,
    required this.buttonText,
    required this.selectedBackground,
    required this.selectedText,
    required this.tableHeader,
    required this.tableRow,
    required this.tableRowAlternate,
  });
  static ClassicTheme of(BuildContext context) =>
      Theme.of(context).extension<ClassicTheme>() ?? derive();
  static ClassicTheme derive({
    Map<String, dynamic>? club,
    Map<String, dynamic>? preferences,
  }) {
    final p = preferences ?? {}, mode = p['mode'] ?? 'Club';
    final known =
        club != null &&
        (club['colorVerified'] == true || club['isPersonalized'] == true);
    final useClub = mode == 'Club' && known;
    final primary =
        rgbColor(
          mode == 'Custom'
              ? p['primary']
              : useClub
              ? club['primaryColor']
              : null,
        ) ??
        const Color(0xFF455A64);
    final secondary =
        rgbColor(
          mode == 'Custom'
              ? p['secondary']
              : useClub
              ? club['secondaryColor']
              : null,
        ) ??
        const Color(0xFF22272B);
    var bg =
        rgbColor(mode == 'Custom' ? p['background'] : null) ??
        (useClub
            ? Color.lerp(Colors.black, primary, .25)!
            : const Color(0xFF171B1E));
    final text = mode == 'Custom'
        ? (rgbColor(p['text']) ?? readableInk(bg))
        : readableInk(
            bg,
            rgbColor(useClub ? club['preferredTextColor'] : null),
          );
    bg = readableSurface(bg, text);
    Color panel(double amount) =>
        readableSurface(Color.lerp(bg, primary, amount)!, text);
    final surface = panel(.09), alternate = panel(.17);
    final rawAccent =
        rgbColor(mode == 'Custom' ? p['accent'] : null) ??
        (useClub ? primary : const Color(0xFFFFD700));
    Color across(Color requested) {
      for (int i = 0; i <= 100; i++) {
        final c = Color.lerp(requested, text, i / 100)!;
        if ([bg, surface, alternate].every((b) => colorContrast(c, b) >= 4.5)) {
          return c;
        }
      }
      return text;
    }

    final accent = across(rawAccent);
    final button = readableSurface(primary, text);
    final selected = primary;
    return ClassicTheme(
      background: bg,
      surface: surface,
      surfaceAlternate: alternate,
      primary: primary,
      secondary: secondary,
      textPrimary: text,
      textSecondary: across(Color.lerp(text, bg, .25)!),
      accent: accent,
      border: Color.lerp(accent, surface, .30)!,
      buttonBackground: button,
      buttonText: readableInk(button, text),
      selectedBackground: selected,
      selectedText: readableInk(selected),
      tableHeader: surface,
      tableRow: surface,
      tableRowAlternate: alternate,
    );
  }

  TextStyle get headingStyle => TextStyle(
    color: accent,
    fontWeight: FontWeight.bold,
    fontFamily: 'monospace',
    letterSpacing: .4,
  );
  ThemeData material() {
    final scheme = ColorScheme(
      brightness: textPrimary.computeLuminance() > .5
          ? Brightness.dark
          : Brightness.light,
      primary: accent,
      onPrimary: readableInk(accent),
      secondary: secondary,
      onSecondary: readableInk(secondary),
      error: const Color(0xFFB71C1C),
      onError: Colors.white,
      surface: surface,
      onSurface: textPrimary,
    );
    final textTheme = ThemeData(
      brightness: scheme.brightness,
      fontFamily: 'monospace',
    ).textTheme.apply(bodyColor: textPrimary, displayColor: textPrimary);
    final shape = const RoundedRectangleBorder(borderRadius: BorderRadius.zero);
    return ThemeData(
      useMaterial3: false,
      colorScheme: scheme,
      scaffoldBackgroundColor: background,
      primaryColor: primary,
      fontFamily: 'monospace',
      textTheme: textTheme,
      extensions: [this],
      dividerColor: border,
      appBarTheme: AppBarTheme(
        backgroundColor: surface,
        foregroundColor: textPrimary,
      ),
      dialogTheme: DialogThemeData(
        backgroundColor: surface,
        titleTextStyle: headingStyle,
        contentTextStyle: TextStyle(
          color: textPrimary,
          fontFamily: 'monospace',
        ),
      ),
      elevatedButtonTheme: ElevatedButtonThemeData(
        style: ElevatedButton.styleFrom(
          backgroundColor: buttonBackground,
          foregroundColor: buttonText,
          shape: shape,
          textStyle: const TextStyle(
            fontWeight: FontWeight.bold,
            fontSize: 12,
            fontFamily: 'monospace',
          ),
          padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 8),
        ),
      ),
      outlinedButtonTheme: OutlinedButtonThemeData(
        style: OutlinedButton.styleFrom(
          foregroundColor: accent,
          side: BorderSide(color: border),
          shape: shape,
        ),
      ),
      textButtonTheme: TextButtonThemeData(
        style: TextButton.styleFrom(foregroundColor: accent),
      ),
      inputDecorationTheme: InputDecorationTheme(
        labelStyle: TextStyle(color: textSecondary),
        enabledBorder: UnderlineInputBorder(
          borderSide: BorderSide(color: border),
        ),
      ),
      expansionTileTheme: ExpansionTileThemeData(
        textColor: textPrimary,
        collapsedTextColor: textPrimary,
        iconColor: accent,
        collapsedIconColor: textSecondary,
      ),
    );
  }

  @override
  ClassicTheme copyWith() => this;
  @override
  ClassicTheme lerp(covariant ClassicTheme? other, double t) => other ?? this;
}

class ClubThemeController extends ChangeNotifier {
  Map<String, dynamic>? palette;
  Map<String, dynamic> preferences = {'mode': 'Club', 'version': 0};
  String? ownerId, worldId, clubId;
  String? error;
  int _generation = 0;
  bool loading = false;
  @override
  void dispose() {
    _generation++;
    super.dispose();
  }

  ClassicTheme get theme =>
      ClassicTheme.derive(club: palette, preferences: preferences);
  Future<void> activate(
    ElifootApiClient api,
    String? owner,
    String? world,
    String? club,
    Map<String, dynamic>? colors,
  ) async {
    if (owner == ownerId && world == worldId && club == clubId) {
      if (colors != null && colors.toString() != palette.toString()) {
        palette = colors;
        notifyListeners();
      }
      return;
    }
    final generation = ++_generation;
    ownerId = owner;
    worldId = world;
    clubId = club;
    palette = colors;
    preferences = {'mode': 'Club', 'version': 0};
    error = null;
    notifyListeners();
    if (owner == null || world == null || club == null) return;
    loading = true;
    try {
      final response = await api.getAppearance(owner, world, club);
      if (generation != _generation) return;
      palette = response['clubColors'] is Map
          ? Map<String, dynamic>.from(response['clubColors'] as Map)
          : colors;
      preferences = Map<String, dynamic>.from(
        response['preferences'] as Map? ?? {'mode': 'Club', 'version': 0},
      );
    } catch (_) {
      if (generation == _generation) {
        error = 'Preferência não carregada; usando a paleta disponível.';
      }
    } finally {
      if (generation == _generation) {
        loading = false;
        notifyListeners();
      }
    }
  }

  void preview(Map<String, dynamic> value) {
    preferences = value;
    notifyListeners();
  }

  Future<void> save(ElifootApiClient api, Map<String, dynamic> value) async {
    if (ownerId == null || worldId == null || clubId == null) {
      throw StateError('Selecione um clube para salvar a aparência.');
    }
    final generation = _generation;
    final result = await api.saveAppearance(
      ownerId!,
      worldId!,
      clubId!,
      value,
      (preferences['version'] as num?)?.toInt() ?? 0,
    );
    if (generation != _generation) return;
    preferences = Map<String, dynamic>.from(result['preferences'] as Map);
    error = null;
    notifyListeners();
  }
}

class TeamColorName extends StatelessWidget {
  final String name;
  final Map<String, dynamic>? palette;
  final TextAlign textAlign;
  final bool highlight;
  const TeamColorName({
    super.key,
    required this.name,
    this.palette,
    this.textAlign = TextAlign.left,
    this.highlight = false,
  });
  @override
  Widget build(BuildContext context) {
    final t = ClassicTheme.of(context), p = palette;
    final known =
        p != null &&
        (p['colorVerified'] == true || p['isPersonalized'] == true);
    final primary = rgbColor(known ? p['primaryColor'] : null) ?? t.surface;
    final secondary = rgbColor(known ? p['secondaryColor'] : null);
    final preferred =
        rgbColor(known ? p['preferredTextColor'] : null) ?? secondary;
    final ink = preferred != null && preferred != primary
        ? preferred
        : readableInk(primary);
    final background = readableSurface(primary, ink);
    return Tooltip(
      message: name,
      child: Container(
        key: ValueKey('team-color-$name'),
        padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 3),
        decoration: BoxDecoration(
          color: background,
          border: Border(
            left: BorderSide(color: secondary ?? t.border, width: 3),
          ),
        ),
        child: Text(
          name,
          textAlign: textAlign,
          maxLines: 1,
          overflow: TextOverflow.ellipsis,
          style: TextStyle(
            color: ink,
            fontSize: 12,
            fontWeight: highlight ? FontWeight.bold : FontWeight.normal,
          ),
        ),
      ),
    );
  }
}
