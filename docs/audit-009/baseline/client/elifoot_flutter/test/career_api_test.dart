import 'dart:convert';
import 'package:flutter_test/flutter_test.dart';
import 'package:http/http.dart' as http;
import 'package:http/testing.dart';
import 'package:elifoot_flutter/api_client.dart';

void main() {
  test('Career rejection sends the invitation and coach identity to the reject route', () async {
    final requests = <http.Request>[];
    final api = ElifootApiClient(httpClient: MockClient((request) async {
      requests.add(request);
      return http.Response('{"success":true}', 200);
    }));
    await api.rejectInvitation('world', 'offer', 'coach');
    expect(requests.single.method, 'POST');
    expect(requests.single.url.path, '/api/worlds/world/invitations/offer/reject');
    expect(requests.single.url.queryParameters['coachId'], 'coach');
  });

  test('Career response preserves half-point ranking and pending invitations', () async {
    final api = ElifootApiClient(httpClient: MockClient((request) async => http.Response(jsonEncode({
      'morale': 50, 'moraleBand': 'AMARELA', 'invitations': [{'invitationId': 'pending'}],
      'ranking': [{'coachPoints': 12.5}],
    }), 200)));
    final data = await api.getInvitations('world', 'coach');
    expect(data['moraleBand'], 'AMARELA');
    expect(data['ranking'][0]['coachPoints'], 12.5);
    expect(data['invitations'][0]['invitationId'], 'pending');
  });
}
