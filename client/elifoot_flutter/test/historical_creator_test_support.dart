import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';

Future<void> selectHistoricalCountries(
  WidgetTester tester,
  Iterable<String> countries,
) async {
  for (final code in countries) {
    final card = find.descendant(
      of: find.byKey(ValueKey('country-card-$code')),
      matching: find.byType(CheckboxListTile),
    );
    if (tester.widget<CheckboxListTile>(card).value == true) continue;
    await tester.ensureVisible(card);
    await tester.tap(card);
    await tester.pumpAndSettle();
  }
}

Finder historicalYear(String year) =>
    find.byKey(ValueKey('catalog-year-$year'));

Future<void> selectHistoricalYear(WidgetTester tester, String year) async {
  final card = historicalYear(year);
  if (tester.widget<CheckboxListTile>(card).value == true) return;
  await tester.ensureVisible(card);
  await tester.tap(card);
  await tester.pumpAndSettle();
}

List<Map<String, dynamic>> historicalYearResponse(Uri request, String year) {
  final countries = request.queryParameters['countries']!.split(',');
  return [
    {
      'year': year,
      'clubCount': countries.length * 32,
      'countries': [
        for (final country in countries)
          {'code': country, 'name': country, 'clubCount': 32},
      ],
    },
  ];
}
