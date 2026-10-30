import 'dart:io';
import 'dart:convert';
import 'package:integration_test/integration_test_driver.dart';

Future<void> main() async {
  final evidence = Directory('../../docs/android-runtime-003');
  await evidence.create(recursive: true);
  await integrationDriver(
    timeout: const Duration(minutes: 45),
    responseDataCallback: (data) async {
      await File(
        '${evidence.path}/integration-response.json',
      ).writeAsString(jsonEncode(data));
      final adb = '../../.tools/android-sdk/platform-tools/adb.exe';
      final serial =
          Platform.environment['QA_ANDROID_SERIAL'] ?? 'emulator-5554';
      const remote = '/data/local/tmp/qa003-profile-results.png';
      for (final args in [
        ['-s', serial, 'shell', '-x', 'screencap', '-p', remote],
        [
          '-s',
          serial,
          'pull',
          remote,
          '${evidence.path}/native-profile-results.png',
        ],
      ]) {
        final capture = await Process.run(
          adb,
          args,
        ).timeout(const Duration(seconds: 60));
        if (capture.exitCode != 0) {
          throw StateError('Falha na captura Android real: ${capture.stderr}');
        }
      }
    },
  );
}
