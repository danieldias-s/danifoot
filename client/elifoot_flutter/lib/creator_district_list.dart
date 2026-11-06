import 'dart:math' as math;

import 'package:flutter/material.dart';

import 'club_theme.dart';
import 'creator_visual.dart';

enum _DistrictStatus { all, selected, available }

/// Presentation preferences only. The creator owns the district assignment.
class CreatorDistrictViewState extends ChangeNotifier {
  final _search = TextEditingController();
  String? _country;
  _DistrictStatus _status = _DistrictStatus.all;
  bool _byStrength = false;
  int _page = 0;

  void _filterChanged() {
    _page = 0;
    notifyListeners();
  }

  void _showPage(int page) {
    _page = page;
    notifyListeners();
  }

  void reset() {
    _search.clear();
    _country = null;
    _status = _DistrictStatus.all;
    _byStrength = false;
    _filterChanged();
  }

  @override
  void dispose() {
    _search.dispose();
    super.dispose();
  }
}

class CreatorDistrictFilters extends StatelessWidget {
  final CreatorDistrictViewState view;
  final Map<String, String> countries;
  final List<Map<String, dynamic>> clubs;
  final bool showSelection;

  const CreatorDistrictFilters({
    super.key,
    required this.view,
    required this.countries,
    required this.clubs,
    required this.showSelection,
  });

  @override
  Widget build(BuildContext context) {
    final theme = ClassicTheme.of(context).paper;
    return ListenableBuilder(
      listenable: view,
      builder: (context, _) {
        final hasStrength = clubs.any((club) => _initialPower(club) != null);
        final countryOptions = countries.entries.toList()
          ..sort(
            (a, b) => _searchable(a.value).compareTo(_searchable(b.value)),
          );
        return LayoutBuilder(
          builder: (context, constraints) {
            final compact = constraints.maxWidth < 510;
            final controlWidth = compact
                ? (constraints.maxWidth - 8) / 2
                : 135.0;
            return Wrap(
              spacing: 8,
              runSpacing: 8,
              crossAxisAlignment: WrapCrossAlignment.center,
              children: [
                SizedBox(
                  width: compact ? constraints.maxWidth : 175,
                  child: TextField(
                    key: const ValueKey('district-search'),
                    controller: view._search,
                    textInputAction: TextInputAction.search,
                    onChanged: (_) => view._filterChanged(),
                    style: TextStyle(color: theme.textPrimary, fontSize: 15),
                    decoration: _fieldDecoration(theme, 'Pesquisar clube')
                        .copyWith(
                          prefixIcon: Icon(Icons.search, color: theme.accent),
                          prefixIconConstraints: const BoxConstraints(
                            minWidth: 37,
                          ),
                        ),
                  ),
                ),
                SizedBox(
                  width: controlWidth,
                  child: _filter<String>(
                    theme: theme,
                    name: 'País',
                    keyName: 'district-country-filter',
                    value: countries.containsKey(view._country)
                        ? view._country!
                        : '',
                    options: {
                      '': 'Todos os países',
                      ...Map.fromEntries(countryOptions),
                    },
                    onChanged: (value) {
                      view._country = value.isEmpty ? null : value;
                      view._filterChanged();
                    },
                  ),
                ),
                if (showSelection)
                  SizedBox(
                    width: controlWidth,
                    child: _filter<_DistrictStatus>(
                      theme: theme,
                      name: 'Situação',
                      keyName: 'district-status-filter',
                      value: view._status,
                      options: const {
                        _DistrictStatus.all: 'Todos',
                        _DistrictStatus.selected: 'Selecionados',
                        _DistrictStatus.available: 'Disponíveis',
                      },
                      onChanged: (value) {
                        view._status = value;
                        view._filterChanged();
                      },
                    ),
                  ),
                if (hasStrength)
                  SizedBox(
                    width: controlWidth,
                    child: _filter<bool>(
                      theme: theme,
                      name: 'Ordenação',
                      keyName: 'district-order-filter',
                      value: view._byStrength,
                      options: const {false: 'Ranking', true: 'Força'},
                      onChanged: (value) {
                        view._byStrength = value;
                        view._filterChanged();
                      },
                    ),
                  ),
              ],
            );
          },
        );
      },
    );
  }

  InputDecoration _fieldDecoration(ClassicTheme theme, String label) =>
      InputDecoration(
        labelText: label,
        labelStyle: TextStyle(color: theme.textSecondary, fontSize: 14),
        filled: true,
        fillColor: theme.tableRow,
        isDense: true,
        contentPadding: const EdgeInsets.symmetric(horizontal: 9, vertical: 12),
        border: OutlineInputBorder(borderRadius: BorderRadius.circular(9)),
        enabledBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(9),
          borderSide: BorderSide(color: theme.border),
        ),
        focusedBorder: OutlineInputBorder(
          borderRadius: BorderRadius.circular(9),
          borderSide: BorderSide(color: theme.accent, width: 1.5),
        ),
      );

  Widget _filter<T>({
    required ClassicTheme theme,
    required String name,
    required String keyName,
    required T value,
    required Map<T, String> options,
    required ValueChanged<T> onChanged,
  }) => InputDecorator(
    decoration: _fieldDecoration(theme, name).copyWith(
      contentPadding: const EdgeInsets.symmetric(horizontal: 9, vertical: 6),
    ),
    child: DropdownButtonHideUnderline(
      child: DropdownButton<T>(
        key: ValueKey(keyName),
        value: value,
        isExpanded: true,
        isDense: true,
        dropdownColor: theme.surface,
        iconEnabledColor: theme.accent,
        style: TextStyle(
          color: theme.textPrimary,
          fontFamily: 'ClassicCondensed',
          fontSize: 15,
        ),
        items: [
          for (final option in options.entries)
            DropdownMenuItem<T>(
              value: option.key,
              child: Text(
                option.value,
                maxLines: 1,
                overflow: TextOverflow.ellipsis,
              ),
            ),
        ],
        onChanged: (value) {
          if (value != null) onChanged(value);
        },
      ),
    ),
  );
}

class CreatorDistrictList extends StatelessWidget {
  static const _pageSize = 10;
  final CreatorDistrictViewState view;
  final Map<String, String> countries;
  final List<Map<String, dynamic>> clubs;
  final Set<String> selectedKeys;
  final bool showSelection;
  final ValueChanged<(String key, bool selected)>? onSelectionChanged;

  const CreatorDistrictList({
    super.key,
    required this.view,
    required this.countries,
    required this.clubs,
    required this.selectedKeys,
    required this.showSelection,
    this.onSelectionChanged,
  });

  @override
  Widget build(BuildContext context) {
    final theme = ClassicTheme.of(context).paper;
    return ListenableBuilder(
      listenable: view,
      builder: (context, _) {
        final filtered = _filteredClubs();
        if (filtered.isEmpty) {
          return _DistrictEmpty(theme: theme, hasClubs: clubs.isNotEmpty);
        }
        final pages = (filtered.length + _pageSize - 1) ~/ _pageSize;
        final page = math.min(view._page, pages - 1);
        final start = page * _pageSize;
        final pageClubs = filtered.skip(start).take(_pageSize).toList();
        return LayoutBuilder(
          builder: (context, constraints) {
            final desktop = constraints.maxWidth >= 860;
            final hasStrength = clubs.any(
              (club) => _initialPower(club) != null,
            );
            final pagination = _DistrictPagination(
              theme: theme,
              page: page,
              pages: pages,
              start: start + 1,
              end: start + pageClubs.length,
              count: filtered.length,
              desktop: desktop,
              onPage: view._showPage,
            );
            if (desktop) {
              return Container(
                key: const ValueKey('district-desktop-table'),
                clipBehavior: Clip.antiAlias,
                decoration: BoxDecoration(
                  borderRadius: BorderRadius.circular(9),
                  border: Border.all(color: theme.border),
                ),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    _tableHeading(theme, hasStrength),
                    for (final (index, club) in pageClubs.indexed)
                      _tableRow(theme, club, index.isOdd, hasStrength),
                    pagination,
                  ],
                ),
              );
            }
            return Column(
              key: const ValueKey('district-mobile-cards'),
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                for (final club in pageClubs) _card(theme, club),
                pagination,
              ],
            );
          },
        );
      },
    );
  }

  List<_DistrictClub> _filteredClubs() {
    final query = _searchable(view._search.text);
    final country = countries.containsKey(view._country) ? view._country : null;
    final filtered = <_DistrictClub>[];
    for (final (index, data) in clubs.indexed) {
      final club = _DistrictClub(data, index, countries);
      if (country != null && club.countryCode != country) continue;
      if (query.isNotEmpty && !_searchable(club.name).contains(query)) continue;
      if (showSelection) {
        final selected = selectedKeys.contains(club.key);
        if (view._status == _DistrictStatus.selected && !selected ||
            view._status == _DistrictStatus.available && selected) {
          continue;
        }
      }
      filtered.add(club);
    }
    if (view._byStrength) {
      filtered.sort((a, b) {
        final order = (b.power ?? -1).compareTo(a.power ?? -1);
        return order == 0 ? a.catalogIndex.compareTo(b.catalogIndex) : order;
      });
    }
    return filtered;
  }

  Widget _tableHeading(ClassicTheme theme, bool hasStrength) {
    Widget heading(String text) => Text(
      text,
      style: TextStyle(color: theme.textPrimary, fontWeight: FontWeight.w700),
    );
    return ColoredBox(
      color: Color.lerp(theme.tableHeader, theme.accent, .12)!,
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 13, vertical: 7),
        child: _columns(
          ranking: heading('#'),
          club: heading('Clube'),
          country: heading('País'),
          power: hasStrength
              ? Tooltip(
                  message: 'Força inicial do clube no catálogo',
                  child: FittedBox(
                    fit: BoxFit.scaleDown,
                    child: Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        heading('Força'),
                        const SizedBox(width: 4),
                        Icon(Icons.info_outline, size: 15, color: theme.accent),
                      ],
                    ),
                  ),
                )
              : null,
          status: heading('Origem / status'),
        ),
      ),
    );
  }

  Widget _tableRow(
    ClassicTheme theme,
    _DistrictClub club,
    bool alternate,
    bool hasStrength,
  ) {
    final selected = showSelection && selectedKeys.contains(club.key);
    return Material(
      color: alternate ? theme.tableRowAlternate : theme.tableRow,
      child: InkWell(
        key: ValueKey('district-${club.key}'),
        onTap: onSelectionChanged == null
            ? null
            : () => onSelectionChanged!((club.key, !selected)),
        child: Container(
          decoration: BoxDecoration(
            border: Border(
              bottom: BorderSide(color: theme.border.withValues(alpha: .45)),
            ),
          ),
          constraints: const BoxConstraints(minHeight: 37),
          padding: const EdgeInsets.symmetric(horizontal: 13, vertical: 4),
          child: _columns(
            ranking: _rank(theme, club),
            club: Row(
              children: [
                _crest(theme, club),
                const SizedBox(width: 9),
                Expanded(child: _clubName(theme, club)),
              ],
            ),
            country: _country(theme, club),
            power: hasStrength ? _power(theme, club) : null,
            status: Row(
              children: [
                Flexible(child: _status(theme, selected)),
                if (onSelectionChanged != null) _checkbox(club, selected),
              ],
            ),
          ),
        ),
      ),
    );
  }

  Widget _columns({
    required Widget ranking,
    required Widget club,
    required Widget country,
    Widget? power,
    required Widget status,
  }) => Row(
    children: [
      SizedBox(width: 45, child: Center(child: ranking)),
      const SizedBox(width: 12),
      Expanded(flex: 4, child: club),
      const SizedBox(width: 12),
      Expanded(flex: 3, child: country),
      if (power != null) ...[
        const SizedBox(width: 12),
        SizedBox(width: 90, child: Center(child: power)),
      ],
      const SizedBox(width: 12),
      Expanded(flex: 3, child: status),
    ],
  );

  Widget _card(ClassicTheme theme, _DistrictClub club) {
    final selected = showSelection && selectedKeys.contains(club.key);
    return Padding(
      padding: const EdgeInsets.only(bottom: 8),
      child: Material(
        color: selected ? theme.tableRowAlternate : theme.tableRow,
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(10),
          side: BorderSide(color: theme.border),
        ),
        clipBehavior: Clip.antiAlias,
        child: InkWell(
          key: ValueKey('district-${club.key}'),
          onTap: onSelectionChanged == null
              ? null
              : () => onSelectionChanged!((club.key, !selected)),
          child: Padding(
            padding: const EdgeInsets.symmetric(horizontal: 9, vertical: 9),
            child: Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                SizedBox(width: 32, child: Center(child: _rank(theme, club))),
                const SizedBox(width: 3),
                _crest(theme, club),
                const SizedBox(width: 8),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      _clubName(theme, club),
                      const SizedBox(height: 4),
                      _country(theme, club),
                      const SizedBox(height: 6),
                      Wrap(
                        spacing: 7,
                        runSpacing: 6,
                        children: [
                          if (club.power != null)
                            _power(theme, club, label: true),
                          _status(theme, selected),
                        ],
                      ),
                    ],
                  ),
                ),
                if (onSelectionChanged != null) _checkbox(club, selected),
              ],
            ),
          ),
        ),
      ),
    );
  }

  Widget _rank(ClassicTheme theme, _DistrictClub club) => Text(
    club.rank?.toString() ?? '—',
    style: TextStyle(color: theme.accent, fontWeight: FontWeight.w700),
  );

  // EligibleCatalogClub does not expose licensed crest artwork.
  Widget _crest(ClassicTheme theme, _DistrictClub club) => Tooltip(
    message: 'Escudo indisponível',
    child: Icon(
      Icons.shield_outlined,
      key: ValueKey('district-crest-${club.key}'),
      color: theme.textSecondary,
      size: 22,
    ),
  );

  Widget _clubName(ClassicTheme theme, _DistrictClub club) => Tooltip(
    message: club.name,
    child: Text(
      club.name,
      maxLines: 2,
      overflow: TextOverflow.ellipsis,
      style: TextStyle(color: theme.textPrimary, fontWeight: FontWeight.w700),
    ),
  );

  Widget _country(ClassicTheme theme, _DistrictClub club) => Row(
    mainAxisSize: MainAxisSize.min,
    children: [
      CreatorCountryFlag(
        key: ValueKey('district-flag-${club.key}'),
        code: club.countryCode,
        countryName: club.countryName,
        width: 24,
        height: 16,
      ),
      const SizedBox(width: 7),
      Flexible(
        child: Text(
          club.countryName.isEmpty ? '—' : club.countryName,
          maxLines: 2,
          overflow: TextOverflow.ellipsis,
          style: TextStyle(color: theme.textPrimary, fontSize: 15),
        ),
      ),
    ],
  );

  Widget _power(ClassicTheme theme, _DistrictClub club, {bool label = false}) {
    if (club.power == null) return const Text('—');
    return _badge(
      theme,
      label ? 'Força: ${club.power}' : '${club.power}',
      fill: Color.lerp(theme.tableHeader, theme.accent, .12)!,
      strong: true,
    );
  }

  Widget _status(ClassicTheme theme, bool selected) => _badge(
    theme,
    selected
        ? 'Selecionado'
        : (showSelection ? 'Disponível' : 'Fora das divisões'),
    fill: selected
        ? Color.lerp(theme.tableHeader, theme.accent, .22)!
        : theme.tableHeader,
    icon: selected ? Icons.check_circle_outline : Icons.layers_outlined,
    strong: selected,
  );

  Widget _badge(
    ClassicTheme theme,
    String label, {
    required Color fill,
    IconData? icon,
    bool strong = false,
  }) => Container(
    decoration: BoxDecoration(
      color: fill,
      borderRadius: BorderRadius.circular(6),
    ),
    padding: const EdgeInsets.symmetric(horizontal: 7, vertical: 3),
    child: Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        if (icon != null) ...[
          Icon(icon, size: 14, color: readableOn(theme.accent, fill)),
          const SizedBox(width: 4),
        ],
        Flexible(
          child: Text(
            label,
            style: TextStyle(
              color: readableOn(theme.textPrimary, fill),
              fontSize: 13,
              fontWeight: strong ? FontWeight.w700 : FontWeight.w500,
            ),
          ),
        ),
      ],
    ),
  );

  Widget _checkbox(_DistrictClub club, bool selected) => Checkbox(
    key: ValueKey('district-checkbox-${club.key}'),
    semanticLabel: 'Selecionar ${club.name} para os distritais',
    value: selected,
    onChanged: (checked) =>
        onSelectionChanged?.call((club.key, checked == true)),
  );
}

class _DistrictClub {
  final Map<String, dynamic> data;
  final int catalogIndex;
  final Map<String, String> countries;

  const _DistrictClub(this.data, this.catalogIndex, this.countries);

  String get key => data['key'].toString();
  String get name => data['name']?.toString() ?? 'Clube sem nome';
  String get countryCode => data['countryCode']?.toString() ?? '';
  String get countryName {
    final name = data['countryName']?.toString();
    return name != null && name.isNotEmpty
        ? name
        : countries[countryCode] ?? countryCode;
  }

  int? get rank =>
      _positiveInt(_profile(data)?['worldRank']) ?? _positiveInt(data['rank']);
  int? get power => _initialPower(data);
}

Map? _profile(Map<String, dynamic> data) => switch (data['initialProfile']) {
  final Map profile => profile,
  _ => null,
};

int? _positiveInt(Object? value) => value is int && value > 0 ? value : null;

int? _initialPower(Map<String, dynamic> data) =>
    _positiveInt(_profile(data)?['clubInitialPower']);

String _searchable(String value) {
  const accents = {
    'á': 'a',
    'à': 'a',
    'â': 'a',
    'ã': 'a',
    'ä': 'a',
    'é': 'e',
    'è': 'e',
    'ê': 'e',
    'ë': 'e',
    'í': 'i',
    'ì': 'i',
    'î': 'i',
    'ï': 'i',
    'ó': 'o',
    'ò': 'o',
    'ô': 'o',
    'õ': 'o',
    'ö': 'o',
    'ú': 'u',
    'ù': 'u',
    'û': 'u',
    'ü': 'u',
    'ç': 'c',
    'ñ': 'n',
  };
  return value
      .trim()
      .toLowerCase()
      .split('')
      .map((letter) => accents[letter] ?? letter)
      .join();
}

class _DistrictEmpty extends StatelessWidget {
  final ClassicTheme theme;
  final bool hasClubs;

  const _DistrictEmpty({required this.theme, required this.hasClubs});

  @override
  Widget build(BuildContext context) => Container(
    key: const ValueKey('district-empty'),
    padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 24),
    decoration: BoxDecoration(
      color: theme.tableRow,
      border: Border.all(color: theme.border),
      borderRadius: BorderRadius.circular(9),
    ),
    child: Column(
      crossAxisAlignment: CrossAxisAlignment.center,
      children: [
        Icon(Icons.search_off, color: theme.accent, size: 28),
        const SizedBox(height: 8),
        Text(
          hasClubs
              ? 'Nenhum clube corresponde aos filtros.'
              : 'Nenhum clube disponível fora das divisões.',
          textAlign: TextAlign.center,
          style: TextStyle(
            color: theme.textPrimary,
            fontWeight: FontWeight.w700,
          ),
        ),
      ],
    ),
  );
}

class _DistrictPagination extends StatelessWidget {
  final ClassicTheme theme;
  final int page, pages, start, end, count;
  final bool desktop;
  final ValueChanged<int> onPage;

  const _DistrictPagination({
    required this.theme,
    required this.page,
    required this.pages,
    required this.start,
    required this.end,
    required this.count,
    required this.desktop,
    required this.onPage,
  });

  @override
  Widget build(BuildContext context) {
    final range = Text(
      'Mostrando $start–$end de $count ${count == 1 ? 'clube' : 'clubes'}',
      key: const ValueKey('district-page-range'),
      style: TextStyle(color: theme.textSecondary, fontWeight: FontWeight.w600),
    );
    if (!desktop) {
      return Column(
        crossAxisAlignment: CrossAxisAlignment.center,
        children: [
          const SizedBox(height: 8),
          range,
          if (pages > 1) ...[
            const SizedBox(height: 8),
            Row(
              children: [
                Expanded(
                  child: _mobileButton('ANTERIOR', page - 1, 'previous'),
                ),
                Padding(
                  padding: const EdgeInsets.symmetric(horizontal: 10),
                  child: Semantics(
                    label: 'Página ${page + 1} de $pages',
                    excludeSemantics: true,
                    child: Text(
                      '${page + 1}/$pages',
                      style: TextStyle(color: theme.textPrimary),
                    ),
                  ),
                ),
                Expanded(child: _mobileButton('PRÓXIMA', page + 1, 'next')),
              ],
            ),
          ],
        ],
      );
    }
    final numberPages = _numberPages();
    return ColoredBox(
      color: theme.tableRow,
      child: Padding(
        padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 8),
        child: Row(
          children: [
            Expanded(child: range),
            if (pages > 1) ...[
              _desktopButton(
                const Text('«'),
                0,
                'first',
                'Primeira página',
                enabled: page > 0,
              ),
              _desktopButton(
                const Text('‹'),
                page - 1,
                'previous',
                'Página anterior',
              ),
              for (final (index, target) in numberPages.indexed) ...[
                if (index > 0 && target - numberPages[index - 1] > 1)
                  const Padding(
                    padding: EdgeInsets.symmetric(horizontal: 5),
                    child: Text('…'),
                  ),
                _desktopButton(
                  Text('${target + 1}'),
                  target,
                  '${target + 1}',
                  'Página ${target + 1}',
                  current: target == page,
                ),
              ],
              _desktopButton(
                const Text('›'),
                page + 1,
                'next',
                'Próxima página',
              ),
              _desktopButton(
                const Text('»'),
                pages - 1,
                'last',
                'Última página',
                enabled: page < pages - 1,
              ),
            ],
          ],
        ),
      ),
    );
  }

  List<int> _numberPages() {
    final firstVisible = math.min(
      math.max(0, page - 2),
      math.max(0, pages - 5),
    );
    return <int>{
      0,
      for (var i = firstVisible; i < math.min(pages, firstVisible + 5); i++) i,
      pages - 1,
    }.toList();
  }

  Widget _mobileButton(String label, int target, String keyName) =>
      OutlinedButton(
        key: ValueKey('district-page-$keyName'),
        style: OutlinedButton.styleFrom(
          foregroundColor: theme.accent,
          side: BorderSide(color: theme.border),
          minimumSize: const Size(64, 44),
          padding: const EdgeInsets.symmetric(horizontal: 5),
        ),
        onPressed: target >= 0 && target < pages ? () => onPage(target) : null,
        child: Text(label, textAlign: TextAlign.center),
      );

  Widget _desktopButton(
    Widget child,
    int target,
    String keyName,
    String label, {
    bool current = false,
    bool enabled = true,
  }) => Padding(
    padding: const EdgeInsets.only(left: 3),
    child: Tooltip(
      message: label,
      child: Semantics(
        selected: current,
        child: OutlinedButton(
          key: ValueKey('district-page-$keyName'),
          style: OutlinedButton.styleFrom(
            foregroundColor: current ? theme.selectedText : theme.textPrimary,
            disabledForegroundColor: theme.textSecondary,
            backgroundColor: current
                ? theme.selectedBackground
                : theme.tableRow,
            side: BorderSide(
              color: current ? theme.selectedBackground : theme.border,
            ),
            padding: const EdgeInsets.symmetric(horizontal: 6),
            minimumSize: const Size(32, 32),
            tapTargetSize: MaterialTapTargetSize.shrinkWrap,
            shape: RoundedRectangleBorder(
              borderRadius: BorderRadius.circular(7),
            ),
          ),
          onPressed: enabled && target >= 0 && target < pages
              ? () => onPage(target)
              : null,
          child: child,
        ),
      ),
    ),
  );
}
