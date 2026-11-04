import 'dart:io';
import 'package:http/http.dart' as http;
import 'package:web_socket_channel/web_socket_channel.dart';
import 'package:web_socket_channel/io.dart';

/// Native clients keep cookies in memory; reopening may require signing in.
http.Client createAccountTransport() => _CookieClient();

WebSocketChannel createAccountSocket(Uri uri, http.Client transport) {
  final httpUri = uri.replace(scheme: uri.scheme == 'wss' ? 'https' : 'http');
  final cookies = transport is _CookieClient
      ? transport.cookieHeader(httpUri)
      : '';
  return IOWebSocketChannel.connect(
    uri,
    headers: cookies.isEmpty ? null : {'Cookie': cookies},
  );
}

class _CookieClient extends http.BaseClient {
  final _inner = http.Client();
  final _cookies = <String, Map<String, Cookie>>{};
  String cookieHeader(Uri uri) {
    final jar = _cookies[uri.origin];
    if (jar == null) return '';
    jar.removeWhere(
      (_, c) => c.expires?.isBefore(DateTime.now()) == true || c.maxAge == 0,
    );
    return jar.values
        .where(
          (c) =>
              (!c.secure || uri.scheme == 'https') &&
              uri.path.startsWith(c.path ?? '/'),
        )
        .map((c) => '${c.name}=${c.value}')
        .join('; ');
  }

  @override
  Future<http.StreamedResponse> send(http.BaseRequest request) async {
    final origin = request.url.origin;
    final jar = _cookies.putIfAbsent(origin, () => {});
    final cookies = cookieHeader(request.url);
    if (cookies.isNotEmpty) request.headers['Cookie'] = cookies;
    final response = await _inner.send(request);
    final raw = response.headers['set-cookie'];
    if (raw != null) {
      for (final value in raw.split(RegExp(r',(?=\s*[^;,=]+=[^;,]*)'))) {
        final cookie = Cookie.fromSetCookieValue(value.trim());
        jar[cookie.name] = cookie;
      }
    }
    return response;
  }

  @override
  void close() {
    _cookies.clear();
    _inner.close();
  }
}
