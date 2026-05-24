import 'package:flutter/material.dart';
import 'package:mobile_app/log_files_page.dart';
import 'package:mobile_app/settings_bottom_sheet.dart';
import 'log_file_manager.dart';
import 'log_service.dart';
import 'mainPageTools.dart'; // İş mantığını (Tools) dahil et
import 'log_page.dart'; // Log sayfasını dahil et

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
  late AnimationController _breathingController;
  late Animation<double> _breathingAnimation;
  bool _showAppBorder = false;

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
            // --- YENİ EFEKT 1: AMBİYANS IŞIĞI (ARKAPLAN GÖLGESİ) ---
            // Durum rengine göre yumuşakça renk değiştiren ışık huzmesi
            AnimatedBuilder(
              animation: _breathingAnimation,
              builder: (context, child) {
                return Container(
                  decoration: BoxDecoration(
                    gradient: RadialGradient(
                      center: const Alignment(0, -0.4),
                      radius: _breathingAnimation.value, // İşte sihir burada! Çap sürekli değişiyor
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

            // 1. KATMAN: ANA İÇERİK (Arayüzünün geri kalanı)
            // --- 1. KATMAN: ANA İÇERİK (Arayüzünün geri kalanı) ---
            SafeArea(
              child: Padding(
                // Yazı arkada süzüleceği için butonları biraz daha aşağı ittik (top: 220)
                padding: const EdgeInsets.all(16),
                child: Column(
                  children: [
                    const Spacer(flex: 2,),
                    // --- YENİ 1: KAYDIRMALI MOD ŞALTERİ (TOGGLE SWITCH) ---
                    Container(
                      width: 220,
                      height: 50,
                      decoration: BoxDecoration(
                        color: const Color(0xFF252525),
                        borderRadius: BorderRadius.circular(25),
                        boxShadow: const [
                          BoxShadow(color: Colors.black26, blurRadius: 8, offset: Offset(0, 4)),
                        ],
                      ),
                      child: Stack(
                        children: [
                          // Kayar Arka Plan (Animasyonlu)
                          AnimatedPositioned(
                            duration: const Duration(milliseconds: 300),
                            curve: Curves.easeInOut,
                            top: 4,
                            bottom: 4,
                            left: isUsbModeSelected ? 110 : 4,
                            right: isUsbModeSelected ? 4 : 110,
                            child: Container(
                              decoration: BoxDecoration(
                                color: Colors.deepPurpleAccent,
                                borderRadius: BorderRadius.circular(20),
                              ),
                            ),
                          ),
                          // Buton Yazıları
                          Row(
                            children: [
                              Expanded(
                                child: GestureDetector(
                                  behavior: HitTestBehavior.opaque,
                                  onTap: () => setState(() => isUsbModeSelected = false),
                                  child: Center(
                                    child: Text("Wi-Fi", style: TextStyle(
                                      color: !isUsbModeSelected ? Colors.white : Colors.white54,
                                      fontWeight: FontWeight.bold,
                                    )),
                                  ),
                                ),
                              ),
                              Expanded(
                                child: GestureDetector(
                                  behavior: HitTestBehavior.opaque,
                                  onTap: () => setState(() => isUsbModeSelected = true),
                                  child: Center(
                                    child: Text("USB", style: TextStyle(
                                      color: isUsbModeSelected ? Colors.white : Colors.white54,
                                      fontWeight: FontWeight.bold,
                                    )),
                                  ),
                                ),
                              ),
                            ],
                          ),
                        ],
                      ),
                    ),
                    const SizedBox(height: 16),
                    // --- YENİ 2: DİNAMİK BAĞLANTI BUTONU ---
                    TextButton.icon(
                      onPressed: () {
                        if (isUsbModeSelected) {
                          tools.enableUsbMode(context);
                        } else {
                          tools.toggleDiscovery(context);
                        }
                      },
                      icon: Icon(
                          isUsbModeSelected ? Icons.usb_rounded : Icons.wifi_find_rounded,
                          color: Colors.white70
                      ),
                      label: Text(
                        isUsbModeSelected ? "Ara" : (tools.isDiscovering ? "Aramayı Durdur" : "Ara"),
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
                              // Yayın aktifse etrafa kırmızı ışık saçar, değilse hafif mor bir gölge
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

                    // 4. BULUNAN CİHAZLAR LİSTESİ
                    // Üstteki Spacer() ile alttaki Expanded ekranı eşit paylaşır, butonlar kusursuz merkeze oturur.
                    Expanded(
                      child: ListView.builder(
                        itemCount: tools.foundDevices.length,
                        itemBuilder: (context, index) {
                          var device = tools.foundDevices[index];
                          return Card(
                            color: const Color(0xFF1E1E1E),
                            margin: const EdgeInsets.symmetric(vertical: 6),
                            elevation: 4,
                            shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
                            child: ListTile(
                              leading: Container(
                                padding: const EdgeInsets.all(8),
                                decoration: BoxDecoration(
                                  color: const Color(0xFF2C2C2C),
                                  borderRadius: BorderRadius.circular(8),
                                ),
                                child: const Icon(Icons.computer, color: Colors.white70),
                              ),
                              title: Text(device['deviceName']!, style: const TextStyle(color: Colors.white, fontWeight: FontWeight.w600)),
                              subtitle: Text(device['deviceAddress']!, style: const TextStyle(color: Colors.white54, fontSize: 12)),
                              trailing: ElevatedButton(
                                style: ElevatedButton.styleFrom(
                                  backgroundColor: Colors.blueAccent.withOpacity(0.2),
                                  foregroundColor: Colors.blueAccent,
                                  elevation: 0,
                                  shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(8)),
                                ),
                                onPressed: () => tools.connectToPC(context, device['deviceAddress']!),
                                child: const Text("Bağlan", style: TextStyle(fontWeight: FontWeight.bold)),
                              ),
                            ),
                          );
                        },
                      ),
                    ),
                  ],
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
}