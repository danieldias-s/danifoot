import 'dart:math' as math;

import 'package:flutter/material.dart';

import 'catalog_provenance.dart';
import 'club_crest.dart';
import 'club_theme.dart';
import 'creator_visual.dart';

/// The last two creator steps present the server preview without rebuilding
/// rankings or owning assignments. All changes are delegated to the creator.
class CreatorWorldReview extends StatefulWidget {
  final Map<String, dynamic> preview;
  final String worldName, rosterMode;
  final List<String> years;
  final Map<String, String> countries;
  final int maxHumans;
  final bool confirmation, busy, synchronized;
  final Map<String, dynamic> initialWorld;
  final VoidCallback? onRefresh, onEditDivisions;
  final ValueChanged<String>? onSwapClub;

  const CreatorWorldReview({
    super.key,
    required this.preview,
    required this.worldName,
    required this.years,
    required this.countries,
    required this.rosterMode,
    required this.maxHumans,
    required this.confirmation,
    this.busy = false,
    this.synchronized = false,
    this.initialWorld = const {},
    this.onRefresh,
    this.onEditDivisions,
    this.onSwapClub,
  });

  @override
  State<CreatorWorldReview> createState() => _CreatorWorldReviewState();
}

class _CreatorWorldReviewState extends State<CreatorWorldReview> {
  final _districtSearch = TextEditingController();
  final _districtTextScroll = ScrollController(keepScrollOffset: false);
  String _districtCountry = '';
  int _districtPage = 0;
  static const _pageSize = 10;

  List<Map<String, dynamic>> _maps(dynamic value) => (value as List? ?? [])
      .whereType<Map>()
      .map((item) => Map<String, dynamic>.from(item))
      .toList();

  List<Map<String, dynamic>> get _entries => _maps(widget.preview['clubs']);

  List<Map<String, dynamic>> _division(int division) =>
      _entries.where((entry) => entry['division'] == division).toList();

  Map<String, dynamic> _club(Map<String, dynamic> entry) =>
      Map<String, dynamic>.from(entry['club'] as Map? ?? entry);

  String _country(Map<String, dynamic> club) =>
      widget.countries[club['countryCode']] ??
      (club['countryName'] ?? club['countryCode'] ?? 'País não informado')
          .toString();

  String _name(Map<String, dynamic> club) {
    final name = (club['name'] ?? 'Clube sem nome').toString();
    final year = club['season']?.toString();
    return widget.years.length > 1 &&
            year != null &&
            year.isNotEmpty &&
            !name.endsWith(' ($year)')
        ? '$name ($year)'
        : name;
  }

  String _divisionName(int division) =>
      division == 5 ? 'DISTRITAIS' : '$divisionª DIVISÃO';

  List<Map<String, dynamic>> get _players => [
    for (final entry in _entries) ..._maps(_club(entry)['players']),
  ];

  bool _generated(Map<String, dynamic> player) =>
      player['playerOrigin'] == 'FICTIONAL_GENERATED' ||
      player['identityOrigin'] == 'Generated' ||
      (player['catalogIdentity'] is Map &&
          player['catalogIdentity']['origin'] == 'FICTIONAL_GENERATED');

  bool _real(Map<String, dynamic> player) {
    if (_generated(player)) return false;
    final identity = player['catalogIdentity'];
    final origin =
        player['playerOrigin'] ?? (identity is Map ? identity['origin'] : null);
    return origin?.toString().startsWith('REAL_') == true ||
        player['identityOrigin'] == 'Real';
  }

  ClassicTheme _theme(BuildContext context) {
    final inherited = ClassicTheme.of(context);
    // These are modern creator panels even when the game uses its old skin.
    return (inherited.classicPresentation
            ? ClassicTheme.classicGreen()
            : inherited)
        .paper;
  }

  @override
  void didUpdateWidget(covariant CreatorWorldReview oldWidget) {
    super.didUpdateWidget(oldWidget);
    if (_districtCountry.isNotEmpty &&
        !_division(
          5,
        ).any((entry) => _club(entry)['countryCode'] == _districtCountry)) {
      _districtCountry = '';
      _districtPage = 0;
    }
  }

  @override
  void dispose() {
    _districtSearch.dispose();
    _districtTextScroll.dispose();
    super.dispose();
  }

  TextStyle _heading(ClassicTheme theme, double size) => TextStyle(
    fontFamily: 'ClassicCondensed',
    fontWeight: FontWeight.w700,
    fontSize: size,
    color: theme.textPrimary,
    height: 1.1,
  );

  ButtonStyle _outline(ClassicTheme theme) => OutlinedButton.styleFrom(
    foregroundColor: theme.accent,
    minimumSize: const Size(0, 44),
    padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 12),
    textStyle: const TextStyle(
      fontFamily: 'ClassicCondensed',
      fontWeight: FontWeight.w700,
      fontSize: 16,
    ),
    side: BorderSide(color: theme.accent),
    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(9)),
  );

  Widget _actions(ClassicTheme theme, bool mobile) {
    final actions = [
      OutlinedButton.icon(
        key: const ValueKey('review-refresh'),
        onPressed: widget.busy ? null : widget.onRefresh,
        style: _outline(theme),
        icon: const Icon(Icons.refresh, size: 20),
        label: const Text('ATUALIZAR PRÉVIA'),
      ),
      OutlinedButton.icon(
        key: const ValueKey('review-edit-divisions'),
        onPressed: widget.busy ? null : widget.onEditDivisions,
        style: _outline(theme),
        icon: const Icon(Icons.edit_outlined, size: 20),
        label: const Text('EDITAR DIVISÕES'),
      ),
    ];
    if (mobile) {
      return Row(
        children: [
          for (final (index, action) in actions.indexed) ...[
            if (index > 0) const SizedBox(width: 8),
            Expanded(child: action),
          ],
        ],
      );
    }
    return Wrap(spacing: 10, runSpacing: 8, children: actions);
  }

  Widget _intro(ClassicTheme theme, bool mobile) {
    final title = Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Icon(Icons.assignment_outlined, color: theme.accent, size: 30),
        const SizedBox(width: 12),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text('REVISÃO DO SEU MUNDO', style: _heading(theme, 25)),
              const SizedBox(height: 7),
              Text(
                'Confira como a Liga Clássica 98 vai começar. '
                'Revise as configurações, as divisões e os clubes '
                'antes de avançar para a criação.',
                style: TextStyle(color: theme.textSecondary, fontSize: 15),
              ),
            ],
          ),
        ),
      ],
    );
    if (mobile) return title;
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Expanded(child: title),
        const SizedBox(width: 24),
        _actions(theme, false),
      ],
    );
  }

  Widget _finalHero(ClassicTheme theme, bool mobile) => Container(
    key: const ValueKey('review-confirmation-hero'),
    padding: EdgeInsets.all(mobile ? 14 : 20),
    decoration: BoxDecoration(
      borderRadius: BorderRadius.circular(12),
      border: Border.all(color: theme.border),
      gradient: LinearGradient(colors: [theme.surfaceAlternate, theme.surface]),
    ),
    child: Row(
      children: [
        Container(
          width: mobile ? 45 : 90,
          height: mobile ? 54 : 70,
          margin: EdgeInsets.only(right: mobile ? 12 : 20),
          decoration: BoxDecoration(
            borderRadius: BorderRadius.circular(10),
            gradient: LinearGradient(
              colors: [theme.primary, theme.primary, theme.selectedBackground],
              stops: const [0, .92, .92],
            ),
          ),
          child: Icon(
            Icons.sports_soccer,
            color: Colors.white,
            size: mobile ? 31 : 48,
          ),
        ),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                widget.worldName,
                style: _heading(
                  theme,
                  mobile ? 27 : 34,
                ).copyWith(fontStyle: FontStyle.italic),
              ),
              const SizedBox(height: 4),
              Text(
                'REVISÃO FINAL DA ESTRUTURA DO SEU MUNDO',
                style: TextStyle(color: theme.textSecondary, fontSize: 14),
              ),
            ],
          ),
        ),
        if (!mobile) ...[
          const SizedBox(width: 20),
          ConstrainedBox(
            constraints: const BoxConstraints(maxWidth: 220),
            child: _stat(
              theme,
              Icons.calendar_month_outlined,
              'Anos do catálogo',
              widget.years.join(', '),
              'confirmation-years',
            ),
          ),
        ],
      ],
    ),
  );

  Widget _stat(
    ClassicTheme theme,
    IconData icon,
    String label,
    String value,
    String key,
  ) => Container(
    key: ValueKey('review-summary-$key'),
    padding: const EdgeInsets.symmetric(horizontal: 12, vertical: 13),
    decoration: BoxDecoration(
      color: theme.surfaceAlternate.withValues(alpha: .58),
      borderRadius: BorderRadius.circular(9),
      border: Border.all(color: theme.border.withValues(alpha: .6)),
    ),
    child: Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Icon(icon, size: 24, color: theme.accent),
        const SizedBox(width: 10),
        Expanded(
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(
                label,
                style: TextStyle(fontSize: 13, color: theme.textSecondary),
              ),
              const SizedBox(height: 4),
              Text(
                value.isEmpty ? 'Não informado' : value,
                style: _heading(theme, 20),
              ),
            ],
          ),
        ),
      ],
    ),
  );

  Widget _summary(ClassicTheme theme, double width) {
    final mainDivisions = _entries
        .map((entry) => entry['division'])
        .where((division) => division is int && division >= 1 && division <= 4)
        .toSet()
        .length;
    final stats = <(IconData, String, String, String)>[
      if (!widget.confirmation)
        (
          Icons.emoji_events_outlined,
          'Mundo / Competição',
          widget.worldName,
          'world',
        ),
      (
        Icons.calendar_month_outlined,
        'Anos selecionados',
        widget.years.join(', '),
        'years',
      ),
      (
        Icons.flag_outlined,
        'Países',
        widget.countries.values.join(', '),
        'countries',
      ),
      (Icons.account_tree_outlined, 'Divisões', '$mainDivisions', 'divisions'),
      (
        Icons.shield_outlined,
        'Distritais',
        '${_division(5).length}',
        'districts',
      ),
      (Icons.groups_outlined, 'Modo de elencos', widget.rosterMode, 'rosters'),
      (Icons.shield_outlined, 'Total de clubes', '${_entries.length}', 'clubs'),
      (
        Icons.people_outline,
        'Total de jogadores',
        '${_players.length}',
        'players',
      ),
    ];
    // Eight narrow desktop cards follow the reference; smaller displays use
    // four or two readable columns without imposing a fixed card height.
    final columns = width >= 1220
        ? stats.length
        : width >= 700
        ? 4
        : 2;
    const gap = 9.0;
    final cardWidth = (width - (columns - 1) * gap) / columns;
    return Wrap(
      spacing: gap,
      runSpacing: gap,
      children: [
        for (final item in stats)
          SizedBox(
            width: cardWidth,
            child: _stat(theme, item.$1, item.$2, item.$3, item.$4),
          ),
      ],
    );
  }

  Widget _playerCounts(ClassicTheme theme) {
    final players = _players;
    final real = players.where(_real).length;
    final generated = players.where(_generated).length;
    final unknown = players.length - real - generated;
    return Padding(
      padding: const EdgeInsets.only(top: 10),
      child: Text(
        '$real jogadores do catálogo • $generated fictícios gerados'
        '${unknown > 0 ? ' • $unknown com origem a conferir' : ''}',
        key: const ValueKey('review-player-origins'),
        style: TextStyle(color: theme.textSecondary, fontSize: 14),
      ),
    );
  }

  Widget _notice(ClassicTheme theme) => Container(
    padding: const EdgeInsets.all(13),
    decoration: BoxDecoration(
      color: const Color(0xFFFFF3C4),
      borderRadius: BorderRadius.circular(9),
    ),
    child: const Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Icon(Icons.info_outline, color: Color(0xFF294B2F), size: 23),
        SizedBox(width: 10),
        Expanded(
          child: Text(
            'Você está revisando a estrutura final do mundo. '
            'Confira as divisões, os clubes e os principais dados antes de confirmar a criação.',
            style: TextStyle(color: Color(0xFF102A1C), fontSize: 15),
          ),
        ),
      ],
    ),
  );

  Widget _flag(Map<String, dynamic> club) => Tooltip(
    message: _country(club),
    child: Semantics(
      label: 'Bandeira: ${_country(club)}',
      child: CreatorCountryFlag(
        code: club['countryCode']?.toString() ?? '',
        countryName: _country(club),
        width: 27,
        height: 19,
      ),
    ),
  );

  Widget? _crest(Map<String, dynamic> club) {
    final palette = club['palette'] ?? club['colors'];
    final colors = palette is Map
        ? Map<String, dynamic>.from(palette)
        : club['primaryColor'] != null
        ? club
        : null;
    if (colors == null) return null;
    return Padding(
      padding: const EdgeInsets.only(right: 8),
      child: ClubCrest(club: _name(club), palette: colors, size: 25),
    );
  }

  Future<void> _showRoster(
    Map<String, dynamic> club,
    ClassicTheme theme,
  ) => showDialog<void>(
    context: context,
    builder: (dialogContext) => Theme(
      data: theme.material(),
      child: AlertDialog(
        title: Text('ELENCO • ${_name(club)}', style: _heading(theme, 25)),
        content: SizedBox(
          width: 610,
          child: SingleChildScrollView(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              mainAxisSize: MainAxisSize.min,
              children: [
                Text(
                  '${_country(club)} • ${_maps(club['players']).length} jogadores',
                ),
                const SizedBox(height: 10),
                if (_maps(club['players']).isEmpty)
                  const Text('Nenhum jogador informado nesta prévia.'),
                ..._maps(club['players']).map(catalogPlayerRow),
                const Divider(),
                catalogRankingLabel(club),
                const SizedBox(height: 8),
                catalogClubStatus(club),
              ],
            ),
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(dialogContext),
            child: const Text('FECHAR'),
          ),
        ],
      ),
    ),
  );

  Widget _clubRow(
    ClassicTheme theme,
    Map<String, dynamic> entry,
    int position, {
    required bool compact,
  }) {
    final club = _club(entry);
    final key = club['key']?.toString();
    final crest = _crest(club);
    return Container(
      key: ValueKey('review-club-${entry['division']}-$key'),
      padding: EdgeInsets.symmetric(horizontal: compact ? 7 : 10, vertical: 4),
      decoration: BoxDecoration(
        border: Border(
          bottom: BorderSide(color: theme.border.withValues(alpha: .45)),
        ),
      ),
      child: Row(
        children: [
          SizedBox(
            width: 27,
            child: Text(
              '$position',
              style: TextStyle(
                fontWeight: FontWeight.w700,
                color: theme.accent,
              ),
            ),
          ),
          _flag(club),
          const SizedBox(width: 9),
          ?crest,
          Expanded(
            child: InkWell(
              onTap: () => _showRoster(club, theme),
              borderRadius: BorderRadius.circular(5),
              child: Padding(
                padding: const EdgeInsets.symmetric(vertical: 9),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Tooltip(
                      message: _name(club),
                      child: Text(
                        _name(club),
                        maxLines: compact ? 3 : 2,
                        overflow: TextOverflow.ellipsis,
                        style: _heading(theme, compact ? 18 : 19),
                      ),
                    ),
                    Text(
                      _country(club),
                      style: TextStyle(
                        color: theme.textSecondary,
                        fontSize: 13,
                      ),
                    ),
                  ],
                ),
              ),
            ),
          ),
          IconButton(
            tooltip: 'Consultar elenco de ${_name(club)}',
            onPressed: () => _showRoster(club, theme),
            color: theme.accent,
            icon: const Icon(Icons.groups_outlined, size: 20),
            constraints: const BoxConstraints(minWidth: 44, minHeight: 44),
          ),
          if (widget.confirmation && widget.onSwapClub != null && key != null)
            compact
                ? IconButton.outlined(
                    key: ValueKey('review-swap-$key'),
                    tooltip: 'Trocar ${_name(club)}',
                    onPressed: widget.busy
                        ? null
                        : () => widget.onSwapClub!(key),
                    color: theme.accent,
                    icon: const Icon(Icons.swap_horiz, size: 21),
                    constraints: const BoxConstraints(
                      minWidth: 44,
                      minHeight: 44,
                    ),
                  )
                : OutlinedButton.icon(
                    key: ValueKey('review-swap-$key'),
                    onPressed: widget.busy
                        ? null
                        : () => widget.onSwapClub!(key),
                    style: _outline(theme).copyWith(
                      padding: const WidgetStatePropertyAll(
                        EdgeInsets.symmetric(horizontal: 10),
                      ),
                    ),
                    icon: const Icon(Icons.swap_horiz, size: 18),
                    label: const Text('TROCAR'),
                  ),
        ],
      ),
    );
  }

  Widget _divisionRows(
    ClassicTheme theme,
    List<Map<String, dynamic>> entries,
    double width,
  ) {
    if (entries.isEmpty) {
      return const Padding(
        padding: EdgeInsets.all(16),
        child: Text('Nenhum clube nesta divisão.'),
      );
    }
    final compact = width < 760;
    if (compact) {
      return Column(
        children: [
          for (final (index, entry) in entries.indexed)
            _clubRow(theme, entry, index + 1, compact: true),
        ],
      );
    }
    final split = (entries.length / 2).ceil();
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        for (var column = 0; column < 2; column++) ...[
          if (column > 0) const SizedBox(width: 20),
          Expanded(
            child: Column(
              children: [
                for (
                  var index = column * split;
                  index < math.min((column + 1) * split, entries.length);
                  index++
                )
                  _clubRow(theme, entries[index], index + 1, compact: false),
              ],
            ),
          ),
        ],
      ],
    );
  }

  Widget _districtRows(ClassicTheme theme, double width) {
    final entries = _division(5);
    final countries = <String, String>{
      for (final entry in entries)
        if (_club(entry)['countryCode'] != null)
          _club(entry)['countryCode'].toString(): _country(_club(entry)),
    };
    final query = _districtSearch.text.trim().toLowerCase();
    final filtered = entries.where((entry) {
      final club = _club(entry);
      return (_districtCountry.isEmpty ||
              club['countryCode'] == _districtCountry) &&
          (query.isEmpty || _name(club).toLowerCase().contains(query));
    }).toList();
    final pages = math.max(1, (filtered.length / _pageSize).ceil());
    final page = _districtPage.clamp(0, pages - 1);
    final from = page * _pageSize;
    final until = math.min(from + _pageSize, filtered.length);
    final search = TextField(
      key: const ValueKey('review-district-search'),
      controller: _districtSearch,
      scrollController: _districtTextScroll,
      onChanged: (_) => setState(() => _districtPage = 0),
      decoration: const InputDecoration(
        labelText: 'Pesquisar clube distrital',
        prefixIcon: Icon(Icons.search),
        border: OutlineInputBorder(),
        isDense: true,
      ),
    );
    final filter = DropdownButtonFormField<String>(
      key: const ValueKey('review-district-country'),
      initialValue: countries.containsKey(_districtCountry)
          ? _districtCountry
          : '',
      isExpanded: true,
      decoration: const InputDecoration(
        labelText: 'País',
        border: OutlineInputBorder(),
        isDense: true,
      ),
      items: [
        const DropdownMenuItem(value: '', child: Text('Todos os países')),
        for (final entry in countries.entries)
          DropdownMenuItem(
            value: entry.key,
            child: Text(entry.value, overflow: TextOverflow.ellipsis),
          ),
      ],
      onChanged: (value) => setState(() {
        _districtCountry = value ?? '';
        _districtPage = 0;
      }),
    );
    return Padding(
      padding: const EdgeInsets.symmetric(horizontal: 9, vertical: 12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          if (width >= 600)
            Row(
              children: [
                Expanded(flex: 3, child: search),
                const SizedBox(width: 12),
                Expanded(flex: 2, child: filter),
              ],
            )
          else ...[
            search,
            const SizedBox(height: 10),
            filter,
          ],
          const SizedBox(height: 12),
          if (filtered.isEmpty)
            const Padding(
              padding: EdgeInsets.all(14),
              child: Text('Nenhum clube distrital corresponde à pesquisa.'),
            ),
          for (var index = from; index < until; index++)
            _clubRow(
              theme,
              filtered[index],
              entries.indexOf(filtered[index]) + 1,
              compact: width < 760,
            ),
          const SizedBox(height: 8),
          Wrap(
            alignment: WrapAlignment.spaceBetween,
            crossAxisAlignment: WrapCrossAlignment.center,
            spacing: 10,
            children: [
              Text(
                filtered.isEmpty
                    ? '0 de ${entries.length} clubes'
                    : 'Mostrando ${from + 1}–$until de ${filtered.length} clubes',
                key: const ValueKey('review-district-count'),
                style: TextStyle(color: theme.textSecondary, fontSize: 14),
              ),
              Row(
                mainAxisSize: MainAxisSize.min,
                children: [
                  IconButton(
                    key: const ValueKey('review-district-previous'),
                    tooltip: 'Página anterior de distritais',
                    onPressed: page == 0
                        ? null
                        : () => setState(() => _districtPage = page - 1),
                    icon: const Icon(Icons.chevron_left),
                  ),
                  Text(
                    '${page + 1}/$pages',
                    style: TextStyle(
                      color: theme.accent,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                  IconButton(
                    key: const ValueKey('review-district-next'),
                    tooltip: 'Próxima página de distritais',
                    onPressed: page + 1 >= pages
                        ? null
                        : () => setState(() => _districtPage = page + 1),
                    icon: const Icon(Icons.chevron_right),
                  ),
                ],
              ),
            ],
          ),
        ],
      ),
    );
  }

  Widget _divisionPanel(ClassicTheme theme, int division, double width) {
    final entries = _division(division);
    final district = division == 5;
    final capacity = district ? 96 : 8;
    final compact = width < 700;
    final count = '${entries.length}/$capacity';
    return Container(
      margin: const EdgeInsets.only(bottom: 10),
      decoration: BoxDecoration(
        borderRadius: BorderRadius.circular(11),
        border: Border.all(color: theme.border.withValues(alpha: .8)),
        color: theme.surface,
      ),
      child: ClipRRect(
        borderRadius: BorderRadius.circular(10),
        child: ExpansionTile(
          key: PageStorageKey(
            'review-${widget.confirmation ? 'confirm' : 'preview'}-division-$division',
          ),
          initiallyExpanded: division == 1,
          tilePadding: EdgeInsets.symmetric(
            horizontal: compact ? 11 : 18,
            vertical: 4,
          ),
          childrenPadding: const EdgeInsets.symmetric(
            horizontal: 10,
            vertical: 6,
          ),
          backgroundColor: theme.surface,
          collapsedBackgroundColor: theme.surfaceAlternate,
          iconColor: theme.accent,
          collapsedIconColor: theme.accent,
          shape: const Border(),
          collapsedShape: const Border(),
          title: Row(
            children: [
              Icon(
                district
                    ? Icons.shield_outlined
                    : widget.confirmation
                    ? Icons.emoji_events_outlined
                    : Icons.layers_outlined,
                size: compact ? 23 : 28,
                color: theme.accent,
              ),
              const SizedBox(width: 10),
              Expanded(
                child: Wrap(
                  spacing: compact ? 10 : 20,
                  runSpacing: 6,
                  crossAxisAlignment: WrapCrossAlignment.center,
                  children: [
                    Text(
                      widget.confirmation
                          ? _divisionName(division)
                          : district
                          ? 'DISTRITAIS'
                          : 'DIV$division',
                      style: _heading(theme, compact ? 21 : 25),
                    ),
                    Container(
                      key: ValueKey('review-division-count-$division'),
                      padding: const EdgeInsets.symmetric(
                        horizontal: 9,
                        vertical: 3,
                      ),
                      decoration: BoxDecoration(
                        color: theme.buttonBackground,
                        borderRadius: BorderRadius.circular(14),
                      ),
                      child: Text(
                        count,
                        style: TextStyle(
                          color: readableInk(theme.buttonBackground),
                          fontWeight: FontWeight.w700,
                          fontSize: 16,
                        ),
                      ),
                    ),
                    if (!compact)
                      Text(
                        '${entries.length} clubes • ${district ? 'Competições distritais (base do sistema)' : '$divisionº nível da competição'}',
                        style: TextStyle(
                          color: theme.textSecondary,
                          fontSize: 16,
                        ),
                      ),
                  ],
                ),
              ),
            ],
          ),
          children: [
            if (district)
              _districtRows(theme, width - 20)
            else
              _divisionRows(theme, entries, width - 20),
          ],
        ),
      ),
    );
  }

  Widget _initialWorld(ClassicTheme theme) {
    final details = widget.initialWorld;
    final extra = <(String, dynamic)>[
      ('Temporada inicial', details['season']),
      ('Data inicial', details['startDate']),
      ('Primeira rodada', details['round']),
      (
        'Competições',
        details['competitions'] is List
            ? (details['competitions'] as List).join(', ')
            : details['competitions'],
      ),
    ];
    return Container(
      key: const ValueKey('review-initial-world'),
      padding: const EdgeInsets.all(16),
      decoration: BoxDecoration(
        color: theme.surfaceAlternate.withValues(alpha: .65),
        borderRadius: BorderRadius.circular(10),
        border: Border.all(color: theme.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            children: [
              Icon(Icons.flag_outlined, color: theme.accent),
              const SizedBox(width: 9),
              Expanded(
                child: Text('INÍCIO DO MUNDO', style: _heading(theme, 23)),
              ),
            ],
          ),
          const SizedBox(height: 12),
          Wrap(
            spacing: 24,
            runSpacing: 8,
            children: [
              for (var division = 1; division <= 5; division++)
                Text(
                  '${_divisionName(division)}: ${_division(division).length} clubes',
                  style: TextStyle(color: theme.textPrimary),
                ),
            ],
          ),
          const Divider(height: 24),
          Text(
            'Treinadores humanos permitidos: ${widget.maxHumans}',
            style: TextStyle(color: theme.textPrimary),
          ),
          if (widget.synchronized)
            Text(
              'Modo de jogo: Clássico sincronizado',
              style: TextStyle(color: theme.textPrimary),
            ),
          for (final entry in extra)
            if (entry.$2 != null && entry.$2.toString().isNotEmpty)
              Text(
                '${entry.$1}: ${entry.$2}',
                style: TextStyle(color: theme.textPrimary),
              ),
        ],
      ),
    );
  }

  Widget _warnings(ClassicTheme theme) {
    final errors = widget.preview['errors'] as List? ?? [];
    final warnings = widget.preview['warnings'] as List? ?? [];
    final messages = [
      for (final error in errors) (message: error, severity: 'Bloqueante'),
      for (final warning in warnings)
        (
          message: warning,
          severity:
              warning is Map &&
                  [
                    'Info',
                    'Information',
                    'Informational',
                    'Informativo',
                  ].contains(warning['severity'])
              ? 'Informativo'
              : 'Atenção',
        ),
    ];
    return Container(
      key: const ValueKey('review-important-warnings'),
      decoration: BoxDecoration(
        border: Border.all(color: theme.border),
        borderRadius: BorderRadius.circular(10),
      ),
      child: ExpansionTile(
        initiallyExpanded: errors.isNotEmpty,
        shape: const Border(),
        collapsedShape: const Border(),
        iconColor: theme.accent,
        collapsedIconColor: theme.accent,
        leading: Icon(
          errors.isNotEmpty ? Icons.error_outline : Icons.info_outline,
          color: errors.isNotEmpty ? const Color(0xFFB3261E) : theme.accent,
        ),
        title: Text('AVISOS IMPORTANTES', style: _heading(theme, 22)),
        subtitle: Text(
          errors.isNotEmpty
              ? '${errors.length} bloqueante(s) • ${warnings.length} aviso(s)'
              : '${warnings.length} aviso(s)',
          style: TextStyle(color: theme.textSecondary),
        ),
        childrenPadding: const EdgeInsets.fromLTRB(14, 0, 14, 14),
        children: [
          if (messages.isEmpty)
            const Align(
              alignment: Alignment.centerLeft,
              child: Text('Nenhum aviso informado nesta prévia.'),
            ),
          for (final item in messages)
            Container(
              width: double.infinity,
              margin: const EdgeInsets.only(top: 8),
              padding: const EdgeInsets.all(12),
              decoration: BoxDecoration(
                color: item.severity == 'Bloqueante'
                    ? const Color(0xFFFFEBE9)
                    : item.severity == 'Informativo'
                    ? theme.surfaceAlternate
                    : const Color(0xFFFFF3C4),
                borderRadius: BorderRadius.circular(7),
              ),
              child: Text(
                '${item.severity}: ${item.message is Map ? item.message['message'] ?? item.message['text'] ?? item.message : item.message}',
                style: TextStyle(
                  color: item.severity == 'Bloqueante'
                      ? const Color(0xFF84201A)
                      : item.severity == 'Informativo'
                      ? theme.textPrimary
                      : const Color(0xFF102A1C),
                  fontSize: 15,
                ),
              ),
            ),
          if (widget.preview['dataAsOf'] != null)
            Padding(
              padding: const EdgeInsets.only(top: 12),
              child: Align(
                alignment: Alignment.centerLeft,
                child: Text(
                  'Prévia validada em ${widget.preview['dataAsOf']}',
                  style: TextStyle(color: theme.textSecondary, fontSize: 13),
                ),
              ),
            ),
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final theme = _theme(context);
    return Theme(
      data: theme.material(),
      child: LayoutBuilder(
        builder: (context, constraints) {
          final width = constraints.maxWidth;
          final mobile = width < 900;
          return DefaultTextStyle(
            style: TextStyle(
              color: theme.textPrimary,
              fontFamily: 'ClassicCondensed',
              fontSize: 16,
            ),
            child: Column(
              key: ValueKey(
                widget.confirmation
                    ? 'creator-confirmation-review'
                    : 'creator-preview-review',
              ),
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                if (widget.confirmation)
                  _finalHero(theme, mobile)
                else
                  _intro(theme, mobile),
                const SizedBox(height: 20),
                _summary(theme, width),
                _playerCounts(theme),
                if (!widget.confirmation && mobile) ...[
                  const SizedBox(height: 16),
                  _actions(theme, true),
                ],
                const SizedBox(height: 20),
                if (widget.confirmation) ...[
                  _notice(theme),
                  const SizedBox(height: 18),
                ],
                for (var division = 1; division <= 5; division++)
                  _divisionPanel(theme, division, width),
                if (widget.confirmation) ...[
                  const SizedBox(height: 8),
                  _initialWorld(theme),
                  const SizedBox(height: 14),
                ],
                _warnings(theme),
              ],
            ),
          );
        },
      ),
    );
  }
}
