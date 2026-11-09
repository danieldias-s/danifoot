import 'dart:math' as math;
import 'package:flutter/material.dart';
import 'club_theme.dart';
import 'visual_components.dart';

class CreatorBackdrop extends StatelessWidget {
  final Widget child;
  final bool stadium;
  const CreatorBackdrop({super.key, required this.child, this.stadium = false});

  @override
  Widget build(BuildContext context) {
    final theme = ClassicTheme.of(context);
    return DecoratedBox(
      decoration: BoxDecoration(
        color: stadium ? theme.primary : theme.background,
        image: stadium || theme.lightPanels
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
  final Color? ink;
  const CreatorBrand({super.key, this.size = 32, this.ink});
  @override
  Widget build(BuildContext context) {
    final theme = ClassicTheme.of(context);
    return Row(
      mainAxisSize: MainAxisSize.min,
      children: [
        Icon(
          Icons.sports_soccer,
          color: ink ?? theme.textPrimary,
          size: size + 7,
        ),
        const SizedBox(width: 9),
        Text(
          'LIGA CLÁSSICA ',
          style: TextStyle(
            fontFamily: 'ClassicCondensed',
            fontSize: size,
            fontWeight: FontWeight.w700,
            fontStyle: FontStyle.italic,
            decoration: TextDecoration.none,
            color: ink ?? theme.textPrimary,
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
  final ({int clubs, int divisions, int capacity})? divisionSummary;
  final String? stageDescription;
  final bool forceStadium;
  final String nextLabel;
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
    this.divisionSummary,
    this.stageDescription,
    this.forceStadium = false,
    this.nextLabel = 'CONTINUAR',
  });

  void _guide(BuildContext context) => showDialog<void>(
    context: context,
    builder: (context) => AlertDialog(
      title: const Text('GUIA DO CRIADOR'),
      content: SingleChildScrollView(
        child: Text(
          'Avance pelas ${steps.length} etapas para conferir a montagem antes de confirmar.\n\n'
          '${steps.asMap().entries.map((e) => '${e.key + 1}. ${e.value}').join('\n')}\n\n'
          'Os totais de clubes vêm do catálogo da categoria, dos países e dos anos escolhidos. '
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
                  child: FittedBox(
                    fit: BoxFit.scaleDown,
                    child: Row(
                      mainAxisSize: MainAxisSize.min,
                      children: [
                        Text(nextLabel, style: labelStyle),
                        const SizedBox(width: 6),
                        const Icon(Icons.arrow_forward, size: 20),
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
        final summary = divisionSummary;
        final divisionStage = summary != null;
        final stadiumStage =
            divisionStage || stageDescription != null || forceStadium;
        final frameInk = stadiumStage
            ? readableInk(theme.primary, Colors.white)
            : theme.textPrimary;
        return Scaffold(
          backgroundColor: theme.background,
          body: CreatorBackdrop(
            stadium: stadiumStage,
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
                                  child: CreatorBrand(
                                    size: mobile ? 25 : 34,
                                    ink: frameInk,
                                  ),
                                ),
                                if (mobile)
                                  Text(
                                    'CRIADOR DE MUNDOS',
                                    style: TextStyle(
                                      fontSize: 12,
                                      color: frameInk,
                                    ),
                                  ),
                              ],
                            ),
                          ),
                        ),
                        if (!mobile) ...[
                          Icon(Icons.language, color: frameInk, size: 30),
                          const SizedBox(width: 12),
                          Text(
                            'CRIADOR DE MUNDOS',
                            style: TextStyle(
                              color: frameInk,
                              fontSize: 25,
                              fontWeight: FontWeight.w700,
                              fontStyle: FontStyle.italic,
                            ),
                          ),
                          const Spacer(),
                        ],
                        if (mobile)
                          IconButton(
                            tooltip: 'GUIA DO CRIADOR',
                            onPressed: () => _guide(context),
                            icon: const Icon(Icons.menu_book_outlined),
                            color: theme.selectedBackground,
                          )
                        else
                          OutlinedButton.icon(
                            onPressed: () => _guide(context),
                            style: OutlinedButton.styleFrom(
                              foregroundColor: frameInk,
                              side: BorderSide(
                                color: theme.selectedBackground,
                                width: 2,
                              ),
                              shape: RoundedRectangleBorder(
                                borderRadius: BorderRadius.circular(18),
                              ),
                            ),
                            icon: const Icon(Icons.menu_book_outlined),
                            label: const Text('GUIA DO CRIADOR'),
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
                                                color: frameInk,
                                              )
                                            : Text(
                                                '${index + 1}',
                                                style: TextStyle(
                                                  fontWeight: FontWeight.bold,
                                                  color: index == step
                                                      ? theme.selectedText
                                                      : frameInk,
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
                                                  : frameInk,
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
                            maxWidth: step == 0
                                ? 720
                                : (stadiumStage ? 1340 : 1280),
                          ),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.stretch,
                            children: [
                              Padding(
                                padding: EdgeInsets.symmetric(
                                  vertical: mobile
                                      ? 12
                                      : (divisionStage ? 16 : 8),
                                ),
                                child: Column(
                                  children: [
                                    Text(
                                      '${step + 1}/${steps.length} • ${steps[step].toUpperCase()}',
                                      textAlign: TextAlign.center,
                                      style: TextStyle(
                                        color: frameInk,
                                        fontSize: mobile
                                            ? 30
                                            : (divisionStage ? 40 : 36),
                                        fontWeight: FontWeight.w700,
                                        height: 1.1,
                                      ),
                                    ),
                                    if (stageDescription
                                        case final description?) ...[
                                      const SizedBox(height: 6),
                                      Text(
                                        description,
                                        textAlign: TextAlign.center,
                                        style: TextStyle(
                                          color: frameInk,
                                          fontSize: mobile ? 16 : 19,
                                        ),
                                      ),
                                    ],
                                    if (step == 1 || summary != null) ...[
                                      const SizedBox(height: 8),
                                      Text(
                                        summary == null
                                            ? 'Escolha até 8 países para compor o seu mundo.'
                                            : 'Revise a distribuição automática dos clubes e troque equipes entre divisões.',
                                        textAlign: TextAlign.center,
                                        style: TextStyle(
                                          color: frameInk,
                                          fontSize: mobile ? 17 : 19,
                                        ),
                                      ),
                                      const SizedBox(height: 12),
                                      Container(
                                        padding: EdgeInsets.symmetric(
                                          horizontal: summary != null && mobile
                                              ? 14
                                              : 30,
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
                                          summary == null
                                              ? '$countryCount/8 • Países'
                                              : '${summary.clubs} clubes • ${summary.divisions} divisões'
                                                    '${mobile ? '' : ' • ${summary.capacity} clubes por divisão'}',
                                          textAlign: TextAlign.center,
                                          style: TextStyle(
                                            fontWeight: FontWeight.w700,
                                            fontSize: summary != null && mobile
                                                ? 20
                                                : 25,
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

class CreatorCountryFlag extends StatelessWidget {
  final String code;
  final String? countryName;
  final double width, height;
  const CreatorCountryFlag({
    super.key,
    required this.code,
    this.countryName,
    this.width = 46,
    this.height = 32,
  });

  @override
  Widget build(BuildContext context) {
    final normalized = code.trim().toUpperCase();
    final name = countryName?.trim().toLowerCase();
    // The football catalog uses GB for England. An explicit UK entry is distinct.
    final flagCode = switch (normalized) {
      'GB'
          when const {
            'reino unido',
            'united kingdom',
            'great britain',
          }.contains(name) =>
        'UK',
      'GB' || 'ENG' || 'GB-ENG' || 'EN' => 'ENG',
      _ => normalized,
    };
    return SizedBox(
      width: width,
      height: height,
      child: ClipRRect(
        borderRadius: BorderRadius.circular(math.min(5, height / 8)),
        child: flagCode.isEmpty
            ? ColoredBox(
                color: const Color(0xFFDCE8E0),
                child: Icon(
                  Icons.flag_outlined,
                  color: const Color(0xFF314B3C),
                  size: height * .8,
                ),
              )
            : CustomPaint(painter: _FlagPainter(flagCode)),
      ),
    );
  }
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
      case 'AT':
        rectangle(red, 0, 0, 1, 1 / 3);
        rectangle(red, 0, 2 / 3, 1, 1 / 3);
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
      case 'ENG':
        rectangle(red, .41, 0, .18, 1);
        rectangle(red, 0, .38, 1, .24);
      case 'UK':
        rectangle(const Color(0xFF012169), 0, 0, 1, 1);
        final topRight = Offset(size.width, 0);
        final bottomLeft = Offset(0, size.height);
        final bottomRight = Offset(size.width, size.height);
        final diagonal = Paint()
          ..color = white
          ..strokeWidth = size.height * .20;
        canvas.drawLine(Offset.zero, bottomRight, diagonal);
        canvas.drawLine(topRight, bottomLeft, diagonal);
        diagonal
          ..color = red
          ..strokeWidth = size.height * .08;
        canvas.drawLine(Offset.zero, bottomRight, diagonal);
        canvas.drawLine(topRight, bottomLeft, diagonal);
        rectangle(white, .40, 0, .20, 1);
        rectangle(white, 0, .33, 1, .34);
        rectangle(red, .44, 0, .12, 1);
        rectangle(red, 0, .40, 1, .20);
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
            style: TextStyle(
              color: const Color(0xFF102A1C),
              fontSize: math.min(
                size.height * .50,
                size.width * 1.4 / code.length,
              ),
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
