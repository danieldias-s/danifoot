import 'package:flutter/material.dart';
import 'club_theme.dart';

/// Read-only schematic. Expansion and repair progress remain server-owned.
class ClassicStadiumView extends StatelessWidget {
  final Map stadium;
  const ClassicStadiumView({super.key, required this.stadium});
  @override
  Widget build(BuildContext context) {
    final built = (stadium['builtCapacity'] as num?)?.toInt();
    final usable = (stadium['usableCapacity'] as num?)?.toInt();
    final condition = (stadium['conditionPercent'] as num?)?.toDouble();
    final expansion = (stadium['expansionRoundsRemaining'] as num? ?? 0)
        .toInt();
    final renovation = (stadium['renovationRoundsRemaining'] as num? ?? 0)
        .toInt();
    final t = ClassicTheme.of(context);
    return Container(
      key: const Key('classic-stadium-visual'),
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        color: t.surfaceAlternate,
        border: Border.all(color: t.border),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Semantics(
            label: built == null
                ? 'Estádio: capacidade ainda não informada'
                : 'Visão do estádio: $built lugares construídos, ${usable ?? 'não informado'} utilizáveis, condição ${condition ?? 'não informada'} por cento',
            image: true,
            child: Center(
              child: ConstrainedBox(
                constraints: const BoxConstraints(maxWidth: 800),
                child: AspectRatio(
                  aspectRatio: 1.8,
                  child: CustomPaint(
                    painter: ClassicStadiumPainter(
                      capacity: built,
                      usable: usable,
                      condition: condition,
                      expansion: expansion > 0,
                      renovation: renovation > 0,
                    ),
                  ),
                ),
              ),
            ),
          ),
          Text(
            'Visão do estádio • ${built == null ? 'capacidade não informada' : '$built lugares'}',
            textAlign: TextAlign.center,
            style: const TextStyle(fontWeight: FontWeight.bold),
          ),
          const SizedBox(height: 5),
          const Text(
            'Arquibancadas acompanham a capacidade construída. Setores escuros indicam lugares não utilizáveis.',
            textAlign: TextAlign.center,
          ),
          if (expansion > 0)
            Text(
              'Ampliação em andamento: $expansion rodada(s) restantes.',
              textAlign: TextAlign.center,
            ),
          if (renovation > 0)
            Text(
              'Reforma em andamento: $renovation rodada(s) restantes.',
              textAlign: TextAlign.center,
            ),
        ],
      ),
    );
  }
}

class ClassicStadiumPainter extends CustomPainter {
  final int? capacity, usable;
  final double? condition;
  final bool expansion, renovation;
  const ClassicStadiumPainter({
    required this.capacity,
    required this.usable,
    required this.condition,
    required this.expansion,
    required this.renovation,
  });
  @override
  void paint(Canvas canvas, Size size) {
    canvas.save();
    canvas.scale(size.width / 720, size.height / 400);
    canvas.drawRRect(
      RRect.fromRectAndRadius(
        const Rect.fromLTWH(0, 0, 720, 400),
        const Radius.circular(12),
      ),
      Paint()..color = const Color(0xFF174E2A),
    );
    canvas.translate(360, 208);
    canvas.scale(1, .72);
    canvas.rotate(-.12);
    final rows = capacity == null ? 1 : (capacity! / 6000).ceil().clamp(1, 12);
    final outer = Rect.fromCenter(
      center: Offset.zero,
      width: 370 + rows * 20,
      height: 220 + rows * 16,
    );
    canvas.drawRRect(
      RRect.fromRectAndRadius(
        outer.shift(const Offset(7, 12)),
        const Radius.circular(50),
      ),
      Paint()..color = const Color(0xFF09291B),
    );
    final usableFraction = capacity == null || capacity! <= 0 || usable == null
        ? 1.0
        : (usable! / capacity!).clamp(0.0, 1.0);
    final wear = 1 - ((condition ?? 100) / 100).clamp(0.0, 1.0);
    for (var row = rows - 1; row >= 0; row--) {
      final rect = Rect.fromCenter(
        center: Offset.zero,
        width: 370 + row * 20,
        height: 220 + row * 16,
      );
      final seatColor = Color.lerp(
        row.isEven ? const Color(0xFFD5D8D0) : const Color(0xFF8DABA1),
        const Color(0xFF8A725B),
        wear,
      )!;
      canvas.drawRRect(
        RRect.fromRectAndRadius(rect, const Radius.circular(38)),
        Paint()..color = seatColor,
      );
      // The closed portion comes from usable/built capacity, never a simulated crowd.
      if (usableFraction < 1) {
        canvas.save();
        canvas.clipRect(
          Rect.fromLTRB(
            rect.left,
            rect.top,
            rect.left + rect.width * (1 - usableFraction),
            rect.bottom,
          ),
        );
        canvas.drawRRect(
          RRect.fromRectAndRadius(rect, const Radius.circular(38)),
          Paint()..color = const Color(0xFF534B46),
        );
        canvas.restore();
      }
      final edge = Paint()
        ..color = const Color(0xFF536C62)
        ..style = PaintingStyle.stroke
        ..strokeWidth = 1;
      canvas.drawRRect(
        RRect.fromRectAndRadius(rect, const Radius.circular(38)),
        edge,
      );
      for (var x = -150.0; x <= 150; x += 50) {
        canvas.drawLine(Offset(x, rect.top), Offset(x, rect.top + 7), edge);
        canvas.drawLine(
          Offset(x, rect.bottom - 7),
          Offset(x, rect.bottom),
          edge,
        );
      }
    }
    canvas.drawRRect(
      RRect.fromRectAndRadius(
        const Rect.fromLTWH(-174, -100, 348, 200),
        const Radius.circular(18),
      ),
      Paint()..color = const Color(0xFFB2A994),
    );
    const field = Rect.fromLTWH(-150, -85, 300, 170);
    canvas.drawRect(field, Paint()..color = const Color(0xFF267B36));
    for (var stripe = 0; stripe < 10; stripe += 2) {
      canvas.drawRect(
        Rect.fromLTWH(-150 + stripe * 30, -85, 30, 170),
        Paint()..color = const Color(0xFF318940),
      );
    }
    final line = Paint()
      ..color = const Color(0xFFE8F3DD)
      ..style = PaintingStyle.stroke
      ..strokeWidth = 1.8;
    canvas.drawRect(field.deflate(5), line);
    canvas.drawLine(const Offset(0, -80), const Offset(0, 80), line);
    canvas.drawCircle(Offset.zero, 24, line);
    canvas.drawCircle(Offset.zero, 2, Paint()..color = line.color);
    for (final side in [-1, 1]) {
      canvas.drawRect(Rect.fromLTWH(side < 0 ? -145 : 100, -48, 45, 96), line);
      canvas.drawRect(Rect.fromLTWH(side < 0 ? -145 : 129, -25, 16, 50), line);
      canvas.drawRect(Rect.fromLTWH(side < 0 ? -155 : 145, -15, 10, 30), line);
    }
    if (expansion || renovation) {
      final building = Paint()
        ..color = const Color(0xFFFFC72A)
        ..style = PaintingStyle.stroke
        ..strokeWidth = 4;
      final x = outer.right + 12;
      canvas.drawLine(
        Offset(x, -outer.height / 2),
        Offset(x, outer.height / 2),
        building,
      );
      canvas.drawLine(
        Offset(x - 55, -outer.height / 2),
        Offset(x + 18, -outer.height / 2),
        building,
      );
      for (var y = -outer.height / 2; y < outer.height / 2; y += 22) {
        canvas.drawLine(Offset(x - 7, y), Offset(x + 7, y + 20), building);
      }
    }
    for (final side in [-1, 1]) {
      canvas.drawLine(
        Offset(side * (outer.width / 2 + 25), -95),
        Offset(side * (outer.width / 2 + 25), -165),
        Paint()
          ..color = const Color(0xFFD8DFD5)
          ..strokeWidth = 4,
      );
      canvas.drawRect(
        Rect.fromLTWH(side * (outer.width / 2 + 25) - 19, -171, 38, 12),
        Paint()..color = const Color(0xFFF6F0B8),
      );
    }
    canvas.restore();
  }

  @override
  bool shouldRepaint(ClassicStadiumPainter old) =>
      capacity != old.capacity ||
      usable != old.usable ||
      condition != old.condition ||
      expansion != old.expansion ||
      renovation != old.renovation;
}
