// Explicit local QA runner for Task 010 visual polish & responsive verification.
// Renders production Flutter widgets using captured real API/SQLite responses.
import 'dart:convert';
import 'dart:io';
import 'dart:ui' as ui;

import 'package:elifoot_flutter/api_client.dart';
import 'package:elifoot_flutter/player_rankings.dart';
import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

const boundaryKey = ValueKey('live-010-render');

void main() {
  final sessionFile = File('../../docs/audit-009/dev-session.json');
  final session =
      jsonDecode(sessionFile.readAsStringSync().replaceFirst('\uFEFF', ''))
          as Map<String, dynamic>;
  final snapshot =
      jsonDecode(
            File(
              '../../docs/audit-009/live-ui-response-snapshot.json',
            ).readAsStringSync(),
          )
          as Map<String, dynamic>;
  final responses = snapshot['responses'] as Map<String, dynamic>;

  final api = ElifootApiClient(
    baseUrl: session['ApiUrl'] as String,
    httpClient: MockClient((request) async {
      final entries = request.url.queryParameters.entries.toList()
        ..sort((a, b) => a.key.compareTo(b.key));
      final uri = request.url.replace(
        queryParameters: {for (final e in entries) e.key: e.value},
      );
      final key = '${uri.path}${entries.isNotEmpty ? '?${uri.query}' : ''}';
      final captured = responses[key] as Map<String, dynamic>?;
      if (captured == null) {
        throw StateError('No captured real API response for $key');
      }
      return http.Response(
        captured['body'] as String,
        captured['status'] as int,
        headers: {'content-type': 'application/json; charset=utf-8'},
      );
    }),
  );

  final worldId = session['WorldId'] as String;
  final outDir = Directory('../../docs/evidence/visual_010')
    ..createSync(recursive: true);
  final checks = <Map<String, dynamic>>[];

  Widget app(Widget child, {String subtitle = '1280px DESKTOP'}) =>
      RepaintBoundary(
        key: boundaryKey,
        child: MaterialApp(
          debugShowCheckedModeBanner: false,
          theme: ThemeData(
            brightness: Brightness.dark,
            useMaterial3: false,
            fontFamily: 'ClassicQA',
            scaffoldBackgroundColor: const Color(0xFF0A4D1C),
          ),
          home: Scaffold(
            appBar: AppBar(
              toolbarHeight: 34,
              backgroundColor: const Color(0xFF062E0F),
              title: Text(
                'ELIFOOT 98 · QA 010 · $subtitle',
                style: const TextStyle(
                  fontSize: 11,
                  color: Color(0xFFFFD700),
                  fontWeight: FontWeight.bold,
                ),
              ),
            ),
            body: child,
          ),
        ),
      );

  Future<void> ready(WidgetTester tester, Finder expected) async {
    await tester.pumpAndSettle();
    expect(expected, findsWidgets);
    expect(find.byType(CircularProgressIndicator), findsNothing);
  }

  Future<void> capture(WidgetTester tester, String name) async {
    await tester.pumpAndSettle();
    expect(tester.takeException(), isNull);
    final boundary = tester.renderObject<RenderRepaintBoundary>(
      find.byKey(boundaryKey),
    );
    await tester.runAsync(() async {
      final image = await boundary.toImage(pixelRatio: 1);
      final bytes = await image.toByteData(format: ui.ImageByteFormat.png);
      File(
        '${outDir.path}/$name.png',
      ).writeAsBytesSync(bytes!.buffer.asUint8List());
      image.dispose();
    });
  }

  Future<void> select(WidgetTester tester, String key, String label) async {
    await tester.ensureVisible(find.byKey(ValueKey(key)));
    await tester.tap(find.byKey(ValueKey(key)));
    await tester.pumpAndSettle();
    await tester.tap(find.text(label).last);
    await tester.pump();
    await tester.pumpAndSettle();
  }

  Future<void> loadFont(WidgetTester tester) async {
    await tester.runAsync(() async {
      final bytes = await File('C:/Windows/Fonts/consola.ttf').readAsBytes();
      final loader = FontLoader('ClassicQA')
        ..addFont(Future.value(ByteData.sublistView(bytes)));
      await loader.load();
      final mono = FontLoader('monospace')
        ..addFont(Future.value(ByteData.sublistView(bytes)));
      await mono.load();
    });
  }

  testWidgets(
    '010 Desktop & Tablet visual captures (1280px, 1024px, 768px): general, cup, divisions, goalkeepers, defenders, player history, season end awards',
    (tester) async {
      tester.view.physicalSize = const Size(1280, 960);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      await loadFont(tester);

      await tester.pumpWidget(
        app(
          PlayerRankingsScreen(
            apiClient: api,
            worldId: worldId,
            currentSeason: 2,
          ),
        ),
      );
      await ready(tester, find.text('Mateus Alves*'));

      for (final scope in <String, String>{
        'general': 'Geral',
        'division1': '1ª divisão',
        'division2': '2ª divisão',
        'division3': '3ª divisão',
        'division4': '4ª divisão',
        'cup': 'Taça',
      }.entries) {
        if (scope.key != 'general') {
          await select(tester, 'ranking-scope', scope.value);
        }
        await capture(tester, 'desktop_1280_scorers_${scope.key}');
        checks.add({
          'viewport': '1280x960',
          'screen': 'scorers_${scope.key}',
          'overflow': false,
        });
      }

      await select(tester, 'ranking-scope', 'Geral');

      // Goalkeepers
      await select(tester, 'ranking-category', 'Goleiros');
      expect(find.text('AINDA NÃO ELEGÍVEIS'), findsOneWidget);
      await capture(tester, 'desktop_1280_goalkeepers_general');
      checks.add({
        'viewport': '1280x960',
        'screen': 'goalkeepers_general',
        'overflow': false,
      });

      // Defenders
      await select(tester, 'ranking-category', 'Defensores');
      expect(find.text('AINDA NÃO ELEGÍVEIS'), findsOneWidget);
      await capture(tester, 'desktop_1280_defenders_general');
      checks.add({
        'viewport': '1280x960',
        'screen': 'defenders_general',
        'overflow': false,
      });

      // Back to Scorers -> Player History
      await select(tester, 'ranking-category', 'Artilheiros');
      await ready(tester, find.text('Mateus Alves*'));
      await tester.tap(find.text('Mateus Alves*').first);
      await ready(tester, find.text('ARTILHEIRO: 1 VEZ'));
      expect(find.text('ARTILHEIRO: 1 VEZES'), findsNothing);
      expect(find.text('Guarani da Serra'), findsWidgets);
      expect(find.text('f7ca895f-a395-994f-a2e4-8b0fc7ad8fb7'), findsNothing);
      expect(find.text('30/10/2026'), findsOneWidget);
      expect(find.text('R\$ 3.000.000'), findsWidgets);
      await capture(tester, 'desktop_1280_player_history_mateus');
      checks.add({
        'viewport': '1280x960',
        'screen': 'player_history_mateus',
        'pluralizationFixed': 'ARTILHEIRO: 1 VEZ',
        'historicalClubNameResolved': 'Guarani da Serra',
        'dateFormatted': '30/10/2026',
        'moneyFormatted': 'R\$ 3.000.000',
        'overflow': false,
      });

      await tester.tap(find.text('FECHAR'));
      await tester.pumpAndSettle();

      // Season 1 End Awards Modal
      await tester.pumpWidget(
        app(
          Builder(
            builder: (context) => Center(
              child: ClassicActionButton(
                label: 'ABRIR ENCERRAMENTO TEMPORADA 1',
                onPressed: () => showSeasonTopScorersDialog(
                  context: context,
                  apiClient: api,
                  worldId: worldId,
                  season: 1,
                ),
              ),
            ),
          ),
        ),
      );
      await tester.tap(find.text('ABRIR ENCERRAMENTO TEMPORADA 1'));
      await ready(
        tester,
        find.byKey(const ValueKey('season-end-awards-dialog')),
      );
      expect(find.text('Mateus Alves*'), findsWidgets);
      expect(find.text('Thiago Machado*'), findsWidgets);
      expect(find.text('R\$ 3.000.000'), findsWidgets);
      await capture(tester, 'desktop_1280_season_1_awards');
      await tester.tap(find.text('FECHAR'));
      await tester.pumpAndSettle();

      // 1024px & 768px desktop/tablet captures
      for (final width in [1024.0, 768.0]) {
        tester.view.physicalSize = Size(width, 900);
        await tester.pumpWidget(
          app(
            PlayerRankingsScreen(
              apiClient: api,
              worldId: worldId,
              currentSeason: 2,
            ),
            subtitle: '${width.toInt()}px VIEWPORT',
          ),
        );
        await ready(tester, find.text('Mateus Alves*'));
        final prefix = width == 1024 ? 'desktop_1024' : 'tablet_768';
        await capture(tester, '${prefix}_scorers_general');
        checks.add({
          'viewport': '${width.toInt()}x900',
          'screen': 'scorers_general',
          'overflow': false,
        });
      }
      await tester.pumpWidget(const SizedBox.shrink());
    },
    timeout: const Timeout(Duration(seconds: 90)),
  );

  testWidgets(
    '010 Mobile responsive captures (320px, 360px, 390px, 430px): left, scrolled with pinned player name, goalkeepers, defenders, player history, season end awards',
    (tester) async {
      await loadFont(tester);
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      tester.view.devicePixelRatio = 1;

      for (final width in [320.0, 360.0, 390.0, 430.0]) {
        final wInt = width.toInt();
        tester.view.physicalSize = Size(width, 844);
        await tester.pumpWidget(
          app(
            PlayerRankingsScreen(
              key: ValueKey('mobile-screen-$wInt'),
              apiClient: api,
              worldId: worldId,
              currentSeason: 2,
            ),
            subtitle: '${wInt}px MOBILE',
          ),
        );
        await ready(tester, find.text('Mateus Alves*'));
        await capture(tester, 'mobile_${wInt}_scorers_left');

        // Scroll horizontally at the top of the table and verify Mateus Alves* remains visible in pinned column
        final horizontal = find.byKey(
          const ValueKey('ranking-table-horizontal'),
        );
        await tester.dragFrom(
          tester.getTopLeft(horizontal) + const Offset(220, 55),
          const Offset(-280, 0),
        );
        await tester.pumpAndSettle();
        final scrollWidget = tester.widget<SingleChildScrollView>(horizontal);
        expect(scrollWidget.controller!.offset, greaterThan(0));
        expect(find.text('Mateus Alves*').hitTestable(), findsWidgets);
        await capture(tester, 'mobile_${wInt}_scorers_scrolled');
        checks.add({
          'viewport': '${wInt}x844',
          'screen': 'scorers_mobile_scroll',
          'horizontalOffset': scrollWidget.controller!.offset,
          'playerNameVisibleAfterHorizontalScroll': true,
          'overflow': false,
        });

        if (wInt == 390) {
          // Goalkeepers on 390px mobile
          await select(tester, 'ranking-category', 'Goleiros');
          await capture(tester, 'mobile_390_goalkeepers_left');
          final gkHoriz = find.byKey(
            const ValueKey('ranking-table-horizontal'),
          );
          await tester.dragFrom(
            tester.getTopLeft(gkHoriz) + const Offset(220, 55),
            const Offset(-280, 0),
          );
          await tester.pumpAndSettle();
          await capture(tester, 'mobile_390_goalkeepers_scrolled');

          // Defenders on 390px mobile
          await select(tester, 'ranking-category', 'Defensores');
          await capture(tester, 'mobile_390_defenders_left');
          final dfHoriz = find.byKey(
            const ValueKey('ranking-table-horizontal'),
          );
          await tester.dragFrom(
            tester.getTopLeft(dfHoriz) + const Offset(220, 55),
            const Offset(-280, 0),
          );
          await tester.pumpAndSettle();
          await capture(tester, 'mobile_390_defenders_scrolled');

          // Back to Scorers -> Player History on 390px mobile
          await select(tester, 'ranking-category', 'Artilheiros');
          await ready(tester, find.text('Mateus Alves*'));
          await tester.tap(find.text('Mateus Alves*').hitTestable().first);
          await ready(tester, find.text('ARTILHEIRO: 1 VEZ'));
          await capture(tester, 'mobile_390_player_history_mateus');
          await tester.tap(find.text('FECHAR'));
          await tester.pumpAndSettle();

          // Season 1 End Awards Modal on 390px mobile
          await tester.pumpWidget(
            app(
              Builder(
                builder: (context) => Center(
                  child: ClassicActionButton(
                    label: 'ABRIR ENCERRAMENTO T1',
                    onPressed: () => showSeasonTopScorersDialog(
                      context: context,
                      apiClient: api,
                      worldId: worldId,
                      season: 1,
                    ),
                  ),
                ),
              ),
              subtitle: '390px MOBILE',
            ),
          );
          await tester.tap(find.text('ABRIR ENCERRAMENTO T1'));
          await ready(
            tester,
            find.byKey(const ValueKey('season-end-awards-dialog')),
          );
          await capture(tester, 'mobile_390_season_1_awards');
          await tester.tap(find.text('FECHAR'));
          await tester.pumpAndSettle();
        }
      }

      File('${outDir.path}/validation_010.json').writeAsStringSync(
        const JsonEncoder.withIndent('  ').convert({
          'taskId': 'ELIFOOT_VISUAL_POLISH_AND_RESPONSIVE_010',
          'worldId': worldId,
          'sourceSnapshotCapturedAt': snapshot['capturedAt'],
          'checks': checks,
        }),
      );
      await tester.pumpWidget(const SizedBox.shrink());
    },
    timeout: const Timeout(Duration(seconds: 120)),
  );
}
