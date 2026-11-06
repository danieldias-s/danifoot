import 'package:flutter/material.dart';

import 'club_theme.dart';

class CreatorDivisions extends StatefulWidget {
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
    final locations = <String, String>{};
    for (final (divisionIndex, clubs) in widget.divisions.indexed) {
      for (final (clubIndex, club) in clubs.indexed) {
        final key = club?['key']?.toString();
        if (key == null) continue;
        locations[key] =
            '${divisionIndex + 1}ª divisão • ${clubIndex + 1}º lugar';
      }
    }
    final candidates = widget.eligibleClubs.where((club) {
      final key = club['key']?.toString();
      return key != null && key != clubKey;
    }).toList();
    var query = '';
    final picked = await showDialog<String>(
      context: context,
      builder: (context) => StatefulBuilder(
        builder: (context, setDialogState) {
          final visible = candidates.where((club) {
            final text =
                '${club['name']} ${club['countryName'] ?? club['countryCode'] ?? ''}';
            return text.toLowerCase().contains(query);
          }).toList();
          return Dialog(
            insetPadding: const EdgeInsets.all(12),
            child: SizedBox(
              width: 520,
              height: 520,
              child: Padding(
                padding: const EdgeInsets.all(16),
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Text(
                      'Escolha o clube para trocar',
                      style: Theme.of(context).textTheme.titleLarge,
                    ),
                    const SizedBox(height: 12),
                    TextField(
                      key: const ValueKey('division-swap-search'),
                      decoration: const InputDecoration(
                        labelText: 'Pesquisar clube',
                        prefixIcon: Icon(Icons.search),
                      ),
                      onChanged: (value) => setDialogState(
                        () => query = value.trim().toLowerCase(),
                      ),
                    ),
                    const SizedBox(height: 8),
                    Expanded(
                      child: visible.isEmpty
                          ? const Center(
                              child: Text('Nenhum clube encontrado.'),
                            )
                          : ListView.builder(
                              itemCount: visible.length,
                              itemBuilder: (context, index) {
                                final club = visible[index];
                                final key = club['key'].toString();
                                final country =
                                    club['countryName'] ?? club['countryCode'];
                                final location =
                                    locations[key] ??
                                    'Disponível para escalação';
                                return ListTile(
                                  key: ValueKey('division-swap-option-$key'),
                                  contentPadding: const EdgeInsets.symmetric(
                                    horizontal: 6,
                                  ),
                                  title: Text(club['name'].toString()),
                                  subtitle: Text(
                                    country == null
                                        ? location
                                        : '$location • $country',
                                  ),
                                  onTap: () => Navigator.pop(context, key),
                                );
                              },
                            ),
                    ),
                    Align(
                      alignment: Alignment.centerRight,
                      child: TextButton(
                        onPressed: () => Navigator.pop(context),
                        child: const Text('CANCELAR'),
                      ),
                    ),
                  ],
                ),
              ),
            ),
          );
        },
      ),
    );
    if (mounted &&
        picked != null &&
        !widget.busy &&
        widget.divisions[division][position]?['key']?.toString() == clubKey &&
        widget.eligibleClubs.any((club) => club['key']?.toString() == picked)) {
      widget.onSwap(division, position, picked);
    }
  }

  Future<void> _restore() async {
    if (widget.manuallyChanged) {
      final confirmed = await showDialog<bool>(
        context: context,
        builder: (context) => AlertDialog(
          title: const Text('Restaurar distribuição automática?'),
          content: const Text(
            'A ordem original do ranking substituirá as trocas feitas nas quatro divisões.',
          ),
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
      if (confirmed != true) return;
    }
    if (mounted && !widget.busy) widget.onRestore();
  }

  @override
  Widget build(BuildContext context) {
    final theme = ClassicTheme.of(context).paper;
    final clubs = widget.divisions[_division];
    return DefaultTabController(
      length: widget.divisions.length,
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          TabBar(
            labelColor: theme.accent,
            unselectedLabelColor: theme.textSecondary,
            indicatorColor: theme.accent,
            labelPadding: const EdgeInsets.symmetric(horizontal: 3),
            onTap: (division) => setState(() => _division = division),
            tabs: [
              for (final (index, _) in widget.divisions.indexed)
                Tab(
                  key: ValueKey('division-tab-${index + 1}'),
                  child: FittedBox(
                    fit: BoxFit.scaleDown,
                    child: Text('${index + 1}ª DIVISÃO'),
                  ),
                ),
            ],
          ),
          const SizedBox(height: 8),
          for (final (position, club) in clubs.indexed) ...[
            Row(
              key: ValueKey(
                'division-${_division + 1}-position-${position + 1}',
              ),
              children: [
                SizedBox(
                  width: 32,
                  child: Text(
                    '${position + 1}º',
                    style: TextStyle(
                      color: theme.textSecondary,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                ),
                Expanded(
                  child: Text(
                    club?['name']?.toString() ?? 'Vaga disponível',
                    maxLines: 2,
                    overflow: TextOverflow.ellipsis,
                    style: TextStyle(
                      color: club == null
                          ? theme.textSecondary
                          : theme.textPrimary,
                      fontSize: 17,
                    ),
                  ),
                ),
                if (club != null)
                  Padding(
                    padding: const EdgeInsets.only(left: 6),
                    child: OutlinedButton(
                      key: ValueKey(
                        'swap-division-${_division + 1}-position-${position + 1}',
                      ),
                      style: OutlinedButton.styleFrom(
                        minimumSize: const Size(76, 44),
                        padding: const EdgeInsets.symmetric(horizontal: 10),
                        visualDensity: VisualDensity.standard,
                      ),
                      onPressed: widget.busy || widget.eligibleClubs.length < 2
                          ? null
                          : () => _pickClub(
                              _division,
                              position,
                              club['key'].toString(),
                            ),
                      child: const Text('TROCAR'),
                    ),
                  )
                else
                  const SizedBox(height: 44),
              ],
            ),
            Divider(height: 1, color: theme.border),
          ],
          const SizedBox(height: 12),
          Align(
            alignment: Alignment.centerLeft,
            child: OutlinedButton(
              key: const ValueKey('restore-automatic-divisions'),
              onPressed: widget.busy ? null : _restore,
              style: OutlinedButton.styleFrom(
                minimumSize: const Size(44, 44),
                visualDensity: VisualDensity.standard,
              ),
              child: const Text(
                'RESTAURAR DISTRIBUIÇÃO AUTOMÁTICA',
                textAlign: TextAlign.center,
              ),
            ),
          ),
        ],
      ),
    );
  }
}
