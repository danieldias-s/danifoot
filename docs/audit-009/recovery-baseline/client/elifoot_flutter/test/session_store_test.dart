import 'package:elifoot_flutter/session_store_stub.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:shared_preferences_platform_interface/in_memory_shared_preferences_async.dart';
import 'package:shared_preferences_platform_interface/shared_preferences_async_platform_interface.dart';

void main() {
  test(
    'Native session reloads from preferences after cache initialization',
    () async {
      // This is a host unit test of the storage adapter, not Android runtime proof.
      final original = SharedPreferencesAsyncPlatform.instance;
      SharedPreferencesAsyncPlatform.instance =
          InMemorySharedPreferencesAsync.empty();
      addTearDown(() => SharedPreferencesAsyncPlatform.instance = original);
      await initializeSessionStore();
      expect(readSession(), isNull);
      const saved = '{"coachId":"qa003","deviceId":"android","worldId":"room"}';
      await writeSession(saved);
      await initializeSessionStore();
      expect(readSession(), saved);
    },
  );
}
