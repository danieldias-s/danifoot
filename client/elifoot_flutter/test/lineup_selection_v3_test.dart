import 'dart:convert';
import 'package:elifoot_flutter/api_client.dart';
import 'package:elifoot_flutter/classic_layout.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'classic_ux_014b_test.dart' show WaitingRoomApi, join;

class SelectionApi extends WaitingRoomApi {
  SelectionApi() : super(2);
  final modes = <String>[];
  final incumbents = <List<String>>[];
  @override
  Future<Map<String, dynamic>> selectLineup({
    required String worldId,
    required String coachId,
    required String formation,
    required String mode,
    required List<String> currentStarterIds,
    required int roundVersion,
    String? idempotencyKey,
  }) async {
    modes.add(mode);
    incumbents.add(List.of(currentStarterIds));
    return {
      'formation': formation,
      'starterPlayerIds': [
        ...List.generate(9, (i) => 'p-${i + 1}'),
        'p-10',
        mode == 'Automatic' ? 'p-16' : 'p-11',
      ],
      'benchPlayerIds': [
        'p-12',
        'p-13',
        'p-14',
        'p-15',
        mode == 'Automatic' ? 'p-11' : 'p-16',
      ],
      'roundVersion': ++this.roundVersion,
    };
  }
}

void main() {
  for (final mode in ['Automatic', 'Best']) {
    test(
      'API sends $mode, formation, incumbents and retry key to server',
      () async {
        final api = ElifootApiClient(
          baseUrl: 'http://test',
          httpClient: MockClient((request) async {
            expect(request.method, 'POST');
            expect(request.url.path, '/api/worlds/w/lineup-selection');
            expect(jsonDecode(request.body), {
              'coachId': 'c',
              'formation': '4-3-3',
              'mode': mode,
              'currentStarterPlayerIds': ['incumbent'],
              'expectedRoundVersion': 8,
              'idempotencyKey': 'retry-1',
            });
            return http.Response(
              jsonEncode({
                'starterPlayerIds': ['server-choice'],
                'benchPlayerIds': ['server-bench'],
                'roundVersion': 9,
              }),
              200,
            );
          }),
        );
        final result = await api.selectLineup(
          worldId: 'w',
          coachId: 'c',
          formation: '4-3-3',
          mode: mode,
          currentStarterIds: ['incumbent'],
          roundVersion: 8,
          idempotencyKey: 'retry-1',
        );
        expect(result['starterPlayerIds'], ['server-choice']);
        expect(result['benchPlayerIds'], ['server-bench']);
      },
    );
  }

  for (final width in [390.0, 1440.0]) {
    testWidgets(
      'Human selects server A and M while preserving formation at $width',
      (tester) async {
        await tester.binding.setSurfaceSize(Size(width, 950));
        addTearDown(() => tester.binding.setSurfaceSize(null));
        final api = SelectionApi();
        await join(tester, api);
        for (final mode in ['Automatic', 'Best']) {
          final picker = find.byKey(const ValueKey('btn-auto-escalar'));
          await tester.ensureVisible(picker);
          await tester.tap(picker);
          await tester.pumpAndSettle();
          await tester.tap(
            find.text(
              mode == 'Automatic'
                  ? 'A — AUTOMÁTICO (FORÇA + MÉRITO)'
                  : 'M — MELHORES (FORÇA ATUAL)',
            ),
          );
          await tester.pumpAndSettle();
          expect(api.modes.last, mode);
          expect(
            tester
                .widget<DropdownButton<String>>(
                  find.byKey(const ValueKey('dropdown-formation')),
                )
                .value,
            '4-4-2',
          );
          if (width >= 768) {
            final pitch = tester.widget<ClassicTacticalPitch>(
              find.byType(ClassicTacticalPitch),
            );
            final ids = pitch.lines.expand((line) => line).map((p) => p['id']);
            expect(ids, contains(mode == 'Automatic' ? 'p-16' : 'p-11'));
            expect(ids, isNot(contains(mode == 'Automatic' ? 'p-11' : 'p-16')));
          }
        }
        expect(api.incumbents[1], contains('p-16'));
        expect(tester.takeException(), isNull);
      },
    );
  }
}
