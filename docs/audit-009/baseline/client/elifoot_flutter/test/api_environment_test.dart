import 'package:elifoot_flutter/api_environment.dart';
import 'package:flutter/foundation.dart';
import 'package:flutter_test/flutter_test.dart';

void main() {
  String resolve({String url = '', bool web = false, bool release = false}) =>
      ApiEnvironment.resolve(
        configured: url,
        isWeb: web,
        isRelease: release,
        platform: TargetPlatform.android,
        webOrigin: Uri.parse('http://192.168.1.40:8083/'),
      );

  test('Android DEV reaches the host through the emulator gateway', () {
    expect(resolve(), 'http://10.0.2.2:5080');
  });

  test('An explicit API origin supports USB reverse and LAN devices', () {
    expect(resolve(url: 'http://127.0.0.1:5080/'), 'http://127.0.0.1:5080');
    expect(
      resolve(url: 'http://192.168.1.40:5080'),
      'http://192.168.1.40:5080',
    );
  });

  test('Web DEV uses its host and the API port', () {
    expect(resolve(web: true), 'http://192.168.1.40:5080');
  });

  test('Native release requires an explicit HTTPS origin', () {
    expect(() => resolve(release: true), throwsStateError);
    expect(
      () => resolve(release: true, url: 'http://10.0.2.2:5080'),
      throwsStateError,
    );
    expect(
      resolve(release: true, url: 'https://api.example.test/'),
      'https://api.example.test',
    );
  });

  test('Invalid origins and embedded credentials are rejected', () {
    for (final url in [
      'localhost:5080',
      'ws://host:5080',
      'http://:5080',
      'https://user:secret@host',
      'https://host?key=value',
      'https://host#fragment',
    ]) {
      expect(() => resolve(url: url), throwsArgumentError, reason: url);
    }
  });
}
