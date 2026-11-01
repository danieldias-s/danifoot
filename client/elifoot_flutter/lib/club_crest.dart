import 'package:flutter/material.dart';
import 'club_theme.dart';

/// Generic club-color shield; the API does not supply licensed crest artwork.
class ClubCrest extends StatelessWidget {
  final Map<String, dynamic>? palette;
  final String club;
  final double size;
  const ClubCrest({
    super.key,
    required this.club,
    this.palette,
    this.size = 42,
  });
  @override
  Widget build(BuildContext context) => Tooltip(
    message: club,
    child: Semantics(
      label: club,
      child: CustomPaint(
        size: Size(size * .84, size),
        painter: _ClubShield(
          rgbColor(palette?['primaryColor']) ?? const Color(0xFF607D70),
          rgbColor(palette?['secondaryColor']) ?? Colors.white,
        ),
      ),
    ),
  );
}

class _ClubShield extends CustomPainter {
  final Color primary, secondary;
  _ClubShield(this.primary, this.secondary);
  @override
  void paint(Canvas canvas, Size size) {
    final path = Path()
      ..moveTo(2, 2)
      ..quadraticBezierTo(size.width / 2, 7, size.width - 2, 2)
      ..lineTo(size.width - 3, size.height * .53)
      ..quadraticBezierTo(
        size.width * .75,
        size.height * .84,
        size.width / 2,
        size.height - 2,
      )
      ..quadraticBezierTo(
        size.width * .25,
        size.height * .84,
        3,
        size.height * .53,
      )
      ..close();
    canvas.save();
    canvas.clipPath(path);
    canvas.drawPaint(Paint()..color = primary);
    canvas.drawRect(
      Rect.fromLTWH(size.width * .38, 0, size.width * .24, size.height),
      Paint()..color = secondary,
    );
    canvas.restore();
    canvas.drawPath(
      path,
      Paint()
        ..color = const Color(0xFF10271B)
        ..style = PaintingStyle.stroke
        ..strokeWidth = 1.5,
    );
  }

  @override
  bool shouldRepaint(covariant _ClubShield old) =>
      old.primary != primary || old.secondary != secondary;
}
