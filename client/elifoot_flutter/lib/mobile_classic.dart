import 'package:flutter/material.dart';
import 'club_theme.dart';
import 'club_crest.dart';
import 'morale_bar.dart';
import 'squad_classic.dart';
import 'visual_components.dart' show ClassicPaper;

/// Light roster surfaces are shared by all mobile squad lists.
abstract final class MobileClassicColors {
  static const surface = Color(0xFFF5F7F4);
  static const alternate = Color(0xFFE3E9E5);
  static const border = Color(0xFF9CAF9F);
  static const ink = Color(0xFF10271B);
  static const muted = Color(0xFF47604F);
  static const fit = Color(0xFF087B25);
  static const unavailable = Color(0xFFB82F1B);
  static Color position(String value) => switch (value) {
    'G' => const Color(0xFFAD5B00),
    'D' => const Color(0xFF087825),
    'M' => const Color(0xFF0858C7),
    'A' => const Color(0xFFB61922),
    _ => muted,
  };
}

class MobileClubOverview extends StatelessWidget {
  final String club, coach, division, standing, cash, opponent;
  final num morale;
  final String moraleBand;
  final Color primary, secondary;
  const MobileClubOverview({
    super.key,
    required this.club,
    required this.coach,
    required this.division,
    required this.standing,
    required this.cash,
    required this.opponent,
    required this.morale,
    required this.moraleBand,
    required this.primary,
    required this.secondary,
  });
  Widget _stat(
    IconData icon,
    String? title,
    String? value, {
    Widget? content,
  }) => Container(
    height: 78,
    padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 6),
    decoration: BoxDecoration(
      color: MobileClassicColors.surface,
      border: Border.all(color: MobileClassicColors.border),
      borderRadius: BorderRadius.circular(3),
    ),
    child: Column(
      mainAxisAlignment: MainAxisAlignment.center,
      children: [
        Icon(icon, size: 20, color: const Color(0xFF006B36)),
        if (title != null)
          Text(
            title,
            textAlign: TextAlign.center,
            maxLines: 1,
            overflow: TextOverflow.ellipsis,
            style: const TextStyle(
              color: MobileClassicColors.ink,
              fontSize: 10,
              fontWeight: FontWeight.w800,
            ),
          ),
        if (value != null)
          Text(
            value,
            textAlign: TextAlign.center,
            maxLines: 2,
            overflow: TextOverflow.ellipsis,
            style: const TextStyle(
              color: MobileClassicColors.ink,
              fontSize: 10,
            ),
          ),
        if (content != null)
          Padding(padding: const EdgeInsets.only(top: 6), child: content),
      ],
    ),
  );
  @override
  Widget build(BuildContext context) => Container(
    key: const Key('mobile-club-overview'),
    margin: const EdgeInsets.fromLTRB(8, 6, 8, 4),
    padding: const EdgeInsets.all(5),
    decoration: BoxDecoration(
      color: MobileClassicColors.alternate,
      border: Border.all(color: MobileClassicColors.border),
      borderRadius: BorderRadius.circular(5),
    ),
    child: Column(
      children: [
        Container(
          padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 8),
          decoration: BoxDecoration(
            color: MobileClassicColors.surface,
            border: Border.all(color: MobileClassicColors.border),
            borderRadius: BorderRadius.circular(3),
          ),
          child: Row(
            children: [
              ClubCrest(
                club: club,
                size: 44,
                palette: {
                  'primaryColor':
                      '#${primary.toARGB32().toRadixString(16).substring(2)}',
                  'secondaryColor':
                      '#${secondary.toARGB32().toRadixString(16).substring(2)}',
                },
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      club,
                      maxLines: 2,
                      overflow: TextOverflow.ellipsis,
                      style: const TextStyle(
                        fontSize: 20,
                        fontWeight: FontWeight.w900,
                        color: MobileClassicColors.ink,
                      ),
                    ),
                    Text(
                      coach,
                      style: const TextStyle(
                        fontSize: 12,
                        color: MobileClassicColors.muted,
                      ),
                    ),
                  ],
                ),
              ),
            ],
          ),
        ),
        const SizedBox(height: 4),
        Row(
          children: [
            Expanded(
              child: _stat(Icons.emoji_events_outlined, division, standing),
            ),
            const SizedBox(width: 3),
            Expanded(
              child: _stat(
                Icons.sentiment_satisfied_alt,
                null,
                null,
                content: SizedBox(
                  width: double.infinity,
                  child: FittedBox(
                    fit: BoxFit.scaleDown,
                    child: MoraleBar(
                      key: const ValueKey('coach-morale'),
                      value: morale,
                      band: moraleBand,
                    ),
                  ),
                ),
              ),
            ),
            const SizedBox(width: 3),
            Expanded(
              child: _stat(
                Icons.account_balance_wallet_outlined,
                'Caixa',
                cash,
              ),
            ),
            const SizedBox(width: 3),
            Expanded(
              child: _stat(Icons.sports_soccer, 'Próximo jogo', opponent),
            ),
          ],
        ),
      ],
    ),
  );
}

class MobileClassicNavigation extends StatelessWidget {
  final Widget? child;
  final int selected;
  final bool locked;
  final bool liveMode;
  final ValueChanged<int> onSelected;
  const MobileClassicNavigation({
    super.key,
    this.child,
    required this.selected,
    required this.locked,
    this.liveMode = false,
    required this.onSelected,
  });
  @override
  Widget build(BuildContext context) {
    if (child != null) return child!;
    final theme = ClassicTheme.of(context);
    final items = liveMode
        ? [
            ('PLANTEL', Icons.checkroom),
            ('JOGOS', Icons.sports_soccer),
            ('DIVISÕES', Icons.emoji_events),
            ('MERCADO', Icons.swap_horiz),
            ('MAIS', Icons.more_horiz),
          ]
        : [
            ('PLANTEL', Icons.checkroom),
            ('TÁTICAS', Icons.account_tree),
            ('MERCADO', Icons.swap_horiz),
            ('COMPETIÇÕES', Icons.emoji_events),
            ('MAIS', Icons.settings),
          ];
    return Material(
      key: const Key('mobile-bottom-navigation'),
      color: theme.secondary,
      child: SafeArea(
        top: false,
        child: Container(
          height: 68,
          decoration: BoxDecoration(
            border: Border(top: BorderSide(color: theme.border)),
          ),
          child: Row(
            children: [
              for (int i = 0; i < items.length; i++)
                Expanded(
                  child: Semantics(
                    button: true,
                    selected: selected == i,
                    enabled: !locked,
                    child: InkWell(
                      key: i == 4 && !locked
                          ? const Key('open-navigation')
                          : ValueKey('mobile-nav-$i'),
                      onTap: locked ? null : () => onSelected(i),
                      child: Container(
                        decoration: BoxDecoration(
                          border: Border(
                            bottom: BorderSide(
                              color: selected == i
                                  ? theme.accent
                                  : Colors.transparent,
                              width: 3,
                            ),
                          ),
                        ),
                        padding: const EdgeInsets.symmetric(
                          horizontal: 2,
                          vertical: 6,
                        ),
                        child: Column(
                          mainAxisAlignment: MainAxisAlignment.center,
                          children: [
                            Icon(
                              items[i].$2,
                              size: 25,
                              color: selected == i
                                  ? theme.accent
                                  : theme.textSecondary,
                            ),
                            const SizedBox(height: 3),
                            FittedBox(
                              fit: BoxFit.scaleDown,
                              child: Text(
                                items[i].$1,
                                style: TextStyle(
                                  fontSize: 10,
                                  fontWeight: FontWeight.w800,
                                  color: selected == i
                                      ? theme.accent
                                      : theme.textSecondary,
                                ),
                              ),
                            ),
                          ],
                        ),
                      ),
                    ),
                  ),
                ),
            ],
          ),
        ),
      ),
    );
  }
}

class MobileLightPanel extends StatelessWidget {
  final String title;
  final Widget child;
  final Widget? trailing;
  const MobileLightPanel({
    super.key,
    required this.title,
    required this.child,
    this.trailing,
  });
  @override
  Widget build(BuildContext context) {
    final t = ClassicTheme.of(context);
    return Container(
      margin: const EdgeInsets.only(bottom: 8),
      clipBehavior: Clip.antiAlias,
      decoration: BoxDecoration(
        color: MobileClassicColors.surface,
        border: Border.all(color: MobileClassicColors.border),
        borderRadius: BorderRadius.circular(4),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 9, vertical: 6),
            decoration: BoxDecoration(
              gradient: LinearGradient(colors: [t.primary, t.secondary]),
            ),
            child: Row(
              children: [
                Expanded(
                  child: Text(
                    title,
                    style: TextStyle(
                      color: t.textPrimary,
                      fontWeight: FontWeight.w800,
                      fontSize: 18,
                    ),
                  ),
                ),
                ?trailing,
              ],
            ),
          ),
          ClassicPaper(builder: (_) => child),
        ],
      ),
    );
  }
}

class MobileSquadScreen extends StatelessWidget {
  final List<Map<String, dynamic>> squad, filteredSquad;
  final List<String> starters, bench, formations;
  final String formation;
  final int requiredStarterCount;
  final bool locked, busy, canManage, tactics;
  final ValueChanged<String> onFormation;
  final VoidCallback onAutoPick;
  final ValueChanged<Map<String, dynamic>> onDetail,
      onToggle,
      onRenew,
      onAnnounce,
      onAuction;
  final ValueChanged<int> onTab;
  final Widget field, filters, coachPanel;
  final Widget? rosterFilter;
  const MobileSquadScreen({
    super.key,
    required this.squad,
    required this.filteredSquad,
    required this.starters,
    required this.bench,
    required this.formations,
    required this.formation,
    required this.requiredStarterCount,
    required this.locked,
    required this.busy,
    required this.canManage,
    required this.tactics,
    required this.onFormation,
    required this.onAutoPick,
    required this.onDetail,
    required this.onToggle,
    required this.onRenew,
    required this.onAnnounce,
    required this.onAuction,
    required this.onTab,
    required this.field,
    required this.filters,
    required this.coachPanel,
    this.rosterFilter,
  });
  String _pos(Map<String, dynamic> p) {
    final raw = p['position'];
    return raw is num
        ? const ['G', 'D', 'M', 'A'][raw.toInt().clamp(0, 3)]
        : raw?.toString() ?? '';
  }

  String _condition(Map<String, dynamic> p) =>
      (p['injuryDaysRemaining'] as num? ?? 0) > 0
      ? 'LES ${p['injuryDaysRemaining']}d'
      : (p['injuryRoundsRemaining'] as num? ?? 0) > 0
      ? 'LES ${p['injuryRoundsRemaining']}r'
      : (p['suspensionRoundsRemaining'] as num? ?? 0) > 0
      ? 'SUS ${p['suspensionRoundsRemaining']}r'
      : 'APTO';
  String _name(Map<String, dynamic> p) =>
      '${p['displayName'] ?? p['name'] ?? '—'}';
  Widget _position(String p) => Container(
    width: 23,
    padding: const EdgeInsets.symmetric(vertical: 3),
    decoration: BoxDecoration(
      color: MobileClassicColors.position(p),
      borderRadius: BorderRadius.circular(3),
    ),
    child: Text(
      p,
      textAlign: TextAlign.center,
      style: const TextStyle(
        color: Colors.white,
        fontSize: 11,
        fontWeight: FontWeight.w800,
      ),
    ),
  );
  Widget _lineup(
    BuildContext context,
    String title,
    List<Map<String, dynamic>> players,
  ) => MobileLightPanel(
    title: '$title (${players.length})',
    child: Column(
      children: [
        Container(
          height: 27,
          color: MobileClassicColors.alternate,
          padding: const EdgeInsets.symmetric(horizontal: 6),
          child: const Row(
            children: [
              SizedBox(width: 29, child: Text('POS', style: _head)),
              Expanded(child: Text('JOGADOR', style: _head)),
              SizedBox(width: 35, child: Text('FORÇA', style: _head)),
              SizedBox(width: 53, child: Text('COND.', style: _head)),
              SizedBox(width: 36),
            ],
          ),
        ),
        if (players.isEmpty)
          const Padding(
            padding: EdgeInsets.all(12),
            child: Text(
              'Nenhum jogador selecionado.',
              style: TextStyle(color: MobileClassicColors.muted),
            ),
          ),
        for (int i = 0; i < players.length; i++)
          Container(
            color: i.isEven
                ? MobileClassicColors.surface
                : MobileClassicColors.alternate,
            padding: const EdgeInsets.symmetric(horizontal: 6),
            height: 42,
            child: Row(
              children: [
                _position(_pos(players[i])),
                const SizedBox(width: 6),
                Expanded(
                  child: Text(
                    _name(players[i]),
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: const TextStyle(
                      color: MobileClassicColors.ink,
                      fontSize: 14,
                      fontWeight: FontWeight.w600,
                    ),
                  ),
                ),
                SizedBox(
                  width: 35,
                  child: Text(
                    '${players[i]['strength'] ?? '—'}',
                    style: const TextStyle(
                      color: MobileClassicColors.ink,
                      fontWeight: FontWeight.bold,
                      fontSize: 12,
                    ),
                  ),
                ),
                SizedBox(
                  width: 53,
                  child: Text(
                    _condition(players[i]),
                    style: TextStyle(
                      fontSize: 10,
                      fontWeight: FontWeight.bold,
                      color: _condition(players[i]) == 'APTO'
                          ? MobileClassicColors.fit
                          : MobileClassicColors.unavailable,
                    ),
                  ),
                ),
                SizedBox(
                  width: 36,
                  child: IconButton(
                    padding: EdgeInsets.zero,
                    tooltip: 'Ficha de ${_name(players[i])}',
                    onPressed: () => onDetail(players[i]),
                    icon: const Icon(
                      Icons.bar_chart,
                      size: 20,
                      color: MobileClassicColors.ink,
                    ),
                  ),
                ),
              ],
            ),
          ),
      ],
    ),
  );
  static const _head = TextStyle(
    color: MobileClassicColors.ink,
    fontSize: 9,
    fontWeight: FontWeight.w800,
  );
  Widget _metric(String label, String value) => Expanded(
    child: Container(
      height: 64,
      padding: const EdgeInsets.symmetric(horizontal: 3, vertical: 8),
      decoration: const BoxDecoration(
        border: Border(left: BorderSide(color: MobileClassicColors.border)),
      ),
      child: Column(
        children: [
          FittedBox(
            fit: BoxFit.scaleDown,
            child: Text(label, style: _head),
          ),
          const SizedBox(height: 6),
          FittedBox(
            fit: BoxFit.scaleDown,
            child: Text(
              value,
              style: const TextStyle(
                color: MobileClassicColors.ink,
                fontSize: 17,
                fontWeight: FontWeight.w800,
              ),
            ),
          ),
        ],
      ),
    ),
  );
  @override
  Widget build(BuildContext context) {
    final selected = squad
        .where((p) => starters.contains('${p['id']}'))
        .toList();
    final reserves = squad.where((p) => bench.contains('${p['id']}')).toList();
    final t = ClassicTheme.of(context);
    return SingleChildScrollView(
      key: ValueKey(tactics ? 'mobile-tactics-scroll' : 'mobile-squad-scroll'),
      padding: const EdgeInsets.fromLTRB(8, 4, 8, 12),
      child: Column(
        key: const Key('mobile-squad-content'),
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Container(
            key: const Key('mobile-squad-screen'),
            margin: const EdgeInsets.only(bottom: 8),
            child: Row(
              children: [
                for (final entry in [
                  (0, 'PLANTEL', Icons.checkroom),
                  (1, 'TÁTICAS ($formation)', Icons.account_tree),
                  (2, 'MERCADO', Icons.swap_horiz),
                  (3, 'CLASSIFICAÇÃO', Icons.emoji_events_outlined),
                ])
                  Expanded(
                    child: Padding(
                      padding: const EdgeInsets.only(right: 3),
                      child: OutlinedButton(
                        key: ValueKey('mobile-squad-tab-${entry.$1}'),
                        onPressed: () => onTab(entry.$1),
                        style: OutlinedButton.styleFrom(
                          backgroundColor: (tactics ? 1 : 0) == entry.$1
                              ? t.accent
                              : t.primary,
                          foregroundColor: (tactics ? 1 : 0) == entry.$1
                              ? t.selectedText
                              : t.textPrimary,
                          minimumSize: const Size(0, 44),
                          padding: const EdgeInsets.symmetric(horizontal: 3),
                        ),
                        child: Column(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            Icon(entry.$3, size: 18),
                            FittedBox(
                              fit: BoxFit.scaleDown,
                              child: Text(
                                entry.$2,
                                style: const TextStyle(
                                  fontSize: 10,
                                  fontWeight: FontWeight.w800,
                                ),
                              ),
                            ),
                          ],
                        ),
                      ),
                    ),
                  ),
              ],
            ),
          ),
          MobileLightPanel(
            title: 'RESUMO TÁTICO',
            child: Column(
              children: [
                Row(
                  children: [
                    Expanded(
                      child: Padding(
                        padding: const EdgeInsets.symmetric(
                          horizontal: 6,
                          vertical: 4,
                        ),
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            const Text('ESQUEMA', style: _head),
                            DropdownButton<String>(
                              key: const ValueKey('dropdown-formation'),
                              value: formation,
                              isExpanded: true,
                              dropdownColor: t.surface,
                              style: TextStyle(
                                fontFamily: 'Roboto',
                                color: t.accent,
                                fontSize: 13,
                                fontWeight: FontWeight.bold,
                              ),
                              icon: Icon(
                                Icons.arrow_drop_down,
                                color: MobileClassicColors.ink,
                              ),
                              selectedItemBuilder: (context) => formations
                                  .map(
                                    (f) => Align(
                                      alignment: Alignment.centerLeft,
                                      child: Text(
                                        f,
                                        style: const TextStyle(
                                          fontFamily: 'Roboto',
                                          color: MobileClassicColors.ink,
                                          fontWeight: FontWeight.w800,
                                          fontSize: 14,
                                        ),
                                      ),
                                    ),
                                  )
                                  .toList(),
                              items: formations
                                  .map(
                                    (f) => DropdownMenuItem(
                                      value: f,
                                      child: Text(f),
                                    ),
                                  )
                                  .toList(),
                              onChanged: locked
                                  ? null
                                  : (f) {
                                      if (f != null) onFormation(f);
                                    },
                            ),
                          ],
                        ),
                      ),
                    ),
                    _metric('TITULARES', '${starters.length}/11'),
                    _metric('BANCO', '${bench.length}/9'),
                    _metric('ELENCO', '${squad.length}/24'),
                  ],
                ),
                Padding(
                  padding: const EdgeInsets.fromLTRB(6, 0, 6, 4),
                  child: Align(
                    alignment: Alignment.centerRight,
                    child: OutlinedButton(
                      key: const ValueKey('btn-auto-escalar'),
                      onPressed: locked ? null : onAutoPick,
                      style: OutlinedButton.styleFrom(
                        foregroundColor: MobileClassicColors.ink,
                        minimumSize: const Size(0, 36),
                      ),
                      child: const Text(
                        'AUTO-ESCALAR 11 + BANCO',
                        style: TextStyle(fontSize: 10),
                      ),
                    ),
                  ),
                ),
              ],
            ),
          ),
          if (!tactics && starters.length < 11)
            Padding(
              padding: const EdgeInsets.only(bottom: 8),
              child: Text(
                starters.length == requiredStarterCount
                    ? 'EM INFERIORIDADE (${starters.length}/11 APTOS)'
                    : 'ESCALAÇÃO INCOMPLETA (${starters.length}/11)',
                style: TextStyle(color: t.accent, fontWeight: FontWeight.bold),
              ),
            ),
          if (tactics) ...[field, const SizedBox(height: 8)],
          _lineup(context, 'TITULARES', selected),
          _lineup(context, 'BANCO', reserves),
          MobileLightPanel(
            title: 'PLANTEL DE JOGADORES (${squad.length})',
            trailing: rosterFilter == null
                ? null
                : Container(
                    color: MobileClassicColors.surface,
                    padding: const EdgeInsets.symmetric(horizontal: 5),
                    child: rosterFilter,
                  ),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Theme(
                  data: Theme.of(context).copyWith(
                    textTheme: Theme.of(
                      context,
                    ).textTheme.apply(bodyColor: MobileClassicColors.ink),
                    expansionTileTheme: const ExpansionTileThemeData(
                      textColor: MobileClassicColors.ink,
                      collapsedTextColor: MobileClassicColors.ink,
                      iconColor: MobileClassicColors.ink,
                      collapsedIconColor: MobileClassicColors.ink,
                    ),
                  ),
                  child: ExpansionTile(
                    key: const Key('mobile-squad-filters'),
                    tilePadding: const EdgeInsets.symmetric(horizontal: 8),
                    title: const Text(
                      'FILTRAR PLANTEL E ORDENAR',
                      style: _head,
                    ),
                    children: [
                      Container(
                        color: t.surface,
                        padding: const EdgeInsets.all(8),
                        child: filters,
                      ),
                    ],
                  ),
                ),
                ClassicSquadRoster(
                  players: filteredSquad,
                  starters: starters,
                  bench: bench,
                  compact: true,
                  locked: locked,
                  busy: busy,
                  canManage: canManage,
                  onToggle: onToggle,
                  onDetail: onDetail,
                  onRenew: onRenew,
                  onAuction: onAuction,
                ),
              ],
            ),
          ),
          ExpansionTile(
            key: const Key('mobile-coach-details'),
            tilePadding: const EdgeInsets.symmetric(horizontal: 4),
            title: Text(
              'INFORMAÇÕES DO TREINADOR',
              style: TextStyle(color: t.textSecondary, fontSize: 11),
            ),
            children: [coachPanel],
          ),
        ],
      ),
    );
  }
}
