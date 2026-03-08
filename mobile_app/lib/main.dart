import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:permission_handler/permission_handler.dart';

void main() => runApp(const ScreenSyncApp());

class ScreenSyncApp extends StatelessWidget {
  const ScreenSyncApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      debugShowCheckedModeBanner: false,
      home: Scaffold(
        appBar: AppBar(title: const Text('ScreenSync Mobile')),
        body: Center(
          child: Column(
            children: [
              ElevatedButton(
                onPressed: _startScreenCapture,
                child: const Text('Ekran Paylaşımını Başlat'),
              ),
              ElevatedButton(
                onPressed: _startDiscovery,
                child: const Text('PC Ara (Wi-Fi Direct)'),
              ),
              ElevatedButton(
                onPressed: _connectToPC,
                child: const Text('Bulunan PC\'ye Bağlan'),
              ),
            ],
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
  Future<void> _startDiscovery() async {
    // Android 13+ için Yakındaki Cihazlar ve Konum iznini canlı olarak istiyoruz
    Map<Permission, PermissionStatus> statuses = await [
      Permission.nearbyWifiDevices,
      Permission.location,
    ].request();

    if (statuses[Permission.nearbyWifiDevices] == PermissionStatus.granted &&
        statuses[Permission.location] == PermissionStatus.granted) {
      try {
        final String result = await platform.invokeMethod('startDiscovery');
        print(result);
      } on PlatformException catch (e) {
        print("Arama hatası: ${e.message}");
      }
    } else {
      print("Gerekli izinler verilmediği için arama başlatılamıyor.");
    }
  }
  Future<void> _connectToPC() async {
    try {
      final String result = await platform.invokeMethod('connect');
      print(result);
    } on PlatformException catch (e) {
      print("Bağlantı hatası: ${e.message}");
    }
  }
}