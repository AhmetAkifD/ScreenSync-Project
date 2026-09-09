import 'package:flutter/material.dart';
import 'dart:typed_data'; // Uint8List için
import 'package:mobile_app/log_files_page.dart';
import 'package:mobile_app/settings_bottom_sheet.dart';
import 'log_file_manager.dart';
import 'log_service.dart';
import 'mainPageTools.dart'; // İş mantığını (Tools) dahil et
import 'log_page.dart'; // Log sayfasını dahil et
import 'custom_sliding_switch.dart';
import 'core/network/transport/wireless_transport.dart';
import 'features/receiver/receiver_service.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();
  await LogFileManager.initSessionLog();
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

class _HomeScreenState extends State<HomeScreen> with SingleTickerProviderStateMixin {

  late MainPageTools tools;
  double activeBitrate = 4.0;
  String activeResolution = '720p';
  bool isUsbModeSelected = false;
  bool isTargetPCSelected = false; // false = Telefon, true = PC
  bool isReceiverSelected = false; // false = Gönderici, true = Alıcı
  late AnimationController _breathingController;
  late Animation<double> _breathingAnimation;
  bool _showAppBorder = false;
  bool isMicMuted = true;

  // Yeni Mimari - Alıcı Servisleri
  ReceiverService? _receiverService;
  bool _isReceivingStream = false;

  @override
  void initState() {
    super.initState();

    // --- 1. ARAÇLAR VE SİSTEM KURULUMU ---
    // Tools'u başlat ve UI yenileme köprüsünü kur
    tools = MainPageTools(() {
      if (mounted) setState(() {});
    });

    // Olay dinleyicisini çalıştır
    tools.listenToNativeEvents(context);

    // --- 2. AMBİYANS ANİMASYONU KURULUMU ---
    // 3 saniyede bir nefes alıp veren bir döngü kuruyoruz
    _breathingController = AnimationController(
      vsync: this,
      duration: const Duration(seconds: 3),
    )..repeat(reverse: true); // Yumuşakça sönmesi için reverse açık

    // Işığın yayılma çapı 1.0 ile 1.5 arasında gidip gelecek
    _breathingAnimation = Tween<double>(begin: 1.0, end: 1.5).animate(
        CurvedAnimation(parent: _breathingController, curve: Curves.easeInOutSine)
    );
    Future.delayed(const Duration(seconds: 3), () {
      if (mounted) {
        setState(() => _showAppBorder = true); // Sınırı belirginleştir

        Future.delayed(const Duration(seconds: 2), () {
          if (mounted) {
            setState(() => _showAppBorder = false); // Sınırı yavaşça yok et
          }
        });
      }
    });
  }
  @override
  void dispose() {
    _breathingController.dispose(); // Animasyon motorunu kapat
    super.dispose();
  }

  // --- İŞTE ARAYÜZÜN (UI) YENİ HALİ ---
  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFF121212),
      body: Stack(
          children: [
            // --- YENİ EFEKT 0: GELEN EKRAN YAYINI (ALICI MODU İÇİN) ---
            if (_isReceivingStream && _receiverService != null)
              Positioned.fill(
                child: ValueListenableBuilder<Uint8List?>(
                  valueListenable: _receiverService!.currentFrame,
                  builder: (context, frame, child) {
                    if (frame == null || frame.isEmpty) {
                      return const Center(
                        child: CircularProgressIndicator(color: Colors.white),
                      );
                    }
                    return Image.memory(
                      frame,
                      fit: BoxFit.contain, // Ekranı taşmadan sığdır
                      gaplessPlayback: true, // Titremeyi (flicker) engeller
                    );
                  },
                ),
              ),

            // --- YENİ EFEKT 1: AMBİYANS IŞIĞI (ARKAPLAN GÖLGESİ) ---
            // Durum rengine göre yumuşakça renk değiştiren ışık huzmesi
            if (!_isReceivingStream) // Yayın izleniyorken ambiyans ışığını gizle
              AnimatedBuilder(
                animation: _breathingAnimation,
                builder: (context, child) {
                  return Container(
                    decoration: BoxDecoration(
                      gradient: RadialGradient(
                        center: const Alignment(0, -0.4),
                        radius: _breathingAnimation.value,
                        colors: [
                          tools.statusColor.withOpacity(0.25), // Işığın merkez gücü
                          const Color(0xFF121212), // Karanlığa karışma
                        ],
                        stops: const [0.1, 0.8],
                      ),
                    ),
                  );
                },
              ),

            // --- YENİ EFEKT 2: HAYALET TİPOGRAFİ (SİSTEM MESAJI) ---
            // Arkada süzülen, büyük ve yarı şeffaf durum yazısı
            if (!_isReceivingStream)
              Align(
                alignment: const Alignment(0, -0.45),
                child: Padding(
                  padding: const EdgeInsets.symmetric(horizontal: 20),
                  child: AnimatedSwitcher(
                    duration: const Duration(milliseconds: 500),
                    child: Text(
                      tools.statusText.toUpperCase(),
                      key: ValueKey(tools.statusText), // Animasyonun tetiklenmesi için gerekli
                      textAlign: TextAlign.center,
                      style: TextStyle(
                        fontSize: 32, // Oldukça büyük
                        fontWeight: FontWeight.w900, // Çok kalın
                        letterSpacing: 4.0, // Harf araları açık, sinematik bir his
                        color: tools.statusColor.withOpacity(0.35), // Yarı şeffaf, arka plana gömülü
                      ),
                    ),
                  ),
                ),
              ),

            // Kapat Butonu (Sadece yayın izlenirken görünür)
            if (_isReceivingStream)
              Positioned(
                bottom: 40,
                left: 0,
                right: 0,
                child: Center(
                  child: FloatingActionButton(
                    backgroundColor: Colors.redAccent,
                    onPressed: () {
                      _receiverService?.stopReceiving();
                      setState(() {
                        _isReceivingStream = false;
                        tools.statusText = "Yayın Durduruldu";
                        tools.statusColor = Colors.grey;
                      });
                    },
                    child: const Icon(Icons.close_rounded, color: Colors.white, size: 30),
                  ),
                ),
              ),

            // --- 1. KATMAN: ANA İÇERİK (Arayüzünün geri kalanı) ---
            if (!_isReceivingStream)
              SafeArea(
                child: Padding(
                  padding: const EdgeInsets.all(16),
                  child: SizedBox(
                    width: double.infinity,
                    child: Column(
                    children: [
                      const Spacer(flex: 4), // Üst boşluk

                      // --- BAĞLANTI TERCİHLERİ ---
                      const Text("Bağlantı Türü", style: TextStyle(color: Colors.white54, fontSize: 12)),
                      const SizedBox(height: 4),
                      CustomSlidingSwitch(
                        textLeft: "Wi-Fi",
                        textRight: "USB",
                        isRightSelected: isUsbModeSelected,
                        onChanged: (val) => setState(() => isUsbModeSelected = val),
                      ),
                      const SizedBox(height: 12),

                      const Text("Karşıdaki Cihaz", style: TextStyle(color: Colors.white54, fontSize: 12)),
                      const SizedBox(height: 4),
                      CustomSlidingSwitch(
                        textLeft: "Telefon",
                        textRight: "Bilgisayar",
                        isRightSelected: isTargetPCSelected,
                        onChanged: (val) => setState(() => isTargetPCSelected = val),
                      ),
                      const SizedBox(height: 12),

                      const Text("Benim Rolüm", style: TextStyle(color: Colors.white54, fontSize: 12)),
                      const SizedBox(height: 4),
                      CustomSlidingSwitch(
                        textLeft: "Ekranı Paylaş",
                        textRight: "Ekranı İzle",
                        isRightSelected: isReceiverSelected,
                        onChanged: (val) => setState(() => isReceiverSelected = val),
                      ),
                      const SizedBox(height: 24),

                      // --- YENİ 2: DİNAMİK BAĞLANTI BUTONU ---
                      TextButton.icon(
                        onPressed: () {
                          if (isUsbModeSelected) {
                            tools.enableUsbMode(context);
                          } else {
                            if (isReceiverSelected && isTargetPCSelected) {
                                // YENİ MİMARİ: Bilgisayardan telefona görüntü al!
                                _showIpInputDialog(context);
                            } else {
                                // ESKİ MİMARİ
                                tools.toggleDiscovery(context);
                            }
                          }
                        },
                        icon: Icon(
                            isUsbModeSelected ? Icons.usb_rounded : Icons.wifi_find_rounded,
                            color: Colors.white70
                        ),
                        label: Text(
                          isUsbModeSelected ? "Ara" : (tools.isDiscovering ? "Aramayı Durdur" : "Bağlan (Ara)"),
                          style: const TextStyle(color: Colors.white, fontSize: 16),
                        ),
                        style: TextButton.styleFrom(
                          padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 12),
                          backgroundColor: Colors.white.withOpacity(0.05),
                          shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(20)),
                        ),
                      ),
                      const SizedBox(height: 16),

                      // --- YENİ 3: DEV YAYIN DEKLANŞÖRÜ (PULSATING CORE) ---
                      GestureDetector(
                        onTap: tools.isStreamLoading ? null : () => tools.toggleScreenCapture(context),
                        child: AnimatedContainer(
                          duration: const Duration(milliseconds: 500),
                          width: 100,
                          height: 100,
                          decoration: BoxDecoration(
                            shape: BoxShape.circle,
                            color: tools.isStreaming ? Colors.red.shade600 : Colors.deepPurpleAccent,
                            boxShadow: [
                              BoxShadow(
                                color: tools.isStreaming ? Colors.red.withOpacity(0.6) : Colors.deepPurpleAccent.withOpacity(0.3),
                                blurRadius: tools.isStreaming ? 30 : 15,
                                spreadRadius: tools.isStreaming ? 10 : 2,
                              ),
                            ],
                          ),
                          child: Center(
                            child: tools.isStreamLoading
                                ? const CircularProgressIndicator(color: Colors.white)
                                : Icon(
                              tools.isStreaming ? Icons.stop_rounded : Icons.cast_rounded,
                              color: Colors.white,
                              size: 40,
                            ),
                          ),
                        ),
                      ),
                      const SizedBox(height: 40),
                      // --- YENİ 4: MİKROFON BUTONU (AŞAĞI KAYMA EFEKTİ VE SABİT ALAN) ---
                      SizedBox(
                        height: 130, // 1. ÇÖZÜM: Alanı sabitledik. Butonlar artık ASLA yukarı kaymaz.
                        child: IgnorePointer(
                          ignoring: !tools.isStreaming, // Görünmezken yanlışlıkla tıklanmasını engeller
                          child: AnimatedOpacity(
                            duration: const Duration(milliseconds: 300),
                            opacity: tools.isStreaming ? 1.0 : 0.0,
                            child: AnimatedSlide(
                              duration: const Duration(milliseconds: 500),
                              curve: Curves.easeOutBack, // Hafif yaylanarak yerine oturma efekti
                              // 2. ÇÖZÜM: Y eksenini -0.8'den 0'a çekerek "yukarıdan aşağı inme" hissi verdik
                              offset: tools.isStreaming ? Offset.zero : const Offset(0, -0.8),
                              child: Column(
                                mainAxisAlignment: MainAxisAlignment.center,
                                children: [
                                  const SizedBox(height: 10),
                                  GestureDetector(
                                    onTap: () {
                                      setState(() {
                                        isMicMuted = !isMicMuted;
                                      });
                                      tools.toggleMicrophone(isMicMuted);
                                    },
                                    child: AnimatedContainer(
                                      duration: const Duration(milliseconds: 300),
                                      padding: const EdgeInsets.all(16),
                                      decoration: BoxDecoration(
                                        shape: BoxShape.circle,
                                        color: isMicMuted ? const Color(0xFF252525) : Colors.tealAccent.withOpacity(0.15),
                                        border: Border.all(
                                          color: isMicMuted ? Colors.white24 : Colors.tealAccent,
                                          width: 2,
                                        ),
                                        boxShadow: [
                                          if (!isMicMuted)
                                            BoxShadow(
                                              color: Colors.tealAccent.withOpacity(0.3),
                                              blurRadius: 15,
                                              spreadRadius: 2,
                                            )
                                        ],
                                      ),
                                      child: Icon(
                                        isMicMuted ? Icons.mic_off_rounded : Icons.mic_rounded,
                                        color: isMicMuted ? Colors.white54 : Colors.tealAccent,
                                        size: 28,
                                      ),
                                    ),
                                  ),
                                  const SizedBox(height: 10),
                                ],
                              ),
                            ),
                          ),
                        ),
                      ),

                      const Spacer(flex: 1),
                    ],
                  ),
                ),
              ),
            ),

            // 2. KATMAN: SÜZÜLEN (FLOATING) KAPSÜLLER
            Positioned(
              top: MediaQuery.of(context).padding.top + 12, // Çentiğin hemen altı
              left: 16,
              right: 16,
              // 2. SORUNUN ÇÖZÜMÜ: Tek bir Container yerine Row içine iki ayrı Container koyduk
              child: Row(
                mainAxisAlignment: MainAxisAlignment.spaceBetween,
                children: [

                  // SOL KAPSÜL: Uygulama İsmi
                  AnimatedContainer(
                    duration: const Duration(milliseconds: 600), // Rengin yumuşakça belirip kaybolma süresi
                    padding: const EdgeInsets.symmetric(horizontal: 20, vertical: 14),
                    decoration: BoxDecoration(
                      color: const Color(0xFF252525),
                      borderRadius: BorderRadius.circular(30),
                      // İŞTE SİHİRLİ SINIR: Duruma göre tatlı bir mavi yanar veya tamamen şeffaf olur
                      border: Border.all(
                        color: _showAppBorder ? Colors.blueAccent.withOpacity(0.8) : Colors.transparent,
                        width: 1.5, // İnce ve zarif bir kalınlık
                      ),
                      boxShadow: const [
                        BoxShadow(color: Colors.black38, blurRadius: 10, offset: Offset(0, 4)),
                      ],
                    ),
                    child: const Text(
                      "ScreenSync",
                      style: TextStyle(color: Colors.white, fontSize: 18, fontWeight: FontWeight.bold, letterSpacing: 1.0),
                    ),
                  ),

                  // SAĞ KAPSÜL: İkonlar (Log ve Ayarlar)
                  Container(
                    padding: const EdgeInsets.symmetric(horizontal: 6, vertical: 4),
                    decoration: BoxDecoration(
                      color: const Color(0xFF252525),
                      borderRadius: BorderRadius.circular(30),
                      boxShadow: const [
                        BoxShadow(color: Colors.black38, blurRadius: 10, offset: Offset(0, 4)),
                      ],
                    ),
                    child: Row(
                      children: [
                        // 1. LOGLAR BUTONU
                        IconButton(
                          icon: const Icon(Icons.terminal_rounded, color: Colors.white70),
                          tooltip: 'Loglar',
                          onPressed: () {
                            // Log ekranına yönlendirme
                            Navigator.push(
                              context,
                              MaterialPageRoute(builder: (context) => LogFilesPage()), // Kendi log sayfanın adını yaz
                            );
                          },
                        ),

                        // 2. AYARLAR BUTONU
                        IconButton(
                          icon: const Icon(Icons.settings_rounded, color: Colors.white70),
                          tooltip: 'Ayarlar',
                          onPressed: () {
                            showModalBottomSheet(
                              context: context,
                              backgroundColor: Colors.transparent,
                              isScrollControlled: true,
                              builder: (context) => SettingsBottomSheet(
                                currentBitrate: activeBitrate,
                                currentResolution: activeResolution,
                                onSettingsSaved: (command) {
                                  final parts = command.split('|');
                                  final int newBitrate = int.parse(parts[1]);
                                  final String newResolution = parts[2];

                                  setState(() {
                                    activeBitrate = newBitrate.toDouble();
                                    activeResolution = newResolution;
                                  });

                                  // Kotlin'e yeni ayarları ateşle
                                  tools.sendSettingsToAndroid(newBitrate, newResolution);
                                  LogService.info("[UI] Ayarlar kaydedildi ve işlenmek üzere gönderildi.");
                                },
                              ),
                            );
                          },
                        ),
                      ],
                    ),
                  ),

                ],
              ),
            ),
          ]
      ),
    );
  }

  // --- YENİ MİMARİ: IP GİRİŞ DİYALOĞU ---
  void _showIpInputDialog(BuildContext context) {
    final TextEditingController ipController = TextEditingController(text: "192.168.1.");
    showDialog(
      context: context,
      builder: (ctx) => AlertDialog(
        title: const Text("Bilgisayarın IP Adresi"),
        content: TextField(
          controller: ipController,
          decoration: const InputDecoration(hintText: "Örn: 192.168.1.50"),
          keyboardType: TextInputType.number,
        ),
        actions: [
          TextButton(
            onPressed: () => Navigator.pop(ctx),
            child: const Text("İptal"),
          ),
          TextButton(
            onPressed: () async {
              Navigator.pop(ctx);
              await _startReceivingFromPC(ipController.text);
            },
            child: const Text("Bağlan"),
          ),
        ],
      ),
    );
  }

  Future<void> _startReceivingFromPC(String ip) async {
    try {
      setState(() {
        tools.statusText = "PC'ye Bağlanılıyor...";
        tools.statusColor = Colors.blue;
      });

      final transport = WirelessTransport(ipAddress: ip, port: 50005);
      await transport.connect();

      _receiverService = ReceiverService(transport: transport);
      _receiverService!.startReceiving();

      setState(() {
        _isReceivingStream = true;
        tools.statusText = "Ekran Alınıyor";
        tools.statusColor = Colors.green;
      });
      LogService.info("PC'ye ($ip) bağlanıldı ve görüntü akışı başlatıldı.");
    } catch (e) {
      LogService.error("Alıcı hatası: $e");
      setState(() {
        tools.statusText = "Bağlantı Hatası";
        tools.statusColor = Colors.red;
      });
      ScaffoldMessenger.of(context).showSnackBar(SnackBar(content: Text("Bağlanılamadı: $e")));
    }
  }
}