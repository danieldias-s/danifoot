import 'package:elifoot_flutter/club_theme.dart';
import 'package:elifoot_flutter/creator_division_picker.dart';
import 'package:elifoot_flutter/creator_visual.dart';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

Map<String, dynamic> _club(int index, {String? name, String year = '2025'}) => {
  'key': 'PT:$year:qa-$index:Male',
  'name': name ?? 'Clube QA $index ($year)',
  'countryCode': 'PT',
  'countryName': 'Portugal',
  'season': year,
  'players': <Map<String, dynamic>>[],
};

class _PickerData {
  final clubs = [for (var i = 0; i < 129; i++) _club(i)];
  List<List<Map<String, dynamic>?>> get divisions => [
    for (var d = 0; d < 4; d++) [for (var p = 0; p < 8; p++) clubs[d * 8 + p]],
  ];
  List<Map<String, dynamic>> get districts => clubs.sublist(32, 128);
}

Future<void> _openPicker(
  WidgetTester tester,
  _PickerData data, {
  double width = 1366,
  int current = 0,
  bool assignedOnly = true,
  bool includeDistricts = true,
}) async {
  tester.view.devicePixelRatio = 1;
  tester.view.physicalSize = Size(width, 850);
  addTearDown(tester.view.resetDevicePixelRatio);
  addTearDown(tester.view.resetPhysicalSize);
  await tester.pumpWidget(
    MaterialApp(
      theme: ClassicTheme.derive().material(),
      home: _PickerHarness(
        data: data,
        current: current,
        assignedOnly: assignedOnly,
        includeDistricts: includeDistricts,
      ),
    ),
  );
  await tester.tap(find.text('ABRIR TROCA'));
  await tester.pumpAndSettle();
}

class _PickerHarness extends StatefulWidget {
  final _PickerData data;
  final int current;
  final bool assignedOnly, includeDistricts;
  const _PickerHarness({
    required this.data,
    required this.current,
    required this.assignedOnly,
    required this.includeDistricts,
  });
  @override
  State<_PickerHarness> createState() => _PickerHarnessState();
}

class _PickerHarnessState extends State<_PickerHarness> {
  String? result;
  @override
  Widget build(BuildContext context) => Scaffold(
    body: Column(
      children: [
        ElevatedButton(
          onPressed: () async {
            final key = await showCreatorDivisionPicker(
              context,
              divisions: widget.data.divisions,
              eligibleClubs: widget.data.clubs,
              districtClubs: widget.includeDistricts
                  ? widget.data.districts
                  : const [],
              assignedOnly: widget.assignedOnly,
              currentClub: widget.data.clubs[widget.current],
            );
            if (mounted) setState(() => result = key);
          },
          child: const Text('ABRIR TROCA'),
        ),
        if (result != null) Text('Resultado: $result'),
      ],
    ),
  );
}

Finder _option(Map<String, dynamic> club) =>
    find.byKey(ValueKey('division-swap-option-${club['key']}'));

Future<void> _search(WidgetTester tester, String query) async {
  final field = find.byKey(const ValueKey('division-swap-search'));
  await tester.ensureVisible(field);
  await tester.enterText(field, query);
  await tester.pumpAndSettle();
}

void main() {
  testWidgets('Revisão troca divisão por distrital sem alterar a montagem', (
    tester,
  ) async {
    final data = _PickerData();
    await _openPicker(tester, data);
    expect(find.byType(Dialog), findsOneWidget);
    expect(find.text('CONFIRMAR TROCA'), findsOneWidget);
    expect(_option(data.clubs.first), findsNothing);
    await _search(tester, 'Clube QA 32 (2025)');
    final target = _option(data.clubs[32]);
    expect(target, findsOneWidget);
    expect(
      find.descendant(of: target, matching: find.byType(CreatorCountryFlag)),
      findsOneWidget,
    );
    await tester.ensureVisible(target);
    await tester.tap(target);
    await tester.pumpAndSettle();
    expect(find.text('Sai: Clube QA 0 (2025)'), findsOneWidget);
    expect(find.text('Entra: Clube QA 32 (2025)'), findsOneWidget);
    expect(find.text('Distritais • Posição 1'), findsWidgets);
    expect(
      find.byKey(const ValueKey('division-swap-reciprocal-note')),
      findsOneWidget,
    );
    await tester.tap(find.byKey(const ValueKey('division-swap-confirm')));
    await tester.pumpAndSettle();
    expect(find.text('Resultado: ${data.clubs[32]['key']}'), findsOneWidget);
    // The picker only returns the complete variant key. The shared creator
    // applies the reciprocal swap after its own eligibility validation.
    expect(data.divisions[0][0], same(data.clubs[0]));
    expect(data.districts.first, same(data.clubs[32]));
    expect(tester.takeException(), isNull);
  });

  testWidgets('Filtro Distritais percorre os 96 e oculta não escalados', (
    tester,
  ) async {
    final data = _PickerData();
    await _openPicker(tester, data);
    final filter = find.byKey(const ValueKey('division-swap-filter'));
    await tester.tap(filter);
    await tester.pumpAndSettle();
    expect(find.text('Não escalados'), findsNothing);
    await tester.tap(find.text('Distritais').last);
    await tester.pumpAndSettle();
    await _search(tester, 'Clube QA 127 (2025)');
    expect(_option(data.clubs[127]), findsOneWidget);
    await _search(tester, 'Clube QA 128 (2025)');
    expect(_option(data.clubs[128]), findsNothing);
    expect(find.text('Nenhum clube encontrado.'), findsOneWidget);
    expect(tester.takeException(), isNull);
  });

  testWidgets('Origem distrital e variantes do mesmo clube mantêm a chave', (
    tester,
  ) async {
    final data = _PickerData();
    data.clubs[0] = _club(0, name: 'Clube QA em duas épocas', year: '1985');
    data.clubs[32] = _club(0, name: 'Clube QA em duas épocas');
    await _openPicker(tester, data, current: 32);
    expect(find.text('Distritais • Posição 1'), findsOneWidget);
    await _search(tester, 'Clube QA em duas épocas');
    final target = _option(data.clubs[0]);
    expect(target, findsOneWidget);
    expect(_option(data.clubs[32]), findsNothing);
    expect(find.textContaining('Ano 1985'), findsOneWidget);
    await tester.ensureVisible(target);
    await tester.tap(target);
    await tester.pumpAndSettle();
    await tester.tap(find.byKey(const ValueKey('division-swap-confirm')));
    await tester.pumpAndSettle();
    expect(find.text('Resultado: PT:1985:qa-0:Male'), findsOneWidget);
  });

  testWidgets('Troca na mesma divisão e cancelamento continuam disponíveis', (
    tester,
  ) async {
    final data = _PickerData();
    await _openPicker(tester, data);
    await _search(tester, 'Clube QA 1 (2025)');
    final target = _option(data.clubs[1]);
    await tester.ensureVisible(target);
    await tester.tap(target);
    await tester.pumpAndSettle();
    expect(find.text('1ª Divisão • Posição 2'), findsWidgets);
    await tester.tap(find.byKey(const ValueKey('division-swap-cancel')));
    await tester.pumpAndSettle();
    expect(find.textContaining('Resultado:'), findsNothing);
    expect(data.divisions[0][0], same(data.clubs[0]));
  });

  testWidgets('Seletor anterior preserva não escalados e botão desktop', (
    tester,
  ) async {
    final data = _PickerData();
    await _openPicker(
      tester,
      data,
      assignedOnly: false,
      includeDistricts: false,
    );
    expect(find.text('TROCAR CLUBES'), findsOneWidget);
    final filter = find.byKey(const ValueKey('division-swap-filter'));
    await tester.tap(filter);
    await tester.pumpAndSettle();
    expect(find.text('Distritais'), findsNothing);
    await tester.tap(find.text('Não escalados').last);
    await tester.pumpAndSettle();
    await _search(tester, 'Clube QA 128 (2025)');
    expect(_option(data.clubs[128]), findsOneWidget);
    expect(tester.takeException(), isNull);
  });

  for (final width in [360.0, 390.0, 430.0]) {
    testWidgets('Troca mobile responsiva e ações acessíveis em $width px', (
      tester,
    ) async {
      final data = _PickerData();
      data.clubs[0] = _club(
        0,
        name: 'Clube QA com nome bastante comprido para leitura em celular',
      );
      data.clubs[32] = _club(
        32,
        name: 'Outra equipe QA com nome extenso para conferir a troca',
      );
      await _openPicker(tester, data, width: width);
      expect(find.byType(BottomSheet), findsOneWidget);
      await _search(tester, 'Outra equipe QA');
      final target = _option(data.clubs[32]);
      await tester.ensureVisible(target);
      await tester.tap(target);
      await tester.pumpAndSettle();
      final confirm = find.byKey(const ValueKey('division-swap-confirm'));
      final cancel = find.byKey(const ValueKey('division-swap-cancel'));
      expect(tester.getRect(confirm).height, greaterThanOrEqualTo(44));
      expect(tester.getRect(cancel).height, greaterThanOrEqualTo(44));
      expect(tester.getRect(confirm).bottom, lessThanOrEqualTo(850));
      expect(tester.getRect(confirm).right, lessThanOrEqualTo(width));
      tester.view.viewInsets = const FakeViewPadding(bottom: 290);
      addTearDown(tester.view.resetViewInsets);
      await tester.pumpAndSettle();
      expect(tester.getRect(confirm).bottom, lessThanOrEqualTo(560));
      expect(tester.takeException(), isNull);
      tester.view.resetViewInsets();
      await tester.pumpAndSettle();
      await tester.tap(cancel);
      await tester.pumpAndSettle();
    });
  }
}
