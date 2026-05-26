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

      // YENİ EKLENEN KISIM: Kotlin'den gelen logları anında yakalıyoruz
      if (type == 'log') {
        LogService.info(data['message'] ?? 'Boş log mesajı');
        return; // Log geldiğinde arayüzü boşuna güncellemeye (updateUI) gerek yok, buradan çık.
      }
      else if (type == 'device_found') {
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
        isStreaming = false;

        // Ekranı kıpkırmızı yapıp mesajı gömüyoruz
        statusText = "Yayın İsteği Reddedildi";
        statusColor = Colors.red.shade800; // O şık, göz alıcı koyu kırmızı ambiyans

        LogService.error("Yayın izni masaüstü tarafından REDDEDİLDİ.");
        updateUI(); // Arayüzü hemen kırmızıya boyamak için tetikliyoruz

        // 3 saniye o kırmızı ambiyansta beklesin, sonra temiz bir "Sistem Hazır" durumuna dönsün
        Future.delayed(const Duration(seconds: 3), () {
          // Eğer o 3 saniye içinde kullanıcı çılgınlık yapıp yeni bir yayın başlatmadıysa
          if (!isStreaming) {
            isConnected = false; // Soket kapandığı için bağlantıyı da sıfırlıyoruz
            statusText = "Sistem Hazır";
            statusColor = Colors.grey;
            updateUI(); // Ve ekran eski cool gri haline geri süzülüyor
          }
        });
      }
      else if (type == 'stream_stopped') {
        isStreaming = false;
        isStreamLoading = false;
        statusText = "Yayın Durduruldu";
        statusColor = Colors.green;
        LogService.info("Masaüstünden yayın durdurma sinyali alındı.");

        // YENİ: 3 saniye bekle ve sistemi eski haline döndür
        Future.delayed(const Duration(seconds: 3), () {
          // Eğer bu 3 saniye içinde kullanıcı tekrar yayını başlatmadıysa durumu sıfırla
          if (!isStreaming) {
            // Tamamen kopmuşsa en baştaki varsayılan duruma dön
            statusText = "Sistem Hazır";
            statusColor = Colors.grey;
            updateUI(); // Arayüze "kendini yenile" komutunu yolla
          }
        });
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
    try {
      // YENİ KONTROL: Kotlin'e USB takılı mı diye sor
      final bool isUsbPlugged = await platform.invokeMethod('isUsbConnected');

      //YENİ UI GÜNCELLEMESİNDEN DOLAYI ALTTAKİ KOD PROGRAMIN ÇALIŞMASINA ENGEL OLUYOR
      //TODO USB tespiti güncellemeden dolayı bozuldu. Düzeltilmesi gerekli
      /*if (!isUsbPlugged) {
        LogService.error("USB kablosu bağlı değil, işlem reddedildi.");
        _showSnackBar(context, 'Hata: Lütfen telefonu PC\'ye kabloyla bağlayın!');
        return; // İşlemi anında durdur, aşağıdaki kodları (arayüz değişimini) çalıştırma
      }*/

      // Kablo takılıysa her zamanki gibi devam et...
      isConnected = true;
      targetIp = "127.0.0.1"; // ADB Reverse IP'si
      isDiscovering = false;
      foundDevices.clear();
      statusText = "USB Modu Aktif (Cihaz Hazırlanıyor...)";
      statusColor = Colors.teal;
      updateUI();

      await platform.invokeMethod('connectCommand', {'ip': targetIp});
      LogService.info("USB Modu: PC ile komut kanalı (50000) el sıkışması tamamlandı.");

      statusText = "PC İle Eşleşildi (USB)";
      updateUI();
      _showSnackBar(context, 'USB Kablosu ile yayına hazır!');

    } catch (e) {
      LogService.error("Komut kanalına bağlanılamadı. C# tüneli kapalı olabilir: $e");
    }
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
        // 1. DURUM: Yayın zaten açıksa durdur. (İzin sormaya gerek yok)
        LogService.info("Yayını durdurma isteği gönderiliyor...");
        await platform.invokeMethod('stopCapture');
      } else {
        // 2. DURUM: Yayın kapalıysa başlat. (İşte burada mikrofon izni şart)
        var status = await Permission.microphone.request();

        if (status.isGranted) {
          LogService.info("Mikrofon izni alındı. Yayın başlatma isteği hedefe gönderiliyor...");
          await platform.invokeMethod('startCapture', {'ip': targetIp});
        } else {
          // İzin reddedilirse yükleme modundan çık ve uyarı ver
          isStreamLoading = false;
          updateUI();
          LogService.error("Mikrofon izni verilmediği için yayın iptal edildi.");
          _showSnackBar(context, 'Ses aktarımı için mikrofon izni gereklidir!');
        }
      }
    } on PlatformException catch (e) {
      isStreamLoading = false;
      updateUI();
      LogService.error("Yayın Hatası: ${e.message}");
    }
  }

  // --- 6. YAYIN AYARLARINI GÜNCELLEME ---
  Future<void> sendSettingsToAndroid(int bitrate, String resolution) async {
    try {
      LogService.info("[FLUTTER] Yeni ayarlar Kotlin'e iletiliyor: $resolution / $bitrate Mbps");

      // Kotlin'deki 'updateStreamSettings' metodunu tetikliyoruz
      await platform.invokeMethod('updateStreamSettings', {
        'bitrate': bitrate,
        'resolution': resolution,
      });

    } on PlatformException catch (e) {
      LogService.error("[FLUTTER] Kotlin ile iletişim hatası (Ayarlar gönderilemedi): ${e.message}");
    }
  }

  Future<void> toggleMicrophone(bool isMuted) async {
    try {
      await platform.invokeMethod('toggleMicrophone', {'isMuted': isMuted});
      LogService.info(isMuted ? "[UI] Mikrofon kapatma isteği gönderildi." : "[UI] Mikrofon açma isteği gönderildi.");
    } catch (e) {
      LogService.error("Mikrofon durumu değiştirilemedi: $e");
    }
  }

  // --- YARDIMCI METOTLAR ---
  void _showSnackBar(BuildContext context, String message) {
    ScaffoldMessenger.of(context).showSnackBar(
      SnackBar(content: Text(message)),
    );
  }
}