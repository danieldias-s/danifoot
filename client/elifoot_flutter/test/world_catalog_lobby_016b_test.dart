import 'dart:convert';
import 'package:elifoot_flutter/api_client.dart';
import 'package:elifoot_flutter/club_theme.dart';
import 'package:elifoot_flutter/world_initial_lobby_016b.dart';
import 'package:elifoot_flutter/world_catalog_status_016b.dart';
import 'package:elifoot_flutter/world_creator.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

const owner = '11111111-1111-1111-1111-111111111111';

class Fixture016B {
  final requests = <http.Request>[];
  bool rejectChoice = false;
  final view = <String, dynamic>{
    'worldId': 'world',
    'gameModeId': 'CLASSIC_SYNC',
    'hostCoachId': owner,
    'state': 'WAITING_FOR_PLAYERS',
    'capacity': 4,
    'registered': 2,
    'connected': 1,
    'canDraw': false,
    'canStart': false,
    'drawCompleted': false,
    'version': 1,
    'participants': [
      {
        'coachId': owner,
        'nickname': 'Fixture treinador com nome completo',
        'choice': 'UNEMPLOYED',
        'clubId': null,
        'clubName': null,
        'connected': true,
      },
      {
        'coachId': 'other',
        'nickname': 'Fixture visitante',
        'choice': 'FOURTH_DIVISION_DRAW',
        'clubId': null,
        'clubName': null,
        'connected': false,
      },
    ],
    'issues': [
      'Aguardando participantes: 2/4.',
      'Todos os participantes precisam estar conectados.',
    ],
  };
  late final api = ElifootApiClient(
    httpClient: MockClient((r) async {
      requests.add(r);
      dynamic value;
      if (r.url.path.contains('/world-lobby-016b/')) {
        if (r.method == 'POST' && r.url.path.endsWith('/choice')) {
          if (rejectChoice) {
            return http.Response(
              jsonEncode({'error': 'Clube já pertence a outro treinador.'}),
              400,
              headers: {'content-type': 'application/json'},
            );
          }
          final choice = jsonDecode(r.body) as Map;
          (view['participants'] as List).first['choice'] = choice['choice'];
        }
        if (r.url.path.endsWith('/draw')) {
          view['drawCompleted'] = true;
          view['canDraw'] = false;
          view['canStart'] = true;
          view['state'] = 'DRAW_COMPLETED';
        }
        if (r.url.path.endsWith('/start')) {
          view['state'] = 'IN_GAME';
          view['canStart'] = false;
        }
        value = view;
      } else if (r.url.path.endsWith('/clubs')) {
        value = [
          {
            'id': 'one',
            'name': 'Fixture clube da primeira divisão',
            'divisionTier': 'PrimeiraDivisao',
            'divisionDisplay': '1ª Divisão',
            'isHumanControlled': false,
          },
          {
            'id': 'four',
            'name': 'Fixture clube da quarta divisão',
            'divisionTier': 'QuartaDivisao',
            'divisionDisplay': '4ª Divisão',
            'isHumanControlled': false,
          },
          {
            'id': 'district',
            'name': 'Fixture distrital proibido',
            'divisionTier': 'Distrital',
            'isHumanControlled': false,
          },
        ];
      } else if (r.url.path.contains('/world-catalog-016b/')) {
        if (r.url.path.endsWith('/refresh')) {
          value = {'status': 'NOT_CONNECTED'};
        } else if (r.url.path.endsWith('/report')) {
          value = {
            'provider': {'status': 'NOT_CONNECTED'},
            'leagues': [],
            'coverage': [
              {
                'clubs': 0,
                'players': 0,
                'completeSquads': 0,
                'partialSquads': 0,
                'documentedPlayers': 0,
                'uncertainPlayers': 0,
                'warnings': ['Sem cobertura atual comprovada.'],
              },
            ],
          };
        } else {
          value = {
            'countries': [
              {'code': 'ES', 'name': 'Espanha'},
              {'code': 'GB', 'name': 'Inglaterra'},
            ],
            'states': [
              {
                'countryCode': 'ES',
                'status': 'NOT_CONNECTED',
                'lastError': 'NOT_CONNECTED',
                'lastSuccessfulUpdateAt': null,
                'nextEligibleUpdateAt': null,
              },
              {'countryCode': 'GB', 'status': 'NOT_REQUESTED'},
            ],
          };
        }
      } else if (r.url.path.endsWith('/seasons')) {
        value = ['2026'];
      } else if (r.url.path.endsWith('/countries')) {
        value = [
          for (final c in ['BR', 'AR', 'GB', 'ES', 'DE', 'IT', 'FR', 'PT'])
            {'code': c, 'name': c, 'maleClubs': 20, 'femaleClubs': 20},
        ];
      } else if (r.url.path.endsWith('/last-configuration')) {
        value = null;
      } else if (r.url.path.endsWith('/catalog')) {
        final body = jsonDecode(r.body) as Map;
        final countries = body['countries'] as List;
        value = {
          'clubs': [
            for (var i = 0; i < 128; i++)
              {
                'key': 'catalog-$i',
                'name': 'Clube de teste $i',
                'countryCode': countries[i % countries.length],
                'countryName': countries[i % countries.length],
                'season': body['season'],
                'gender': body['gender'],
                'initialProfile': {'clubInitialPower': 20, 'worldRank': i + 1},
                'verification': {'status': 'REAL_CLUB_VERIFIED'},
              },
          ],
          'references': [],
          'warnings': [],
        };
      } else if (r.url.path.endsWith('/preview')) {
        value = {
          'draftId': 'fixture-preview',
          'clubs': [],
          'pendingSlots': 128,
          'errors': ['Faltam 128 clubes.'],
          'warnings': [],
        };
      } else {
        throw StateError('Unexpected fixture route ${r.url}');
      }
      return http.Response(
        jsonEncode(value),
        200,
        headers: {'content-type': 'application/json; charset=utf-8'},
      );
    }),
  );
}

Future<void> app(WidgetTester t, Widget body, double width) async {
  await t.binding.setSurfaceSize(Size(width, 1000));
  addTearDown(() => t.binding.setSurfaceSize(null));
  await t.pumpWidget(
    MaterialApp(theme: ClassicTheme.classicGreen().material(), home: body),
  );
  await t.pumpAndSettle();
}

Future<void> finish(WidgetTester t) async {
  await t.pumpWidget(const SizedBox());
  await t.pumpAndSettle();
}

void main() {
  for (final width in [360.0, 390.0, 430.0, 768.0, 1366.0, 1920.0]) {
    testWidgets('Initial lobby uses authoritative counters and fits $width', (
      t,
    ) async {
      final f = Fixture016B();
      await app(
        t,
        WorldInitialLobby016B(
          api: f.api,
          worldId: 'world',
          coachId: owner,
          onStarted: () async {},
          onLeave: () async {},
        ),
        width,
      );
      expect(find.text('Treinadores: 2/4 • Conectados: 1/4'), findsOneWidget);
      await t.ensureVisible(find.byKey(const Key('initial-draw')));
      await t.pumpAndSettle();
      expect(
        t
            .widget<ElevatedButton>(find.byKey(const Key('initial-draw')))
            .onPressed,
        isNull,
      );
      expect(
        t
            .widget<ElevatedButton>(find.byKey(const Key('initial-start')))
            .onPressed,
        isNull,
      );
      expect(t.takeException(), isNull);
      await finish(t);
    });
    testWidgets(
      'Country status and no-key report fit $width without invented coverage',
      (t) async {
        final f = Fixture016B();
        await app(t, WorldCatalogStatus016B(api: f.api, coachId: owner), width);
        expect(find.text('Espanha'), findsOneWidget);
        expect(find.text('Inglaterra'), findsOneWidget);
        await t.ensureVisible(find.text('ATUALIZAR PAÍS').first);
        await t.pumpAndSettle();
        await t.tap(find.text('ATUALIZAR PAÍS').first);
        await t.pumpAndSettle();
        final request = f.requests.lastWhere(
          (r) => r.url.path.endsWith('/refresh'),
        );
        expect(jsonDecode(request.body), {
          'countryCode': 'ES',
          'gender': 'Male',
          'season': '2026',
        });
        expect(
          request.headers['Authorization'],
          'Bearer localdev-${owner.replaceAll('-', '')}',
        );
        await t.ensureVisible(find.text('VER RELATÓRIO / PENDÊNCIAS').first);
        await t.pumpAndSettle();
        await t.tap(find.text('VER RELATÓRIO / PENDÊNCIAS').first);
        await t.pumpAndSettle();
        expect(
          find.text('Clubes: 0 • Jogadores atuais utilizáveis: 0'),
          findsOneWidget,
        );
        expect(t.takeException(), isNull);
        await finish(t);
      },
    );
  }
  testWidgets(
    'Career choice is explicit, excludes districts and preserves rejection message',
    (t) async {
      final f = Fixture016B();
      await app(
        t,
        WorldInitialLobby016B(
          api: f.api,
          worldId: 'world',
          coachId: owner,
          onStarted: () async {},
          onLeave: () async {},
        ),
        390,
      );
      await t.ensureVisible(find.byKey(const Key('initial-career-choice')));
      await t.pumpAndSettle();
      await t.tap(find.byKey(const Key('initial-career-choice')));
      await t.pumpAndSettle();
      await t.tap(find.text('ASSUMIR CLUBE').last);
      await t.pumpAndSettle();
      expect(find.text('Fixture distrital proibido'), findsNothing);
      final selector = find.byWidgetPredicate(
        (w) =>
            w is DropdownButtonFormField<String> &&
            w.decoration.labelText == 'Clube disponível das quatro divisões',
      );
      await t.ensureVisible(selector);
      await t.tap(selector);
      await t.pumpAndSettle();
      await t.tap(
        find.textContaining('Fixture clube da primeira divisão').last,
      );
      await t.pumpAndSettle();
      f.rejectChoice = true;
      await t.ensureVisible(find.byKey(const Key('save-initial-choice')));
      await t.pumpAndSettle();
      await t.tap(find.byKey(const Key('save-initial-choice')));
      await t.pumpAndSettle();
      await t.pump(const Duration(seconds: 3));
      await t.pumpAndSettle();
      expect(find.textContaining('Clube já pertence'), findsOneWidget);
      expect(t.takeException(), isNull);
      await finish(t);
    },
  );
  testWidgets(
    'Host draw then start use server state, even when a command is repeated',
    (t) async {
      final f = Fixture016B();
      f.view.addAll({
        'capacity': 2,
        'registered': 2,
        'connected': 2,
        'canDraw': true,
        'issues': [],
        'state': 'READY_FOR_DRAW',
      });
      int started = 0;
      await app(
        t,
        WorldInitialLobby016B(
          api: f.api,
          worldId: 'world',
          coachId: owner,
          onStarted: () async {
            started++;
          },
          onLeave: () async {},
        ),
        1366,
      );
      await t.ensureVisible(find.byKey(const Key('initial-draw')));
      await t.pumpAndSettle();
      await t.tap(find.byKey(const Key('initial-draw')));
      await t.pumpAndSettle();
      expect(f.view['state'], 'DRAW_COMPLETED');
      expect(
        t
            .widget<ElevatedButton>(find.byKey(const Key('initial-start')))
            .onPressed,
        isNotNull,
      );
      await t.tap(find.byKey(const Key('initial-start')));
      await t.pumpAndSettle();
      expect(started, 1);
      expect(f.requests.where((r) => r.url.path.endsWith('/draw')).length, 1);
      await finish(t);
    },
  );
  testWidgets(
    'New creator sends catalogue rules, host lifecycle and explicit fictional consent',
    (t) async {
      final f = Fixture016B();
      await app(
        t,
        WorldCreatorScreen(api: f.api, coachId: owner, worldCatalogRules: true),
        390,
      );
      await t.tap(find.text('CONTINUAR'));
      await t.pumpAndSettle();
      await t.tap(find.text('CONTINUAR'));
      await t.pumpAndSettle();
      expect(find.text('3/7 • DIVISÕES'), findsOneWidget);
      expect(find.byKey(const Key('fictional-world-016b')), findsNothing);
      await t.tap(find.text('CONTINUAR'));
      await t.pumpAndSettle();
      await t.tap(find.text('CONTINUAR'));
      await t.pumpAndSettle();
      expect(find.text('5/7 • ELENCOS / FONTES'), findsOneWidget);
      final consent = find.byKey(const Key('fictional-world-016b'));
      await t.ensureVisible(consent);
      await t.pumpAndSettle();
      await t.tap(consent);
      await t.pumpAndSettle();
      await t.tap(find.text('CONTINUAR'));
      await t.pumpAndSettle();
      final request =
          jsonDecode(
                f.requests
                    .lastWhere((r) => r.url.path.endsWith('/preview'))
                    .body,
              )
              as Map;
      expect(request['useWorldCatalogRules'], true);
      expect(request['requireHostStart'], true);
      expect(request['gameModeId'], 'CLASSIC_SYNC');
      expect(request['fictionalOnly'], true);
      expect(request['allowFictionalTeams'], true);
      expect(request['allowFictionalPlayers'], true);
      expect((request['countries'] as List).length, 8);
      expect(t.takeException(), isNull);
      await finish(t);
    },
  );
}
