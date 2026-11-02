// ignore: deprecated_member_use, avoid_web_libraries_in_flutter
import 'dart:html' as html;
import 'session_storage_policy.dart';

final _storage = SessionStoragePolicy(
  tabRead: (key) => html.window.sessionStorage[key],
  tabWrite: (key, value) {
    html.window.sessionStorage[key] = value;
  },
  persistentRead: (key) => html.window.localStorage[key],
  persistentWrite: (key, value) {
    html.window.localStorage[key] = value;
  },
);
String? readSession() => _storage.read();
List<Map<String, dynamic>> readSavedSessions() => _storage.profiles();
Future<void> initializeSessionStore() async {}
Future<void> writeSession(String value) => _storage.write(value);
