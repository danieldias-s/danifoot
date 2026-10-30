import 'dart:async';
import 'dart:convert';

import 'package:elifoot_flutter/api_client.dart';
import 'package:elifoot_flutter/main.dart';
import 'package:elifoot_flutter/session_store.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:integration_test/integration_test.dart';

void main() {
  IntegrationTestWidgetsFlutterBinding.ensureInitialized();
  testWidgets(
    'Native Android UI and real server runtime',
    (tester) async {
      final api = ElifootApiClient();
      addTearDown(api.disconnectRealtime);
      Future<void> until(
        FutureOr<bool> Function() ready,
        String description, {
        Duration timeout = const Duration(minutes: 5),
      }) async {
        final deadline = DateTime.now().add(timeout);
        while (!await ready()) {
          if (DateTime.now().isAfter(deadline)) {
            fail('Timeout: $description');
          }
          await tester.pump(const Duration(milliseconds: 250));
          await Future<void>.delayed(const Duration(milliseconds: 250));
        }
      }

      Future<void> tap(Finder finder) async {
        await until(() => finder.evaluate().isNotEmpty, finder.toString());
        await tester.ensureVisible(finder.first);
        await tester.tap(finder.first);
        await tester.pump(const Duration(milliseconds: 500));
      }

      Future<void> shot(String name) async {
        await tester.pump(const Duration(milliseconds: 500));
        // Capture the actual device framebuffer with adb at these checkpoints.
        // ImageView conversion can stall the legacy software renderer.
        // ignore: avoid_print
        print('QA003_STAGE $name');
        expect(tester.takeException(), isNull);
      }

      await initializeSessionStore();
      final runId = DateTime.now().millisecondsSinceEpoch;
      // Real-server fixture only; all trainer interaction below uses app controls.
      final created = await api.createWorld(
        worldName: 'QA003 Android $runId',
        maxHumans: 2,
        totalClubs: 64,
        seed: 3003,
        tickIntervalMs: 5000,
        pauseTimeoutSeconds: 240,
      );
      final worldId =
          created['id']?.toString() ?? created['gameWorldId'].toString();
      expect(worldId, isNot('null'));
      await writeSession(
        jsonEncode({
          'nickname': 'QA003 Android',
          'deviceId': 'qa003-android-$runId',
        }),
      );
      await tester.pumpWidget(ElifootClassicApp(apiClient: api));
      await shot('01-native-menu');
      await tap(find.text('ENTRAR NO LOBBY (LOCAL DEV)'));
      await until(
        () => find.byKey(Key('join-world-$worldId')).evaluate().isNotEmpty,
        'REST login and room list',
      );
      await shot('02-native-lobby');
      await tap(find.byKey(Key('join-world-$worldId')));
      final clubs = await api.getClubs(worldId);
      final club = clubs.firstWhere(
        (c) =>
            c['divisionTier'] != 'Distrital' &&
            c['divisionTier'] != 5 &&
            c['isHumanControlled'] != true,
      );
      await tap(find.byKey(Key('choose-club-${club['id']}')));
      await until(
        () =>
            api.signalRConnected &&
            find.byKey(const Key('ready-counter-label')).evaluate().isNotEmpty,
        'SignalR JoinRoom completion and native squad',
      );
      await shot('03-native-squad');
      // ignore: avoid_print
      print(
        'QA003_ROOM ${jsonEncode({'worldId': worldId, 'name': 'QA003 Android $runId', 'clubId': club['id'], 'signalR': api.signalRConnected, 'frames': api.signalRMessagesReceived})}',
      );

      for (final tab in [
        '4. MERCADO & LEILÃO',
        '5. FINANÇAS & ESTÁDIO',
        '8. CALENDÁRIO & RESULTADOS',
        '3. DIVISÕES & TAÇA',
        '1. PLANTEL & TÁTICAS',
      ]) {
        await tap(find.text(tab));
        await shot('04-native-tab-${tab.substring(0, 1)}');
      }

      // A second trainer must join using the actual Web UI. No simulated peer.
      var snap = await api.getSnapshot(worldId);
      await until(
        () async {
          snap = await api.getSnapshot(worldId);
          return (snap['readyCounter']['totalHumans'] as num) == 2;
        },
        'Web trainer joins same room',
        timeout: const Duration(minutes: 15),
      );
      expect(snap['readyCounter']['readyCount'], 0);
      await shot('05-native-counter-0-of-2');
      await until(
        () async {
          snap = await api.getSnapshot(worldId);
          return snap['readyCounter']['readyCount'] == 1;
        },
        'Web trainer sends tactics',
        timeout: const Duration(minutes: 15),
      );
      await shot('06-native-counter-1-of-2');
      await tap(find.byKey(const Key('btn-enviar-tatica')));
      await until(
        () => find.byKey(const Key('btn-pedir-parada')).evaluate().isNotEmpty,
        'Automatic match start',
      );
      await shot('07-native-running');
      await tap(find.byKey(const Key('btn-pedir-parada')));
      await until(() async {
        snap = await api.getSnapshot(worldId);
        return snap['roundState'] == 'GLOBAL_PAUSED';
      }, 'Android global pause');
      final pausedMinute = snap['currentMinute'];
      await shot('08-native-global-pause');
      // ignore: avoid_print
      print(
        'QA003_PAUSED ${jsonEncode({'minute': pausedMinute, 'worldId': worldId})}',
      );
      await Future<void>.delayed(const Duration(seconds: 3));
      expect((await api.getSnapshot(worldId))['currentMinute'], pausedMinute);
      final match = (snap['matches'] as List).firstWhere(
        (m) => m['homeClubId'] == club['id'] || m['awayClubId'] == club['id'],
      );
      final side = match['homeClubId'] == club['id'] ? 'home' : 'away';
      final out = (match['${side}Starters'] as List).first;
      final incoming = (match['${side}Bench'] as List).first;
      await tap(find.text('Sai (Em campo)'));
      await tap(find.text('Sai: ${out['displayName']} (F:${out['strength']})'));
      await tap(find.text('Entra (Banco)'));
      await tap(
        find.text(
          'Entra: ${incoming['displayName']} (F:${incoming['strength']})',
        ),
      );
      await tap(find.text('APLICAR AJUSTE NA JANELA'));
      await until(() async {
        snap = await api.getSnapshot(worldId);
        final updated = (snap['matches'] as List).firstWhere(
          (m) => m['matchId'] == match['matchId'],
        );
        return updated['${side}SubCount'] == 1;
      }, 'Native substitution is persisted');
      await shot('09-native-substitution');
      // ignore: avoid_print
      print(
        'QA003_SUBSTITUTION ${jsonEncode({'matchId': match['matchId'], 'side': side, 'out': out['playerId'], 'incoming': incoming['playerId']})}',
      );
      // Web observes the paused snapshot and substitution before confirming.
      await until(
        () async {
          snap = await api.getSnapshot(worldId);
          return (snap['activePauseWindow']?['confirmedCount'] as num? ?? 0) >=
              1;
        },
        'Web trainer confirms pause',
        timeout: const Duration(minutes: 3),
      );
      await tap(find.byKey(const Key('btn-confirmar-continuar')));
      await until(() async {
        snap = await api.getSnapshot(worldId);
        return (snap['currentMinute'] as num) > (pausedMinute as num);
      }, 'Match resumes');
      await shot('10-native-resumed');
      final saved = readSession();
      api.disconnectRealtime();
      await tester.pumpWidget(const SizedBox.shrink());
      await initializeSessionStore();
      expect(readSession(), saved);
      await tester.pumpWidget(ElifootClassicApp(apiClient: api));
      await until(
        () =>
            api.signalRConnected &&
            find.byKey(const Key('ready-counter-label')).evaluate().isNotEmpty,
        'Native session and SignalR reconnect',
      );
      expect(
        ((await api.getSnapshot(worldId))['readyCounter']['participants']
                as List)
            .length,
        2,
      );
      await shot('11-native-reconnected');
      await until(
        () async {
          snap = await api.getSnapshot(worldId);
          if (snap['roundState'] == 'HALFTIME_PAUSED' &&
              find
                  .byKey(const Key('btn-confirmar-continuar'))
                  .evaluate()
                  .isNotEmpty) {
            await tap(find.byKey(const Key('btn-confirmar-continuar')));
          }
          return snap['roundState'] == 'RESULTS';
        },
        'Real match reaches final results',
        timeout: const Duration(minutes: 10),
      );
      await shot('12-native-results');
      expect(api.signalRMessagesReceived, greaterThan(0));
      // ignore: avoid_print
      print(
        'QA003_COMPLETE ${jsonEncode({'worldId': worldId, 'frames': api.signalRMessagesReceived, 'minute': snap['currentMinute'], 'state': snap['roundState']})}',
      );
    },
    timeout: const Timeout(Duration(minutes: 30)),
  );
}
