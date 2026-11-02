// Real recorded API data rendered by Flutter. These are NOT Chrome screenshots.
import 'dart:convert';
import 'dart:io';
import 'dart:ui' as ui;
import 'package:elifoot_flutter/main.dart';
import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'player_rankings_capture_016b_test.dart' show RecordedRankingApi;

void main() {
  for (final width in [390.0, 1366.0]) {
    testWidgets('Recorded real coach and finance panels at $width', (
      tester,
    ) async {
      await tester.binding.setSurfaceSize(
        Size(width, width < 700 ? 1200 : 1000),
      );
      addTearDown(() => tester.binding.setSurfaceSize(null));
      await tester.runAsync(() async {
        final fonts = FontLoader('ClassicCondensed');
        for (final name in ['Regular', 'Bold', 'BoldItalic']) {
          fonts.addFont(
            File(
              'assets/fonts/BarlowCondensed-$name.ttf',
            ).readAsBytes().then((bytes) => ByteData.sublistView(bytes)),
          );
        }
        await fonts.load();
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
      final scene =
          jsonDecode(
                File(
                  '../../artifacts/016b/integration-015b/real-api-scene.json',
                ).readAsStringSync(),
              )
              as Map<String, dynamic>;
      final output = Directory(
        '../../docs/coach-finance-integration-016b/flutter-renders',
      )..createSync(recursive: true);
      final boundary = GlobalKey();
      Future<void> capture(String name) async {
        await tester.pumpAndSettle();
        expect(tester.takeException(), isNull);
        final view = tester.renderObject<RenderRepaintBoundary>(
          find.byKey(boundary),
        );
        await tester.runAsync(() async {
          final image = await view.toImage(pixelRatio: 1);
          final bytes = await image.toByteData(format: ui.ImageByteFormat.png);
          await File(
            '${output.path}/$name-${width.toInt()}.png',
          ).writeAsBytes(bytes!.buffer.asUint8List());
          image.dispose();
        });
      }

      Future<void> select(int tab) async {
        if (width < 1024) {
          await tester.tap(find.byKey(const Key('open-navigation')));
          await tester.pumpAndSettle();
        }
        final item = find.byKey(ValueKey('shell-nav-$tab'));
        await tester.ensureVisible(item);
        await tester.tap(item);
        await tester.pumpAndSettle();
      }

      await tester.pumpWidget(
        RepaintBoundary(
          key: boundary,
          child: ElifootClassicApp(apiClient: RecordedRankingApi(scene)),
        ),
      );
      await tester.tap(find.text('ENTRAR NO LOBBY (LOCAL DEV)'));
      await tester.pumpAndSettle();
      await tester.ensureVisible(find.text('ENTRAR COM CLUBE'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('ENTRAR COM CLUBE'));
      await tester.pumpAndSettle();
      await select(4);
      await capture('coach-ranking');
      if (width < 700) {
        await tester.ensureVisible(
          find.byKey(const Key('coach-ranking-complete')),
        );
        await tester.tap(find.byKey(const Key('coach-ranking-complete')));
        await tester.pumpAndSettle();
        await tester.ensureVisible(find.byKey(const Key('coach-tab-ranking')));
        await capture('coach-ranking-complete');
      }
      await tester.ensureVisible(find.byKey(const Key('coach-tab-career')));
      await tester.tap(find.byKey(const Key('coach-tab-career')));
      await capture('coach-career');
      await select(3);
      await capture('finances-top');
      if (width < 700) {
        await tester.ensureVisible(
          find.byKey(const Key('finances-ledger-table')),
        );
        await capture('finances-ledger');
      }
      await tester.pumpWidget(const SizedBox());
    });
  }
}
