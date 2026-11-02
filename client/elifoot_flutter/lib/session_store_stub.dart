import 'package:shared_preferences/shared_preferences.dart';
import 'session_storage_policy.dart';

SharedPreferencesWithCache? _preferences;
final _storage = SessionStoragePolicy(
  tabRead: (_) => null,
  tabWrite: (_, _) {},
  persistentRead: (key) => _preferences?.getString(key),
  persistentWrite: (key, value) async {
    await _preferences?.setString(key, value);
  },
);
Future<void> initializeSessionStore() async {
  _preferences = await SharedPreferencesWithCache.create(
    cacheOptions: const SharedPreferencesWithCacheOptions(
      allowList: {
        SessionStoragePolicy.sessionKey,
        SessionStoragePolicy.profilesKey,
      },
    ),
  );
}

String? readSession() => _storage.read();
List<Map<String, dynamic>> readSavedSessions() => _storage.profiles();
Future<void> writeSession(String value) => _storage.write(value);
