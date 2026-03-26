import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:permission_handler/permission_handler.dart';

void main() {
  runApp(const ScreenSyncApp());
}

class ScreenSyncApp extends StatelessWidget {
  const ScreenSyncApp({super.key});

  @override
  Widget build(BuildContext context) {
    return MaterialApp(
      title: 'ScreenSync Mobile',
      debugShowCheckedModeBanner: false,
      theme: ThemeData(
        brightness: Brightness.dark,
        useMaterial3: true,
        colorSchemeSeed: Colors.deepPurple, // Uygulamanın ana vurgu rengi
        fontFamily: 'Roboto', // Veya projende olan özel bir font
      ),
      home: const HomeScreen(),
    );
  }
}

class HomeScreen extends StatefulWidget {
  const HomeScreen({super.key});

  @override
  State<HomeScreen> createState() => _HomeScreenState();
}

class _HomeScreenState extends State<HomeScreen> {
  static const platform = MethodChannel('com.akif.screensync/stream');

  // Arayüzdeki durum yazısını ve renkleri değiştirmek için stateler
  String _statusText = "Bağlantı Bekleniyor";
  Color _statusColor = Colors.grey;
  bool _isStreaming = false;

  Future<void> _startDiscovery() async {
    Map<Permission, PermissionStatus> statuses = await [
      Permission.nearbyWifiDevices,
      Permission.location,
    ].request();

    if (statuses[Permission.nearbyWifiDevices] == PermissionStatus.granted &&
        statuses[Permission.location] == PermissionStatus.granted) {
      try {
        setState(() {
          _statusText = "PC Aranıyor...";
          _statusColor = Colors.orangeAccent;
        });

        final String result = await platform.invokeMethod('startDiscovery');
        print(result);
      } on PlatformException catch (e) {
        print("Arama hatası: ${e.message}");
      }
    } else {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Gerekli izinler verilmedi!')),
      );
    }
  }

  Future<void> _connectToPC() async {
    try {
      setState(() {
        _statusText = "Bağlanılıyor...";
        _statusColor = Colors.blueAccent;
      });

      final String result = await platform.invokeMethod('connect');
      print(result);

      // Not: Aslında bu durumu Kotlin'den dinlemek en iyisi ama şimdilik manuel tetikliyoruz
      setState(() {
        _statusText = "PC'ye Bağlanıldı";
        _statusColor = Colors.greenAccent;
      });
    } on PlatformException catch (e) {
      print("Bağlantı hatası: ${e.message}");
      setState(() {
        _statusText = "Bağlantı Başarısız";
        _statusColor = Colors.redAccent;
      });
    }
  }

  Future<void> _startScreenCapture() async {
    try {
      await platform.invokeMethod('startCapture');
      setState(() {
        _isStreaming = true;
        _statusText = "Yayında!";
        _statusColor = Colors.green;
      });
    } on PlatformException catch (e) {
      print("Yayın Hatası: ${e.message}");
    }
  }

  Future<void> _sendTestMessage() async {
    try {
      await platform.invokeMethod('sendTest');
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Test mesajı fırlatıldı!')),
      );
    } catch (e) {
      print("Hata: $e");
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('ScreenSync', style: TextStyle(fontWeight: FontWeight.bold)),
        centerTitle: true,
        actions: [
          IconButton(
            icon: const Icon(Icons.settings),
            onPressed: () {
              // TODO: Ayarlar sayfasına yönlendirme yapılacak
              ScaffoldMessenger.of(context).showSnackBar(
                const SnackBar(content: Text('Ayarlar sayfası yakında eklenecek!')),
              );
            },
          )
        ],
      ),
      body: Padding(
        padding: const EdgeInsets.all(20.0),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            // DURUM KARTI
            Card(
              elevation: 4,
              shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
              child: Padding(
                padding: const EdgeInsets.symmetric(vertical: 30),
                child: Column(
                  children: [
                    Icon(
                      _isStreaming ? Icons.cast_connected : Icons.cast,
                      size: 64,
                      color: _statusColor,
                    ),
                    const SizedBox(height: 16),
                    Text(
                      _statusText,
                      style: TextStyle(
                        fontSize: 20,
                        fontWeight: FontWeight.bold,
                        color: _statusColor,
                      ),
                    ),
                  ],
                ),
              ),
            ),

            const Spacer(),

            // BAĞLANTI BUTONLARI
            Row(
              children: [
                Expanded(
                  child: ElevatedButton.icon(
                    onPressed: _startDiscovery,
                    icon: const Icon(Icons.wifi_find),
                    label: const Text('PC Ara'),
                    style: ElevatedButton.styleFrom(
                      padding: const EdgeInsets.symmetric(vertical: 16),
                      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                    ),
                  ),
                ),
                const SizedBox(width: 16),
                Expanded(
                  child: ElevatedButton.icon(
                    onPressed: _connectToPC,
                    icon: const Icon(Icons.link),
                    label: const Text('Bağlan'),
                    style: ElevatedButton.styleFrom(
                      padding: const EdgeInsets.symmetric(vertical: 16),
                      shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                    ),
                  ),
                ),
              ],
            ),

            const SizedBox(height: 16),

            // ANA YAYIN BUTONU
            ElevatedButton.icon(
              onPressed: _isStreaming ? null : _startScreenCapture,
              icon: Icon(_isStreaming ? Icons.stop_screen_share : Icons.screen_share),
              label: Text(_isStreaming ? 'Yayın Devam Ediyor...' : 'Ekran Paylaşımını Başlat', style: const TextStyle(fontSize: 16)),
              style: ElevatedButton.styleFrom(
                foregroundColor: Colors.white,
                backgroundColor: _isStreaming ? Colors.grey : Colors.deepPurpleAccent,
                padding: const EdgeInsets.symmetric(vertical: 20),
                shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
              ),
            ),

            const SizedBox(height: 32),

            // GİZLİ/KÜÇÜK TEST BUTONU
            TextButton(
              onPressed: _sendTestMessage,
              child: const Text('UDP Test Mesajı Gönder', style: TextStyle(color: Colors.grey)),
            ),
          ],
        ),
      ),
    );
  }
}