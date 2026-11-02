import 'dart:async';
import 'dart:convert';

/// Per-tab identity wins; persistent profiles allow returning after a tab/browser closes.
/// Independent tabs may explicitly choose different trainers on the same browser.
class SessionStoragePolicy {
  static const sessionKey = 'elifoot-session';
  static const profilesKey = 'elifoot-local-profiles';
  final String? Function(String) tabRead, persistentRead;
  final FutureOr<void> Function(String, String) tabWrite, persistentWrite;
  const SessionStoragePolicy({
    required this.tabRead,
    required this.tabWrite,
    required this.persistentRead,
    required this.persistentWrite,
  });
  String? read() => tabRead(sessionKey) ?? persistentRead(sessionKey);
  List<Map<String, dynamic>> profiles() {
    try {
      return (jsonDecode(persistentRead(profilesKey) ?? '[]') as List)
          .whereType<Map>()
          .map((p) => Map<String, dynamic>.from(p))
          .where(
            (p) =>
                p['deviceId'] is String && (p['deviceId'] as String).isNotEmpty,
          )
          .toList();
    } catch (_) {
      return [];
    }
  }

  Future<void> write(String value) async {
    await tabWrite(sessionKey, value);
    await persistentWrite(sessionKey, value);
    final profile = Map<String, dynamic>.from(jsonDecode(value) as Map);
    if (profile['deviceId'] is! String ||
        (profile['deviceId'] as String).isEmpty) {
      return;
    }
    final saved = profiles()
      ..removeWhere((p) => p['deviceId'] == profile['deviceId']);
    saved.add(profile);
    await persistentWrite(profilesKey, jsonEncode(saved));
  }
}
