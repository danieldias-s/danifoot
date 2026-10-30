import 'package:shared_preferences/shared_preferences.dart';

SharedPreferencesWithCache? _preferences;
const _sessionKey = 'elifoot-session';

Future<void> initializeSessionStore() async {
  _preferences = await SharedPreferencesWithCache.create(
    cacheOptions: const SharedPreferencesWithCacheOptions(
      allowList: {_sessionKey},
    ),
  );
}

String? readSession() => _preferences?.getString(_sessionKey);
Future<void> writeSession(String value) async {
  await _preferences?.setString(_sessionKey, value);
}
