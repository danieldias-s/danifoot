import 'dart:ui' as ui;
import 'package:flutter/material.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:elifoot_flutter/main.dart';
import 'package:elifoot_flutter/classic98_widgets.dart';
import 'package:elifoot_flutter/stadium_visual.dart';
import 'package:elifoot_flutter/presentation_version.dart';
import 'package:elifoot_flutter/presentation_widgets.dart';
import 'presentation_017a_test.dart' show GameplayPresentationApi;
import 'classic_ux_014b_test.dart' show join;

class FullBenchApi extends GameplayPresentationApi {
  @override
  Future<Map<String, dynamic>> getClubDetail(
    String worldId,
    String clubId,
  ) async {
    final club = await super.getClubDetail(worldId, clubId);
    final squad = club['squad'] as List;
    for (var i = 19; i <= 23; i++) {
      squad.add({
        ...Map<String, dynamic>.from(squad[12] as Map),
        'id': 'p-$i',
        'displayName': 'Reserva extra $i',
      });
    }
    club['preferredBenchIds'] = [
      for (var i = 12; i <= 16; i++) 'p-$i',
      for (var i = 19; i <= 22; i++) 'p-$i',
    ];
    return club;
  }
}

void main() {
  testWidgets(
    'Bolinha aceita excesso no rascunho e bloqueia envio até corrigir sem trocar outro atleta',
    (t) async {
      t.view.physicalSize = const Size(1366, 1000);
      t.view.devicePixelRatio = 1;
      addTearDown(t.view.resetPhysicalSize);
      addTearDown(t.view.resetDevicePixelRatio);
      final api = FullBenchApi();
      await join(t, api);
      final controller = PresentationScope.maybeOf(
        t.element(find.byType(ElifootShellScreen)),
      )!;
      await controller.select(api, PresentationVersion.classic);
      await t.pumpAndSettle();
      RetroTable roster() =>
          t.widget<RetroTable>(find.byKey(const Key('retro-roster')));
      bool canSend() =>
          t
              .widget<ElevatedButton>(
                find.byKey(const Key('btn-enviar-tatica')),
              )
              .onPressed !=
          null;
      Future<void> click(String id) async {
        final dot = find.byKey(ValueKey('retro-role-$id'));
        await t.ensureVisible(dot);
        await t.tap(dot);
        await t.pumpAndSettle();
      }

      expect(
        roster().headers,
        equals(const ['POS', 'JOGADOR', 'FORÇA', 'ESTILO', 'SALÁRIO']),
      );
      expect(roster().sectionHeaders, isNotEmpty);
      expect(
        roster().sectionHeaders.values.first[1],
        startsWith('BANCO ('),
      );
      expect(roster().rows.first.length, 5);
      expect(canSend(), isTrue);
      await click('p-23');
      expect(find.textContaining('Titulares: 12/11'), findsWidgets);
      expect(canSend(), isFalse);
      expect(roster().rows.firstWhere((r) => r[1] == 'Atleta 2').first, '● D');
      expect(find.text('Trocar com titular'), findsNothing);
      expect(find.byType(PopupMenuItem<String>), findsNothing);
      await click('p-2');
      expect(find.textContaining('Banco: 10/9'), findsWidgets);
      expect(canSend(), isFalse);
      await click('p-2');
      expect(roster().rows.firstWhere((r) => r[1] == 'Atleta 2').first, '· D');
      expect(
        roster().headers,
        equals(const ['POS', 'JOGADOR', 'FORÇA', 'ESTILO', 'SALÁRIO']),
      );
      expect(roster().sectionHeaders, isNotEmpty);
      expect(
        roster().sectionHeaders.values.first[1],
        startsWith('BANCO ('),
      );
      expect(roster().rows.first.length, 5);
      expect(canSend(), isTrue);
      await click('p-23');
      expect(canSend(), isFalse);
      await click('p-23');
      expect(canSend(), isFalse);
      await click('p-2');
      expect(
        roster().headers,
        equals(const ['POS', 'JOGADOR', 'FORÇA', 'ESTILO', 'SALÁRIO']),
      );
      expect(roster().sectionHeaders, isNotEmpty);
      expect(
        roster().sectionHeaders.values.first[1],
        startsWith('BANCO ('),
      );
      expect(roster().rows.first.length, 5);
      expect(canSend(), isTrue);
      api.ready = 1;
      api.isReady = true;
      api.roundVersion++;
      api.onSnapshotUpdated?.call(await api.getSnapshot('world-011'));
      await t.pumpAndSettle();
      for (final cell in roster().leadingCells.values) {
        expect(((cell as Row).children.first as IconButton).onPressed, isNull);
      }
      expect(t.takeException(), isNull);
      await t.pumpWidget(const SizedBox());
      api.disconnectRealtime();
    },
  );
  for (final width in [360.0, 390.0, 430.0, 768.0, 1366.0, 1920.0]) {
    testWidgets(
      'Bolinha permite retirar e escalar; Banco abre empréstimos em $width px',
      (t) async {
        t.view.physicalSize = Size(width, 1000);
        t.view.devicePixelRatio = 1;
        addTearDown(t.view.resetPhysicalSize);
        addTearDown(t.view.resetDevicePixelRatio);
        final api = GameplayPresentationApi();
        await join(t, api);
        final controller = PresentationScope.maybeOf(
          t.element(find.byType(ElifootShellScreen)),
        )!;
        await controller.select(api, PresentationVersion.classic);
        await t.pumpAndSettle();
        final before = t.widget<RetroTable>(
          find.byKey(const Key('retro-roster')),
        );
        final starter = before.rows.indexWhere((r) => r.first == '● G');
        final name = before.rows[starter][1];
        final lead = before.leadingCells[starter]! as Row;
        final dot = lead.children.first as IconButton;
        final version = api.roundVersion;
        final finder = find.byKey(dot.key!);
        await t.ensureVisible(finder);
        await t.tap(finder);
        await t.pumpAndSettle();
        await t.tap(finder);
        await t.pumpAndSettle();
        var roster = t.widget<RetroTable>(
          find.byKey(const Key('retro-roster')),
        );
        expect(roster.rows.firstWhere((r) => r[1] == name).first, '· G');
        final removedIndex = roster.rows.indexWhere((r) => r[1] == name);
        final roleKey =
            ((roster.leadingCells[removedIndex] as Row).children.first
                    as IconButton)
                .key!;
        await t.tap(find.byKey(roleKey));
        await t.pumpAndSettle();
        roster = t.widget<RetroTable>(find.byKey(const Key('retro-roster')));
        expect(roster.rows.firstWhere((r) => r[1] == name).first, '● G');
        expect(api.roundVersion, version);
        if (width < 1024) {
          await t.tap(find.text('Banco').last);
        } else {
          await t.tap(find.text('Banco'));
        }
        await t.pumpAndSettle();
        expect(find.byKey(const Key('classic-bank-screen')), findsOneWidget);
        expect(
          find.byKey(const ValueKey('btn-emprestimo-bancario')),
          findsOneWidget,
        );
        expect(find.text('BANCO (RESERVAS)'), findsNothing);
        expect(
          find.byKey(const ValueKey('btn-reformar-estadio')),
          findsNothing,
        );
        await t.tap(find.text('Estádio'));
        await t.pumpAndSettle();
        expect(find.byType(ClassicStadiumView), findsOneWidget);
        expect(
          find.byKey(const ValueKey('btn-emprestimo-bancario')),
          findsNothing,
        );
        expect(t.takeException(), isNull);
        await t.pumpWidget(const SizedBox());
        api.disconnectRealtime();
      },
    );
  }

  test(
    'Desenho do estádio muda com ampliação, conservação e obras reais',
    () async {
      Future<List<int>> render({
        required int capacity,
        required int usable,
        required double condition,
        bool expansion = false,
      }) async {
        final recorder = ui.PictureRecorder();
        final canvas = Canvas(recorder);
        ClassicStadiumPainter(
          capacity: capacity,
          usable: usable,
          condition: condition,
          expansion: expansion,
          renovation: false,
        ).paint(canvas, const Size(720, 400));
        final picture = recorder.endRecording();
        final image = await picture.toImage(720, 400);
        final data = await image.toByteData();
        image.dispose();
        picture.dispose();
        return data!.buffer.asUint8List();
      }

      final small = await render(capacity: 8000, usable: 8000, condition: 100);
      final expanded = await render(
        capacity: 40000,
        usable: 40000,
        condition: 100,
      );
      final worn = await render(capacity: 40000, usable: 22000, condition: 60);
      final construction = await render(
        capacity: 40000,
        usable: 22000,
        condition: 60,
        expansion: true,
      );
      expect(listEquals(small, expanded), isFalse);
      expect(listEquals(expanded, worn), isFalse);
      expect(listEquals(worn, construction), isFalse);
    },
  );
}
