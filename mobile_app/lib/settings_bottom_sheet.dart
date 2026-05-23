import 'package:flutter/material.dart';

class SettingsBottomSheet extends StatefulWidget {
  final double currentBitrate;
  final String currentResolution;
  // Ayar seçildikten sonra komutu ana sayfaya fırlatacak callback fonksiyonu
  final Function(String) onSettingsSaved;

  const SettingsBottomSheet({
    Key? key,
    required this.currentBitrate,
    required this.currentResolution,
    required this.onSettingsSaved
  }) : super(key: key);

  @override
  _SettingsBottomSheetState createState() => _SettingsBottomSheetState();
}

class _SettingsBottomSheetState extends State<SettingsBottomSheet> {
  late double _bitrate;
  late String _resolution;

  @override
  void initState() {
    super.initState();
    // Panel açıldığı an, o an dışarıda hangi ayar aktifse onu içeriye yüklüyoruz
    _bitrate = widget.currentBitrate;
    _resolution = widget.currentResolution;
  }
  @override
  Widget build(BuildContext context) {
    return Container(
      padding: const EdgeInsets.all(24),
      decoration: const BoxDecoration(
        color: Color(0xFF1E1E1E), // Senin kart tasarımlarına uygun koyu gri
        borderRadius: BorderRadius.vertical(top: Radius.circular(24)),
      ),
      child: Column(
        mainAxisSize: MainAxisSize.min, // Sadece içindeki elemanlar kadar yer kaplar
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          const Text(
              "Yayın Ayarları",
              style: TextStyle(color: Colors.white, fontSize: 20, fontWeight: FontWeight.bold)
          ),
          const SizedBox(height: 24),

          // Çözünürlük Seçimi
          const Text("Çözünürlük", style: TextStyle(color: Colors.grey, fontSize: 14)),
          const SizedBox(height: 8),
          Container(
            padding: const EdgeInsets.symmetric(horizontal: 16),
            decoration: BoxDecoration(
              color: const Color(0xFF2C2C2C),
              borderRadius: BorderRadius.circular(12),
            ),
            child: DropdownButtonHideUnderline(
              child: DropdownButton<String>(
                value: _resolution,
                dropdownColor: const Color(0xFF2C2C2C),
                isExpanded: true,
                icon: const Icon(Icons.arrow_drop_down, color: Colors.blueAccent),
                style: const TextStyle(color: Colors.white, fontSize: 16),
                items: ['720p', '1080p'].map((String value) {
                  return DropdownMenuItem<String>(
                    value: value,
                    child: Text(value),
                  );
                }).toList(),
                onChanged: (newValue) {
                  setState(() {
                    _resolution = newValue!;
                  });
                },
              ),
            ),
          ),
          const SizedBox(height: 24),

          // Bitrate Seçimi (Slider)
          Row(
            mainAxisAlignment: MainAxisAlignment.spaceBetween,
            children: [
              const Text("Video Kalitesi (Bitrate)", style: TextStyle(color: Colors.grey, fontSize: 14)),
              Text("${_bitrate.toInt()} Mbps", style: const TextStyle(color: Colors.blueAccent, fontWeight: FontWeight.bold)),
            ],
          ),
          Slider(
            value: _bitrate,
            min: 1,
            max: 8,
            divisions: 7,
            activeColor: Colors.blueAccent,
            inactiveColor: Colors.grey.shade800,
            onChanged: (value) {
              setState(() {
                _bitrate = value;
              });
            },
          ),
          const SizedBox(height: 24),

          // Kaydet Butonu
          SizedBox(
            width: double.infinity,
            child: ElevatedButton(
              style: ElevatedButton.styleFrom(
                backgroundColor: Colors.blueAccent,
                foregroundColor: Colors.white,
                padding: const EdgeInsets.symmetric(vertical: 16),
                shape: RoundedRectangleBorder(borderRadius: BorderRadius.circular(12)),
              ),
              onPressed: () {
                // Bizim meşhur komut formatını oluşturuyoruz
                String command = "SETTINGS|${_bitrate.toInt()}|$_resolution";

                // Komutu ana sayfaya gönder ve paneli kapat
                widget.onSettingsSaved(command);
                Navigator.pop(context);
              },
              child: const Text("Uygula", style: TextStyle(fontSize: 16, fontWeight: FontWeight.bold)),
            ),
          ),
          const SizedBox(height: 10), // Cihazların alt barları için ufak bir boşluk
        ],
      ),
    );
  }
}