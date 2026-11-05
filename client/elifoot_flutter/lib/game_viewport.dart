import 'package:flutter/material.dart';

const double maxGameWidth = 1360;
const gameBackdrop = Color(0xFF002719);

/// Gives the game and its Navigator overlays the same responsive viewport.
class GameViewport extends StatelessWidget {
  const GameViewport({super.key, required this.child});

  final Widget child;

  @override
  Widget build(BuildContext context) => ColoredBox(
    color: gameBackdrop,
    child: Center(
      child: ConstrainedBox(
        constraints: const BoxConstraints(maxWidth: maxGameWidth),
        child: SizedBox.expand(
          child: LayoutBuilder(
            builder: (context, constraints) {
              final media = MediaQuery.of(context);
              return MediaQuery(
                data: media.copyWith(
                  size: Size(constraints.maxWidth, media.size.height),
                ),
                child: child,
              );
            },
          ),
        ),
      ),
    ),
  );
}
