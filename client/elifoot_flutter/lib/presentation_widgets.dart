import 'package:flutter/material.dart';
import 'api_client.dart';
import 'club_theme.dart';
import 'presentation_version.dart';

class PresentationScope extends InheritedNotifier<PresentationController> {
  const PresentationScope({
    super.key,
    required PresentationController controller,
    required super.child,
  }) : super(notifier: controller);
  static PresentationController? maybeOf(BuildContext context) =>
      context.dependOnInheritedWidgetOfExactType<PresentationScope>()?.notifier;
}

enum PresentationScreen {
  lobby,
  clubSelection,
  divisions,
  squad,
  tactics,
  market,
  finances,
  calendar,
  rankings,
  match,
  settings,
  worldCreator,
}

/// Future reference-based classic screens plug in here. Their builders must use
/// the existing application state/actions, not a second game implementation.
class PresentationSurface extends StatelessWidget {
  final PresentationScreen screen;
  final Widget current;
  final WidgetBuilder? classicBuilder;
  const PresentationSurface({
    super.key,
    required this.screen,
    required this.current,
    this.classicBuilder,
  });
  @override
  Widget build(BuildContext context) {
    final selected = PresentationScope.maybeOf(context)?.version;
    if (selected == PresentationVersion.classic && classicBuilder != null) {
      return classicBuilder!(context);
    }
    return current;
  }
}

/// Keep the Navigator in the same element slot for either selection. A change
/// must not recreate router/game state, tactical selections or connections.
class PresentationHost extends StatelessWidget {
  final Widget child;
  const PresentationHost({super.key, required this.child});
  @override
  Widget build(BuildContext context) {
    final preparing =
        PresentationScope.maybeOf(context)?.version ==
        PresentationVersion.classic;
    final theme = ClassicTheme.of(context);
    return Column(
      children: [
        preparing
            ? SafeArea(
                bottom: false,
                child: Material(
                  key: const Key('classic-presentation-pending'),
                  color: theme.selectedBackground,
                  child: Padding(
                    padding: const EdgeInsets.symmetric(
                      horizontal: 12,
                      vertical: 8,
                    ),
                    child: Row(
                      children: [
                        Icon(
                          Icons.construction,
                          size: 22,
                          color: theme.selectedText,
                        ),
                        const SizedBox(width: 8),
                        Expanded(
                          child: Text(
                            'VERSÃO CLÁSSICA (ANTIGA) em preparação. Exibindo as telas da VERSÃO ATUAL enquanto aguardamos as referências do proprietário.',
                            style: TextStyle(
                              color: theme.selectedText,
                              fontSize: 15,
                            ),
                          ),
                        ),
                      ],
                    ),
                  ),
                ),
              )
            : const SizedBox.shrink(),
        Expanded(
          key: const Key('shared-game-navigation'),
          child: Semantics(container: true, explicitChildNodes: true, child: child),
        ),
      ],
    );
  }
}

class PresentationVersionSelector extends StatelessWidget {
  final ElifootApiClient api;
  final PresentationController controller;
  final bool locked;
  final String title;
  const PresentationVersionSelector({
    super.key,
    required this.api,
    required this.controller,
    this.locked = false,
    this.title = 'ESCOLHA A APARÊNCIA:',
  });
  @override
  Widget build(BuildContext context) => ListenableBuilder(
    listenable: controller,
    builder: (context, _) {
      final disabled = locked || controller.busy || controller.owner == null;
      return Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Text(
            title,
            style: const TextStyle(fontSize: 20, fontWeight: FontWeight.bold),
          ),
          const Text(
            'A escolha é individual. As duas aparências usam o mesmo jogo.',
          ),
          RadioGroup<PresentationVersion>(
            groupValue: controller.version,
            onChanged: (value) {
              if (!disabled && value != null) {
                controller.select(api, value, locked: locked);
              }
            },
            child: Column(
              children: [
                for (final value in PresentationVersion.values)
                  RadioListTile<PresentationVersion>(
                    key: ValueKey('presentation-${value.code}'),
                    value: value,
                    enabled: !disabled,
                    contentPadding: EdgeInsets.zero,
                    title: Text(value.label),
                    subtitle: value == PresentationVersion.classic
                        ? const Text(
                            'Visual em preparação. Por enquanto, serão exibidas as telas atuais.',
                          )
                        : null,
                  ),
              ],
            ),
          ),
          if (controller.busy) const LinearProgressIndicator(),
          if (locked)
            const Text(
              'A troca de aparência fica bloqueada enquanto a navegação da partida está travada.',
            ),
          if (controller.error != null) Text(controller.error!),
          const Text(
            'Preferência do treinador, salva automaticamente ao selecionar.',
          ),
        ],
      );
    },
  );
}
