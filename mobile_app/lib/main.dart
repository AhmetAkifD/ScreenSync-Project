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
        colorSchemeSeed: Colors.deepPurple,
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
  static const eventChannel = EventChannel('com.akif.screensync/events');

  String _statusText = "Sistem Hazır";
  Color _statusColor = Colors.grey;

  bool _isStreaming = false;
  bool _isDiscovering = false;
  bool _isConnected = false;

  // YENİ: Hedef IP'yi dinamik tutuyoruz. Varsayılan Wi-Fi Direct IP'si.
  String _targetIp = "192.168.137.1";

  List<Map<String, String>> _foundDevices = [];

  @override
  void initState() {
    super.initState();
    _listenToNativeEvents();
  }

  void _listenToNativeEvents() {
    eventChannel.receiveBroadcastStream().listen((dynamic event) {
      final Map<dynamic, dynamic> data = event;
      final String type = data['type'];

      setState(() {
        if (type == 'device_found') {
          bool exists = _foundDevices.any((d) => d['deviceAddress'] == data['deviceAddress']);
          if (!exists) {
            _foundDevices.add({
              'deviceName': data['deviceName'] ?? 'Bilinmeyen PC',
              'deviceAddress': data['deviceAddress'] ?? '',
            });
          }
          if (!_isConnected && _statusText != "Bağlanılıyor...") {
            _statusText = "${_foundDevices.length} PC Bulundu";
            _statusColor = Colors.orangeAccent;
          }
        }
        else if (type == 'connected') {
          _isConnected = true;
          _isDiscovering = false;
          _targetIp = "192.168.137.1"; // Wi-Fi Direct IP'si
          _statusText = "PC'ye Bağlanıldı (Wi-Fi)";
          _statusColor = Colors.green;
          _foundDevices.clear();
        }
        else if (type == 'stream_started') {
          _isStreaming = true;
          _statusText = "Ekran Paylaşılıyor!";
          _statusColor = Colors.deepPurpleAccent;
        }
        else if (type == 'stream_rejected') {
          ScaffoldMessenger.of(context).showSnackBar(
            const SnackBar(content: Text('Ekran paylaşım izni reddedildi.')),
          );
        }
        else if (type == 'stream_stopped') {
          _isStreaming = false;
          _statusText = "Yayın Durduruldu";
          _statusColor = Colors.green;
        }
        else if (type == 'disconnected') {
          _isConnected = false;
          _isStreaming = false;
          _isDiscovering = false;
          _statusText = "Bağlantı Koptu";
          _statusColor = Colors.redAccent;
          _foundDevices.clear();
        }
      });
    }, onError: (dynamic error) {
      print('EventChannel Hatası: $error');
    });
  }

  Future<void> _toggleDiscovery() async {
    if (_isDiscovering) {
      await platform.invokeMethod('stopDiscovery');
      setState(() {
        _isDiscovering = false;
        if (!_isConnected) {
          _statusText = "Arama Durduruldu";
          _statusColor = Colors.grey;
        }
      });
    } else {
      Map<Permission, PermissionStatus> statuses = await [
        Permission.nearbyWifiDevices,
        Permission.location,
      ].request();

      if (statuses[Permission.nearbyWifiDevices] == PermissionStatus.granted &&
          statuses[Permission.location] == PermissionStatus.granted) {
        setState(() {
          _isDiscovering = true;
          _foundDevices.clear();
          _statusText = "Ağ Taranıyor...";
          _statusColor = Colors.orange;
        });
        await platform.invokeMethod('startDiscovery');
      } else {
        ScaffoldMessenger.of(context).showSnackBar(
          const SnackBar(content: Text('Konum ve Yakın Cihaz izni gerekli!')),
        );
      }
    }
  }

  Future<void> _connectToPC(String deviceAddress) async {
    try {
      setState(() {
        _statusText = "Bağlanılıyor...";
        _statusColor = Colors.blueAccent;
        _isDiscovering = false;
      });

      await platform.invokeMethod('connect', {'address': deviceAddress});
    } on PlatformException catch (e) {
      print("Bağlantı hatası: ${e.message}");
      setState(() {
        _statusText = "Bağlantı Başarısız";
        _statusColor = Colors.redAccent;
      });
    }
  }

  // YENİ: USB Moduna Geçiş Fonksiyonu
  void _enableUsbMode() {
    setState(() {
      _isConnected = true; // Yayına izin vermek için sanal bağlantı
      _targetIp = "127.0.0.1"; // ADB Reverse IP'si
      _isDiscovering = false;
      _foundDevices.clear();
      _statusText = "USB Modu Aktif";
      _statusColor = Colors.teal;
    });
    ScaffoldMessenger.of(context).showSnackBar(
      const SnackBar(content: Text('USB Kablosu ile yayına hazır!')),
    );
  }

  Future<void> _toggleScreenCapture() async {
    if (!_isConnected) {
      ScaffoldMessenger.of(context).showSnackBar(
        const SnackBar(content: Text('Önce bir PC\'ye bağlanmalısın veya USB Modunu açmalısın!')),
      );
      return;
    }

    try {
      if (_isStreaming) {
        await platform.invokeMethod('stopCapture');
      } else {
        // YENİ: Hangi IP'ye yayın yapılacağını Kotlin'e gönderiyoruz
        await platform.invokeMethod('startCapture', {'ip': _targetIp});
      }
    } on PlatformException catch (e) {
      print("Yayın Hatası: ${e.message}");
    }
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: const Text('ScreenSync', style: TextStyle(fontWeight: FontWeight.bold)),
        centerTitle: true,
      ),
      body: Padding(
        padding: const EdgeInsets.all(20.0),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.stretch,
          children: [
            Card(
              elevation: 4,
              shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(16)),
              child: Padding(
                padding: const EdgeInsets.symmetric(vertical: 20),
                child: Column(
                  children: [
                    Icon(
                      _isStreaming ? Icons.cast_connected : (_isConnected ? Icons.link : Icons.cast),
                      size: 64,
                      color: _statusColor,
                    ),
                    const SizedBox(height: 16),
                    Text(
                      _statusText,
                      style: TextStyle(fontSize: 20, fontWeight: FontWeight.bold, color: _statusColor),
                    ),
                  ],
                ),
              ),
            ),

            const SizedBox(height: 16),

            if (_foundDevices.isNotEmpty && !_isConnected)
              Expanded(
                child: ListView.builder(
                  itemCount: _foundDevices.length,
                  itemBuilder: (context, index) {
                    final device = _foundDevices[index];
                    return Card(
                      color: Colors.deepPurple.withOpacity(0.2),
                      child: ListTile(
                        leading: const Icon(Icons.computer, color: Colors.white),
                        title: Text(device['deviceName']!),
                        subtitle: Text(device['deviceAddress']!),
                        trailing: ElevatedButton(
                          onPressed: () => _connectToPC(device['deviceAddress']!),
                          child: const Text('Bağlan'),
                        ),
                      ),
                    );
                  },
                ),
              )
            else
              const Spacer(),

            // YENİ: USB MODU BUTONU
            if (!_isConnected) ...[
              OutlinedButton.icon(
                onPressed: _enableUsbMode,
                icon: const Icon(Icons.usb),
                label: const Text('USB (Kablolu) Moduna Geç'),
                style: OutlinedButton.styleFrom(
                  padding: const EdgeInsets.symmetric(vertical: 16),
                  side: const BorderSide(color: Colors.teal),
                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                ),
              ),
              const SizedBox(height: 12),

              ElevatedButton.icon(
                onPressed: _toggleDiscovery,
                icon: Icon(_isDiscovering ? Icons.stop : Icons.wifi_find),
                label: Text(_isDiscovering ? 'Aramayı Durdur' : 'PC Ara (Wi-Fi Direct)'),
                style: ElevatedButton.styleFrom(
                  foregroundColor: Colors.white,
                  backgroundColor: _isDiscovering ? Colors.orange : Colors.deepPurple,
                  padding: const EdgeInsets.symmetric(vertical: 16),
                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                ),
              ),
            ],

            const SizedBox(height: 16),

            ElevatedButton.icon(
              onPressed: _toggleScreenCapture,
              icon: Icon(_isStreaming ? Icons.stop_screen_share : Icons.screen_share),
              label: Text(_isStreaming ? 'Yayını Durdur' : 'Ekran Paylaşımını Başlat', style: const TextStyle(fontSize: 16)),
              style: ElevatedButton.styleFrom(
                foregroundColor: Colors.white,
                backgroundColor: _isConnected ? (_isStreaming ? Colors.redAccent : Colors.green) : Colors.grey,
                padding: const EdgeInsets.symmetric(vertical: 20),
                shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
              ),
            ),
          ],
        ),
      ),
    );
  }
}