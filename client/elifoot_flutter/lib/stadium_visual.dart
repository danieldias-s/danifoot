import 'dart:math' as math;
import 'package:flutter/material.dart';
import 'classic98_widgets.dart';

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
    return Container(
      key: const Key('classic-stadium-visual'),
      decoration: BoxDecoration(
        color: ClassicColors.creamLight,
        border: Border.all(color: ClassicColors.border, width: 1.2),
      ),
      child: Container(
        padding: const EdgeInsets.fromLTRB(10, 10, 10, 12),
        decoration: const BoxDecoration(
          border: Border(
            top: BorderSide(color: ClassicColors.bevelLight, width: 1.3),
            left: BorderSide(color: ClassicColors.bevelLight, width: 1.3),
            bottom: BorderSide(color: ClassicColors.bevelDark, width: 1.3),
            right: BorderSide(color: ClassicColors.bevelDark, width: 1.3),
          ),
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
                    aspectRatio: 1.52,
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
            const SizedBox(height: 8),
            Text(
              'Visão do estádio • ${built == null ? 'capacidade não informada' : '$built lugares'}',
              textAlign: TextAlign.center,
              style: const TextStyle(
                fontFamily: ClassicTypography.fontFamily,
                fontSize: 18,
                fontWeight: FontWeight.w900,
                color: ClassicColors.rosterBlack,
                height: 1.1,
              ),
            ),
            const SizedBox(height: 3),
            const Text(
              'Arquibancadas acompanham a capacidade construída. Setores escuros indicam lugares não utilizáveis.',
              textAlign: TextAlign.center,
              style: TextStyle(
                fontFamily: ClassicTypography.fontFamily,
                fontSize: 13,
                fontStyle: FontStyle.italic,
                color: Color(0xFF2B2923),
                height: 1.15,
              ),
            ),
            if (expansion > 0)
              Padding(
                padding: const EdgeInsets.only(top: 4),
                child: Text(
                  'Ampliação em andamento: $expansion rodada(s) restantes.',
                  textAlign: TextAlign.center,
                  style: const TextStyle(
                    fontFamily: ClassicTypography.fontFamily,
                    fontSize: 13,
                    fontWeight: FontWeight.w700,
                    color: ClassicColors.rosterBlack,
                  ),
                ),
              ),
            if (renovation > 0)
              Padding(
                padding: const EdgeInsets.only(top: 4),
                child: Text(
                  'Reforma em andamento: $renovation rodada(s) restantes.',
                  textAlign: TextAlign.center,
                  style: const TextStyle(
                    fontFamily: ClassicTypography.fontFamily,
                    fontSize: 13,
                    fontWeight: FontWeight.w700,
                    color: ClassicColors.rosterBlack,
                  ),
                ),
              ),
          ],
        ),
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
    canvas.scale(size.width / 720, size.height / 470);

    final bgRect = RRect.fromRectAndRadius(
      const Rect.fromLTWH(0, 0, 720, 470),
      const Radius.circular(12),
    );
    canvas.clipRRect(bgRect);
    canvas.drawRRect(
      bgRect,
      Paint()
        ..shader = const LinearGradient(
          begin: Alignment.topCenter,
          end: Alignment.bottomCenter,
          colors: [Color(0xFF0E451E), Color(0xFF185E2B), Color(0xFF0F471F)],
        ).createShader(const Rect.fromLTWH(0, 0, 720, 470)),
    );

    // Park trees in the corners
    void drawTreeCluster(double cx, double cy, double scale) {
      final shadow = Paint()..color = const Color(0x55041D0B);
      final darkFoliage = Paint()..color = const Color(0xFF154E20);
      final midFoliage = Paint()..color = const Color(0xFF257330);
      final lightFoliage = Paint()..color = const Color(0xFF3A8F42);
      canvas.drawOval(
        Rect.fromCenter(
          center: Offset(cx + 4 * scale, cy + 10 * scale),
          width: 58 * scale,
          height: 30 * scale,
        ),
        shadow,
      );
      for (final offset in [
        Offset(-14 * scale, 4 * scale),
        Offset(14 * scale, 5 * scale),
        Offset(0, -6 * scale),
      ]) {
        canvas.drawCircle(Offset(cx, cy) + offset, 16 * scale, darkFoliage);
        canvas.drawCircle(
          Offset(cx, cy) + offset + Offset(-2 * scale, -2 * scale),
          13 * scale,
          midFoliage,
        );
        canvas.drawCircle(
          Offset(cx, cy) + offset + Offset(-4 * scale, -5 * scale),
          7 * scale,
          lightFoliage,
        );
      }
    }

    drawTreeCluster(74, 398, 1.15);
    drawTreeCluster(128, 422, 0.9);
    drawTreeCluster(645, 395, 1.15);
    drawTreeCluster(592, 424, 0.9);
    drawTreeCluster(155, 86, 0.95);
    drawTreeCluster(215, 76, 0.8);
    drawTreeCluster(565, 86, 0.95);
    drawTreeCluster(505, 76, 0.8);

    final rows = capacity == null ? 2 : (capacity! / 5500).ceil().clamp(2, 12);
    final outerW = 470.0 + rows * 13.0;
    final outerH = 278.0 + rows * 10.0;
    const center = Offset(360, 248);

    final outerRect = Rect.fromCenter(
      center: center,
      width: outerW,
      height: outerH,
    );
    const outerRadius = Radius.circular(64);

    // Drop shadow under stadium
    canvas.drawRRect(
      RRect.fromRectAndRadius(
        outerRect.shift(const Offset(0, 22)),
        const Radius.circular(68),
      ),
      Paint()..color = const Color(0x77051F0D),
    );

    // Concrete outer facade wall (visible along the front/bottom & lower sides)
    final wallRect = outerRect.shift(const Offset(0, 14));
    canvas.drawRRect(
      RRect.fromRectAndRadius(wallRect, outerRadius),
      Paint()
        ..shader = const LinearGradient(
          begin: Alignment.topCenter,
          end: Alignment.bottomCenter,
          colors: [Color(0xFF9C988E), Color(0xFF6E6B64)],
        ).createShader(wallRect),
    );
    canvas.drawRRect(
      RRect.fromRectAndRadius(wallRect, outerRadius),
      Paint()
        ..color = const Color(0xFF47453F)
        ..style = PaintingStyle.stroke
        ..strokeWidth = 2,
    );

    // Arched windows & pillars along front facade
    final windowPaint = Paint()..color = const Color(0xFF1F3828);
    final pillarPaint = Paint()
      ..color = const Color(0xFFB8B4A9)
      ..strokeWidth = 2;
    for (var x = outerRect.left + 58; x <= outerRect.right - 58; x += 26) {
      if ((x - center.dx).abs() < 32) continue;
      final wy = outerRect.bottom - 2;
      canvas.drawRRect(
        RRect.fromRectAndCorners(
          Rect.fromLTWH(x - 6, wy, 12, 13),
          topLeft: const Radius.circular(5),
          topRight: const Radius.circular(5),
        ),
        windowPaint,
      );
      canvas.drawLine(
        Offset(x + 7, wy - 2),
        Offset(x + 7, wy + 13),
        pillarPaint,
      );
    }

    // Main front entrance gate
    final gateRect = Rect.fromCenter(
      center: Offset(center.dx, outerRect.bottom + 4),
      width: 52,
      height: 20,
    );
    canvas.drawRRect(
      RRect.fromRectAndRadius(gateRect, const Radius.circular(3)),
      Paint()..color = const Color(0xFFDAD5C8),
    );
    canvas.drawRect(
      Rect.fromLTWH(gateRect.left + 8, gateRect.top + 5, 15, 15),
      Paint()..color = const Color(0xFF174A25),
    );
    canvas.drawRect(
      Rect.fromLTWH(gateRect.right - 23, gateRect.top + 5, 15, 15),
      Paint()..color = const Color(0xFF174A25),
    );

    // Upper rim / roof structure
    canvas.drawRRect(
      RRect.fromRectAndRadius(outerRect, outerRadius),
      Paint()..color = const Color(0xFFD2CEC2),
    );
    // Terracotta/brown roof highlight on left/west curve
    canvas.save();
    canvas.clipRect(
      Rect.fromLTWH(
        outerRect.left - 5,
        outerRect.top + 20,
        95,
        outerRect.height - 40,
      ),
    );
    canvas.drawRRect(
      RRect.fromRectAndRadius(outerRect.deflate(2), outerRadius),
      Paint()..color = const Color(0xFF8C6249),
    );
    canvas.restore();

    final usableFraction = capacity == null || capacity! <= 0 || usable == null
        ? 1.0
        : (usable! / capacity!).clamp(0.0, 1.0);
    final wear = 1 - ((condition ?? 100) / 100).clamp(0.0, 1.0);

    // Tiered grandstands
    for (var row = rows - 1; row >= 0; row--) {
      final t = rows <= 1 ? 0.0 : row / (rows - 1);
      final w = 368.0 + t * (outerW - 392.0);
      final h = 204.0 + t * (outerH - 226.0);
      final rect = Rect.fromCenter(center: center, width: w, height: h);
      final radius = Radius.circular(34 + t * 22);
      final seatColor = Color.lerp(
        row.isEven ? const Color(0xFFC9CCC6) : const Color(0xFF9AA39E),
        const Color(0xFF7B6752),
        wear,
      )!;
      canvas.drawRRect(
        RRect.fromRectAndRadius(rect, radius),
        Paint()..color = seatColor,
      );
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
          RRect.fromRectAndRadius(rect, radius),
          Paint()..color = const Color(0xFF4B4540),
        );
        canvas.restore();
      }
      canvas.drawRRect(
        RRect.fromRectAndRadius(rect, radius),
        Paint()
          ..color = const Color(0xFF606864)
          ..style = PaintingStyle.stroke
          ..strokeWidth = 1.1,
      );
    }

    // Yellow stairways across the stands
    final stairPaint = Paint()
      ..color = const Color(0xFFE5B724)
      ..strokeWidth = 5.5
      ..strokeCap = StrokeCap.butt;
    final innerBowlW = 356.0;
    final innerBowlH = 192.0;
    final standDepthX = (outerW - 24 - innerBowlW) / 2;
    final standDepthY = (outerH - 22 - innerBowlH) / 2;
    for (final dx in [-115.0, 0.0, 115.0]) {
      // Top stand stairs
      canvas.drawLine(
        Offset(center.dx + dx, center.dy - innerBowlH / 2),
        Offset(center.dx + dx * 1.06, center.dy - innerBowlH / 2 - standDepthY),
        stairPaint,
      );
      // Bottom stand stairs
      canvas.drawLine(
        Offset(center.dx + dx, center.dy + innerBowlH / 2),
        Offset(center.dx + dx * 1.06, center.dy + innerBowlH / 2 + standDepthY),
        stairPaint,
      );
    }
    for (final dy in [-42.0, 42.0]) {
      // Left stand stairs
      canvas.drawLine(
        Offset(center.dx - innerBowlW / 2, center.dy + dy),
        Offset(center.dx - innerBowlW / 2 - standDepthX, center.dy + dy * 1.08),
        stairPaint,
      );
      // Right stand stairs
      canvas.drawLine(
        Offset(center.dx + innerBowlW / 2, center.dy + dy),
        Offset(center.dx + innerBowlW / 2 + standDepthX, center.dy + dy * 1.08),
        stairPaint,
      );
    }

    // Inner pitch surround / track
    final surroundRect = Rect.fromCenter(
      center: center,
      width: innerBowlW,
      height: innerBowlH,
    );
    canvas.drawRRect(
      RRect.fromRectAndRadius(surroundRect, const Radius.circular(24)),
      Paint()..color = const Color(0xFF205E2A),
    );
    canvas.drawRRect(
      RRect.fromRectAndRadius(surroundRect, const Radius.circular(24)),
      Paint()
        ..color = const Color(0xFF4A524D)
        ..style = PaintingStyle.stroke
        ..strokeWidth = 2.2,
    );

    // Football pitch
    final field = Rect.fromCenter(center: center, width: 316, height: 162);
    canvas.save();
    canvas.clipRRect(RRect.fromRectAndRadius(field, const Radius.circular(6)));
    canvas.drawRect(field, Paint()..color = const Color(0xFF247E2E));
    final stripeW = field.width / 10;
    for (var i = 0; i < 10; i += 2) {
      canvas.drawRect(
        Rect.fromLTWH(
          field.left + i * stripeW,
          field.top,
          stripeW,
          field.height,
        ),
        Paint()..color = const Color(0xFF2F9138),
      );
    }
    canvas.restore();

    // Pitch markings
    final line = Paint()
      ..color = const Color(0xFFF0F7EC)
      ..style = PaintingStyle.stroke
      ..strokeWidth = 1.8;
    final pitchLines = field.deflate(6);
    canvas.drawRect(pitchLines, line);
    canvas.drawLine(
      Offset(center.dx, pitchLines.top),
      Offset(center.dx, pitchLines.bottom),
      line,
    );
    canvas.drawCircle(center, 24, line);
    canvas.drawCircle(center, 2.2, Paint()..color = line.color);

    for (final side in [-1, 1]) {
      final penaltyW = 44.0;
      final penaltyH = 88.0;
      final goalBoxW = 16.0;
      final goalBoxH = 44.0;
      final px = side < 0 ? pitchLines.left : pitchLines.right - penaltyW;
      final gx = side < 0 ? pitchLines.left : pitchLines.right - goalBoxW;
      canvas.drawRect(
        Rect.fromLTWH(px, center.dy - penaltyH / 2, penaltyW, penaltyH),
        line,
      );
      canvas.drawRect(
        Rect.fromLTWH(gx, center.dy - goalBoxH / 2, goalBoxW, goalBoxH),
        line,
      );
      // Penalty arc
      final arcRect = Rect.fromCenter(
        center: Offset(
          side < 0 ? pitchLines.left + 32 : pitchLines.right - 32,
          center.dy,
        ),
        width: 30,
        height: 30,
      );
      canvas.drawArc(
        arcRect,
        side < 0 ? -math.pi / 3 : math.pi * 2 / 3,
        math.pi * 2 / 3,
        false,
        line,
      );
      // Goal net
      final netX = side < 0 ? pitchLines.left - 8 : pitchLines.right;
      canvas.drawRect(Rect.fromLTWH(netX, center.dy - 13, 8, 26), line);
    }

    // Floodlight towers (top-left and top-right)
    void drawFloodlight(double baseX, double baseY, bool leftSide) {
      const topY = 78.0;
      final towerX = leftSide ? baseX - 10 : baseX + 10;
      final strutPaint = Paint()
        ..color = const Color(0xFFC5CEC8)
        ..strokeWidth = 2.2
        ..style = PaintingStyle.stroke;
      // Lattice legs
      canvas.drawLine(
        Offset(towerX - 8, baseY),
        Offset(towerX - 4, topY),
        strutPaint,
      );
      canvas.drawLine(
        Offset(towerX + 8, baseY),
        Offset(towerX + 4, topY),
        strutPaint,
      );
      for (var y = topY + 12; y < baseY - 6; y += 14) {
        canvas.drawLine(
          Offset(towerX - 6, y),
          Offset(towerX + 6, y + 10),
          strutPaint..strokeWidth = 1.4,
        );
        canvas.drawLine(
          Offset(towerX + 6, y),
          Offset(towerX - 6, y + 10),
          strutPaint,
        );
      }

      // Soft light glow
      final headCenter = Offset(towerX, topY - 12);
      canvas.drawCircle(
        headCenter,
        46,
        Paint()
          ..shader = RadialGradient(
            colors: const [
              Color(0x66FFF8C2),
              Color(0x22FFF8C2),
              Color(0x00FFF8C2),
            ],
          ).createShader(Rect.fromCircle(center: headCenter, radius: 46)),
      );

      // Tilted lamp panel
      canvas.save();
      canvas.translate(headCenter.dx, headCenter.dy);
      canvas.rotate(leftSide ? -0.24 : 0.24);
      final panelRect = Rect.fromCenter(
        center: Offset.zero,
        width: 48,
        height: 26,
      );
      canvas.drawRRect(
        RRect.fromRectAndRadius(panelRect, const Radius.circular(3)),
        Paint()..color = const Color(0xFF4A524E),
      );
      canvas.drawRRect(
        RRect.fromRectAndRadius(panelRect, const Radius.circular(3)),
        Paint()
          ..color = const Color(0xFFDCE3DD)
          ..style = PaintingStyle.stroke
          ..strokeWidth = 1.5,
      );
      final bulbPaint = Paint()..color = const Color(0xFFFFF7BE);
      for (var r = -1; r <= 1; r++) {
        for (var c = -2; c <= 2; c++) {
          canvas.drawCircle(Offset(c * 8.2, r * 6.8), 2.6, bulbPaint);
        }
      }
      canvas.restore();
    }

    drawFloodlight(outerRect.left + 26, center.dy - 58, true);
    drawFloodlight(outerRect.right - 26, center.dy - 58, false);

    // Construction crane when expansion or renovation is active
    if (expansion || renovation) {
      final cranePaint = Paint()
        ..color = const Color(0xFFFFC72A)
        ..style = PaintingStyle.stroke
        ..strokeWidth = 4;
      final x = outerRect.right - 10;
      canvas.drawLine(
        Offset(x, center.dy - 110),
        Offset(x, center.dy + 95),
        cranePaint,
      );
      canvas.drawLine(
        Offset(x - 70, center.dy - 110),
        Offset(x + 22, center.dy - 110),
        cranePaint,
      );
      for (var y = center.dy - 105; y < center.dy + 85; y += 22) {
        canvas.drawLine(Offset(x - 7, y), Offset(x + 7, y + 18), cranePaint);
      }
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
