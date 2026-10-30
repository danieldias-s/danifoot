import 'dart:async';
import 'package:flutter/material.dart';
import 'package:flutter/scheduler.dart';

// Independent control: no Elifoot, storage, HTTP, SignalR or game engine.
void main() {
  WidgetsFlutterBinding.ensureInitialized();
  var frames = 0;
  var slow = 0;
  SchedulerBinding.instance.addTimingsCallback((timings) {
    frames += timings.length;
    slow += timings.where((t) => t.totalSpan.inMilliseconds > 100).length;
  });
  Timer.periodic(const Duration(seconds: 10), (_) {
    // ignore: avoid_print
    print('QA004_REFERENCE frames=$frames over100ms=$slow');
  });
  runApp(const MaterialApp(home: ReferenceScreen()));
}

class ReferenceScreen extends StatefulWidget {
  const ReferenceScreen({super.key});
  @override
  State<ReferenceScreen> createState() => _ReferenceState();
}

class _ReferenceState extends State<ReferenceScreen>
    with SingleTickerProviderStateMixin {
  late final AnimationController animation;
  int taps = 0;
  @override
  void initState() {
    super.initState();
    animation = AnimationController(
      vsync: this,
      duration: const Duration(seconds: 2),
    )..repeat();
  }

  @override
  void dispose() {
    animation.dispose();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) => Scaffold(
    appBar: AppBar(title: const Text('QA004 Flutter Reference')),
    body: Center(
      child: Column(
        mainAxisSize: MainAxisSize.min,
        children: [
          const Text('Independent Flutter control'),
          RotationTransition(
            turns: animation,
            child: const Icon(Icons.sync, size: 64),
          ),
          Text('Taps: $taps'),
          ElevatedButton(
            onPressed: () => setState(() => taps++),
            child: const Text('INCREMENT'),
          ),
        ],
      ),
    ),
  );
}
