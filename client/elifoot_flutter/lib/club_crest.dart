import 'dart:convert';

import 'package:flutter/material.dart';
import 'club_theme.dart';

/// Catalog artwork with a club-color shield as a fallback.
class ClubCrest extends StatelessWidget {
  static final _images = <String, MemoryImage>{};
  final Map<String, dynamic>? palette;
  final String club;
  final double size;
  final String? crestDataUrl;
  const ClubCrest({
    super.key,
    required this.club,
    this.palette,
    this.size = 42,
    this.crestDataUrl,
  });

  static String? catalogImage(Map club) {
    final source = club['offlineSource'];
    final image = source is Map ? source['crestDataUrl'] : null;
    return image is String && image.isNotEmpty ? image : null;
  }

  Widget _shield() => CustomPaint(
    size: Size(size * .84, size),
    painter: _ClubShield(
      rgbColor(palette?['primaryColor']) ?? const Color(0xFF607D70),
      rgbColor(palette?['secondaryColor']) ?? Colors.white,
    ),
  );

  Widget _artwork() {
    final data = crestDataUrl;
    if (data != null &&
        (data.startsWith('data:image/png;base64,') ||
            data.startsWith('data:image/jpeg;base64,'))) {
      try {
        final image =
            _images.remove(data) ??
            MemoryImage(base64Decode(data.substring(data.indexOf(',') + 1)));
        _images[data] = image;
        if (_images.length > 128) _images.remove(_images.keys.first);
        return Image(
          image: image,
          width: size,
          height: size,
          fit: BoxFit.contain,
          excludeFromSemantics: true,
          errorBuilder: (_, _, _) => _shield(),
        );
      } on FormatException {
        return _shield();
      }
    }
    return _shield();
  }

  @override
  Widget build(BuildContext context) => Tooltip(
    message: club,
    child: Semantics(label: club, child: _artwork()),
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
