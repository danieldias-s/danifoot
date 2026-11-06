import 'package:flutter/foundation.dart';

/// One API origin shared by REST and SignalR. Overrides are build-time settings.
class ApiEnvironment {
  static const _localWebHosts = {'localhost', '127.0.0.1'};

  static String get baseUrl => resolve(
    configured: const String.fromEnvironment('API_BASE_URL'),
    isWeb: kIsWeb,
    isRelease: kReleaseMode,
    platform: defaultTargetPlatform,
    webOrigin: kIsWeb ? Uri.base : null,
  );

  static String resolve({
    required String configured,
    required bool isWeb,
    required bool isRelease,
    required TargetPlatform platform,
    Uri? webOrigin,
  }) {
    if (configured.trim().isNotEmpty) {
      final uri = Uri.tryParse(configured.trim());
      if (uri == null ||
          !uri.hasAuthority ||
          uri.host.isEmpty ||
          (uri.scheme != 'http' && uri.scheme != 'https') ||
          uri.userInfo.isNotEmpty ||
          uri.hasQuery ||
          uri.hasFragment) {
        throw ArgumentError('API_BASE_URL deve ser uma URL HTTP(S) válida.');
      }
      if (!isWeb && isRelease && uri.scheme != 'https') {
        throw StateError('O Android release exige API_BASE_URL com HTTPS.');
      }
      // Local cookie authentication needs the page and API on the same site.
      final usePageHost =
          isWeb &&
          uri.scheme == 'http' &&
          webOrigin?.scheme == 'http' &&
          _localWebHosts.contains(uri.host) &&
          _localWebHosts.contains(webOrigin?.host);
      final apiUrl = usePageHost
          ? uri.replace(host: webOrigin!.host).toString()
          : configured.trim();
      return apiUrl.replaceAll(RegExp(r'/+$'), '');
    }
    if (!isWeb && isRelease) {
      throw StateError('Defina API_BASE_URL com HTTPS para a versão release.');
    }
    if (isWeb) {
      return Uri(
        scheme: webOrigin?.scheme ?? 'http',
        host: webOrigin?.host ?? '127.0.0.1',
        port: 5080,
      ).toString();
    }
    return platform == TargetPlatform.android
        ? 'http://10.0.2.2:5080'
        : 'http://127.0.0.1:5080';
  }
}
