import 'package:flutter/material.dart';
import 'package:flutter/services.dart';
import 'package:permission_handler/permission_handler.dart';
import 'log_service.dart'; // Demin kurduğumuz Log sistemini buraya dahil ediyoruz

class MainPageTools {
  // --- KANALLAR (CHANNELS) ---
  static const platform = MethodChannel('com.akif.screensync/stream');
  static const eventChannel = EventChannel('com.akif.screensync/events');

  // --- DURUM DEĞİŞKENLERİ (STATE) ---
  String statusText = "Sistem Hazır";
  Color statusColor = Colors.grey;
  bool isStreaming = false;
  bool isDiscovering = false;
  bool isConnected = false;
  bool isStreamLoading = false;
  String targetIp = "192.168.137.1";
  List<Map<String, String>> foundDevices = [];

  // Arayüzü (UI) güncellemek için kullanacağımız köprü fonksiyon
  final VoidCallback updateUI;

  // Sınıf oluşturulurken UI güncelleme fonksiyonunu mecbur kılıyoruz
  MainPageTools(this.updateUI);

  // --- 1. NATIVE OLAY DİNLEYİCİSİ ---
  void listenToNativeEvents(BuildContext context) {
    eventChannel.receiveBroadcastStream().listen((dynamic event) {
      final Map<dynamic, dynamic> data = event;
      final String type = data['type'];

      if (type == 'device_found') {
        bool exists = foundDevices.any((d) => d['deviceAddress'] == data['deviceAddress']);
        if (!exists) {
          foundDevices.add({
            'deviceName': data['deviceName'] ?? 'Bilinmeyen PC',
            'deviceAddress': data['deviceAddress'] ?? '',
          });
          LogService.info("Yeni cihaz bulundu: ${data['deviceName']}");
        }
        if (!isConnected && statusText != "Bağlanılıyor...") {
          statusText = "${foundDevices.length} PC Bulundu";
          statusColor = Colors.orangeAccent;
        }
      }
      else if (type == 'connected') {
        isConnected = true;
        isDiscovering = false;
        targetIp = "192.168.137.1"; // Wi-Fi Direct IP'si
        statusText = "PC'ye Bağlanıldı (Wi-Fi)";
        statusColor = Colors.green;
        foundDevices.clear();
        LogService.info("PC'ye Wi-Fi Direct üzerinden bağlanıldı.");
      }
      else if (type == 'stream_started') {
        isStreaming = true;
        isStreamLoading = false;
        statusText = "Ekran Paylaşılıyor!";
        statusColor = Colors.deepPurpleAccent;
        LogService.info("Ekran yayını başlatıldı.");
      }
      else if (type == 'stream_rejected') {
        isStreamLoading = false;
        LogService.error("Yayın izni masaüstü tarafından REDDEDİLDİ.");
        _showSnackBar(context, 'Ekran paylaşım izni reddedildi.');
      }
      else if (type == 'stream_stopped') {
        isStreaming = false;
        isStreamLoading = false;
        statusText = "Yayın Durduruldu";
        statusColor = Colors.green;
        LogService.info("Masaüstünden yayın durdurma sinyali alındı.");
      }
      else if (type == 'disconnected') {
        isConnected = false;
        isStreaming = false;
        isDiscovering = false;
        statusText = "Bağlantı Koptu";
        statusColor = Colors.redAccent;
        foundDevices.clear();
        LogService.error("Bağlantı koptu veya cihazdan ayrılıldı.");
      }

      // Olaylar işlendikten sonra arayüze "Kendini Yenile" diyoruz
      updateUI();

    }, onError: (dynamic error) {
      LogService.error('EventChannel Hatası: $error');
    });
  }

  // --- 2. CİHAZ KEŞFİ VE İZİNLER ---
  Future<void> toggleDiscovery(BuildContext context) async {
    if (isDiscovering) {
      LogService.info("Cihaz araması durduruluyor...");
      await platform.invokeMethod('stopDiscovery');
      isDiscovering = false;

      if (!isConnected) {
        statusText = "Arama Durduruldu";
        statusColor = Colors.grey;
      }
      updateUI();
    } else {
      // İzin kontrolü
      Map<Permission, PermissionStatus> statuses = await [
        Permission.nearbyWifiDevices,
        Permission.location,
      ].request();

      if (statuses[Permission.nearbyWifiDevices] == PermissionStatus.granted &&
          statuses[Permission.location] == PermissionStatus.granted) {

        isDiscovering = true;
        foundDevices.clear();
        statusText = "Ağ Taranıyor...";
        statusColor = Colors.orange;
        updateUI();

        LogService.info("Ağ taraması başlatıldı.");
        await platform.invokeMethod('startDiscovery');
      } else {
        LogService.error("Gerekli ağ/konum izinleri verilmedi.");
        _showSnackBar(context, 'Konum ve Yakın Cihaz izni gerekli!');
      }
    }
  }

  // --- 3. BAĞLANTI (CONNECT) ---
  Future<void> connectToPC(BuildContext context, String deviceAddress) async {
    try {
      statusText = "Bağlanılıyor...";
      statusColor = Colors.blueAccent;
      isDiscovering = false;
      updateUI();

      LogService.info("Şu adrese bağlantı deneniyor: $deviceAddress");
      await platform.invokeMethod('connect', {'address': deviceAddress});
    } on PlatformException catch (e) {
      statusText = "Bağlantı Başarısız";
      statusColor = Colors.redAccent;
      updateUI();
      LogService.error("Bağlantı hatası: ${e.message}");
    }
  }

  // --- 4. USB MODU ---
  Future<void> enableUsbMode(BuildContext context) async {
    isConnected = true;
    targetIp = "127.0.0.1"; // ADB Reverse IP'si
    isDiscovering = false;
    foundDevices.clear();
    statusText = "USB Modu Aktif";
    statusColor = Colors.teal;
    updateUI();

    try {
      // YENİ EKLENEN KISIM: Butona basar basmaz C# ile tüneli aç!
      await platform.invokeMethod('connectCommand', {'ip': targetIp});
      LogService.info("USB Modu: PC ile komut kanalı (50000) el sıkışması tamamlandı.");

      // Başarılı olursa arayüzdeki yazıyı güncelle
      statusText = "PC İle Eşleşildi (USB)";
      updateUI();
    } catch (e) {
      LogService.error("Komut kanalına bağlanılamadı. C# tüneli kapalı olabilir: $e");
    }
    _showSnackBar(context, 'USB Kablosu ile yayına hazır!');
  }

  // --- 5. YAYIN KONTROLÜ ---
  Future<void> toggleScreenCapture(BuildContext context) async {
    if (!isConnected) {
      LogService.error("Bağlantı yokken yayın başlatılmaya çalışıldı.");
      _showSnackBar(context, 'Önce bir PC\'ye bağlanmalısın veya USB Modunu açmalısın!');
      return;
    }

    try {
      isStreamLoading = true;
      updateUI();
      if (isStreaming) {
        LogService.info("Yayını durdurma isteği gönderiliyor...");
        await platform.invokeMethod('stopCapture');
      } else {
        LogService.info("Yayın başlatma isteği hedefe gönderiliyor...");
        await platform.invokeMethod('startCapture', {'ip': targetIp});
      }
    } on PlatformException catch (e) {
      isStreamLoading = false;
      updateUI();
      LogService.error("Yayın Hatası: ${e.message}");
    }
  }

  // --- YARDIMCI METOTLAR ---
  void _showSnackBar(BuildContext context, String message) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(message)),
    );
  }
}