import 'package:flutter/material.dart';

class LogService {
  static final ValueNotifier<List<String>> logs = ValueNotifier<List<String>>([]);

  static void info(String message) {
    _addLog("INFO", message);
  }

  static void error(String message) {
    _addLog("ERROR", message);
  }

  static void _addLog(String level, String message) {
    final timestamp = DateTime.now().toString().substring(11, 19);
    logs.value = [...logs.value, "[$timestamp] [$level] $message"];
  }
}