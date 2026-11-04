import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:elifoot_flutter/classic_layout.dart';
import 'package:elifoot_flutter/classic_match_board.dart';
import 'package:elifoot_flutter/live_match_navigation.dart';
import 'package:elifoot_flutter/main.dart';
import 'package:elifoot_flutter/match_router.dart';
import 'package:elifoot_flutter/mobile_classic.dart';
import 'package:elifoot_flutter/presentation_version.dart';
import 'package:elifoot_flutter/presentation_widgets.dart';
import 'package:elifoot_flutter/round_navigation.dart';
import 'package:elifoot_flutter/visual_components.dart';
import 'classic_ux_014b_test.dart' show WaitingRoomApi, join, snap;

class AutomaticRoundApi extends WaitingRoomApi {
  String competition = 'League';
  bool reserveLeftClub = false;
  List<String> sentBench = [];
  final advances = <(int?, int?)>[];
  @override
  Future<Map<String, dynamic>> advanceToNextRound(
    String worldId, {
    int? expectedSeasonNumber,
    int? expectedRoundNumber,
  }) {
    advances.add((expectedSeasonNumber, expectedRoundNumber));
    return super.advanceToNextRound(
      worldId,
      expectedSeasonNumber: expectedSeasonNumber,
      expectedRoundNumber: expectedRoundNumber,
    );
  }

  final requestedTiers = <String>[];
  AutomaticRoundApi(super.humans);
  @override
  Future<Map<String, dynamic>> getSnapshot(String worldId) async {
    final value = await super.getSnapshot(worldId);
    value['competitionType'] = competition;
    for (final match in value['matches'] as List) {
      match['competitionType'] = competition;
      match['divisionTier'] = 'QuartaDivisao';
    }
    return value;
  }

  @override
  Future<Map<String, dynamic>> getClubDetail(
    String worldId,
    String clubId,
  ) async {
    final detail = {
      ...await super.getClubDetail(worldId, clubId),
      'divisionTier': 'QuartaDivisao',
      'divisionDisplay': '4ª Divisão',
    };
    if (reserveLeftClub) {
      (detail['squad'] as List).removeWhere((p) => p['id'] == 'p-12');
    }
    return detail;
  }

  @override
  Future<List<dynamic>> getStandings(String worldId, String tier) {
    requestedTiers.add(tier);
    return super.getStandings(worldId, tier);
  }

  @override
  Future<Map<String, dynamic>> submitTactics({
    required String worldId,
    required String coachId,
    required String formation,
    required List<String> starterIds,
    required List<String> benchIds,
    required int roundVersion,
  }) async {
    sentBench = List.of(benchIds);
    final result = await super.submitTactics(
      worldId: worldId,
      coachId: coachId,
      formation: formation,
      starterIds: starterIds,
      benchIds: benchIds,
      roundVersion: roundVersion,
    );
    if (humans == 1) {
      currentRoundState = 'RUNNING_FIRST_HALF';
      this.roundVersion++;
    }
    return result;
  }
}

void main() {
  test(
    'League and every cup stage restore the correct completed destination once',
    () {
      for (final type in [
        'League',
        0,
        'CupPreliminary1',
        'CupPreliminary2',
        'CupRoundOf1024',
        'CupRoundOf512',
        'CupRoundOf256',
        'CupRoundOf128',
        'CupRoundOf64',
        'CupRoundOf32',
        'CupRoundOf16',
        'CupQuarterFinal',
        'CupSemiFinal',
        'CupFinal',
        8,
      ]) {
        for (final state in ['RESULTS', 'FINISHED']) {
          final coordinator = RoundViewCoordinator();
          final data = {...snap(state), 'competitionType': type};
          expect(
            coordinator.accept(data),
            type == 'League' || type == 0
                ? RoundViewDecision.standings
                : RoundViewDecision.coach,
          );
          expect(coordinator.accept(data), RoundViewDecision.none);
        }
      }
    },
  );
  for (final classic in [false, true]) {
    for (final humans in [1, 2]) {
      for (final width in [390.0, 1366.0]) {
        testWidgets(
          'Automatic flow classic=$classic humans=$humans width=$width',
          (t) async {
            t.view.physicalSize = Size(width, 1000);
            t.view.devicePixelRatio = 1;
            addTearDown(t.view.resetPhysicalSize);
            addTearDown(t.view.resetDevicePixelRatio);
            final api = AutomaticRoundApi(humans);
            await join(t, api);
            final controller = PresentationScope.maybeOf(
              t.element(find.byType(ElifootShellScreen)),
            )!;
            if (classic) {
              await controller.select(api, PresentationVersion.classic);
              await t.pumpAndSettle();
            }
            final router =
                t.widget<MaterialApp>(find.byType(MaterialApp)).routerDelegate!
                    as MatchRouterDelegate;
            Future<void> sendAndStart() async {
              final send = find.byKey(const Key('btn-enviar-tatica'));
              await t.ensureVisible(send);
              await t.tap(send);
              await t.pumpAndSettle();
              if (humans > 1) {
                expect(
                  find.byKey(const Key('ready-waiting-panel')),
                  findsOneWidget,
                );
                expect(find.byType(ClassicMatchBoard), findsNothing);
                api.ready = humans;
                api.onReadyCounterUpdated?.call(
                  (await api.getSnapshot('world-011'))['readyCounter'],
                );
                await t.pumpAndSettle();
                expect(
                  find.byType(ClassicMatchBoard),
                  findsNothing,
                  reason: 'A counter alone cannot start the game',
                );
                await api.emit('RUNNING_FIRST_HALF');
                await t.pumpAndSettle();
              }
              expect(find.byType(ClassicMatchBoard), findsOneWidget);
              expect(router.currentConfiguration, '/partida');
            }

            void noMenus() {
              expect(find.byType(AppSidebar), findsNothing);
              expect(find.byType(LiveMatchNavigation), findsNothing);
              expect(find.byType(MobileClassicNavigation), findsNothing);
              expect(find.byType(ClassicBottomNavigation), findsNothing);
              expect(find.byTooltip('Abrir menus'), findsNothing);
              expect(find.byKey(const Key('open-navigation')), findsNothing);
              expect(find.text('Configurações'), findsNothing);
              expect(find.text('TROCAR SALA'), findsNothing);
              expect(find.text('SAIR DA CONTA'), findsNothing);
            }

            await sendAndStart();
            noMenus();
            await router.setNewRoutePath('/mercado');
            await t.pumpAndSettle();
            expect(router.currentConfiguration, '/partida');
            expect(await router.popRoute(), isTrue);
            await t.pumpAndSettle();
            expect(find.byType(ClassicMatchBoard), findsOneWidget);
            await api.emit('GLOBAL_PAUSED');
            await t.pumpAndSettle();
            noMenus();
            expect(
              find.byKey(const ValueKey('in-match-substitution-panel')),
              findsOneWidget,
            );
            api.reserveLeftClub = true;
            await api.emit('RUNNING_SECOND_HALF');
            await t.pumpAndSettle();
            await api.emit('RESULTS');
            await t.pumpAndSettle();
            expect(router.currentConfiguration, '/divisoes');
            expect(find.byType(ClassicMatchBoard), findsNothing);
            expect(find.text('CLASSIFICAÇÃO'), findsWidgets);
            expect(api.requestedTiers.last, 'QuartaDivisao');
            expect(find.byKey(const Key('btn-proxima-rodada')), findsNothing);
            expect(api.advances, isEmpty);
            await t.pump(const Duration(seconds: 4));
            await api.emit('RESULTS');
            await t.pumpAndSettle();
            expect(router.currentConfiguration, '/divisoes');
            expect(api.advances, isEmpty);
            await t.pump(const Duration(seconds: 1));
            await t.pumpAndSettle();
            expect(router.currentConfiguration, '/plantel');
            expect(api.currentRound, 2);
            expect(api.advances, [(1, 1)]);
            expect(find.byKey(const Key('btn-enviar-tatica')), findsOneWidget);
            api.competition = 'CupRoundOf32';
            api.ready = 0;
            api.isReady = false;
            await api.emit('PREPARING');
            await t.pumpAndSettle();
            await sendAndStart();
            expect(
              api.sentBench,
              isNot(contains('p-12')),
              reason: 'A reserve who left the club must not remain selected',
            );
            noMenus();
            await api.emit('RESULTS');
            await t.pumpAndSettle();
            expect(router.currentConfiguration, '/plantel');
            expect(find.byType(ClassicMatchBoard), findsNothing);
            expect(api.currentRound, 3);
            expect(api.advances, [(1, 1), (1, 2)]);
            expect(find.byKey(const Key('btn-proxima-rodada')), findsNothing);
            expect(find.byKey(const Key('btn-enviar-tatica')), findsOneWidget);
            if (classic) {
              expect(find.byKey(const Key('retro-roster')), findsOneWidget);
            } else if (width < 768) {
              expect(find.byType(MobileSquadScreen), findsOneWidget);
            } else {
              expect(find.text('PLANTEL & TÁTICAS'), findsWidgets);
            }
            expect(t.takeException(), isNull);
            await t.pumpWidget(const SizedBox());
            api.disconnectRealtime();
          },
        );
      }
    }
  }
}
