import 'dart:convert';
import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:elifoot_flutter/classic98_widgets.dart';
import 'package:elifoot_flutter/club_theme.dart';
import 'package:elifoot_flutter/live_match_data.dart';
import 'package:elifoot_flutter/live_match_view.dart';
import 'package:elifoot_flutter/player_rankings.dart';
import 'package:elifoot_flutter/visual_components.dart';

void main() {
  testWidgets('Cabeçalhos compartilhados mantêm contraste no visual clássico', (
    t,
  ) async {
    await t.pumpWidget(
      MaterialApp(
        theme: classic98Theme(),
        home: const Scaffold(
          body: Column(
            children: [
              ClassicTableHeader(
                title: 'Ranking',
                subtitle: 'Temporada',
                trailing: 'Atualizado',
              ),
              ClassicPaperPanel(
                title: 'Jogadores',
                icon: Icons.people,
                child: Text('Dados'),
              ),
              ClassicTable(
                tableKey: 'color-table',
                columns: ['JOGADOR', 'GOLS'],
                rows: [],
              ),
            ],
          ),
        ),
      ),
    );
    await t.pumpAndSettle();
    for (final label in ['Ranking', 'Temporada', 'Atualizado']) {
      expect(t.widget<Text>(find.text(label)).style!.color, Colors.white);
    }
    expect(
      t.widget<DataTable>(find.byType(DataTable)).headingTextStyle!.color,
      Colors.white,
    );
    expect(t.widget<Icon>(find.byIcon(Icons.people)).color, Colors.white);
    expect(t.takeException(), isNull);
  });

  test('Paleta das telas segue o papel claro e verde da referência', () {
    expect(classic98Palette.background, const Color(0xFFF7F5E9));
    expect(classic98Palette.tableHeader, const Color(0xFF00542E));
    for (final background in [
      classic98Palette.background,
      classic98Palette.surfaceAlternate,
      classic98Palette.tableRow,
      classic98Palette.tableRowAlternate,
      classic98Palette.selectedBackground,
    ]) {
      expect(
        colorContrast(classic98Palette.textPrimary, background),
        greaterThanOrEqualTo(4.5),
      );
    }
    expect(
      colorContrast(Colors.white, classic98Palette.tableHeader),
      greaterThanOrEqualTo(4.5),
    );
  });

  test(
    'Cores antigas são lidas sem alterar cadastro nem inventar verificação',
    () {
      final club = <String, dynamic>{
        'id': 'a',
        'primaryColorHex': '#006400',
        'secondaryColorHex': '#FFFFFF',
        'colorPalette': null,
      };
      final before = jsonEncode(club);
      final result = clubMatchPalette(club)!;
      expect(result['primaryColor'], '#006400');
      expect(result['secondaryColor'], '#FFFFFF');
      expect(result['colorVerified'], isNull);
      expect(result['registeredClubColors'], isTrue);
      expect(jsonEncode(club), before);
      expect(
        clubMatchPalette({
          'primaryColorHex': 'invalid',
          'secondaryColorHex': '#FFFFFF',
        }),
        isNull,
      );
    },
  );

  test(
    'Partidas atuais e calendário consultam o clube por ID e preservam os placares',
    () {
      final clubs = <String, Map<String, dynamic>>{
        'a': {
          'id': 'a',
          'name': 'Mesmo nome',
          'primaryColorHex': '#006400',
          'secondaryColorHex': '#FFFFFF',
        },
        'b': {
          'id': 'b',
          'name': 'Mesmo nome',
          'primaryColorHex': '#FFD700',
          'secondaryColorHex': '#000066',
        },
      };
      final match = <String, dynamic>{
        'matchId': 'm',
        'homeClubId': 'a',
        'awayClubId': 'b',
        'homeGoals': 1,
        'awayGoals': 2,
        'minute': 90,
      };
      final before = jsonEncode([match, clubs]);
      final enriched = matchWithClubColors(match, clubs);
      expect(enriched['homeColorPalette']['primaryColor'], '#006400');
      expect(enriched['awayColorPalette']['primaryColor'], '#FFD700');
      expect(enriched['homeGoals'], 1);
      expect(enriched['awayGoals'], 2);
      expect(enriched['minute'], 90);
      expect(jsonEncode([match, clubs]), before);
      final historical = calendarRoundMatches({
        'competition': 'League',
        'fixtures': [
          {...match, 'id': 'm'},
        ],
      }, clubs).single;
      expect(historical['awayColorPalette']['secondaryColor'], '#000066');
      final unknown = matchWithClubColors({
        ...match,
        'awayClubId': 'missing',
      }, clubs);
      expect(unknown['awayColorPalette'], isNull);
    },
  );

  test(
    'Paleta verificada prevalece sobre campos antigos; cores secundárias prevalecem sobre tinta preferida',
    () {
      final verified = {
        'primaryColor': '#FFD700',
        'secondaryColor': '#000066',
        'preferredTextColor': '#000000',
        'colorVerified': true,
      };
      expect(
        clubMatchPalette({
          'colorPalette': verified,
          'primaryColorHex': '#FF0000',
        }),
        verified,
      );
      final colors = matchTeamColors(classic98Palette, verified);
      expect(colors.background, const Color(0xFFFFD700));
      expect(colors.ink, const Color(0xFF000066));
      final merged = matchWithClubColors(
        {'homeClubId': 'a', 'homeColorPalette': verified},
        {
          'a': {'primaryColorHex': '#006400'},
        },
      );
      expect(merged['homeColorPalette'], verified);
    },
  );

  test(
    'Baixo contraste ajusta apenas a tinta renderizada e mantém as cores salvas',
    () {
      for (final pair in [
        ('#777777', '#888888'),
        ('#0000FF', '#FF0000'),
        ('#FFFFFF', '#FFFFFF'),
      ]) {
        final palette = {
          'primaryColor': pair.$1,
          'secondaryColor': pair.$2,
          'colorVerified': true,
        };
        final before = jsonEncode(palette);
        final colors = matchTeamColors(classic98Palette, palette);
        expect(colors.background, rgbColor(pair.$1));
        expect(
          colorContrast(colors.background, colors.ink),
          greaterThanOrEqualTo(4.5),
        );
        expect(jsonEncode(palette), before);
      }
      final unknown = matchTeamColors(classic98Palette, {
        'primaryColor': '#FF0000',
        'colorVerified': false,
      });
      expect(unknown.background, classic98Palette.surface);
      final neutral = ClassicTheme.derive(
        preferences: {'matchColors': 'Neutral'},
      );
      expect(
        matchTeamColors(neutral, {
          'primaryColor': '#FF0000',
          'colorVerified': true,
        }).background,
        neutral.surface,
      );
    },
  );

  for (final width in [360.0, 390.0, 430.0, 768.0, 1366.0, 1920.0]) {
    testWidgets(
      'Placar e lista usam fundos e textos próprios dos dois times em $width px',
      (t) async {
        t.view.physicalSize = Size(width, 1000);
        t.view.devicePixelRatio = 1;
        addTearDown(t.view.resetPhysicalSize);
        addTearDown(t.view.resetDevicePixelRatio);
        final match = <String, dynamic>{
          'matchId': 'm',
          'homeClubId': 'a',
          'awayClubId': 'b',
          'homeClubName': 'Clube Verde',
          'awayClubName': 'Clube Amarelo',
          'homeGoals': 1,
          'awayGoals': 2,
          'minute': 90,
          'isFinished': true,
          'competitionType': 'League',
          'divisionTier': 1,
        };
        await t.pumpWidget(
          MaterialApp(
            theme: classic98Theme(),
            home: Scaffold(
              body: LiveMatchScreen(
                snapshot: {
                  'gameWorldId': 'w',
                  'roundNumber': 1,
                  'seasonNumber': 1,
                  'roundState': 'RESULTS',
                  'competitionType': 'League',
                  'matches': [match],
                },
                calendar: const [],
                clubs: const {
                  'a': {
                    'id': 'a',
                    'primaryColorHex': '#006400',
                    'secondaryColorHex': '#FFFFFF',
                  },
                  'b': {
                    'id': 'b',
                    'primaryColorHex': '#FFD700',
                    'secondaryColorHex': '#000066',
                  },
                },
                coachId: 'c',
                coachName: 'Treinador',
                coachClubId: null,
                selection: LiveMatchSelection(),
                onSelectionChanged: () {},
              ),
            ),
          ),
        );
        await t.pumpAndSettle();
        Color block(String side) =>
            (t
                        .widget<Container>(
                          find.byKey(ValueKey('selected-$side-team-colors')),
                        )
                        .decoration
                    as BoxDecoration)
                .color!;
        expect(block('home'), const Color(0xFF006400));
        expect(block('away'), const Color(0xFFFFD700));
        for (final finder in [
          find.widgetWithText(OutlinedButton, 'TODOS'),
          find.byKey(const ValueKey('live-detail-tab-0')),
        ]) {
          if (finder.evaluate().isEmpty) continue;
          final button = t.widget<OutlinedButton>(finder);
          expect(
            colorContrast(
              button.style!.foregroundColor!.resolve({})!,
              button.style!.backgroundColor!.resolve({})!,
            ),
            greaterThanOrEqualTo(4.5),
          );
        }
        expect(
          t
              .widget<Text>(find.byKey(const ValueKey('selected-home-name')))
              .style!
              .fontFamily,
          'ClassicCondensed',
        );
        expect(
          t
              .widget<Text>(find.byKey(const ValueKey('selected-home-name')))
              .style!
              .color,
          Colors.white,
        );
        expect(
          t
              .widget<Text>(find.byKey(const ValueKey('selected-away-name')))
              .style!
              .color,
          const Color(0xFF000066),
        );
        expect(t.takeException(), isNull);
        await t.pumpWidget(const SizedBox());
      },
    );
  }
}
