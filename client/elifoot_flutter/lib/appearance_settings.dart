import 'package:flutter/material.dart';
import 'api_client.dart';
import 'club_theme.dart';

class AppearanceSettings extends StatefulWidget {
  final ElifootApiClient api;
  final ClubThemeController controller;
  const AppearanceSettings({
    super.key,
    required this.api,
    required this.controller,
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
      _draft = {'mode': 'Club', 'version': _original['version'] ?? 0};
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
  Widget build(BuildContext context) {
    final t = ClassicTheme.of(context), palette = widget.controller.palette;
    return PopScope(
      onPopInvokedWithResult: (didPop, result) {
        if (didPop && !_saved && _sameClub) {
          widget.controller.preview(_original);
        }
      },
      child: Scaffold(
        appBar: AppBar(title: const Text('APARÊNCIA')),
        body: SafeArea(
          child: Center(
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 720),
              child: ListView(
                padding: const EdgeInsets.all(16),
                children: [
                  Text('CORES DO JOGO', style: t.headingStyle),
                  if (!_sameClub)
                    const Text(
                      'CLUBE ALTERADO. Reabra Aparência para configurar o clube atual.',
                    ),
                  Text(
                    palette == null
                        ? 'Cores tradicionais não disponíveis; tema neutro.'
                        : '${palette['colorSource']} • ${palette['colorVerified'] == true ? 'VERIFICADAS' : 'PERSONALIZADAS'}',
                  ),
                  if (palette != null)
                    Text(
                      'Cores registradas: ${palette['primaryColor']} / ${palette['secondaryColor'] ?? 'não informada'}',
                    ),
                  const Text(
                    'As escolhas são suas e não alteram o catálogo nem a aparência dos outros treinadores.',
                  ),
                  Wrap(
                    spacing: 8,
                    runSpacing: 8,
                    children: [
                      OutlinedButton(
                        onPressed: _busy || !_sameClub
                            ? null
                            : () => _change('mode', 'Club'),
                        child: const Text('USAR CORES DO CLUBE'),
                      ),
                      OutlinedButton(
                        onPressed: _busy || !_sameClub ? null : _custom,
                        child: const Text('PERSONALIZAR CORES'),
                      ),
                      OutlinedButton(
                        onPressed: _busy || !_sameClub
                            ? null
                            : () => _save(restore: true),
                        child: const Text('RESTAURAR PADRÃO'),
                      ),
                    ],
                  ),
                  if (_draft['mode'] == 'Custom') ...[
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
                  Container(
                    key: const ValueKey('appearance-preview'),
                    margin: const EdgeInsets.symmetric(vertical: 12),
                    padding: const EdgeInsets.all(12),
                    decoration: BoxDecoration(
                      color: t.surface,
                      border: Border.all(color: t.border),
                    ),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        Text('PRÉVIA EM TEMPO REAL', style: t.headingStyle),
                        Text(
                          'Plantel • Jogos • Mercado • Finanças',
                          style: TextStyle(color: t.textPrimary),
                        ),
                        Container(
                          padding: const EdgeInsets.all(8),
                          color: t.selectedBackground,
                          child: Text(
                            'CLUBE  2 - 1  ADVERSÁRIO',
                            textAlign: TextAlign.center,
                            style: TextStyle(color: t.selectedText),
                          ),
                        ),
                        Text(
                          'Contraste texto/fundo: ${colorContrast(t.textPrimary, t.background).toStringAsFixed(2)}:1',
                        ),
                        const Text(
                          'Tonalidades visuais podem ser ajustadas para manter a leitura; cores registradas não são alteradas.',
                        ),
                      ],
                    ),
                  ),
                  if (widget.controller.clubId == null)
                    const Text(
                      'Selecione um clube para salvar sua preferência.',
                    ),
                  if (_error != null) Text('ERRO: $_error'),
                  if (_busy) const LinearProgressIndicator(),
                  ElevatedButton(
                    onPressed:
                        _busy || !_sameClub || widget.controller.clubId == null
                        ? null
                        : () => _save(),
                    child: const Text('SALVAR APARÊNCIA'),
                  ),
                ],
              ),
            ),
          ),
        ),
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
