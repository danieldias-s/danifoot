// Explicit local QA runner for Task 011 complete gameplay UI & responsive visual evidence.
// Replays real API/SQLite responses captured from the 22-round full season playtest.
import 'dart:convert';
import 'dart:io';
import 'dart:ui' as ui;

import 'package:elifoot_flutter/api_client.dart';
import 'package:elifoot_flutter/main.dart';
import 'package:elifoot_flutter/player_rankings.dart';
import 'package:flutter/material.dart';
import 'package:flutter/rendering.dart';
import 'package:flutter/services.dart';
import 'package:flutter_test/flutter_test.dart';

const _boundaryKey = ValueKey('qa-011-boundary');

class Replay011ApiClient extends ElifootApiClient {
  final Map<String, dynamic> snapshots;
  final Map<String, dynamic> mainInfo;

  String mode = 'prep';
  bool hasJoined = false;
  bool isUnemployed = false;

  Replay011ApiClient({
    required this.snapshots,
    required this.mainInfo,
  }) : super(baseUrl: 'http://127.0.0.1:5089');

  /// PowerShell 5.1 serializes arrays nested in hashtables as {value, Count}.
  List<dynamic> _list(String key) {
    final raw = snapshots[key];
    if (raw is List) return raw;
    if (raw is Map && raw['value'] is List) return raw['value'] as List;
    return const [];
  }

  @override
  void connectRealtimeRoom(String worldId, String coachId) {}

  @override
  void disconnectRealtime() {}

  @override
  Future<Map<String, dynamic>> localLogin(
    String nickname,
    String deviceId,
  ) async {
    return Map<String, dynamic>.from(snapshots['auth'] as Map);
  }

  @override
  Future<List<dynamic>> listWorlds() async {
    final wList = _list('worlds');
    final targetWid = mainInfo['worldId'].toString();
    final filtered = wList.where((w) => (w['id'] ?? w['gameWorldId']) == targetWid).toList();
    if (filtered.isNotEmpty) return filtered;
    return wList.take(3).toList();
  }

  @override
  Future<List<dynamic>> getClubs(String worldId, {String? tier}) async {
    return List<dynamic>.from(_list('clubs'));
  }

  @override
  Future<Map<String, dynamic>> joinWorld({
    required String worldId,
    required String coachId,
    required String nickname,
    required String deviceId,
    String? preferredClubId,
    bool joinUnemployed = false,
  }) async {
    hasJoined = true;
    isUnemployed = joinUnemployed;
    return {
      'worldId': worldId,
      'clubId': joinUnemployed ? null : mainInfo['myClubId'],
      'isUnemployed': joinUnemployed,
      'snapshot': await getSnapshot(worldId),
    };
  }

  @override
  Future<Map<String, dynamic>> getSnapshot(String worldId) async {
    if (isUnemployed) {
      return Map<String, dynamic>.from(snapshots['unemployedSnap'] as Map);
    }
    final raw = switch (mode) {
      'running' => snapshots['snapRunningR1'],
      'paused' => snapshots['snapPausedR1'],
      'afterSub' => snapshots['snapAfterSubR1'] ?? snapshots['snapPausedR1'],
      'results' => snapshots['snapResultsR1'],
      'season2' => snapshots['snapSeason2R1'],
      _ => snapshots['snapPrepR1'],
    };
    final map = Map<String, dynamic>.from(raw as Map);
    if (!hasJoined && mode == 'prep') {
      final rc = Map<String, dynamic>.from(map['readyCounter'] as Map);
      rc['participants'] = const [];
      map['readyCounter'] = rc;
    }
    return map;
  }

  @override
  Future<Map<String, dynamic>> getClubDetail(
    String worldId,
    String clubId,
  ) async {
    return Map<String, dynamic>.from(snapshots['clubDetailR1'] as Map);
  }

  @override
  Future<Map<String, dynamic>> getMarket(String worldId) async {
    return Map<String, dynamic>.from(snapshots['marketR1'] as Map);
  }

  @override
  Future<Map<String, dynamic>> getClubFinances(
    String worldId,
    String clubId,
  ) async {
    return Map<String, dynamic>.from(snapshots['financesR1'] as Map);
  }

  @override
  Future<List<dynamic>> getStandings(String worldId, String tier) async {
    return switch (tier) {
      'SegundaDivisao' => List<dynamic>.from(_list('standingsDiv2')),
      'TerceiraDivisao' => List<dynamic>.from(_list('standingsDiv3')),
      'QuartaDivisao' => List<dynamic>.from(_list('standingsDiv4')),
      _ => List<dynamic>.from(_list('standingsDiv1')),
    };
  }

  @override
  Future<List<dynamic>> getCupFixtures(String worldId) async {
    return List<dynamic>.from(_list('cupEndS1'));
  }

  @override
  Future<List<dynamic>> getCalendar(String worldId) async {
    return List<dynamic>.from(_list('calendarEndS1'));
  }

  @override
  Future<List<dynamic>> getTopScorers(String worldId) async {
    return List<dynamic>.from(_list('scorersEndS1'));
  }

  @override
  Future<Map<String, dynamic>> getRankingAndHistory(String worldId) async {
    return Map<String, dynamic>.from(snapshots['rankingAfterS1'] as Map);
  }

  @override
  Future<Map<String, dynamic>> getInvitations(
    String worldId,
    String coachId,
  ) async {
    if (isUnemployed) {
      return Map<String, dynamic>.from(snapshots['unemployedInvites'] as Map);
    }
    return Map<String, dynamic>.from(snapshots['invitationsAfterS1'] as Map);
  }

  @override
  Future<Map<String, dynamic>> getSeasonTopScorers(
    String worldId, {
    int? season,
  }) async {
    return Map<String, dynamic>.from(snapshots['awardsSeason1'] as Map);
  }

  @override
  Future<Map<String, dynamic>> getPlayerRankings(
    String worldId, {
    String category = 'scorers',
    String scope = 'general',
    String period = 'career',
    int? season,
  }) async {
    return Map<String, dynamic>.from(snapshots['playerRankingsGeneral'] as Map);
  }

  @override
  Future<Map<String, dynamic>> getPlayerHistory(
    String worldId,
    String playerId,
  ) async {
    return Map<String, dynamic>.from(snapshots['topPlayerHistory'] as Map);
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
    mode = 'running';
    final snap = await getSnapshot(worldId);
    return {
      'counter': snap['readyCounter'],
      'roundStarted': true,
    };
  }

  @override
  Future<Map<String, dynamic>> requestGlobalPause(
    String worldId,
    String coachId,
  ) async {
    mode = 'paused';
    return await getSnapshot(worldId);
  }

  @override
  Future<Map<String, dynamic>> submitInMatchAdjustments({
    required String worldId,
    required String coachId,
    required String matchId,
    String? newFormation,
    required List<Map<String, String>> substitutions,
  }) async {
    mode = 'afterSub';
    return await getSnapshot(worldId);
  }

  @override
  Future<Map<String, dynamic>> confirmContinue(
    String worldId,
    String coachId, {
    String? pauseWindowId,
  }) async {
    mode = 'results';
    return {
      'resumed': true,
      'snapshot': await getSnapshot(worldId),
    };
  }
}

void main() {
  final bundleFile = File(
    '../../docs/evidence/gameplay_011/live-playtest-bundle-011.json',
  );
  final outDir = Directory('../../docs/evidence/gameplay_011')
    ..createSync(recursive: true);

  Future<void> loadMonospaceFont(WidgetTester tester) async {
    await tester.runAsync(() async {
      final bytes = await File('C:/Windows/Fonts/consola.ttf').readAsBytes();
      final mono = FontLoader('monospace')
        ..addFont(Future.value(ByteData.sublistView(bytes)));
      await mono.load();
    });
  }

  Future<void> capturePng(WidgetTester tester, String filename) async {
    await tester.pumpAndSettle();
    expect(
      tester.takeException(),
      isNull,
      reason: 'Exception while capturing $filename',
    );
    final boundary = tester.renderObject<RenderRepaintBoundary>(
      find.byKey(_boundaryKey),
    );
    await tester.runAsync(() async {
      final image = await boundary.toImage(pixelRatio: 1.0);
      final bytes = await image.toByteData(format: ui.ImageByteFormat.png);
      File(
        '${outDir.path}/$filename.png',
      ).writeAsBytesSync(bytes!.buffer.asUint8List());
      image.dispose();
    });
  }

  for (final vp in [
    ('desktop', const Size(1280, 900)),
    ('mobile', const Size(390, 844)),
  ]) {
    final suffix = vp.$1;
    final size = vp.$2;

    testWidgets(
      '011 Visual Evidence Capture ($suffix ${size.width.toInt()}x${size.height.toInt()}) — All 18 Mandatory Gameplay Screens',
      (WidgetTester tester) async {
        tester.view.physicalSize = size;
        tester.view.devicePixelRatio = 1.0;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);
        await loadMonospaceFont(tester);

        final rawJson = bundleFile.readAsStringSync().replaceFirst('\uFEFF', '');
        final bundle = jsonDecode(rawJson) as Map<String, dynamic>;
        final snapshots = bundle['snapshots'] as Map<String, dynamic>;
        final mainInfo = bundle['mainPlaytest'] as Map<String, dynamic>;
        final myClubId = mainInfo['myClubId'].toString();

        final api = Replay011ApiClient(
          snapshots: snapshots,
          mainInfo: mainInfo,
        );

        await tester.pumpWidget(
          RepaintBoundary(
            key: _boundaryKey,
            child: ElifootClassicApp(apiClient: api),
          ),
        );
        await tester.pumpAndSettle();

        // 1. Login to Lobby -> capture 01_home_lobby
        await tester.tap(find.text('ENTRAR NO LOBBY (LOCAL DEV)'));
        await tester.pumpAndSettle();
        await capturePng(tester, '01_home_lobby_$suffix');

        // 2. Open Club Selection Dialog -> capture 02_club_selection
        await tester.ensureVisible(find.text('ENTRAR COM CLUBE').first);
        await tester.tap(find.text('ENTRAR COM CLUBE').first);
        await tester.pumpAndSettle();
        await capturePng(tester, '02_club_selection_$suffix');

        // Select club
        await tester.tap(find.byKey(Key('choose-club-$myClubId')));
        await tester.pumpAndSettle();

        // 3. Coach Main Panel -> capture 03_coach_panel
        await capturePng(tester, '03_coach_panel_$suffix');

        // 4. Squad & Lineup Table -> scroll to squad-table & capture 04_squad_lineup
        await tester.ensureVisible(find.byKey(const ValueKey('squad-table')));
        await tester.pumpAndSettle();
        await capturePng(tester, '04_squad_lineup_$suffix');

        // 5. Tactical Field Preview -> scroll to tactical-field-preview & capture 05_tactics_field
        await tester.ensureVisible(
          find.byKey(const ValueKey('tactical-field-preview')),
        );
        await tester.pumpAndSettle();
        await capturePng(tester, '05_tactics_field_$suffix');

        // 6. Transfer Market -> Tab 3 -> capture 06_transfer_market
        await tester.ensureVisible(find.text('4. MERCADO & LEILÃO'));
        await tester.tap(find.text('4. MERCADO & LEILÃO'));
        await tester.pumpAndSettle();
        await capturePng(tester, '06_transfer_market_$suffix');

        // 7. Auctions & Bilateral Offers -> scroll down on Tab 3 -> capture 07_auctions_offers
        await tester.ensureVisible(
          find.text('LEILÕES ATIVOS (ATLETAS EM FIM DE CONTRATO / LIVRES)'),
        );
        await tester.pumpAndSettle();
        await capturePng(tester, '07_auctions_offers_$suffix');

        // 8. Stadium & Finances -> Tab 4 -> capture 08_stadium_finances
        await tester.ensureVisible(find.text('5. FINANÇAS & ESTÁDIO'));
        await tester.tap(find.text('5. FINANÇAS & ESTÁDIO'));
        await tester.pumpAndSettle();
        await capturePng(tester, '08_stadium_finances_$suffix');

        // 9. Live Match -> Submit tactics to start match -> capture 09_live_match
        await tester.ensureVisible(find.byKey(const Key('btn-enviar-tatica')));
        await tester.tap(find.byKey(const Key('btn-enviar-tatica')));
        await tester.pumpAndSettle();
        await capturePng(tester, '09_live_match_$suffix');

        // 10. Global Pause Requested -> click PEDIR PARADA -> capture 10_global_pause
        await tester.ensureVisible(find.byKey(const Key('btn-pedir-parada')));
        await tester.tap(find.byKey(const Key('btn-pedir-parada')));
        await tester.pumpAndSettle();
        await capturePng(tester, '10_global_pause_$suffix');

        // 11. In-Match Substitutions -> scroll to substitution panel & apply -> capture 11_substitutions
        await tester.ensureVisible(
          find.byKey(const ValueKey('in-match-substitution-panel')),
        );
        await tester.pumpAndSettle();
        await tester.ensureVisible(
          find.byKey(const ValueKey('btn-aplicar-ajuste-janela')),
        );
        await tester.tap(
          find.byKey(const ValueKey('btn-aplicar-ajuste-janela')),
        );
        await tester.pumpAndSettle();
        await capturePng(tester, '11_substitutions_$suffix');

        // 12. Match Results -> click CONTINUAR -> capture 12_match_results
        await tester.ensureVisible(
          find.byKey(const Key('btn-confirmar-continuar')),
        );
        await tester.tap(find.byKey(const Key('btn-confirmar-continuar')));
        await tester.pumpAndSettle();
        await capturePng(tester, '12_match_results_$suffix');

        // 13. Standings & Round Summary -> Tab 2 -> capture 13_standings_calendar
        await tester.ensureVisible(find.text('3. DIVISÕES & TAÇA'));
        await tester.tap(find.text('3. DIVISÕES & TAÇA'));
        await tester.pumpAndSettle();
        await capturePng(tester, '13_standings_calendar_$suffix');

        // 14. National Cup (Final Stage without truncation) -> scroll to cup-fixtures-table -> capture 14_national_cup
        final stageChips = find.byWidgetPredicate(
          (w) =>
              w.key is ValueKey<String> &&
              (w.key as ValueKey<String>).value.startsWith('cup-stage-') &&
              (w.key as ValueKey<String>).value != 'cup-stage-ALL',
        );
        if (stageChips.evaluate().isNotEmpty) {
          await tester.ensureVisible(stageChips.last);
          await tester.tap(stageChips.last);
          await tester.pumpAndSettle();
        }
        final champBanner = find.byKey(const ValueKey('cup-champion-banner'));
        await tester.ensureVisible(
          champBanner.evaluate().isNotEmpty
              ? champBanner
              : find.byKey(const ValueKey('cup-fixtures-table')),
        );
        await tester.pumpAndSettle();
        await capturePng(tester, '14_national_cup_$suffix');

        // 15. Coach Ranking & Career -> Tab 5 -> capture 15_coach_ranking
        await tester.ensureVisible(find.text('6. RANKING & CARREIRA'));
        await tester.tap(find.text('6. RANKING & CARREIRA'));
        await tester.pumpAndSettle();
        await capturePng(tester, '15_coach_ranking_$suffix');

        // 17. Player Rankings -> Tab 8 -> capture 17_player_rankings
        await tester.ensureVisible(find.text('9. RANKINGS DE JOGADORES'));
        await tester.tap(find.text('9. RANKINGS DE JOGADORES'));
        await tester.pumpAndSettle();
        await capturePng(tester, '17_player_rankings_$suffix');

        // 18. Season End Awards Dialog -> real end-of-season dialog (season 1) -> capture 18_season_end_awards
        final panelContext = tester.element(
          find.byType(SeasonTopScorersPanel).first,
        );
        final worldId = mainInfo['worldId'].toString();
        showSeasonTopScorersDialog(
          context: panelContext,
          apiClient: api,
          worldId: worldId,
          season: 1,
        );
        await tester.pumpAndSettle();
        await capturePng(tester, '18_season_end_awards_$suffix');
        Navigator.of(
          tester.element(find.byKey(const ValueKey('season-end-awards-dialog'))),
        ).pop();
        await tester.pumpAndSettle();

        // 16. Unemployed / Invitations / Dismissal view -> switch room & join unemployed -> capture 16_invitations_dismissal
        await tester.ensureVisible(find.text('TROCAR SALA'));
        await tester.tap(find.text('TROCAR SALA'));
        await tester.pumpAndSettle();
        await tester.ensureVisible(find.text('ENTRAR DESEMPREGADO').first);
        await tester.tap(find.text('ENTRAR DESEMPREGADO').first);
        await tester.pumpAndSettle();
        await tester.ensureVisible(find.text('1. PLANTEL & TÁTICAS'));
        await tester.tap(find.text('1. PLANTEL & TÁTICAS'));
        await tester.pumpAndSettle();
        await capturePng(tester, '16_invitations_dismissal_$suffix');
      },
    );
  }
}
