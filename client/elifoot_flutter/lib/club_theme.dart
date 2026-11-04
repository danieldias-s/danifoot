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

  /// 014C rebalance: club colors belong to MATCH areas (team blocks, fixture
  /// lists). `true` paints each team with its registered palette; `false`
  /// shows a neutral block with only a thin identity stripe.
  final bool strongTeamColors;
  final bool lightPanels;
  final bool classicPresentation;
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
    this.strongTeamColors = true,
    this.lightPanels = false,
    this.classicPresentation = false,
  });
  static ClassicTheme of(BuildContext context) =>
      Theme.of(context).extension<ClassicTheme>() ?? derive();

  /// Global app theme selection (the APP THEME concept):
  /// - `Classic` / `Club` / `Neutral` (default): fixed "Liga Clássica 98"
  ///   green. `Club` is the legacy default persisted by 014C saves; since the
  ///   014C rebalance it no longer paints the whole interface with club colors.
  /// - `Custom` (advanced): user-chosen colors for the whole interface.
  /// - `ClubGlobal` (advanced, legacy 014C behavior): coach club colors on the
  ///   whole interface. Never the default.
  /// Club palettes keep feeding MATCH TEAM COLORS independently of this mode.
  static ClassicTheme derive({
    Map<String, dynamic>? club,
    Map<String, dynamic>? preferences,
  }) {
    final p = preferences ?? {};
    final mode = (p['mode'] ?? 'Classic').toString();
    final strong = p['matchColors']?.toString() != 'Neutral';
    if (mode != 'Custom' && mode != 'ClubGlobal') {
      return classicGreen(
        variant: p['themeVariant']?.toString(),
        brightness: (p['brightness'] as num?)?.toInt() ?? 0,
        strongTeamColors: strong,
      );
    }
    return _dynamic(club, p, mode == 'Custom' ? 'Custom' : 'Club', strong);
  }

  /// Fixed classic green theme (reference "Liga Clássica 98"), with optional
  /// variant (`Classico`, `Escuro`, `AltoContraste`) and brightness -2..2.
  static ClassicTheme classicGreen({
    String? variant,
    int brightness = 0,
    bool strongTeamColors = true,
  }) {
    final level = brightness.clamp(-2, 2);
    const white = Colors.white;
    final (
      Color bg,
      Color surf,
      Color alt,
      Color prim,
      Color sec,
      Color border,
      Color button,
      Color accentRaw,
      Color textSec,
    ) = switch (variant) {
      'Escuro' => (
        const Color(0xFF062A10),
        const Color(0xFF0A2512),
        const Color(0xFF0D2E16),
        const Color(0xFF041F0B),
        const Color(0xFF03180A),
        const Color(0xFF2E6B3D),
        const Color(0xFF0F5A24),
        const Color(0xFFFFD400),
        const Color(0xFFB8D8BC),
      ),
      'AltoContraste' => (
        const Color(0xFF003300),
        const Color(0xFF000000),
        const Color(0xFF0A1A0A),
        const Color(0xFF000000),
        const Color(0xFF000000),
        const Color(0xFFFFFF00),
        const Color(0xFF006400),
        const Color(0xFFFFFF00),
        const Color(0xFFFFFFFF),
      ),
      _ => (
        const Color(0xFF002E20),
        const Color(0xFF00452B),
        const Color(0xFF064E32),
        const Color(0xFF003923),
        const Color(0xFF002719),
        const Color(0xFF3D7250),
        const Color(0xFF00883C),
        const Color(0xFFFFD400),
        const Color(0xFFC8E6C9),
      ),
    };
    Color tone(Color c) {
      if (level == 0) return c;
      final shifted = level > 0
          ? Color.lerp(c, white, level * .07)!
          : Color.lerp(c, Colors.black, -level * .25)!;
      return readableSurface(shifted, white);
    }

    final background = tone(bg), surface = tone(surf), alternate = tone(alt);
    final primary = tone(prim), secondary = tone(sec), buttonBg = tone(button);
    Color across(Color requested) {
      for (int i = 0; i <= 100; i++) {
        final c = Color.lerp(requested, white, i / 100)!;
        if ([
          background,
          surface,
          alternate,
          secondary,
        ].every((b) => colorContrast(c, b) >= 4.5)) {
          return c;
        }
      }
      return white;
    }

    final accent = across(accentRaw);
    return ClassicTheme(
      background: background,
      surface: surface,
      surfaceAlternate: alternate,
      primary: primary,
      secondary: secondary,
      textPrimary: white,
      textSecondary: across(textSec),
      accent: accent,
      border: tone(border),
      buttonBackground: buttonBg,
      buttonText: readableInk(buttonBg, white),
      selectedBackground: accent,
      selectedText: readableInk(accent),
      tableHeader: secondary,
      tableRow: surface,
      tableRowAlternate: alternate,
      strongTeamColors: strongTeamColors,
      lightPanels: variant != 'Escuro' && variant != 'AltoContraste',
    );
  }

  /// Advanced/legacy dynamic theme: `Custom` colors or (`Club`) the coach club
  /// palette applied to the whole interface (pre-rebalance 014C behavior).
  static ClassicTheme _dynamic(
    Map<String, dynamic>? club,
    Map<String, dynamic> p,
    String mode,
    bool strongTeamColors,
  ) {
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
        (useClub ? primary : const Color(0xFFFFD400));
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
      strongTeamColors: strongTeamColors,
    );
  }

  ClassicTheme get paper => !lightPanels
      ? this
      : ClassicTheme(
          background: background,
          surface: const Color(0xFFF1F4F2),
          surfaceAlternate: const Color(0xFFDFEAE2),
          primary: primary,
          secondary: secondary,
          textPrimary: const Color(0xFF102A1C),
          textSecondary: const Color(0xFF314B3C),
          accent: const Color(0xFF00512E),
          border: const Color(0xFFA1BDB1),
          buttonBackground: buttonBackground,
          buttonText: buttonText,
          selectedBackground: selectedBackground,
          selectedText: selectedText,
          tableHeader: const Color(0xFFDFE6E1),
          tableRow: const Color(0xFFF5F7F5),
          tableRowAlternate: const Color(0xFFE0EAE2),
          strongTeamColors: strongTeamColors,
        );

  TextStyle get headingStyle => TextStyle(
    color: accent,
    fontWeight: FontWeight.bold,
    fontFamily: 'ClassicCondensed',
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
      fontFamily: 'ClassicCondensed',
    ).textTheme.apply(bodyColor: textPrimary, displayColor: textPrimary);
    final shape = RoundedRectangleBorder(
      borderRadius: BorderRadius.circular(5),
    );
    return ThemeData(
      useMaterial3: false,
      colorScheme: scheme,
      scaffoldBackgroundColor: background,
      primaryColor: primary,
      fontFamily: 'ClassicCondensed',
      textTheme: textTheme.copyWith(
        bodyMedium: textTheme.bodyMedium?.copyWith(fontSize: 16),
        bodySmall: textTheme.bodySmall?.copyWith(fontSize: 14),
      ),
      extensions: [this],
      visualDensity: VisualDensity.compact,
      dataTableTheme: DataTableThemeData(
        headingRowColor: WidgetStatePropertyAll(tableHeader),
        headingRowHeight: 36,
        dataRowMinHeight: 34,
        dataRowMaxHeight: 44,
        columnSpacing: 16,
        horizontalMargin: 12,
      ),
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
          fontFamily: 'ClassicCondensed',
        ),
      ),
      elevatedButtonTheme: ElevatedButtonThemeData(
        style: ElevatedButton.styleFrom(
          backgroundColor: buttonBackground,
          foregroundColor: buttonText,
          shape: shape,
          textStyle: const TextStyle(
            fontWeight: FontWeight.bold,
            fontSize: 16,
            fontFamily: 'ClassicCondensed',
          ),
          minimumSize: const Size(44, 44),
          padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 9),
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
        isDense: true,
        filled: true,
        fillColor: surfaceAlternate,
        contentPadding: const EdgeInsets.symmetric(
          horizontal: 10,
          vertical: 12,
        ),
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

/// Read-only presentation fallback for saves that predate catalog palettes.
/// Registered legacy colors are not promoted to catalog-verified colors.
Map<String, dynamic>? clubMatchPalette(Map? club) {
  if (club == null) return null;
  final palette = club['colorPalette'] is Map
      ? Map<String, dynamic>.from(club['colorPalette'])
      : null;
  if (palette != null &&
      (palette['colorVerified'] == true || palette['isPersonalized'] == true) &&
      rgbColor(palette['primaryColor']) != null) {
    return palette;
  }
  if (rgbColor(club['primaryColorHex']) == null) return palette;
  return {
    'primaryColor': club['primaryColorHex'],
    if (rgbColor(club['secondaryColorHex']) != null)
      'secondaryColor': club['secondaryColorHex'],
    'registeredClubColors': true,
  };
}

/// Preserve the registered background; adjust only rendered text for contrast.
({Color background, Color ink}) matchTeamColors(
  ClassicTheme theme,
  Map? palette,
) {
  final known =
      palette != null &&
      (palette['colorVerified'] == true ||
          palette['isPersonalized'] == true ||
          palette['registeredClubColors'] == true);
  final primary = rgbColor(known ? palette['primaryColor'] : null);
  if (!theme.strongTeamColors || primary == null) {
    return (
      background: theme.surface,
      ink: readableInk(theme.surface, theme.textPrimary),
    );
  }
  final secondary = rgbColor(palette?['secondaryColor']);
  return (
    background: primary,
    ink: secondary == null
        ? readableInk(primary)
        : readableOn(secondary, primary),
  );
}

class ClubThemeController extends ChangeNotifier {
  Map<String, dynamic>? palette;
  Map<String, dynamic> preferences = {'mode': 'Club', 'version': 0};
  String? ownerId, worldId, clubId;
  String? error;
  int _generation = 0;
  bool loading = false;
  bool _disposed = false;
  bool get isActive => !_disposed;
  @override
  void dispose() {
    _generation++;
    _disposed = true;
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
  final int maxLines;
  final double fontSize;
  const TeamColorName({
    super.key,
    required this.name,
    this.palette,
    this.textAlign = TextAlign.left,
    this.highlight = false,
    this.maxLines = 1,
    this.fontSize = 12,
  });
  @override
  Widget build(BuildContext context) {
    final t = ClassicTheme.of(context), p = palette;
    final known =
        p != null &&
        (p['colorVerified'] == true ||
            p['isPersonalized'] == true ||
            p['registeredClubColors'] == true);
    final teamPrimary = rgbColor(known ? p['primaryColor'] : null);
    final secondary = rgbColor(known ? p['secondaryColor'] : null);
    // Neutral match mode keeps only a thin identity stripe in the team color.
    final strong = t.strongTeamColors && teamPrimary != null;
    final primary = strong ? teamPrimary : t.surface;
    final preferred = strong
        ? (rgbColor(p!['preferredTextColor']) ?? secondary)
        : t.textPrimary;
    final ink = preferred != null && preferred != primary
        ? preferred
        : readableInk(primary);
    final classic = matchTeamColors(t, p);
    final background = t.classicPresentation
        ? classic.background
        : readableSurface(primary, ink);
    final textInk = t.classicPresentation ? classic.ink : ink;
    final stripe = strong ? (secondary ?? t.border) : (teamPrimary ?? t.border);
    return Tooltip(
      message: name,
      child: Container(
        key: ValueKey('team-color-$name'),
        padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 3),
        decoration: BoxDecoration(
          color: background,
          border: Border(left: BorderSide(color: stripe, width: 3)),
        ),
        child: Text(
          name,
          textAlign: textAlign,
          maxLines: maxLines,
          overflow: TextOverflow.ellipsis,
          style: TextStyle(
            color: textInk,
            fontSize: fontSize,
            fontWeight: highlight ? FontWeight.bold : FontWeight.normal,
          ),
        ),
      ),
    );
  }
}
