import 'package:flutter/material.dart';

abstract final class AccountColors {
  static const background = Color(0xFF002F23),
      dark = Color(0xFF003F2B),
      action = Color(0xFF006D40),
      paper = Color(0xFFF8F9F2),
      tint = Color(0xFFE9F3EC),
      yellow = Color(0xFFFFD12D),
      ink = Color(0xFF0A2B1D),
      border = Color(0xFFC7DCD1),
      muted = Color(0xFF466C5A);
  static ThemeData theme() => ThemeData(
    useMaterial3: false,
    fontFamily: 'AccountSans',
    scaffoldBackgroundColor: tint,
    colorScheme: const ColorScheme.light(
      primary: action,
      secondary: yellow,
      surface: paper,
      onSurface: ink,
      onPrimary: Colors.white,
    ),
    textTheme: ThemeData.light().textTheme.apply(
      fontFamily: 'AccountSans',
      bodyColor: ink,
      displayColor: ink,
    ),
    inputDecorationTheme: InputDecorationTheme(
      filled: true,
      fillColor: Colors.white,
      contentPadding: const EdgeInsets.symmetric(horizontal: 14, vertical: 15),
      border: OutlineInputBorder(
        borderRadius: BorderRadius.circular(8),
        borderSide: const BorderSide(color: border),
      ),
      enabledBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(8),
        borderSide: const BorderSide(color: border),
      ),
      focusedBorder: OutlineInputBorder(
        borderRadius: BorderRadius.circular(8),
        borderSide: const BorderSide(color: action, width: 2),
      ),
    ),
    elevatedButtonTheme: ElevatedButtonThemeData(
      style: ElevatedButton.styleFrom(
        backgroundColor: action,
        foregroundColor: Colors.white,
        minimumSize: const Size(48, 48),
        elevation: 0,
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
        textStyle: const TextStyle(
          fontFamily: 'AccountSans',
          fontWeight: FontWeight.bold,
          fontSize: 17,
        ),
      ),
    ),
    outlinedButtonTheme: OutlinedButtonThemeData(
      style: OutlinedButton.styleFrom(
        foregroundColor: dark,
        backgroundColor: Colors.white,
        minimumSize: const Size(48, 48),
        side: const BorderSide(color: border),
        shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
      ),
    ),
    textButtonTheme: TextButtonThemeData(
      style: TextButton.styleFrom(
        foregroundColor: action,
        minimumSize: const Size(48, 48),
      ),
    ),
  );
}

class AccountBrand extends StatelessWidget {
  final bool compact;
  const AccountBrand({super.key, this.compact = false});
  @override
  Widget build(BuildContext context) => Row(
    mainAxisSize: MainAxisSize.min,
    children: [
      Icon(Icons.sports_soccer, color: Colors.white, size: compact ? 30 : 40),
      const SizedBox(width: 8),
      Flexible(
        child: Text.rich(
          TextSpan(
            children: [
              const TextSpan(text: 'LIGA CLÁSSICA '),
              TextSpan(
                text: '98',
                style: const TextStyle(color: AccountColors.yellow),
              ),
            ],
          ),
          maxLines: 1,
          style: TextStyle(
            fontFamily: 'ClassicCondensed',
            fontWeight: FontWeight.bold,
            fontStyle: FontStyle.italic,
            fontSize: compact ? 24 : 36,
            color: Colors.white,
          ),
        ),
      ),
    ],
  );
}

class AccountActionLabel extends StatelessWidget {
  final String label;
  final bool arrow, star;
  final TextStyle? style;
  const AccountActionLabel(
    this.label, {
    super.key,
    this.arrow = false,
    this.star = false,
    this.style,
  });
  @override
  Widget build(BuildContext context) => Row(
    mainAxisSize: MainAxisSize.min,
    mainAxisAlignment: MainAxisAlignment.center,
    children: [
      if (star) ...[
        Icon(Icons.star, size: style?.fontSize ?? 18, color: style?.color),
        const SizedBox(width: 5),
      ],
      Flexible(child: Text(label, style: style)),
      if (arrow) ...[
        const SizedBox(width: 6),
        const Icon(Icons.arrow_forward, size: 18),
      ],
    ],
  );
}

class AccountLoginScreen extends StatefulWidget {
  final Future<void> Function(
    String username,
    String password,
    String? displayName,
  )
  submit;
  final String? notice;
  const AccountLoginScreen({super.key, required this.submit, this.notice});
  @override
  State<AccountLoginScreen> createState() => _AccountLoginScreenState();
}

class _AccountLoginScreenState extends State<AccountLoginScreen> {
  final _form = GlobalKey<FormState>();
  final _username = TextEditingController(),
      _password = TextEditingController(),
      _name = TextEditingController();
  bool _register = false, _obscure = true, _busy = false;
  String? _error;
  @override
  void dispose() {
    _username.dispose();
    _password.clear();
    _password.dispose();
    _name.dispose();
    super.dispose();
  }

  Future<void> _submit() async {
    if (_busy || !_form.currentState!.validate()) return;
    setState(() {
      _busy = true;
      _error = null;
    });
    try {
      await widget.submit(
        _username.text.trim(),
        _password.text,
        _register ? _name.text.trim() : null,
      );
    } catch (e) {
      if (mounted) {
        setState(
          () => _error = e.toString().replaceFirst(RegExp(r'^Exception: '), ''),
        );
      }
    } finally {
      if (mounted) setState(() => _busy = false);
    }
  }

  @override
  Widget build(BuildContext context) => Theme(
    data: AccountColors.theme(),
    child: Builder(
      builder: (context) => Scaffold(
        body: CustomPaint(
          painter: _AccountBackdrop(),
          child: LayoutBuilder(
            builder: (context, box) => SingleChildScrollView(
              keyboardDismissBehavior: ScrollViewKeyboardDismissBehavior.onDrag,
              child: ConstrainedBox(
                constraints: BoxConstraints(minHeight: box.maxHeight),
                child: FocusTraversalGroup(
                  child: Column(
                    mainAxisAlignment: MainAxisAlignment.spaceBetween,
                    children: [
                      Align(
                        alignment: box.maxWidth > 700
                            ? Alignment.topLeft
                            : Alignment.center,
                        child: Padding(
                          padding: EdgeInsets.only(
                            left: box.maxWidth > 700 ? 72 : 24,
                            right: 24,
                            top: 32,
                            bottom: 24,
                          ),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.start,
                            children: [
                              const AccountBrand(),
                              const SizedBox(height: 6),
                              const Text(
                                'MANAGER DE FUTEBOL CLÁSSICO',
                                style: TextStyle(
                                  color: Color(0xFFCCE0D6),
                                  letterSpacing: 2,
                                  fontSize: 12,
                                ),
                              ),
                            ],
                          ),
                        ),
                      ),
                      Padding(
                        padding: const EdgeInsets.symmetric(
                          horizontal: 16,
                          vertical: 16,
                        ),
                        child: Center(
                          child: ConstrainedBox(
                            constraints: const BoxConstraints(maxWidth: 446),
                            child: Container(
                              padding: EdgeInsets.all(
                                box.maxWidth > 600 ? 36 : 24,
                              ),
                              decoration: BoxDecoration(
                                color: AccountColors.paper,
                                borderRadius: BorderRadius.circular(16),
                                border: Border.all(color: AccountColors.border),
                              ),
                              child: Form(
                                key: _form,
                                child: Column(
                                  crossAxisAlignment:
                                      CrossAxisAlignment.stretch,
                                  mainAxisSize: MainAxisSize.min,
                                  children: [
                                    Align(
                                      alignment: Alignment.centerLeft,
                                      child: Container(
                                        padding: const EdgeInsets.all(14),
                                        decoration: BoxDecoration(
                                          color: AccountColors.tint,
                                          borderRadius: BorderRadius.circular(
                                            12,
                                          ),
                                        ),
                                        child: const Icon(
                                          Icons.lock_outline,
                                          color: AccountColors.action,
                                          size: 26,
                                        ),
                                      ),
                                    ),
                                    const SizedBox(height: 14),
                                    Text(
                                      _register
                                          ? 'Crie sua conta de teste'
                                          : 'Entre na sua conta',
                                      style: const TextStyle(
                                        fontWeight: FontWeight.bold,
                                        fontSize: 30,
                                      ),
                                    ),
                                    const SizedBox(height: 4),
                                    const Text(
                                      'Continue seu campeonato em qualquer dispositivo.',
                                      style: TextStyle(
                                        color: AccountColors.muted,
                                        fontSize: 17,
                                      ),
                                    ),
                                    if (widget.notice != null)
                                      Padding(
                                        padding: const EdgeInsets.only(top: 12),
                                        child: Semantics(
                                          liveRegion: true,
                                          child: Text(widget.notice!),
                                        ),
                                      ),
                                    const SizedBox(height: 22),
                                    if (_register) ...[
                                      TextFormField(
                                        key: const Key('account-display-name'),
                                        controller: _name,
                                        enabled: !_busy,
                                        maxLength: 60,
                                        textInputAction: TextInputAction.next,
                                        decoration: const InputDecoration(
                                          labelText: 'Nome do treinador',
                                          counterText: '',
                                        ),
                                        validator: (v) =>
                                            (v?.trim().length ?? 0) < 2
                                            ? 'Informe seu nome.'
                                            : null,
                                      ),
                                      const SizedBox(height: 18),
                                    ],
                                    TextFormField(
                                      key: const Key('account-username'),
                                      controller: _username,
                                      enabled: !_busy,
                                      maxLength: 80,
                                      autofillHints: const [
                                        AutofillHints.username,
                                      ],
                                      keyboardType: TextInputType.emailAddress,
                                      textInputAction: TextInputAction.next,
                                      decoration: const InputDecoration(
                                        labelText: 'Usuário ou e-mail',
                                        hintText:
                                            'Digite seu usuário ou e-mail',
                                        counterText: '',
                                      ),
                                      validator: (v) =>
                                          (v?.trim().length ?? 0) < 3
                                          ? 'Informe seu usuário ou e-mail.'
                                          : null,
                                    ),
                                    const SizedBox(height: 20),
                                    TextFormField(
                                      key: const Key('account-password'),
                                      controller: _password,
                                      enabled: !_busy,
                                      maxLength: 128,
                                      obscureText: _obscure,
                                      autocorrect: false,
                                      enableSuggestions: false,
                                      autofillHints: [
                                        _register
                                            ? AutofillHints.newPassword
                                            : AutofillHints.password,
                                      ],
                                      textInputAction: TextInputAction.done,
                                      onFieldSubmitted: (_) => _submit(),
                                      decoration: InputDecoration(
                                        labelText: 'Senha',
                                        hintText: _register
                                            ? 'Pelo menos 10 caracteres'
                                            : 'Digite sua senha',
                                        counterText: '',
                                        suffixIcon: IconButton(
                                          tooltip: _obscure
                                              ? 'Mostrar senha'
                                              : 'Ocultar senha',
                                          onPressed: () => setState(
                                            () => _obscure = !_obscure,
                                          ),
                                          icon: Icon(
                                            _obscure
                                                ? Icons.visibility_outlined
                                                : Icons.visibility_off_outlined,
                                          ),
                                        ),
                                      ),
                                      validator: (v) =>
                                          (v?.length ?? 0) <
                                              (_register ? 10 : 1)
                                          ? (_register
                                                ? 'Use pelo menos 10 caracteres.'
                                                : 'Informe sua senha.')
                                          : null,
                                    ),
                                    if (_error != null)
                                      Padding(
                                        padding: const EdgeInsets.only(top: 12),
                                        child: Semantics(
                                          liveRegion: true,
                                          child: Text(
                                            _error!,
                                            key: const Key(
                                              'account-login-error',
                                            ),
                                            style: const TextStyle(
                                              color: Color(0xFF9B2020),
                                            ),
                                          ),
                                        ),
                                      ),
                                    const SizedBox(height: 20),
                                    ElevatedButton(
                                      key: const Key('account-submit'),
                                      onPressed: _busy ? null : _submit,
                                      child: AccountActionLabel(
                                        _busy
                                            ? 'AGUARDE...'
                                            : _register
                                            ? 'CRIAR CONTA DE TESTE'
                                            : 'ENTRAR',
                                        arrow: !_busy && !_register,
                                      ),
                                    ),
                                    const SizedBox(height: 10),
                                    TextButton(
                                      onPressed: _busy
                                          ? null
                                          : () => setState(() {
                                              _register = !_register;
                                              _error = null;
                                              _password.clear();
                                            }),
                                      child: Text(
                                        _register
                                            ? 'Já tenho conta — Entrar'
                                            : 'Criar conta de teste',
                                      ),
                                    ),
                                  ],
                                ),
                              ),
                            ),
                          ),
                        ),
                      ),
                      const Padding(
                        padding: EdgeInsets.all(16),
                        child: Text(
                          'Acesso local de teste • Sem pagamentos ativos',
                          textAlign: TextAlign.center,
                          style: TextStyle(
                            color: Color(0xFFCCE0D6),
                            fontSize: 13,
                          ),
                        ),
                      ),
                    ],
                  ),
                ),
              ),
            ),
          ),
        ),
      ),
    ),
  );
}

class _AccountBackdrop extends CustomPainter {
  @override
  void paint(Canvas canvas, Size size) {
    canvas.drawRect(
      Offset.zero & size,
      Paint()..color = AccountColors.background,
    );
    final band = Paint()
      ..color = const Color(0xFF184D3B).withValues(alpha: .4)
      ..style = PaintingStyle.stroke
      ..strokeWidth = size.height * .12;
    for (var i = 0; i < 3; i++) {
      canvas.drawOval(
        Rect.fromLTWH(
          -size.width * .5,
          -size.height * .8 + i * size.height * .22,
          size.width * 2.1,
          size.height * 1.4,
        ),
        band,
      );
    }
    final field = Paint()
      ..shader =
          const RadialGradient(
            colors: [Color(0xFF10663F), Color(0x00002F23)],
          ).createShader(
            Rect.fromLTWH(0, size.height * .6, size.width, size.height * .6),
          );
    canvas.drawRect(Offset.zero & size, field);
  }

  @override
  bool shouldRepaint(_AccountBackdrop oldDelegate) => false;
}

class AccountPortal extends StatefulWidget {
  final Map<String, dynamic> account;
  final List<dynamic> worlds;
  final bool lobby, busy;
  final String? notice;
  final Widget appearancePicker;
  final VoidCallback home,
      openLobby,
      create,
      refresh,
      joinByCode,
      logout,
      profile;
  final void Function(Map<String, dynamic>) resume, enter;
  const AccountPortal({
    super.key,
    required this.account,
    required this.worlds,
    required this.lobby,
    required this.busy,
    required this.appearancePicker,
    required this.home,
    required this.openLobby,
    required this.create,
    required this.refresh,
    required this.joinByCode,
    required this.logout,
    required this.profile,
    required this.resume,
    required this.enter,
    this.notice,
  });
  @override
  State<AccountPortal> createState() => _AccountPortalState();
}

class _AccountPortalState extends State<AccountPortal> {
  String _filter = 'all', _search = '';
  @override
  Widget build(BuildContext context) => Theme(
    data: AccountColors.theme(),
    child: Builder(
      builder: (context) => LayoutBuilder(
        builder: (context, box) {
          final mobile = box.maxWidth < 1000;
          return Scaffold(
            appBar: AppBar(
              backgroundColor: AccountColors.dark,
              toolbarHeight: mobile ? 68 : 86,
              automaticallyImplyLeading: false,
              title: AccountBrand(compact: mobile),
              actions: [
                PopupMenuButton<String>(
                  tooltip: 'Menu da conta',
                  onSelected: (v) {
                    if (v == 'logout') {
                      widget.logout();
                    } else {
                      widget.profile();
                    }
                  },
                  itemBuilder: (_) => [
                    const PopupMenuItem(
                      value: 'profile',
                      child: Text('Perfil e vínculos'),
                    ),
                    const PopupMenuItem(value: 'logout', child: Text('Sair')),
                  ],
                  child: Padding(
                    padding: const EdgeInsets.symmetric(horizontal: 16),
                    child: Row(
                      children: [
                        const Icon(
                          Icons.account_circle_outlined,
                          color: Colors.white,
                        ),
                        const SizedBox(width: 8),
                        ConstrainedBox(
                          constraints: BoxConstraints(
                            maxWidth: mobile ? 65 : 170,
                          ),
                          child: Text(
                            '${widget.account['displayName']}',
                            maxLines: 1,
                            overflow: TextOverflow.ellipsis,
                            style: const TextStyle(
                              color: Colors.white,
                              fontWeight: FontWeight.bold,
                            ),
                          ),
                        ),
                        const Icon(Icons.expand_more, color: Colors.white),
                      ],
                    ),
                  ),
                ),
              ],
            ),
            body: Row(
              crossAxisAlignment: CrossAxisAlignment.stretch,
              children: [
                if (!mobile)
                  Container(
                    width: 235,
                    color: AccountColors.background,
                    padding: const EdgeInsets.all(14),
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.stretch,
                      children: [
                        _nav(
                          'Início',
                          Icons.home_outlined,
                          !widget.lobby,
                          widget.home,
                        ),
                        _nav(
                          'Criar campeonato',
                          Icons.emoji_events_outlined,
                          false,
                          widget.create,
                        ),
                        _nav(
                          'Entrar no lobby',
                          Icons.groups_outlined,
                          widget.lobby,
                          widget.openLobby,
                        ),
                        const ListTile(
                          leading: Icon(
                            Icons.workspace_premium_outlined,
                            color: Colors.white,
                          ),
                          title: Text(
                            'Pagamento',
                            style: TextStyle(color: Colors.white),
                          ),
                          subtitle: Text(
                            'EM BREVE',
                            style: TextStyle(color: AccountColors.yellow),
                          ),
                        ),
                        const Spacer(),
                        const Padding(
                          padding: EdgeInsets.all(8),
                          child: Text(
                            'Uma conta. Seus campeonatos e seus treinadores, em qualquer dispositivo.',
                            style: TextStyle(
                              color: Color(0xFFCCE0D6),
                              fontSize: 14,
                            ),
                          ),
                        ),
                      ],
                    ),
                  ),
                Expanded(
                  child: SingleChildScrollView(
                    child: Padding(
                      padding: EdgeInsets.all(mobile ? 16 : 36),
                      child: Align(
                        alignment: Alignment.topLeft,
                        child: ConstrainedBox(
                          constraints: const BoxConstraints(maxWidth: 1440),
                          child: Column(
                            crossAxisAlignment: CrossAxisAlignment.stretch,
                            children: [
                              if (widget.notice != null)
                                Padding(
                                  padding: const EdgeInsets.only(bottom: 16),
                                  child: Semantics(
                                    liveRegion: true,
                                    child: Text(
                                      widget.notice!,
                                      key: const Key('account-notice'),
                                    ),
                                  ),
                                ),
                              if (widget.busy) const LinearProgressIndicator(),
                              Text(
                                widget.lobby
                                    ? 'MULTIPLAYER'
                                    : 'BEM-VINDO À LIGA CLÁSSICA 98',
                                style: const TextStyle(
                                  color: AccountColors.action,
                                  fontWeight: FontWeight.bold,
                                  letterSpacing: 1.1,
                                  fontSize: 13,
                                ),
                              ),
                              const SizedBox(height: 3),
                              Row(
                                children: [
                                  Expanded(
                                    child: Text(
                                      widget.lobby
                                          ? 'Lobby de campeonatos'
                                          : 'Olá, ${widget.account['displayName']}!',
                                      style: const TextStyle(
                                        fontWeight: FontWeight.bold,
                                        fontSize: 30,
                                      ),
                                    ),
                                  ),
                                  if (widget.lobby)
                                    IconButton(
                                      tooltip: 'Atualizar salas',
                                      onPressed: widget.busy
                                          ? null
                                          : widget.refresh,
                                      icon: const Icon(Icons.refresh),
                                    ),
                                ],
                              ),
                              Text(
                                widget.lobby
                                    ? 'Escolha uma sala. Seus campeonatos ficam em destaque.'
                                    : 'Escolha como quer jogar. Suas salas e seus clubes continuam vinculados à sua conta.',
                                style: const TextStyle(
                                  color: AccountColors.muted,
                                  fontSize: 17,
                                ),
                              ),
                              const SizedBox(height: 22),
                              if (widget.lobby) ...[
                                Wrap(
                                  spacing: 8,
                                  runSpacing: 8,
                                  children: [
                                    for (final option in [
                                      ('all', 'TODAS AS SALAS'),
                                      ('mine', 'MINHAS SALAS'),
                                      ('vacancies', 'COM VAGAS'),
                                    ])
                                      OutlinedButton(
                                        onPressed: () =>
                                            setState(() => _filter = option.$1),
                                        style: OutlinedButton.styleFrom(
                                          backgroundColor: _filter == option.$1
                                              ? AccountColors.dark
                                              : Colors.white,
                                          foregroundColor: _filter == option.$1
                                              ? Colors.white
                                              : AccountColors.dark,
                                        ),
                                        child: AccountActionLabel(
                                          option.$2,
                                          star: option.$1 == 'mine',
                                        ),
                                      ),
                                    OutlinedButton(
                                      onPressed: widget.busy
                                          ? null
                                          : widget.joinByCode,
                                      child: const Text('ENTRAR POR CÓDIGO'),
                                    ),
                                  ],
                                ),
                                const SizedBox(height: 14),
                                TextField(
                                  key: const Key('account-room-search'),
                                  decoration: const InputDecoration(
                                    labelText: 'Buscar campeonato',
                                    prefixIcon: Icon(Icons.search),
                                  ),
                                  onChanged: (s) => setState(() => _search = s),
                                ),
                                const SizedBox(height: 16),
                                widget.appearancePicker,
                                const SizedBox(height: 16),
                                ..._rooms(mobile, mineOnly: false),
                                const SizedBox(height: 12),
                                _info(
                                  'Retomar uma vaga existente não ocupa outra vaga. Novos treinadores seguem as regras da sala.',
                                ),
                              ] else ...[
                                if (mobile)
                                  ..._actions(true).expand(
                                    (card) => [
                                      card,
                                      const SizedBox(height: 12),
                                    ],
                                  )
                                else
                                  Row(
                                    crossAxisAlignment:
                                        CrossAxisAlignment.start,
                                    children: [
                                      for (final (i, card) in _actions(
                                        false,
                                      ).indexed) ...[
                                        if (i > 0) const SizedBox(width: 16),
                                        Expanded(child: card),
                                      ],
                                    ],
                                  ),
                                const SizedBox(height: 24),
                                Wrap(
                                  alignment: WrapAlignment.spaceBetween,
                                  crossAxisAlignment: WrapCrossAlignment.center,
                                  children: [
                                    const AccountActionLabel(
                                      'Meus campeonatos',
                                      star: true,
                                      style: TextStyle(
                                        fontWeight: FontWeight.bold,
                                        fontSize: 23,
                                      ),
                                    ),
                                    TextButton(
                                      onPressed: widget.openLobby,
                                      child: const AccountActionLabel(
                                        'Ver todas as salas',
                                        arrow: true,
                                      ),
                                    ),
                                  ],
                                ),
                                ..._rooms(mobile, mineOnly: true),
                              ],
                            ],
                          ),
                        ),
                      ),
                    ),
                  ),
                ),
              ],
            ),
            bottomNavigationBar: mobile
                ? BottomNavigationBar(
                    backgroundColor: AccountColors.dark,
                    selectedItemColor: AccountColors.yellow,
                    unselectedItemColor: Colors.white,
                    type: BottomNavigationBarType.fixed,
                    currentIndex: widget.lobby ? 2 : 0,
                    onTap: (i) {
                      switch (i) {
                        case 0:
                          widget.home();
                        case 1:
                          widget.create();
                        case 2:
                          widget.openLobby();
                        case 3:
                          widget.profile();
                      }
                    },
                    items: const [
                      BottomNavigationBarItem(
                        icon: Icon(Icons.home_outlined),
                        label: 'Início',
                      ),
                      BottomNavigationBarItem(
                        icon: Icon(Icons.emoji_events_outlined),
                        label: 'Criar',
                      ),
                      BottomNavigationBarItem(
                        icon: Icon(Icons.groups_outlined),
                        label: 'Lobby',
                      ),
                      BottomNavigationBarItem(
                        icon: Icon(Icons.menu),
                        label: 'Mais',
                      ),
                    ],
                  )
                : null,
          );
        },
      ),
    ),
  );

  Widget _nav(
    String title,
    IconData icon,
    bool selected,
    VoidCallback action,
  ) => Padding(
    padding: const EdgeInsets.only(bottom: 10),
    child: Material(
      color: selected ? AccountColors.yellow : Colors.transparent,
      borderRadius: BorderRadius.circular(10),
      child: ListTile(
        onTap: action,
        leading: Icon(icon, color: selected ? AccountColors.ink : Colors.white),
        title: Text(
          title,
          style: TextStyle(
            color: selected ? AccountColors.ink : Colors.white,
            fontWeight: FontWeight.bold,
          ),
        ),
      ),
    ),
  );
  Widget _info(String text) => Container(
    padding: const EdgeInsets.all(14),
    decoration: BoxDecoration(
      color: const Color(0xFFFFF8E2),
      borderRadius: BorderRadius.circular(8),
      border: Border.all(color: const Color(0xFFEAD99F)),
    ),
    child: Text(text, style: const TextStyle(color: Color(0xFF705A19))),
  );
  List<Widget> _actions(bool mobile) => [
    _action(
      mobile,
      'Criar campeonato',
      'Abra o criador de mundos atual, escolha os países e as regras.',
      Icons.emoji_events_outlined,
      'CRIAR CAMPEONATO',
      widget.create,
    ),
    _action(
      mobile,
      'Entrar no lobby',
      'Veja as salas existentes e retome seu time ou entre como treinador.',
      Icons.groups_outlined,
      'VER SALAS',
      widget.openLobby,
    ),
    _action(
      mobile,
      'Pagamento (sem anúncios)',
      'Futuro plano sem anúncios. Nenhuma cobrança nesta etapa.',
      Icons.workspace_premium_outlined,
      'EM BREVE',
      null,
    ),
  ];
  Widget _action(
    bool mobile,
    String title,
    String description,
    IconData icon,
    String label,
    VoidCallback? action,
  ) {
    if (mobile) {
      return Material(
        color: action == null
            ? const Color(0xFFFFFAE9)
            : const Color(0xFFFAFFFC),
        shape: RoundedRectangleBorder(
          borderRadius: BorderRadius.circular(16),
          side: BorderSide(
            color: action == null
                ? const Color(0xFFEAD99F)
                : AccountColors.border,
          ),
        ),
        child: Semantics(
          button: action != null,
          child: InkWell(
            key: ValueKey('account-action-$label'),
            borderRadius: BorderRadius.circular(16),
            onTap: widget.busy ? null : action,
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Row(
                children: [
                  Container(
                    padding: const EdgeInsets.all(10),
                    decoration: BoxDecoration(
                      color: action == null
                          ? const Color(0xFFFFEBAB)
                          : AccountColors.tint,
                      borderRadius: BorderRadius.circular(12),
                    ),
                    child: Icon(icon, color: AccountColors.dark, size: 26),
                  ),
                  const SizedBox(width: 12),
                  Expanded(
                    child: Column(
                      crossAxisAlignment: CrossAxisAlignment.start,
                      children: [
                        Text(
                          action == null ? 'Pagamento' : title,
                          style: const TextStyle(
                            fontSize: 19,
                            fontWeight: FontWeight.bold,
                          ),
                        ),
                        if (action == null)
                          const Padding(
                            padding: EdgeInsets.only(top: 3),
                            child: Text(
                              'EM BREVE',
                              style: TextStyle(
                                fontSize: 11,
                                color: Color(0xFF705A19),
                                fontWeight: FontWeight.bold,
                              ),
                            ),
                          ),
                        const SizedBox(height: 5),
                        Text(
                          description,
                          style: const TextStyle(
                            fontSize: 14,
                            height: 1.3,
                            color: AccountColors.muted,
                          ),
                        ),
                      ],
                    ),
                  ),
                  if (action != null)
                    const Icon(
                      Icons.chevron_right,
                      color: AccountColors.action,
                    ),
                ],
              ),
            ),
          ),
        ),
      );
    }
    return Container(
      constraints: mobile ? null : const BoxConstraints(minHeight: 250),
      padding: const EdgeInsets.all(22),
      decoration: BoxDecoration(
        color: action == null
            ? const Color(0xFFFFFAE9)
            : const Color(0xFFFAFFFC),
        borderRadius: BorderRadius.circular(16),
        border: Border.all(
          color: action == null
              ? const Color(0xFFEAD99F)
              : AccountColors.border,
        ),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.stretch,
        children: [
          Row(
            children: [
              Container(
                padding: const EdgeInsets.all(10),
                decoration: BoxDecoration(
                  color: action == null
                      ? const Color(0xFFFFEBAB)
                      : AccountColors.tint,
                  borderRadius: BorderRadius.circular(12),
                ),
                child: Icon(icon, color: AccountColors.dark, size: 28),
              ),
              if (mobile) ...[
                const SizedBox(width: 12),
                Expanded(
                  child: Text(
                    title,
                    style: const TextStyle(
                      fontSize: 22,
                      fontWeight: FontWeight.bold,
                    ),
                  ),
                ),
              ],
            ],
          ),
          if (!mobile) ...[
            const SizedBox(height: 12),
            Text(
              title,
              style: const TextStyle(fontSize: 23, fontWeight: FontWeight.bold),
            ),
          ],
          const SizedBox(height: 10),
          Text(
            description,
            style: const TextStyle(color: AccountColors.muted, fontSize: 16),
          ),
          const SizedBox(height: 18),
          if (action != null)
            ElevatedButton(
              onPressed: widget.busy ? null : action,
              child: AccountActionLabel(label, arrow: true),
            )
          else
            const SizedBox(
              height: 48,
              child: Center(
                child: Text(
                  'EM BREVE',
                  style: TextStyle(
                    fontWeight: FontWeight.bold,
                    color: Color(0xFF776323),
                  ),
                ),
              ),
            ),
        ],
      ),
    );
  }

  List<Widget> _rooms(bool mobile, {required bool mineOnly}) {
    final rooms =
        widget.worlds
            .whereType<Map>()
            .map((r) => Map<String, dynamic>.from(r))
            .where((r) {
              final mine = r['accountOwnsSeat'] == true;
              final full =
                  (r['currentHumans'] as num? ?? 0) >=
                  (r['maxHumans'] as num? ?? 8);
              return (!mineOnly || mine) &&
                  (mineOnly || _filter != 'mine' || mine) &&
                  (mineOnly || _filter != 'vacancies' || !full) &&
                  (mineOnly ||
                      '${r['name']} ${r['inviteCode']}'.toLowerCase().contains(
                        _search.toLowerCase(),
                      ));
            })
            .toList()
          ..sort(
            (a, b) => (b['accountOwnsSeat'] == true ? 1 : 0).compareTo(
              a['accountOwnsSeat'] == true ? 1 : 0,
            ),
          );
    if (rooms.isEmpty) {
      return [
        Padding(
          padding: const EdgeInsets.symmetric(vertical: 24),
          child: Text(
            mineOnly
                ? 'Você ainda não participa de um campeonato. Crie um ou entre no lobby.'
                : 'Nenhuma sala encontrada para esse filtro.',
          ),
        ),
      ];
    }
    return rooms.map((room) {
      final mine = room['accountOwnsSeat'] == true;
      final full =
          (room['currentHumans'] as num? ?? 0) >=
          (room['maxHumans'] as num? ?? 8);
      final people = (room['readyCounter']?['participants'] as List? ?? [])
          .whereType<Map>()
          .toList();
      final summary = Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Wrap(
            spacing: 10,
            runSpacing: 5,
            crossAxisAlignment: WrapCrossAlignment.center,
            children: [
              Text(
                '${room['name']}',
                style: const TextStyle(
                  fontWeight: FontWeight.bold,
                  fontSize: 21,
                ),
              ),
              if (mine)
                const AccountActionLabel(
                  'VOCÊ É TREINADOR',
                  star: true,
                  style: TextStyle(
                    color: AccountColors.action,
                    fontWeight: FontWeight.bold,
                    fontSize: 13,
                  ),
                ),
            ],
          ),
          const SizedBox(height: 5),
          Text(
            'Temporada ${room['seasonNumber']} • ${room['totalClubs']} clubes • Rodada ${room['roundNumber']}${mine ? ' • Seu clube: ${room['myClubName'] ?? 'Sem clube'}' : ''}',
            style: const TextStyle(color: AccountColors.muted),
          ),
          if (widget.lobby) ...[
            const SizedBox(height: 5),
            Text(
              'Código: ${room['inviteCode']}',
              style: const TextStyle(color: AccountColors.muted),
            ),
          ],
        ],
      );
      final button = ElevatedButton(
        onPressed: widget.busy || (!mine && full)
            ? null
            : () => mine ? widget.resume(room) : widget.enter(room),
        child: AccountActionLabel(
          mine
              ? 'RETOMAR MINHA SESSÃO'
              : full
              ? 'SALA LOTADA'
              : 'ENTRAR',
          arrow: !mine && !full,
        ),
      );
      return Container(
        key: ValueKey('account-room-${room['id']}'),
        margin: const EdgeInsets.only(bottom: 12),
        padding: const EdgeInsets.all(16),
        decoration: BoxDecoration(
          color: mine ? const Color(0xFFE5F4E9) : const Color(0xFFFAFFFC),
          borderRadius: BorderRadius.circular(14),
          border: Border.all(
            color: mine ? AccountColors.action : AccountColors.border,
            width: mine ? 1.5 : 1,
          ),
        ),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            if (mobile) ...[
              summary,
              const SizedBox(height: 12),
              button,
            ] else
              Row(
                children: [
                  const Icon(
                    Icons.emoji_events_outlined,
                    color: AccountColors.action,
                    size: 32,
                  ),
                  const SizedBox(width: 16),
                  Expanded(child: summary),
                  const SizedBox(width: 16),
                  button,
                ],
              ),
            if (widget.lobby) ...[
              const SizedBox(height: 10),
              Wrap(
                spacing: 16,
                runSpacing: 5,
                children: [
                  Text(
                    'CONECTADOS: ${room['connectedHumans'] ?? '—'}/${room['maxHumans']}',
                  ),
                  Text(
                    'VAGAS OCUPADAS: ${room['currentHumans']}/${room['maxHumans']}',
                  ),
                  Text(
                    'TÁTICAS ENVIADAS: ${room['readyCounter']?['readyCount'] ?? 0}/${room['readyCounter']?['totalHumans'] ?? room['currentHumans']}',
                  ),
                  Text('ESTADO: ${room['roundState']}'),
                ],
              ),
              if (people.isNotEmpty)
                ExpansionTile(
                  tilePadding: EdgeInsets.zero,
                  title: const Text('TREINADORES DA SALA'),
                  children: [
                    for (final person in people)
                      ListTile(
                        dense: true,
                        title: Text('${person['nickname']}'),
                        subtitle: Text(
                          '${person['clubName'] ?? 'Sem clube'} • ${person['isConnected'] == true ? 'CONECTADO' : 'DESCONECTADO'} • ${person['isReady'] == true ? 'TÁTICA ENVIADA' : 'TÁTICA PENDENTE'}',
                        ),
                      ),
                  ],
                ),
            ],
          ],
        ),
      );
    }).toList();
  }
}
