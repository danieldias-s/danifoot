// ELIFOOT_VISUAL_THEME_REBALANCE_014C — Flutter renderer of real recorded HTTP
// responses (artifacts/014c/scene-*.json). NOT a browser screenshot.
import 'dart:convert';
import 'dart:io';
import 'dart:ui' as ui;
import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:elifoot_flutter/main.dart';
import 'package:elifoot_flutter/club_theme.dart';
import 'club_colors_capture_014c_test.dart' show SceneApi;

Map<String, dynamic> read(String name) =>
    jsonDecode(
          File(
            name.startsWith('fresh-')
                ? '../../artifacts/014c-rebalance/scenes/${name.substring(6)}.json'
                : '../../artifacts/014c/scene-$name.json',
          ).readAsStringSync(),
        )
        as Map<String, dynamic>;

/// Recorded scene with the stored appearance preference reset to the legacy
/// default (`Club`), proving old saves now render the classic green shell.
class RebalanceSceneApi extends SceneApi {
  RebalanceSceneApi(super.scene);
  @override
  Future<Map<String, dynamic>> getAppearance(
    String o,
    String w,
    String c,
  ) async => {
    ...await super.getAppearance(o, w, c),
    'preferences': {'mode': 'Club', 'version': 1},
  };
}

void main() {
  for (final width in [390.0, 1366.0]) {
    testWidgets('014C rebalance captures at $width', (tester) async {
      await tester.runAsync(() async {
        await ui.loadFontFromList(
          await File('C:/Windows/Fonts/consola.ttf').readAsBytes(),
          fontFamily: 'monospace',
        );
        await ui.loadFontFromList(
          await File('C:/Windows/Fonts/arial.ttf').readAsBytes(),
          fontFamily: 'Roboto',
        );
        await ui.loadFontFromList(
          await File(
            'C:/flutter/bin/cache/artifacts/material_fonts/MaterialIcons-Regular.otf',
          ).readAsBytes(),
          fontFamily: 'MaterialIcons',
        );
      });
      tester.view.physicalSize = Size(width, 1000);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      final out = Directory('../../docs/audit-014c-rebalance/automated-renders')
        ..createSync(recursive: true);
      final boundaryKey = GlobalKey();
      Future<void> capture(String name) async {
        await tester.pumpAndSettle();
        expect(tester.takeException(), isNull);
        for (final block in tester.widgetList<Container>(
          find.byType(Container),
        )) {
          if (block.key is! ValueKey<String> ||
              !(block.key as ValueKey<String>).value.startsWith(
                'team-color-',
              )) {
            continue;
          }
          final text = block.child! as Text;
          final background = (block.decoration! as BoxDecoration).color!;
          expect(
            colorContrast(text.style!.color!, background),
            greaterThanOrEqualTo(4.5),
            reason: '${block.key} in $name',
          );
        }
        final boundary = tester.renderObject<RenderRepaintBoundary>(
          find.byKey(boundaryKey),
        );
        await tester.runAsync(() async {
          final picture = await boundary.toImage(pixelRatio: 1);
          final data = await picture.toByteData(format: ui.ImageByteFormat.png);
          await File(
            '${out.path}/$name-${width.toInt()}.png',
          ).writeAsBytes(data!.buffer.asUint8List());
          picture.dispose();
        });
      }

      Future<void> open(String scene) async {
        await tester.pumpWidget(const SizedBox());
        await tester.pumpAndSettle();
        await tester.pumpWidget(
          RepaintBoundary(
            key: boundaryKey,
            child: ElifootClassicApp(apiClient: RebalanceSceneApi(read(scene))),
          ),
        );
        await tester.tap(find.text('ENTRAR NO LOBBY (LOCAL DEV)'));
        await tester.pumpAndSettle();
        await tester.tap(find.text('ENTRAR COM CLUBE'));
        await tester.pumpAndSettle();
      }

      Future<void> tab(String label) async {
        final f = find.textContaining(label);
        await tester.ensureVisible(f.first);
        await tester.tap(f.first);
        await tester.pumpAndSettle();
      }

      // General screens: red club (Flamengo) and blue club (Manchester City).
      await open('male-red');
      await capture('general-red-club');
      await tab('CALENDÁRIO & RESULTADOS');
      await capture('calendar-red-club');
      await tab('DIVISÕES & TAÇA');
      await capture('divisions-red-club');
      final cup = find.byKey(const ValueKey('cup-fixtures-table'));
      if (cup.evaluate().isNotEmpty) {
        await tester.ensureVisible(cup);
        await capture('cup-grouped');
      }
      await tab('MERCADO & LEILÃO');
      await capture('market-red-club');
      await tester.ensureVisible(find.byKey(const ValueKey('appearance-menu')));
      await tester.tap(find.byKey(const ValueKey('appearance-menu')));
      await tester.pumpAndSettle();
      await capture('appearance-menu');

      await open('male-blue');
      await capture('general-blue-club');

      // Match screens: each team with its own palette, 4 divisions grouped.
      for (final scene in ['matches-red', 'matches-blue']) {
        await open(scene);
        final group = find.textContaining('JOGOS');
        if (group.evaluate().length == 1) {
          await tester.ensureVisible(group);
          await tester.tap(group);
          await tester.pumpAndSettle();
        }
        await capture('$scene-board');
      }

      // Fresh real API snapshots: league R3 and cup R5 in progress. Other
      // auxiliary screens still use SceneApi's explicit test adapters.
      for (final phase in ['league', 'cup']) {
        await open('fresh-$phase-red');
        await capture('live-$phase-grouped');
      }
    });
  }
}
