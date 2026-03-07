import 'package:flutter/material.dart';
import 'package:flutter/services.dart';

void main() => runApp(const ScreenSyncApp());

class ScreenSyncApp extends StatelessWidget {
  const ScreenSyncApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      home: Scaffold(
        appBar: AppBar(title: const Text('ScreenSync Mobile')),
        body: Center(
          child: ElevatedButton(
            onPressed: _startScreenCapture,
            child: const Text('Ekran Paylaşımını Başlat'),
          ),
        ),
      ),
    );
  }

  // Native tarafa komut gönderen köprü (MethodChannel)
  static const platform = MethodChannel('com.akif.screensync/stream');

  Future<void> _startScreenCapture() async {
    try {
      await platform.invokeMethod('startCapture');
    } on PlatformException catch (e) {
      print("Hata: ${e.message}");
    }
  }
}