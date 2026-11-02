import 'package:flutter/material.dart';

/// Color is the authoritative domain band, including room-specific thresholds.
class MoraleBar extends StatelessWidget {
  final num value;
  final String band;
  const MoraleBar({super.key, required this.value, required this.band});
  @override
  Widget build(BuildContext context) {
    final color = switch (band) {
      'VERMELHA' => const Color(0xFFEF5350),
      'AMARELA' => const Color(0xFFFFD54F),
      _ => const Color(0xFF81C784),
    };
    return Semantics(
      label: 'Moral do treinador',
      value: '${value.clamp(0, 100)} de 100, $band',
      child: ExcludeSemantics(
        child: LayoutBuilder(
          builder: (context, c) => Container(
            width: c.maxWidth.isFinite ? c.maxWidth.clamp(28.0, 104.0) : 104,
            height: 16,
            decoration: BoxDecoration(
              border: Border.all(color: Colors.white54),
            ),
            padding: const EdgeInsets.all(2),
            child: LinearProgressIndicator(
              key: const ValueKey('morale-bar-fill'),
              value: value.clamp(0, 100) / 100,
              color: color,
              backgroundColor: const Color(0xFF55665B),
              minHeight: 10,
            ),
          ),
        ),
      ),
    );
  }
}
