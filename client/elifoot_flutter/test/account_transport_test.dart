import 'dart:async';
import 'dart:io';
import 'package:flutter_test/flutter_test.dart';
import 'package:elifoot_flutter/account_transport_io.dart';

void main() {
  test(
    'Native HTTP and WebSocket share cookies in memory and respect expiry, path and HTTPS',
    () async {
      final server = await HttpServer.bind(InternetAddress.loopbackIPv4, 0);
      final seen = <String>[];
      final accepted = Completer<String>();
      final subscription = server.listen((request) async {
        final path = request.uri.path;
        final cookies = request.headers.value(HttpHeaders.cookieHeader) ?? '';
        if (path == '/login') {
          request.response.cookies.addAll([
            Cookie('session', 'opaque-native-session')
              ..httpOnly = true
              ..path = '/',
            Cookie('secure-only', 'hidden')
              ..secure = true
              ..path = '/',
            Cookie('expired', 'no')
              ..expires = DateTime.utc(2000)
              ..path = '/',
            Cookie('limited', 'private')..path = '/private',
          ]);
        } else if (path == '/logout') {
          request.response.cookies.add(
            Cookie('session', '')
              ..maxAge = 0
              ..path = '/',
          );
        } else if (WebSocketTransformer.isUpgradeRequest(request)) {
          final socket = await WebSocketTransformer.upgrade(request);
          accepted.complete(cookies);
          await socket.close();
          return;
        } else {
          seen.add(cookies);
        }
        await request.response.close();
      });
      final client = createAccountTransport();
      try {
        final base = 'http://127.0.0.1:${server.port}';
        await client.get(Uri.parse('$base/login'));
        await client.get(Uri.parse('$base/public'));
        expect(seen.last, 'session=opaque-native-session');
        await client.get(Uri.parse('$base/private/detail'));
        expect(seen.last, contains('limited=private'));
        final channel = createAccountSocket(
          Uri.parse('ws://127.0.0.1:${server.port}/hubs/gameroom'),
          client,
        );
        await channel.ready;
        expect(
          await accepted.future.timeout(const Duration(seconds: 5)),
          'session=opaque-native-session',
        );
        await channel.sink.close();
        await client.get(Uri.parse('$base/logout'));
        await client.get(Uri.parse('$base/public'));
        expect(seen.last, isEmpty);
      } finally {
        client.close();
        await subscription.cancel();
        await server.close(force: true);
      }
    },
  );
}
