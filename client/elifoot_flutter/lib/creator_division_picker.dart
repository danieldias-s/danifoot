import 'package:flutter/material.dart';

import 'club_theme.dart';
import 'creator_visual.dart';

/// Opens the swap picker. With no [currentClub], it lists unassigned clubs
/// without offering any changes to the lineup.
Future<String?> showCreatorDivisionPicker(
  BuildContext context, {
  required List<List<Map<String, dynamic>?>> divisions,
  required List<Map<String, dynamic>> eligibleClubs,
  Map<String, dynamic>? currentClub,
  bool? mobile,
}) {
  final compact = mobile ?? MediaQuery.sizeOf(context).width < 600;
  final theme = ClassicTheme.of(context).paper;
  final shape = RoundedRectangleBorder(
    borderRadius: BorderRadius.circular(16),
    side: BorderSide(color: theme.selectedBackground),
  );
  final picker = Theme(
    data: theme.material(),
    child: _DivisionPicker(
      divisions: divisions,
      eligibleClubs: eligibleClubs,
      currentClub: currentClub,
      mobile: compact,
    ),
  );

  if (!compact) {
    return showDialog<String>(
      context: context,
      builder: (context) => Dialog(
        insetPadding: const EdgeInsets.symmetric(horizontal: 24, vertical: 18),
        shape: shape,
        clipBehavior: Clip.antiAlias,
        backgroundColor: theme.surface,
        child: SizedBox(width: 680, height: 690, child: picker),
      ),
    );
  }

  return showModalBottomSheet<String>(
    context: context,
    isScrollControlled: true,
    useSafeArea: true,
    backgroundColor: Colors.transparent,
    builder: (context) {
      final keyboard = MediaQuery.viewInsetsOf(context).bottom;
      return Padding(
        padding: EdgeInsets.only(bottom: keyboard),
        child: FractionallySizedBox(
          heightFactor: keyboard > 0 ? 1 : .92,
          widthFactor: 1,
          child: Material(
            shape: shape,
            clipBehavior: Clip.antiAlias,
            color: theme.surface,
            child: SafeArea(top: false, child: picker),
          ),
        ),
      );
    },
  );
}

typedef _DivisionPosition = ({int division, int position});

class _DivisionPicker extends StatefulWidget {
  final List<List<Map<String, dynamic>?>> divisions;
  final List<Map<String, dynamic>> eligibleClubs;
  final Map<String, dynamic>? currentClub;
  final bool mobile;

  const _DivisionPicker({
    required this.divisions,
    required this.eligibleClubs,
    required this.currentClub,
    required this.mobile,
  });

  @override
  State<_DivisionPicker> createState() => _DivisionPickerState();
}

class _DivisionPickerState extends State<_DivisionPicker> {
  static const _allDivisions = 0;
  static const _unassigned = -1;
  final _positions = <String, _DivisionPosition>{};
  late final List<Map<String, dynamic>> _candidates;
  String _query = '';
  String? _selectedKey;
  int _filter = _allDivisions;
  String _countryFilter = '', _yearFilter = '';

  bool get _exploring => widget.currentClub == null;

  @override
  void initState() {
    super.initState();
    for (final (division, clubs) in widget.divisions.indexed) {
      for (final (position, club) in clubs.indexed) {
        final key = club?['key']?.toString();
        if (key != null) {
          _positions[key] = (division: division + 1, position: position + 1);
        }
      }
    }
    final currentKey = widget.currentClub?['key']?.toString();
    _candidates = widget.eligibleClubs.where((club) {
      final key = club['key']?.toString();
      return key != null &&
          key != currentKey &&
          (!_exploring || !_positions.containsKey(key));
    }).toList();
  }

  String _country(Map<String, dynamic> club) =>
      (club['countryName'] ?? club['countryCode'] ?? '').toString();

  String _countryCode(Map<String, dynamic> club) =>
      club['countryCode']?.toString() ?? '';

  String _year(Map<String, dynamic> club) => club['season']?.toString() ?? '';

  String _location(_DivisionPosition? location) => location == null
      ? 'Não escalado'
      : '${location.division}ª Divisão • Posição ${location.position}';

  Widget _flag(Map<String, dynamic> club) => CreatorCountryFlag(
    code: club['countryCode']?.toString() ?? '',
    countryName: club['countryName']?.toString(),
    width: 30,
    height: 21,
  );

  Widget _search(ClassicTheme theme) => TextField(
    key: ValueKey(
      _exploring ? 'division-unassigned-search' : 'division-swap-search',
    ),
    decoration: InputDecoration(
      labelText: 'Pesquisar clube',
      prefixIcon: Icon(Icons.search, color: theme.accent),
      border: OutlineInputBorder(borderRadius: BorderRadius.circular(9)),
      enabledBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(9),
        borderSide: BorderSide(color: theme.border),
      ),
    ),
    onChanged: (value) => setState(() => _query = value.trim().toLowerCase()),
  );

  Widget _divisionFilter(ClassicTheme theme) => DropdownButtonFormField<int>(
    key: const ValueKey('division-swap-filter'),
    initialValue: _allDivisions,
    isExpanded: true,
    dropdownColor: theme.surface,
    decoration: InputDecoration(
      labelText: 'Divisão atual',
      constraints: const BoxConstraints(minHeight: 46),
      border: OutlineInputBorder(borderRadius: BorderRadius.circular(9)),
      enabledBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(9),
        borderSide: BorderSide(color: theme.border),
      ),
    ),
    items: [
      const DropdownMenuItem(
        value: _allDivisions,
        child: Text('Todas as divisões'),
      ),
      for (final (index, _) in widget.divisions.indexed)
        DropdownMenuItem(
          value: index + 1,
          child: Text('${index + 1}ª Divisão'),
        ),
      const DropdownMenuItem(value: _unassigned, child: Text('Não escalados')),
    ],
    onChanged: (value) {
      if (value != null) setState(() => _filter = value);
    },
  );

  Widget _catalogFilter(
    ClassicTheme theme, {
    required String key,
    required String label,
    required String value,
    required Map<String, String> options,
    required ValueChanged<String> onChanged,
  }) => DropdownButtonFormField<String>(
    key: ValueKey(key),
    initialValue: value,
    isExpanded: true,
    dropdownColor: theme.surface,
    decoration: InputDecoration(
      labelText: label,
      constraints: const BoxConstraints(minHeight: 46),
      border: OutlineInputBorder(borderRadius: BorderRadius.circular(9)),
      enabledBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(9),
        borderSide: BorderSide(color: theme.border),
      ),
    ),
    items: [
      for (final entry in options.entries)
        DropdownMenuItem(
          value: entry.key,
          child: Text(entry.value, overflow: TextOverflow.ellipsis),
        ),
    ],
    onChanged: (choice) {
      if (choice != null) onChanged(choice);
    },
  );

  Widget _catalogFilters(ClassicTheme theme) {
    final countries = {
      for (final club in _candidates)
        if (_countryCode(club).isNotEmpty) _countryCode(club): _country(club),
    }.entries.toList()..sort((a, b) => a.value.compareTo(b.value));
    final years =
        _candidates.map(_year).where((year) => year.isNotEmpty).toSet().toList()
          ..sort(
            (a, b) => (int.tryParse(b) ?? 0).compareTo(int.tryParse(a) ?? 0),
          );
    if (countries.isEmpty && years.isEmpty) return const SizedBox.shrink();
    return Padding(
      padding: const EdgeInsets.only(top: 8),
      child: Row(
        children: [
          if (countries.isNotEmpty)
            Expanded(
              child: _catalogFilter(
                theme,
                key: 'division-swap-country',
                label: 'País',
                value: _countryFilter,
                options: {'': 'Todos os países', ...Map.fromEntries(countries)},
                onChanged: (choice) => setState(() => _countryFilter = choice),
              ),
            ),
          if (countries.isNotEmpty && years.isNotEmpty)
            const SizedBox(width: 10),
          if (years.isNotEmpty)
            Expanded(
              child: _catalogFilter(
                theme,
                key: 'division-swap-year',
                label: 'Ano',
                value: _yearFilter,
                options: {
                  '': 'Todos os anos',
                  for (final year in years) year: year,
                },
                onChanged: (choice) => setState(() => _yearFilter = choice),
              ),
            ),
        ],
      ),
    );
  }

  Widget _currentClub(ClassicTheme theme, Map<String, dynamic> club) =>
      Container(
        padding: const EdgeInsets.all(12),
        decoration: BoxDecoration(
          color: theme.surfaceAlternate,
          border: Border.all(color: theme.border),
          borderRadius: BorderRadius.circular(9),
        ),
        child: Row(
          children: [
            _flag(club),
            const SizedBox(width: 10),
            Expanded(
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'CLUBE ATUAL',
                    style: TextStyle(
                      color: theme.accent,
                      fontSize: 13,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                  Text(
                    club['name'].toString(),
                    style: TextStyle(
                      color: theme.textPrimary,
                      fontSize: 20,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                  Text(_location(_positions[club['key']?.toString()])),
                ],
              ),
            ),
          ],
        ),
      );

  Widget _result(ClassicTheme theme, Map<String, dynamic> club, String? key) {
    final clubKey = club['key'].toString();
    final selected = clubKey == key;
    final country = _country(club);
    return ListTile(
      key: ValueKey(
        _exploring
            ? 'division-unassigned-$clubKey'
            : 'division-swap-option-$clubKey',
      ),
      contentPadding: const EdgeInsets.symmetric(horizontal: 12, vertical: 3),
      visualDensity: VisualDensity.standard,
      minLeadingWidth: 30,
      leading: _flag(club),
      title: Text(
        club['name'].toString(),
        style: TextStyle(
          fontSize: 18,
          fontWeight: FontWeight.w700,
          color: theme.textPrimary,
        ),
      ),
      subtitle: Text(
        [
          if (country.isNotEmpty) country,
          _location(_positions[clubKey]),
        ].join(' • '),
        style: TextStyle(color: theme.textSecondary),
      ),
      selected: selected,
      selectedTileColor: Color.lerp(
        theme.surface,
        theme.selectedBackground,
        .19,
      ),
      trailing: _exploring
          ? null
          : Icon(
              selected ? Icons.check_circle : Icons.radio_button_unchecked,
              color: selected ? theme.accent : theme.border,
            ),
      onTap: _exploring
          ? null
          : () {
              FocusScope.of(context).unfocus();
              setState(() => _selectedKey = clubKey);
            },
    );
  }

  Widget _actions(ClassicTheme theme, Map<String, dynamic>? selected) =>
      DecoratedBox(
        decoration: BoxDecoration(
          color: theme.surface,
          border: Border(top: BorderSide(color: theme.border)),
        ),
        child: Padding(
          padding: const EdgeInsets.fromLTRB(14, 8, 14, 10),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              if (selected != null) ...[
                if (widget.mobile && widget.currentClub != null)
                  Text(
                    'Sai: ${widget.currentClub!['name']}',
                    maxLines: 1,
                    overflow: TextOverflow.ellipsis,
                    style: TextStyle(color: theme.textSecondary),
                  ),
                Text(
                  'Entra: ${selected['name']}',
                  maxLines: 1,
                  overflow: TextOverflow.ellipsis,
                  style: TextStyle(
                    color: theme.accent,
                    fontWeight: FontWeight.w700,
                  ),
                ),
                const SizedBox(height: 6),
              ],
              Row(
                mainAxisAlignment: MainAxisAlignment.end,
                children: [
                  TextButton(
                    key: const ValueKey('division-swap-cancel'),
                    style: TextButton.styleFrom(
                      foregroundColor: theme.accent,
                      minimumSize: const Size(76, 44),
                      visualDensity: VisualDensity.standard,
                    ),
                    onPressed: () => Navigator.pop(context),
                    child: Text(_exploring ? 'FECHAR' : 'CANCELAR'),
                  ),
                  if (!_exploring) ...[
                    const SizedBox(width: 8),
                    Flexible(
                      child: ElevatedButton(
                        key: const ValueKey('division-swap-confirm'),
                        style: ElevatedButton.styleFrom(
                          backgroundColor: theme.selectedBackground,
                          foregroundColor: theme.selectedText,
                          minimumSize: const Size(44, 44),
                          visualDensity: VisualDensity.standard,
                        ),
                        onPressed: selected == null
                            ? null
                            : () => Navigator.pop(
                                context,
                                selected['key'].toString(),
                              ),
                        child: Text(
                          widget.mobile ? 'CONFIRMAR TROCA' : 'TROCAR CLUBES',
                          textAlign: TextAlign.center,
                        ),
                      ),
                    ),
                  ],
                ],
              ),
            ],
          ),
        ),
      );

  @override
  Widget build(BuildContext context) {
    final theme = ClassicTheme.of(context);
    final visible = _candidates.where((club) {
      final key = club['key'].toString();
      final location = _positions[key];
      final matchesFilter =
          _filter == _allDivisions ||
          (_filter == _unassigned
              ? location == null
              : location?.division == _filter);
      return matchesFilter &&
          (_countryFilter.isEmpty || _countryCode(club) == _countryFilter) &&
          (_yearFilter.isEmpty || _year(club) == _yearFilter) &&
          '${club['name']} ${_country(club)} ${_year(club)}'
              .toLowerCase()
              .contains(_query);
    }).toList();
    final selected = visible
        .where((club) => club['key'].toString() == _selectedKey)
        .firstOrNull;
    final current = widget.currentClub;

    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [
        Expanded(
          child: CustomScrollView(
            keyboardDismissBehavior: ScrollViewKeyboardDismissBehavior.onDrag,
            slivers: [
              SliverToBoxAdapter(
                child: ColoredBox(
                  color: theme.primary,
                  child: Padding(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 18,
                      vertical: 14,
                    ),
                    child: Row(
                      children: [
                        Icon(
                          _exploring ? Icons.groups_outlined : Icons.swap_horiz,
                          color: theme.selectedBackground,
                        ),
                        const SizedBox(width: 9),
                        Expanded(
                          child: Text(
                            _exploring
                                ? 'CLUBES NÃO ESCALADOS'
                                : 'TROCAR CLUBE',
                            style: TextStyle(
                              color: readableInk(theme.primary),
                              fontSize: 24,
                              fontWeight: FontWeight.w700,
                            ),
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
              ),
              SliverToBoxAdapter(
                child: Padding(
                  padding: const EdgeInsets.fromLTRB(14, 12, 14, 8),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      if (current != null) ...[
                        _currentClub(theme, current),
                        const SizedBox(height: 12),
                      ] else ...[
                        Text(
                          _candidates.length == 1
                              ? '1 clube disponível'
                              : '${_candidates.length} clubes disponíveis',
                          key: const ValueKey('division-unassigned-count'),
                          style: TextStyle(
                            color: theme.accent,
                            fontWeight: FontWeight.w700,
                          ),
                        ),
                        const SizedBox(height: 10),
                      ],
                      if (_exploring)
                        _search(theme)
                      else if (widget.mobile) ...[
                        _search(theme),
                        const SizedBox(height: 8),
                        _divisionFilter(theme),
                      ] else
                        Row(
                          children: [
                            Expanded(flex: 3, child: _search(theme)),
                            const SizedBox(width: 10),
                            Expanded(flex: 2, child: _divisionFilter(theme)),
                          ],
                        ),
                      _catalogFilters(theme),
                    ],
                  ),
                ),
              ),
              if (visible.isEmpty)
                SliverToBoxAdapter(
                  child: Padding(
                    padding: const EdgeInsets.all(22),
                    child: Text(
                      _exploring && _candidates.isEmpty
                          ? 'Nenhum clube não escalado está disponível.'
                          : 'Nenhum clube encontrado.',
                      textAlign: TextAlign.center,
                    ),
                  ),
                )
              else
                SliverPadding(
                  padding: const EdgeInsets.symmetric(horizontal: 14),
                  sliver: SliverList.separated(
                    itemCount: visible.length,
                    itemBuilder: (context, index) => _result(
                      theme,
                      visible[index],
                      selected?['key']?.toString(),
                    ),
                    separatorBuilder: (context, index) =>
                        Divider(height: 1, color: theme.border),
                  ),
                ),
            ],
          ),
        ),
        _actions(theme, selected),
      ],
    );
  }
}
