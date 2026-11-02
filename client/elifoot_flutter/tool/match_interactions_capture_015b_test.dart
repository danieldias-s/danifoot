// Recorded official API QA scenes rendered by Flutter. NOT Chrome/device screenshots.
import 'dart:convert';
import 'dart:io';
import 'dart:ui' as ui;
import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:elifoot_flutter/main.dart';
import 'mobile_reference_capture_015b_test.dart' show RecordedMobileApi;

void main() {
  testWidgets(
    'Official human penalty and shared substitution windows, desktop/mobile',
    (tester) async {
      await tester.runAsync(() async {
        for (final font in ['roboto-regular.ttf', 'roboto-bold.ttf']) {
          await ui.loadFontFromList(
            await File(
              'C:/flutter/bin/cache/artifacts/material_fonts/$font',
            ).readAsBytes(),
            fontFamily: 'Roboto',
          );
        }
        await ui.loadFontFromList(
          await File(
            'C:/flutter/bin/cache/artifacts/material_fonts/materialicons-regular.otf',
          ).readAsBytes(),
          fontFamily: 'MaterialIcons',
        );
      });
      final out = Directory(
        '../../docs/evidence-match-interactions-015b/automated-renders',
      )..createSync(recursive: true);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetDevicePixelRatio);
      addTearDown(tester.view.resetPhysicalSize);
      for (final sceneName in [
        'substitution',
        'substitution-applied',
        'halftime',
        'penalty-choice',
        'penalty-waiting',
      ]) {
        final scene =
            jsonDecode(
                  File(
                    '../../artifacts/015b/interactions-$sceneName-scene.json',
                  ).readAsStringSync(),
                )
                as Map<String, dynamic>;
        for (final size in [const Size(1440, 1000), const Size(390, 900)]) {
          tester.view.physicalSize = size;
          await tester.pumpWidget(const SizedBox());
          await tester.pumpAndSettle();
          final key = GlobalKey();
          await tester.pumpWidget(
            RepaintBoundary(
              key: key,
              child: ElifootClassicApp(apiClient: RecordedMobileApi(scene)),
            ),
          );
          await tester.enterText(
            find.byType(TextField).first,
            scene['nickname'] as String,
          );
          final login = find.text('ENTRAR NO LOBBY (LOCAL DEV)');
          await tester.ensureVisible(login);
          await tester.tap(login);
          await tester.pumpAndSettle();
          final enter = find.text('ENTRAR COM CLUBE');
          await tester.ensureVisible(enter);
          await tester.tap(enter);
          await tester.pumpAndSettle();
          if (sceneName == 'penalty-choice') {
            final id =
                scene['snapshot']['pendingPenalty']['eligiblePlayers'][1]['playerId'];
            final player = find.byKey(ValueKey('penalty-taker-$id'));
            await tester.ensureVisible(player);
            await tester.tap(player);
            await tester.pumpAndSettle();
          }
          expect(tester.takeException(), isNull);
          final boundary = tester.renderObject<RenderRepaintBoundary>(
            find.byKey(key),
          );
          await tester.runAsync(() async {
            final picture = await boundary.toImage(pixelRatio: 1.5);
            final bytes = await picture.toByteData(
              format: ui.ImageByteFormat.png,
            );
            await File(
              '${out.path}/${size.width > 1000 ? 'desktop' : 'mobile'}-$sceneName.png',
            ).writeAsBytes(bytes!.buffer.asUint8List());
            picture.dispose();
          });
        }
      }
      await tester.pumpWidget(const SizedBox());
    },
  );
}
