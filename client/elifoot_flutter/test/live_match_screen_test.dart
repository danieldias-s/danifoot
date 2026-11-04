import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:elifoot_flutter/club_theme.dart';
import 'package:elifoot_flutter/live_match_data.dart';
import 'package:elifoot_flutter/live_match_view.dart';
import 'package:elifoot_flutter/classic_match_board.dart';
import 'classic_ux_014b_test.dart' show WaitingRoomApi, join;

Map<String, dynamic> game(int i) => {
  'matchId': 'live-$i',
  'competitionType': 'League',
  'divisionTier': i ~/ 4 + 1,
  'homeClubId': i == 0 ? 'club-1' : 'h$i',
  'awayClubId': i == 0 ? 'club-2' : 'a$i',
  'homeClubName': 'Mandante com nome longo $i',
  'awayClubName': 'Visitante com nome completo $i',
  'homeGoals': i,
  'awayGoals': 1,
  'minute': 31,
  'isFinished': false,
  'homeColorPalette': {
    'primaryColor': '#BB0000',
    'secondaryColor': '#FFFFFF',
    'preferredTextColor': '#FFFFFF',
    'colorVerified': true,
  },
  'awayColorPalette': {
    'primaryColor': '#000066',
    'secondaryColor': '#FFFFFF',
    'preferredTextColor': '#FFFFFF',
    'colorVerified': true,
  },
  'attendance': 12000 + i,
  'ticketRevenue': 360000 + i,
  'events': [
    {
      'minute': 30,
      'eventType': 'YellowCard',
      'clubId': i == 0 ? 'club-1' : 'h$i',
      'clubName': 'Mandante $i',
      'primaryPlayerId': 'p$i',
      'description': 'Amarelo oficial do jogo $i',
    },
    {
      'minute': 7,
      'eventType': 'Goal',
      'clubId': i == 0 ? 'club-2' : 'a$i',
      'description': 'Gol oficial do jogo $i',
    },
  ],
};
Map<String, dynamic> snapshot({
  String world = 'world-live',
  int round = 3,
  String stage = 'RUNNING_FIRST_HALF',
}) => {
  'gameWorldId': world,
  'seasonNumber': 1,
  'roundNumber': round,
  'roundVersion': 1,
  'competitionType': 'League',
  'competitionLabel': 'Campeonato Nacional',
  'roundState': stage,
  'matches': [for (int i = 0; i < 16; i++) game(i)],
};
Map<String, Map<String, dynamic>> clubs() => {
  for (int i = 0; i < 16; i++)
    for (final side in ['home', 'away'])
      '${game(i)['${side}ClubId']}': {
        'id': game(i)['${side}ClubId'],
        'name': game(i)['${side}ClubName'],
        'divisionTier': i ~/ 4 + 1,
        'stadiumName': 'Estádio oficial $side $i',
        'builtCapacity': 20000 + i,
        'colorPalette': game(i)['${side}ColorPalette'],
      },
};
final calendar = [
  {
    'roundNumber': 1,
    'competition': 'League',
    'fixtures': [
      {
        'id': 'past-1',
        'homeClubId': 'h2',
        'awayClubId': 'a2',
        'home': 'Mandante passado',
        'away': 'Visitante passado',
        'homeGoals': 2,
        'awayGoals': 0,
        'isFinished': true,
      },
    ],
  },
  {'roundNumber': 3, 'competition': 'League', 'fixtures': []},
  {
    'roundNumber': 4,
    'competition': 'CupFinal',
    'fixtures': [
      {
        'id': 'future-1',
        'homeClubId': 'club-1',
        'awayClubId': 'a2',
        'home': 'Clube futuro',
        'away': 'Rival futuro',
        'homeGoals': 0,
        'awayGoals': 0,
        'isFinished': false,
      },
    ],
  },
];
Future<void> render(
  WidgetTester tester,
  Map<String, dynamic> snap,
  LiveMatchSelection selection, {
  String? club = 'club-1',
  Widget? controls,
}) => tester.pumpWidget(
  MaterialApp(
    theme: ClassicTheme.derive().material(),
    home: Scaffold(
      body: LiveMatchScreen(
        snapshot: snap,
        calendar: calendar,
        clubs: clubs(),
        coachId: 'coach-a',
        coachName: 'Treinador A',
        coachClubId: club,
        selection: selection,
        onSelectionChanged: () {},
        coachControls: controls,
      ),
    ),
  ),
);

class LiveShellApi extends WaitingRoomApi {
  LiveShellApi() : super(2);
  int capacity = 20000;
  @override
  Future<Map<String, dynamic>> getSnapshot(String world) async {
    final snap = await super.getSnapshot(world);
    snap['matches'] = [
      if ((snap['matches'] as List).isNotEmpty)
        (snap['matches'] as List).first
      else
        game(0),
      game(1),
      game(4),
    ];
    return snap;
  }

  @override
  Future<List<dynamic>> getClubs(String world, {String? tier}) async => clubs()
      .values
      .map((club) => {...club, 'builtCapacity': capacity})
      .toList();
}

void main() {
  test(
    'Selection survives snapshots and reload, resets across coach room club season and round',
    () {
      final selected = LiveMatchSelection(),
          games = [for (int i = 0; i < 16; i++) game(i)];
      void accept({
        String world = 'a',
        String coach = 'x',
        String? club = 'h5',
        int season = 1,
        int round = 3,
      }) => selected.accept(
        world: world,
        coach: coach,
        club: club,
        season: season,
        round: round,
        matches: games,
      );
      accept();
      expect(selected.matchId, 'live-5');
      selected.select(game(1));
      accept();
      expect(selected.matchId, 'live-1');
      final restored = LiveMatchSelection()..restore(selected.toJson());
      restored.accept(
        world: 'a',
        coach: 'x',
        club: 'h5',
        season: 1,
        round: 3,
        matches: games,
      );
      expect(restored.matchId, 'live-1');
      for (final change in [
        () {
          accept(world: 'b');
        },
        () {
          accept(coach: 'y');
        },
        () {
          accept(round: 4);
        },
        () {
          accept(season: 2);
        },
        () {
          accept(club: 'h9');
        },
      ]) {
        selected.select(game(1));
        change();
        expect(selected.matchId, isNot('live-1'));
      }
      selected.accept(
        world: 'a',
        coach: 'x',
        club: null,
        season: 1,
        round: 1,
        matches: [],
      );
      expect(selected.matchId, isNull);
    },
  );
  test(
    'Official full snapshots are replaced and same-minute order is retained without inventing event identities',
    () {
      final m = game(0);
      m['events'] = [
        {'minute': 20, 'description': 'primeiro'},
        {'minute': 7, 'description': 'cedo'},
        {'minute': 20, 'description': 'segundo'},
      ];
      expect(officialMatchEvents(m).map((e) => e['description']), [
        'cedo',
        'primeiro',
        'segundo',
      ]);
      expect(officialMatchEvents(m), hasLength(3));
      expect(officialMatchEvents(m), hasLength(3));
      expect((m['events'] as List).first['description'], 'primeiro');
    },
  );
  for (final width in [320.0, 390.0, 768.0, 1024.0, 1440.0, 1920.0]) {
    testWidgets(
      'Own match, observation, official venue/events/stats and update selection at $width',
      (tester) async {
        tester.view.physicalSize = Size(width, 1100);
        tester.view.devicePixelRatio = 1;
        addTearDown(tester.view.resetPhysicalSize);
        addTearDown(tester.view.resetDevicePixelRatio);
        final selected = LiveMatchSelection();
        var snap = snapshot();
        await render(tester, snap, selected);
        await tester.pumpAndSettle();
        expect(selected.matchId, 'live-0');
        expect(
          tester.widget<Text>(find.byKey(const Key('selected-home-name'))).data,
          'Mandante com nome longo 0',
        );
        final target = find.byKey(const ValueKey('select-match-live-1'));
        await tester.ensureVisible(target);
        final before = tester
            .state<ScrollableState>(
              find
                  .descendant(
                    of: find.byKey(const Key('live-match-screen')),
                    matching: find.byType(Scrollable),
                  )
                  .first,
            )
            .position
            .pixels;
        await tester.tap(target);
        await tester.pumpAndSettle();
        expect(selected.matchId, 'live-1');
        expect(find.text('Gol oficial do jogo 1'), findsOneWidget);
        expect(find.text('Gol oficial do jogo 0'), findsNothing);
        if (width < 1000) {
          final after = tester
              .state<ScrollableState>(
                find
                    .descendant(
                      of: find.byKey(const Key('live-match-screen')),
                      matching: find.byType(Scrollable),
                    )
                    .first,
              )
              .position
              .pixels;
          expect(after, before);
        }
        final venue = tester.widget<SelectedMatchVenue>(
          find.byType(SelectedMatchVenue).first,
        );
        expect(venue.club?['stadiumName'], 'Estádio oficial home 1');
        expect(venue.match['attendance'], 12001);
        expect(venue.match['ticketRevenue'], 360001);
        snap = snapshot();
        (snap['matches'] as List)[1]['homeGoals'] = 8;
        await render(tester, snap, selected);
        await tester.pumpAndSettle();
        expect(selected.matchId, 'live-1');
        expect(find.text('Gol oficial do jogo 1'), findsOneWidget);
        if (width < 1000) {
          final stats = find.byKey(const ValueKey('live-detail-tab-1'));
          await tester.ensureVisible(stats);
          await tester.tap(stats);
          await tester.pumpAndSettle();
        }
        final panel = tester.widget<MatchStatisticsPanel>(
          find.byType(MatchStatisticsPanel),
        );
        expect(panel.match['matchId'], 'live-1');
        expect(
          find.textContaining('não são disponibilizados nesta consulta'),
          findsOneWidget,
        );
        final mine = find.byKey(const Key('my-live-match'));
        await tester.ensureVisible(mine);
        await tester.tap(mine);
        await tester.pumpAndSettle();
        expect(selected.matchId, 'live-0');
        expect(tester.takeException(), isNull);
        await tester.pumpWidget(const SizedBox());
      },
    );
  }
  testWidgets(
    'Round browser is read-only, calendar facts do not inherit live data; next official round resets selection',
    (tester) async {
      tester.view.physicalSize = const Size(1440, 1100);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      final selected = LiveMatchSelection();
      await render(tester, snapshot(), selected);
      await tester.pumpAndSettle();
      final previous = find.byKey(const Key('view-previous-round'));
      await tester.ensureVisible(previous);
      await tester.tap(previous);
      await tester.pumpAndSettle();
      expect(selected.matchId, 'past-1');
      expect(
        find.text('Lances não disponíveis na consulta do calendário.'),
        findsOneWidget,
      );
      expect(
        find.text('Estatísticas não disponíveis para este confronto.'),
        findsOneWidget,
      );
      final venue = tester.widget<SelectedMatchVenue>(
        find.byType(SelectedMatchVenue),
      );
      expect(venue.match.containsKey('attendance'), isFalse);
      expect(venue.match.containsKey('ticketRevenue'), isFalse);
      expect(
        tester
            .widget<OutlinedButton>(find.byKey(const Key('my-live-match')))
            .onPressed,
        isNull,
      );
      final current = find.byKey(const Key('view-current-round'));
      await tester.ensureVisible(current);
      await tester.tap(current);
      await tester.pumpAndSettle();
      expect(selected.matchId, 'live-0');
      await tester.tap(find.byKey(const ValueKey('select-match-live-1')));
      await tester.pumpAndSettle();
      await render(tester, snapshot(round: 4), selected);
      await tester.pumpAndSettle();
      expect(selected.matchId, 'live-0');
      expect(tester.takeException(), isNull);
    },
  );
  testWidgets(
    'No own game, no events, before kickoff, interval and final state',
    (tester) async {
      await tester.binding.setSurfaceSize(const Size(1440, 1000));
      addTearDown(() => tester.binding.setSurfaceSize(null));
      final selected = LiveMatchSelection();
      var snap = snapshot(stage: 'PREPARING');
      for (final m in snap['matches'] as List) {
        m['minute'] = 0;
        m['events'] = [];
      }
      await render(tester, snap, selected, club: 'idle');
      await tester.pumpAndSettle();
      expect(find.text('Aguardando início da partida.'), findsOneWidget);
      expect(find.text('Seu clube não disputa esta rodada.'), findsOneWidget);
      snap = snapshot(stage: 'HALFTIME_PAUSED');
      for (final m in snap['matches'] as List) {
        m['events'] = [];
      }
      await render(tester, snap, selected, club: 'idle');
      await tester.pumpAndSettle();
      expect(find.text('Intervalo'), findsWidgets);
      expect(
        find.text('Nenhum lance registrado até o momento.'),
        findsOneWidget,
      );
      snap = snapshot(stage: 'RESULTS');
      for (final m in snap['matches'] as List) {
        m['isFinished'] = true;
      }
      await render(tester, snap, selected, club: 'idle');
      await tester.pumpAndSettle();
      expect(find.text('Encerrada'), findsWidgets);
      expect(tester.takeException(), isNull);
    },
  );
  testWidgets(
    'Observing another game exposes no controls; own game restores own-club controls',
    (tester) async {
      await tester.binding.setSurfaceSize(const Size(1440, 1100));
      addTearDown(() => tester.binding.setSurfaceSize(null));
      final selected = LiveMatchSelection();
      await render(
        tester,
        snapshot(stage: 'GLOBAL_PAUSED'),
        selected,
        controls: const Text('CONTROLES EXCLUSIVOS DO CLUBE'),
      );
      await tester.pumpAndSettle();
      expect(find.text('CONTROLES EXCLUSIVOS DO CLUBE'), findsOneWidget);
      await tester.tap(find.byKey(const ValueKey('select-match-live-1')));
      await tester.pumpAndSettle();
      expect(find.text('CONTROLES EXCLUSIVOS DO CLUBE'), findsNothing);
      await tester.tap(find.byKey(const Key('my-live-match')));
      await tester.pumpAndSettle();
      expect(find.text('CONTROLES EXCLUSIVOS DO CLUBE'), findsOneWidget);
      expect(tester.takeException(), isNull);
    },
  );
  testWidgets(
    'Shell isolates room updates, preserves observation during ticks and handles route lock',
    (tester) async {
      await tester.binding.setSurfaceSize(const Size(1440, 1100));
      addTearDown(() => tester.binding.setSurfaceSize(null));
      final api = LiveShellApi();
      await join(tester, api);
      await tester.tap(find.byKey(const Key('open-live-matches')));
      await tester.pumpAndSettle();
      expect(find.byType(LiveMatchScreen), findsOneWidget);
      await api.emit('RUNNING_FIRST_HALF');
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const ValueKey('select-match-live-1')));
      await tester.pumpAndSettle();
      expect(
        tester.widget<Text>(find.byKey(const Key('selected-home-name'))).data,
        'Mandante com nome longo 1',
      );
      final foreign = snapshot(world: 'foreign');
      foreign['roundVersion'] = 999;
      api.onSnapshotUpdated!(foreign);
      await tester.pumpAndSettle();
      expect(
        tester.widget<Text>(find.byKey(const Key('selected-home-name'))).data,
        'Mandante com nome longo 1',
      );
      await api.emit('RUNNING_SECOND_HALF');
      await tester.pumpAndSettle();
      expect(
        tester.widget<Text>(find.byKey(const Key('selected-home-name'))).data,
        'Mandante com nome longo 1',
      );
      expect(find.byType(ClassicMatchBoard), findsOneWidget);
      api.capacity = 31000;
      await api.emit('RESULTS');
      await tester.pumpAndSettle();
      expect(find.byType(ClassicMatchBoard), findsNothing);
      await tester.tap(find.byKey(const Key('return-to-match')));
      await tester.pumpAndSettle();
      expect(
        tester
            .widget<SelectedMatchVenue>(find.byType(SelectedMatchVenue))
            .club?['builtCapacity'],
        31000,
        reason: 'Stadium metadata refreshes after round settlement.',
      );
      expect(tester.takeException(), isNull);
    },
  );
  testWidgets(
    'Division filter retains selection, updates and My Game restores all groups',
    (tester) async {
      await tester.binding.setSurfaceSize(const Size(1440, 1100));
      addTearDown(() => tester.binding.setSurfaceSize(null));
      final selected = LiveMatchSelection();
      await render(tester, snapshot(), selected);
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const ValueKey('live-filter-2ª DIVISÃO')));
      await tester.pumpAndSettle();
      expect(
        tester
            .widget<ClassicMatchBoard>(find.byType(ClassicMatchBoard))
            .matches,
        hasLength(4),
      );
      expect(selected.matchId, 'live-0');
      await tester.tap(find.byKey(const ValueKey('select-match-live-5')));
      await tester.pumpAndSettle();
      expect(selected.matchId, 'live-5');
      await render(tester, snapshot(), selected);
      await tester.pumpAndSettle();
      expect(selected.matchId, 'live-5');
      expect(
        tester
            .widget<ClassicMatchBoard>(find.byType(ClassicMatchBoard))
            .matches,
        hasLength(4),
      );
      await tester.tap(find.byKey(const Key('my-live-match')));
      await tester.pumpAndSettle();
      expect(selected.matchId, 'live-0');
      expect(
        tester
            .widget<ClassicMatchBoard>(find.byType(ClassicMatchBoard))
            .matches,
        hasLength(16),
      );
      expect(tester.takeException(), isNull);
    },
  );
  testWidgets(
    'Mobile expansion and selection preserve list scroll, with return to scoreboard and list',
    (tester) async {
      tester.view.physicalSize = const Size(390, 844);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      final selected = LiveMatchSelection();
      await render(tester, snapshot(), selected);
      await tester.pumpAndSettle();
      final group = find.byKey(
        PageStorageKey('live-group-${selected.scope}-2ª DIVISÃO'),
      );
      await tester.ensureVisible(group);
      await tester.tap(group);
      await tester.pumpAndSettle();
      final target = find.byKey(const ValueKey('select-match-live-5'));
      await tester.ensureVisible(target);
      final scroll = tester
          .state<ScrollableState>(
            find
                .descendant(
                  of: find.byKey(const Key('live-match-screen')),
                  matching: find.byType(Scrollable),
                )
                .first,
          )
          .position;
      final offset = scroll.pixels;
      await tester.tap(target);
      await tester.pumpAndSettle();
      expect(selected.matchId, 'live-5');
      expect(scroll.pixels, offset);
      final up = find.byKey(const Key('view-selected-scoreboard'));
      await tester.ensureVisible(up);
      final returnOffset = scroll.pixels;
      await tester.tap(up);
      await tester.pumpAndSettle();
      expect(scroll.pixels, 0);
      await tester.tap(find.byKey(const Key('return-to-live-list')));
      await tester.pumpAndSettle();
      expect(scroll.pixels, closeTo(returnOffset, 1));
      expect(selected.matchId, 'live-5');
      expect(tester.takeException(), isNull);
    },
  );
  test(
    'Additional division labels adapt to numeric divisions supplied by the server',
    () {
      expect(classicDivisionLabel(6), '6ª DIVISÃO');
      expect(classicDivisionLabel(12), '12ª DIVISÃO');
      expect(classicDivisionLabel(null), 'CAMPEONATO — DIVISÃO NÃO INFORMADA');
    },
  );
  testWidgets(
    'Gate placeholders stay pending until figures are reported, including legitimate settled zero',
    (tester) async {
      final match = game(0)
        ..['attendance'] = 0
        ..['ticketRevenue'] = 0
        ..['ticketPrice'] = 0;
      Future<void> venue() => tester.pumpWidget(
        MaterialApp(
          theme: ClassicTheme.derive().material(),
          home: Scaffold(
            body: SelectedMatchVenue(
              match: match,
              club: clubs()['club-1'],
              competition: 'Liga',
              round: 3,
            ),
          ),
        ),
      );
      await venue();
      await tester.pumpAndSettle();
      expect(find.text('Em apuração'), findsNWidgets(2));
      expect(find.text('R\$ 0'), findsNothing);
      match['ticketPrice'] = 20;
      match['isFinished'] = true;
      await venue();
      await tester.pumpAndSettle();
      expect(find.text('Em apuração'), findsNothing);
      expect(find.text('0'), findsOneWidget);
      expect(find.text('R\$ 0'), findsOneWidget);
      expect(tester.takeException(), isNull);
    },
  );
}
