import 'dart:async';
import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:elifoot_flutter/api_client.dart';
import 'package:elifoot_flutter/appearance_settings.dart';
import 'package:elifoot_flutter/club_theme.dart';
import 'package:elifoot_flutter/club_selection_dialog.dart';
import 'package:elifoot_flutter/presentation_version.dart';
import 'package:elifoot_flutter/presentation_widgets.dart';
import 'package:elifoot_flutter/world_initial_lobby_016b.dart';
import 'package:elifoot_flutter/main.dart';
import 'package:elifoot_flutter/squad_classic.dart';
import 'classic_ux_014b_test.dart' show WaitingRoomApi, join;
import 'world_catalog_lobby_016b_test.dart' show Fixture016B, owner;

class ProfileApi extends ElifootApiClient {
  final versions = <String, String>{};
  final writes = <(String, String)>[];
  bool fail = false;
  final Fixture016B? lobby;
  ProfileApi([this.lobby]);
  @override
  Future<String> profileAppearance(String owner, {String? version}) async {
    if (fail) throw Exception('offline');
    if (version != null) {
      versions[owner] = version;
      writes.add((owner, version));
    }
    return versions[owner] ?? 'CURRENT';
  }

  @override
  Future<dynamic> world016b(
    String owner,
    String area,
    String path, {
    Map<String, dynamic>? body,
    String? adminToken,
  }) => lobby!.api.world016b(
    owner,
    area,
    path,
    body: body,
    adminToken: adminToken,
  );
  @override
  Future<List<dynamic>> getClubs(String worldId, {String? tier}) =>
      lobby!.api.getClubs(worldId, tier: tier);
}

class PendingApi extends ProfileApi {
  final pending = <String, Completer<String>>{};
  @override
  Future<String> profileAppearance(String owner, {String? version}) =>
      pending.putIfAbsent(owner, () => Completer<String>()).future;
}

class GameplayPresentationApi extends WaitingRoomApi {
  int connects = 0, disconnects = 0;
  GameplayPresentationApi() : super(2);
  @override
  void connectRealtimeRoom(String worldId, String coachId) {
    connects++;
  }

  @override
  void disconnectRealtime() {
    disconnects++;
  }
}

class InitialRoomApi extends GameplayPresentationApi {
  final fixture = Fixture016B();
  String? requestedClub;
  InitialRoomApi() {
    fixture.view['hostCoachId'] = 'coach-1';
    (fixture.view['participants'] as List).first['coachId'] = 'coach-1';
  }
  @override
  Future<List<dynamic>> listWorlds() async => [
    for (final world in await super.listWorlds())
      {...world as Map, 'hasInitialLobby': true, 'lobbyStarted': false},
  ];
  @override
  Future<Map<String, dynamic>> getSnapshot(String worldId) async => {
    ...await super.getSnapshot(worldId),
    'hasInitialLobby': true,
    'lobbyStarted': false,
  };
  @override
  Future<Map<String, dynamic>> joinWorld({
    required String worldId,
    required String coachId,
    required String nickname,
    required String deviceId,
    String? preferredClubId,
    bool joinUnemployed = false,
  }) async {
    requestedClub = preferredClubId;
    return {
      ...await super.joinWorld(
        worldId: worldId,
        coachId: coachId,
        nickname: nickname,
        deviceId: deviceId,
        preferredClubId: preferredClubId,
        joinUnemployed: joinUnemployed,
      ),
      'clubId': null,
    };
  }

  @override
  Future<dynamic> world016b(
    String owner,
    String area,
    String path, {
    Map<String, dynamic>? body,
    String? adminToken,
  }) => fixture.api.world016b(
    owner,
    area,
    path,
    body: body,
    adminToken: adminToken,
  );
}

class CounterGame extends StatefulWidget {
  const CounterGame({super.key});
  @override
  State<CounterGame> createState() => CounterGameState();
}

class CounterGameState extends State<CounterGame> {
  int count = 0;
  @override
  Widget build(BuildContext context) => Scaffold(
    body: TextButton(
      onPressed: () => setState(() => count++),
      child: Text('Estado preservado $count'),
    ),
  );
}

Future<void> harness(
  WidgetTester t,
  PresentationController controller,
  Widget body, {
  double width = 1366,
}) async {
  t.view.physicalSize = Size(width, 1000);
  t.view.devicePixelRatio = 1;
  addTearDown(t.view.resetPhysicalSize);
  addTearDown(t.view.resetDevicePixelRatio);
  await t.pumpWidget(
    PresentationScope(
      controller: controller,
      child: MaterialApp(
        theme: ClassicTheme.classicGreen().material(),
        builder: (_, child) => PresentationHost(child: child!),
        home: body,
      ),
    ),
  );
  await t.pumpAndSettle();
}

Future<void> close(WidgetTester t) async {
  await t.pumpWidget(const SizedBox());
  await t.pumpAndSettle();
}

void main() {
  testWidgets(
    'Entrar em sala com lobby inicial apresenta aparência e opções antes de atribuir clube',
    (t) async {
      t.view.physicalSize = const Size(1366, 1000);
      t.view.devicePixelRatio = 1;
      addTearDown(t.view.resetPhysicalSize);
      addTearDown(t.view.resetDevicePixelRatio);
      final api = InitialRoomApi();
      await t.pumpWidget(ElifootClassicApp(apiClient: api));
      await t.tap(find.byKey(const Key('enter-local-lobby')));
      await t.pumpAndSettle();
      await t.ensureVisible(find.text('ENTRAR NA SALA'));
      await t.tap(find.text('ENTRAR NA SALA'));
      await t.pumpAndSettle();
      expect(find.byType(ClubSelectionDialog), findsNothing);
      expect(find.byType(WorldInitialLobby016B), findsOneWidget);
      expect(api.requestedClub, isNull);
      expect(find.text('ESCOLHA A APARÊNCIA:'), findsOneWidget);
      expect(
        t.getTopLeft(find.byType(PresentationVersionSelector)).dy,
        lessThan(
          t.getTopLeft(find.byKey(const Key('initial-career-choice'))).dy,
        ),
      );
      await close(t);
    },
  );
  testWidgets(
    'Troca no jogo preserva escalação, formação, clube e conexão e respeita partida travada',
    (t) async {
      t.view.physicalSize = const Size(1366, 1000);
      t.view.devicePixelRatio = 1;
      addTearDown(t.view.resetPhysicalSize);
      addTearDown(t.view.resetDevicePixelRatio);
      final api = GameplayPresentationApi();
      await join(t, api);
      final shell = t.state(find.byType(ElifootShellScreen));
      var tactics = t.widget<ClassicSquadTactics>(
        find.byType(ClassicSquadTactics),
      );
      tactics.onFormation('4-3-3');
      t
          .widget<ClassicSquadRoster>(find.byType(ClassicSquadRoster))
          .onToggle(
            tactics.squad.firstWhere((p) => tactics.starters.contains(p['id'])),
          );
      await t.pumpAndSettle();
      tactics = t.widget<ClassicSquadTactics>(find.byType(ClassicSquadTactics));
      final starters = List<String>.of(tactics.starters),
          bench = List<String>.of(tactics.bench);
      final connects = api.connects,
          disconnects = api.disconnects,
          round = api.currentRound;
      await t.tap(find.text('APARÊNCIA'));
      await t.pumpAndSettle();
      await t.ensureVisible(
        find.byKey(const ValueKey('presentation-CLASSIC_98')),
      );
      await t.tap(find.byKey(const ValueKey('presentation-CLASSIC_98')));
      await t.pumpAndSettle();
      expect(t.state(find.byType(ElifootShellScreen)), same(shell));
      await t.ensureVisible(find.byType(BackButton));
      await t.pumpAndSettle();
      await t.tap(find.byType(BackButton));
      await t.pumpAndSettle();
      final after = t.widget<ClassicSquadTactics>(
        find.byType(ClassicSquadTactics),
      );
      expect(after.formation, '4-3-3');
      expect(after.starters, starters);
      expect(after.bench, bench);
      expect(api.currentRound, round);
      expect(api.connects, connects);
      expect(api.disconnects, disconnects);
      expect(find.textContaining('Atlético Verde'), findsWidgets);
      await t.tap(find.text('APARÊNCIA'));
      await t.pumpAndSettle();
      await api.emit('RUNNING_FIRST_HALF');
      await t.pumpAndSettle();
      expect(find.byType(PresentationVersionSelector), findsNothing);
      final controller = PresentationScope.maybeOf(
        t.element(find.byType(ElifootShellScreen)),
      )!;
      expect(
        await controller.select(api, PresentationVersion.current, locked: true),
        isFalse,
      );
      expect(controller.version, PresentationVersion.classic);
      expect(api.connects, connects);
      expect(api.disconnects, disconnects);
      await close(t);
    },
  );
  test(
    'Duas preferências independentes restauram por treinador e não por clube',
    () async {
      final api = ProfileApi(),
          a = PresentationController(),
          b = PresentationController();
      await a.activate(api, 'one');
      await b.activate(api, 'two');
      expect(await a.select(api, PresentationVersion.classic), isTrue);
      expect(a.version, PresentationVersion.classic);
      expect(b.version, PresentationVersion.current);
      final reconnect = PresentationController();
      await reconnect.activate(api, 'one');
      expect(reconnect.version, PresentationVersion.classic);
      await reconnect.activate(api, 'two');
      expect(reconnect.version, PresentationVersion.current);
      await b.select(api, PresentationVersion.classic, locked: true);
      expect(api.writes, [('one', 'CLASSIC_98')]);
      a.dispose();
      b.dispose();
      reconnect.dispose();
    },
  );
  test(
    'Resposta atrasada de outro treinador não substitui a preferência atual',
    () async {
      final api = PendingApi(), controller = PresentationController();
      final old = controller.activate(api, 'old');
      final current = controller.activate(api, 'new');
      api.pending['new']!.complete('CURRENT');
      await current;
      api.pending['old']!.complete('CLASSIC_98');
      await old;
      expect(controller.owner, 'new');
      expect(controller.version, PresentationVersion.current);
      controller.dispose();
    },
  );
  test(
    'Falha de gravação mantém a seleção anterior e informa o erro',
    () async {
      final api = ProfileApi(), controller = PresentationController();
      await controller.activate(api, 'one');
      api.fail = true;
      expect(
        await controller.select(api, PresentationVersion.classic),
        isFalse,
      );
      expect(controller.version, PresentationVersion.current);
      expect(controller.error, isNotNull);
      controller.dispose();
    },
  );
  testWidgets(
    'Fallback explícito mantém Navigator e estado compartilhado ao alternar versões',
    (t) async {
      final api = ProfileApi(), controller = PresentationController();
      await controller.activate(api, owner);
      await harness(
        t,
        controller,
        const PresentationSurface(
          screen: PresentationScreen.squad,
          current: CounterGame(),
        ),
      );
      final state = t.state<CounterGameState>(find.byType(CounterGame));
      await t.tap(find.text('Estado preservado 0'));
      await t.pumpAndSettle();
      final themeBefore = Theme.of(t.element(find.byType(CounterGame)));
      await controller.select(api, PresentationVersion.classic);
      await t.pumpAndSettle();
      expect(
        find.byKey(const Key('classic-presentation-pending')),
        findsOneWidget,
      );
      expect(find.textContaining('em preparação'), findsOneWidget);
      expect(t.state(find.byType(CounterGame)), same(state));
      expect(state.count, 1);
      expect(
        Theme.of(t.element(find.byType(CounterGame))).colorScheme,
        themeBefore.colorScheme,
      );
      await controller.select(api, PresentationVersion.current);
      await t.pumpAndSettle();
      expect(
        find.byKey(const Key('classic-presentation-pending')),
        findsNothing,
      );
      expect(state.count, 1);
      await close(t);
      controller.dispose();
    },
  );
  for (final career in ['MANUAL', 'FOURTH_DIVISION_DRAW', 'UNEMPLOYED']) {
    testWidgets(
      'Aparência vem antes de $career e sobrevive às ações originais da sala',
      (t) async {
        final fixture = Fixture016B(), controller = PresentationController();
        final api = ProfileApi(fixture);
        await controller.activate(api, owner);
        await harness(
          t,
          controller,
          WorldInitialLobby016B(
            api: api,
            worldId: 'world',
            coachId: owner,
            presentation: controller,
            onStarted: () async {},
            onLeave: () async {},
          ),
        );
        final selector = find.byKey(const ValueKey('presentation-CLASSIC_98'));
        await t.ensureVisible(selector);
        await t.tap(selector);
        await t.pumpAndSettle();
        expect(controller.version, PresentationVersion.classic);
        await t.ensureVisible(find.byKey(const Key('initial-career-choice')));
        await t.tap(find.byKey(const Key('initial-career-choice')));
        await t.pumpAndSettle();
        await t.tap(
          find.text(WorldInitialLobby016BStateNames.name(career)).last,
        );
        await t.pumpAndSettle();
        if (career == 'MANUAL') {
          final clubs = find.byWidgetPredicate(
            (w) =>
                w is DropdownButtonFormField<String> &&
                w.decoration.labelText ==
                    'Clube disponível das quatro divisões',
          );
          await t.ensureVisible(clubs);
          await t.tap(clubs);
          await t.pumpAndSettle();
          await t.tap(
            find.textContaining('Fixture clube da primeira divisão').last,
          );
          await t.pumpAndSettle();
        }
        await t.ensureVisible(find.byKey(const Key('save-initial-choice')));
        await t.tap(find.byKey(const Key('save-initial-choice')));
        await t.pumpAndSettle();
        final command = fixture.requests.lastWhere(
          (r) => r.url.path.endsWith('/choice'),
        );
        expect(jsonDecode(command.body)['choice'], career);
        expect(jsonDecode(command.body).containsKey('mode'), isFalse);
        if (career == 'FOURTH_DIVISION_DRAW') {
          fixture.view['canDraw'] = true;
          await t.pump(const Duration(seconds: 3));
          await t.pumpAndSettle();
          await t.ensureVisible(find.byKey(const Key('initial-draw')));
          await t.tap(find.byKey(const Key('initial-draw')));
          await t.pumpAndSettle();
          expect(fixture.view['drawCompleted'], isTrue);
        }
        expect(controller.version, PresentationVersion.classic);
        expect(api.versions[owner], 'CLASSIC_98');
        await close(t);
        controller.dispose();
      },
    );
  }
  testWidgets(
    'Configurações salvam versão sem clube e respeitam bloqueio sem alterar as cores',
    (t) async {
      final api = ProfileApi(),
          controller = PresentationController(),
          colors = ClubThemeController();
      await controller.activate(api, owner);
      final before = jsonEncode(colors.preferences);
      await harness(
        t,
        controller,
        AppearanceSettings(
          api: api,
          controller: colors,
          presentation: controller,
        ),
      );
      await t.ensureVisible(
        find.byKey(const ValueKey('presentation-CLASSIC_98')),
      );
      await t.tap(find.byKey(const ValueKey('presentation-CLASSIC_98')));
      await t.pumpAndSettle();
      expect(controller.version, PresentationVersion.classic);
      expect(jsonEncode(colors.preferences), before);
      await close(t);
      await harness(
        t,
        controller,
        AppearanceSettings(
          api: api,
          controller: colors,
          presentation: controller,
          locked: true,
        ),
      );
      final current = t.widget<RadioListTile<PresentationVersion>>(
        find.byKey(const ValueKey('presentation-CURRENT')),
      );
      expect(current.enabled, isFalse);
      expect(api.writes, hasLength(1));
      await close(t);
      controller.dispose();
      colors.dispose();
    },
  );
  for (final width in [360.0, 390.0, 430.0, 768.0, 1366.0, 1920.0]) {
    testWidgets('Seleção e aviso de preparação são legíveis em $width px', (
      t,
    ) async {
      final api = ProfileApi(), controller = PresentationController();
      await controller.activate(api, owner);
      await harness(
        t,
        controller,
        Scaffold(
          body: SingleChildScrollView(
            child: PresentationVersionSelector(
              api: api,
              controller: controller,
            ),
          ),
        ),
        width: width,
      );
      expect(find.text('VERSÃO ATUAL'), findsOneWidget);
      expect(find.text('VERSÃO CLÁSSICA (ANTIGA)'), findsOneWidget);
      await t.tap(find.byKey(const ValueKey('presentation-CLASSIC_98')));
      await t.pumpAndSettle();
      expect(
        find.byKey(const Key('classic-presentation-pending')),
        findsOneWidget,
      );
      expect(t.takeException(), isNull);
      expect(
        t.getRect(find.byKey(const ValueKey('presentation-CLASSIC_98'))).right,
        lessThanOrEqualTo(width),
      );
      await close(t);
      controller.dispose();
    });
  }
  testWidgets('Seleção aparece antes de assumir um clube no modal existente', (
    t,
  ) async {
    final api = ProfileApi(), controller = PresentationController();
    await controller.activate(api, owner);
    await harness(
      t,
      controller,
      Builder(
        builder: (context) => Scaffold(
          body: TextButton(
            onPressed: () => showDialog(
              context: context,
              builder: (_) => ClubSelectionDialog(
                api: api,
                presentation: controller,
                clubs: const [
                  {
                    'id': 'one',
                    'name': 'Clube de revisão',
                    'divisionDisplay': '4ª Divisão',
                    'cashBalance': 1100000,
                  },
                ],
              ),
            ),
            child: const Text('ABRIR'),
          ),
        ),
      ),
    );
    await t.tap(find.text('ABRIR'));
    await t.pumpAndSettle();
    expect(
      t.getTopLeft(find.text('ESCOLHA A APARÊNCIA:')).dy,
      lessThan(t.getTopLeft(find.text('ASSUMIR')).dy),
    );
    await t.tap(find.byKey(const ValueKey('presentation-CLASSIC_98')));
    await t.pumpAndSettle();
    await t.tap(find.text('ASSUMIR'));
    await t.pumpAndSettle();
    expect(controller.version, PresentationVersion.classic);
    await close(t);
    controller.dispose();
  });
}

class WorldInitialLobby016BStateNames {
  static String name(String value) => switch (value) {
    'MANUAL' => 'ASSUMIR CLUBE',
    'FOURTH_DIVISION_DRAW' => 'SORTEAR CLUBE DA 4ª DIVISÃO',
    _ => 'COMEÇAR DESEMPREGADO',
  };
}
