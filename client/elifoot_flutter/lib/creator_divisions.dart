import 'package:flutter/material.dart';

import 'club_theme.dart';
import 'creator_division_picker.dart';
import 'creator_visual.dart';

class CreatorDivisions extends StatefulWidget {
  // CreatorFrame adds 60 px of outer padding and 36 px of panel padding.
  static const desktopContentWidth = 1100.0;

  final List<List<Map<String, dynamic>?>> divisions;
  final List<Map<String, dynamic>> eligibleClubs;
  final bool busy;
  final bool manuallyChanged;
  final void Function(int division, int position, String clubKey) onSwap;
  final VoidCallback onRestore;

  const CreatorDivisions({
    super.key,
    required this.divisions,
    required this.eligibleClubs,
    required this.busy,
    required this.manuallyChanged,
    required this.onSwap,
    required this.onRestore,
  });

  @override
  State<CreatorDivisions> createState() => _CreatorDivisionsState();
}

class _CreatorDivisionsState extends State<CreatorDivisions> {
  int _division = 0;

  Future<void> _pickClub(int division, int position, String clubKey) async {
    final currentClub = widget.divisions[division][position];
    if (currentClub == null) return;
    final picked = await showCreatorDivisionPicker(
      context,
      divisions: widget.divisions,
      eligibleClubs: widget.eligibleClubs,
      currentClub: currentClub,
    );
    if (mounted &&
        picked != null &&
        !widget.busy &&
        widget.divisions[division][position]?['key']?.toString() == clubKey &&
        widget.eligibleClubs.any((club) => club['key']?.toString() == picked)) {
      widget.onSwap(division, position, picked);
    }
  }

  Future<void> _showUnassigned() async {
    await showCreatorDivisionPicker(
      context,
      divisions: widget.divisions,
      eligibleClubs: widget.eligibleClubs,
    );
  }

  Future<void> _restore() async {
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (context) => AlertDialog(
        title: const Text('Deseja restaurar a distribuição automática?'),
        content: const Text('As trocas manuais realizadas serão desfeitas.'),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(context, false),
            child: const Text('CANCELAR'),
          ),
          TextButton(
            onPressed: () => Navigator.pop(context, true),
            child: const Text('RESTAURAR'),
          ),
        ],
      ),
    );
    if (confirmed == true && mounted && !widget.busy) widget.onRestore();
  }

  Widget _guidance(ClassicTheme theme) => Container(
    padding: const EdgeInsets.symmetric(horizontal: 14, vertical: 11),
    decoration: BoxDecoration(
      color: theme.surfaceAlternate,
      border: Border.all(color: theme.border),
      borderRadius: BorderRadius.circular(10),
    ),
    child: Row(
      children: [
        Icon(Icons.info_outline, size: 24, color: theme.accent),
        const SizedBox(width: 12),
        Expanded(
          child: Text.rich(
            TextSpan(
              children: [
                const TextSpan(text: 'Toque em '),
                TextSpan(
                  text: 'TROCAR',
                  style: TextStyle(
                    fontWeight: FontWeight.w800,
                    color: readableOn(theme.accent, theme.surfaceAlternate),
                  ),
                ),
                const TextSpan(
                  text:
                      ' para substituir um clube por outro já distribuído ou ainda não escalado.',
                ),
              ],
            ),
            style: TextStyle(
              color: readableOn(theme.textPrimary, theme.surfaceAlternate),
              fontSize: 17,
              height: 1.25,
            ),
          ),
        ),
      ],
    ),
  );

  Widget _divisionCard(
    ClassicTheme theme,
    int division, {
    required bool desktop,
  }) {
    final clubs = widget.divisions[division];
    final occupied = clubs.where((club) => club != null).length;
    final highlight = theme.selectedBackground;
    final header = Color.lerp(theme.surface, highlight, .22)!;
    return Container(
      key: ValueKey('division-card-${division + 1}'),
      clipBehavior: Clip.antiAlias,
      decoration: BoxDecoration(
        color: theme.surface,
        border: Border.all(color: highlight, width: 1.5),
        borderRadius: BorderRadius.circular(12),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 13, vertical: 13),
            color: header,
            child: Row(
              children: [
                Icon(
                  division == 0
                      ? Icons.emoji_events
                      : Icons.emoji_events_outlined,
                  color: readableOn(theme.accent, header),
                  size: 24,
                ),
                const SizedBox(width: 9),
                Expanded(
                  child: Text(
                    desktop
                        ? '${division + 1}ª DIVISÃO'
                        : '${division + 1}ª DIVISÃO — $occupied CLUBES',
                    style: TextStyle(
                      color: readableOn(theme.textPrimary, header),
                      fontSize: 22,
                      fontWeight: FontWeight.w800,
                      height: 1.05,
                    ),
                  ),
                ),
              ],
            ),
          ),
          Padding(
            padding: const EdgeInsets.fromLTRB(12, 5, 10, 8),
            child: Column(
              children: [
                for (final (position, club) in clubs.indexed) ...[
                  if (position != 0)
                    Divider(
                      height: 1,
                      color: theme.border.withValues(alpha: .55),
                    ),
                  _clubRow(theme, division, position, club, desktop: desktop),
                ],
              ],
            ),
          ),
        ],
      ),
    );
  }

  Widget _clubRow(
    ClassicTheme theme,
    int division,
    int position,
    Map<String, dynamic>? club, {
    required bool desktop,
  }) {
    final name = club?['name']?.toString() ?? 'Vaga disponível';
    final clubKey = club?['key']?.toString();
    return ConstrainedBox(
      key: ValueKey('division-${division + 1}-position-${position + 1}'),
      constraints: BoxConstraints(minHeight: desktop ? 43 : 57),
      child: Row(
        children: [
          SizedBox(
            width: desktop ? 23 : 28,
            child: Text(
              '${position + 1}º',
              style: TextStyle(
                color: theme.accent,
                fontSize: 16,
                fontWeight: FontWeight.w700,
              ),
            ),
          ),
          if (club != null) ...[
            CreatorCountryFlag(
              code: club['countryCode']?.toString() ?? '',
              countryName: club['countryName']?.toString(),
              width: desktop ? 24 : 30,
              height: desktop ? 16 : 20,
            ),
            const SizedBox(width: 8),
          ],
          Expanded(
            child: Tooltip(
              message: name,
              excludeFromSemantics: true,
              child: Text(
                name,
                maxLines: desktop ? 2 : 3,
                overflow: TextOverflow.ellipsis,
                style: TextStyle(
                  color: club == null ? theme.textSecondary : theme.textPrimary,
                  fontSize: desktop ? 17 : 18,
                  height: 1.14,
                ),
              ),
            ),
          ),
          if (clubKey != null) ...[
            const SizedBox(width: 6),
            OutlinedButton.icon(
              key: ValueKey(
                'swap-division-${division + 1}-position-${position + 1}',
              ),
              style: _actionStyle(theme).copyWith(
                minimumSize: WidgetStatePropertyAll(Size(0, desktop ? 32 : 44)),
                padding: WidgetStatePropertyAll(
                  EdgeInsets.symmetric(horizontal: desktop ? 7 : 9),
                ),
                tapTargetSize: desktop
                    ? MaterialTapTargetSize.shrinkWrap
                    : MaterialTapTargetSize.padded,
                textStyle: const WidgetStatePropertyAll(
                  TextStyle(fontSize: 14, fontWeight: FontWeight.w700),
                ),
              ),
              onPressed: widget.busy || widget.eligibleClubs.length < 2
                  ? null
                  : () => _pickClub(division, position, clubKey),
              icon: const Icon(Icons.swap_horiz, size: 17),
              label: const Text('TROCAR'),
            ),
          ],
        ],
      ),
    );
  }

  ButtonStyle _actionStyle(ClassicTheme theme) => OutlinedButton.styleFrom(
    foregroundColor: theme.accent,
    side: BorderSide(color: theme.border),
    shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(9)),
    minimumSize: const Size(0, 44),
    visualDensity: VisualDensity.standard,
  );

  Widget _mobileDivisions(ClassicTheme theme) {
    final active = _division < widget.divisions.length ? _division : 0;
    final highlight = theme.selectedBackground;
    return DefaultTabController(
      length: widget.divisions.length,
      initialIndex: active,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          DecoratedBox(
            decoration: BoxDecoration(
              color: theme.surfaceAlternate,
              border: Border.all(color: theme.border),
              borderRadius: BorderRadius.circular(10),
            ),
            child: Padding(
              padding: const EdgeInsets.all(3),
              child: TabBar(
                labelColor: readableInk(highlight),
                unselectedLabelColor: theme.textPrimary,
                labelStyle: const TextStyle(
                  fontSize: 21,
                  fontWeight: FontWeight.w800,
                ),
                indicatorSize: TabBarIndicatorSize.tab,
                indicator: BoxDecoration(
                  color: highlight,
                  borderRadius: BorderRadius.circular(7),
                ),
                labelPadding: EdgeInsets.zero,
                onTap: (division) => setState(() => _division = division),
                tabs: [
                  for (final (index, _) in widget.divisions.indexed)
                    Tab(
                      key: ValueKey('division-tab-${index + 1}'),
                      height: 44,
                      child: Semantics(
                        label: '${index + 1}ª divisão',
                        excludeSemantics: true,
                        child: Text('${index + 1}ª'),
                      ),
                    ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 12),
          _divisionCard(theme, active, desktop: false),
        ],
      ),
    );
  }

  Widget _actions(ClassicTheme theme, {required bool desktop}) {
    final style = _actionStyle(theme);
    final restore = Semantics(
      hint: widget.manuallyChanged
          ? 'Desfaz as trocas manuais nas divisões.'
          : 'Distribui os clubes pelo ranking.',
      child: OutlinedButton.icon(
        key: const ValueKey('restore-automatic-divisions'),
        onPressed: widget.busy ? null : _restore,
        style: style,
        icon: const Icon(Icons.refresh, size: 23),
        label: const Text(
          'RESTAURAR DISTRIBUIÇÃO AUTOMÁTICA',
          textAlign: TextAlign.center,
        ),
      ),
    );
    final unassigned = OutlinedButton.icon(
      key: const ValueKey('view-unassigned-clubs'),
      onPressed: widget.busy ? null : _showUnassigned,
      style: style,
      icon: const Icon(Icons.groups_outlined, size: 23),
      label: const Text(
        'VER CLUBES NÃO ESCALADOS',
        textAlign: TextAlign.center,
      ),
    );
    if (desktop) {
      return Row(
        mainAxisAlignment: MainAxisAlignment.spaceBetween,
        children: [
          Flexible(child: restore),
          Flexible(child: unassigned),
        ],
      );
    }
    return Column(
      crossAxisAlignment: CrossAxisAlignment.stretch,
      children: [unassigned, const SizedBox(height: 8), restore],
    );
  }

  @override
  Widget build(BuildContext context) {
    final theme = ClassicTheme.of(context).paper;
    if (widget.divisions.isEmpty) {
      return const Text('Nenhuma divisão disponível.');
    }
    return LayoutBuilder(
      builder: (context, constraints) {
        final desktop =
            constraints.maxWidth >= CreatorDivisions.desktopContentWidth;
        return Column(
          key: ValueKey(
            desktop ? 'division-desktop-layout' : 'division-mobile-layout',
          ),
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            _guidance(theme),
            SizedBox(height: desktop ? 20 : 14),
            if (desktop)
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  for (final (index, _) in widget.divisions.indexed) ...[
                    if (index != 0) const SizedBox(width: 12),
                    Expanded(child: _divisionCard(theme, index, desktop: true)),
                  ],
                ],
              )
            else
              _mobileDivisions(theme),
            SizedBox(height: desktop ? 22 : 14),
            _actions(theme, desktop: desktop),
          ],
        );
      },
    );
  }
}
