// Recorded real API responses rendered by Flutter, not browser screenshots.
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
  for (final width in [390.0, 1366.0, 1536.0]) {
    testWidgets('Real recorded squad and tactics at $width', (tester) async {
      await tester.binding.setSurfaceSize(
        Size(width, width < 700 ? 1500 : 1100),
      );
      addTearDown(() => tester.binding.setSurfaceSize(null));
      await tester.runAsync(() async {
        final fonts = FontLoader('ClassicCondensed');
        for (final face in ['Regular', 'Bold', 'BoldItalic']) {
          fonts.addFont(
            File(
              'assets/fonts/BarlowCondensed-$face.ttf',
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
                  '../../artifacts/master-playtest/squad-real-api-scene.json',
                ).readAsStringSync(),
              )
              as Map<String, dynamic>;
      final output = Directory(
        '../../docs/squad-reference-20261102/flutter-renders',
      )..createSync(recursive: true);
      final boundaryKey = GlobalKey();
      Future<void> capture(String label) async {
        await tester.pumpAndSettle();
        expect(tester.takeException(), isNull);
        final boundary = tester.renderObject<RenderRepaintBoundary>(
          find.byKey(boundaryKey),
        );
        await tester.runAsync(() async {
          final image = await boundary.toImage(pixelRatio: 1);
          final bytes = await image.toByteData(format: ui.ImageByteFormat.png);
          await File(
            '${output.path}/$label-${width.toInt()}.png',
          ).writeAsBytes(bytes!.buffer.asUint8List());
          image.dispose();
        });
      }

      await tester.pumpWidget(
        RepaintBoundary(
          key: boundaryKey,
          child: ElifootClassicApp(apiClient: RecordedRankingApi(scene)),
        ),
      );
      await tester.tap(find.text('ENTRAR NO LOBBY (LOCAL DEV)'));
      await tester.pumpAndSettle();
      final enter = find.byKey(ValueKey('join-world-${scene['world']['id']}'));
      await tester.ensureVisible(enter);
      await tester.tap(enter);
      await tester.pumpAndSettle();
      await tester.tap(
        find.byKey(
          width < 768
              ? const Key('mobile-nav-0')
              : const ValueKey('shell-nav-0'),
        ),
      );
      await tester.pumpAndSettle();
      await capture('plantel');
      if (width < 700) {
        await tester.ensureVisible(find.byKey(const ValueKey('squad-table')));
        expect(
          tester.getRect(find.byTooltip('Colocar em leilão').first).right,
          lessThanOrEqualTo(width),
          reason: 'Sale action stays visible without horizontal scrolling.',
        );
        await capture('plantel-elenco');
        await tester.tap(find.byKey(const Key('mobile-nav-1')));
        await tester.pumpAndSettle();
        await capture('taticas');
      } else {
        // The pitch and roster must remain alongside each other on desktop.
        final pitch = tester.getRect(
          find.byKey(const Key('desktop-tactics-column')),
        );
        final roster = tester.getRect(
          find.byKey(const ValueKey('squad-table')),
        );
        expect(pitch.right, lessThan(roster.left));
        expect(
          tester.getRect(find.byTooltip('Colocar em leilão').first).right,
          lessThanOrEqualTo(width),
          reason: 'Desktop sale action remains visible.',
        );
      }
      await tester.pumpWidget(const SizedBox());
    });
  }
}
