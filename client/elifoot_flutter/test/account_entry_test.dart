import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:shared_preferences/shared_preferences.dart';
import 'package:elifoot_flutter/account_screens.dart';
import 'package:elifoot_flutter/api_client.dart';
import 'package:elifoot_flutter/main.dart';

final account = <String, dynamic>{
  'accountId': 'account-server-id',
  'username': 'treinador',
  'displayName': 'Daniel',
  'coachId': 'coach-server-id',
  'profiles': [],
};
Map<String, dynamic> room(String id, {bool mine = false, bool full = false}) =>
    {
      'id': id,
      'name': 'Campeonato $id',
      'inviteCode': 'COD-$id',
      'seasonNumber': 1,
      'roundNumber': 2,
      'totalClubs': 128,
      'maxHumans': 2,
      'currentHumans': full ? 2 : 1,
      'connectedHumans': 0,
      'roundState': 'PREPARING',
      'accountOwnsSeat': mine,
      'reconnectCoachId': mine ? 'coach-server-id' : null,
      'ownedCoachIds': mine ? ['coach-server-id'] : [],
      'myClubName': mine ? 'Meu clube' : null,
      'readyCounter': {
        'readyCount': 1,
        'totalHumans': full ? 2 : 1,
        'participants': [],
      },
    };

void main() {
  test(
    'Falha de conexão no login informa indisponibilidade sem expor detalhes técnicos',
    () async {
      final api = ElifootApiClient(
        baseUrl: 'http://localhost',
        httpClient: MockClient(
          (_) async => throw http.ClientException('internal transport details'),
        ),
      );
      api.enableAccountSession();
      await expectLater(
        api.signInAccount('treinador', 'Senha-local98!'),
        throwsA(
          predicate(
            (e) =>
                e.toString().contains('Conexão indisponível') &&
                !e.toString().contains('internal transport'),
          ),
        ),
      );
    },
  );
  for (final width in [360.0, 390.0, 430.0, 768.0, 1366.0, 1920.0]) {
    testWidgets('Login, início e lobby acessíveis em $width px', (t) async {
      t.view.physicalSize = Size(width, 1000);
      t.view.devicePixelRatio = 1;
      addTearDown(t.view.resetPhysicalSize);
      addTearDown(t.view.resetDevicePixelRatio);
      await t.pumpWidget(
        MaterialApp(home: AccountLoginScreen(submit: (_, _, _) async {})),
      );
      expect(find.text('Entre na sua conta'), findsOneWidget);
      expect(find.byKey(const Key('account-username')), findsOneWidget);
      expect(find.byKey(const Key('account-password')), findsOneWidget);
      expect(find.text('CRIAR CAMPEONATO'), findsNothing);
      expect(find.textContaining('deviceId'), findsNothing);
      expect(find.textContaining('accountId'), findsNothing);
      expect(t.takeException(), isNull);
      var resumed = 0, created = 0, entered = 0;
      var lobby = false;
      Widget portal() => MaterialApp(
        home: StatefulBuilder(
          builder: (context, set) => AccountPortal(
            account: account,
            worlds: [
              room('meu', mine: true, full: true),
              room('aberto'),
              room('lotado', full: true),
            ],
            lobby: lobby,
            busy: false,
            appearancePicker: const Text('ESCOLHA A APARÊNCIA:'),
            home: () => set(() => lobby = false),
            openLobby: () => set(() => lobby = true),
            create: () => created++,
            refresh: () {},
            joinByCode: () {},
            logout: () {},
            profile: () {},
            resume: (_) => resumed++,
            enter: (_) => entered++,
          ),
        ),
      );
      await t.pumpWidget(portal());
      await t.pumpAndSettle();
      expect(find.text('Olá, Daniel!'), findsOneWidget);
      expect(find.text('Campeonato meu'), findsOneWidget);
      expect(find.text('Campeonato aberto'), findsNothing);
      expect(find.text('EM BREVE'), findsWidgets);
      await t.ensureVisible(find.text('RETOMAR MINHA SESSÃO'));
      await t.tap(find.text('RETOMAR MINHA SESSÃO'));
      await t.pump();
      expect(resumed, 1);
      final createAction = width < 1000
          ? find.byKey(const ValueKey('account-action-CRIAR CAMPEONATO'))
          : find.text('CRIAR CAMPEONATO');
      await t.ensureVisible(createAction);
      await t.tap(createAction);
      expect(created, 1);
      final lobbyAction = width < 1000
          ? find.byKey(const ValueKey('account-action-VER SALAS'))
          : find.text('VER SALAS');
      await t.ensureVisible(lobbyAction);
      await t.tap(lobbyAction);
      await t.pumpAndSettle();
      expect(find.text('Lobby de campeonatos'), findsOneWidget);
      expect(find.text('Campeonato aberto'), findsOneWidget);
      await t.ensureVisible(find.text('MINHAS SALAS'));
      await t.tap(find.text('MINHAS SALAS'));
      await t.pumpAndSettle();
      expect(find.text('Campeonato aberto'), findsNothing);
      expect(find.text('Campeonato meu'), findsOneWidget);
      await t.tap(find.text('TODAS AS SALAS'));
      await t.pumpAndSettle();
      await t.enterText(find.byKey(const Key('account-room-search')), 'aberto');
      await t.pumpAndSettle();
      expect(find.text('Campeonato meu'), findsNothing);
      expect(find.text('Campeonato aberto'), findsOneWidget);
      await t.ensureVisible(find.text('ENTRAR'));
      await t.tap(find.text('ENTRAR'));
      expect(entered, 1);
      expect(t.takeException(), isNull);
      await t.pumpWidget(const SizedBox());
    });
  }

  testWidgets(
    'Senha pode ser mostrada, erro é anunciado e teclado não oculta a ação',
    (t) async {
      t.view.physicalSize = const Size(360, 700);
      t.view.devicePixelRatio = 1;
      t.view.viewInsets = const FakeViewPadding(bottom: 280);
      addTearDown(t.view.resetPhysicalSize);
      addTearDown(t.view.resetDevicePixelRatio);
      addTearDown(t.view.resetViewInsets);
      var calls = 0;
      await t.pumpWidget(
        MaterialApp(
          home: AccountLoginScreen(
            submit: (u, p, n) async {
              calls++;
              throw Exception('Credenciais inválidas.');
            },
          ),
        ),
      );
      await t.enterText(find.byKey(const Key('account-username')), 'daniel');
      await t.enterText(
        find.byKey(const Key('account-password')),
        'senha-informada',
      );
      await t.ensureVisible(find.byTooltip('Mostrar senha'));
      await t.tap(find.byTooltip('Mostrar senha'));
      await t.pump();
      expect(
        t
            .widget<TextFormField>(find.byKey(const Key('account-password')))
            .controller!
            .text,
        'senha-informada',
      );
      await t.ensureVisible(find.byKey(const Key('account-submit')));
      await t.tap(find.byKey(const Key('account-submit')));
      await t.pumpAndSettle();
      expect(calls, 1);
      expect(find.text('Credenciais inválidas.'), findsOneWidget);
      expect(t.takeException(), isNull);
    },
  );

  testWidgets(
    'Aplicação abre com conta, autentica e só depois mostra ações de campeonato',
    (t) async {
      SharedPreferences.setMockInitialValues({});
      var authenticated = false;
      final requests = <http.Request>[];
      final api = ElifootApiClient(
        baseUrl: 'http://localhost',
        httpClient: MockClient((r) async {
          requests.add(r);
          if (r.url.path == '/api/auth/csrf') {
            return http.Response(
              jsonEncode({'csrfToken': 'csrf-in-memory'}),
              200,
            );
          }
          if (r.url.path == '/api/auth/session') {
            return http.Response(
              jsonEncode(authenticated ? account : {}),
              authenticated ? 200 : 401,
            );
          }
          if (r.url.path == '/api/auth/login') {
            expect(
              r.headers['X-CSRF-TOKEN'] ?? r.headers['x-csrf-token'],
              'csrf-in-memory',
            );
            expect(jsonDecode(r.body)['username'], 'treinador');
            authenticated = true;
            return http.Response(jsonEncode(account), 200);
          }
          if (r.url.path == '/api/worlds') {
            return http.Response(jsonEncode([room('real', mine: true)]), 200);
          }
          if (r.url.path == '/api/profile/appearance') {
            return http.Response(jsonEncode({'mode': 'CURRENT'}), 200);
          }
          return http.Response('{}', 404);
        }),
      );
      await t.pumpWidget(ElifootClassicApp(apiClient: api));
      await t.pumpAndSettle();
      expect(find.text('Entre na sua conta'), findsOneWidget);
      expect(find.text('CRIAR CAMPEONATO'), findsNothing);
      await t.enterText(find.byKey(const Key('account-username')), 'treinador');
      await t.enterText(
        find.byKey(const Key('account-password')),
        'Senha-local98!',
      );
      await t.ensureVisible(find.byKey(const Key('account-submit')));
      await t.tap(find.byKey(const Key('account-submit')));
      await t.pumpAndSettle();
      expect(find.text('Olá, Daniel!'), findsOneWidget);
      expect(find.text('Campeonato real'), findsOneWidget);
      expect(
        requests.where((r) => r.url.path == '/api/auth/local-login'),
        isEmpty,
      );
      expect(
        requests.where((r) => r.headers.containsKey('Authorization')),
        isEmpty,
      );
      final prefs = await SharedPreferences.getInstance();
      expect(
        prefs.getKeys().where(
          (k) => k.contains('password') || k.contains('token'),
        ),
        isEmpty,
      );
      expect(t.takeException(), isNull);
      await t.pumpWidget(const SizedBox());
    },
  );
}
