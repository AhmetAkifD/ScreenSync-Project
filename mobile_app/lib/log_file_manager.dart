import 'dart:io';
import 'package:path_provider/path_provider.dart';

class LogFileManager {
  static File? currentLogFile;

  // Uygulama açıldığında (veya yayın başladığında) yeni bir oturum dosyası oluşturur
  static Future<void> initSessionLog() async {
    try {
      final directory = await getApplicationDocumentsDirectory();
      // Dosya adında geçersiz karakter olmasın diye saatteki ':' işaretlerini '-' yapıyoruz
      final timestamp = DateTime.now().toString().replaceAll(':', '-').substring(0, 19);

      final filePath = '${directory.path}/LogSession_$timestamp.txt';
      currentLogFile = File(filePath);

      await currentLogFile!.writeAsString("--- Log Oturumu Başladı: $timestamp ---\n");
    } catch (e) {
      print("Log dosyası oluşturulamadı: $e");
    }
  }

  // Gelen logları anında dosyaya ekler
  static Future<void> writeLog(String message) async {
    if (currentLogFile != null) {
      // mode: FileMode.append sayesinde eski yazılanları silmeden alt satıra ekler
      await currentLogFile!.writeAsString("$message\n", mode: FileMode.append);
    }
  }

  // İleride arayüzde göstermek ve silmek için tüm log dosyalarını getirir
  static Future<List<File>> getAllLogFiles() async {
    final directory = await getApplicationDocumentsDirectory();
    final List<FileSystemEntity> entities = directory.listSync();

    // Sadece .txt uzantılı bizim log dosyalarımızı filtrele
    return entities.whereType<File>().where((f) => f.path.endsWith('.txt')).toList();
  }
}