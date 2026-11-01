import 'package:elifoot_flutter/appearance_settings.dart';
import 'package:elifoot_flutter/club_theme.dart';
import 'package:elifoot_flutter/main.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'complete_gameplay_011_test.dart' show FakeGameplay011ApiClient;

Map<String, dynamic> palette(String primary, [String? secondary]) => {
  'primaryColor': primary,
  'secondaryColor': secondary,
  'preferredTextColor': secondary,
  'colorVerified': true,
  'colorSource': 'Reviewed fixture',
};

class ThemedApi extends FakeGameplay011ApiClient {
  final prefs = <String, Map<String, dynamic>>{};
  int writes = 0;
  Map<String, dynamic> colors(String club) => club == 'club-2'
      ? palette('#0000FF', '#FFFFFF')
      : palette('#C3281E', '#000000');
  @override
  Future<Map<String, dynamic>> getClubDetail(String world, String club) async =>
      {...await super.getClubDetail(world, club), 'colorPalette': colors(club)};
  @override
  Future<Map<String, dynamic>> getAppearance(
    String owner,
    String world,
    String club,
  ) async => {
    'worldId': world,
    'clubId': club,
    'clubColors': colors(club),
    'preferences':
        prefs['$owner:$world:$club'] ?? {'mode': 'Club', 'version': 0},
  };
  @override
  Future<Map<String, dynamic>> saveAppearance(
    String owner,
    String world,
    String club,
    Map<String, dynamic> p,
    int version,
  ) async {
    final key = '$owner:$world:$club';
    expect(prefs[key]?['version'] ?? 0, version);
    writes++;
    prefs[key] = {...p, 'version': version + 1};
    return getAppearance(owner, world, club);
  }
}

Future<void> join(WidgetTester tester, ThemedApi api) async {
  await tester.pumpWidget(ElifootClassicApp(apiClient: api));
  await tester.tap(find.text('ENTRAR NO LOBBY (LOCAL DEV)'));
  await tester.pumpAndSettle();
  await tester.tap(find.text('ENTRAR COM CLUBE'));
  await tester.pumpAndSettle();
  await tester.tap(find.byKey(const Key('choose-club-club-1')));
  await tester.pumpAndSettle();
}

void main() {
  testWidgets(
    'Open appearance draft cannot overwrite another club after a live job change',
    (tester) async {
      final api = ThemedApi(), controller = ClubThemeController();
      await controller.activate(
        api,
        'owner',
        'world',
        'club-1',
        api.colors('club-1'),
      );
      await tester.pumpWidget(
        ListenableBuilder(
          listenable: controller,
          builder: (context, _) => MaterialApp(
            theme: controller.theme.material(),
            home: AppearanceSettings(api: api, controller: controller),
          ),
        ),
      );
      await tester.tap(find.text('OPÇÕES AVANÇADAS'));
      await tester.pumpAndSettle();
      await tester.ensureVisible(find.text('PERSONALIZAR CORES'));
      await tester.tap(find.text('PERSONALIZAR CORES'));
      await tester.pumpAndSettle();
      await controller.activate(
        api,
        'owner',
        'world',
        'club-2',
        api.colors('club-2'),
      );
      await tester.pumpAndSettle();
      await tester.drag(find.byType(Scrollable).first, const Offset(0, 3000));
      await tester.pumpAndSettle();
      expect(find.textContaining('CLUBE ALTERADO.'), findsOneWidget);
      await tester.scrollUntilVisible(
        find.text('SALVAR APARÊNCIA'),
        300,
        scrollable: find.byType(Scrollable).first,
      );
      final save = tester.widget<ElevatedButton>(
        find.widgetWithText(ElevatedButton, 'SALVAR APARÊNCIA'),
      );
      expect(save.onPressed, isNull);
      expect(api.writes, 0);
      // Rebalance: the new club palette is loaded for match areas while the
      // global interface stays classic green.
      expect(controller.palette?['primaryColor'], '#0000FF');
      expect(
        controller.theme.background,
        ClassicTheme.classicGreen().background,
      );
      await tester.pumpWidget(const SizedBox());
      controller.dispose();
    },
  );

  test(
    'Every imported traditional palette remains legible when team colors are paired',
    () {
      final palettes = [
        ["#C3281E", "#000000"],
        ["#0000FF", "#FFD700"],
        ["#FFFFFF", "#FF0000"],
        ["#77B5FE", "#FFFFFF"],
        ["#002366", null],
        ["#FF0000", "#FFFFFF"],
        ["#FF0000", "#FFFFFF"],
        ["#FF0000", "#FFFFFF"],
        ["#0000FF", "#FF0000"],
        ["#FF0000", "#FFFFFF"],
        ["#FFFFFF", "#800080"],
        ["#FFFF00", null],
        ["#FF0000", "#000000"],
        ["#FFFFFF", "#000000"],
        ["#0000FF", "#000000"],
        ["#FFFFFF", "#77B5FE"],
        ["#000080", "#FF0000"],
      ];
      for (final p in palettes) {
        final primary = rgbColor(p[0])!, preferred = rgbColor(p[1]);
        final ink = preferred != null && preferred != primary
            ? preferred
            : readableInk(primary);
        expect(
          colorContrast(ink, readableSurface(primary, ink)),
          greaterThanOrEqualTo(4.5),
          reason: '$p',
        );
      }
      final gray = rgbColor('#777777')!;
      expect(
        colorContrast(gray, readableSurface(rgbColor('#C3281E')!, gray)),
        greaterThanOrEqualTo(4.5),
      );
    },
  );

  for (final colors in [
    palette('#C3281E', '#000000'),
    palette('#0000FF', '#FFFFFF'),
    palette('#FFFFFF', '#000000'),
    palette('#FFFF00', '#000000'),
    palette('#002366'),
    <String, dynamic>{},
  ]) {
    test('All semantic text tokens meet contrast for $colors', () {
      for (final t in [
        ClassicTheme.derive(club: colors),
        ClassicTheme.derive(club: colors, preferences: {'mode': 'ClubGlobal'}),
      ]) {
        for (final bg in [
          t.background,
          t.surface,
          t.surfaceAlternate,
          t.tableHeader,
          t.tableRow,
          t.tableRowAlternate,
        ]) {
          expect(colorContrast(t.textPrimary, bg), greaterThanOrEqualTo(4.5));
        }
        expect(
          colorContrast(t.buttonText, t.buttonBackground),
          greaterThanOrEqualTo(4.5),
        );
        expect(
          colorContrast(t.selectedText, t.selectedBackground),
          greaterThanOrEqualTo(4.5),
        );
        expect(colorContrast(t.accent, t.surface), greaterThanOrEqualTo(4.5));
      }
    });
  }
  test(
    'Custom black-on-red adjusts only rendered background and keeps registered colors',
    () {
      final p = palette('#C3281E', '#000000');
      final original = Map.from(p);
      final t = ClassicTheme.derive(
        club: p,
        preferences: {
          'mode': 'Custom',
          'background': '#C3281E',
          'primary': '#C3281E',
          'secondary': '#000000',
          'text': '#000000',
          'accent': '#FFFFFF',
        },
      );
      expect(t.primary, rgbColor('#C3281E'));
      expect(t.textPrimary, Colors.black);
      expect(
        colorContrast(t.textPrimary, t.background),
        greaterThanOrEqualTo(4.5),
      );
      expect(p, original);
    },
  );
  test(
    'Preferences restore after controller restart, isolate trainers/worlds and return to previous club',
    () async {
      final api = ThemedApi(),
          a = ClubThemeController(),
          b = ClubThemeController();
      await a.activate(api, 'coach-a', 'world', 'club-1', api.colors('club-1'));
      await b.activate(api, 'coach-b', 'world', 'club-1', api.colors('club-1'));
      final custom = {
        'mode': 'Custom',
        'background': '#000000',
        'primary': '#C3281E',
        'secondary': '#000000',
        'text': '#FFFFFF',
        'accent': '#FF0000',
      };
      await a.save(api, custom);
      expect(a.theme.background, Colors.black);
      expect(b.preferences['mode'], 'Club');
      await a.activate(api, 'coach-a', 'world', 'club-2', api.colors('club-2'));
      expect(a.palette?['primaryColor'], '#0000FF');
      expect(a.theme.background, ClassicTheme.classicGreen().background);
      await a.activate(api, 'coach-a', 'world', null, null);
      expect(a.palette, isNull);
      await a.activate(api, 'coach-a', 'world', 'club-1', api.colors('club-1'));
      expect(a.preferences['mode'], 'Custom');
      a.dispose();
      final restarted = ClubThemeController();
      await restarted.activate(api, 'coach-a', 'world', 'club-1', null);
      expect(restarted.theme.background, Colors.black);
      await restarted.activate(api, 'coach-a', 'other-world', 'club-1', null);
      expect(restarted.preferences['mode'], 'Club');
      restarted.dispose();
      b.dispose();
    },
  );
  testWidgets(
    'Match teams keep independent colors and scores remain centered',
    (tester) async {
      await tester.pumpWidget(
        MaterialApp(
          theme: ClassicTheme.derive(
            club: palette('#C3281E', '#000000'),
          ).material(),
          home: Scaffold(
            body: Row(
              children: [
                Expanded(
                  child: TeamColorName(
                    name: 'Flamengo',
                    palette: palette('#C3281E', '#000000'),
                  ),
                ),
                const SizedBox(
                  width: 74,
                  child: Text('2 - 1', textAlign: TextAlign.center),
                ),
                Expanded(
                  child: TeamColorName(
                    name: 'Clube azul',
                    palette: palette('#0000FF', '#FFFFFF'),
                  ),
                ),
              ],
            ),
          ),
        ),
      );
      final red = tester.widget<Container>(
        find.byKey(const ValueKey('team-color-Flamengo')),
      );
      final blue = tester.widget<Container>(
        find.byKey(const ValueKey('team-color-Clube azul')),
      );
      expect(
        (red.decoration as BoxDecoration).color,
        isNot((blue.decoration as BoxDecoration).color),
      );
      for (final name in ['Flamengo', 'Clube azul']) {
        final c = tester.widget<Container>(
          find.byKey(ValueKey('team-color-$name')),
        );
        final text = tester.widget<Text>(find.text(name));
        expect(
          colorContrast(
            text.style!.color!,
            (c.decoration as BoxDecoration).color!,
          ),
          greaterThanOrEqualTo(4.5),
        );
      }
    },
  );
  for (final width in [390.0, 1366.0]) {
    testWidgets(
      'Global theme stays classic green for Flamengo; picker, persistence and restore at $width',
      (tester) async {
        await tester.binding.setSurfaceSize(Size(width, 1000));
        addTearDown(() => tester.binding.setSurfaceSize(null));
        final api = ThemedApi();
        await join(tester, api);
        var theme = ClassicTheme.of(
          tester.element(find.byType(ElifootShellScreen)),
        );
        expect(theme.background, ClassicTheme.classicGreen().background);
        expect(
          HSVColor.fromColor(theme.background).hue,
          inInclusiveRange(90, 180),
        );
        expect(find.text('7. EDITOR & PACOTES'), findsNothing);
        if (width < 1024) {
          await tester.tap(find.byKey(const Key('open-navigation')));
          await tester.pumpAndSettle();
        }
        await tester.ensureVisible(
          find.byKey(const ValueKey('appearance-menu')),
        );
        await tester.tap(find.byKey(const ValueKey('appearance-menu')));
        await tester.pumpAndSettle();
        await tester.ensureVisible(find.text('OPÇÕES AVANÇADAS'));
        await tester.tap(find.text('OPÇÕES AVANÇADAS'));
        await tester.pumpAndSettle();
        await tester.ensureVisible(find.text('PERSONALIZAR CORES'));
        await tester.tap(find.text('PERSONALIZAR CORES'));
        await tester.pumpAndSettle();
        await tester.ensureVisible(
          find.byKey(const ValueKey('color-hex-COR DE FUNDO')),
        );
        await tester.enterText(
          find.byKey(const ValueKey('color-hex-COR DE FUNDO')),
          '#000000',
        );
        await tester.pumpAndSettle();
        await tester.ensureVisible(find.byIcon(Icons.palette).first);
        await tester.tap(find.byIcon(Icons.palette).first);
        await tester.pumpAndSettle();
        expect(find.byType(Slider), findsNWidgets(3));
        await tester.tap(find.text('CONCLUIR'));
        await tester.pumpAndSettle();
        await tester.ensureVisible(find.text('SALVAR APARÊNCIA'));
        await tester.tap(find.text('SALVAR APARÊNCIA'));
        await tester.pumpAndSettle();
        expect(api.writes, 1);
        await tester.ensureVisible(find.text('RESTAURAR PADRÃO'));
        await tester.tap(find.text('RESTAURAR PADRÃO'));
        await tester.pumpAndSettle();
        expect(api.writes, 2);
        expect(api.prefs.values.single['mode'], 'Classic');
        await tester.pageBack();
        await tester.pumpAndSettle();
        expect(find.byKey(const ValueKey('coach-main-panel')), findsOneWidget);
        expect(tester.takeException(), isNull);
      },
    );
  }
}
