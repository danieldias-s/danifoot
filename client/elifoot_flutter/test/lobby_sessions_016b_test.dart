import 'dart:convert';
import 'package:elifoot_flutter/api_client.dart';
import 'package:elifoot_flutter/main.dart';
import 'package:elifoot_flutter/classic_layout.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'classic_ux_014b_test.dart' show WaitingRoomApi;
import 'package:elifoot_flutter/session_storage_policy.dart';
import 'package:elifoot_flutter/lobby_room_status.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

SessionStoragePolicy storage(
  Map<String, String> tab,
  Map<String, String> persistent,
) => SessionStoragePolicy(
  tabRead: (k) => tab[k],
  tabWrite: (k, v) {
    tab[k] = v;
  },
  persistentRead: (k) => persistent[k],
  persistentWrite: (k, v) {
    persistent[k] = v;
  },
);

class RestoredIdentityApi extends WaitingRoomApi {
  RestoredIdentityApi() : super(1);
  String? realtimeCoach;
  @override
  Future<Map<String, dynamic>> localLogin(
    String nickname,
    String deviceId,
  ) async => {
    'coachId': 'renamed-login',
    'nickname': nickname,
    'deviceId': deviceId,
  };
  @override
  Future<List<dynamic>> listWorlds() async => [
    for (final w in await super.listWorlds())
      {
        ...w as Map,
        'maxHumans': 1,
        'currentHumans': 1,
        'availableSeats': 0,
        'reconnectCoachId': 'coach-1',
      },
  ];
  @override
  void connectRealtimeRoom(String worldId, String coachId) {
    realtimeCoach = coachId;
  }
}

void main() {
  test(
    'Lobby requests use the authenticated device without exposing it in room data',
    () async {
      final api = ElifootApiClient(
        httpClient: MockClient((request) async {
          if (request.url.path.endsWith('local-login')) {
            return http.Response(
              jsonEncode({
                'coachId': '11111111-1111-1111-1111-111111111111',
                'deviceId': 'stable-device',
                'nickname': 'Edited',
              }),
              200,
            );
          }
          expect(request.url.queryParameters['deviceId'], 'stable-device');
          expect(
            request.url.queryParameters['coachId'],
            '11111111-1111-1111-1111-111111111111',
          );
          return http.Response('[]', 200);
        }),
      );
      await api.localLogin('Edited', 'stable-device');
      await api.listWorlds();
    },
  );
  testWidgets(
    'A full room restores the canonical trainer and realtime identity without choosing another club',
    (tester) async {
      final api = RestoredIdentityApi();
      await tester.pumpWidget(
        ElifootClassicApp(accountLogin: false, apiClient: api),
      );
      await tester.tap(find.text('ENTRAR NO LOBBY'));
      await tester.pumpAndSettle();
      await tester.ensureVisible(find.text('RETOMAR MINHA SESSÃO'));
      await tester.pumpAndSettle();
      await tester.tap(find.text('RETOMAR MINHA SESSÃO'));
      await tester.pumpAndSettle();
      expect(find.text('SELECIONE SEU CLUBE'), findsNothing);
      expect(find.byType(AppHeader), findsOneWidget);
      expect(api.realtimeCoach, 'coach-1');
      expect(tester.takeException(), isNull);
      await tester.pumpWidget(const SizedBox());
    },
  );
  test(
    'Closed browser/tab restores identity, while eight tabs remain independent',
    () async {
      final persistent = <String, String>{};
      final tabs = List.generate(8, (_) => <String, String>{});
      for (var i = 0; i < 8; i++) {
        await storage(tabs[i], persistent).write(
          jsonEncode({
            'nickname': 'Coach $i',
            'deviceId': 'device-$i',
            'coachId': 'coach-$i',
            'worldId': 'world',
          }),
        );
      }
      for (var i = 0; i < 8; i++) {
        expect(
          jsonDecode(storage(tabs[i], persistent).read()!)['coachId'],
          'coach-$i',
        );
      }
      final reopened = storage({}, persistent);
      expect(jsonDecode(reopened.read()!)['coachId'], 'coach-7');
      expect(reopened.profiles(), hasLength(8));
      final restored = reopened.profiles().firstWhere(
        (p) => p['coachId'] == 'coach-0',
      );
      await reopened.write(jsonEncode(restored));
      expect(
        jsonDecode(storage({}, persistent).read()!)['deviceId'],
        'device-0',
      );
      expect(reopened.profiles(), hasLength(8));
    },
  );
  test(
    'Legacy per-tab identity is migrated without changing its device or coach',
    () async {
      final legacy = jsonEncode({
        'nickname': 'Existing',
        'deviceId': 'legacy-device',
        'worldId': 'legacy-world',
      });
      final tab = {SessionStoragePolicy.sessionKey: legacy},
          persistent = <String, String>{};
      final s = storage(tab, persistent);
      expect(s.read(), legacy);
      await s.write(legacy);
      expect(storage({}, persistent).read(), legacy);
      await s.write(
        jsonEncode({
          'nickname': 'Edited',
          'deviceId': 'legacy-device',
          'coachId': 'original-coach',
        }),
      );
      expect(s.profiles(), hasLength(1));
      expect(s.profiles().single['coachId'], 'original-coach');
    },
  );
  test('Corrupted profile index does not prevent a valid per-tab return', () {
    final s = storage(
      {SessionStoragePolicy.sessionKey: 'existing'},
      {SessionStoragePolicy.profilesKey: 'broken'},
    );
    expect(s.read(), 'existing');
    expect(s.profiles(), isEmpty);
  });
  for (final width in [360.0, 1366.0]) {
    for (final capacity in [1, 2, 8]) {
      testWidgets(
        'Lobby separates connected, occupied, ready and state at $width / $capacity',
        (tester) async {
          await tester.binding.setSurfaceSize(Size(width, 1000));
          addTearDown(() => tester.binding.setSurfaceSize(null));
          await tester.pumpWidget(
            MaterialApp(
              home: Scaffold(
                body: SingleChildScrollView(
                  child: LobbyRoomStatus(
                    room: {
                      'id': 'w',
                      'maxHumans': capacity,
                      'currentHumans': capacity,
                      'connectedHumans': 0,
                      'roundState': 'PREPARING',
                      'readyCounter': {
                        'readyCount': 1,
                        'totalHumans': capacity,
                        'participants': <dynamic>[],
                      },
                    },
                  ),
                ),
              ),
            ),
          );
          expect(find.text('CONECTADOS: 0/$capacity'), findsOneWidget);
          expect(
            find.text('VAGAS OCUPADAS: $capacity/$capacity'),
            findsOneWidget,
          );
          expect(find.text('TÁTICAS ENVIADAS: 1/$capacity'), findsOneWidget);
          expect(find.text('ESTADO: PREPARING'), findsOneWidget);
          expect(find.textContaining('SALA LOTADA'), findsOneWidget);
          expect(
            find.textContaining('vaga e clube preservados'),
            findsOneWidget,
          );
          expect(tester.takeException(), isNull);
        },
      );
    }
  }
}
