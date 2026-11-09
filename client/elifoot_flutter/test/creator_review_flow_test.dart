import 'dart:async';
import 'dart:convert';

import 'package:elifoot_flutter/api_client.dart';
import 'package:elifoot_flutter/club_theme.dart';
import 'package:elifoot_flutter/creator_world_review.dart';
import 'package:elifoot_flutter/world_creator.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

import 'historical_creator_test_support.dart';

const _owner = '11111111-1111-1111-1111-111111111111';
const _worldName = 'Liga QA de épocas selecionadas';

http.Response _json(dynamic value, {int status = 200}) => http.Response.bytes(
  utf8.encode(jsonEncode(value)),
  status,
  headers: {'content-type': 'application/json; charset=utf-8'},
);

Map<String, dynamic> _copy(Map<String, dynamic> value) =>
    Map<String, dynamic>.from(jsonDecode(jsonEncode(value)) as Map);

Map<String, dynamic> _catalogClub(int index) {
  final year = index == 32 || index >= 64 ? '2025' : '1985';
  final id = index == 0 || index == 32 ? 'mesmo-clube' : 'qa-$index';
  return {
    'key': 'PT:$year:$id:Male',
    'name': index == 0 || index == 32
        ? 'Clube QA da história'
        : 'Equipe QA $index',
    'countryCode': 'PT',
    'countryName': 'Portugal',
    'season': year,
    'gender': 'Male',
    'rank': index + 1,
    'isFictional': false,
    'verification': {'status': 'REAL_CLUB_VERIFIED'},
    'players': [
      for (var player = 0; player < 18; player++)
        {
          'playerId': '$year-$id-$player',
          'playerName': 'Atleta QA $year-$id-$player',
          'position': player < 2
              ? 'G'
              : player < 8
              ? 'D'
              : player < 14
              ? 'M'
              : 'A',
          'strength': 25 + (index + player) % 26,
          'playerOrigin': 'REAL_HISTORICAL_CATALOG',
          'identityOrigin': 'Real',
          'source': 'Fixture local de teste',
        },
    ],
  };
}

class _ReviewServer {
  final clubs = [for (var i = 0; i < 128; i++) _catalogClub(i)];
  int catalogCount = 128;
  bool generatedPlayers = false;
  List<Map<String, dynamic>> get catalogClubs =>
      clubs.take(catalogCount).toList();
  final requests = <http.Request>[];
  final previews = <String, Map<String, dynamic>>{};
  final reassignments = <Map<String, dynamic>>[];
  final confirms = <http.Request>[];
  String? malformed;
  String? lastDraftId;
  int _draftNumber = 0;
  int failConfirmCount = 0;
  Completer<http.Response>? pendingConfirm;

  late final api = ElifootApiClient(
    httpClient: MockClient((request) async {
      requests.add(request);
      final path = request.url.path;
      if (path.endsWith('/last-configuration')) return _json(null);
      if (path.endsWith('/historical-countries')) {
        expect(request.method, 'GET');
        expect(request.url.queryParameters['gender'], 'Male');
        return _json([
          {'code': 'PT', 'name': 'Portugal', 'clubCount': catalogClubs.length},
        ]);
      }
      if (path.endsWith('/historical-years')) {
        expect(request.method, 'GET');
        expect(request.url.queryParameters['countries'], 'PT');
        return _json([
          for (final year in ['2025', '1985'])
            {
              'year': year,
              'clubCount': catalogClubs
                  .where((club) => club['season'] == year)
                  .length,
              'countries': [
                {
                  'code': 'PT',
                  'name': 'Portugal',
                  'clubCount': catalogClubs
                      .where((club) => club['season'] == year)
                      .length,
                },
              ],
            },
        ]);
      }
      if (path.endsWith('/catalog')) {
        final body = jsonDecode(request.body) as Map;
        expect(body['countries'], ['PT']);
        expect(body['catalogYears'], ['2025', '1985']);
        return _json({'clubs': catalogClubs, 'references': [], 'warnings': []});
      }
      if (path.endsWith('/preview')) {
        final body = jsonDecode(request.body) as Map;
        expect(body['name'], _worldName);
        expect(body['catalogYears'], ['2025', '1985']);
        expect(body['countries'], ['PT']);
        expect(body['generateAllRosterPlayers'] == true, generatedPlayers);
        final assignments = (body['assignments'] as List).cast<Map>();
        final selectedClubs = [
          for (var index = 0; index < 128; index++)
            if (index < catalogCount)
              _previewClub(clubs[index], generated: generatedPlayers)
            else
              _previewClub({
                ...clubs[index],
                'key': 'generated-qa-$index',
                'name': 'Clube gerado QA $index',
                'isFictional': true,
              }, generated: true),
        ];
        final byKey = {for (final club in selectedClubs) club['key']: club};
        final assigned = assignments.map((entry) => entry['clubKey']).toSet();
        final entries = [
          for (final entry in assignments)
            {'division': entry['division'], 'club': byKey[entry['clubKey']]},
          for (final club in selectedClubs)
            if (!assigned.contains(club['key'])) {'division': 5, 'club': club},
        ];
        return _json(_preview(entries));
      }
      final reassignment = RegExp(
        r'/preview/([^/]+)/reassign$',
      ).firstMatch(path);
      if (reassignment != null) {
        final source = previews[reassignment.group(1)];
        expect(
          source,
          isNotNull,
          reason: 'Reassign must use an existing draft',
        );
        final body = Map<String, dynamic>.from(jsonDecode(request.body) as Map);
        expect(body['name'], _worldName);
        expect(body['maxHumans'], 1);
        reassignments.add(body);
        final assignments = (body['assignments'] as List).cast<Map>();
        final byKey = {
          for (final entry in source!['clubs'] as List)
            (entry['club'] as Map)['key']: entry['club'],
        };
        _expectValidAssignments(assignments);
        expect(
          assignments.map((entry) => entry['clubKey']).toSet(),
          byKey.keys.toSet(),
        );
        final entries = [
          for (final assignment in assignments)
            {
              'division': assignment['division'],
              'club': byKey[assignment['clubKey']],
            },
        ];
        // Controlled server responses let this widget test verify that the UI
        // honors the returned order. The real seeded algorithm has domain tests.
        if (body['districtMode'] case final String mode) {
          final primary = entries
              .where((entry) => entry['division'] != 5)
              .toList();
          var districts = entries
              .where((entry) => entry['division'] == 5)
              .toList();
          if (mode == 'Random') {
            districts = districts.reversed.toList();
          } else if (mode == 'Ranking') {
            districts.sort(
              (a, b) => ((a['club'] as Map)['rank'] as int).compareTo(
                (b['club'] as Map)['rank'] as int,
              ),
            );
          }
          return _json(_preview([...primary, ...districts]));
        }
        return _json(_preview(entries));
      }
      if (path.contains('/confirm/')) {
        confirms.add(request);
        expect(previews.containsKey(path.split('/').last), isTrue);
        if (pendingConfirm != null) return pendingConfirm!.future;
        if (failConfirmCount > 0) {
          failConfirmCount--;
          return _json({
            'error': 'Falha temporária de QA. Tente novamente.',
          }, status: 503);
        }
        return _json({'id': 'world-qa', 'name': _worldName});
      }
      return _json({'error': 'Rota inesperada: $path'}, status: 500);
    }),
  );

  Map<String, dynamic> _previewClub(
    Map<String, dynamic> club, {
    required bool generated,
  }) {
    final result = _copy(club);
    if (generated) {
      result['players'] = [
        for (final player in (result['players'] as List).cast<Map>())
          {
            ...player,
            'playerOrigin': 'FICTIONAL_GENERATED',
            'identityOrigin': 'Generated',
          },
      ];
    }
    return result;
  }

  Map<String, dynamic> _preview(List<Map<String, dynamic>> rawEntries) {
    final entries = [for (final entry in rawEntries) _copy(entry)];
    if (malformed == 'duplicate') {
      // Duplicate across groups so the review can show the invalid response
      // without two equal row keys in the same division widget.
      entries[40]['club'] = _copy(
        entries.first['club'] as Map<String, dynamic>,
      );
    } else if (malformed == 'too-many') {
      entries.add({'division': 5, 'club': _catalogClub(128)});
    } else if (malformed == 'division-count') {
      entries[7]['division'] = 2;
    }
    final id = 'draft-qa-${++_draftNumber}';
    final data = <String, dynamic>{
      'draftId': id,
      'name': _worldName,
      'clubs': entries,
      'pendingSlots': 0,
      'errors': <String>[],
      'warnings': <String>[],
      'dataSummary': {
        'verifiedRealClubs': catalogCount,
        'completeRealSquads': catalogCount,
        'incompleteRealSquads': 0,
        'missingClubSlots': 0,
        'selectedRealClubs': entries
            .where((entry) => (entry['club'] as Map)['isFictional'] != true)
            .length,
        'selectedFictionalClubs': entries
            .where((entry) => (entry['club'] as Map)['isFictional'] == true)
            .length,
        'realCurrentPlayers': generatedPlayers
            ? 0
            : entries.fold<int>(
                0,
                (sum, entry) =>
                    sum + ((entry['club'] as Map)['players'] as List).length,
              ),
        'generatedPlayers': generatedPlayers
            ? entries.fold<int>(
                0,
                (sum, entry) =>
                    sum + ((entry['club'] as Map)['players'] as List).length,
              )
            : 0,
      },
    };
    previews[id] = _copy(data);
    lastDraftId = id;
    return data;
  }
}

void _expectValidAssignments(Iterable<Map> assignments) {
  final all = assignments.toList();
  expect(all.length, 128);
  expect(all.map((entry) => entry['clubKey']).toSet().length, 128);
  expect(
    [
      for (var division = 1; division <= 5; division++)
        all.where((entry) => entry['division'] == division).length,
    ],
    [8, 8, 8, 8, 96],
  );
}

List<Map<String, dynamic>> _reviewEntries(WidgetTester tester) =>
    (tester
                .widget<CreatorWorldReview>(find.byType(CreatorWorldReview))
                .preview['clubs']
            as List)
        .map((entry) => Map<String, dynamic>.from(entry as Map))
        .toList();

Map<String, List<int>> _strengths(List<Map<String, dynamic>> entries) => {
  for (final entry in entries)
    (entry['club'] as Map)['key'].toString(): [
      for (final player in (entry['club'] as Map)['players'] as List)
        (player['strength'] as num).toInt(),
    ],
};

Finder _next() => find.widgetWithText(ElevatedButton, 'CONTINUAR');
Finder _confirm() => find.widgetWithText(ElevatedButton, 'CONFIRMAR CRIAÇÃO');

Future<void> _tap(WidgetTester tester, Finder finder) async {
  await tester.ensureVisible(finder);
  await tester.tap(finder);
  await tester.pumpAndSettle();
}

Future<void> _openPreview(
  WidgetTester tester,
  _ReviewServer server, {
  bool generatedRoster = false,
}) async {
  server.generatedPlayers = generatedRoster;
  tester.view.devicePixelRatio = 1;
  tester.view.physicalSize = const Size(1366, 1000);
  addTearDown(tester.view.resetDevicePixelRatio);
  addTearDown(tester.view.resetPhysicalSize);
  await tester.pumpWidget(
    MaterialApp(
      theme: ClassicTheme.classicGreen().material(),
      home: Builder(
        builder: (context) => Scaffold(
          body: ElevatedButton(
            onPressed: () async {
              final created = await Navigator.push<bool>(
                context,
                MaterialPageRoute(
                  builder: (_) => WorldCreatorScreen(
                    api: server.api,
                    coachId: _owner,
                    worldCatalogRules: true,
                  ),
                ),
              );
              if (created == true && context.mounted) {
                ScaffoldMessenger.of(context).showSnackBar(
                  const SnackBar(content: Text('Mundo QA criado uma vez')),
                );
              }
            },
            child: const Text('ABRIR CRIADOR QA'),
          ),
        ),
      ),
    ),
  );
  await _tap(tester, find.text('ABRIR CRIADOR QA'));
  final nameField = find.byWidgetPredicate(
    (widget) =>
        widget is TextField && widget.decoration?.labelText == 'Nome do mundo',
  );
  await tester.enterText(nameField, _worldName);
  await _tap(tester, _next());
  await selectHistoricalCountries(tester, ['PT']);
  await _tap(tester, _next());
  await selectHistoricalYear(tester, '2025');
  await selectHistoricalYear(tester, '1985');
  await _tap(tester, _next()); // Divisões.
  await _tap(tester, _next()); // Distritais.
  if (generatedRoster && server.catalogCount < 128) {
    await _tap(tester, find.text('AJUSTAR ELENCOS E FONTES'));
    await _tap(tester, find.byKey(const ValueKey('source-generated')));
    await _tap(tester, find.widgetWithText(ElevatedButton, 'APLICAR OPÇÕES'));
  }
  await _tap(tester, _next()); // Elencos / fontes, modo real.
  if (generatedRoster && server.catalogCount == 128) {
    await _tap(tester, find.byKey(const ValueKey('source-generated')));
  }
  await _tap(tester, _next()); // Prévia.
  expect(find.text('7/8 • PRÉVIA'), findsOneWidget);
  expect(find.byKey(const ValueKey('creator-preview-review')), findsOneWidget);
}

void main() {
  testWidgets(
    'Revisão compartilha troca recíproca, variantes e forças entre etapas',
    (tester) async {
      final server = _ReviewServer();
      await _openPreview(tester, server);
      final original = _reviewEntries(tester);
      final originalStrengths = _strengths(original);
      final outgoing = server.clubs[0]['key'].toString();
      final incoming = server.clubs[32]['key'].toString();
      expect(outgoing, isNot(incoming));
      expect(server.clubs[0]['name'], server.clubs[32]['name']);
      await _tap(tester, _next());
      expect(find.text('8/8 • CONFIRMAR'), findsOneWidget);
      await _tap(tester, find.byKey(ValueKey('review-swap-$outgoing')));
      await tester.enterText(
        find.byKey(const ValueKey('division-swap-search')),
        'Clube QA da história',
      );
      await tester.pumpAndSettle();
      await _tap(
        tester,
        find.byKey(ValueKey('division-swap-option-$incoming')),
      );
      await _tap(tester, find.byKey(const ValueKey('division-swap-confirm')));
      expect(server.reassignments, hasLength(1));
      final assignments = (server.reassignments.single['assignments'] as List)
          .cast<Map>();
      _expectValidAssignments(assignments);
      expect(assignments.first['clubKey'], incoming);
      expect(
        assignments.singleWhere(
          (entry) => entry['clubKey'] == outgoing,
        )['division'],
        5,
      );
      expect(_strengths(_reviewEntries(tester)), originalStrengths);

      await _tap(tester, find.widgetWithText(OutlinedButton, 'VOLTAR'));
      expect(find.text('7/8 • PRÉVIA'), findsOneWidget);
      expect(_reviewEntries(tester).first['club']['key'], incoming);
      await _tap(tester, find.byKey(const ValueKey('review-refresh')));
      expect(server.reassignments, hasLength(2));
      expect(_strengths(_reviewEntries(tester)), originalStrengths);
      _expectValidAssignments(
        (server.reassignments.last['assignments'] as List).cast<Map>(),
      );
      expect(_reviewEntries(tester).first['club']['key'], incoming);

      await _tap(tester, find.byKey(const ValueKey('review-edit-divisions')));
      expect(find.text('4/8 • DIVISÕES'), findsOneWidget);
      final firstSlot = find.byKey(const ValueKey('division-1-position-1'));
      expect(
        find.descendant(
          of: firstSlot,
          matching: find.text('Clube QA da história (2025)'),
        ),
        findsOneWidget,
      );
      expect(
        server.requests.where(
          (request) => request.url.path.endsWith('/catalog'),
        ),
        hasLength(1),
      );
      await _tap(tester, _next());
      await _tap(tester, _next());
      await _tap(tester, _next());
      expect(find.text('7/8 • PRÉVIA'), findsOneWidget);
      expect(_reviewEntries(tester).first['club']['key'], incoming);
      expect(_strengths(_reviewEntries(tester)), originalStrengths);
      await _tap(tester, _next());

      server.pendingConfirm = Completer<http.Response>();
      final confirm = _confirm();
      await tester.ensureVisible(confirm);
      await tester.tap(confirm);
      await tester.tap(confirm);
      await tester.pump();
      expect(server.confirms, hasLength(1));
      expect(
        server.confirms.single.url.path,
        endsWith('/confirm/${server.lastDraftId}'),
      );
      expect(jsonDecode(server.confirms.single.body), isEmpty);
      expect(tester.widget<ElevatedButton>(confirm).onPressed, isNull);
      server.pendingConfirm!.complete(
        _json({'id': 'world-qa', 'name': _worldName}),
      );
      await tester.pumpAndSettle();
      expect(find.text('Mundo QA criado uma vez'), findsOneWidget);
      expect(server.confirms, hasLength(1));
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'Falha de confirmação permite retry com o mesmo draft e uma única criação',
    (tester) async {
      final server = _ReviewServer()..failConfirmCount = 1;
      await _openPreview(tester, server);
      await _tap(tester, _next());
      final draft = server.lastDraftId;
      await _tap(tester, _confirm());
      expect(find.text('8/8 • CONFIRMAR'), findsOneWidget);
      expect(server.confirms, hasLength(1));
      expect(tester.widget<ElevatedButton>(_confirm()).onPressed, isNotNull);
      expect(find.textContaining('Falha temporária de QA'), findsOneWidget);
      await _tap(tester, _confirm());
      expect(server.confirms, hasLength(2));
      expect(server.confirms.map((request) => request.url.path).toSet(), {
        '/api/world-creator/confirm/$draft',
      });
      expect(server.lastDraftId, draft);
      expect(find.text('Mundo QA criado uma vez'), findsOneWidget);
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'Primeira prévia mista já pode ser editada sem regenerar equipes ou atletas',
    (tester) async {
      final server = _ReviewServer()..catalogCount = 40;
      await _openPreview(tester, server, generatedRoster: true);
      expect(server.reassignments, isEmpty);
      final initial = _reviewEntries(tester);
      final strengths = _strengths(initial);
      final keys = strengths.keys.toSet();
      expect(
        initial.where((entry) => entry['club']['isFictional'] == true),
        hasLength(88),
      );
      expect(
        initial
            .expand((entry) => entry['club']['players'] as List)
            .every((player) => player['playerOrigin'] == 'FICTIONAL_GENERATED'),
        isTrue,
      );

      await _tap(tester, find.byKey(const ValueKey('review-edit-divisions')));
      expect(find.text('4/8 • DIVISÕES'), findsOneWidget);
      await _tap(
        tester,
        find.byKey(const ValueKey('swap-division-1-position-1')),
      );
      await tester.enterText(
        find.byKey(const ValueKey('division-swap-search')),
        'Clube gerado QA 127',
      );
      await tester.pumpAndSettle();
      await _tap(
        tester,
        find.byKey(const ValueKey('division-swap-option-generated-qa-127')),
      );
      await _tap(tester, find.byKey(const ValueKey('division-swap-confirm')));
      expect(server.reassignments, hasLength(1));
      final assignments = (server.reassignments.single['assignments'] as List)
          .cast<Map>();
      _expectValidAssignments(assignments);
      expect(assignments.first['clubKey'], 'generated-qa-127');
      expect(assignments.map((entry) => entry['clubKey']).toSet(), keys);

      await _tap(tester, _next()); // Distritais.
      await _tap(tester, _next()); // Elencos / fontes.
      await _tap(tester, _next()); // Prévia preservada no servidor.
      expect(find.text('7/8 • PRÉVIA'), findsOneWidget);
      expect(_reviewEntries(tester).first['club']['key'], 'generated-qa-127');
      expect(_strengths(_reviewEntries(tester)), strengths);
      expect(
        server.requests.where(
          (request) => request.url.path.endsWith('/preview'),
        ),
        hasLength(1),
      );
      expect(
        server.requests.where(
          (request) => request.url.path.endsWith('/catalog'),
        ),
        hasLength(1),
      );
      expect(tester.takeException(), isNull);
    },
  );

  testWidgets(
    'Ranking e sorteio após a prévia solicitam somente a redistribuição dos distritais',
    (tester) async {
      final server = _ReviewServer();
      await _openPreview(tester, server);
      final initial = _reviewEntries(tester);
      final strengths = _strengths(initial);
      final primary = initial
          .where((entry) => entry['division'] != 5)
          .map((entry) => entry['club']['key'])
          .toList();
      final rankedDistricts = initial
          .where((entry) => entry['division'] == 5)
          .map((entry) => entry['club']['key'])
          .toList();
      await _tap(tester, find.byKey(const ValueKey('review-edit-divisions')));
      await _tap(tester, _next());
      expect(find.text('5/8 • DISTRITAIS'), findsOneWidget);
      await _tap(tester, find.byKey(const ValueKey('Distritais:Ranking')));
      await _tap(tester, find.text('Sorteio com seed salva').last);
      expect(server.reassignments, hasLength(1));
      expect(server.reassignments.last['districtMode'], 'Random');
      final random = server.previews[server.lastDraftId]!['clubs'] as List;
      expect(
        random
            .where((entry) => entry['division'] != 5)
            .map((entry) => entry['club']['key'])
            .toList(),
        primary,
      );
      expect(
        random
            .where((entry) => entry['division'] == 5)
            .map((entry) => entry['club']['key'])
            .toList(),
        rankedDistricts.reversed.toList(),
      );

      await _tap(tester, find.byKey(const ValueKey('Distritais:Random')));
      await _tap(tester, find.text('Ranking dos clubes restantes').last);
      expect(server.reassignments, hasLength(2));
      expect(server.reassignments.last['districtMode'], 'Ranking');
      final ranking = server.previews[server.lastDraftId]!['clubs'] as List;
      expect(
        ranking
            .where((entry) => entry['division'] != 5)
            .map((entry) => entry['club']['key'])
            .toList(),
        primary,
      );
      expect(
        ranking
            .where((entry) => entry['division'] == 5)
            .map((entry) => entry['club']['key'])
            .toList(),
        rankedDistricts,
      );
      await _tap(tester, _next());
      await _tap(tester, _next());
      expect(find.text('7/8 • PRÉVIA'), findsOneWidget);
      expect(server.reassignments, hasLength(3));
      expect(server.reassignments.last.containsKey('districtMode'), isFalse);
      expect(_strengths(_reviewEntries(tester)), strengths);
      expect(
        _reviewEntries(tester)
            .where((entry) => entry['division'] == 5)
            .map((entry) => entry['club']['key'])
            .toList(),
        rankedDistricts,
      );
      expect(
        server.requests.where(
          (request) => request.url.path.endsWith('/preview'),
        ),
        hasLength(1),
      );
      expect(tester.takeException(), isNull);
    },
  );

  for (final malformed in ['duplicate', 'too-many', 'division-count']) {
    testWidgets(
      'Prévia malformada ($malformed) mostra bloqueio e impede continuar',
      (tester) async {
        final server = _ReviewServer();
        await _openPreview(tester, server);
        server.malformed = malformed;
        await _tap(tester, find.byKey(const ValueKey('review-refresh')));
        expect(server.reassignments, hasLength(1));
        expect(find.text('7/8 • PRÉVIA'), findsOneWidget);
        expect(tester.widget<ElevatedButton>(_next()).onPressed, isNull);
        final review = tester.widget<CreatorWorldReview>(
          find.byType(CreatorWorldReview),
        );
        expect(review.preview['errors'], [
          'A montagem precisa de 4 divisões com 8 clubes e 96 distritais, sem repetições.',
        ]);
        final warnings = find.byKey(
          const ValueKey('review-important-warnings'),
        );
        final blockingMessage = find.descendant(
          of: warnings,
          matching: find.textContaining('Bloqueante'),
        );
        if (blockingMessage.evaluate().isEmpty) {
          await _tap(tester, find.text('AVISOS IMPORTANTES'));
        }
        expect(blockingMessage, findsOneWidget);
        expect(
          find.descendant(
            of: warnings,
            matching: find.textContaining(
              'A montagem precisa de 4 divisões com 8 clubes e 96 distritais, sem repetições.',
            ),
          ),
          findsOneWidget,
        );
        expect(server.confirms, isEmpty);
        expect(tester.takeException(), isNull);
      },
    );
  }
}
