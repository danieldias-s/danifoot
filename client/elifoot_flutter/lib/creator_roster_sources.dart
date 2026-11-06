import 'package:flutter/material.dart';

import 'club_theme.dart';

class CreatorRosterSources extends StatelessWidget {
  final bool generatedOnly, completeMissing, fictionalWorld;
  final ValueChanged<bool>? onChooseGenerated;
  final Widget advanced;

  const CreatorRosterSources({
    super.key,
    required this.generatedOnly,
    required this.completeMissing,
    required this.fictionalWorld,
    required this.onChooseGenerated,
    required this.advanced,
  });

  Widget _choice(ClassicTheme theme, bool generated, bool compact) {
    final selected = generated == (generatedOnly || fictionalWorld);
    final accent = readableOn(theme.accent, theme.surface);
    final fill = selected
        ? Color.lerp(theme.surface, const Color(0xFFBCE9CB), .53)!
        : theme.tableRow;
    final border = selected ? accent : theme.border;
    final title = generated ? 'USAR ELENCO FICTÍCIO' : 'USAR ELENCO REAL';
    return Semantics(
      button: true,
      selected: selected,
      child: Material(
        color: fill,
        shape: RoundedRectangleBorder(
          side: BorderSide(color: border, width: selected ? 2 : 1.5),
          borderRadius: BorderRadius.circular(13),
        ),
        clipBehavior: Clip.antiAlias,
        child: InkWell(
          key: ValueKey(generated ? 'source-generated' : 'source-real'),
          onTap: onChooseGenerated == null
              ? null
              : () => onChooseGenerated!(generated),
          child: Container(
            constraints: BoxConstraints(minHeight: compact ? 112 : 158),
            padding: EdgeInsets.symmetric(
              horizontal: compact ? 12 : 22,
              vertical: compact ? 14 : 22,
            ),
            child: Row(
              children: [
                Icon(
                  selected
                      ? Icons.radio_button_checked
                      : Icons.radio_button_off,
                  color: selected ? accent : theme.textSecondary,
                  size: compact ? 25 : 31,
                ),
                SizedBox(width: compact ? 13 : 23),
                Icon(
                  generated ? Icons.casino_rounded : Icons.storage_rounded,
                  color: accent,
                  size: compact ? 37 : 57,
                ),
                SizedBox(width: compact ? 11 : 19),
                SizedBox(
                  width: 1,
                  height: compact ? 51 : 74,
                  child: ColoredBox(color: theme.border),
                ),
                SizedBox(width: compact ? 11 : 16),
                Expanded(
                  child: Column(
                    mainAxisSize: MainAxisSize.min,
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        title,
                        style: TextStyle(
                          color: readableOn(theme.textPrimary, fill),
                          fontWeight: FontWeight.w800,
                          fontSize: compact ? 20 : 25,
                          height: 1.05,
                        ),
                      ),
                      const SizedBox(height: 6),
                      Text(
                        generated
                            ? 'Gera elencos totalmente fictícios para todos os clubes.'
                            : 'Utiliza os elencos do catálogo (dados reais).',
                        style: TextStyle(
                          color: readableOn(theme.textPrimary, fill),
                          fontSize: compact ? 16 : 19,
                          height: 1.2,
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            ),
          ),
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final theme = ClassicTheme.of(context).paper;
    return LayoutBuilder(
      builder: (context, constraints) {
        final compact = constraints.maxWidth < 760;
        final real = _choice(theme, false, compact);
        final generated = _choice(theme, true, compact);
        final notice = Color.lerp(
          theme.surface,
          theme.selectedBackground,
          .24,
        )!;
        return Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Row(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Icon(
                  Icons.groups_rounded,
                  color: theme.accent,
                  size: compact ? 35 : 45,
                ),
                const SizedBox(width: 12),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'Escolha a origem dos elencos',
                        style: theme.headingStyle.copyWith(
                          fontSize: compact ? 23 : 28,
                          height: 1.1,
                        ),
                      ),
                      const SizedBox(height: 4),
                      Text(
                        'Defina como serão gerados os elencos dos clubes do seu mundo.',
                        style: TextStyle(
                          color: theme.textPrimary,
                          fontSize: compact ? 16 : 20,
                        ),
                      ),
                    ],
                  ),
                ),
              ],
            ),
            SizedBox(height: compact ? 18 : 24),
            if (compact) ...[
              real,
              const SizedBox(height: 10),
              generated,
            ] else
              IntrinsicHeight(
                child: Row(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Expanded(child: real),
                    const SizedBox(width: 16),
                    Expanded(child: generated),
                  ],
                ),
              ),
            SizedBox(height: compact ? 16 : 22),
            Container(
              key: const ValueKey('roster-source-notice'),
              padding: EdgeInsets.all(compact ? 13 : 19),
              decoration: BoxDecoration(
                color: notice,
                borderRadius: BorderRadius.circular(12),
                border: Border.all(color: theme.selectedBackground),
              ),
              child: Row(
                children: [
                  Icon(
                    Icons.info_outline,
                    size: compact ? 26 : 34,
                    color: readableOn(theme.accent, notice),
                  ),
                  SizedBox(width: compact ? 10 : 14),
                  Expanded(
                    child: Text(
                      fictionalWorld
                          ? 'Mundo totalmente fictício autorizado nas opções avançadas. Os clubes e os elencos serão gerados.'
                          : generatedOnly
                          ? 'O jogo gera todos os atletas. Os clubes escolhidos na montagem são preservados.'
                          : completeMissing
                          ? 'Ao continuar com USAR ELENCO REAL, você autoriza o jogo a completar com jogadores fictícios os elencos que não tiverem dados completos.'
                          : 'As opções avançadas exigem elencos reais sem complemento fictício. Escolha USAR ELENCO REAL para autorizar o complemento.',
                      style: TextStyle(
                        color: readableOn(theme.textPrimary, notice),
                        fontSize: compact ? 16 : 20,
                        height: 1.25,
                      ),
                    ),
                  ),
                ],
              ),
            ),
            const SizedBox(height: 8),
            ExpansionTile(
              key: const PageStorageKey('roster-advanced'),
              tilePadding: const EdgeInsets.symmetric(horizontal: 8),
              title: Text(
                'OPÇÕES AVANÇADAS E FONTES',
                style: TextStyle(
                  color: theme.accent,
                  fontWeight: FontWeight.w700,
                ),
              ),
              children: [advanced],
            ),
          ],
        );
      },
    );
  }
}
