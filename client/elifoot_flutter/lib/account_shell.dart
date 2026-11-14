// ignore_for_file: invalid_use_of_protected_member
part of 'main.dart';

extension _AccountShell on _ElifootShellScreenState {
  void _acceptAccount(Map<String, dynamic> account) {
    _account = account;
    _coachId = account['coachId']?.toString();
    _nickname = account['displayName']?.toString() ?? 'Treinador';
    _deviceId = 'account';
    _authenticatedDeviceId = 'account';
    _authWarning = null;
  }

  Future<void> _restoreAccountSession() async {
    try {
      final account = await widget.apiClient.accountSession();
      if (!mounted || account == null) return;
      setState(() => _acceptAccount(account));
      await widget.presentation?.activate(widget.apiClient, _coachId!);
      final worlds = await widget.apiClient.listWorlds();
      if (!mounted) return;
      setState(() => _worlds = worlds);
      final preferences = await SharedPreferences.getInstance();
      final raw = preferences.getString(
        'lc98.account-navigation.${account['accountId']}',
      );
      Map? saved;
      try {
        if (raw != null) saved = jsonDecode(raw) as Map;
      } catch (_) {}
      final room = worlds
          .whereType<Map>()
          .where(
            (w) => w['id'] == saved?['worldId'] && w['accountOwnsSeat'] == true,
          )
          .firstOrNull;
      if (room != null) {
        if (saved?['liveSelection'] is Map) {
          _liveSelection.restore(
            Map<String, dynamic>.from(saved!['liveSelection']),
          );
        }
        await _resumeAccountRoom(
          Map<String, dynamic>.from(room),
          preferredCoach: saved?['coachId']?.toString(),
        );
      }
    } catch (_) {
      if (mounted) {
        setState(
          () => _accountNotice =
              'Conexão indisponível. Confira se o servidor de teste está aberto e tente novamente.',
        );
      }
    } finally {
      if (mounted) setState(() => _restoringSession = false);
    }
  }

  Future<void> _authenticateAccount(
    String username,
    String password,
    String? displayName,
  ) async {
    final account = await widget.apiClient.signInAccount(
      username,
      password,
      displayName: displayName,
    );
    if (!mounted) return;
    setState(() {
      _acceptAccount(account);
      _accountLobby = false;
      _accountNotice = null;
      _statusBanner = null;
    });
    await widget.presentation?.activate(widget.apiClient, _coachId!);
    await _reloadAccountWorlds();
  }

  Future<void> _reloadAccountWorlds() async {
    if (_account == null) return;
    setState(() => _isLoading = true);
    try {
      final rooms = await widget.apiClient.listWorlds();
      if (mounted) {
        setState(() {
          _worlds = rooms;
          _statusBanner = null;
        });
      }
    } catch (_) {
      if (mounted) {
        setState(
          () => _statusBanner =
              'Não foi possível carregar os campeonatos. Confira a conexão e atualize.',
        );
      }
    } finally {
      if (mounted) setState(() => _isLoading = false);
    }
  }

  void _expireAccountSession() {
    if (!mounted || _account == null) return;
    widget.apiClient.disconnectRealtime();
    setState(() {
      _account = null;
      _coachId = null;
      _worldId = null;
      _snapshot = null;
      _myClubId = null;
      _myClubDetail = null;
      _worlds = [];
      _accountNotice = 'Sua sessão expirou. Entre novamente para continuar.';
    });
  }

  Future<void> _logoutAccount() async {
    try {
      await _saveAccountNavigation(clearWorld: true);
      await widget.apiClient.signOutAccount();
      if (!mounted) return;
      setState(() {
        _account = null;
        _coachId = null;
        _worldId = null;
        _snapshot = null;
        _myClubId = null;
        _myClubDetail = null;
        _worlds = [];
        _accountNotice = null;
        _statusBanner = null;
      });
    } catch (_) {
      if (mounted) {
        setState(
          () => _statusBanner =
              'Não foi possível sair. Confira a conexão e tente novamente.',
        );
      }
    }
  }

  Future<void> _saveAccountNavigation({bool clearWorld = false}) async {
    final account = _account;
    if (account == null) return;
    final preferences = await SharedPreferences.getInstance();
    // Navigation hints only: the server revalidates every profile and room after reload.
    await preferences.setString(
      'lc98.account-navigation.${account['accountId']}',
      jsonEncode({
        'worldId': clearWorld ? null : _worldId,
        'coachId': _coachId,
        'liveSelection': _liveSelection.toJson(),
      }),
    );
  }

  Future<void> _resumeAccountRoom(
    Map<String, dynamic> room, {
    String? preferredCoach,
  }) async {
    final candidates = (room['ownedCoachIds'] as List? ?? [])
        .map((x) => x.toString())
        .toList();
    if (room['accountOwnsSeat'] != true || candidates.isEmpty) return;
    var coach = candidates.contains(preferredCoach)
        ? preferredCoach
        : candidates.contains(_coachId)
        ? _coachId
        : null;
    if (coach == null && candidates.length == 1) coach = candidates.single;
    if (coach == null) {
      final people = room['readyCounter']?['participants'] as List? ?? [];
      coach = await showDialog<String>(
        context: context,
        builder: (ctx) => SimpleDialog(
          title: const Text('Escolha seu treinador nesta sala'),
          children: [
            for (final id in candidates)
              SimpleDialogOption(
                onPressed: () => Navigator.pop(ctx, id),
                child: Text(
                  '${people.whereType<Map>().where((p) => p['coachId'] == id).firstOrNull?['nickname'] ?? 'Treinador'}',
                ),
              ),
          ],
        ),
      );
    }
    if (coach == null || !mounted) return;
    try {
      final account = await widget.apiClient.selectAccountProfile(coach);
      if (!mounted) return;
      setState(() => _acceptAccount(account));
      await widget.presentation?.activate(widget.apiClient, coach);
      await _joinSelectedWorld('${room['id']}');
    } catch (e) {
      if (mounted) {
        setState(
          () => _statusBanner =
              'Não foi possível retomar sua sessão: ${e.toString().replaceFirst('Exception: ', '')}',
        );
      }
    }
  }

  Future<void> _enterAccountRoom(Map<String, dynamic> room) async {
    if (room['accountOwnsSeat'] == true) {
      await _resumeAccountRoom(room);
      return;
    }
    if (room['hasInitialLobby'] == true) {
      await _joinSelectedWorld('${room['id']}');
      return;
    }
    final choice = await showDialog<String>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Como deseja entrar?'),
        content: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            if (widget.presentation != null)
              PresentationVersionSelector(
                api: widget.apiClient,
                controller: widget.presentation!,
              ),
            const SizedBox(height: 12),
            ListTile(
              leading: const Icon(Icons.shield_outlined),
              title: const Text('ASSUMIR CLUBE'),
              onTap: () => Navigator.pop(ctx, 'club'),
            ),
            ListTile(
              leading: const Icon(Icons.person_outline),
              title: const Text('COMEÇAR DESEMPREGADO'),
              onTap: () => Navigator.pop(ctx, 'unemployed'),
            ),
          ],
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx),
            child: const Text('CANCELAR'),
          ),
        ],
      ),
    );
    if (choice == 'club') await _chooseClub('${room['id']}');
    if (choice == 'unemployed') {
      await _joinSelectedWorld('${room['id']}', joinUnemployed: true);
    }
  }

  Future<void> _accountJoinCode() async {
    final controller = TextEditingController();
    final code = await showDialog<String>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('ENTRAR POR CÓDIGO'),
        content: TextField(
          controller: controller,
          autofocus: true,
          decoration: const InputDecoration(labelText: 'Código da sala'),
          onSubmitted: (s) => Navigator.pop(ctx, s.trim()),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx),
            child: const Text('CANCELAR'),
          ),
          ElevatedButton(
            onPressed: () => Navigator.pop(ctx, controller.text.trim()),
            child: const Text('CONTINUAR'),
          ),
        ],
      ),
    );
    await Future<void>.delayed(const Duration(milliseconds: 300));
    controller.dispose();
    if (code != null && code.isNotEmpty && mounted) {
      try {
        final preview = await widget.apiClient.previewAccountWorldInvite(code);
        final choice = !mounted
            ? null
            : preview['hasInitialLobby'] == true
            ? 'lobby'
            : await showDialog<String>(
                context: context,
                builder: (ctx) => SimpleDialog(
                  title: const Text('Como deseja entrar?'),
                  children: [
                    SimpleDialogOption(
                      onPressed: () => Navigator.pop(ctx, 'club'),
                      child: const Text('ASSUMIR CLUBE'),
                    ),
                    SimpleDialogOption(
                      onPressed: () => Navigator.pop(ctx, 'unemployed'),
                      child: const Text('COMEÇAR DESEMPREGADO'),
                    ),
                  ],
                ),
              );
        if (choice != null && mounted) {
          final club = choice == 'club'
              ? await showDialog<String>(
                  context: context,
                  builder: (_) => ClubSelectionDialog(
                    api: widget.apiClient,
                    presentation: widget.presentation,
                    clubs: (preview['clubs'] as List)
                        .map((item) => Map<String, dynamic>.from(item as Map))
                        .toList(),
                  ),
                )
              : null;
          if (!mounted || (choice == 'club' && club == null)) return;
          final joined = await widget.apiClient.joinAccountWorldByCode(
            code,
            joinUnemployed: choice == 'unemployed',
            preferredClubId: club,
          );
          if (mounted) {
            await _joinSelectedWorld(
              joined['worldId'].toString(),
              joined: joined,
            );
          }
        }
      } catch (e) {
        if (mounted) setState(() => _statusBanner = e.toString());
      }
    }
  }

  Future<void> _accountProfile() async {
    final account = _account;
    if (account == null) return;
    final code = TextEditingController();
    final result = await showDialog<String>(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text('Perfil e vínculos'),
        content: SingleChildScrollView(
          child: Column(
            mainAxisSize: MainAxisSize.min,
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text('${account['displayName']}'),
              Text('Usuário: ${account['username']}'),
              const SizedBox(height: 10),
              const Text(
                'Campeonatos antigos precisam de um vínculo autorizado. Não usamos o nome nem um ID de dispositivo para tomar uma vaga.',
              ),
              const SizedBox(height: 16),
              TextField(
                controller: code,
                maxLength: 64,
                decoration: const InputDecoration(
                  labelText: 'Código de vínculo autorizado',
                  helperText: 'Use somente o código emitido para sua conta.',
                ),
              ),
            ],
          ),
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx),
            child: const Text('FECHAR'),
          ),
          OutlinedButton(
            onPressed: () => Navigator.pop(ctx, 'logout'),
            child: const Text('SAIR DA CONTA'),
          ),
          ElevatedButton(
            onPressed: () => Navigator.pop(ctx, code.text.trim()),
            child: const Text('VINCULAR'),
          ),
        ],
      ),
    );
    if (result == 'logout') {
      await _logoutAccount();
    } else if (result != null && result.isNotEmpty) {
      try {
        final updated = await widget.apiClient.linkLegacyAccount(result);
        if (mounted) {
          setState(() {
            _account = updated;
            _statusBanner =
                'Treinador vinculado. Sua vaga antiga foi preservada.';
          });
        }
        await _reloadAccountWorlds();
      } catch (e) {
        if (mounted) {
          setState(
            () => _statusBanner = e.toString().replaceFirst('Exception: ', ''),
          );
        }
      }
    }
    await Future<void>.delayed(const Duration(milliseconds: 300));
    code.dispose();
  }

  Widget _buildAccountPortal() => AccountPortal(
    account: _account!,
    worlds: _worlds,
    lobby: _accountLobby,
    busy: _isLoading,
    notice: _statusBanner,
    appearancePicker: widget.presentation == null
        ? const SizedBox.shrink()
        : ListenableBuilder(
            listenable: widget.presentation!,
            builder: (context, _) => ExpansionTile(
              tilePadding: EdgeInsets.zero,
              title: Text(
                'Aparência: ${widget.presentation!.version.label}',
                style: const TextStyle(fontWeight: FontWeight.bold),
              ),
              children: [
                PresentationVersionSelector(
                  api: widget.apiClient,
                  controller: widget.presentation!,
                ),
              ],
            ),
          ),
    home: () => setState(() => _accountLobby = false),
    openLobby: () {
      setState(() => _accountLobby = true);
      _reloadAccountWorlds();
    },
    create: _openCatalogCreator,
    refresh: _reloadAccountWorlds,
    joinByCode: _accountJoinCode,
    logout: _logoutAccount,
    profile: _accountProfile,
    resume: _resumeAccountRoom,
    enter: _enterAccountRoom,
  );
}
