import 'dart:math' as math;
import 'package:flutter/material.dart';
import 'club_theme.dart';
import 'visual_components.dart';

class CreatorBackdrop extends StatelessWidget {
  final Widget child;
  const CreatorBackdrop({super.key, required this.child});

  @override
  Widget build(BuildContext context) {
    final theme = ClassicTheme.of(context);
    return DecoratedBox(
      decoration: BoxDecoration(
        color: theme.background,
        image: theme.lightPanels
            ? const DecorationImage(
                image: AssetImage('assets/images/creator-stadium.png'),
                fit: BoxFit.cover,
              )
            : null,
      ),
      child: CustomPaint(
        foregroundPainter: _CornerAccent(theme.selectedBackground),
        child: child,
      ),
    );
  }
}

class CreatorBrand extends StatelessWidget {
  final double size;
  const CreatorBrand({super.key, this.size = 32});
  @override
  Widget build(BuildContext context) {
    final theme = ClassicTheme.of(context);
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(Icons.sports_soccer, color: theme.textPrimary, size: size + 7),
        const SizedBox(width: 9),
        Text(
          'LIGA CLÁSSICA ',
          style: TextStyle(
            fontFamily: 'ClassicCondensed',
            fontSize: size,
            fontWeight: FontWeight.w700,
            fontStyle: FontStyle.italic,
            decoration: TextDecoration.none,
            color: theme.textPrimary,
            height: 1,
          ),
        ),
        Text(
          '98',
          style: TextStyle(
            fontFamily: 'ClassicCondensed',
            fontSize: size + 8,
            fontWeight: FontWeight.w700,
            fontStyle: FontStyle.italic,
            decoration: TextDecoration.none,
            color: theme.selectedBackground,
            height: 1,
          ),
        ),
      ],
    );
  }
}

class CreatorFrame extends StatelessWidget {
  final List<String> steps;
  final int step, countryCount;
  final Widget body;
  final bool busy;
  final String error;
  final VoidCallback? back, next;
  const CreatorFrame({
    super.key,
    required this.steps,
    required this.step,
    required this.countryCount,
    required this.body,
    required this.busy,
    required this.error,
    this.back,
    this.next,
  });

  void _guide(BuildContext context) => showDialog<void>(
    context: context,
    builder: (context) => AlertDialog(
      title: const Text('GUIA DO MODO'),
      content: SingleChildScrollView(
        child: Text(
          'Avance pelas oito etapas para conferir a montagem antes de confirmar.\n\n'
          '${steps.asMap().entries.map((e) => '${e.key + 1}. ${e.value}').join('\n')}\n\n'
          'Os totais de clubes vêm do catálogo da categoria e do ano escolhidos. '
          'As opções de dados exibem as autorizações necessárias e os motivos de bloqueio.',
        ),
      ),
      actions: [
        TextButton(
          onPressed: () => Navigator.pop(context),
          child: const Text('FECHAR'),
        ),
      ],
    ),
  );

  Widget _navigation(ClassicTheme theme, bool mobile) {
    final backFill = Color.lerp(Colors.white, theme.primary, .12)!;
    final backInk = readableInk(backFill);
    final inactiveBackFill = Color.lerp(backInk, theme.primary, .30)!;
    final inactiveBackInk = readableInk(inactiveBackFill);
    final mutedInk = readableOn(
      Color.lerp(
        readableInk(theme.primary, theme.textPrimary),
        theme.primary,
        .3,
      )!,
      theme.primary,
    );
    final shape = RoundedRectangleBorder(
      borderRadius: BorderRadius.circular(10),
    );
    const labelStyle = TextStyle(fontSize: 20, fontWeight: FontWeight.bold);
    return Padding(
      padding: EdgeInsets.fromLTRB(0, 12, 0, mobile ? 12 : 16),
      child: Row(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          Flexible(
            child: SizedBox(
              width: 220,
              height: 48,
              child: OutlinedButton(
                onPressed: back,
                style: OutlinedButton.styleFrom(
                  foregroundColor: backInk,
                  disabledForegroundColor: inactiveBackInk,
                  iconColor: backInk,
                  disabledIconColor: inactiveBackInk,
                  backgroundColor: backFill,
                  disabledBackgroundColor: inactiveBackFill,
                  side: BorderSide(
                    color: back == null
                        ? Color.lerp(inactiveBackFill, inactiveBackInk, .45)!
                        : backInk,
                    width: back == null ? 1 : 2,
                  ),
                  shape: shape,
                ),
                child: const FittedBox(
                  fit: BoxFit.scaleDown,
                  child: Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Icon(Icons.arrow_back, size: 20),
                      SizedBox(width: 6),
                      Text('VOLTAR', style: labelStyle),
                    ],
                  ),
                ),
              ),
            ),
          ),
          if (step < steps.length - 1) ...[
            SizedBox(width: mobile ? 12 : 16),
            Flexible(
              child: SizedBox(
                width: 260,
                height: 48,
                child: ElevatedButton(
                  onPressed: next,
                  style: ElevatedButton.styleFrom(
                    backgroundColor: theme.selectedBackground,
                    foregroundColor: theme.selectedText,
                    disabledBackgroundColor: theme.primary,
                    disabledForegroundColor: mutedInk,
                    shape: shape,
                  ),
                  child: const FittedBox(
                    fit: BoxFit.scaleDown,
                    child: Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Text('CONTINUAR', style: labelStyle),
                        SizedBox(width: 6),
                        Icon(Icons.arrow_forward, size: 20),
                      ],
                    ),
                  ),
                ),
              ),
            ),
          ],
        ],
      ),
    );
  }

  @override
  Widget build(BuildContext context) {
    final theme = ClassicTheme.of(context);
    return LayoutBuilder(
      builder: (context, constraints) {
        final mobile = constraints.maxWidth < 1024;
        return Scaffold(
          backgroundColor: theme.background,
          body: CreatorBackdrop(
            child: SafeArea(
              child: Column(
                children: [
                  Container(
                    padding: EdgeInsets.symmetric(
                      horizontal: mobile ? 8 : 28,
                      vertical: mobile ? 10 : 14,
                    ),
                    decoration: BoxDecoration(
                      color: theme.primary.withValues(alpha: .92),
                      border: Border(
                        bottom: BorderSide(
                          color: theme.selectedBackground,
                          width: 1,
                        ),
                      ),
                    ),
                    child: Row(
                      children: [
                        IconButton(
                          tooltip: 'VOLTAR À SALA',
                          onPressed: () => Navigator.maybePop(context),
                          icon: const Icon(Icons.arrow_back),
                          color: readableInk(theme.primary, theme.textPrimary),
                        ),
                        Expanded(
                          child: Align(
                            alignment: Alignment.centerLeft,
                            child: Column(
                              mainAxisSize: MainAxisSize.min,
                              crossAxisAlignment: CrossAxisAlignment.start,
                              children: [
                                FittedBox(
                                  fit: BoxFit.scaleDown,
                                  child: CreatorBrand(size: mobile ? 25 : 34),
                                ),
                                if (mobile)
                                  Text(
                                    'CRIADOR DE MUNDOS',
                                    style: TextStyle(
                                      fontSize: 12,
                                      color: theme.textPrimary,
                                    ),
                                  ),
                              ],
                            ),
                          ),
                        ),
                        if (!mobile) ...[
                          Icon(
                            Icons.language,
                            color: theme.textPrimary,
                            size: 30,
                          ),
                          const SizedBox(width: 12),
                          Text(
                            'CRIADOR DE MUNDOS',
                            style: TextStyle(
                              color: theme.textPrimary,
                              fontSize: 25,
                              fontWeight: FontWeight.w700,
                              fontStyle: FontStyle.italic,
                            ),
                          ),
                          const Spacer(),
                        ],
                        if (mobile)
                          IconButton(
                            tooltip: 'GUIA DO MODO',
                            onPressed: () => _guide(context),
                            icon: const Icon(Icons.menu_book_outlined),
                            color: theme.selectedBackground,
                          )
                        else
                          OutlinedButton.icon(
                            onPressed: () => _guide(context),
                            style: OutlinedButton.styleFrom(
                              foregroundColor: theme.textPrimary,
                              side: BorderSide(
                                color: theme.selectedBackground,
                                width: 2,
                              ),
                              shape: RoundedRectangleBorder(
                                borderRadius: BorderRadius.circular(18),
                              ),
                            ),
                            icon: const Icon(Icons.menu_book_outlined),
                            label: const Text('GUIA DO MODO'),
                          ),
                      ],
                    ),
                  ),
                  Center(
                    child: ConstrainedBox(
                      constraints: const BoxConstraints(maxWidth: 1340),
                      child: Padding(
                        padding: EdgeInsets.symmetric(
                          horizontal: mobile ? 16 : 24,
                          vertical: mobile ? 10 : 15,
                        ),
                        child: Row(
                          children: [
                            for (int index = 0; index < steps.length; index++)
                              Expanded(
                                child: Semantics(
                                  label: '${index + 1}. ${steps[index]}',
                                  selected: index == step,
                                  child: Row(
                                    children: [
                                      Container(
                                        width: mobile ? 24 : 29,
                                        height: mobile ? 24 : 29,
                                        decoration: BoxDecoration(
                                          shape: BoxShape.circle,
                                          color: index == step
                                              ? theme.selectedBackground
                                              : theme.primary,
                                          border: Border.all(
                                            color: index == step
                                                ? theme.selectedBackground
                                                : theme.border,
                                          ),
                                        ),
                                        alignment: Alignment.center,
                                        child: index < step
                                            ? Icon(
                                                Icons.check,
                                                size: 16,
                                                color: theme.textPrimary,
                                              )
                                            : Text(
                                                '${index + 1}',
                                                style: TextStyle(
                                                  fontWeight: FontWeight.bold,
                                                  color: index == step
                                                      ? theme.selectedText
                                                      : theme.textPrimary,
                                                ),
                                              ),
                                      ),
                                      if (!mobile) ...[
                                        const SizedBox(width: 6),
                                        Expanded(
                                          child: Text(
                                            steps[index],
                                            maxLines: 2,
                                            style: TextStyle(
                                              fontSize: 15,
                                              height: 1.05,
                                              color: index == step
                                                  ? theme.selectedBackground
                                                  : theme.textPrimary,
                                            ),
                                          ),
                                        ),
                                      ] else if (index < steps.length - 1)
                                        Expanded(
                                          child: Divider(
                                            color: theme.border,
                                            indent: 3,
                                            endIndent: 3,
                                          ),
                                        ),
                                    ],
                                  ),
                                ),
                              ),
                          ],
                        ),
                      ),
                    ),
                  ),
                  if (busy) const LinearProgressIndicator(minHeight: 3),
                  Expanded(
                    child: SingleChildScrollView(
                      key: ValueKey('creator-body-scroll-$step'),
                      padding: EdgeInsets.fromLTRB(
                        mobile ? 12 : 30,
                        0,
                        mobile ? 12 : 30,
                        16,
                      ),
                      child: Center(
                        child: ConstrainedBox(
                          constraints: BoxConstraints(
                            maxWidth: step == 0 ? 720 : 1280,
                          ),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.stretch,
                            children: [
                              Padding(
                                padding: EdgeInsets.symmetric(
                                  vertical: mobile ? 12 : 8,
                                ),
                                child: Column(
                                  children: [
                                    Text(
                                      step == 1
                                          ? 'CRIADOR DE MUNDOS'
                                          : '${step + 1}/8 • ${steps[step].toUpperCase()}',
                                      textAlign: TextAlign.center,
                                      style: TextStyle(
                                        color: theme.textPrimary,
                                        fontSize: step == 1
                                            ? (mobile ? 32 : 48)
                                            : (mobile ? 30 : 36),
                                        fontWeight: FontWeight.w700,
                                        fontStyle: step == 1
                                            ? FontStyle.italic
                                            : FontStyle.normal,
                                        height: 1.1,
                                      ),
                                    ),
                                    if (step == 1 || step == 2) ...[
                                      const SizedBox(height: 8),
                                      Text(
                                        step == 1
                                            ? 'Escolha até 8 países para compor o seu mundo.'
                                            : 'Defina como seu mundo será montado e quais dados serão usados.',
                                        textAlign: TextAlign.center,
                                        style: TextStyle(
                                          color: theme.textPrimary,
                                          fontSize: mobile ? 17 : 19,
                                        ),
                                      ),
                                    ],
                                    if (step == 1) ...[
                                      const SizedBox(height: 12),
                                      Container(
                                        padding: const EdgeInsets.symmetric(
                                          horizontal: 38,
                                          vertical: 4,
                                        ),
                                        decoration: BoxDecoration(
                                          border: Border.all(
                                            color: theme.selectedBackground,
                                            width: 2,
                                          ),
                                          borderRadius: BorderRadius.circular(
                                            30,
                                          ),
                                          color: theme.primary,
                                        ),
                                        child: Text(
                                          '$countryCount/8 • Países',
                                          style: TextStyle(
                                            fontWeight: FontWeight.w700,
                                            fontSize: 26,
                                            color: theme.selectedBackground,
                                          ),
                                        ),
                                      ),
                                    ],
                                  ],
                                ),
                              ),
                              if (error.isNotEmpty)
                                Container(
                                  margin: const EdgeInsets.only(bottom: 12),
                                  padding: const EdgeInsets.all(12),
                                  decoration: BoxDecoration(
                                    color: theme.primary,
                                    border: Border.all(
                                      color: theme.selectedBackground,
                                    ),
                                  ),
                                  child: Text(
                                    error,
                                    style: TextStyle(
                                      color: theme.selectedBackground,
                                    ),
                                  ),
                                ),
                              Container(
                                padding: EdgeInsets.all(mobile ? 14 : 18),
                                decoration: BoxDecoration(
                                  color: theme.paper.surface,
                                  borderRadius: BorderRadius.circular(16),
                                  border: Border.all(color: theme.paper.border),
                                ),
                                child: ClassicPaper(builder: (_) => body),
                              ),
                              if (step == 0) _navigation(theme, mobile),
                            ],
                          ),
                        ),
                      ),
                    ),
                  ),
                  if (step != 0)
                    Padding(
                      padding: const EdgeInsets.symmetric(horizontal: 16),
                      child: _navigation(theme, mobile),
                    ),
                ],
              ),
            ),
          ),
        );
      },
    );
  }
}

class CreatorField extends StatelessWidget {
  final IconData icon;
  final Widget child;
  final bool prominent;
  const CreatorField({
    super.key,
    required this.icon,
    required this.child,
    this.prominent = false,
  });
  @override
  Widget build(BuildContext context) {
    final theme = ClassicTheme.of(context);
    final foreground = prominent
        ? readableInk(theme.primary, Colors.white)
        : theme.textPrimary;
    return Container(
      margin: const EdgeInsets.only(bottom: 12),
      decoration: BoxDecoration(
        color: prominent ? theme.primary : theme.surfaceAlternate,
        border: Border.all(color: theme.border),
        borderRadius: BorderRadius.circular(8),
      ),
      child: Row(
        children: [
          Container(
            width: 58,
            height: 56,
            alignment: Alignment.center,
            decoration: BoxDecoration(
              color: theme.primary,
              borderRadius: BorderRadius.circular(7),
            ),
            child: Icon(icon, color: theme.selectedBackground, size: 28),
          ),
          Expanded(
            child: Padding(
              padding: const EdgeInsets.symmetric(horizontal: 14),
              child: Theme(
                data: Theme.of(context).copyWith(
                  textTheme: Theme.of(context).textTheme.apply(
                    bodyColor: foreground,
                    displayColor: foreground,
                  ),
                  inputDecorationTheme: InputDecorationTheme(
                    border: InputBorder.none,
                    enabledBorder: InputBorder.none,
                    focusedBorder: InputBorder.none,
                    labelStyle: TextStyle(color: foreground),
                    contentPadding: const EdgeInsets.symmetric(vertical: 5),
                  ),
                ),
                child: DefaultTextStyle.merge(
                  style: TextStyle(color: foreground),
                  child: child,
                ),
              ),
            ),
          ),
        ],
      ),
    );
  }
}

class CreatorConsent extends StatelessWidget {
  final String title;
  final String? description;
  final IconData icon;
  final bool value;
  final ValueChanged<bool?>? onChanged;
  const CreatorConsent({
    super.key,
    required this.title,
    this.description,
    required this.icon,
    required this.value,
    required this.onChanged,
  });
  @override
  Widget build(BuildContext context) {
    final theme = ClassicTheme.of(context);
    return DecoratedBox(
      decoration: BoxDecoration(
        border: Border(
          bottom: BorderSide(color: theme.border.withValues(alpha: .6)),
        ),
      ),
      child: CheckboxListTile(
        dense: true,
        visualDensity: const VisualDensity(vertical: -2),
        contentPadding: const EdgeInsets.symmetric(horizontal: 4, vertical: 2),
        secondary: Icon(icon, color: theme.accent, size: 27),
        title: Text(
          title,
          style: TextStyle(
            color: theme.textPrimary,
            fontWeight: FontWeight.w700,
            fontSize: 19,
            height: 1.15,
          ),
        ),
        subtitle: description == null
            ? null
            : Text(
                description!,
                style: TextStyle(
                  color: theme.textSecondary,
                  fontSize: 15,
                  height: 1.15,
                ),
              ),
        value: value,
        onChanged: onChanged,
        activeColor: theme.primary,
        checkColor: readableInk(theme.primary),
        checkboxShape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(3),
        ),
      ),
    );
  }
}

class CreatorCountryFlag extends StatelessWidget {
  final String code;
  const CreatorCountryFlag({super.key, required this.code});
  @override
  Widget build(BuildContext context) => SizedBox(
    width: 46,
    height: 32,
    child: ClipRRect(
      borderRadius: BorderRadius.circular(5),
      child: CustomPaint(painter: _FlagPainter(code)),
    ),
  );
}

class _FlagPainter extends CustomPainter {
  final String code;
  const _FlagPainter(this.code);
  @override
  void paint(Canvas canvas, Size size) {
    void rectangle(
      Color color,
      double x,
      double y,
      double width,
      double height,
    ) => canvas.drawRect(
      Rect.fromLTWH(
        x * size.width,
        y * size.height,
        width * size.width,
        height * size.height,
      ),
      Paint()..color = color,
    );
    const white = Colors.white,
        red = Color(0xFFD71920),
        blue = Color(0xFF003DA5),
        yellow = Color(0xFFFFD400);
    rectangle(white, 0, 0, 1, 1);
    switch (code) {
      case 'DE':
        rectangle(Colors.black, 0, 0, 1, 1 / 3);
        rectangle(red, 0, 1 / 3, 1, 1 / 3);
        rectangle(yellow, 0, 2 / 3, 1, 1 / 3);
      case 'FR':
      case 'IT':
        rectangle(
          code == 'FR' ? blue : const Color(0xFF009246),
          0,
          0,
          1 / 3,
          1,
        );
        rectangle(red, 2 / 3, 0, 1 / 3, 1);
      case 'GB':
        rectangle(red, .41, 0, .18, 1);
        rectangle(red, 0, .38, 1, .24);
      case 'ES':
        rectangle(red, 0, 0, 1, .25);
        rectangle(yellow, 0, .25, 1, .5);
        rectangle(red, 0, .75, 1, .25);
        rectangle(red, .24, .39, .10, .25);
      case 'AR':
        rectangle(const Color(0xFF75B5E7), 0, 0, 1, 1 / 3);
        rectangle(const Color(0xFF75B5E7), 0, 2 / 3, 1, 1 / 3);
        canvas.drawCircle(
          Offset(size.width / 2, size.height / 2),
          size.height * .10,
          Paint()..color = yellow,
        );
      case 'PT':
        rectangle(const Color(0xFF00963E), 0, 0, .4, 1);
        rectangle(red, .4, 0, .6, 1);
        canvas.drawCircle(
          Offset(size.width * .4, size.height * .5),
          size.height * .20,
          Paint()..color = yellow,
        );
        rectangle(white, .34, .38, .12, .24);
        rectangle(red, .37, .40, .06, .16);
      case 'BR':
        rectangle(const Color(0xFF009B3A), 0, 0, 1, 1);
        canvas.drawPath(
          Path()
            ..moveTo(size.width * .5, size.height * .1)
            ..lineTo(size.width * .9, size.height * .5)
            ..lineTo(size.width * .5, size.height * .9)
            ..lineTo(size.width * .1, size.height * .5)
            ..close(),
          Paint()..color = yellow,
        );
        canvas.drawCircle(
          Offset(size.width / 2, size.height / 2),
          size.height * .24,
          Paint()..color = blue,
        );
        canvas.drawLine(
          Offset(size.width * .36, size.height * .45),
          Offset(size.width * .65, size.height * .57),
          Paint()
            ..color = white
            ..strokeWidth = 2,
        );
      default:
        rectangle(const Color(0xFFDCE8E0), 0, 0, 1, 1);
        final text = TextPainter(
          text: TextSpan(
            text: code,
            style: const TextStyle(
              color: Color(0xFF102A1C),
              fontSize: 15,
              fontWeight: FontWeight.bold,
            ),
          ),
          textDirection: TextDirection.ltr,
        )..layout();
        text.paint(
          canvas,
          Offset(
            (size.width - text.width) / 2,
            (size.height - text.height) / 2,
          ),
        );
    }
  }

  @override
  bool shouldRepaint(_FlagPainter oldDelegate) => oldDelegate.code != code;
}

class _CornerAccent extends CustomPainter {
  final Color color;
  const _CornerAccent(this.color);
  @override
  void paint(Canvas canvas, Size size) {
    final length = math.min(170.0, size.width * .25);
    canvas.drawPath(
      Path()
        ..moveTo(size.width - length, size.height)
        ..lineTo(size.width, size.height - length * .6)
        ..lineTo(size.width, size.height - length * .6 + 12)
        ..lineTo(size.width - length + 20, size.height)
        ..close(),
      Paint()..color = color,
    );
  }

  @override
  bool shouldRepaint(_CornerAccent oldDelegate) => oldDelegate.color != color;
}
