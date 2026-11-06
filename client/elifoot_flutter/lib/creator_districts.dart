import 'package:flutter/material.dart';

import 'club_theme.dart';

/// Layout for the district step. The creator owns distribution and validation;
/// the catalog browser supplies the filters and the club list.
class CreatorDistricts extends StatelessWidget {
  static const desktopContentWidth = 1000.0;

  final int districtCount;
  final int districtLimit;
  final String? districtCountLabel;
  final int availableCount;
  final int divisionCount;
  final String mode;
  final ValueChanged<String>? onModeChanged;
  final Widget? filters;
  final Widget? clubList;
  final Widget? notice;

  const CreatorDistricts({
    super.key,
    required this.districtCount,
    required this.districtLimit,
    required this.availableCount,
    required this.divisionCount,
    required this.mode,
    required this.onModeChanged,
    this.districtCountLabel,
    this.filters,
    this.clubList,
    this.notice,
  });

  Widget _intro(ClassicTheme theme, bool desktop) => Row(
    crossAxisAlignment: CrossAxisAlignment.start,
    children: [
      Container(
        width: desktop ? 64 : 48,
        height: desktop ? 64 : 48,
        decoration: BoxDecoration(
          shape: BoxShape.circle,
          color: theme.surfaceAlternate,
        ),
        child: Icon(
          Icons.shield_outlined,
          size: desktop ? 42 : 30,
          color: readableOn(theme.accent, theme.surfaceAlternate),
        ),
      ),
      const SizedBox(width: 12),
      Expanded(
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              'Distritais',
              style: theme.headingStyle.copyWith(
                fontSize: desktop ? 29 : 24,
                height: 1.1,
              ),
            ),
            const SizedBox(height: 4),
            SizedBox(
              width: 58,
              height: 3,
              child: ColoredBox(color: theme.selectedBackground),
            ),
            const SizedBox(height: 7),
            Text(
              'Ranking dos clubes restantes que poderão compor os distritais. '
              'Os clubes das quatro divisões principais permanecem fora dos distritais.',
              style: TextStyle(
                color: theme.textPrimary,
                fontSize: 16,
                height: 1.25,
              ),
            ),
          ],
        ),
      ),
    ],
  );

  Widget _metrics(ClassicTheme theme) {
    final metrics = [
      (
        key: 'districts-count',
        icon: Icons.groups_outlined,
        value: '$districtCount/$districtLimit',
        label:
            districtCountLabel ??
            (mode == 'Manual'
                ? 'Distritais selecionados'
                : 'Distritais previstos'),
      ),
      (
        key: 'districts-available-count',
        icon: Icons.sports_soccer,
        value: '$availableCount',
        label: 'Clubes disponíveis fora das divisões',
      ),
      (
        key: 'districts-division-count',
        icon: Icons.layers_outlined,
        value: '$divisionCount',
        label: 'Divisões principais reservadas',
      ),
    ];
    return LayoutBuilder(
      builder: (context, constraints) {
        const spacing = 8.0;
        final columns = constraints.maxWidth >= 575 ? 3 : 2;
        final width =
            (constraints.maxWidth - spacing * (columns - 1)) / columns;
        final compact = columns == 2;
        final background = Color.lerp(
          theme.surface,
          theme.surfaceAlternate,
          .76,
        )!;
        return Wrap(
          spacing: spacing,
          runSpacing: spacing,
          children: [
            for (final metric in metrics)
              Container(
                key: ValueKey(metric.key),
                width: width,
                constraints: BoxConstraints(minHeight: compact ? 76 : 94),
                padding: EdgeInsets.symmetric(
                  horizontal: compact ? 9 : 15,
                  vertical: compact ? 8 : 12,
                ),
                decoration: BoxDecoration(
                  color: background,
                  borderRadius: BorderRadius.circular(12),
                ),
                child: Row(
                  children: [
                    Icon(
                      metric.icon,
                      size: compact ? 23 : 35,
                      color: readableOn(theme.accent, background),
                    ),
                    SizedBox(width: compact ? 7 : 12),
                    Expanded(
                      child: Column(
                        crossAxisAlignment: CrossAxisAlignment.start,
                        children: [
                          Text(
                            metric.value,
                            style: TextStyle(
                              color: theme.textPrimary,
                              fontSize: compact ? 24 : 31,
                              height: 1.1,
                              fontWeight: FontWeight.w800,
                            ),
                          ),
                          Text(
                            metric.label,
                            style: TextStyle(
                              color: theme.textPrimary,
                              fontSize: compact ? 13 : 16,
                              height: 1.15,
                            ),
                          ),
                        ],
                      ),
                    ),
                  ],
                ),
              ),
          ],
        );
      },
    );
  }

  Widget _modeSelector(ClassicTheme theme) => Column(
    crossAxisAlignment: CrossAxisAlignment.stretch,
    children: [
      Text(
        'Modo de ranking',
        style: TextStyle(color: theme.accent, fontWeight: FontWeight.w700),
      ),
      const SizedBox(height: 4),
      DropdownButtonFormField<String>(
        key: ValueKey('Distritais:$mode'),
        initialValue: mode,
        isExpanded: true,
        dropdownColor: theme.surface,
        decoration: InputDecoration(
          filled: true,
          fillColor: theme.surface,
          prefixIcon: Icon(Icons.bar_chart, color: theme.accent, size: 21),
          prefixIconConstraints: const BoxConstraints(minWidth: 37),
          isDense: true,
          contentPadding: const EdgeInsets.symmetric(
            horizontal: 10,
            vertical: 11,
          ),
          border: OutlineInputBorder(borderRadius: BorderRadius.circular(9)),
          enabledBorder: OutlineInputBorder(
            borderSide: BorderSide(color: theme.border),
            borderRadius: BorderRadius.circular(9),
          ),
        ),
        items: [
          const DropdownMenuItem(
            value: 'Ranking',
            child: Text('Ranking dos clubes restantes'),
          ),
          const DropdownMenuItem(
            value: 'Random',
            child: Text('Sorteio com seed salva'),
          ),
          DropdownMenuItem(
            value: 'Manual',
            child: Text('Escolher os $districtLimit manualmente'),
          ),
        ],
        onChanged: onModeChanged == null
            ? null
            : (value) {
                if (value != null) onModeChanged!(value);
              },
      ),
    ],
  );

  Widget _controls(ClassicTheme theme, bool desktop) => Container(
    padding: const EdgeInsets.all(10),
    decoration: BoxDecoration(
      color: Color.lerp(theme.surface, theme.surfaceAlternate, .72),
      border: Border.all(color: theme.border.withValues(alpha: .65)),
      borderRadius: BorderRadius.circular(10),
    ),
    child: desktop && filters != null
        ? Row(
            crossAxisAlignment: CrossAxisAlignment.center,
            children: [
              SizedBox(width: 235, child: _modeSelector(theme)),
              const SizedBox(width: 12),
              Expanded(child: filters!),
            ],
          )
        : Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              _modeSelector(theme),
              if (filters != null) ...[const SizedBox(height: 12), filters!],
            ],
          ),
  );

  Widget _helpBody(ClassicTheme theme) {
    const items = [
      (
        Icons.account_tree_outlined,
        'Usam os clubes restantes conforme o modo de distribuição escolhido.',
      ),
      (Icons.public, 'Respeitam os países escolhidos para o seu mundo.'),
      (
        Icons.check_circle_outline,
        'Completam a quantidade de clubes prevista para a competição.',
      ),
    ];
    return Padding(
      padding: const EdgeInsets.fromLTRB(13, 12, 13, 8),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(
            'Os distritais reúnem clubes que ficaram fora das divisões principais.',
            style: TextStyle(
              color: theme.textPrimary,
              fontSize: 16,
              height: 1.25,
            ),
          ),
          const SizedBox(height: 6),
          for (final (icon, label) in items) ...[
            Divider(height: 1, color: theme.border.withValues(alpha: .7)),
            Padding(
              padding: const EdgeInsets.symmetric(vertical: 9),
              child: Row(
                children: [
                  Icon(icon, size: 22, color: theme.accent),
                  const SizedBox(width: 10),
                  Expanded(
                    child: Text(
                      label,
                      style: TextStyle(
                        color: theme.textPrimary,
                        fontSize: 15,
                        height: 1.2,
                      ),
                    ),
                  ),
                ],
              ),
            ),
          ],
        ],
      ),
    );
  }

  Widget _guide(ClassicTheme theme, bool desktop) => Container(
    key: ValueKey(desktop ? 'districts-help-desktop' : 'districts-help-mobile'),
    clipBehavior: Clip.antiAlias,
    decoration: BoxDecoration(
      color: theme.surface,
      border: Border.all(color: theme.border),
      borderRadius: BorderRadius.circular(11),
      boxShadow: desktop
          ? [
              BoxShadow(
                color: theme.primary.withValues(alpha: .17),
                blurRadius: 4,
                offset: const Offset(0, 2),
              ),
            ]
          : null,
    ),
    child: desktop
        ? Column(
            crossAxisAlignment: CrossAxisAlignment.stretch,
            children: [
              Container(
                padding: const EdgeInsets.fromLTRB(14, 12, 14, 12),
                decoration: BoxDecoration(
                  color: theme.primary,
                  border: Border(
                    right: BorderSide(
                      color: theme.selectedBackground,
                      width: 7,
                    ),
                  ),
                ),
                child: Row(
                  children: [
                    Icon(
                      Icons.outlined_flag,
                      color: readableOn(
                        theme.selectedBackground,
                        theme.primary,
                      ),
                      size: 29,
                    ),
                    const SizedBox(width: 10),
                    Expanded(
                      child: Text(
                        'Como os distritais serão usados',
                        style: TextStyle(
                          color: readableInk(theme.primary, Colors.white),
                          fontSize: 22,
                          fontWeight: FontWeight.w700,
                        ),
                      ),
                    ),
                  ],
                ),
              ),
              _helpBody(theme),
            ],
          )
        : ExpansionTile(
            key: const PageStorageKey('districts-help-expanded'),
            tilePadding: const EdgeInsets.symmetric(horizontal: 12),
            leading: Icon(Icons.outlined_flag, color: theme.accent),
            iconColor: theme.accent,
            collapsedIconColor: theme.accent,
            title: Text(
              'COMO FUNCIONAM OS DISTRITAIS',
              style: TextStyle(
                color: theme.accent,
                fontWeight: FontWeight.w700,
              ),
            ),
            shape: const Border(),
            collapsedShape: const Border(),
            children: [_helpBody(theme)],
          ),
  );

  @override
  Widget build(BuildContext context) {
    final theme = ClassicTheme.of(context).paper;
    return LayoutBuilder(
      builder: (context, constraints) {
        final desktop = constraints.maxWidth >= desktopContentWidth;
        final main = Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            _intro(theme, desktop),
            SizedBox(height: desktop ? 18 : 12),
            _metrics(theme),
            if (!desktop) ...[const SizedBox(height: 12), _guide(theme, false)],
            SizedBox(height: desktop ? 18 : 12),
            _controls(theme, desktop),
          ],
        );
        return Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            if (desktop)
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Expanded(flex: 21, child: main),
                  const SizedBox(width: 14),
                  Expanded(flex: 10, child: _guide(theme, true)),
                ],
              )
            else
              main,
            if (notice != null) ...[const SizedBox(height: 12), notice!],
            if (clubList != null) ...[const SizedBox(height: 14), clubList!],
          ],
        );
      },
    );
  }
}
