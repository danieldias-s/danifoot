// QA-only entry point: renders the real app under phone-sized constraints.
// This validates responsive Web layout, not native Android execution.
import 'package:flutter/material.dart';
import 'package:flutter/semantics.dart';
import 'main.dart';

void main() {
  WidgetsFlutterBinding.ensureInitialized();
  SemanticsBinding.instance.ensureSemantics();
  runApp(const Directionality(textDirection: TextDirection.ltr, child: Center(
    child: SizedBox(width: 390, height: 844,
      child: MediaQuery(data: MediaQueryData(size: Size(390,844)), child: ElifootClassicApp())),
  )));
}
