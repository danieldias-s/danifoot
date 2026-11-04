import 'dart:convert';
import 'package:http/http.dart' as http;

/// Every game API shares the same cookie transport and antiforgery protection.
/// Web session secrets remain in HttpOnly cookies; native cookies stay in memory.
class AccountHttpClient extends http.BaseClient {
  final http.Client inner;
  bool enabled = false;
  void Function()? onExpired;
  AccountHttpClient(this.inner);
  @override
  Future<http.StreamedResponse> send(http.BaseRequest request) async {
    try {
      return await _send(request);
    } on http.ClientException {
      if (enabled) {
        throw Exception(
          'Conexão indisponível. Confira o servidor de teste e tente novamente.',
        );
      }
      rethrow;
    }
  }

  Future<http.StreamedResponse> _send(http.BaseRequest request) async {
    if (enabled) {
      request.headers.remove('Authorization');
      request.headers.remove('authorization');
      if (!['GET', 'HEAD', 'OPTIONS'].contains(request.method)) {
        final uri = request.url.replace(path: '/api/auth/csrf', query: '');
        final response = await inner.get(uri);
        if (response.statusCode != 200) {
          throw Exception(
            'Não foi possível preparar o acesso. Confira a conexão.',
          );
        }
        request.headers['X-CSRF-TOKEN'] =
            jsonDecode(response.body)['csrfToken'] as String;
      }
    }
    final response = await inner.send(request);
    if (enabled &&
        response.statusCode == 401 &&
        !request.url.path.startsWith('/api/auth/')) {
      onExpired?.call();
    }
    return response;
  }

  @override
  void close() => inner.close();
}
