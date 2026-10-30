// Per-tab storage keeps eight trainers independent and survives a page reload.
// ignore: deprecated_member_use, avoid_web_libraries_in_flutter
import 'dart:html' as html;

String? readSession() => html.window.sessionStorage['elifoot-session'];
Future<void> initializeSessionStore() async {}
Future<void> writeSession(String value) async {
  html.window.sessionStorage['elifoot-session'] = value;
}
