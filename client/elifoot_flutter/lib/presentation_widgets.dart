import 'package:flutter/material.dart';
import 'api_client.dart';
import 'game_viewport.dart';
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

/// Reference-based classic screens plug in here. Their builders must use
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
  Widget build(BuildContext context) => GameViewport(
    child: Semantics(
      key: const Key('shared-game-navigation'),
      container: true,
      explicitChildNodes: true,
      child: child,
    ),
  );
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
                            'Interface compacta inspirada no Elifoot 98. Mesmo jogo e mesmos dados.',
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
