import 'dart:convert';
import 'package:elifoot_flutter/api_client.dart';
import 'package:elifoot_flutter/custom_assembly_editor.dart';
import 'package:elifoot_flutter/world_creator.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';

Map<String, dynamic> fixture() => {
  'id': 'assembly-private',
  'name': 'Minha montagem',
  'gender': 'Female',
  'countries': ['BR'],
  'seed': 199801,
  'version': 1,
  'isSaved': false,
  'allowPersonalizedContent': true,
  'validation': {
    'isValid': false,
    'errors': ['Complete os 128 clubes antes de confirmar.'],
  },
  'package': {
    'gender': 'Female',
    'seed': 199801,
    'formatVersion': '1.0',
    'clubs': [
      {
        'id': 'club1',
        'name': 'Clube de teste',
        'shortName': 'CT',
        'country': 'Brasil',
        'divisionTier': 'PrimeiraDivisao',
        'primaryColorHex': '#006400',
        'secondaryColorHex': '#FFFFFF',
        'stadiumName': 'Estádio de teste',
        'builtCapacity': 20000,
        'usableCapacity': 20000,
        'conditionPercent': 100,
        'reputation': 40,
      },
    ],
    'players': [
      {
        'id': 'p1',
        'clubId': 'club1',
        'name': 'Atleta de teste',
        'position': 'G',
        'behavior': 'FairPlay',
        'strength': 20,
        'salaryPerRound': 1200,
        'contractRoundsRemaining': 30,
        'isStar': false,
      },
    ],
    'aiManagers': [],
  },
};
void main() {
  for (final width in [320.0, 1366.0]) {
    testWidgets(
      'Pre-save editor keeps invalid drafts and saves actual edited club/player fields at $width',
      (tester) async {
        await tester.binding.setSurfaceSize(Size(width, 1000));
        addTearDown(() => tester.binding.setSurfaceSize(null));
        Map<String, dynamic>? sent;
        int updates = 0;
        final a = fixture();
        final api = ElifootApiClient(
          httpClient: MockClient((req) async {
            expect(
              req.url.path,
              '/api/world-creator/assemblies/assembly-private',
            );
            expect(
              req.headers['Authorization'],
              'Bearer localdev-privateowner',
            );
            sent = Map<String, dynamic>.from(jsonDecode(req.body) as Map);
            updates++;
            expect(sent!['expectedVersion'], a['version']);
            expect(sent!['savePreset'], true);
            a['package'] = sent!['package'];
            a['version'] = (a['version'] as int) + 1;
            a['isSaved'] = true;
            return http.Response(jsonEncode(a), 200);
          }),
        );
        await tester.pumpWidget(
          MaterialApp(
            theme: ThemeData.dark(),
            home: CustomAssemblyEditor(
              api: api,
              coachId: 'private-owner',
              assembly: a,
              countryOptions: const [
                {'code': 'BR', 'name': 'Brasil'},
              ],
            ),
          ),
        );
        await tester.pumpAndSettle();
        expect(find.text('MONTAGEM ANTES DO SAVE'), findsOneWidget);
        await tester.tap(find.text('Clube de teste'));
        await tester.pumpAndSettle();
        await tester.ensureVisible(find.text('EDITAR CLUBE / DIVISÃO'));
        await tester.tap(find.text('EDITAR CLUBE / DIVISÃO'));
        await tester.pumpAndSettle();
        await tester.enterText(
          find.byKey(const ValueKey('assembly-field-name')),
          'Clube editado antes do save',
        );
        await tester.tap(find.text('APLICAR AO RASCUNHO'));
        await tester.pumpAndSettle();
        await tester.ensureVisible(find.textContaining('Atleta de teste'));
        await tester.tap(find.textContaining('Atleta de teste'));
        await tester.pumpAndSettle();
        await tester.enterText(
          find.byKey(const ValueKey('assembly-field-name')),
          'Atleta personalizada',
        );
        await tester.enterText(
          find.byKey(const ValueKey('assembly-field-strength')),
          '50',
        );
        await tester.enterText(
          find.byKey(const ValueKey('assembly-field-salaryPerRound')),
          '3210',
        );
        await tester.enterText(
          find.byKey(const ValueKey('assembly-field-contractRoundsRemaining')),
          '12',
        );
        await tester.tap(find.text('APLICAR AO RASCUNHO'));
        await tester.pumpAndSettle();
        await tester.ensureVisible(find.text('SALVAR MINHA MONTAGEM'));
        await tester.tap(find.text('SALVAR MINHA MONTAGEM'));
        await tester.pumpAndSettle();
        expect(updates, 1);
        final p = sent!['package'] as Map;
        expect(
          (p['clubs'] as List).first['name'],
          'Clube editado antes do save',
        );
        final player = (p['players'] as List).first as Map;
        expect(player['name'], 'Atleta personalizada');
        expect(player['strength'], 50);
        expect(player['salaryPerRound'], 3210);
        expect(player['contractRoundsRemaining'], 12);
        expect(find.text('MONTAGEM SALVA NA SUA CONTA.'), findsOneWidget);
        expect(
          find.text('Complete os 128 clubes antes de confirmar.'),
          findsOneWidget,
        );
        expect(find.textContaining('Atleta personalizada'), findsWidgets);
        expect(tester.takeException(), isNull);
        await tester.pumpWidget(const SizedBox.shrink());
      },
    );
  }
  testWidgets(
    'Saved assembly is restored only on request and confirms through private assembly endpoint',
    (tester) async {
      await tester.binding.setSurfaceSize(const Size(1366, 1000));
      addTearDown(() => tester.binding.setSurfaceSize(null));
      final a = fixture();
      a['isSaved'] = true;
      a['validation'] = {'isValid': true, 'errors': []};
      String? key;
      int lists = 0;
      final api = ElifootApiClient(
        httpClient: MockClient((req) async {
          expect(req.headers['Authorization'], 'Bearer localdev-privateowner');
          dynamic response;
          if (req.url.path.endsWith('/countries')) {
            response = [
              {
                'code': 'BR',
                'name': 'Brasil',
                'maleClubs': 0,
                'femaleClubs': 0,
              },
            ];
          } else if (req.url.path.endsWith('/last-configuration')) {
            response = null;
          } else if (req.url.path.endsWith('/assemblies')) {
            lists++;
            response = [a];
          } else if (req.url.path.endsWith('/assembly-private/confirm')) {
            final body = jsonDecode(req.body) as Map;
            key = body['idempotencyKey'];
            response = {'id': 'new-world', 'name': 'Mundo independente'};
          } else {
            expect(req.url.path.endsWith('/assemblies/assembly-private'), true);
            response = a;
          }
          return http.Response(jsonEncode(response), 200);
        }),
      );
      await tester.pumpWidget(
        MaterialApp(
          theme: ThemeData.dark(),
          home: WorldCreatorScreen(api: api, coachId: 'private-owner'),
        ),
      );
      await tester.pumpAndSettle();
      expect(lists, 0);
      await tester.tap(find.text('USAR MONTAGEM SALVA'));
      await tester.pumpAndSettle();
      await tester.tap(find.textContaining('Minha montagem • FEMININO'));
      await tester.pumpAndSettle();
      expect(lists, 1);
      expect(find.text('MONTAGEM SALVA NA SUA CONTA'), findsOneWidget);
      expect(find.textContaining('7/8'), findsOneWidget);
      await tester.tap(find.text('CONTINUAR'));
      await tester.pumpAndSettle();
      await tester.ensureVisible(find.text('CONFIRMAR CRIAÇÃO'));
      await tester.tap(find.text('CONFIRMAR CRIAÇÃO'));
      await tester.pumpAndSettle();
      expect(key, isNotEmpty);
      expect(tester.takeException(), isNull);
    },
  );
}
