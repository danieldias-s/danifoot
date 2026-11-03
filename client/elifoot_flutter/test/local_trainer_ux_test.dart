import 'dart:convert';
import 'package:elifoot_flutter/main.dart';
import 'package:elifoot_flutter/local_trainer_panel.dart';
import 'package:elifoot_flutter/session_store_stub.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences_platform_interface/in_memory_shared_preferences_async.dart';
import 'package:shared_preferences_platform_interface/shared_preferences_async_platform_interface.dart';
import 'classic_ux_014b_test.dart' show WaitingRoomApi;

class TrainerAccessApi extends WaitingRoomApi {
  final devices = <String>[];
  bool failWorldList = false;
  TrainerAccessApi() : super(2);
  @override
  Future<List<dynamic>> listWorlds() {
    if (failWorldList) throw Exception('Falha temporária de rede');
    return super.listWorlds();
  }

  @override
  Future<Map<String, dynamic>> localLogin(
    String nickname,
    String deviceId,
  ) async {
    devices.add(deviceId);
    return {
      'coachId': 'login-$deviceId',
      'nickname': nickname,
      'deviceId': deviceId,
    };
  }
}

void desktop(WidgetTester tester) {
  tester.view.physicalSize = const Size(1366, 1000);
  tester.view.devicePixelRatio = 1;
  addTearDown(tester.view.resetPhysicalSize);
  addTearDown(tester.view.resetDevicePixelRatio);
}

void main() {
  SharedPreferencesAsyncPlatform? original;
  setUp(() async {
    original = SharedPreferencesAsyncPlatform.instance;
    SharedPreferencesAsyncPlatform.instance =
        InMemorySharedPreferencesAsync.empty();
    await initializeSessionStore();
  });
  tearDown(() {
    SharedPreferencesAsyncPlatform.instance = original;
  });

  testWidgets(
    'Criar, cancelar, fechar e trocar mantêm os acessos anteriores pelo nome',
    (tester) async {
      desktop(tester);
      await writeSession(
        jsonEncode({
          'nickname': 'Original',
          'coachId': 'original-coach',
          'deviceId': 'original-device',
          'lastClubName': 'Clube Original',
          'lastWorldName': 'Mundo Original',
        }),
      );
      final api = TrainerAccessApi();
      await tester.pumpWidget(ElifootClassicApp(apiClient: api));
      await tester.pumpAndSettle();
      expect(find.textContaining('original-device'), findsNothing);
      await tester.tap(find.byKey(const Key('new-local-trainer')));
      await tester.pumpAndSettle();
      await tester.tap(find.text('CANCELAR'));
      await tester.pumpAndSettle();
      expect(jsonDecode(readSession()!)['deviceId'], 'original-device');
      expect(api.devices, isEmpty);

      await tester.tap(find.byKey(const Key('new-local-trainer')));
      await tester.pumpAndSettle();
      await tester.enterText(find.byKey(const Key('trainer-name')), '   ');
      await tester.tap(find.text('CRIAR E SALVAR'));
      await tester.pumpAndSettle();
      expect(find.text('Digite o nome do treinador.'), findsOneWidget);
      await tester.enterText(find.byKey(const Key('trainer-name')), 'Daniel');
      await tester.tap(find.text('CRIAR E SALVAR'));
      await tester.pumpAndSettle();
      final newDevice = jsonDecode(readSession()!)['deviceId'];
      expect(newDevice, isNot('original-device'));
      expect(readSavedSessions(), hasLength(2));
      expect(
        find.descendant(
          of: find.byKey(const Key('current-local-trainer')),
          matching: find.text('Daniel'),
        ),
        findsOneWidget,
      );
      expect(find.text('ID Dispositivo (Sessão)'), findsNothing);
      expect(find.textContaining(newDevice), findsNothing);

      await tester.pumpWidget(const SizedBox());
      await tester.pumpWidget(ElifootClassicApp(apiClient: api));
      await tester.pumpAndSettle();
      expect(
        tester
            .widget<LocalTrainerPanel>(find.byType(LocalTrainerPanel))
            .deviceId,
        newDevice,
      );
      await tester.tap(find.byType(DropdownButtonFormField<String>));
      await tester.pumpAndSettle();
      expect(find.text('EM USO'), findsOneWidget);
      await tester.tap(find.text('Original').last);
      await tester.pumpAndSettle();
      expect(api.devices.last, 'original-device');
      expect(jsonDecode(readSession()!)['coachId'], 'original-coach');
      expect(readSavedSessions(), hasLength(2));
      expect(find.textContaining('Clube Original'), findsWidgets);
      await tester.pumpWidget(const SizedBox());
    },
  );

  testWidgets(
    'Recuperação manual usa o identificador antigo sem criar outra identidade',
    (tester) async {
      desktop(tester);
      await writeSession(
        jsonEncode({
          'nickname': 'Antigo',
          'coachId': 'canonical',
          'deviceId': 'old-access',
        }),
      );
      final api = TrainerAccessApi();
      await tester.pumpWidget(ElifootClassicApp(apiClient: api));
      await tester.pumpAndSettle();
      await tester.tap(find.text('Recuperação de acesso'));
      await tester.pumpAndSettle();
      await tester.ensureVisible(find.text('RECUPERAR TREINADOR ANTIGO'));
      await tester.tap(find.text('RECUPERAR TREINADOR ANTIGO'));
      await tester.pumpAndSettle();
      await tester.enterText(
        find.byKey(const Key('trainer-recovery-id')),
        'old-access',
      );
      await tester.tap(find.text('RECUPERAR ACESSO'));
      await tester.pumpAndSettle();
      expect(api.devices.single, 'old-access');
      expect(readSavedSessions(), hasLength(1));
      expect(jsonDecode(readSession()!)['coachId'], 'canonical');
      await tester.pumpWidget(const SizedBox());
    },
  );

  testWidgets(
    'VOCÊ usa a identidade da sessão e o clube continua salvo ao sair da sala',
    (tester) async {
      desktop(tester);
      final api = WaitingRoomApi(2);
      await tester.pumpWidget(ElifootClassicApp(apiClient: api));
      await tester.tap(find.byKey(const Key('enter-local-lobby')));
      await tester.pumpAndSettle();
      await tester.ensureVisible(find.text('ENTRAR COM CLUBE'));
      await tester.tap(find.text('ENTRAR COM CLUBE'));
      await tester.pumpAndSettle();
      await tester.tap(find.byKey(const Key('choose-club-club-1')));
      await tester.pumpAndSettle();
      expect(
        find.descendant(
          of: find.byKey(const Key('active-trainer-in-room')),
          matching: find.textContaining('VOCÊ • Treinador 1'),
        ),
        findsOneWidget,
      );
      expect(
        find.descendant(
          of: find.byKey(const Key('active-trainer-in-room')),
          matching: find.textContaining('Treinador 2'),
        ),
        findsNothing,
      );
      expect(readSavedSessions().single['lastClubName'], 'Atlético Verde');
      await tester.ensureVisible(find.text('TROCAR SALA'));
      await tester.tap(find.text('TROCAR SALA'));
      await tester.pumpAndSettle();
      expect(jsonDecode(readSession()!)['worldId'], isNull);
      expect(readSavedSessions().single['lastClubName'], 'Atlético Verde');
      await tester.pumpWidget(const SizedBox());
    },
  );

  testWidgets(
    'Acesso autenticado fica salvo mesmo se a consulta das salas falhar',
    (tester) async {
      desktop(tester);
      final api = TrainerAccessApi()..failWorldList = true;
      await tester.pumpWidget(ElifootClassicApp(apiClient: api));
      await tester.tap(find.byKey(const Key('new-local-trainer')));
      await tester.pumpAndSettle();
      await tester.enterText(find.byKey(const Key('trainer-name')), 'Daniel');
      await tester.tap(find.text('CRIAR E SALVAR'));
      await tester.pumpAndSettle();
      final savedDevice = jsonDecode(readSession()!)['deviceId'];
      expect(readSavedSessions().single['nickname'], 'Daniel');
      expect(find.textContaining('Falha temporária de rede'), findsOneWidget);
      api.failWorldList = false;
      await tester.tap(find.byKey(const Key('enter-local-lobby')));
      await tester.pumpAndSettle();
      expect(api.devices, [savedDevice, savedDevice]);
      expect(readSavedSessions(), hasLength(1));
      await tester.pumpWidget(const SizedBox());
    },
  );

  for (final width in [360.0, 390.0, 430.0, 768.0, 1366.0, 1920.0]) {
    testWidgets('Painel identifica treinador e ações em $width px', (
      tester,
    ) async {
      tester.view.physicalSize = Size(width, 900);
      tester.view.devicePixelRatio = 1;
      addTearDown(tester.view.resetPhysicalSize);
      addTearDown(tester.view.resetDevicePixelRatio);
      await tester.pumpWidget(
        MaterialApp(
          home: Scaffold(
            body: SingleChildScrollView(
              child: LocalTrainerPanel(
                nickname: 'Treinador com nome comprido para leitura no celular',
                deviceId: 'internal-only',
                detail: 'Clube de nome comprido • Campeonato de teste',
                connected: true,
                busy: false,
                profiles: const [
                  LocalTrainerChoice(
                    'internal-only',
                    'Treinador com nome comprido',
                    'Clube de nome comprido • Campeonato',
                  ),
                ],
                enter: () {},
                create: () {},
                select: (_) {},
              ),
            ),
          ),
        ),
      );
      await tester.pumpAndSettle();
      expect(tester.takeException(), isNull);
      expect(find.textContaining('internal-only'), findsNothing);
      await tester.ensureVisible(find.byKey(const Key('new-local-trainer')));
      expect(
        tester.getRect(find.byKey(const Key('new-local-trainer'))).right,
        lessThanOrEqualTo(width),
      );
      await tester.tap(find.byType(DropdownButtonFormField<String>));
      await tester.pumpAndSettle();
      expect(find.text('EM USO'), findsOneWidget);
      expect(tester.takeException(), isNull);
    });
  }
}
