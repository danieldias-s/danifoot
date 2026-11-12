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
  final ValueChanged<String>? onRepayLoan;
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
    this.onRepayLoan,
  });
  @override
  Widget build(BuildContext context) {
    final ledger = data['ledger'] as List? ?? [];
    final summary = financeSummaryForRound(ledger, season, round);
    final stadium = club['stadium'] as Map? ?? {};
    final t = ClassicTheme.of(context);
    if (compactClassic) {
      if (bankOnly) {
        return _buildClassicBankScreen(context);
      }
      if (stadiumOnly) {
        return _buildClassicStadiumScreen(context, stadium);
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

  static const Color _bankGreen = Color(0xFF006822);
  static const Color _bankRed = Color(0xFFB8141A);

  String _classicLoanInterestText(Map loan) {
    if (loan['mode'] == 'ClassicFixed') {
      return '${formatClassicMoney(loan['interestPerMatch'] ?? 0)} por partida';
    }
    final rate = ((loan['interestRatePerRound'] as num?) ?? 0) * 100;
    return '${rate.toStringAsFixed(2).replaceAll('.', ',')}% sobre o saldo; prestação ${formatClassicMoney(loan['installmentPerRound'] ?? 0)}';
  }

  Widget _buildClassicBankScreen(BuildContext context) {
    return LayoutBuilder(
      builder: (context, constraints) {
        final mobile = constraints.maxWidth < 640;
        final cash =
            (data['availableCash'] as num?) ??
            (club['cashBalance'] as num?) ??
            0;
        final availableCash = cash;
        final credit = data['credit'] is Map
            ? Map<String, dynamic>.from(data['credit'] as Map)
            : const <String, dynamic>{};
        final activeLoans = (data['loans'] as List? ?? const [])
            .whereType<Map>()
            .where((l) => l['isPaidOff'] != true)
            .map((l) => Map<String, dynamic>.from(l))
            .toList();
        final activeLoanCount = credit['activeLoans'] ?? activeLoans.length;
        final loanLimit = credit['loanLimit'] ?? 2;
        final availableLoanCount =
            credit['availableLoans'] ??
            ((loanLimit is num && activeLoanCount is num)
                ? (loanLimit - activeLoanCount).clamp(0, loanLimit)
                : 0);
        final outstandingDebt = (credit['outstandingDebt'] as num?) ?? 0;
        final interestPerMatch = (credit['interestPerMatch'] as num?) ?? 0;
        final sponsorshipBudget =
            (data['annualSponsorshipBudget'] as num?) ??
            (club['annualSponsorshipBudget'] as num?) ??
            72000;
        final sponsorshipInstallments =
            (data['sponsorshipInstallments'] as num?) ??
            (club['sponsorshipInstallments'] as num?) ??
            21;
        final negCash = data['negativeCash'] is Map
            ? Map<String, dynamic>.from(data['negativeCash'] as Map)
            : const <String, dynamic>{};
        final negPrincipal =
            (negCash['principal'] as num?) ??
            (availableCash < 0 ? availableCash.abs() : 0);
        final negEstimate =
            (negCash['estimatedNextRoundInterest'] as num?) ?? 0;

        final summaryRows = <(Widget, String, Widget)>[
          (
            Icon(
              Icons.handshake_outlined,
              size: mobile ? 18 : 21,
              color: ClassicColors.rosterBlack,
            ),
            'Patrocínio anual:',
            Text(
              '${formatClassicMoney(sponsorshipBudget)} em $sponsorshipInstallments parcelas',
              style: TextStyle(
                fontFamily: ClassicTypography.fontFamily,
                fontSize: mobile ? 14.5 : 17,
                fontWeight: FontWeight.w600,
                color: ClassicColors.rosterBlack,
              ),
            ),
          ),
          (
            Icon(
              Icons.groups,
              size: mobile ? 18 : 21,
              color: ClassicColors.rosterBlack,
            ),
            'Folha salarial:',
            Text(
              'por partida oficial disputada',
              style: TextStyle(
                fontFamily: ClassicTypography.fontFamily,
                fontSize: mobile ? 14.5 : 16.5,
                fontWeight: FontWeight.w500,
                color: ClassicColors.rosterBlack,
              ),
            ),
          ),
          (
            _ClassicBanknoteIcon(color: _bankGreen, size: mobile ? 19 : 23),
            'Saldo disponível:',
            Text(
              formatClassicMoney(availableCash),
              style: TextStyle(
                fontFamily: ClassicTypography.fontFamily,
                fontSize: mobile ? 15 : 17.5,
                fontWeight: FontWeight.w900,
                color: availableCash < 0 ? _bankRed : _bankGreen,
              ),
            ),
          ),
          (
            _ClassicCoinsIcon(
              color: ClassicColors.rosterBlack,
              size: mobile ? 18 : 21,
            ),
            'Crédito individual:',
            Text(
              '$activeLoanCount/$loanLimit empréstimos; disponíveis: $availableLoanCount',
              style: TextStyle(
                fontFamily: ClassicTypography.fontFamily,
                fontSize: mobile ? 14.5 : 16.5,
                fontWeight: FontWeight.w500,
                color: ClassicColors.rosterBlack,
              ),
            ),
          ),
          (
            _ClassicCoinsIcon(color: _bankRed, size: mobile ? 18 : 21),
            'Dívida bancária:',
            Text(
              formatClassicMoney(outstandingDebt),
              style: TextStyle(
                fontFamily: ClassicTypography.fontFamily,
                fontSize: mobile ? 15 : 17.5,
                fontWeight: FontWeight.w900,
                color: _bankRed,
              ),
            ),
          ),
          (
            Text(
              '%',
              style: TextStyle(
                fontFamily: ClassicTypography.fontFamily,
                fontSize: mobile ? 17 : 20,
                fontWeight: FontWeight.w900,
                color: _bankRed,
                height: 1,
              ),
            ),
            'Juros clássicos por partida:',
            Text(
              formatClassicMoney(interestPerMatch),
              style: TextStyle(
                fontFamily: ClassicTypography.fontFamily,
                fontSize: mobile ? 15 : 17.5,
                fontWeight: FontWeight.w900,
                color: _bankRed,
              ),
            ),
          ),
          (
            _ClassicCalendarGridIcon(
              color: ClassicColors.rosterBlack,
              size: mobile ? 17 : 20,
            ),
            'Caixa negativo:',
            Text.rich(
              TextSpan(
                style: TextStyle(
                  fontFamily: ClassicTypography.fontFamily,
                  fontSize: mobile ? 14 : 16.5,
                  color: ClassicColors.rosterBlack,
                ),
                children: [
                  TextSpan(
                    text: formatClassicMoney(negPrincipal),
                    style: const TextStyle(
                      color: _bankRed,
                      fontWeight: FontWeight.w900,
                    ),
                  ),
                  const TextSpan(
                    text:
                        ' | Juros: 2% por rodada global concluída; estimativa: ',
                    style: TextStyle(fontWeight: FontWeight.w500),
                  ),
                  TextSpan(
                    text: formatClassicMoney(negEstimate),
                    style: const TextStyle(
                      color: _bankRed,
                      fontWeight: FontWeight.w900,
                    ),
                  ),
                ],
              ),
            ),
          ),
        ];

        return SingleChildScrollView(
          key: const Key('classic-bank-screen'),
          padding: EdgeInsets.symmetric(
            horizontal: mobile ? 8 : 14,
            vertical: mobile ? 8 : 10,
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Text(
                'BANCO — EMPRÉSTIMOS',
                textAlign: TextAlign.center,
                style: TextStyle(
                  fontFamily: ClassicTypography.fontFamily,
                  fontSize: mobile ? 24 : 31,
                  fontWeight: FontWeight.w900,
                  color: ClassicColors.rosterBlack,
                  letterSpacing: 0.3,
                  height: 1.05,
                ),
              ),
              SizedBox(height: mobile ? 8 : 10),
              RetroSection(
                key: const Key('classic-bank-credit-section'),
                title: 'Crédito do clube',
                padding: EdgeInsets.symmetric(
                  horizontal: mobile ? 10 : 14,
                  vertical: mobile ? 10 : 12,
                ),
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.center,
                  children: [
                    Container(
                      width: mobile ? 52 : 64,
                      height: mobile ? 46 : 56,
                      decoration: BoxDecoration(
                        color: const Color(0xFFE6DFCE),
                        borderRadius: BorderRadius.circular(3),
                        border: Border.all(
                          color: const Color(0xFFB5AD9B),
                          width: 1.2,
                        ),
                        boxShadow: const [
                          BoxShadow(
                            color: Color(0x33000000),
                            offset: Offset(0, 1),
                            blurRadius: 1,
                          ),
                        ],
                      ),
                      child: Container(
                        decoration: const BoxDecoration(
                          border: Border(
                            top: BorderSide(
                              color: ClassicColors.bevelLight,
                              width: 1.4,
                            ),
                            left: BorderSide(
                              color: ClassicColors.bevelLight,
                              width: 1.4,
                            ),
                            bottom: BorderSide(
                              color: Color(0xFF9C9482),
                              width: 1.4,
                            ),
                            right: BorderSide(
                              color: Color(0xFF9C9482),
                              width: 1.4,
                            ),
                          ),
                        ),
                        alignment: Alignment.center,
                        child: _ClassicBankIcon(
                          color: ClassicColors.titleBarBlue,
                          size: mobile ? 32 : 40,
                        ),
                      ),
                    ),
                    SizedBox(width: mobile ? 10 : 14),
                    Container(
                      width: 1.2,
                      height: mobile ? 44 : 52,
                      color: ClassicColors.border,
                    ),
                    SizedBox(width: mobile ? 10 : 16),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            'Caixa: ${formatClassicMoney(cash)}',
                            key: const Key('classic-bank-cash-headline'),
                            style: TextStyle(
                              fontFamily: ClassicTypography.fontFamily,
                              fontSize: mobile ? 23 : 31,
                              fontWeight: FontWeight.w900,
                              color: cash < 0 ? _bankRed : _bankGreen,
                              height: 1.02,
                            ),
                          ),
                          const SizedBox(height: 3),
                          Text(
                            'Consulte as condições e os empréstimos em aberto antes de contratar.',
                            style: TextStyle(
                              fontFamily: ClassicTypography.fontFamily,
                              fontSize: mobile ? 13 : 15.5,
                              fontWeight: FontWeight.w500,
                              color: ClassicColors.rosterBlack,
                              height: 1.15,
                            ),
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
              for (final action in actions.where(
                (a) => a.key == const ValueKey('btn-emprestimo-bancario'),
              ))
                Padding(
                  padding: EdgeInsets.only(bottom: mobile ? 8 : 10),
                  child: _confirmClassicLoanButton(context, action, mobile),
                ),
              RetroSection(
                key: const Key('classic-bank-summary-section'),
                title: 'Resumo financeiro do clube',
                padding: EdgeInsets.all(mobile ? 6 : 8),
                child: Container(
                  key: const Key('classic-bank-summary-table'),
                  decoration: BoxDecoration(
                    border: Border.all(color: ClassicColors.border, width: 1.2),
                  ),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      for (final (idx, row) in summaryRows.indexed)
                        Container(
                          key: ValueKey('classic-bank-summary-row-$idx'),
                          decoration: BoxDecoration(
                            color: idx.isEven
                                ? ClassicColors.creamLight
                                : ClassicColors.creamRowAlt,
                            border: idx < summaryRows.length - 1
                                ? const Border(
                                    bottom: BorderSide(
                                      color: ClassicColors.border,
                                      width: 1,
                                    ),
                                  )
                                : null,
                          ),
                          child: IntrinsicHeight(
                            child: Row(
                              crossAxisAlignment: CrossAxisAlignment.stretch,
                              children: [
                                SizedBox(
                                  width: mobile ? 34 : 46,
                                  child: Center(child: row.$1),
                                ),
                                Container(
                                  width: 1,
                                  color: ClassicColors.border,
                                ),
                                if (mobile)
                                  Expanded(
                                    flex: 5,
                                    child: Padding(
                                      padding: const EdgeInsets.symmetric(
                                        horizontal: 7,
                                        vertical: 5,
                                      ),
                                      child: Align(
                                        alignment: Alignment.centerLeft,
                                        child: Text(
                                          row.$2,
                                          style: const TextStyle(
                                            fontFamily:
                                                ClassicTypography.fontFamily,
                                            fontSize: 14,
                                            fontWeight: FontWeight.w800,
                                            color: ClassicColors.rosterBlack,
                                            height: 1.12,
                                          ),
                                        ),
                                      ),
                                    ),
                                  )
                                else
                                  SizedBox(
                                    width: 245,
                                    child: Padding(
                                      padding: const EdgeInsets.symmetric(
                                        horizontal: 12,
                                        vertical: 5.5,
                                      ),
                                      child: Align(
                                        alignment: Alignment.centerLeft,
                                        child: Text(
                                          row.$2,
                                          style: const TextStyle(
                                            fontFamily:
                                                ClassicTypography.fontFamily,
                                            fontSize: 16.5,
                                            fontWeight: FontWeight.w800,
                                            color: ClassicColors.rosterBlack,
                                          ),
                                        ),
                                      ),
                                    ),
                                  ),
                                Container(
                                  width: 1,
                                  color: ClassicColors.border,
                                ),
                                Expanded(
                                  flex: mobile ? 6 : 1,
                                  child: Padding(
                                    padding: EdgeInsets.symmetric(
                                      horizontal: mobile ? 7 : 12,
                                      vertical: mobile ? 5 : 5.5,
                                    ),
                                    child: Align(
                                      alignment: Alignment.centerLeft,
                                      child: row.$3,
                                    ),
                                  ),
                                ),
                              ],
                            ),
                          ),
                        ),
                    ],
                  ),
                ),
              ),
              RetroSection(
                key: const Key('classic-bank-active-loan-section'),
                title: 'Empréstimo ativo',
                padding: EdgeInsets.all(mobile ? 6 : 8),
                child: activeLoans.isEmpty
                    ? Container(
                        key: const Key('classic-bank-no-active-loan'),
                        padding: EdgeInsets.symmetric(
                          horizontal: mobile ? 10 : 12,
                          vertical: mobile ? 10 : 12,
                        ),
                        decoration: BoxDecoration(
                          color: ClassicColors.creamLight,
                          border: Border.all(
                            color: ClassicColors.border,
                            width: 1.1,
                          ),
                          borderRadius: BorderRadius.circular(3),
                        ),
                        child: Text(
                          'Nenhum empréstimo ativo em aberto.',
                          style: TextStyle(
                            fontFamily: ClassicTypography.fontFamily,
                            fontSize: mobile ? 14.5 : 16,
                            fontWeight: FontWeight.w600,
                            color: ClassicColors.rosterBlack,
                          ),
                        ),
                      )
                    : Column(
                        crossAxisAlignment: CrossAxisAlignment.stretch,
                        children: [
                          for (final (idx, loan) in activeLoans.indexed) ...[
                            if (idx > 0) const SizedBox(height: 8),
                            _buildClassicActiveLoanCard(
                              context,
                              loan,
                              idx,
                              mobile,
                            ),
                          ],
                        ],
                      ),
              ),
            ],
          ),
        );
      },
    );
  }

  Widget _buildClassicActiveLoanCard(
    BuildContext context,
    Map<String, dynamic> loan,
    int index,
    bool mobile,
  ) {
    final loanId = '${loan['id'] ?? index}';
    final isClassic = loan['mode'] == 'ClassicFixed';
    final loanTitle = isClassic
        ? 'Empréstimo clássico'
        : 'Empréstimo legado em prestações';
    final remainingDebt = formatClassicMoney(loan['remainingBalance'] ?? 0);
    final rows = <(String, String, bool)>[
      (
        'Principal contratado:',
        formatClassicMoney(loan['principalAmount'] ?? 0),
        false,
      ),
      (
        'Principal pendente:',
        loan['outstandingPrincipal'] == null
            ? 'composição anterior indisponível'
            : formatClassicMoney(loan['outstandingPrincipal']),
        true,
      ),
      ('Juros:', _classicLoanInterestText(loan), true),
      ('Juros pagos:', formatClassicMoney(loan['interestPaid'] ?? 0), true),
      (
        'Juros pendentes:',
        formatClassicMoney(loan['unpaidInterest'] ?? 0),
        true,
      ),
    ];

    final detailsTable = Container(
      key: ValueKey('classic-loan-table-$loanId'),
      decoration: BoxDecoration(
        border: Border.all(color: ClassicColors.border, width: 1.2),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          for (final (rIdx, r) in rows.indexed)
            Container(
              decoration: BoxDecoration(
                color: rIdx.isEven
                    ? ClassicColors.creamLight
                    : ClassicColors.creamRowAlt,
                border: rIdx < rows.length - 1
                    ? const Border(
                        bottom: BorderSide(
                          color: ClassicColors.border,
                          width: 1,
                        ),
                      )
                    : null,
              ),
              child: IntrinsicHeight(
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    if (mobile)
                      Expanded(
                        flex: 5,
                        child: Padding(
                          padding: const EdgeInsets.symmetric(
                            horizontal: 8,
                            vertical: 4.5,
                          ),
                          child: Align(
                            alignment: Alignment.centerLeft,
                            child: Text(
                              r.$1,
                              style: const TextStyle(
                                fontFamily: ClassicTypography.fontFamily,
                                fontSize: 14,
                                fontWeight: FontWeight.w800,
                                color: ClassicColors.rosterBlack,
                              ),
                            ),
                          ),
                        ),
                      )
                    else
                      SizedBox(
                        width: 230,
                        child: Padding(
                          padding: const EdgeInsets.symmetric(
                            horizontal: 12,
                            vertical: 4.5,
                          ),
                          child: Align(
                            alignment: Alignment.centerLeft,
                            child: Text(
                              r.$1,
                              style: const TextStyle(
                                fontFamily: ClassicTypography.fontFamily,
                                fontSize: 16,
                                fontWeight: FontWeight.w800,
                                color: ClassicColors.rosterBlack,
                              ),
                            ),
                          ),
                        ),
                      ),
                    Container(width: 1, color: ClassicColors.border),
                    Expanded(
                      flex: mobile ? 6 : 1,
                      child: Padding(
                        padding: EdgeInsets.symmetric(
                          horizontal: mobile ? 8 : 12,
                          vertical: 4.5,
                        ),
                        child: Align(
                          alignment: Alignment.centerLeft,
                          child: Text(
                            r.$2,
                            style: TextStyle(
                              fontFamily: ClassicTypography.fontFamily,
                              fontSize: mobile ? 14.5 : 16.5,
                              fontWeight: FontWeight.w900,
                              color: r.$3
                                  ? _bankRed
                                  : ClassicColors.rosterBlack,
                            ),
                          ),
                        ),
                      ),
                    ),
                  ],
                ),
              ),
            ),
        ],
      ),
    );

    final quitarButton = Container(
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(4),
        border: Border.all(color: const Color(0xFF4A463D), width: 1.3),
        gradient: const LinearGradient(
          begin: Alignment.topCenter,
          end: Alignment.bottomCenter,
          colors: [Color(0xFFF3EFE4), Color(0xFFD8D0BE)],
        ),
      ),
      child: OutlinedButton(
        key: ValueKey('btn-quitar-emprestimo-$loanId'),
        style: OutlinedButton.styleFrom(
          backgroundColor: Colors.transparent,
          foregroundColor: ClassicColors.titleBarBlue,
          disabledForegroundColor: const Color(0xFF7D786C),
          side: const BorderSide(color: ClassicColors.bevelLight, width: 1.2),
          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(3)),
          minimumSize: Size(mobile ? double.infinity : 122, 36),
          padding: const EdgeInsets.symmetric(horizontal: 18, vertical: 6),
          textStyle: TextStyle(
            fontFamily: ClassicTypography.fontFamily,
            fontSize: mobile ? 16.5 : 18.5,
            fontWeight: FontWeight.w900,
            letterSpacing: 0.4,
          ),
        ),
        onPressed: onRepayLoan == null ? null : () => onRepayLoan!(loanId),
        child: const Text('QUITAR'),
      ),
    );

    return Container(
      key: ValueKey('classic-active-loan-$loanId'),
      padding: EdgeInsets.all(mobile ? 8 : 12),
      decoration: BoxDecoration(
        color: ClassicColors.creamLight,
        border: Border.all(color: ClassicColors.border, width: 1.1),
        borderRadius: BorderRadius.circular(3),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            children: [
              _ClassicCoinsIcon(color: _bankRed, size: mobile ? 24 : 30),
              SizedBox(width: mobile ? 8 : 12),
              Container(
                width: 1.2,
                height: mobile ? 22 : 26,
                color: ClassicColors.border,
              ),
              SizedBox(width: mobile ? 8 : 12),
              Expanded(
                child: Text.rich(
                  TextSpan(
                    style: TextStyle(
                      fontFamily: ClassicTypography.fontFamily,
                      fontSize: mobile ? 17 : 21,
                      fontWeight: FontWeight.w900,
                      height: 1.1,
                    ),
                    children: [
                      TextSpan(
                        text: '$loanTitle — ',
                        style: const TextStyle(
                          color: ClassicColors.rosterBlack,
                        ),
                      ),
                      TextSpan(
                        text: 'dívida $remainingDebt',
                        style: const TextStyle(color: _bankRed),
                      ),
                    ],
                  ),
                ),
              ),
            ],
          ),
          SizedBox(height: mobile ? 8 : 10),
          if (mobile) ...[
            detailsTable,
            const SizedBox(height: 8),
            quitarButton,
          ] else
            Row(
              crossAxisAlignment: CrossAxisAlignment.center,
              children: [
                Expanded(child: detailsTable),
                const SizedBox(width: 18),
                Container(width: 1.2, height: 126, color: ClassicColors.border),
                SizedBox(width: 180, child: Center(child: quitarButton)),
              ],
            ),
        ],
      ),
    );
  }

  Widget _confirmClassicLoanButton(
    BuildContext context,
    Widget action,
    bool mobile,
  ) {
    if (action is! ButtonStyleButton) return action;
    final enabled = action.onPressed != null;
    return Container(
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(4),
        border: Border.all(color: const Color(0xFF4A0709), width: 1.3),
        gradient: LinearGradient(
          begin: Alignment.topCenter,
          end: Alignment.bottomCenter,
          colors: enabled
              ? const [Color(0xFFC91A20), Color(0xFFA31015)]
              : const [Color(0xFF8C6B6C), Color(0xFF6E5253)],
        ),
      ),
      child: Container(
        decoration: const BoxDecoration(
          border: Border(
            top: BorderSide(color: Color(0xFFE55B60), width: 1.5),
            left: BorderSide(color: Color(0xFFE55B60), width: 1.5),
            bottom: BorderSide(color: Color(0xFF6B080B), width: 1.5),
            right: BorderSide(color: Color(0xFF6B080B), width: 1.5),
          ),
        ),
        child: ElevatedButton(
          key: action.key,
          style: ElevatedButton.styleFrom(
            elevation: 0,
            backgroundColor: Colors.transparent,
            disabledBackgroundColor: Colors.transparent,
            shadowColor: Colors.transparent,
            foregroundColor: Colors.white,
            disabledForegroundColor: Colors.white70,
            minimumSize: Size(double.infinity, mobile ? 42 : 46),
            padding: EdgeInsets.symmetric(
              horizontal: mobile ? 8 : 16,
              vertical: mobile ? 6 : 8,
            ),
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(2),
            ),
          ),
          onPressed: !enabled
              ? null
              : () async {
                  final confirmed = await showDialog<bool>(
                    context: context,
                    builder: (ctx) => AlertDialog(
                      backgroundColor: ClassicColors.cream,
                      titlePadding: EdgeInsets.zero,
                      title: const RetroTitle(
                        title: 'Confirmar operação financeira',
                      ),
                      content: action.child,
                      actions: [
                        TextButton(
                          onPressed: () => Navigator.pop(ctx, false),
                          child: const Text('CANCELAR'),
                        ),
                        ElevatedButton(
                          style: ElevatedButton.styleFrom(
                            backgroundColor: ClassicColors.actionGreen,
                            foregroundColor: Colors.white,
                          ),
                          onPressed: () => Navigator.pop(ctx, true),
                          child: const Text('CONFIRMAR'),
                        ),
                      ],
                    ),
                  );
                  if (confirmed == true && context.mounted) action.onPressed!();
                },
          child: FittedBox(
            fit: BoxFit.scaleDown,
            child: Row(
              mainAxisSize: MainAxisSize.min,
              children: [
                _ClassicCoinsIcon(color: Colors.white, size: mobile ? 20 : 25),
                SizedBox(width: mobile ? 8 : 10),
                Text(
                  'EMPRÉSTIMO (+R\$ 500.000; JUROS R\$ 25.000/JOGO)',
                  style: TextStyle(
                    fontFamily: ClassicTypography.fontFamily,
                    fontSize: mobile ? 17 : 21,
                    fontWeight: FontWeight.w900,
                    color: Colors.white,
                    letterSpacing: 0.3,
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  Widget _buildClassicStadiumScreen(BuildContext context, Map stadium) {
    return LayoutBuilder(
      builder: (context, constraints) {
        final mobile = constraints.maxWidth < 720;
        final desktopTwoCol = constraints.maxWidth >= 720;
        final cash =
            (data['availableCash'] as num?) ??
            (club['cashBalance'] as num?) ??
            0;
        final rawStadiumName = (stadium['stadiumName'] ?? '').toString().trim();
        final clubName = (club['name'] ?? '').toString().trim();
        final stadiumTitle = rawStadiumName.isNotEmpty
            ? rawStadiumName
            : (clubName.isNotEmpty ? 'Estádio de $clubName' : 'Estádio');

        final rows = <(Widget, String, String, bool)>[
          (
            Icon(
              Icons.groups,
              size: mobile ? 18 : 20,
              color: ClassicColors.rosterBlack,
            ),
            'Capacidade construída:',
            '${stadium['builtCapacity'] ?? '—'} lugares',
            false,
          ),
          (
            Icon(
              Icons.event_seat,
              size: mobile ? 17 : 19,
              color: ClassicColors.rosterBlack,
            ),
            'Capacidade utilizável:',
            '${stadium['usableCapacity'] ?? '—'} lugares',
            false,
          ),
          (
            Icon(
              Icons.build,
              size: mobile ? 17 : 18,
              color: ClassicColors.rosterBlack,
            ),
            'Condição estrutural:',
            '${stadium['conditionPercent'] ?? '—'}%',
            false,
          ),
          (
            Icon(
              Icons.groups,
              size: mobile ? 18 : 20,
              color: ClassicColors.rosterBlack,
            ),
            'Último público:',
            '${stadium['lastAttendance'] ?? '—'}',
            false,
          ),
          (
            Icon(
              Icons.confirmation_number_outlined,
              size: mobile ? 17 : 19,
              color: ClassicColors.rosterBlack,
            ),
            'Ingresso:',
            formatClassicMoney(stadium['lastCalculatedTicketPrice'] ?? 0),
            false,
          ),
          (
            Icon(
              Icons.handyman,
              size: mobile ? 17 : 19,
              color: ClassicColors.rosterBlack,
            ),
            'Manutenção por jogo:',
            formatClassicMoney(stadium['fixedCostPerMatch'] ?? 0),
            false,
          ),
          (
            _ClassicCoinsIcon(
              color: cash < 0 ? _bankRed : _bankGreen,
              size: mobile ? 17 : 19,
            ),
            'Caixa:',
            formatClassicMoney(cash),
            true,
          ),
        ];

        final detailsColumn = Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Container(
              key: const Key('classic-stadium-summary-card'),
              decoration: BoxDecoration(
                color: ClassicColors.creamLight,
                border: Border.all(color: ClassicColors.border, width: 1.2),
              ),
              child: Container(
                decoration: const BoxDecoration(
                  border: Border(
                    top: BorderSide(
                      color: ClassicColors.bevelLight,
                      width: 1.3,
                    ),
                    left: BorderSide(
                      color: ClassicColors.bevelLight,
                      width: 1.3,
                    ),
                    bottom: BorderSide(
                      color: ClassicColors.bevelDark,
                      width: 1.3,
                    ),
                    right: BorderSide(
                      color: ClassicColors.bevelDark,
                      width: 1.3,
                    ),
                  ),
                ),
                padding: const EdgeInsets.all(2),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Container(
                      padding: EdgeInsets.symmetric(
                        horizontal: mobile ? 10 : 12,
                        vertical: mobile ? 6 : 7,
                      ),
                      decoration: const BoxDecoration(
                        gradient: LinearGradient(
                          colors: [
                            ClassicColors.titleBarBlue,
                            ClassicColors.titleBarBlueLight,
                          ],
                        ),
                      ),
                      child: Row(
                        children: [
                          _ClassicStadiumIcon(
                            color: Colors.white,
                            size: mobile ? 20 : 22,
                          ),
                          const SizedBox(width: 8),
                          Expanded(
                            child: Text(
                              stadiumTitle,
                              maxLines: 1,
                              overflow: TextOverflow.ellipsis,
                              style: TextStyle(
                                fontFamily: ClassicTypography.fontFamily,
                                fontSize: mobile ? 17 : 19,
                                fontWeight: FontWeight.w900,
                                color: Colors.white,
                                height: 1.05,
                              ),
                            ),
                          ),
                        ],
                      ),
                    ),
                    Padding(
                      padding: EdgeInsets.symmetric(
                        horizontal: mobile ? 8 : 10,
                        vertical: 2,
                      ),
                      child: Column(
                        key: const Key('classic-stadium-details'),
                        crossAxisAlignment: CrossAxisAlignment.stretch,
                        children: [
                          for (final (idx, row) in rows.indexed) ...[
                            if (idx > 0)
                              Container(
                                height: 1,
                                color: const Color(0xFFD5CEBE),
                              ),
                            Padding(
                              padding: EdgeInsets.symmetric(
                                vertical: mobile ? 4.5 : 5,
                              ),
                              child: Row(
                                children: [
                                  SizedBox(
                                    width: mobile ? 24 : 28,
                                    child: Center(child: row.$1),
                                  ),
                                  const SizedBox(width: 6),
                                  Expanded(
                                    child: Text(
                                      row.$2,
                                      style: TextStyle(
                                        fontFamily:
                                            ClassicTypography.fontFamily,
                                        fontSize: mobile ? 14.5 : 15.5,
                                        fontWeight: FontWeight.w800,
                                        color: ClassicColors.rosterBlack,
                                      ),
                                    ),
                                  ),
                                  const SizedBox(width: 8),
                                  Text(
                                    row.$3,
                                    textAlign: TextAlign.right,
                                    style: TextStyle(
                                      fontFamily: ClassicTypography.fontFamily,
                                      fontSize: row.$4
                                          ? (mobile ? 16 : 17.5)
                                          : (mobile ? 14.5 : 15.5),
                                      fontWeight: row.$4
                                          ? FontWeight.w900
                                          : FontWeight.w600,
                                      color: row.$4
                                          ? (cash < 0 ? _bankRed : _bankGreen)
                                          : ClassicColors.rosterBlack,
                                    ),
                                  ),
                                ],
                              ),
                            ),
                          ],
                        ],
                      ),
                    ),
                  ],
                ),
              ),
            ),
            SizedBox(height: mobile ? 8 : 10),
            for (final action in actions.where(
              (a) => a.key != const ValueKey('btn-emprestimo-bancario'),
            ))
              Padding(
                padding: EdgeInsets.only(bottom: mobile ? 8 : 10),
                child: _confirmClassicStadiumButton(
                  context,
                  action,
                  stadium,
                  mobile,
                ),
              ),
            Container(
              key: const Key('classic-stadium-info-note'),
              padding: EdgeInsets.symmetric(
                horizontal: mobile ? 10 : 12,
                vertical: mobile ? 8 : 10,
              ),
              decoration: BoxDecoration(
                color: const Color(0xFFEDE8DA),
                borderRadius: BorderRadius.circular(5),
                border: Border.all(color: const Color(0xFFA69F8E), width: 1.1),
              ),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Padding(
                    padding: const EdgeInsets.only(top: 1),
                    child: Icon(
                      Icons.info_outline,
                      size: mobile ? 20 : 21,
                      color: ClassicColors.titleBarBlue,
                    ),
                  ),
                  const SizedBox(width: 8),
                  Expanded(
                    child: Text(
                      'Novas obras: conclusão após uma rodada. O jogo atual usa a capacidade anterior; o seguinte usa a obra concluída.',
                      style: TextStyle(
                        fontFamily: ClassicTypography.fontFamily,
                        fontSize: mobile ? 13 : 13.5,
                        fontStyle: FontStyle.italic,
                        color: const Color(0xFF2B2923),
                        height: 1.22,
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ],
        );

        return SingleChildScrollView(
          key: const Key('classic-stadium-screen'),
          padding: EdgeInsets.symmetric(
            horizontal: mobile ? 8 : 14,
            vertical: mobile ? 8 : 10,
          ),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Row(
                key: const Key('classic-stadium-header'),
                crossAxisAlignment: CrossAxisAlignment.center,
                children: [
                  _ClassicStadiumIcon(
                    color: ClassicColors.rosterBlack,
                    size: mobile ? 28 : 34,
                  ),
                  SizedBox(width: mobile ? 8 : 12),
                  Container(
                    width: 1.4,
                    height: mobile ? 22 : 28,
                    color: ClassicColors.border,
                  ),
                  SizedBox(width: mobile ? 8 : 12),
                  Text(
                    'ESTÁDIO',
                    style: TextStyle(
                      fontFamily: ClassicTypography.fontFamily,
                      fontSize: mobile ? 23 : 29,
                      fontWeight: FontWeight.w900,
                      color: ClassicColors.rosterBlack,
                      letterSpacing: 0.4,
                      height: 1,
                    ),
                  ),
                ],
              ),
              SizedBox(height: mobile ? 6 : 8),
              Container(height: 1.5, color: const Color(0xFFB58C24)),
              const SizedBox(height: 1.5),
              Container(height: 1.2, color: const Color(0xFF8C6B18)),
              SizedBox(height: mobile ? 8 : 12),
              if (desktopTwoCol)
                Row(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Expanded(
                      flex: 55,
                      child: ClassicStadiumView(stadium: stadium),
                    ),
                    const SizedBox(width: 14),
                    Expanded(flex: 45, child: detailsColumn),
                  ],
                )
              else ...[
                ClassicStadiumView(stadium: stadium),
                const SizedBox(height: 10),
                detailsColumn,
              ],
            ],
          ),
        );
      },
    );
  }

  Widget _confirmClassicStadiumButton(
    BuildContext context,
    Widget action,
    Map stadium,
    bool mobile,
  ) {
    if (action is! ButtonStyleButton) return action;
    final enabled = action.onPressed != null;
    final isRenovate = action.key == const ValueKey('btn-reformar-estadio');
    final isExpand = action.key == const ValueKey('btn-ampliar-estadio');

    final renoCost =
        (data['renovationCostEstimate'] as num?) ??
        (stadium['renovationCostEstimate'] as num?) ??
        50000;
    final expCost =
        (data['expansion5kCostEstimate'] as num?) ??
        (stadium['expansion5kCostEstimate'] as num?) ??
        425000;

    final String labelText;
    if (action.child is Text && (action.child as Text).data != null) {
      labelText = (action.child as Text).data!;
    } else if (isRenovate) {
      labelText = 'REFORMAR ESTÁDIO (${formatClassicMoney(renoCost)})';
    } else if (isExpand) {
      labelText = 'AMPLIAR +5.000 LUGARES (${formatClassicMoney(expCost)})';
    } else {
      labelText = '';
    }

    final bgColor = isRenovate
        ? const Color(0xFF0A3494)
        : isExpand
        ? const Color(0xFFE8BE3B)
        : ClassicColors.actionGreen;
    final fgColor = isExpand ? ClassicColors.rosterBlack : Colors.white;
    final borderOuter = isRenovate
        ? const Color(0xFF041642)
        : isExpand
        ? const Color(0xFF594408)
        : const Color(0xFF0E3D1C);
    final bevelTop = isRenovate
        ? const Color(0xFF4F7DE8)
        : isExpand
        ? const Color(0xFFFCE48E)
        : const Color(0xFF56B870);
    final bevelBottom = isRenovate
        ? const Color(0xFF051C57)
        : isExpand
        ? const Color(0xFF9C7610)
        : const Color(0xFF0F4A20);

    return Container(
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(4),
        border: Border.all(color: borderOuter, width: 1.3),
        gradient: LinearGradient(
          begin: Alignment.topCenter,
          end: Alignment.bottomCenter,
          colors: !enabled
              ? const [Color(0xFF8C887D), Color(0xFF6E6A60)]
              : isRenovate
              ? const [Color(0xFF1242A8), Color(0xFF082A78)]
              : isExpand
              ? const [Color(0xFFF2CA4B), Color(0xFFD9AC26)]
              : const [Color(0xFF1F7A39), Color(0xFF145927)],
        ),
      ),
      child: Container(
        decoration: BoxDecoration(
          border: Border(
            top: BorderSide(color: bevelTop, width: 1.4),
            left: BorderSide(color: bevelTop, width: 1.4),
            bottom: BorderSide(color: bevelBottom, width: 1.4),
            right: BorderSide(color: bevelBottom, width: 1.4),
          ),
        ),
        child: ElevatedButton(
          key: action.key,
          style: ElevatedButton.styleFrom(
            elevation: 0,
            backgroundColor: bgColor,
            disabledBackgroundColor: const Color(0xFF7D796E),
            shadowColor: Colors.transparent,
            foregroundColor: fgColor,
            disabledForegroundColor: Colors.white70,
            minimumSize: Size(double.infinity, mobile ? 42 : 44),
            padding: EdgeInsets.symmetric(
              horizontal: mobile ? 10 : 14,
              vertical: mobile ? 6 : 8,
            ),
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(2),
            ),
          ),
          onPressed: !enabled
              ? null
              : () async {
                  final confirmed = await showDialog<bool>(
                    context: context,
                    builder: (ctx) => AlertDialog(
                      backgroundColor: ClassicColors.cream,
                      titlePadding: EdgeInsets.zero,
                      title: const RetroTitle(
                        title: 'Confirmar operação financeira',
                      ),
                      content: action.child,
                      actions: [
                        TextButton(
                          onPressed: () => Navigator.pop(ctx, false),
                          child: const Text('CANCELAR'),
                        ),
                        ElevatedButton(
                          style: ElevatedButton.styleFrom(
                            backgroundColor: ClassicColors.actionGreen,
                            foregroundColor: Colors.white,
                          ),
                          onPressed: () => Navigator.pop(ctx, true),
                          child: const Text('CONFIRMAR'),
                        ),
                      ],
                    ),
                  );
                  if (confirmed == true && context.mounted) action.onPressed!();
                },
          child: labelText.isEmpty
              ? action.child
              : FittedBox(
                  fit: BoxFit.scaleDown,
                  child: Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      if (isRenovate)
                        Icon(
                          Icons.build,
                          color: fgColor,
                          size: mobile ? 20 : 22,
                        )
                      else if (isExpand)
                        _ClassicStadiumIcon(
                          color: fgColor,
                          size: mobile ? 22 : 24,
                        ),
                      if (isRenovate || isExpand)
                        SizedBox(width: mobile ? 8 : 10),
                      Text(
                        labelText,
                        style: TextStyle(
                          fontFamily: ClassicTypography.fontFamily,
                          fontSize: mobile ? 17 : 19.5,
                          fontWeight: FontWeight.w900,
                          color: fgColor,
                          letterSpacing: 0.3,
                        ),
                      ),
                    ],
                  ),
                ),
        ),
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
            ? ClassicColors.titleBarBlue
            : action.key == const ValueKey('btn-ampliar-estadio')
            ? ClassicColors.softGold
            : ClassicColors.actionGreen,
        foregroundColor: action.key == const ValueKey('btn-ampliar-estadio')
            ? ClassicColors.rosterBlack
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
                  backgroundColor: ClassicColors.cream,
                  titlePadding: EdgeInsets.zero,
                  title: const RetroTitle(
                    title: 'Confirmar operação financeira',
                  ),
                  content: action.child,
                  actions: [
                    TextButton(
                      onPressed: () => Navigator.pop(ctx, false),
                      child: const Text('CANCELAR'),
                    ),
                    ElevatedButton(
                      style: ElevatedButton.styleFrom(
                        backgroundColor: ClassicColors.actionGreen,
                        foregroundColor: Colors.white,
                      ),
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

class _ClassicBankIcon extends StatelessWidget {
  final Color color;
  final double size;
  const _ClassicBankIcon({required this.color, required this.size});

  @override
  Widget build(BuildContext context) => SizedBox(
    width: size,
    height: size,
    child: CustomPaint(painter: _ClassicBankPainter(color)),
  );
}

class _ClassicBankPainter extends CustomPainter {
  final Color color;
  const _ClassicBankPainter(this.color);

  @override
  void paint(Canvas canvas, Size size) {
    final fill = Paint()
      ..color = color
      ..style = PaintingStyle.fill;
    final stroke = Paint()
      ..color = color
      ..style = PaintingStyle.stroke
      ..strokeWidth = size.width * 0.055;

    final w = size.width;
    final h = size.height;

    // Outer roof triangle (pediment)
    final roof = Path()
      ..moveTo(w * 0.08, h * 0.36)
      ..lineTo(w * 0.50, h * 0.08)
      ..lineTo(w * 0.92, h * 0.36)
      ..close();
    canvas.drawPath(roof, fill);

    // Inner pediment cut-out triangle
    final innerRoof = Path()
      ..moveTo(w * 0.23, h * 0.31)
      ..lineTo(w * 0.50, h * 0.14)
      ..lineTo(w * 0.77, h * 0.31)
      ..close();
    canvas.drawPath(
      innerRoof,
      Paint()
        ..color = const Color(0xFFE6DFCE)
        ..style = PaintingStyle.fill,
    );

    // Architrave bar under roof
    canvas.drawRect(
      Rect.fromLTWH(w * 0.12, h * 0.37, w * 0.76, h * 0.06),
      fill,
    );

    // Three pillars with capitals and bases
    for (final cx in [0.25, 0.50, 0.75]) {
      final px = w * cx;
      canvas.drawRect(
        Rect.fromCenter(
          center: Offset(px, h * 0.45),
          width: w * 0.16,
          height: h * 0.045,
        ),
        fill,
      );
      canvas.drawRect(
        Rect.fromCenter(
          center: Offset(px, h * 0.61),
          width: w * 0.105,
          height: h * 0.28,
        ),
        fill,
      );
      canvas.drawRect(
        Rect.fromCenter(
          center: Offset(px, h * 0.76),
          width: w * 0.16,
          height: h * 0.045,
        ),
        fill,
      );
    }

    // Stepped base
    canvas.drawRect(
      Rect.fromLTWH(w * 0.11, h * 0.80, w * 0.78, h * 0.055),
      fill,
    );
    canvas.drawRect(
      Rect.fromLTWH(w * 0.06, h * 0.87, w * 0.88, h * 0.065),
      fill,
    );
    canvas.drawLine(
      Offset(w * 0.06, h * 0.36),
      Offset(w * 0.94, h * 0.36),
      stroke,
    );
  }

  @override
  bool shouldRepaint(covariant _ClassicBankPainter old) => old.color != color;
}

class _ClassicCoinsIcon extends StatelessWidget {
  final Color color;
  final double size;
  const _ClassicCoinsIcon({required this.color, required this.size});

  @override
  Widget build(BuildContext context) => SizedBox(
    width: size,
    height: size * 0.88,
    child: CustomPaint(painter: _ClassicCoinsPainter(color)),
  );
}

class _ClassicCoinsPainter extends CustomPainter {
  final Color color;
  const _ClassicCoinsPainter(this.color);

  @override
  void paint(Canvas canvas, Size size) {
    final fill = Paint()
      ..color = color
      ..style = PaintingStyle.fill;
    final w = size.width;
    final h = size.height;

    void drawCoinDisc(double cx, double cy, double cw, double ch) {
      final rrect = RRect.fromRectAndRadius(
        Rect.fromCenter(center: Offset(cx, cy), width: cw, height: ch),
        Radius.circular(ch * 0.48),
      );
      canvas.drawRRect(rrect, fill);
    }

    // Back/right taller stack (3 discs)
    final rightX = w * 0.66;
    final rightW = w * 0.52;
    final discH = h * 0.165;
    drawCoinDisc(rightX, h * 0.20, rightW, discH * 1.15);
    drawCoinDisc(rightX, h * 0.40, rightW, discH);
    drawCoinDisc(rightX, h * 0.59, rightW, discH);

    // Front/left stack (3 discs)
    final leftX = w * 0.35;
    final leftW = w * 0.56;
    drawCoinDisc(leftX, h * 0.44, leftW, discH * 1.15);
    drawCoinDisc(leftX, h * 0.64, leftW, discH);
    drawCoinDisc(leftX, h * 0.84, leftW, discH);
  }

  @override
  bool shouldRepaint(covariant _ClassicCoinsPainter old) => old.color != color;
}

class _ClassicBanknoteIcon extends StatelessWidget {
  final Color color;
  final double size;
  const _ClassicBanknoteIcon({required this.color, required this.size});

  @override
  Widget build(BuildContext context) => SizedBox(
    width: size,
    height: size * 0.68,
    child: CustomPaint(painter: _ClassicBanknotePainter(color)),
  );
}

class _ClassicBanknotePainter extends CustomPainter {
  final Color color;
  const _ClassicBanknotePainter(this.color);

  @override
  void paint(Canvas canvas, Size size) {
    final w = size.width;
    final h = size.height;
    final stroke = Paint()
      ..color = color
      ..style = PaintingStyle.stroke
      ..strokeWidth = h * 0.11;
    final fill = Paint()
      ..color = color
      ..style = PaintingStyle.fill;

    canvas.drawRRect(
      RRect.fromRectAndRadius(
        Rect.fromLTWH(w * 0.04, h * 0.08, w * 0.92, h * 0.84),
        Radius.circular(h * 0.10),
      ),
      stroke,
    );
    canvas.drawCircle(Offset(w * 0.50, h * 0.50), h * 0.21, stroke);
    canvas.drawCircle(Offset(w * 0.22, h * 0.50), h * 0.065, fill);
    canvas.drawCircle(Offset(w * 0.78, h * 0.50), h * 0.065, fill);
  }

  @override
  bool shouldRepaint(covariant _ClassicBanknotePainter old) =>
      old.color != color;
}

class _ClassicCalendarGridIcon extends StatelessWidget {
  final Color color;
  final double size;
  const _ClassicCalendarGridIcon({required this.color, required this.size});

  @override
  Widget build(BuildContext context) => SizedBox(
    width: size,
    height: size,
    child: CustomPaint(painter: _ClassicCalendarGridPainter(color)),
  );
}

class _ClassicCalendarGridPainter extends CustomPainter {
  final Color color;
  const _ClassicCalendarGridPainter(this.color);

  @override
  void paint(Canvas canvas, Size size) {
    final w = size.width;
    final h = size.height;
    final stroke = Paint()
      ..color = color
      ..style = PaintingStyle.stroke
      ..strokeWidth = w * 0.085;
    final fill = Paint()
      ..color = color
      ..style = PaintingStyle.fill;

    canvas.drawRRect(
      RRect.fromRectAndRadius(
        Rect.fromLTWH(w * 0.10, h * 0.12, w * 0.80, h * 0.76),
        Radius.circular(w * 0.10),
      ),
      stroke,
    );
    canvas.drawLine(
      Offset(w * 0.12, h * 0.34),
      Offset(w * 0.88, h * 0.34),
      stroke,
    );
    for (final y in [0.51, 0.70]) {
      for (final x in [0.29, 0.50, 0.71]) {
        canvas.drawRect(
          Rect.fromCenter(
            center: Offset(w * x, h * y),
            width: w * 0.11,
            height: h * 0.10,
          ),
          fill,
        );
      }
    }
  }

  @override
  bool shouldRepaint(covariant _ClassicCalendarGridPainter old) =>
      old.color != color;
}

class _ClassicStadiumIcon extends StatelessWidget {
  final Color color;
  final double size;
  const _ClassicStadiumIcon({required this.color, required this.size});

  @override
  Widget build(BuildContext context) => SizedBox(
    width: size * 1.15,
    height: size * 0.90,
    child: CustomPaint(painter: _ClassicStadiumIconPainter(color)),
  );
}

class _ClassicStadiumIconPainter extends CustomPainter {
  final Color color;
  const _ClassicStadiumIconPainter(this.color);

  @override
  void paint(Canvas canvas, Size size) {
    final w = size.width;
    final h = size.height;
    final stroke = Paint()
      ..color = color
      ..style = PaintingStyle.stroke
      ..strokeWidth = w * 0.065;
    final fill = Paint()
      ..color = color
      ..style = PaintingStyle.fill;

    // 4 small flags along top rim
    for (final fx in [0.22, 0.40, 0.60, 0.78]) {
      final px = w * fx;
      canvas.drawLine(
        Offset(px, h * 0.06),
        Offset(px, h * 0.25),
        Paint()
          ..color = color
          ..strokeWidth = w * 0.045,
      );
      final flag = Path()
        ..moveTo(px, h * 0.06)
        ..lineTo(px + w * 0.11, h * 0.11)
        ..lineTo(px, h * 0.17)
        ..close();
      canvas.drawPath(flag, fill);
    }

    // Top oval stadium rim
    canvas.drawOval(
      Rect.fromLTWH(w * 0.06, h * 0.22, w * 0.88, h * 0.26),
      stroke,
    );

    // Stadium body wall
    final bodyPath = Path()
      ..moveTo(w * 0.06, h * 0.35)
      ..lineTo(w * 0.08, h * 0.84)
      ..quadraticBezierTo(w * 0.50, h * 0.98, w * 0.92, h * 0.84)
      ..lineTo(w * 0.94, h * 0.35)
      ..quadraticBezierTo(w * 0.50, h * 0.50, w * 0.06, h * 0.35)
      ..close();
    canvas.drawPath(bodyPath, fill);

    // Cutout windows & entrance in contrasting color
    final cutoutColor = color.computeLuminance() > 0.6
        ? ClassicColors.titleBarBlue
        : ClassicColors.creamLight;
    final cutout = Paint()
      ..color = cutoutColor
      ..style = PaintingStyle.fill;

    for (final wx in [0.20, 0.35, 0.65, 0.80]) {
      canvas.drawRRect(
        RRect.fromRectAndRadius(
          Rect.fromCenter(
            center: Offset(w * wx, h * 0.61),
            width: w * 0.08,
            height: h * 0.13,
          ),
          Radius.circular(w * 0.02),
        ),
        cutout,
      );
    }
    // Central entrance arch
    canvas.drawRRect(
      RRect.fromRectAndCorners(
        Rect.fromCenter(
          center: Offset(w * 0.50, h * 0.77),
          width: w * 0.16,
          height: h * 0.22,
        ),
        topLeft: Radius.circular(w * 0.04),
        topRight: Radius.circular(w * 0.04),
      ),
      cutout,
    );
  }

  @override
  bool shouldRepaint(covariant _ClassicStadiumIconPainter old) =>
      old.color != color;
}
