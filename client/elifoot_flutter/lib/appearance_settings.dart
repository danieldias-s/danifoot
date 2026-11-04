import 'package:flutter/material.dart';
import 'api_client.dart';
import 'club_theme.dart';
import 'visual_components.dart';
import 'presentation_version.dart';
import 'presentation_widgets.dart';

class AppearanceSettings extends StatefulWidget {
  final ElifootApiClient api;
  final ClubThemeController controller;
  final bool embedded;
  final VoidCallback? onClose;
  final PresentationController? presentation;
  final bool locked;
  const AppearanceSettings({
    super.key,
    required this.api,
    required this.controller,
    this.embedded = false,
    this.onClose,
    this.presentation,
    this.locked = false,
  });
  @override
  State<AppearanceSettings> createState() => _AppearanceSettingsState();
}

class _AppearanceSettingsState extends State<AppearanceSettings> {
  late Map<String, dynamic> _original, _draft;
  bool _busy = false, _saved = false;
  String? _error;
  late final String _openedScope;
  String get _scope =>
      '${widget.controller.ownerId}:${widget.controller.worldId}:${widget.controller.clubId}';
  bool get _sameClub => _scope == _openedScope;
  @override
  void initState() {
    super.initState();
    _openedScope = _scope;
    _original = Map.from(widget.controller.preferences);
    _draft = Map.from(_original);
  }

  void _change(String key, dynamic value) {
    if (!_sameClub) return;
    setState(() {
      _draft[key] = value;
      _saved = false;
    });
    widget.controller.preview(Map.from(_draft));
  }

  /// True when the draft renders the fixed classic green app theme.
  bool get _classic =>
      _draft['mode'] != 'Custom' && _draft['mode'] != 'ClubGlobal';
  int get _brightness => (_draft['brightness'] as num?)?.toInt() ?? 0;

  /// Green-theme tweaks always return the interface to the classic green.
  void _classicChange(String key, dynamic value) {
    if (!_sameClub) return;
    setState(() {
      if (!_classic) _draft['mode'] = 'Classic';
      _draft[key] = value;
      _saved = false;
    });
    widget.controller.preview(Map.from(_draft));
  }

  Widget _choice({
    required String key,
    required String label,
    required bool selected,
    required VoidCallback onTap,
  }) {
    final t = ClassicTheme.of(context);
    return OutlinedButton(
      key: ValueKey(key),
      style: OutlinedButton.styleFrom(
        minimumSize: const Size(44, 44),
        padding: const EdgeInsets.symmetric(horizontal: 8, vertical: 10),
        textStyle: const TextStyle(
          fontFamily: 'ClassicCondensed',
          fontSize: 18,
          fontWeight: FontWeight.bold,
        ),
        backgroundColor: selected
            ? t.selectedBackground
            : MediaQuery.sizeOf(context).width >= 1024
            ? t.primary
            : t.paper.surface,
        foregroundColor: selected
            ? t.selectedText
            : MediaQuery.sizeOf(context).width >= 1024
            ? t.textPrimary
            : t.paper.textPrimary,
      ),
      onPressed: _busy || !_sameClub ? null : onTap,
      child: Text(selected ? '[X] $label' : label),
    );
  }

  void _custom() {
    if (!_sameClub) return;
    final t = widget.controller.theme;
    setState(() {
      _saved = false;
      _draft = {
        ..._draft,
        'mode': 'Custom',
        'background': _draft['background'] ?? rgbHex(t.background),
        'primary': _draft['primary'] ?? rgbHex(t.primary),
        'secondary': _draft['secondary'] ?? rgbHex(t.secondary),
        'text': _draft['text'] ?? rgbHex(t.textPrimary),
        'accent': _draft['accent'] ?? rgbHex(t.accent),
      };
    });
    widget.controller.preview(Map.from(_draft));
  }

  Future<void> _save({bool restore = false}) async {
    if (!_sameClub) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    if (restore) {
      _draft = {'mode': 'Classic', 'version': _original['version'] ?? 0};
      widget.controller.preview(_draft);
    }
    try {
      await widget.controller.save(widget.api, _draft);
      if (!mounted || !_sameClub) return;
      setState(() {
        _saved = true;
        _original = Map.from(widget.controller.preferences);
        _draft = Map.from(_original);
      });
      ScaffoldMessenger.of(
        context,
      ).showSnackBar(const SnackBar(content: Text('APARÊNCIA SALVA.')));
    } catch (e) {
      if (mounted) setState(() => _error = '$e');
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  void dispose() {
    if (widget.embedded && !_saved && _sameClub) {
      final controller = widget.controller;
      final original = Map<String, dynamic>.from(_original);
      WidgetsBinding.instance.addPostFrameCallback((_) {
        if (controller.isActive) controller.preview(original);
      });
    }
    super.dispose();
  }

  Widget _options(
    String title,
    IconData icon,
    Widget controls, {
    Widget? footer,
  }) => ClassicPaperPanel(
    child: LayoutBuilder(
      builder: (context, c) => Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          if (c.maxWidth >= 760)
            Row(
              children: [
                Icon(
                  icon,
                  color: ClassicTheme.of(context).paper.accent,
                  size: 30,
                ),
                const SizedBox(width: 14),
                Expanded(
                  flex: 4,
                  child: Text(
                    title,
                    style: TextStyle(
                      color: ClassicTheme.of(context).paper.textPrimary,
                      fontSize: 20,
                      fontWeight: FontWeight.w700,
                    ),
                  ),
                ),
                Expanded(flex: 6, child: controls),
              ],
            )
          else ...[
            Container(
              padding: const EdgeInsets.all(9),
              color: ClassicTheme.of(context).primary,
              child: Text(
                title,
                style: TextStyle(
                  color: readableInk(
                    ClassicTheme.of(context).primary,
                    Colors.white,
                  ),
                  fontSize: 20,
                  fontWeight: FontWeight.bold,
                ),
              ),
            ),
            const SizedBox(height: 10),
            controls,
          ],
          if (footer != null) ...[const SizedBox(height: 8), footer],
        ],
      ),
    ),
  );

  @override
  Widget build(BuildContext context) {
    final t = ClassicTheme.of(context), palette = widget.controller.palette;
    Widget choices(List<Widget> buttons) => Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        for (var i = 0; i < buttons.length; i++) ...[
          if (i > 0) const SizedBox(width: 6),
          Expanded(child: buttons[i]),
        ],
      ],
    );
    final body = SingleChildScrollView(
      key: const Key('appearance-content'),
      padding: const EdgeInsets.all(12),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          ClassicPaperPanel(
            child: Row(
              children: [
                Icon(Icons.palette_outlined, color: t.paper.accent, size: 40),
                const SizedBox(width: 14),
                Expanded(
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      Text(
                        'TEMA DO JOGO',
                        style: TextStyle(
                          color: t.paper.textPrimary,
                          fontSize: 29,
                          fontWeight: FontWeight.w700,
                        ),
                      ),
                      Text(
                        'Aparência e personalização visual da interface',
                        style: TextStyle(
                          color: t.paper.textPrimary,
                          fontSize: 18,
                        ),
                      ),
                    ],
                  ),
                ),
                if (widget.embedded)
                  BackButton(
                    onPressed: widget.onClose,
                    color: t.paper.textPrimary,
                  ),
              ],
            ),
          ),
          const SizedBox(height: 12),
          if (widget.presentation != null) ...[
            ClassicPaperPanel(
              child: PresentationVersionSelector(
                api: widget.api,
                controller: widget.presentation!,
                title: 'APARÊNCIA',
                locked: widget.locked,
              ),
            ),
            const SizedBox(height: 12),
          ],
          ClassicPaperPanel(
            title: 'TEMA DO JOGO',
            child: ClassicPaper(
              builder: (_) => const Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    'A interface usa o verde clássico em qualquer clube. As cores dos clubes aparecem nas partidas, confrontos e listas de jogos.',
                    style: TextStyle(fontSize: 18),
                  ),
                  Text(
                    'As escolhas são suas e não alteram o catálogo nem a aparência dos outros treinadores.',
                    style: TextStyle(fontSize: 18),
                  ),
                ],
              ),
            ),
          ),
          if (!_sameClub)
            const Text(
              'CLUBE ALTERADO. Reabra Aparência para configurar o clube atual.',
            ),
          const SizedBox(height: 12),
          _options(
            'VARIAÇÃO DO VERDE CLÁSSICO',
            Icons.layers,
            choices([
              for (final v in const [
                ('Classico', 'CLÁSSICO'),
                ('Escuro', 'ESCURO'),
                ('AltoContraste', 'ALTO CONTRASTE'),
              ])
                _choice(
                  key: 'theme-variant-${v.$1}',
                  label: v.$2,
                  selected:
                      _classic &&
                      (_draft['themeVariant'] ?? 'Classico') == v.$1,
                  onTap: () => _classicChange('themeVariant', v.$1),
                ),
            ]),
          ),
          const SizedBox(height: 12),
          _options(
            'BRILHO',
            Icons.wb_sunny_outlined,
            choices([
              OutlinedButton(
                key: const ValueKey('brightness-down'),
                onPressed: _busy || !_sameClub || _brightness <= -2
                    ? null
                    : () => _classicChange('brightness', _brightness - 1),
                child: const Text('- BRILHO'),
              ),
              Padding(
                padding: const EdgeInsets.symmetric(vertical: 13),
                child: Text(
                  _brightness == 0
                      ? 'PADRÃO'
                      : _brightness > 0
                      ? '+$_brightness'
                      : '$_brightness',
                  key: const ValueKey('brightness-value'),
                  textAlign: TextAlign.center,
                  style: TextStyle(
                    color: t.paper.textPrimary,
                    fontSize: 17,
                    fontWeight: FontWeight.bold,
                  ),
                ),
              ),
              OutlinedButton(
                key: const ValueKey('brightness-up'),
                onPressed: _busy || !_sameClub || _brightness >= 2
                    ? null
                    : () => _classicChange('brightness', _brightness + 1),
                child: const Text('+ BRILHO'),
              ),
            ]),
          ),
          const SizedBox(height: 12),
          _options(
            'CORES DOS CLUBES NAS PARTIDAS',
            Icons.checkroom,
            choices([
              _choice(
                key: 'match-colors-Strong',
                label: 'CORES FORTES DOS CLUBES',
                selected: _draft['matchColors'] != 'Neutral',
                onTap: () => _change('matchColors', 'Strong'),
              ),
              _choice(
                key: 'match-colors-Neutral',
                label: 'MODO NEUTRO',
                selected: _draft['matchColors'] == 'Neutral',
                onTap: () => _change('matchColors', 'Neutral'),
              ),
            ]),
            footer: Text(
              palette == null
                  ? 'Seu clube não tem cores tradicionais registradas; nas partidas ele aparece neutro.'
                  : 'Seu clube nas partidas: ${palette['primaryColor']} / ${palette['secondaryColor'] ?? 'não informada'} • ${palette['colorVerified'] == true ? 'VERIFICADAS' : 'PERSONALIZADAS'}',
              style: TextStyle(color: t.paper.textPrimary, fontSize: 16),
            ),
          ),
          const SizedBox(height: 12),
          ClassicPaperPanel(
            padding: EdgeInsets.zero,
            child: ExpansionTile(
              key: const ValueKey('appearance-advanced'),
              initiallyExpanded: !_classic,
              backgroundColor: t.primary,
              collapsedBackgroundColor: t.primary,
              iconColor: readableInk(t.primary, Colors.white),
              collapsedIconColor: readableInk(t.primary, Colors.white),
              title: Text(
                'OPÇÕES AVANÇADAS',
                style: TextStyle(
                  color: readableInk(t.primary, Colors.white),
                  fontSize: 21,
                  fontWeight: FontWeight.bold,
                ),
              ),
              childrenPadding: EdgeInsets.zero,
              children: [
                Container(
                  color: t.paper.surface,
                  padding: const EdgeInsets.all(12),
                  child: Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      ClassicPaper(
                        builder: (_) => const Text(
                          'Não recomendadas: substituem o verde clássico na interface inteira.',
                        ),
                      ),
                      Wrap(
                        spacing: 8,
                        runSpacing: 8,
                        children: [
                          OutlinedButton(
                            onPressed: _busy || !_sameClub ? null : _custom,
                            child: const Text('PERSONALIZAR CORES'),
                          ),
                          OutlinedButton(
                            key: const ValueKey('appearance-club-global'),
                            onPressed: _busy || !_sameClub
                                ? null
                                : () => _change('mode', 'ClubGlobal'),
                            child: const Text('COR DO CLUBE NO JOGO INTEIRO'),
                          ),
                          OutlinedButton(
                            onPressed: _busy || !_sameClub
                                ? null
                                : () => _change('mode', 'Classic'),
                            child: const Text('VOLTAR AO VERDE CLÁSSICO'),
                          ),
                          OutlinedButton(
                            onPressed: _busy || !_sameClub
                                ? null
                                : () => _save(restore: true),
                            child: const Text('RESTAURAR PADRÃO'),
                          ),
                        ],
                      ),
                      if (_draft['mode'] == 'Custom')
                        for (final item in const [
                          ('background', 'COR DE FUNDO'),
                          ('primary', 'COR PRINCIPAL'),
                          ('secondary', 'COR SECUNDÁRIA'),
                          ('text', 'COR DO TEXTO'),
                          ('accent', 'COR DE DESTAQUE'),
                        ])
                          ColorChoice(
                            label: item.$2,
                            value: rgbColor(_draft[item.$1])!,
                            onChanged: _busy || !_sameClub
                                ? null
                                : (c) => _change(item.$1, rgbHex(c)),
                          ),
                    ],
                  ),
                ),
              ],
            ),
          ),
          const SizedBox(height: 12),
          ClassicPaperPanel(
            key: const ValueKey('appearance-preview'),
            title: 'PRÉVIA EM TEMPO REAL',
            child: ClassicPaper(
              builder: (context) => Column(
                crossAxisAlignment: CrossAxisAlignment.stretch,
                children: [
                  const Text(
                    'Plantel • Jogos • Mercado • Finanças',
                    style: TextStyle(fontSize: 18),
                  ),
                  const SizedBox(height: 8),
                  Container(
                    color: t.selectedBackground,
                    padding: const EdgeInsets.all(12),
                    child: Text(
                      'CLUBE  2 - 1  ADVERSÁRIO',
                      textAlign: TextAlign.center,
                      style: TextStyle(
                        color: t.selectedText,
                        fontSize: 23,
                        fontWeight: FontWeight.bold,
                      ),
                    ),
                  ),
                  const SizedBox(height: 6),
                  Text(
                    'Contraste texto/fundo: ${colorContrast(t.textPrimary, t.background).toStringAsFixed(2)}:1',
                    style: const TextStyle(fontSize: 17),
                  ),
                  const Text(
                    'Tonalidades visuais podem ser ajustadas para manter a leitura; cores registradas não são alteradas.',
                    style: TextStyle(fontSize: 17),
                  ),
                ],
              ),
            ),
          ),
          const SizedBox(height: 12),
          ClassicPaperPanel(
            child: LayoutBuilder(
              builder: (context, c) {
                final button = ElevatedButton(
                  key: const Key('save-appearance'),
                  onPressed:
                      _busy || !_sameClub || widget.controller.clubId == null
                      ? null
                      : () => _save(),
                  style: ElevatedButton.styleFrom(
                    minimumSize: const Size(44, 52),
                    backgroundColor: c.maxWidth < 600
                        ? t.selectedBackground
                        : t.buttonBackground,
                    foregroundColor: c.maxWidth < 600
                        ? t.selectedText
                        : t.buttonText,
                  ),
                  child: const Row(
                    mainAxisSize: MainAxisSize.min,
                    children: [
                      Icon(Icons.save_outlined),
                      SizedBox(width: 8),
                      Flexible(
                        child: Text(
                          'SALVAR APARÊNCIA',
                          textAlign: TextAlign.center,
                          style: TextStyle(
                            fontSize: 23,
                            fontWeight: FontWeight.bold,
                          ),
                        ),
                      ),
                    ],
                  ),
                );
                final message = Text(
                  _error != null
                      ? 'ERRO: $_error'
                      : widget.controller.clubId == null
                      ? 'Selecione um clube para salvar sua preferência.'
                      : _saved
                      ? 'Aparência salva para o seu treinador.'
                      : 'Suas preferências ficam salvas para este clube.',
                  style: TextStyle(color: t.paper.textPrimary, fontSize: 17),
                );
                return Column(
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    if (_busy) const LinearProgressIndicator(),
                    if (c.maxWidth >= 760)
                      Row(
                        children: [
                          Expanded(child: message),
                          const SizedBox(width: 12),
                          SizedBox(width: 340, child: button),
                        ],
                      )
                    else ...[
                      message,
                      const SizedBox(height: 10),
                      button,
                    ],
                  ],
                );
              },
            ),
          ),
        ],
      ),
    );
    if (widget.embedded) return body;
    return PopScope(
      onPopInvokedWithResult: (didPop, result) {
        if (didPop && !_saved && _sameClub) {
          widget.controller.preview(_original);
        }
      },
      child: Scaffold(
        appBar: AppBar(title: const Text('APARÊNCIA')),
        body: SafeArea(child: body),
      ),
    );
  }
}

/// Visual HSV controls plus exact RGB entry; usable with touch, keyboard and screen readers.
class ColorChoice extends StatefulWidget {
  final String label;
  final Color value;
  final ValueChanged<Color>? onChanged;
  const ColorChoice({
    super.key,
    required this.label,
    required this.value,
    this.onChanged,
  });
  @override
  State<ColorChoice> createState() => _ColorChoiceState();
}

class _ColorChoiceState extends State<ColorChoice> {
  late final TextEditingController _hex;
  String? _error;
  @override
  void initState() {
    super.initState();
    _hex = TextEditingController(text: rgbHex(widget.value));
  }

  @override
  void didUpdateWidget(covariant ColorChoice old) {
    super.didUpdateWidget(old);
    if (old.value != widget.value && _error == null) {
      _hex.text = rgbHex(widget.value);
    }
  }

  @override
  void dispose() {
    _hex.dispose();
    super.dispose();
  }

  Future<void> _picker() async {
    var hsv = HSVColor.fromColor(widget.value);
    await showDialog<void>(
      context: context,
      builder: (ctx) => StatefulBuilder(
        builder: (ctx, update) => AlertDialog(
          title: Text(widget.label),
          content: SizedBox(
            width: 360,
            child: Column(
              mainAxisSize: MainAxisSize.min,
              children: [
                Container(
                  height: 55,
                  color: hsv.toColor(),
                  child: Center(
                    child: Text(
                      rgbHex(hsv.toColor()),
                      style: TextStyle(color: readableInk(hsv.toColor())),
                    ),
                  ),
                ),
                for (final channel in const [
                  'Matiz',
                  'Saturação',
                  'Luminosidade',
                ])
                  Semantics(
                    label: channel,
                    child: Row(
                      children: [
                        SizedBox(width: 90, child: Text(channel)),
                        Expanded(
                          child: Slider(
                            value: channel == 'Matiz'
                                ? hsv.hue / 360
                                : channel == 'Saturação'
                                ? hsv.saturation
                                : hsv.value,
                            onChanged: (v) {
                              update(
                                () => hsv = channel == 'Matiz'
                                    ? hsv.withHue(v * 360)
                                    : channel == 'Saturação'
                                    ? hsv.withSaturation(v)
                                    : hsv.withValue(v),
                              );
                              widget.onChanged?.call(hsv.toColor());
                            },
                          ),
                        ),
                      ],
                    ),
                  ),
                Wrap(
                  spacing: 6,
                  children: [
                    for (final c in const [
                      Colors.red,
                      Colors.black,
                      Colors.white,
                      Colors.blue,
                      Colors.yellow,
                      Colors.green,
                      Colors.purple,
                      Colors.orange,
                    ])
                      Semantics(
                        label: rgbHex(c),
                        button: true,
                        child: InkWell(
                          onTap: () {
                            update(() => hsv = HSVColor.fromColor(c));
                            widget.onChanged?.call(c);
                          },
                          child: Container(
                            width: 32,
                            height: 32,
                            margin: const EdgeInsets.all(3),
                            decoration: BoxDecoration(
                              color: c,
                              border: Border.all(color: readableInk(c)),
                            ),
                          ),
                        ),
                      ),
                  ],
                ),
              ],
            ),
          ),
          actions: [
            TextButton(
              onPressed: () => Navigator.pop(ctx),
              child: const Text('CONCLUIR'),
            ),
          ],
        ),
      ),
    );
  }

  @override
  Widget build(BuildContext context) => Padding(
    padding: const EdgeInsets.symmetric(vertical: 8),
    child: Row(
      children: [
        Semantics(
          label: 'Selecionar ${widget.label}',
          button: true,
          child: InkWell(
            onTap: widget.onChanged == null ? null : _picker,
            child: Container(
              width: 46,
              height: 46,
              decoration: BoxDecoration(
                color: widget.value,
                border: Border.all(color: ClassicTheme.of(context).border),
              ),
              child: Icon(Icons.palette, color: readableInk(widget.value)),
            ),
          ),
        ),
        const SizedBox(width: 12),
        Expanded(
          child: TextField(
            key: ValueKey('color-hex-${widget.label}'),
            controller: _hex,
            enabled: widget.onChanged != null,
            decoration: InputDecoration(
              labelText: widget.label,
              errorText: _error,
            ),
            onChanged: (v) {
              final c = rgbColor(v);
              setState(() => _error = c == null ? 'Use #RRGGBB' : null);
              if (c != null) widget.onChanged?.call(c);
            },
          ),
        ),
      ],
    ),
  );
}
