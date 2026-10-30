// Explicit local QA runner. Not discovered by the hermetic test/ suite.
// Renders the production Flutter widgets through Flutter's test engine, not a browser.
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

const boundaryKey = ValueKey('live-009-render');

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
  // Replay exact, freshly captured HTTP bodies. This is transport replay, not
  // direct networking from a widget test or a real browser session.
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
  final directory = Directory('../../docs/audit-009/live-flutter-render')
    ..createSync(recursive: true);
  final evidence = <Map<String, dynamic>>[];

  Widget app(Widget child) => RepaintBoundary(
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
          backgroundColor: const Color(0xFF062E0F),
          title: const Text(
            'QA 009 · FLUTTER LOCAL · API/SQLite REAIS',
            style: TextStyle(fontSize: 12, color: Color(0xFFFFD700)),
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
        '${directory.path}/$name.png',
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
    await ready(
      tester,
      find
              .byKey(const ValueKey('ranking-table'))
              .hitTestable()
              .evaluate()
              .isNotEmpty
          ? find.byKey(const ValueKey('ranking-table'))
          : find.text('RANKINGS DE JOGADORES'),
    );
  }

  Future<void> font(WidgetTester tester) async {
    debugPrint('QA009 loading local font');
    await tester.runAsync(() async {
      final bytes = await File('C:/Windows/Fonts/consola.ttf').readAsBytes();
      final loader = FontLoader('ClassicQA')
        ..addFont(Future.value(ByteData.sublistView(bytes)));
      await loader.load();
      final mono = FontLoader('monospace')
        ..addFont(Future.value(ByteData.sublistView(bytes)));
      await mono.load();
    });
    debugPrint('QA009 local font loaded');
  }

  testWidgets(
    '009 captured real API data: render categories, scopes, history and season awards',
    (tester) async {
      tester.view.physicalSize = const Size(1280, 1000);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      await font(tester);
      debugPrint('QA009 mounting production ranking widget');
      await tester.pumpWidget(
        app(
          PlayerRankingsScreen(
            apiClient: api,
            worldId: worldId,
            currentSeason: 2,
          ),
        ),
      );
      debugPrint('QA009 waiting for live ranking response');
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
        final data = await api.getPlayerRankings(worldId, scope: scope.key);
        final rows = data['rows'] as List;
        expect(rows, isNotEmpty);
        final leader = rows.first as Map;
        await ready(tester, find.text(leader['displayName'] as String));
        expect(find.text(leader['displayName'] as String), findsWidgets);
        final table = tester.widget<DataTable>(
          find.byKey(const ValueKey('ranking-table')),
        );
        expect(
          (table.rows.first.cells[3].child as Text).data,
          '${leader['matchesPlayed']}',
        );
        expect(
          (table.rows.first.cells[4].child as Text).data,
          '${leader['goalsScored']}',
        );
        await capture(tester, 'scorers-${scope.key}');
        evidence.add({
          'category': 'scorers',
          'scope': scope.key,
          'leader': leader,
          'renderedMatchesAndGoalsMatchLiveApi': true,
        });
      }
      await select(tester, 'ranking-scope', 'Geral');
      for (final category in {
        'goalkeepers': 'Goleiros',
        'defenders': 'Defensores',
      }.entries) {
        await select(tester, 'ranking-category', category.value);
        final data = await api.getPlayerRankings(
          worldId,
          category: category.key,
        );
        final leader = (data['rows'] as List).first as Map;
        await ready(tester, find.text(leader['displayName'] as String));
        final table = tester.widget<DataTable>(
          find.byKey(const ValueKey('ranking-table')),
        );
        expect(
          (table.rows.first.cells[3].child as Text).data,
          '${leader['matchesPlayed']}',
        );
        expect(
          (table.rows.first.cells[4].child as Text).data,
          '${leader[category.key == 'goalkeepers' ? 'goalsConceded' : 'yellowCards']}',
        );
        expect(leader['matchesPlayed'] as num, greaterThanOrEqualTo(20));
        expect(find.text('AINDA NÃO ELEGÍVEIS'), findsOneWidget);
        await capture(tester, '${category.key}-general');
        evidence.add({
          'category': category.key,
          'scope': 'general',
          'leader': leader,
          'renderedValuesMatchLiveApi': true,
        });
      }
      await select(tester, 'ranking-category', 'Artilheiros');
      await ready(tester, find.text('Mateus Alves*'));
      await tester.tap(find.text('Mateus Alves*').first);
      await ready(tester, find.text('ARTILHEIRO: 1 VEZES'));
      expect(
        find.text('Total de jogos: 17 | Total de gols: 10'),
        findsOneWidget,
      );
      expect(
        find.byKey(const ValueKey('player-award-history')),
        findsOneWidget,
      );
      await capture(tester, 'player-history-mateus');
      await tester.tap(find.text('FECHAR'));
      await tester.pumpAndSettle();
      await tester.pumpWidget(
        app(
          Builder(
            builder: (context) => Center(
              child: ElevatedButton(
                onPressed: () => showSeasonTopScorersDialog(
                  context: context,
                  apiClient: api,
                  worldId: worldId,
                  season: 1,
                ),
                child: const Text('VER ENCERRAMENTO REAL T1'),
              ),
            ),
          ),
        ),
      );
      await tester.tap(find.text('VER ENCERRAMENTO REAL T1'));
      await ready(
        tester,
        find.byKey(const ValueKey('season-end-awards-dialog')),
      );
      expect(find.text('Mateus Alves*'), findsWidgets);
      expect(find.text('Thiago Machado*'), findsWidgets);
      expect(find.textContaining('3.000.000'), findsWidgets);
      await capture(tester, 'season-1-awards');
      evidence.add({
        'season': 1,
        'coWinnersVisible': ['Mateus Alves*', 'Thiago Machado*'],
        'awardPerWinner': 3000000,
      });
      await tester.pumpWidget(const SizedBox.shrink());
      File('${directory.path}/validation.json').writeAsStringSync(
        const JsonEncoder.withIndent('  ').convert({
          'method':
              'Production Flutter widgets rendered by flutter test; exact response bodies captured from the running API and real SQLite season, with transport replayed in memory.',
          'browserUsed': false,
          'inventedSportingDataUsed': false,
          'httpTransportReplayed': true,
          'responseSnapshotCapturedAt': snapshot['capturedAt'],
          'worldId': worldId,
          'checks': evidence,
        }),
      );
    },
    timeout: const Timeout(Duration(seconds: 60)),
  );

  testWidgets(
    '009 captured real API data: 390px layout and horizontal table scrolling',
    (tester) async {
      tester.view.physicalSize = const Size(390, 844);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      await font(tester);
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
      expect(tester.takeException(), isNull);
      await capture(tester, 'width-390-left');
      final horizontal = find
          .ancestor(
            of: find.byKey(const ValueKey('ranking-table')),
            matching: find.byType(SingleChildScrollView),
          )
          .evaluate()
          .where(
            (e) =>
                (e.widget as SingleChildScrollView).scrollDirection ==
                Axis.horizontal,
          )
          .first;
      final horizontalFinder = find.byWidget(horizontal.widget);
      await tester.dragFrom(
        tester.getTopLeft(horizontalFinder) + const Offset(280, 45),
        const Offset(-260, 0),
      );
      await tester.pumpAndSettle();
      expect(
        (horizontal.widget as SingleChildScrollView).controller!.offset,
        greaterThan(0),
      );
      await capture(tester, 'width-390-scrolled');
      await tester.pumpWidget(const SizedBox.shrink());
    },
    timeout: const Timeout(Duration(seconds: 60)),
  );
}
