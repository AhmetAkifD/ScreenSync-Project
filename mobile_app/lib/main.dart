import 'package:flutter/material.dart';
import 'package:mobile_app/log_files_page.dart';
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

class _HomeScreenState extends State<HomeScreen> {
  late MainPageTools tools;

  @override
  void initState() {
    super.initState();

    // Tools'u başlat ve UI yenileme köprüsünü kur
    tools = MainPageTools(() {
      if (mounted) setState(() {});
    });

    // Olay dinleyicisini çalıştır
    tools.listenToNativeEvents(context);
  }

  // --- İŞTE ARAYÜZÜN (UI) YENİ HALİ ---
  @override
  Widget build(BuildContext context) {
    return Listener(
      onPointerDown: (PointerDownEvent event) {
        LogService.info("[FLUTTER-DOKUNMATİK] Ekrana dokunuldu! Koordinat: X=${event.position.dx}, Y=${event.position.dy}");
      },
      child: Scaffold(
        appBar: AppBar(
          title: const Text('ScreenSync Mobile'),
          actions: [
            // Gelişmiş Log Ekranı Butonu (Sağ üste ekledik)
            IconButton(
              icon: const Icon(Icons.bug_report),
              tooltip: 'Logları Gör',
              onPressed: () {
                Navigator.push(
                  context,
                  MaterialPageRoute(builder: (context) => LogFilesPage()),
                );
              },
            )
          ],
        ),
        body: Column(
          children: [
            // 1. DURUM ÇUBUĞU (Eskiden _statusText idi, şimdi tools.statusText)
            Container(
              padding: const EdgeInsets.all(16),
              color: tools.statusColor,
              width: double.infinity,
              child: Text(
                tools.statusText,
                textAlign: TextAlign.center,
                style: const TextStyle(fontSize: 18, fontWeight: FontWeight.bold),
              ),
            ),
      
            const SizedBox(height: 20),
      
            // 2. BAĞLANTI BUTONLARI (Fonksiyonların içine context ekliyoruz)
            Row(
              mainAxisAlignment: MainAxisAlignment.spaceEvenly,
              children: [
                ElevatedButton(
                  onPressed: () => tools.toggleDiscovery(context),
                  child: Text(tools.isDiscovering ? "Aramayı Durdur" : "PC Ara (Wi-Fi)"),
                ),
                ElevatedButton(
                  onPressed: () => tools.enableUsbMode(context),
                  child: const Text("USB Modu"),
                ),
              ],
            ),
      
            const SizedBox(height: 20),
      
            // 3. YAYIN BUTONU
            ElevatedButton(
              style: ElevatedButton.styleFrom(
                backgroundColor: tools.isStreaming ? Colors.red : Colors.deepPurple,
                foregroundColor: Colors.white,
                padding: const EdgeInsets.symmetric(horizontal: 40, vertical: 15),
              ),
              onPressed: tools.isStreamLoading ? null : () => tools.toggleScreenCapture(context),
              child: tools.isStreamLoading
                  ? const SizedBox(
                  width: 20, height: 20,
                  child: CircularProgressIndicator(color: Colors.white, strokeWidth: 2)
              )
                  : Text(
                tools.isStreaming ? "Yayını Durdur" : "Yayını Başlat",
                style: const TextStyle(fontSize: 16),
              ),
            ),
      
            const SizedBox(height: 20),
      
            // 4. BULUNAN CİHAZLAR LİSTESİ (Eskiden _foundDevices idi, şimdi tools.foundDevices)
            Expanded(
              child: ListView.builder(
                itemCount: tools.foundDevices.length,
                itemBuilder: (context, index) {
                  var device = tools.foundDevices[index];
                  return Card(
                    margin: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
                    child: ListTile(
                      leading: const Icon(Icons.computer),
                      title: Text(device['deviceName']!),
                      subtitle: Text(device['deviceAddress']!),
                      trailing: ElevatedButton(
                        // Bağlanma fonksiyonuna da context ve adresi yolluyoruz
                        onPressed: () => tools.connectToPC(context, device['deviceAddress']!),
                        child: const Text("Bağlan"),
                      ),
                    ),
                  );
                },
              ),
            ),
          ],
        ),
      ),
    );
  }
}