import 'package:flutter/material.dart';
import 'api_client.dart';
import 'auction_config.dart';
import 'club_theme.dart';
import 'classic_layout.dart';
import 'visual_components.dart';

String marketPosition(dynamic position) => position is num
    ? const ['G', 'D', 'M', 'A'][position.toInt().clamp(0, 3)]
    : position?.toString() ?? '';

/// Kept in the shell so filters survive details, tab changes and visits to other menus.
class MarketPreferences {
  int tab = 0;
  final positions = ['ALL', 'ALL'];
  final sorts = ['STRENGTH_DESC', 'STRENGTH_DESC'];
}

List<Map<String, dynamic>> orderedMarketPlayers(
  List<dynamic> source, {
  required bool auction,
  required String position,
  required String order,
}) {
  final rows = source
      .whereType<Map>()
      .map((r) => Map<String, dynamic>.from(r))
      .where(
        (r) => position == 'ALL' || marketPosition(r['position']) == position,
      )
      .toList();
  num price(Map<String, dynamic> r) => auction
      ? (r['highestBidderClubId'] == null
            ? r['minimumBid'] as num? ?? 0
            : r['currentBid'] as num? ?? 0)
      : r['askingPrice'] as num? ?? 0;
  rows.sort((a, b) {
    final comparison = switch (order) {
      'PRICE_ASC' => price(a).compareTo(price(b)),
      'PRICE_DESC' => price(b).compareTo(price(a)),
      'STRENGTH_ASC' => (a['strength'] as num? ?? 0).compareTo(
        b['strength'] as num? ?? 0,
      ),
      _ => (b['strength'] as num? ?? 0).compareTo(a['strength'] as num? ?? 0),
    };
    return comparison != 0
        ? comparison
        : '${a['auctionId'] ?? a['playerId']}'.compareTo(
            '${b['auctionId'] ?? b['playerId']}',
          );
  });
  return rows;
}

class MarketScreen extends StatefulWidget {
  final ElifootApiClient api;
  final String worldId, coachId;
  final String? clubId;
  final Map<String, dynamic> snapshot, data;
  final List<dynamic> squad;
  final MarketPreferences preferences;
  final bool locked;
  final Future<void> Function() refresh;
  const MarketScreen({
    super.key,
    required this.api,
    required this.worldId,
    required this.coachId,
    required this.clubId,
    required this.snapshot,
    required this.data,
    required this.squad,
    required this.preferences,
    required this.refresh,
    this.locked = false,
  });
  @override
  State<MarketScreen> createState() => _MarketScreenState();
}

class _MarketScreenState extends State<MarketScreen> {
  bool _busy = false;
  String? _message, _ownPlayerId;
  MarketPreferences get pref => widget.preferences;
  bool get _safe =>
      !widget.locked &&
      const ['PREPARING', 'RESULTS'].contains(widget.snapshot['roundState']);
  bool get _canTrade =>
      _safe &&
      widget.clubId != null &&
      !_busy &&
      !(widget.snapshot['roundState'] == 'PREPARING' &&
          ((widget.snapshot['readyCounter']?['participants'] as List?) ?? [])
              .any(
                (p) => p['coachId'] == widget.coachId && p['isReady'] == true,
              ));

  Future<void> _command(Future<String> Function() action) async {
    if (!_canTrade) return;
    setState(() {
      _busy = true;
      _message = null;
    });
    try {
      final message = await action();
      await widget.refresh();
      if (mounted) setState(() => _message = message);
    } catch (e) {
      if (mounted) {
        setState(() => _message = e.toString().replaceFirst('Exception: ', ''));
      }
      try {
        await widget.refresh();
      } catch (_) {}
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  Future<void> _announce(bool direct) async {
    final player = widget.squad
        .where((p) => p['id'] == _ownPlayerId)
        .firstOrNull;
    if (player == null || !_canTrade) return;
    final config = await showDialog<VoluntaryAuctionConfig>(
      context: context,
      builder: (_) => AuctionConfigDialog(
        playerName: player['displayName'] ?? player['name'] ?? '',
        position: marketPosition(player['position']),
        strength: player['strength'] as num?,
        salary: player['salaryPerRound'] as num?,
        roundNumber: (widget.snapshot['roundNumber'] as num?)?.toInt() ?? 1,
        seasonNumber: (widget.snapshot['seasonNumber'] as num?)?.toInt() ?? 1,
        totalRoundsInSeason:
            (widget.snapshot['totalRoundsInSeason'] as num?)?.toInt() ?? 22,
        roundAlreadyCompleted: widget.snapshot['roundState'] == 'RESULTS',
        allowDirect: true,
        initialDirect: direct,
        directPrice: player['fairValue'] as num? ?? 500000,
      ),
    );
    if (!mounted || config == null || !_canTrade) return;
    await _command(() async {
      if (config.isDirect) {
        await widget.api.listPlayerForTransfer(
          widget.worldId,
          widget.coachId,
          player['id'].toString(),
          config.minimumBid,
        );
      } else {
        await widget.api.createVoluntaryAuction(
          widget.worldId,
          widget.coachId,
          player['id'].toString(),
          minimumBid: config.minimumBid,
          durationRounds: config.durationRounds,
        );
      }
      if (mounted) setState(() => pref.tab = config.isDirect ? 1 : 0);
      return 'ANÚNCIO PUBLICADO.';
    });
  }

  Widget _details(Map<String, dynamic> p, {bool auction = false}) => Column(
    mainAxisSize: MainAxisSize.min,
    crossAxisAlignment: CrossAxisAlignment.start,
    children: [
      Text(
        '${p['displayName'] ?? p['playerDisplayName'] ?? ""}',
        style: const TextStyle(fontWeight: FontWeight.bold),
      ),
      Text(
        'Posição: ${marketPosition(p['position'])} • Força: ${p['strength']}',
      ),
      if (auction) ...[
        Text(
          'Lance mínimo: ${formatAuctionMoney(p['minimumBid'] as num? ?? 0)}',
        ),
        Text(
          'Maior lance atual: ${p['highestBidderClubId'] == null ? "Sem lances" : formatAuctionMoney(p['currentBid'] as num? ?? 0)}',
        ),
        Text('Rodadas restantes: ${_roundsRemaining(p)}'),
      ] else if (p['askingPrice'] is num)
        Text(
          'Preço fixo: ${formatAuctionMoney(p['askingPrice'] as num? ?? 0)}',
        ),
      if (p['salaryPerRound'] is num)
        Text(
          'Salário: ${formatAuctionMoney(p['salaryPerRound'] as num)} / rodada',
        ),
      Text('Vendedor: ${p['sellerClubName'] ?? "Atleta livre"}'),
    ],
  );

  int _roundsRemaining(Map<String, dynamic> p) {
    if (p['isClosed'] == true) return 0;
    if (p['roundsRemaining'] is num) {
      return (p['roundsRemaining'] as num).toInt();
    }
    final total =
        (widget.snapshot['totalRoundsInSeason'] as num?)?.toInt() ?? 22;
    final season = (widget.snapshot['seasonNumber'] as num?)?.toInt() ?? 1;
    return (((p['expiresAtSeason'] as num?)?.toInt() ?? season) - season) *
            total +
        ((p['expiresAtRound'] as num?)?.toInt() ?? 0) -
        ((widget.snapshot['roundNumber'] as num?)?.toInt() ?? 1) +
        (widget.snapshot['roundState'] == 'RESULTS' ? 0 : 1);
  }

  Future<double?> _amountDialog(
    String title,
    Map<String, dynamic> player, {
    required num initial,
    required num minimum,
    bool auction = false,
    bool allowZero = false,
  }) async {
    final controller = TextEditingController(text: initial.toString());
    final form = GlobalKey<FormState>();
    final result = await showDialog<double>(
      context: context,
      builder: (ctx) => AlertDialog(
        insetPadding: const EdgeInsets.symmetric(horizontal: 12, vertical: 16),
        title: Text(title),
        content: SingleChildScrollView(
          child: SizedBox(
            width: 380,
            child: Form(
              key: form,
              child: Column(
                mainAxisSize: MainAxisSize.min,
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  _details(player, auction: auction),
                  const SizedBox(height: 12),
                  TextFormField(
                    key: const ValueKey('market-custom-amount'),
                    controller: controller,
                    keyboardType: const TextInputType.numberWithOptions(
                      decimal: true,
                    ),
                    decoration: InputDecoration(
                      labelText: auction
                          ? 'Seu lance (R\$)'
                          : 'Valor da proposta (R\$)',
                    ),
                    validator: (raw) {
                      final value = parseMarketAmount(
                        raw,
                        allowZero: allowZero,
                      );
                      if (value == null) return 'Informe um valor válido.';
                      if (value < minimum) {
                        return auction
                            ? 'LANCE INSUFICIENTE. Mínimo: ${formatAuctionMoney(minimum)}'
                            : 'Valor abaixo do mínimo.';
                      }
                      return null;
                    },
                  ),
                ],
              ),
            ),
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx),
            child: const Text('CANCELAR'),
          ),
          ElevatedButton(
            key: const ValueKey('market-confirm-amount'),
            onPressed: () {
              if (form.currentState!.validate()) {
                Navigator.pop(
                  ctx,
                  parseMarketAmount(controller.text, allowZero: allowZero),
                );
              }
            },
            child: Text(auction ? 'CONFIRMAR LANCE' : 'CONFIRMAR PROPOSTA'),
          ),
        ],
      ),
    );
    // The dialog route may still animate after its future completes.
    await Future<void>.delayed(const Duration(milliseconds: 250));
    controller.dispose();
    return result;
  }

  Future<void> _bid(Map<String, dynamic> p) async {
    final next = p['highestBidderClubId'] == null
        ? p['minimumBid'] as num? ?? 0
        : (p['currentBid'] as num? ?? 0) + 5000;
    final value = await _amountDialog(
      'DAR LANCE',
      p,
      initial: next,
      minimum: next,
      auction: true,
      allowZero: true,
    );
    if (!mounted || value == null || !_canTrade) return;
    await _command(() async {
      await widget.api.placeAuctionBid(
        widget.worldId,
        p['auctionId'].toString(),
        widget.coachId,
        value,
      );
      return 'LANCE REGISTRADO.';
    });
  }

  Future<void> _purchase(Map<String, dynamic> p) async {
    final action = await showDialog<String>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('COMPRAR JOGADOR'),
        insetPadding: const EdgeInsets.symmetric(horizontal: 12, vertical: 16),
        content: SingleChildScrollView(
          child: SizedBox(
            width: 380,
            child: Column(
              mainAxisSize: MainAxisSize.min,
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                _details(p),
                Text(
                  'Contrato vigente: ${p['contractRoundsRemaining']} rodadas.',
                ),
                const SizedBox(height: 12),
                const Text(
                  'Confirme COMPRAR AGORA para contratar imediatamente pelo preço fixo anunciado.',
                ),
              ],
            ),
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx),
            child: const Text('CANCELAR'),
          ),
          OutlinedButton(
            key: const ValueKey('direct-custom-offer'),
            onPressed: () => Navigator.pop(ctx, 'offer'),
            child: const Text('FAZER PROPOSTA'),
          ),
          ElevatedButton(
            key: const ValueKey('direct-confirm-buy'),
            onPressed: () => Navigator.pop(ctx, 'buy'),
            child: const Text('COMPRAR AGORA'),
          ),
        ],
      ),
    );
    if (!mounted || action == null || !_canTrade) return;
    if (action == 'offer') {
      final value = await _amountDialog(
        'FAZER PROPOSTA',
        p,
        initial: (p['askingPrice'] as num? ?? 0) * 0.85,
        minimum: 0.01,
      );
      if (!mounted || value == null || !_canTrade) return;
      await _command(() async {
        final response = await widget.api.makeTransferOffer(
          widget.worldId,
          widget.coachId,
          p['playerId'].toString(),
          value,
        );
        return switch (response['status']) {
          'Accepted' => 'COMPRA CONFIRMADA.',
          'Countered' => 'CONTRAPROPOSTA RECEBIDA.',
          'Rejected' => 'PROPOSTA RECUSADA.',
          _ => 'PROPOSTA ENVIADA.',
        };
      });
    } else {
      await _command(() async {
        await widget.api.buyConfirmedListing(widget.worldId, widget.coachId, p);
        return 'COMPRA CONFIRMADA.';
      });
    }
  }

  Widget _panel(Widget child) => Padding(
    padding: const EdgeInsets.only(bottom: 8),
    child: ClassicPaperPanel(child: child),
  );

  @override
  Widget build(BuildContext context) {
    final auction = pref.tab == 0;
    final rows = orderedMarketPlayers(
      widget.data[auction ? 'auctions' : 'listings'] as List? ?? [],
      auction: auction,
      position: pref.positions[pref.tab],
      order: pref.sorts[pref.tab],
    );
    final ownPlayers = widget.squad
        .where(
          (p) =>
              p['isListedForTransfer'] != true &&
              !(widget.data['auctions'] as List? ?? []).any(
                (a) => a['playerId'] == p['id'] && a['isClosed'] != true,
              ),
        )
        .toList();
    if (!ownPlayers.any((p) => p['id'] == _ownPlayerId)) _ownPlayerId = null;
    final offers = (widget.data['offers'] as List? ?? [])
        .where(
          (o) =>
              o['buyerClubId'] == widget.clubId ||
              o['sellerClubId'] == widget.clubId,
        )
        .toList();
    return SingleChildScrollView(
      padding: const EdgeInsets.all(10),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          const ClassicPageHeader(
            title: 'MERCADO & LEILÃO',
            icon: Icons.swap_horiz,
            subtitle:
                'Anuncie jogadores, acompanhe lances e negocie com os outros clubes.',
          ),
          const SizedBox(height: 8),
          if (!_safe) const Text('Mercado disponível somente entre partidas.'),
          if (widget.clubId == null)
            const Text(
              'Treinador sem clube: consulta disponível; negociações exigem um clube.',
            ),
          if (_message != null)
            _panel(
              Text(
                _message!,
                key: const ValueKey('market-feedback'),
                style: TextStyle(
                  color: ClassicTheme.of(context).accent,
                  fontWeight: FontWeight.bold,
                ),
              ),
            ),
          LayoutBuilder(
            builder: (context, c) => Row(
              children: [
                for (final tab in [(0, 'LEILÃO'), (1, 'COMPRA DIRETA')]) ...[
                  if (tab.$1 > 0) const SizedBox(width: 8),
                  Expanded(
                    child: SizedBox(
                      height: 48,
                      child: ChoiceChip(
                        key: ValueKey('market-tab-${tab.$1}'),
                        label: SizedBox(
                          width: double.infinity,
                          child: Text(tab.$2, textAlign: TextAlign.center),
                        ),
                        selected: pref.tab == tab.$1,
                        selectedColor: ClassicTheme.of(
                          context,
                        ).selectedBackground,
                        backgroundColor: ClassicTheme.of(context).primary,
                        padding: const EdgeInsets.symmetric(
                          horizontal: 8,
                          vertical: 10,
                        ),
                        shape: RoundedRectangleBorder(
                          borderRadius: BorderRadius.circular(6),
                          side: BorderSide(
                            color: ClassicTheme.of(context).border,
                          ),
                        ),
                        labelStyle: TextStyle(
                          color: pref.tab == tab.$1
                              ? ClassicTheme.of(context).selectedText
                              : readableInk(
                                  ClassicTheme.of(context).primary,
                                  Colors.white,
                                ),
                          fontSize: 20,
                          fontWeight: FontWeight.bold,
                        ),
                        onSelected: (_) => setState(() {
                          pref.tab = tab.$1;
                          _message = null;
                        }),
                      ),
                    ),
                  ),
                ],
              ],
            ),
          ),
          const SizedBox(height: 8),
          _panel(
            LayoutBuilder(
              builder: (context, c) => Row(
                children: [
                  Expanded(
                    child: DropdownButtonFormField<String>(
                      key: ValueKey('market-position-${pref.tab}'),
                      isExpanded: true,
                      initialValue: pref.positions[pref.tab],
                      decoration: const InputDecoration(labelText: 'Posição'),
                      items: const [
                        DropdownMenuItem(value: 'ALL', child: Text('TODOS')),
                        DropdownMenuItem(value: 'G', child: Text('GOLEIRO')),
                        DropdownMenuItem(value: 'D', child: Text('DEFESA')),
                        DropdownMenuItem(value: 'M', child: Text('MEIO-CAMPO')),
                        DropdownMenuItem(value: 'A', child: Text('ATAQUE')),
                      ],
                      onChanged: (v) {
                        if (v != null) {
                          setState(() => pref.positions[pref.tab] = v);
                        }
                      },
                    ),
                  ),
                  const SizedBox(width: 10),
                  Expanded(
                    child: DropdownButtonFormField<String>(
                      key: ValueKey('market-sort-${pref.tab}'),
                      isExpanded: true,
                      initialValue: pref.sorts[pref.tab],
                      decoration: const InputDecoration(labelText: 'Ordenação'),
                      items: const [
                        DropdownMenuItem(
                          value: 'PRICE_ASC',
                          child: Text('MENOR PREÇO'),
                        ),
                        DropdownMenuItem(
                          value: 'PRICE_DESC',
                          child: Text('MAIOR PREÇO'),
                        ),
                        DropdownMenuItem(
                          value: 'STRENGTH_DESC',
                          child: Text('MAIOR FORÇA'),
                        ),
                        DropdownMenuItem(
                          value: 'STRENGTH_ASC',
                          child: Text('MENOR FORÇA'),
                        ),
                      ],
                      onChanged: (v) {
                        if (v != null) setState(() => pref.sorts[pref.tab] = v);
                      },
                    ),
                  ),
                ],
              ),
            ),
          ),
          ClassicPaperPanel(
            title:
                '${auction ? "LEILÕES" : "COMPRA DIRETA"} — ${rows.length} jogador(es)',
            padding: EdgeInsets.zero,
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                if (auction)
                  const Text(
                    'Ordenação de preço: maior lance atual; sem lances, usa o lance mínimo.',
                  ),
                if (rows.isEmpty)
                  const Padding(
                    padding: EdgeInsets.all(16),
                    child: Text('Nenhum jogador corresponde aos filtros.'),
                  ),
                if (MediaQuery.sizeOf(context).width >= 900)
                  _marketTable(rows, auction),
                if (MediaQuery.sizeOf(context).width < 900)
                  for (final (index, p) in rows.indexed)
                    Container(
                      color: index.isEven
                          ? ClassicTheme.of(context).paper.tableRow
                          : ClassicTheme.of(context).paper.tableRowAlternate,
                      padding: const EdgeInsets.symmetric(
                        horizontal: 8,
                        vertical: 4,
                      ),
                      child: ExpansionTile(
                        tilePadding: EdgeInsets.zero,
                        childrenPadding: const EdgeInsets.only(top: 8),
                        title: Row(
                          children: [
                            Expanded(
                              flex: 5,
                              child: Column(
                                crossAxisAlignment: CrossAxisAlignment.start,
                                children: [
                                  Text(
                                    '${p['displayName']}',
                                    style: const TextStyle(
                                      fontSize: 18,
                                      fontWeight: FontWeight.bold,
                                    ),
                                  ),
                                  Text(
                                    '${marketPosition(p['position'])} | Força: ${p['strength']}',
                                    style: const TextStyle(fontSize: 14),
                                  ),
                                ],
                              ),
                            ),
                            Expanded(
                              flex: 5,
                              child: Text(
                                auction
                                    ? 'Mín: ${formatAuctionMoney(p['minimumBid'] as num? ?? 0)}\nRodadas: ${_roundsRemaining(p)} • ${p['isClosed'] == true ? 'ENCERRADO' : 'ABERTO'}'
                                    : '${formatAuctionMoney(p['askingPrice'] as num? ?? 0)}\nSalário: ${formatAuctionMoney(p['salaryPerRound'] as num? ?? 0)}',
                                style: const TextStyle(fontSize: 14),
                              ),
                            ),
                            if (auction)
                              ElevatedButton(
                                key: ValueKey('bid-${p['auctionId']}'),
                                onPressed:
                                    !_canTrade ||
                                        p['isClosed'] == true ||
                                        (p['sellerClubId'] == widget.clubId &&
                                            const [
                                              'Voluntary',
                                              'FinancialInsolvency',
                                            ].contains(p['reason']))
                                    ? null
                                    : () => _bid(p),
                                child: const Text('DAR LANCE'),
                              ),
                            if (!auction && p['sellerClubId'] != widget.clubId)
                              ElevatedButton(
                                key: ValueKey('purchase-${p['playerId']}'),
                                onPressed: !_canTrade
                                    ? null
                                    : () => _purchase(p),
                                child: const Text('COMPRAR'),
                              ),
                          ],
                        ),
                        children: [
                          _details(p, auction: auction),
                          if (!auction && p['sellerClubId'] == widget.clubId)
                            Wrap(
                              spacing: 8,
                              children: [
                                const Text('ANÚNCIO DO MEU CLUBE'),
                                OutlinedButton(
                                  onPressed: !_canTrade
                                      ? null
                                      : () => _editListing(p),
                                  child: const Text('ALTERAR PREÇO'),
                                ),
                                TextButton(
                                  onPressed: !_canTrade
                                      ? null
                                      : () => _withdrawListing(p),
                                  child: const Text('RETIRAR ANÚNCIO'),
                                ),
                              ],
                            ),
                        ],
                      ),
                    ),
              ],
            ),
          ),
          const SizedBox(height: 12),
          if (widget.clubId != null)
            _panel(
              Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  const Text(
                    'ANUNCIAR JOGADOR DO MEU CLUBE',
                    style: TextStyle(fontWeight: FontWeight.bold),
                  ),
                  DropdownButtonFormField<String>(
                    key: ValueKey('market-own-player-$_ownPlayerId'),
                    initialValue: _ownPlayerId,
                    isExpanded: true,
                    decoration: const InputDecoration(
                      labelText: 'Jogador próprio',
                    ),
                    items: [
                      for (final p in ownPlayers)
                        DropdownMenuItem(
                          value: p['id'].toString(),
                          child: Text(
                            '${p['displayName'] ?? p['name']} • ${marketPosition(p['position'])} • F${p['strength']}',
                            overflow: TextOverflow.ellipsis,
                          ),
                        ),
                    ],
                    onChanged: !_canTrade
                        ? null
                        : (v) => setState(() => _ownPlayerId = v),
                  ),
                  const SizedBox(height: 8),
                  Wrap(
                    spacing: 8,
                    runSpacing: 8,
                    children: [
                      ElevatedButton(
                        key: const ValueKey('market-create-auction'),
                        onPressed: !_canTrade || _ownPlayerId == null
                            ? null
                            : () => _announce(false),
                        child: const Text('COLOCAR EM LEILÃO'),
                      ),
                      OutlinedButton(
                        key: const ValueKey('market-create-direct'),
                        onPressed: !_canTrade || _ownPlayerId == null
                            ? null
                            : () => _announce(true),
                        child: const Text('VENDA POR PREÇO FIXO'),
                      ),
                    ],
                  ),
                ],
              ),
            ),
          if (!auction) ...[
            const Text(
              'PROPOSTAS E CONTRAPROPOSTAS BILATERAIS',
              style: TextStyle(fontWeight: FontWeight.bold),
            ),
            if (offers.isEmpty)
              const Text('Nenhuma proposta para o seu clube.'),
            for (final o in offers)
              _panel(
                Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      '${o['playerDisplayName']} • ${o['buyerClubName']} → ${o['sellerClubName']}',
                    ),
                    Text(
                      'Proposta: ${formatAuctionMoney(o['offeredAmount'] as num? ?? 0)} • ${_offerStatus(o['status'].toString())}',
                    ),
                    if (o['counterAmount'] is num)
                      Text(
                        'Contraproposta: ${formatAuctionMoney(o['counterAmount'] as num)}',
                      ),
                    if (o['status'] == 'Pending' &&
                            o['sellerClubId'] == widget.clubId ||
                        o['status'] == 'Countered' &&
                            o['buyerClubId'] == widget.clubId)
                      Wrap(
                        spacing: 8,
                        runSpacing: 8,
                        children: [
                          ElevatedButton(
                            onPressed: !_canTrade
                                ? null
                                : () => _respond(o, true),
                            child: const Text('ACEITAR'),
                          ),
                          OutlinedButton(
                            onPressed: !_canTrade
                                ? null
                                : () => _respond(o, false),
                            child: const Text('RECUSAR'),
                          ),
                          if (o['status'] == 'Pending')
                            OutlinedButton(
                              onPressed: !_canTrade ? null : () => _counter(o),
                              child: const Text('CONTRAPROPOSTA'),
                            ),
                        ],
                      ),
                  ],
                ),
              ),
          ],
        ],
      ),
    );
  }

  Widget _marketTable(
    List<Map<String, dynamic>> rows,
    bool auction,
  ) => LayoutBuilder(
    builder: (context, c) => SingleChildScrollView(
      scrollDirection: Axis.horizontal,
      child: ConstrainedBox(
        constraints: BoxConstraints(minWidth: c.maxWidth),
        child: DataTable(
          key: ValueKey(
            auction ? 'market-auctions-table' : 'market-listings-table',
          ),
          headingRowHeight: 36,
          dataRowMinHeight: 40,
          dataRowMaxHeight: 56,
          horizontalMargin: 8,
          columnSpacing: 14,
          columns: [
            for (final label in [
              'JOGADOR',
              'POS',
              'FORÇA',
              'VENDEDOR',
              if (auction) ...[
                'MÍNIMO',
                'MAIOR LANCE',
                'RODADAS',
                'SITUAÇÃO',
              ] else ...[
                'PREÇO FIXO',
                'SALÁRIO',
                'CONTRATO',
              ],
              'AÇÕES',
            ])
              DataColumn(label: Text(label)),
          ],
          rows: [
            for (final (index, p) in rows.indexed)
              DataRow(
                color: WidgetStatePropertyAll(
                  index.isEven
                      ? ClassicTheme.of(context).tableRow
                      : ClassicTheme.of(context).paper.tableRowAlternate,
                ),
                key: ValueKey(p[auction ? 'auctionId' : 'playerId']),
                cells: [
                  DataCell(
                    SizedBox(
                      width: 165,
                      child: Text(p['displayName'].toString(), maxLines: 2),
                    ),
                  ),
                  DataCell(Text(marketPosition(p['position']))),
                  DataCell(Text('${p['strength']}')),
                  DataCell(
                    SizedBox(
                      width: 125,
                      child: Text(
                        '${p['sellerClubName'] ?? "Atleta livre"}',
                        maxLines: 2,
                      ),
                    ),
                  ),
                  if (auction) ...[
                    DataCell(
                      Text(formatAuctionMoney(p['minimumBid'] as num? ?? 0)),
                    ),
                    DataCell(
                      Text(
                        p['highestBidderClubId'] == null
                            ? 'Sem lances'
                            : formatAuctionMoney(p['currentBid'] as num? ?? 0),
                      ),
                    ),
                    DataCell(Text('${_roundsRemaining(p)}')),
                    DataCell(
                      Text(p['isClosed'] == true ? 'ENCERRADO' : 'ABERTO'),
                    ),
                    DataCell(
                      ElevatedButton(
                        key: ValueKey('bid-${p['auctionId']}'),
                        onPressed:
                            !_canTrade ||
                                p['isClosed'] == true ||
                                (p['sellerClubId'] == widget.clubId &&
                                    const [
                                      'Voluntary',
                                      'FinancialInsolvency',
                                    ].contains(p['reason']))
                            ? null
                            : () => _bid(p),
                        child: const Text('DAR LANCE'),
                      ),
                    ),
                  ] else ...[
                    DataCell(
                      Text(formatAuctionMoney(p['askingPrice'] as num? ?? 0)),
                    ),
                    DataCell(
                      Text(
                        formatAuctionMoney(p['salaryPerRound'] as num? ?? 0),
                      ),
                    ),
                    DataCell(Text('${p['contractRoundsRemaining'] ?? "—"}r')),
                    DataCell(
                      p['sellerClubId'] == widget.clubId
                          ? Row(
                              mainAxisSize: MainAxisSize.min,
                              children: [
                                TextButton(
                                  onPressed: !_canTrade
                                      ? null
                                      : () => _editListing(p),
                                  child: const Text('ALTERAR PREÇO'),
                                ),
                                TextButton(
                                  onPressed: !_canTrade
                                      ? null
                                      : () => _withdrawListing(p),
                                  child: const Text('RETIRAR ANÚNCIO'),
                                ),
                              ],
                            )
                          : ElevatedButton(
                              key: ValueKey('purchase-${p['playerId']}'),
                              onPressed: !_canTrade ? null : () => _purchase(p),
                              child: const Text('COMPRAR JOGADOR'),
                            ),
                    ),
                  ],
                ],
              ),
          ],
        ),
      ),
    ),
  );

  Future<void> _editListing(Map<String, dynamic> listing) async {
    final config = await showDialog<VoluntaryAuctionConfig>(
      context: context,
      builder: (_) => AuctionConfigDialog(
        playerName: listing['displayName'] ?? '',
        position: marketPosition(listing['position']),
        strength: listing['strength'] as num?,
        salary: listing['salaryPerRound'] as num?,
        roundNumber: (widget.snapshot['roundNumber'] as num?)?.toInt() ?? 1,
        seasonNumber: (widget.snapshot['seasonNumber'] as num?)?.toInt() ?? 1,
        totalRoundsInSeason:
            (widget.snapshot['totalRoundsInSeason'] as num?)?.toInt() ?? 22,
        initialDirect: true,
        directPrice: listing['askingPrice'] as num,
      ),
    );
    if (!mounted || config == null || !_canTrade) return;
    await _command(() async {
      await widget.api.listPlayerForTransfer(
        widget.worldId,
        widget.coachId,
        listing['playerId'].toString(),
        config.minimumBid,
      );
      return 'PREÇO ATUALIZADO.';
    });
  }

  Future<void> _withdrawListing(Map<String, dynamic> listing) async {
    final confirm = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('RETIRAR ANÚNCIO?'),
        content: Text(listing['displayName'].toString()),
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
    if (!mounted || confirm != true || !_canTrade) return;
    await _command(() async {
      await widget.api.withdrawTransferListing(
        widget.worldId,
        widget.coachId,
        listing['playerId'].toString(),
      );
      return 'ANÚNCIO RETIRADO.';
    });
  }

  String _offerStatus(String s) => switch (s) {
    'Pending' => 'PENDENTE',
    'Accepted' => 'ACEITA',
    'Rejected' => 'RECUSADA',
    'Countered' => 'CONTRAPROPOSTA',
    'Cancelled' => 'CANCELADA',
    _ => s,
  };
  Future<void> _respond(dynamic offer, bool accept) async {
    final approved = await showDialog<bool>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: Text(accept ? 'CONFIRMAR NEGOCIAÇÃO?' : 'RECUSAR PROPOSTA?'),
        content: Text(
          '${offer['playerDisplayName']} • ${formatAuctionMoney((offer['counterAmount'] ?? offer['offeredAmount']) as num)}',
        ),
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
    if (!mounted || approved != true || !_canTrade) return;
    await _command(() async {
      await widget.api.respondTransferOffer(
        widget.worldId,
        offer['offerId'].toString(),
        widget.coachId,
        accept,
      );
      return accept ? 'COMPRA CONFIRMADA.' : 'PROPOSTA RECUSADA.';
    });
  }

  Future<void> _counter(dynamic offer) async {
    final listing = (widget.data['listings'] as List? ?? [])
        .where((p) => p['playerId'] == offer['playerId'])
        .firstOrNull;
    final own = widget.squad
        .where((p) => p['id'] == offer['playerId'])
        .firstOrNull;
    final details = <String, dynamic>{
      ...Map<String, dynamic>.from(offer),
      if (own != null) ...Map<String, dynamic>.from(own),
      if (listing != null) ...Map<String, dynamic>.from(listing),
      'displayName': offer['playerDisplayName'],
    };
    final value = await _amountDialog(
      'CONTRAPROPOSTA',
      details,
      initial: offer['offeredAmount'] as num,
      minimum: 0.01,
    );
    if (!mounted || value == null || !_canTrade) return;
    await _command(() async {
      await widget.api.counterTransferOffer(
        widget.worldId,
        offer['offerId'].toString(),
        widget.coachId,
        value,
      );
      return 'CONTRAPROPOSTA ENVIADA.';
    });
  }
}
