import 'dart:io';
import 'package:http/http.dart' as http;

/// Native clients keep cookies in memory; reopening may require signing in.
http.Client createAccountTransport() => _CookieClient();

class _CookieClient extends http.BaseClient {
  final _inner = http.Client();
  final _cookies = <String, Map<String, Cookie>>{};
  @override
  Future<http.StreamedResponse> send(http.BaseRequest request) async {
    final origin = request.url.origin;
    final jar = _cookies.putIfAbsent(origin, () => {});
    jar.removeWhere(
      (_, c) => c.expires?.isBefore(DateTime.now()) == true || c.maxAge == 0,
    );
    if (jar.isNotEmpty) {
      request.headers['Cookie'] = jar.values
          .where((c) => !c.secure || request.url.scheme == 'https')
          .map((c) => '${c.name}=${c.value}')
          .join('; ');
    }
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
