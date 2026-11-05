import 'package:flutter/material.dart';
import 'classic98_widgets.dart';
import 'club_theme.dart';
import 'visual_components.dart';
import 'player_rankings.dart' show formatClassicMoney, ClassicSectionTitle;
import 'stadium_visual.dart';

/// Presentation only: ranking order and all figures come from the server.
class ClassicCoachScreen extends StatefulWidget {
  final List<dynamic> ranking;
  final Widget career;
  final ValueChanged<Map<String, dynamic>> onCoach;
  const ClassicCoachScreen({
    super.key,
    required this.ranking,
    required this.career,
    required this.onCoach,
  });
  @override
  State<ClassicCoachScreen> createState() => _ClassicCoachScreenState();
}

class _ClassicCoachScreenState extends State<ClassicCoachScreen> {
  String _tab = 'ranking';
  bool _complete = false;
  @override
  Widget build(BuildContext context) => SingleChildScrollView(
    key: const Key('coach-screen-scroll'),
    padding: const EdgeInsets.all(12),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        const ClassicSectionTitle(title: 'RANKING & CARREIRA'),
        const SizedBox(height: 10),
        ClassicSelectionTabs(
          keyPrefix: 'coach-tab-',
          items: const [
            ('ranking', 'RANKING DE TREINADORES'),
            ('career', 'CARREIRA DO TREINADOR'),
          ],
          selected: _tab,
          onSelected: (v) => setState(() => _tab = v),
        ),
        const SizedBox(height: 12),
        if (_tab == 'career')
          widget.career
        else ...[
          const Text(
            'Mostra o ranking histórico de treinadores, considerando títulos (campeonatos e taças) e pontos de treinador.',
          ),
          const SizedBox(height: 12),
          LayoutBuilder(
            builder: (context, c) {
              final compact = c.maxWidth < 760;
              final rows = compact && !_complete
                  ? widget.ranking.take(5).toList()
                  : widget.ranking;
              return Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  ClassicPaperPanel(
                    title: 'RANKING CLÁSSICO DE TREINADORES',
                    icon: Icons.emoji_events,
                    iconColor: ClassicTheme.of(context).accent,
                    padding: const EdgeInsets.all(6),
                    child: Builder(
                      builder: (paper) {
                        final t = ClassicTheme.of(paper);
                        return Column(
                          key: const ValueKey('coach-ranking-table'),
                          crossAxisAlignment: CrossAxisAlignment.stretch,
                          children: [
                            if (!compact) ...[
                              Padding(
                                padding: const EdgeInsets.all(8),
                                child: Row(
                                  children: [
                                    Icon(
                                      Icons.info_outline,
                                      size: 17,
                                      color: t.accent,
                                    ),
                                    const SizedBox(width: 6),
                                    const Expanded(
                                      child: Text(
                                        'Ranking calculado por: Campeonatos, Taças e Pontos de Treinador.',
                                      ),
                                    ),
                                  ],
                                ),
                              ),
                              _desktopHeader(t),
                            ],
                            if (rows.isEmpty)
                              const Padding(
                                padding: EdgeInsets.all(24),
                                child: Text(
                                  'Nenhum treinador classificado neste mundo.',
                                ),
                              ),
                            for (var i = 0; i < rows.length; i++)
                              _row(
                                paper,
                                Map<String, dynamic>.from(rows[i] as Map),
                                i,
                                compact,
                              ),
                          ],
                        );
                      },
                    ),
                  ),
                  if (compact && widget.ranking.length > 5) ...[
                    const SizedBox(height: 12),
                    OutlinedButton(
                      key: const Key('coach-ranking-complete'),
                      onPressed: () => setState(() => _complete = !_complete),
                      style: OutlinedButton.styleFrom(
                        minimumSize: const Size(44, 48),
                        foregroundColor: ClassicTheme.of(context).accent,
                        side: BorderSide(
                          color: ClassicTheme.of(context).accent,
                        ),
                      ),
                      child: Text(
                        _complete
                            ? 'Ver resumo do ranking ‹'
                            : 'Ver ranking completo »',
                      ),
                    ),
                  ],
                ],
              );
            },
          ),
        ],
      ],
    ),
  );
  static const _flex = [5, 25, 10, 23, 14, 8, 15, 12];
  Widget _desktopHeader(ClassicTheme t) => Container(
    padding: const EdgeInsets.symmetric(horizontal: 7, vertical: 9),
    color: t.tableHeader,
    child: Row(
      children: [
        for (var i = 0; i < 8; i++)
          Expanded(
            flex: _flex[i],
            child: Text(
              const [
                '#',
                'TREINADOR',
                'TIPO',
                'CLUBE ATUAL',
                'CAMPEONATOS',
                'TAÇAS',
                'PTS TREINADOR',
                'V / E / D',
              ][i],
              textAlign: i == 1 || i == 3 ? TextAlign.left : TextAlign.center,
              style: TextStyle(
                color: readableInk(t.tableHeader, t.textPrimary),
                fontSize: 14,
                fontWeight: FontWeight.bold,
              ),
            ),
          ),
      ],
    ),
  );
  Widget _row(
    BuildContext context,
    Map<String, dynamic> row,
    int i,
    bool compact,
  ) {
    final t = ClassicTheme.of(context);
    final human = row['isHuman'] == true;
    final base = i.isEven ? t.tableRow : t.tableRowAlternate;
    final bg = human ? Color.alphaBlend(const Color(0x3000D9B2), base) : base;
    final ink = readableInk(bg, t.textPrimary);
    final accent = readableOn(const Color(0xFF007D68), bg);
    final name = row['name']?.toString() ?? 'Treinador';
    final club = row['currentClubName']?.toString() ?? 'Sem clube';
    final rank = row['rank']?.toString() ?? '—';
    final badge = Container(
      padding: const EdgeInsets.symmetric(horizontal: 5, vertical: 2),
      decoration: BoxDecoration(
        color: human ? const Color(0xFF00CCAC) : null,
        borderRadius: BorderRadius.circular(5),
      ),
      child: Text(
        human ? 'HUMANO' : 'IA',
        style: TextStyle(
          color: human ? const Color(0xFF002D24) : ink,
          fontSize: 12,
          fontWeight: human ? FontWeight.bold : FontWeight.normal,
        ),
      ),
    );
    final rankCell = Container(
      alignment: Alignment.center,
      padding: const EdgeInsets.symmetric(horizontal: 4, vertical: 3),
      decoration: BoxDecoration(
        color: human ? const Color(0xFF008670) : null,
        borderRadius: BorderRadius.circular(5),
      ),
      child: Text(
        rank,
        style: TextStyle(
          color: human ? Colors.white : ink,
          fontSize: 17,
          fontWeight: FontWeight.bold,
        ),
      ),
    );
    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 2),
      child: Material(
        color: bg,
        borderRadius: BorderRadius.circular(7),
        child: InkWell(
          onTap: () => widget.onCoach(row),
          borderRadius: BorderRadius.circular(7),
          child: Container(
            padding: EdgeInsets.symmetric(
              horizontal: 7,
              vertical: compact ? 9 : 6,
            ),
            decoration: BoxDecoration(
              border: human ? Border.all(color: accent, width: 1.2) : null,
              borderRadius: BorderRadius.circular(7),
            ),
            child: compact
                ? Row(
                    children: [
                      SizedBox(width: 27, child: rankCell),
                      const SizedBox(width: 8),
                      Expanded(
                        child: Column(
                          crossAxisAlignment: CrossAxisAlignment.start,
                          children: [
                            Wrap(
                              spacing: 6,
                              crossAxisAlignment: WrapCrossAlignment.center,
                              children: [
                                Text(
                                  name,
                                  style: TextStyle(
                                    color: ink,
                                    fontSize: 17,
                                    fontWeight: FontWeight.bold,
                                  ),
                                ),
                                badge,
                              ],
                            ),
                            Text(
                              club,
                              style: TextStyle(color: ink, fontSize: 15),
                            ),
                          ],
                        ),
                      ),
                      const SizedBox(width: 6),
                      Container(
                        padding: const EdgeInsets.all(9),
                        decoration: BoxDecoration(
                          color: t.surfaceAlternate,
                          borderRadius: BorderRadius.circular(6),
                        ),
                        child: Text(
                          '${row['coachPoints'] ?? 0} pts',
                          style: TextStyle(
                            color: t.textPrimary,
                            fontSize: 16,
                            fontWeight: FontWeight.bold,
                          ),
                        ),
                      ),
                    ],
                  )
                : Row(
                    children: [
                      for (var n = 0; n < 8; n++)
                        Expanded(
                          flex: _flex[n],
                          child: Padding(
                            padding: const EdgeInsets.symmetric(horizontal: 3),
                            child: n == 0
                                ? rankCell
                                : n == 2
                                ? Align(
                                    alignment: Alignment.center,
                                    child: badge,
                                  )
                                : Text(
                                    [
                                      rank,
                                      name,
                                      '',
                                      club,
                                      '${row['leagueTitles'] ?? 0}',
                                      '${row['cupTitles'] ?? 0}',
                                      '${row['coachPoints'] ?? 0}',
                                      '${row['totalWins'] ?? 0} / ${row['totalDraws'] ?? 0} / ${row['totalLosses'] ?? 0}',
                                    ][n],
                                    textAlign: n == 1 || n == 3
                                        ? TextAlign.left
                                        : TextAlign.center,
                                    style: TextStyle(
                                      color: ink,
                                      fontSize: 17,
                                      fontWeight: n == 1
                                          ? FontWeight.bold
                                          : FontWeight.normal,
                                    ),
                                  ),
                          ),
                        ),
                    ],
                  ),
          ),
        ),
      ),
    );
  }
}

Map<String, num> financeSummaryForRound(
  List<dynamic> ledger,
  int season,
  int round,
) {
  final values = <String, num>{
    for (final k in [
      'income',
      'expenses',
      'tickets',
      'wages',
      'transfers',
      'prizes',
      'stadium',
    ])
      k: 0,
  };
  for (final raw in ledger) {
    final row = raw as Map;
    if (row['seasonNumber'] != season || row['roundNumber'] != round) continue;
    final amount = row['amount'] as num? ?? 0;
    final text = (row['description'] ?? '').toString().toUpperCase();
    final side = amount >= 0 ? 'income' : 'expenses';
    values[side] = values[side]! + amount.abs();
    final category = text.contains('BILHETERIA') || text.contains('INGRESSO')
        ? 'tickets'
        : text.contains('SALÁRIO') || text.contains('FOLHA')
        ? 'wages'
        : text.contains('TRANSFER') ||
              text.contains('COMPRA') ||
              text.contains('VENDA') ||
              text.contains('LEILÃO')
        ? 'transfers'
        : text.contains('PREMIA') ||
              text.contains('ARTILH') ||
              text.contains('TÍTULO') ||
              text.contains('CAMPE')
        ? 'prizes'
        : text.contains('ESTÁDIO') ||
              text.contains('MANUTEN') ||
              text.contains('REFORMA') ||
              text.contains('AMPLIA')
        ? 'stadium'
        : null;
    if (category != null) values[category] = values[category]! + amount;
  }
  return values;
}

class ClassicFinanceView extends StatelessWidget {
  final bool compactClassic, stadiumOnly, bankOnly;
  final Map<String, dynamic> club, data;
  final int season, round;
  final List<Widget> actions;
  final Widget obligations;
  const ClassicFinanceView({
    super.key,
    this.compactClassic = false,
    this.stadiumOnly = false,
    this.bankOnly = false,
    required this.club,
    required this.data,
    required this.season,
    required this.round,
    required this.actions,
    required this.obligations,
  });
  @override
  Widget build(BuildContext context) {
    final ledger = data['ledger'] as List? ?? [];
    final summary = financeSummaryForRound(ledger, season, round);
    final stadium = club['stadium'] as Map? ?? {};
    final t = ClassicTheme.of(context);
    if (compactClassic) {
      if (bankOnly) {
        return SingleChildScrollView(
          key: const Key('classic-bank-screen'),
          padding: const EdgeInsets.all(12),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              const Text(
                'BANCO — EMPRÉSTIMOS',
                textAlign: TextAlign.center,
                style: TextStyle(fontSize: 30, fontWeight: FontWeight.w900),
              ),
              const SizedBox(height: 10),
              RetroSection(
                title: 'Crédito do clube',
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      'Caixa: ${formatClassicMoney(club['cashBalance'] ?? 0)}',
                      style: const TextStyle(
                        fontSize: 26,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                    const Text(
                      'Consulte as condições e os empréstimos em aberto antes de contratar.',
                    ),
                  ],
                ),
              ),
              for (final action in actions.where(
                (a) => a.key == const ValueKey('btn-emprestimo-bancario'),
              ))
                Padding(
                  padding: const EdgeInsets.only(bottom: 12),
                  child: _confirmClassicAction(context, action),
                ),
              Container(
                padding: const EdgeInsets.all(14),
                decoration: BoxDecoration(
                  color: const Color(0xFFE5E4DF),
                  border: Border.all(color: t.textPrimary),
                ),
                child: DefaultTextStyle.merge(
                  style: TextStyle(color: t.textPrimary, fontSize: 17),
                  child: obligations,
                ),
              ),
            ],
          ),
        );
      }
      if (stadiumOnly) {
        final details = Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            RetroSection(
              title: '${stadium['stadiumName'] ?? 'Estádio'}',
              child: Column(
                key: const Key('classic-stadium-details'),
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'Capacidade construída: ${stadium['builtCapacity'] ?? '—'} lugares',
                  ),
                  Text(
                    'Capacidade utilizável: ${stadium['usableCapacity'] ?? '—'} lugares',
                  ),
                  Text(
                    'Condição estrutural: ${stadium['conditionPercent'] ?? '—'}%',
                  ),
                  Text('Último público: ${stadium['lastAttendance'] ?? '—'}'),
                  Text(
                    'Ingresso: ${formatClassicMoney(stadium['lastCalculatedTicketPrice'] ?? 0)}',
                  ),
                  Text(
                    'Manutenção por jogo: ${formatClassicMoney(stadium['fixedCostPerMatch'] ?? 0)}',
                  ),
                  Text(
                    'Caixa: ${formatClassicMoney(club['cashBalance'] ?? 0)}',
                  ),
                ],
              ),
            ),
            const SizedBox(height: 10),
            for (final action in actions.where(
              (a) => a.key != const ValueKey('btn-emprestimo-bancario'),
            ))
              Padding(
                padding: const EdgeInsets.only(bottom: 10),
                child: _confirmClassicAction(context, action),
              ),
            const Text(
              'Novas obras: conclusão após uma rodada. O jogo atual usa a capacidade anterior; o seguinte usa a obra concluída.',
            ),
          ],
        );
        return LayoutBuilder(
          builder: (context, c) => SingleChildScrollView(
            key: const Key('classic-stadium-screen'),
            padding: const EdgeInsets.all(12),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                const Text(
                  'ESTÁDIO',
                  style: TextStyle(fontSize: 24, fontWeight: FontWeight.bold),
                ),
                const SizedBox(height: 10),
                if (c.maxWidth >= 900)
                  Row(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Expanded(
                        flex: 6,
                        child: ConstrainedBox(
                          constraints: const BoxConstraints(maxWidth: 680),
                          child: ClassicStadiumView(stadium: stadium),
                        ),
                      ),
                      const SizedBox(width: 16),
                      Expanded(flex: 4, child: details),
                    ],
                  )
                else ...[
                  ClassicStadiumView(stadium: stadium),
                  const SizedBox(height: 12),
                  details,
                ],
              ],
            ),
          ),
        );
      }
      return SingleChildScrollView(
        padding: const EdgeInsets.all(12),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Text(
              stadiumOnly ? 'ESTÁDIO' : 'FINANÇAS',
              style: const TextStyle(fontSize: 22, fontWeight: FontWeight.bold),
            ),
            const SizedBox(height: 10),
            if (stadiumOnly) ...[
              ClassicStadiumView(stadium: stadium),
              const SizedBox(height: 12),
            ],
            RetroSection(
              title: '${stadium['stadiumName'] ?? 'Estádio'}',
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'Capacidade construída: ${stadium['builtCapacity'] ?? '—'} lugares',
                  ),
                  Text(
                    'Capacidade utilizável: ${stadium['usableCapacity'] ?? '—'} lugares',
                  ),
                  Text(
                    'Condição estrutural: ${stadium['conditionPercent'] ?? '—'}%',
                  ),
                  Text('Último público: ${stadium['lastAttendance'] ?? '—'}'),
                  Text(
                    'Ingresso: ${formatClassicMoney(stadium['lastCalculatedTicketPrice'] ?? 0)}',
                  ),
                  Text(
                    'Manutenção por jogo: ${formatClassicMoney(stadium['fixedCostPerMatch'] ?? 0)}',
                  ),
                  Text(
                    'Caixa: ${formatClassicMoney(club['cashBalance'] ?? 0)}',
                  ),
                ],
              ),
            ),
            for (final action in actions.where(
              (a) =>
                  !stadiumOnly ||
                  a.key != const ValueKey('btn-emprestimo-bancario'),
            ))
              Padding(
                padding: const EdgeInsets.only(bottom: 8),
                child: _confirmClassicAction(context, action),
              ),
            if (!stadiumOnly) ...[
              RetroSection(
                title: 'Resumo da rodada $round',
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    for (final key in [
                      'income',
                      'expenses',
                      'tickets',
                      'wages',
                      'transfers',
                      'prizes',
                      'stadium',
                    ])
                      Text(
                        '${const {'income': 'Receitas', 'expenses': 'Despesas', 'tickets': 'Bilheteria', 'wages': 'Salários', 'transfers': 'Transferências', 'prizes': 'Premiações', 'stadium': 'Estádio'}[key]}: ${formatClassicMoney(summary[key])}',
                      ),
                  ],
                ),
              ),
              obligations,
            ],
          ],
        ),
      );
    }
    return SingleChildScrollView(
      key: const Key('finance-screen-scroll'),
      padding: const EdgeInsets.all(12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const ClassicSectionTitle(
            title: 'FINANÇAS & ESTÁDIO',
            subtitle: 'Caixa, contratos, crédito e patrimônio do clube.',
          ),
          Container(
            margin: const EdgeInsets.only(top: 8, bottom: 14),
            height: 2,
            color: t.accent,
          ),
          ClassicPaperPanel(
            title: 'RESUMO FINANCEIRO CONSOLIDADO DO CLUBE',
            icon: Icons.toll,
            padding: const EdgeInsets.all(8),
            child: LayoutBuilder(
              builder: (context, c) {
                final columns = c.maxWidth >= 1000
                    ? 8
                    : c.maxWidth >= 620
                    ? 4
                    : 2;
                final cards = [
                  (
                    'cash',
                    'SALDO TOTAL EM CAIXA',
                    club['cashBalance'] as num? ?? 0,
                  ),
                  ('income', 'TOTAL RECEBIDO NESTA RODADA', summary['income']!),
                  (
                    'expenses',
                    'TOTAL DESPESAS NESTA RODADA',
                    summary['expenses']!,
                  ),
                  ('tickets', 'BILHETERIA', summary['tickets']!),
                  ('wages', 'SALÁRIOS', summary['wages']!),
                  ('transfers', 'TRANSFERÊNCIAS', summary['transfers']!),
                  ('prizes', 'PREMIAÇÕES', summary['prizes']!),
                  ('stadium', 'CUSTO DO ESTÁDIO', summary['stadium']!),
                ];
                return Wrap(
                  spacing: 6,
                  runSpacing: 6,
                  children: [
                    for (final item in cards)
                      SizedBox(
                        width: (c.maxWidth - (columns - 1) * 6) / columns,
                        child: _metric(context, item.$1, item.$2, item.$3),
                      ),
                  ],
                );
              },
            ),
          ),
          const SizedBox(height: 12),
          ClassicPaperPanel(
            padding: const EdgeInsets.all(12),
            child: LayoutBuilder(
              builder: (context, c) {
                final information = Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Text(
                      'ESTÁDIO: ${stadium['stadiumName'] ?? 'Não informado'}',
                      style: const TextStyle(
                        fontSize: 25,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                    const Divider(),
                    Wrap(
                      spacing: 16,
                      runSpacing: 4,
                      children: [
                        Text(
                          'Capacidade construída: ${stadium['builtCapacity'] ?? '—'} lugares',
                        ),
                        Text(
                          'Capacidade utilizável: ${stadium['usableCapacity'] ?? '—'} lugares',
                        ),
                        Text(
                          'Condição estrutural: ${stadium['conditionPercent'] ?? '—'}%',
                        ),
                      ],
                    ),
                    const SizedBox(height: 6),
                    Text(
                      'Último público: ${stadium['lastAttendance'] ?? '—'} • Ingresso: ${formatClassicMoney(stadium['lastCalculatedTicketPrice'] ?? 0)} • Custo fixo: ${formatClassicMoney(stadium['fixedCostPerMatch'] ?? 0)} por jogo',
                    ),
                    const SizedBox(height: 12),
                    if (c.maxWidth < 700)
                      Column(
                        crossAxisAlignment: CrossAxisAlignment.stretch,
                        children: [
                          for (final action in actions)
                            Padding(
                              padding: const EdgeInsets.only(bottom: 6),
                              child: action,
                            ),
                        ],
                      )
                    else
                      Wrap(spacing: 8, runSpacing: 8, children: actions),
                  ],
                );
                final illustration = SizedBox(
                  width: 174,
                  height: 122,
                  child: CustomPaint(
                    painter: _StadiumIllustration(t.primary, t.secondary),
                  ),
                );
                return c.maxWidth < 700
                    ? Column(
                        crossAxisAlignment: CrossAxisAlignment.stretch,
                        children: [
                          Center(child: illustration),
                          const SizedBox(height: 12),
                          information,
                        ],
                      )
                    : Row(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          illustration,
                          const SizedBox(width: 18),
                          Expanded(child: information),
                        ],
                      );
              },
            ),
          ),
          const SizedBox(height: 12),
          ClassicPaperPanel(
            title: 'BANCO & CONTRATOS',
            icon: Icons.account_balance_wallet_outlined,
            child: obligations,
          ),
          const SizedBox(height: 12),
          ClassicPaperPanel(
            title: 'LIVRO-CAIXA / AUDITORIA (TRANSAÇÕES POR RODADA)',
            icon: Icons.receipt_long,
            padding: const EdgeInsets.all(6),
            child: LayoutBuilder(
              builder: (context, c) {
                final p = ClassicTheme.of(context);
                return Column(
                  key: const ValueKey('finances-ledger-table'),
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    if (ledger.isEmpty)
                      const Padding(
                        padding: EdgeInsets.all(14),
                        child: Text(
                          'Nenhuma movimentação financeira registrada ainda.',
                        ),
                      ),
                    if (ledger.isNotEmpty && c.maxWidth >= 620)
                      Container(
                        color: p.primary,
                        padding: const EdgeInsets.all(8),
                        child: Row(
                          children: [
                            SizedBox(
                              width: 90,
                              child: Text(
                                'RODADA',
                                style: TextStyle(
                                  color: readableInk(p.primary),
                                  fontWeight: FontWeight.bold,
                                ),
                              ),
                            ),
                            Expanded(
                              child: Text(
                                'DESCRIÇÃO',
                                style: TextStyle(
                                  color: readableInk(p.primary),
                                  fontWeight: FontWeight.bold,
                                ),
                              ),
                            ),
                            Text(
                              'VALOR',
                              style: TextStyle(
                                color: readableInk(p.primary),
                                fontWeight: FontWeight.bold,
                              ),
                            ),
                          ],
                        ),
                      ),
                    for (var i = 0; i < ledger.length; i++)
                      _ledgerRow(
                        context,
                        ledger[i] as Map,
                        i,
                        c.maxWidth < 620,
                      ),
                  ],
                );
              },
            ),
          ),
        ],
      ),
    );
  }

  Widget _confirmClassicAction(BuildContext context, Widget action) {
    if (action is! ButtonStyleButton) return action;
    return ElevatedButton(
      key: action.key,
      style: ElevatedButton.styleFrom(
        minimumSize: const Size(double.infinity, 48),
        backgroundColor: action.key == const ValueKey('btn-reformar-estadio')
            ? const Color(0xFF155A9A)
            : action.key == const ValueKey('btn-ampliar-estadio')
            ? const Color(0xFFF5C400)
            : const Color(0xFF006620),
        foregroundColor: action.key == const ValueKey('btn-ampliar-estadio')
            ? const Color(0xFF152B1D)
            : Colors.white,
        textStyle: const TextStyle(
          fontFamily: 'ClassicCondensed',
          fontWeight: FontWeight.bold,
          fontSize: 18,
        ),
      ),
      onPressed: action.onPressed == null
          ? null
          : () async {
              final confirmed = await showDialog<bool>(
                context: context,
                builder: (ctx) => AlertDialog(
                  title: const Text('Confirmar operação financeira'),
                  content: action.child,
                  actions: [
                    TextButton(
                      onPressed: () => Navigator.pop(ctx, false),
                      child: const Text('CANCELAR'),
                    ),
                    ElevatedButton(
                      onPressed: () => Navigator.pop(ctx, true),
                      child: const Text('CONFIRMAR'),
                    ),
                  ],
                ),
              );
              if (confirmed == true && context.mounted) action.onPressed!();
            },
      child: action.child,
    );
  }

  Widget _metric(BuildContext context, String id, String title, num value) {
    final p = ClassicTheme.of(context);
    final cash = id == 'cash';
    final bg = cash ? const Color(0xFFFFD900) : p.surfaceAlternate;
    return Container(
      key: ValueKey('finance-metric-$id'),
      clipBehavior: Clip.antiAlias,
      decoration: BoxDecoration(
        border: Border.all(color: p.border),
        borderRadius: BorderRadius.circular(6),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Container(
            constraints: const BoxConstraints(minHeight: 44),
            padding: const EdgeInsets.symmetric(horizontal: 5, vertical: 6),
            color: p.primary,
            alignment: Alignment.center,
            child: Text(
              title,
              textAlign: TextAlign.center,
              style: TextStyle(
                color: readableInk(p.primary),
                fontWeight: FontWeight.bold,
                fontSize: 14,
                height: 1.05,
              ),
            ),
          ),
          Container(
            constraints: const BoxConstraints(minHeight: 43),
            alignment: Alignment.center,
            padding: const EdgeInsets.all(6),
            color: bg,
            child: Text(
              formatClassicMoney(value),
              textAlign: TextAlign.center,
              style: TextStyle(
                color: readableInk(bg),
                fontSize: 21,
                fontWeight: FontWeight.bold,
              ),
            ),
          ),
        ],
      ),
    );
  }

  Widget _ledgerRow(BuildContext context, Map row, int i, bool compact) {
    final p = ClassicTheme.of(context);
    final amount = row['amount'] as num? ?? 0;
    final bg = i.isEven ? p.tableRow : p.tableRowAlternate;
    final value = Text(
      '${amount >= 0 ? '+' : ''}${formatClassicMoney(amount)}',
      style: TextStyle(
        color: readableOn(
          amount >= 0 ? const Color(0xFF06722F) : const Color(0xFFC51C23),
          bg,
        ),
        fontWeight: FontWeight.bold,
        fontSize: 18,
      ),
    );
    final date = Text('T${row['seasonNumber']} R${row['roundNumber']}');
    final description = Text(
      '${row['description'] ?? ''}',
      style: const TextStyle(fontSize: 17),
    );
    return Container(
      color: bg,
      padding: const EdgeInsets.all(9),
      child: compact
          ? Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                Wrap(
                  alignment: WrapAlignment.spaceBetween,
                  runSpacing: 4,
                  children: [date, value],
                ),
                const SizedBox(height: 4),
                description,
              ],
            )
          : Row(
              children: [
                SizedBox(width: 90, child: date),
                Expanded(child: description),
                const SizedBox(width: 10),
                value,
              ],
            ),
    );
  }
}

class _StadiumIllustration extends CustomPainter {
  final Color primary, secondary;
  _StadiumIllustration(this.primary, this.secondary);
  @override
  void paint(Canvas canvas, Size size) {
    final bounds = Offset.zero & size;
    canvas.drawRRect(
      RRect.fromRectAndRadius(bounds, const Radius.circular(9)),
      Paint()
        ..shader = LinearGradient(
          colors: [primary, secondary],
        ).createShader(bounds),
    );
    for (var i = 0; i < 6; i++) {
      canvas.drawOval(
        Rect.fromLTWH(
          9 + i * 3,
          23 + i * 3,
          size.width - 18 - i * 6,
          85 - i * 9,
        ),
        Paint()
          ..color = i.isEven
              ? const Color(0xFFC3D4CB)
              : const Color(0xFF608C73),
      );
    }
    final field = Rect.fromLTWH(35, 47, size.width - 70, 43);
    canvas.drawRect(field, Paint()..color = const Color(0xFF3D9C49));
    final line = Paint()
      ..color = const Color(0xFFE5F0DC)
      ..style = PaintingStyle.stroke
      ..strokeWidth = 1;
    canvas.drawRect(field.deflate(3), line);
    canvas.drawLine(
      Offset(size.width / 2, 50),
      Offset(size.width / 2, 87),
      line,
    );
    canvas.drawOval(
      Rect.fromCenter(center: field.center, width: 19, height: 15),
      line,
    );
    for (final x in [25.0, size.width - 25]) {
      canvas.drawLine(Offset(x, 12), Offset(x, 42), line);
      canvas.drawRect(
        Rect.fromLTWH(x - 9, 9, 18, 8),
        Paint()..color = const Color(0xFFE9F4DD),
      );
    }
  }

  @override
  bool shouldRepaint(covariant _StadiumIllustration old) =>
      old.primary != primary || old.secondary != secondary;
}
