// ELIFOOT_VISUAL_THEME_REBALANCE_014C
// APP THEME = fixed classic green; MATCH TEAM COLORS = each club's palette.
import 'package:elifoot_flutter/appearance_settings.dart';
import 'package:elifoot_flutter/classic_match_board.dart';
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

final flamengo = palette('#C3281E', '#000000');
final blueClub = palette('#0000FF', '#FFFFFF');
final porto = palette('#00428C', '#FFFFFF');

/// Coach club is red (Flamengo palette) or blue depending on [coachColors].
class RebalanceApi extends FakeGameplay011ApiClient {
  final Map<String, dynamic> coachColors;
  final prefs = <String, Map<String, dynamic>>{};
  RebalanceApi(this.coachColors);

  @override
  Future<Map<String, dynamic>> getClubDetail(String world, String club) async =>
      {...await super.getClubDetail(world, club), 'colorPalette': coachColors};

  @override
  Future<List<dynamic>> getClubs(String worldId, {String? tier}) async => [
    for (final c in await super.getClubs(worldId, tier: tier))
      {
        ...c as Map,
        'colorPalette': c['name'] == 'Atlético Verde' ? flamengo : blueClub,
      },
    {
      'id': 'club-3',
      'name': 'União Imperial',
      'divisionTier': 'PrimeiraDivisao',
      'colorPalette': porto,
    },
  ];

  @override
  Future<Map<String, dynamic>> getAppearance(
    String owner,
    String world,
    String club,
  ) async => {
    'worldId': world,
    'clubId': club,
    'clubColors': coachColors,
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
    prefs['$owner:$world:$club'] = {...p, 'version': version + 1};
    return getAppearance(owner, world, club);
  }
}

Future<void> join(WidgetTester tester, RebalanceApi api) async {
  await tester.pumpWidget(ElifootClassicApp(apiClient: api));
  await tester.tap(find.text('ENTRAR NO LOBBY (LOCAL DEV)'));
  await tester.pumpAndSettle();
  await tester.tap(find.text('ENTRAR COM CLUBE'));
  await tester.pumpAndSettle();
  await tester.tap(find.byKey(const Key('choose-club-club-1')));
  await tester.pumpAndSettle();
}

Color blockColor(WidgetTester tester, String name) =>
    (tester
                .widget<Container>(
                  find.byKey(ValueKey('team-color-$name')).first,
                )
                .decoration
            as BoxDecoration)
        .color!;

double hue(Color c) => HSVColor.fromColor(c).hue;
bool isRed(Color c) => hue(c) < 15 || hue(c) > 345;
bool isBlue(Color c) => hue(c) > 200 && hue(c) < 250;

Map<String, dynamic> match(
  String id,
  int tier,
  String home,
  String away,
  Map<String, dynamic>? hp,
  Map<String, dynamic>? ap,
) => {
  'matchId': id,
  'competitionType': 'League',
  'divisionTier': tier,
  'homeClubId': 'h$id',
  'awayClubId': 'a$id',
  'homeClubName': home,
  'awayClubName': away,
  'homeGoals': 1,
  'awayGoals': 0,
  'minute': 37,
  'homeColorPalette': hp,
  'awayColorPalette': ap,
};

void main() {
  group('APP THEME stays classic green', () {
    for (final entry in {'vermelho': flamengo, 'azul': blueClub}.entries) {
      test('clube ${entry.key}: derive() ignores club palette for the app', () {
        final green = ClassicTheme.classicGreen();
        for (final prefs in [
          null,
          {'mode': 'Club'},
          {'mode': 'Classic'},
          {'mode': 'Neutral'},
        ]) {
          final t = ClassicTheme.derive(club: entry.value, preferences: prefs);
          expect(t.background, green.background);
          expect(t.surface, green.surface);
          expect(t.primary, green.primary);
          expect(t.selectedBackground, const Color(0xFFFFD400));
          expect(hue(t.background), inInclusiveRange(90, 180));
        }
      });
    }

    for (final width in [390.0, 1366.0]) {
      for (final entry in {'vermelho': flamengo, 'azul': blueClub}.entries) {
        testWidgets('shell is green for clube ${entry.key} at $width', (
          tester,
        ) async {
          await tester.binding.setSurfaceSize(Size(width, 1000));
          addTearDown(() => tester.binding.setSurfaceSize(null));
          await join(tester, RebalanceApi(entry.value));
          final t = ClassicTheme.of(
            tester.element(find.byType(ElifootShellScreen)),
          );
          expect(t.background, ClassicTheme.classicGreen().background);
          final scaffold = tester.widget<Scaffold>(find.byType(Scaffold).first);
          final bg =
              scaffold.backgroundColor ??
              Theme.of(
                tester.element(find.byType(Scaffold).first),
              ).scaffoldBackgroundColor;
          expect(hue(bg), inInclusiveRange(90, 180));
          expect(
            find.byKey(
              width < 768
                  ? const Key('mobile-squad-screen')
                  : const ValueKey('coach-main-panel'),
            ),
            findsOneWidget,
          );
          expect(tester.takeException(), isNull);
        });
      }
    }

    test('variants and brightness stay green and legible', () {
      for (final variant in ['Classico', 'Escuro', 'AltoContraste']) {
        for (var b = -2; b <= 2; b++) {
          final t = ClassicTheme.derive(
            club: flamengo,
            preferences: {
              'mode': 'Classic',
              'themeVariant': variant,
              'brightness': b,
            },
          );
          for (final bg in [t.background, t.surface, t.surfaceAlternate]) {
            expect(colorContrast(t.textPrimary, bg), greaterThanOrEqualTo(4.5));
            expect(colorContrast(t.accent, bg), greaterThanOrEqualTo(4.5));
          }
          expect(
            colorContrast(t.buttonText, t.buttonBackground),
            greaterThanOrEqualTo(4.5),
          );
          expect(
            colorContrast(t.selectedText, t.selectedBackground),
            greaterThanOrEqualTo(4.5),
          );
          if (variant != 'AltoContraste') {
            expect(hue(t.background), inInclusiveRange(90, 180));
          }
        }
      }
    });

    test('existing saved preferences keep working', () {
      // Legacy default "Club" → green; Custom remains an advanced explicit choice.
      expect(
        ClassicTheme.derive(
          club: flamengo,
          preferences: {'mode': 'Club', 'version': 4},
        ).background,
        ClassicTheme.classicGreen().background,
      );
      final custom = ClassicTheme.derive(
        club: flamengo,
        preferences: {
          'mode': 'Custom',
          'background': '#000000',
          'primary': '#C3281E',
          'secondary': '#000000',
          'text': '#FFFFFF',
          'accent': '#FF0000',
        },
      );
      expect(custom.background, Colors.black);
      // Advanced legacy option: club colors on the whole interface.
      final global = ClassicTheme.derive(
        club: flamengo,
        preferences: {'mode': 'ClubGlobal'},
      );
      expect(global.primary, rgbColor('#C3281E'));
      expect(hue(global.background), lessThan(30));
    });
  });

  group('MATCH TEAM COLORS by club', () {
    Future<void> pumpBoard(
      WidgetTester tester,
      double width, {
      Map<String, dynamic>? prefs,
    }) async {
      await tester.binding.setSurfaceSize(Size(width, 1000));
      addTearDown(() => tester.binding.setSurfaceSize(null));
      await tester.pumpWidget(
        MaterialApp(
          theme: ClassicTheme.derive(
            club: flamengo,
            preferences: prefs,
          ).material(),
          home: Scaffold(
            body: SingleChildScrollView(
              child: ClassicMatchBoard(
                competitionType: 'League',
                competitionLabel: 'Campeonato Nacional',
                coachClubId: 'hm1',
                matches: [
                  match('m1', 1, 'Flamengo', 'FC Porto', flamengo, porto),
                  match('m2', 2, 'Clube Azul', 'Sem Cores FC', blueClub, null),
                  match(
                    'm3',
                    3,
                    'Corinthians',
                    'Barcelona',
                    palette('#FFFFFF', '#000000'),
                    palette('#004D98', '#A50044'),
                  ),
                  match('m4', 4, 'Quarta A', 'Quarta B', null, null),
                ],
              ),
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();
    }

    for (final width in [390.0, 1366.0]) {
      testWidgets('Flamengo red/black and blue club blue/white at $width', (
        tester,
      ) async {
        await pumpBoard(tester, width);
        final fla = blockColor(tester, 'Flamengo');
        expect(isRed(fla), isTrue, reason: '$fla');
        final flaText = tester.widget<Text>(find.text('Flamengo'));
        expect(flaText.style!.color, Colors.black);
        final blue = blockColor(tester, 'Clube Azul');
        expect(isBlue(blue), isTrue, reason: '$blue');
        expect(
          tester.widget<Text>(find.text('Clube Azul')).style!.color,
          Colors.white,
        );
        expect(isBlue(blockColor(tester, 'FC Porto')), isTrue);
        // Clubs without a registered palette stay neutral (no invented colors).
        expect(
          blockColor(tester, 'Sem Cores FC'),
          ClassicTheme.classicGreen().surface,
        );
        for (final heading in [
          '1ª DIVISÃO',
          '2ª DIVISÃO',
          '3ª DIVISÃO',
          '4ª DIVISÃO',
        ]) {
          expect(find.text(heading), findsOneWidget);
        }
        // Score stays centered between the two team blocks.
        final score = tester.getCenter(find.byKey(const ValueKey('score-m1')));
        final home = tester.getCenter(
          find.byKey(const ValueKey('team-color-Flamengo')),
        );
        final away = tester.getCenter(
          find.byKey(const ValueKey('team-color-FC Porto')),
        );
        expect((score.dx - home.dx - (away.dx - score.dx)).abs(), lessThan(8));
        expect(tester.takeException(), isNull);
      });
    }

    testWidgets('neutral match mode keeps only an identity stripe', (
      tester,
    ) async {
      await pumpBoard(
        tester,
        1366,
        prefs: {'mode': 'Classic', 'matchColors': 'Neutral'},
      );
      expect(
        blockColor(tester, 'Flamengo'),
        ClassicTheme.classicGreen().surface,
      );
      final c = tester.widget<Container>(
        find.byKey(const ValueKey('team-color-Flamengo')),
      );
      expect(
        ((c.decoration as BoxDecoration).border as Border).left.color,
        rgbColor('#C3281E'),
      );
    });

    testWidgets('calendar and cup lists paint each team with its own colors', (
      tester,
    ) async {
      await tester.binding.setSurfaceSize(const Size(1366, 1000));
      addTearDown(() => tester.binding.setSurfaceSize(null));
      await join(tester, RebalanceApi(blueClub));
      final calendarTab = find.textContaining('CALENDÁRIO & RESULTADOS');
      await tester.ensureVisible(calendarTab.first);
      await tester.tap(calendarTab.first);
      await tester.pumpAndSettle();
      expect(isRed(blockColor(tester, 'Atlético Verde')), isTrue);
      expect(isBlue(blockColor(tester, 'Real Dourado')), isTrue);
      final cupTab = find.textContaining('DIVISÕES & TAÇA');
      await tester.ensureVisible(cupTab.first);
      await tester.tap(cupTab.first);
      await tester.pumpAndSettle();
      final finalBlock = find.byKey(
        const ValueKey('team-color-União Imperial (1ª Divisão)'),
      );
      await tester.ensureVisible(finalBlock.first);
      expect(isBlue(blockColor(tester, 'União Imperial (1ª Divisão)')), isTrue);
      expect(isRed(blockColor(tester, 'Atlético Verde (1ª Divisão)')), isTrue);
      // The interface around the match lists stays green.
      expect(
        ClassicTheme.of(
          tester.element(find.byType(ElifootShellScreen)),
        ).background,
        ClassicTheme.classicGreen().background,
      );
      expect(tester.takeException(), isNull);
    });
  });

  group('APARÊNCIA menu', () {
    for (final width in [390.0, 1366.0]) {
      testWidgets(
        'green variants, brightness and match colors persist at $width',
        (tester) async {
          await tester.binding.setSurfaceSize(Size(width, 1000));
          addTearDown(() => tester.binding.setSurfaceSize(null));
          final api = RebalanceApi(flamengo),
              controller = ClubThemeController();
          await controller.activate(api, 'owner', 'world', 'club-1', flamengo);
          await tester.pumpWidget(
            ListenableBuilder(
              listenable: controller,
              builder: (context, _) => MaterialApp(
                theme: controller.theme.material(),
                home: AppearanceSettings(api: api, controller: controller),
              ),
            ),
          );
          expect(find.text('USAR CORES DO CLUBE'), findsNothing);
          await tester.tap(find.byKey(const ValueKey('theme-variant-Escuro')));
          await tester.pumpAndSettle();
          expect(
            controller.theme.background,
            ClassicTheme.classicGreen(variant: 'Escuro').background,
          );
          await tester.tap(find.byKey(const ValueKey('brightness-up')));
          await tester.pumpAndSettle();
          await tester.ensureVisible(
            find.byKey(const ValueKey('match-colors-Neutral')),
          );
          await tester.tap(find.byKey(const ValueKey('match-colors-Neutral')));
          await tester.pumpAndSettle();
          expect(controller.theme.strongTeamColors, isFalse);
          await tester.ensureVisible(find.text('SALVAR APARÊNCIA'));
          await tester.tap(find.text('SALVAR APARÊNCIA'));
          await tester.pumpAndSettle();
          final saved = api.prefs.values.single;
          expect(saved['themeVariant'], 'Escuro');
          expect(saved['brightness'], 1);
          expect(saved['matchColors'], 'Neutral');
          // Reload from "server": preferences restored.
          final again = ClubThemeController();
          await again.activate(api, 'owner', 'world', 'club-1', flamengo);
          expect(again.theme.strongTeamColors, isFalse);
          expect(
            again.theme.background,
            ClassicTheme.classicGreen(
              variant: 'Escuro',
              brightness: 1,
            ).background,
          );
          await tester.ensureVisible(find.text('RESTAURAR PADRÃO'));
          await tester.tap(find.text('RESTAURAR PADRÃO'));
          await tester.pumpAndSettle();
          expect(api.prefs.values.single['mode'], 'Classic');
          expect(
            controller.theme.background,
            ClassicTheme.classicGreen().background,
          );
          expect(tester.takeException(), isNull);
          await tester.pumpWidget(const SizedBox());
          controller.dispose();
          again.dispose();
        },
      );
    }
  });
}
