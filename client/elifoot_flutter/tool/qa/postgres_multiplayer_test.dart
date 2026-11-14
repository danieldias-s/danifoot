import 'dart:async';
import 'dart:convert';
import 'dart:io';

import 'package:elifoot_flutter/api_client.dart';
import 'package:elifoot_flutter/main.dart';
import 'package:elifoot_flutter/presentation_version.dart';
import 'package:elifoot_flutter/presentation_widgets.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences/shared_preferences.dart';

void main() {
  TestWidgetsFlutterBinding.ensureInitialized();
  HttpOverrides.global = null;
  final url = Platform.environment['ELIFOOT_041_API'];
  test(
    'Two real Flutter transports agree after a durable PostgreSQL round',
    skip: url == null ? 'Requires a disposable PostgreSQL API' : false,
    timeout: const Timeout(Duration(minutes: 4)),
    () async {
      expect(Uri.parse(url!).host, '127.0.0.1');
      final a = ElifootApiClient(baseUrl: url)..enableAccountSession();
      final b = ElifootApiClient(baseUrl: url)..enableAccountSession();
      addTearDown(a.disconnectRealtime);
      addTearDown(b.disconnectRealtime);
      const password = 'Local-Stage041-QA!';
      final tag = DateTime.now().millisecondsSinceEpoch.toRadixString(36);
      final nameA = 'coach-a-$tag', nameB = 'coach-b-$tag';
      await a.signInAccount(nameA, password, displayName: 'QA Current');
      await b.signInAccount(nameB, password, displayName: 'QA Classic');
      await a.signOutAccount();
      await b.signOutAccount();
      final accountA = await a.signInAccount(nameA, password);
      final accountB = await b.signInAccount(nameB, password);
      final coachA = accountA['coachId'] as String;
      final coachB = accountB['coachId'] as String;
      expect(coachA, isNot(coachB));
      expect(await a.profileAppearance(coachA, version: 'CURRENT'), 'CURRENT');
      expect(
        await b.profileAppearance(coachB, version: 'CLASSIC_98'),
        'CLASSIC_98',
      );
      final code = 'QA-$tag'.toUpperCase();
      final created = await a.createWorld(
        worldName: 'PostgreSQL $tag',
        totalClubs: 64,
        maxHumans: 2,
        tickIntervalMs: 80,
        pauseTimeoutSeconds: 8,
        inviteCode: code,
      );
      final world = created['gameWorldId'] as String;
      expect((await a.listWorlds()).any((row) => row['id'] == world), isTrue);
      expect((await b.listWorlds()).any((row) => row['id'] == world), isFalse);
      final preview = await b.previewAccountWorldInvite(code);
      final clubs = (preview['clubs'] as List).take(2).toList();
      final clubA = clubs[0]['id'] as String, clubB = clubs[1]['id'] as String;
      final host = await a.joinWorld(
        worldId: world,
        coachId: coachA,
        nickname: 'QA Current',
        deviceId: 'fixture',
        preferredClubId: clubA,
      );
      final guest = await b.joinAccountWorldByCode(
        code,
        joinUnemployed: false,
        preferredClubId: clubB,
      );
      expect(host['clubId'], clubA);
      expect(guest['clubId'], clubB);

      Map<String, dynamic>? latestA, latestB;
      final statesA = <String>{}, statesB = <String>{};
      final timesA = <int>{}, timesB = <int>{};
      a.onSnapshotUpdated = (s) {
        latestA = s;
        statesA.add('${s['roundState']}');
        timesA.add(s['currentMinute'] as int);
      };
      b.onSnapshotUpdated = (s) {
        latestB = s;
        statesB.add('${s['roundState']}');
        timesB.add(s['currentMinute'] as int);
      };
      a.connectRealtimeRoom(world, coachA);
      b.connectRealtimeRoom(world, coachB);
      await until(() => a.signalRConnected && b.signalRConnected);
      final roster = await a.getClubDetail(world, clubA);
      final player = (roster['squad'] as List).lastWhere(
        (p) => p['position'] == 'A',
      );
      final event = Completer<Map<String, dynamic>>();
      b.onWorldDataChanged = (change) {
        if (!event.isCompleted && change['worldId'] == world)
          event.complete(change);
      };
      await a.listPlayerForTransfer(
        world,
        coachA,
        player['id'] as String,
        150000,
      );
      expect(
        (await event.future.timeout(const Duration(seconds: 12)))['areas'],
        contains('market'),
      );
      expect((await b.getMarket(world))['listings'], isNotEmpty);
      expect(
        await a.getRenewalContractPreview(
          world,
          player['id'] as String,
          coachA,
        ),
        isNotEmpty,
      );
      await a.getMarket(world);
      final auction = await a.createVoluntaryAuction(
        world,
        coachA,
        player['id'] as String,
        minimumBid: 1000,
      );
      final auctionId = auction['auctionId'] as String;
      await b.getMarket(world);
      await b.placeAuctionBid(world, auctionId, coachB, 1000);
      final auctionState = (await a.getMarket(world))['auctions'] as List;
      final updatedAuction = auctionState.singleWhere(
        (row) => row['auctionId'] == auctionId,
      );
      expect(updatedAuction['highestBidderClubId'], clubB);
      expect(updatedAuction['currentBid'], 1000);
      expect(await a.getInvitations(world, coachA), isNotEmpty);
      await a.submitTactics(
        worldId: world,
        coachId: coachA,
        formation: roster['preferredFormation'] as String,
        starterIds: List<String>.from(roster['preferredStarterIds'] as List),
        benchIds: List<String>.from(roster['preferredBenchIds'] as List),
        roundVersion: (await a.getSnapshot(world))['roundVersion'] as int,
      );
      expect((await a.getSnapshot(world))['roundState'], 'PREPARING');
      await b.submitUnemployedReady(
        worldId: world,
        coachId: coachB,
        roundVersion: (await b.getSnapshot(world))['roundVersion'] as int,
      );
      await until(() => timesA.any((m) => m >= 5) && timesB.any((m) => m >= 5));
      b.disconnectRealtime();
      final minute = latestA!['currentMinute'] as int;
      await until(() => (latestA!['currentMinute'] as int) > minute);
      b.connectRealtimeRoom(world, coachB);
      await until(
        () => b.signalRConnected && (latestB!['currentMinute'] as int) > minute,
      );

      String? lastPause;
      String? lastPenalty;
      final deadline = DateTime.now().add(const Duration(seconds: 90));
      while (latestA?['roundState'] != 'RESULTS' &&
          DateTime.now().isBefore(deadline)) {
        final penalty = latestA?['pendingPenalty'] as Map?;
        final decision = penalty?['decisionId'] as String?;
        if (decision != null && decision != lastPenalty) {
          lastPenalty = decision;
          final coach = penalty!['coachId'] as String;
          final taker =
              (penalty['eligiblePlayers'] as List).first['playerId'] as String;
          final owner = coach == coachA ? a : b;
          await Future.wait([
            owner.choosePenaltyTaker(world, coach, decision, taker),
            owner.choosePenaltyTaker(world, coach, decision, taker),
          ]);
        }
        final pause =
            latestA?['activePauseWindow']?['pauseWindowId'] as String?;
        if (pause != null && pause != lastPause) {
          lastPause = pause;
          await Future.wait([
            a.confirmContinue(world, coachA, pauseWindowId: pause),
            b.confirmContinue(world, coachB, pauseWindowId: pause),
          ]);
        }
        await Future<void>.delayed(const Duration(milliseconds: 100));
      }
      expect(latestA?['roundState'], 'RESULTS');
      await until(() => latestB?['roundState'] == 'RESULTS');
      final finalA = latestA!, finalB = latestB!;
      expect(finalA['roundVersion'], finalA['persistedRoundVersion']);
      expect(finalA['roundVersion'], finalB['roundVersion']);
      expect(finalA['matches'], finalB['matches']);
      expect(
        await a.getStandings(world, 'PrimeiraDivisao'),
        await b.getStandings(world, 'PrimeiraDivisao'),
      );
      Future<void> settleSalary(
        ElifootApiClient client,
        String club,
        String coach,
      ) async {
        for (var i = 0; i < 30; i++) {
          final finances = await client.getClubFinances(world, club);
          final pending = (finances['salaryRequests'] as List)
              .where((r) => r['status'] == 'PENDING')
              .firstOrNull;
          if (pending == null) return;
          await client.respondSalaryRequest(
            world,
            club,
            coach,
            pending['id'] as String,
            false,
          );
        }
        fail('Salary queue did not finish.');
      }

      await Future.wait([
        settleSalary(a, clubA, coachA),
        settleSalary(b, clubB, coachB),
      ]);
      final round = finalA['roundNumber'] as int,
          season = finalA['seasonNumber'] as int;
      final advances = await Future.wait([
        for (final c in [a, b, a])
          c.advanceToNextRound(
            world,
            expectedSeasonNumber: season,
            expectedRoundNumber: round,
          ),
      ]);
      expect(advances.map((s) => s['roundNumber']), everyElement(round + 1));
      expect(advances.map((s) => s['roundVersion']).toSet(), hasLength(1));
      print(
        jsonEncode({
          'statesA': statesA.toList(),
          'statesB': statesB.toList(),
          'minutesA': timesA.length,
          'minutesB': timesB.length,
          'messagesA': a.signalRMessagesReceived,
          'messagesB': b.signalRMessagesReceived,
          'roundVersion': advances.first['roundVersion'],
        }),
      );

      // The widget pass uses the same account and persisted appearance via live HTTP.
      sessions = [
        (nameA, world, 'PostgreSQL $tag', PresentationVersion.current),
        (nameB, world, 'PostgreSQL $tag', PresentationVersion.classic),
      ];
    },
  );

  for (var i = 0; i < 2; i++) {
    testWidgets('Real API renders the saved room in theme $i', (tester) async {
      expect(
        sessions,
        hasLength(2),
        reason: 'The transport scenario must pass first.',
      );
      final session = sessions[i];
      SharedPreferences.setMockInitialValues({});
      await tester.binding.setSurfaceSize(const Size(1440, 900));
      addTearDown(() => tester.binding.setSurfaceSize(null));
      final api = ElifootApiClient(baseUrl: url);
      addTearDown(api.disconnectRealtime);
      await tester.pumpWidget(ElifootClassicApp(apiClient: api));
      await pumpUntil(tester, find.byKey(const Key('account-submit')));
      await tester.enterText(
        find.byKey(const Key('account-username')),
        session.$1,
      );
      await tester.enterText(
        find.byKey(const Key('account-password')),
        'Local-Stage041-QA!',
      );
      await tester.runAsync(() async {
        await tester.tap(find.byKey(const Key('account-submit')));
        await Future<void>.delayed(const Duration(milliseconds: 600));
      });
      await pumpUntil(tester, find.text(session.$3));
      final scope = tester.widget<PresentationScope>(
        find.byType(PresentationScope),
      );
      expect(scope.notifier!.version, session.$4);
      expect(tester.takeException(), isNull);
      await tester.pumpWidget(const SizedBox());
    }, skip: url == null);
  }
}

var sessions = <(String, String, String, PresentationVersion)>[];

Future<void> until(bool Function() check) async {
  final deadline = DateTime.now().add(const Duration(seconds: 18));
  while (!check() && DateTime.now().isBefore(deadline)) {
    await Future<void>.delayed(const Duration(milliseconds: 50));
  }
  expect(check(), isTrue);
}

Future<void> pumpUntil(WidgetTester tester, Finder finder) async {
  for (var i = 0; i < 60 && finder.evaluate().isEmpty; i++) {
    await tester.runAsync(
      () => Future<void>.delayed(const Duration(milliseconds: 100)),
    );
    await tester.pump(const Duration(milliseconds: 100));
  }
  expect(finder, findsWidgets);
}
