// Recorded official QA API responses rendered by Flutter. NOT Chrome/device screenshots.
import 'dart:convert';
import 'dart:io';
import 'dart:ui' as ui;
import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:elifoot_flutter/main.dart';
import 'package:elifoot_flutter/classic_match_board.dart';
import 'package:elifoot_flutter/live_match_data.dart';
import 'package:elifoot_flutter/live_match_view.dart';
import 'mobile_reference_capture_015b_test.dart' show RecordedMobileApi;

void main() {
  testWidgets('Official live scene desktop and mobile selection captures', (
    tester,
  ) async {
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
    final scene =
        jsonDecode(
              File(
                "../../artifacts/015b/${Platform.environment['LIVE_CAPTURE_SCENE'] ?? 'live-review-scene.json'}",
              ).readAsStringSync(),
            )
            as Map<String, dynamic>;
    final snap = scene['snapshot'] as Map<String, dynamic>,
        matches = (snap['matches'] as List).cast<Map>();
    final other = matches.firstWhere(
      (m) => !isCoachMatch(Map<String, dynamic>.from(m), scene['club']),
    );
    final out = Directory(
      '../../docs/evidence-live-matches-015b/automated-renders',
    )..createSync(recursive: true);
    tester.view.devicePixelRatio = 1;
    addTearDown(tester.view.resetDevicePixelRatio);
    addTearDown(tester.view.resetPhysicalSize);
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
      Future<void> capture(String name) async {
        await tester.pumpAndSettle();
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
            "${out.path}/${Platform.environment['LIVE_CAPTURE_PREFIX'] ?? ''}$name.png",
          ).writeAsBytes(bytes!.buffer.asUint8List());
          picture.dispose();
        });
      }

      await capture(size.width > 1000 ? 'desktop-own' : 'mobile-own');
      if (size.width < 1000) {
        final selection = tester
            .widget<LiveMatchScreen>(find.byType(LiveMatchScreen))
            .selection;
        final group = matchGroup(
          Map<String, dynamic>.from(other),
          '${snap['competitionType']}',
          '${snap['competitionLabel']}',
        );
        final target = find.byKey(ValueKey('select-match-${other['matchId']}'));
        if (target.evaluate().isEmpty) {
          final tile = find.byKey(
            PageStorageKey('live-group-${selection.scope}-$group'),
          );
          await tester.ensureVisible(tile);
          await tester.tap(tile);
          await tester.pumpAndSettle();
        }
      }
      final target = find.byKey(ValueKey('select-match-${other['matchId']}'));
      await tester.ensureVisible(target);
      await tester.tap(target);
      await tester.pumpAndSettle();
      await tester.ensureVisible(
        find.byKey(const Key('selected-match-scoreboard')),
      );
      await tester.pumpAndSettle();
      await capture(
        size.width > 1000
            ? 'desktop-observing-other'
            : 'mobile-observing-other',
      );
      if (size.width < 1000) {
        final tab = find.byKey(const ValueKey('live-detail-tab-1'));
        await tester.ensureVisible(tab);
        await tester.tap(tab);
        await tester.pumpAndSettle();
        await tester.ensureVisible(
          find.byKey(const Key('selected-match-scoreboard')),
        );
        await tester.pumpAndSettle();
        await capture('mobile-statistics');
        await tester.tap(find.byKey(const ValueKey('live-detail-tab-0')));
        await tester.pumpAndSettle();
        final board = tester.widget<ClassicMatchBoard>(
          find.byType(ClassicMatchBoard),
        );
        final groups = board.matches
            .map(
              (m) =>
                  matchGroup(m, board.competitionType, board.competitionLabel),
            )
            .toSet();
        for (final group in groups) {
          final tile = find.byKey(
            PageStorageKey('live-group-${board.selectionScope}-$group'),
          );
          final title = find
              .descendant(of: tile, matching: find.byType(ListTile))
              .first;
          ExpansibleController.of(tester.element(title)).expand();
        }
        tester.view.physicalSize = const Size(390, 1800);
        await tester.pumpAndSettle();
        await tester.ensureVisible(
          find.byKey(const Key('selected-match-scoreboard')),
        );
        await tester.pumpAndSettle();
        await capture('mobile-expanded-divisions');
      }
    }
    await tester.pumpWidget(const SizedBox());
  });
}
