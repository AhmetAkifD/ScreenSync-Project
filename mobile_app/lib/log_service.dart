import 'package:flutter/material.dart';

class LogService {
  static final ValueNotifier<List<String>> logs = ValueNotifier<List<String>>([]);

  // UI tarafından nesne üretilmeden erişilebilmesi için static yaptık
  static Color getLogColor(String message) {
    final msg = message.toUpperCase();

    // Hem senin Türkçe etiketlerin hem de sistemin ERROR etiketi
    if (msg.contains('HATA') || msg.contains('CRITICAL') || msg.contains('ERROR')) {
      return Colors.redAccent;
    }
    if (msg.contains('ROTATION')) {
      return Colors.orangeAccent;
    }
    if (msg.contains('TCP')) {
      return Colors.lightBlueAccent;
    }
    if (msg.contains('VİDEO') || msg.contains('STREAM')) {
      return Colors.deepPurpleAccent;
    }
    if (msg.contains('KOMUT') || msg.contains('SİSTEM')) {
      return Colors.tealAccent;
    }

    return Colors.white70; // Varsayılan renk
  }

  static void info(String message) {
    _addLog("INFO", message);
  }

  static void error(String message) {
    _addLog("ERROR", message);
  }

  static void _addLog(String level, String message) {
    final timestamp = DateTime.now().toString().substring(11, 19);
    // Yeni logu listenin en sonuna ekler
    logs.value = [...logs.value, "[$timestamp] [$level] $message"];
  }
}