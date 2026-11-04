import 'package:http/browser_client.dart';
import 'package:http/http.dart' as http;
import 'package:web_socket_channel/web_socket_channel.dart';

http.Client createAccountTransport() => BrowserClient()..withCredentials = true;

// The browser sends HttpOnly cookies on this WebSocket without exposing them.
WebSocketChannel createAccountSocket(Uri uri, http.Client transport) =>
    WebSocketChannel.connect(uri);
