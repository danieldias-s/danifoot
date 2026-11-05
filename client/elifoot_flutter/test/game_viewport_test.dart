import 'dart:math' as math;

import 'package:elifoot_flutter/classic98_widgets.dart';
import 'package:elifoot_flutter/game_viewport.dart';
import 'package:elifoot_flutter/main.dart';
import 'package:elifoot_flutter/match_interaction_dialog.dart';
import 'package:elifoot_flutter/presentation_version.dart';
import 'package:elifoot_flutter/presentation_widgets.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

import 'classic_ux_014b_test.dart' show WaitingRoomApi, join;
import 'presentation_017a_test.dart' show GameplayPresentationApi;

class _FullBenchApi extends WaitingRoomApi {
  _FullBenchApi() : super(2);

  @override
  Future<Map<String, dynamic>> getSnapshot(String worldId) async {
    final snapshot = await super.getSnapshot(worldId);
    for (final match in (snapshot['matches'] as List).whereType<Map>()) {
      final bench = match['homeBench'] as List;
      match['homeBench'] = [
        ...bench,
        for (var index = bench.length; index < 9; index++)
          {
            'playerId': 'extra-$index',
            'displayName': 'Reserva ${index + 1}',
            'position': 'D',
            'strength': 18,
          },
      ];
    }
    return snapshot;
  }
}

Future<void> _selectClassic(WidgetTester tester, WaitingRoomApi api) async {
  final controller = PresentationScope.maybeOf(
    tester.element(find.byType(ElifootShellScreen)),
  )!;
  await controller.select(api, PresentationVersion.classic);
  await tester.pumpAndSettle();
}

void main() {
  const widths = [
    1920.0,
    1600.0,
    1440.0,
    1360.0,
    1280.0,
    1024.0,
    768.0,
    390.0,
    360.0,
  ];

  for (final classic in [false, true]) {
    testWidgets(
      'Game stays centered through desktop and phone sizes (classic=$classic)',
      (tester) async {
        tester.view.physicalSize = const Size(1360, 900);
        tester.view.devicePixelRatio = 1;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);
        final api = GameplayPresentationApi();
        await join(tester, api);
        if (classic) await _selectClassic(tester, api);

        final shell = tester.state(find.byType(ElifootShellScreen));
        final nav = find.byKey(const Key('shared-game-navigation'));
        for (final width in widths) {
          tester.view.physicalSize = Size(width, 900);
          await tester.pumpAndSettle();
          final rect = tester.getRect(nav);
          final gameWidth = math.min(width, maxGameWidth);
          expect(rect.size, Size(gameWidth, 900), reason: 'viewport $width');
          expect(rect.left, (width - gameWidth) / 2, reason: 'viewport $width');
          expect(
            MediaQuery.sizeOf(tester.element(find.byType(Scaffold).first)),
            rect.size,
          );
          expect(tester.state(find.byType(ElifootShellScreen)), same(shell));
          if (classic) {
            final title = tester.getRect(find.byType(RetroTitle).first);
            final footer = tester.getRect(
              find.widgetWithText(RetroButton, 'Jogo'),
            );
            expect(title.left, greaterThanOrEqualTo(rect.left));
            expect(title.right, lessThanOrEqualTo(rect.right));
            expect(footer.left, greaterThanOrEqualTo(rect.left));
            expect(footer.bottom, lessThanOrEqualTo(rect.bottom));
            expect(title.bottom, lessThan(footer.top));
          }
          expect(tester.takeException(), isNull, reason: 'viewport $width');
        }

        await tester.pumpWidget(const SizedBox());
        await tester.pumpAndSettle();
      },
    );
  }

  testWidgets(
    'Classic substitutions stay centered and scroll to the ninth reserve',
    (tester) async {
      tester.view.physicalSize = const Size(1360, 900);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      final api = _FullBenchApi();
      await join(tester, api);
      await _selectClassic(tester, api);
      await api.emit('HALFTIME_PAUSED');
      await tester.pumpAndSettle();
      final modal = find.byKey(const Key('shared-substitution-dialog'));
    expect(tester.widget(modal), isA<ClassicMatchActionDialog>());
      final lastReserve = find.byKey(const ValueKey('sub-select-in-extra-8'));

      for (final size in [const Size(1920, 500), const Size(360, 640)]) {
        tester.view.physicalSize = size;
        await tester.pumpAndSettle();
        final panel = tester.getRect(
          find.descendant(of: modal, matching: find.byType(Material)).first,
        );
        expect(panel.center.dx, closeTo(size.width / 2, 0.5));
        expect(panel.width, lessThanOrEqualTo(720));
        expect(panel.top, greaterThanOrEqualTo(0));
        expect(panel.bottom, lessThanOrEqualTo(size.height));
        expect(find.widgetWithText(RetroButton, 'Jogo'), findsNothing);
        await tester.ensureVisible(lastReserve);
        await tester.pumpAndSettle();
        expect(lastReserve.hitTestable(), findsOneWidget);
        await tester.ensureVisible(
          find.byKey(const Key('btn-confirmar-continuar')),
        );
        await tester.pumpAndSettle();
        expect(
          find.byKey(const Key('btn-confirmar-continuar')).hitTestable(),
          findsOneWidget,
        );
        expect(tester.takeException(), isNull);
      }

      await tester.pumpWidget(const SizedBox());
      await tester.pumpAndSettle();
    },
  );
}
