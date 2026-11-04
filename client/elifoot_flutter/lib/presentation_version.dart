import 'package:flutter/foundation.dart';
import 'api_client.dart';

/// Presentation only. Never sent to a world, match, career or draw command.
enum PresentationVersion {
  current('CURRENT', 'VERSÃO ATUAL'),
  classic('CLASSIC_98', 'VERSÃO CLÁSSICA (ANTIGA)');

  const PresentationVersion(this.code, this.label);
  final String code, label;
  static PresentationVersion parse(String code) =>
      code == classic.code ? classic : current;
}

/// Independent of club colors and of the selected world/club. A trainer without
/// a club can save this preference, and changing clubs cannot reset it.
class PresentationController extends ChangeNotifier {
  String? _owner;
  String? get owner => _owner;
  PresentationVersion _version = PresentationVersion.current;
  PresentationVersion get version => _version;
  bool loading = false, saving = false;
  bool get busy => loading || saving;
  String? error;
  int _generation = 0;
  bool _disposed = false, _loaded = false;

  void clear() {
    _generation++;
    _owner = null;
    _loaded = false;
    _version = PresentationVersion.current;
    loading = saving = false;
    error = null;
    if (!_disposed) notifyListeners();
  }

  Future<void> activate(ElifootApiClient api, String owner) async {
    if (_owner == owner && _loaded) return;
    final generation = ++_generation;
    if (_owner != owner) _version = PresentationVersion.current;
    _owner = owner;
    loading = true;
    saving = false;
    error = null;
    notifyListeners();
    try {
      final result = await api.profileAppearance(owner);
      if (_disposed || generation != _generation) return;
      _version = PresentationVersion.parse(result);
      _loaded = true;
    } catch (_) {
      if (!_disposed && generation == _generation) {
        error =
            'Não foi possível carregar a preferência visual. A versão atual continua disponível.';
      }
    } finally {
      if (!_disposed && generation == _generation) {
        loading = false;
        notifyListeners();
      }
    }
  }

  Future<bool> select(
    ElifootApiClient api,
    PresentationVersion value, {
    bool locked = false,
  }) async {
    final owner = _owner;
    if (_disposed || locked || busy || owner == null) return false;
    if (_loaded && value == _version) return true;
    final generation = _generation;
    saving = true;
    error = null;
    notifyListeners();
    try {
      final saved = await api.profileAppearance(owner, version: value.code);
      if (_disposed || generation != _generation) return false;
      _version = PresentationVersion.parse(saved);
      _loaded = true;
      return true;
    } catch (_) {
      if (!_disposed && generation == _generation) {
        error =
            'Não foi possível salvar a aparência. Sua seleção anterior foi mantida. Tente novamente.';
      }
      return false;
    } finally {
      if (!_disposed && generation == _generation) {
        saving = false;
        notifyListeners();
      }
    }
  }

  @override
  void dispose() {
    _disposed = true;
    _generation++;
    super.dispose();
  }
}
