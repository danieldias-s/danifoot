import 'package:flutter/material.dart';
import 'round_navigation.dart';

/// A single persistent shell handles URL, history, back and internal navigation.
/// Only a full, version-checked server snapshot may change the live-match lock.
class MatchNavigationController extends ChangeNotifier {
  static const paths = [
    '/plantel',
    '/divisoes',
    '/mercado',
    '/financas',
    '/carreira',
    '/calendario',
    '/rankings',
  ];
  String path = '/plantel';
  bool locked = false;
  void acceptStage(String? stage) {
    final next = RoundViewCoordinator.isLiveMatch(stage);
    final changed = locked != next;
    locked = next;
    if (locked && path != '/partida') {
      path = '/partida';
      notifyListeners();
    } else if (changed) {
      notifyListeners();
    }
  }

  void navigate(String next) {
    path = locked
        ? '/partida'
        : (paths.contains(next) || next == '/partida' ? next : '/plantel');
    notifyListeners();
  }
}

class MatchRouteParser extends RouteInformationParser<String> {
  const MatchRouteParser();
  @override
  Future<String> parseRouteInformation(
    RouteInformation routeInformation,
  ) async => routeInformation.uri.path;
  @override
  RouteInformation restoreRouteInformation(String configuration) =>
      RouteInformation(uri: Uri.parse(configuration));
}

class MatchRouterDelegate extends RouterDelegate<String>
    with ChangeNotifier, PopNavigatorRouterDelegateMixin<String> {
  final MatchNavigationController navigation;
  final Widget shell;
  @override
  final GlobalKey<NavigatorState> navigatorKey = GlobalKey<NavigatorState>();
  MatchRouterDelegate(this.navigation, this.shell) {
    navigation.addListener(notifyListeners);
  }
  @override
  String get currentConfiguration => navigation.path;
  @override
  Future<void> setNewRoutePath(String configuration) async {
    navigation.navigate(configuration);
  }

  @override
  Future<bool> popRoute() async {
    if (navigation.locked) {
      navigation.navigate('/partida');
      return true;
    }
    return super.popRoute();
  }

  @override
  Widget build(BuildContext context) => Navigator(
    key: navigatorKey,
    pages: [
      MaterialPage(
        key: const ValueKey('authoritative-game-shell'),
        child: shell,
      ),
    ],
    onDidRemovePage: (_) {},
  );
  @override
  void dispose() {
    navigation.removeListener(notifyListeners);
    super.dispose();
  }
}
