import 'dart:io';
import 'package:flutter/material.dart';
import 'log_file_manager.dart';
import 'log_page.dart'; // Okuma ekranına geçiş için

class LogFilesPage extends StatefulWidget {
  @override
  _LogFilesPageState createState() => _LogFilesPageState();
}

class _LogFilesPageState extends State<LogFilesPage> {
  List<File> logFiles = [];
  bool isLoading = true;

  @override
  void initState() {
    super.initState();
    _loadFiles();
  }

  Future<void> _loadFiles() async {
    final files = await LogFileManager.getAllLogFiles();
    // En yeni dosyalar en üstte görünsün diye tersine çeviriyoruz
    setState(() {
      logFiles = files.reversed.toList();
      isLoading = false;
    });
  }

  Future<void> _deleteFile(File file) async {
    await file.delete();
    _loadFiles(); // Listeyi yenile
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: const Color(0xFF121212),
      appBar: AppBar(
        title: const Text("Log Kayıtları"),
        backgroundColor: Colors.black,
      ),
      body: isLoading
          ? const Center(child: CircularProgressIndicator(color: Colors.white))
          : logFiles.isEmpty
          ? const Center(child: Text("Henüz kaydedilmiş bir log yok.", style: TextStyle(color: Colors.grey)))
          : ListView.builder(
        itemCount: logFiles.length,
        itemBuilder: (context, index) {
          final file = logFiles[index];
          // Dosya adından "LogSession_" kısmını temizleyip sadece tarihi gösterelim
          final fileName = file.path.split('/').last.replaceAll('LogSession_', '').replaceAll('.txt', '');

          return Card(
            color: const Color(0xFF1E1E1E),
            margin: const EdgeInsets.symmetric(horizontal: 10, vertical: 5),
            child: ListTile(
              leading: const Icon(Icons.insert_drive_file, color: Colors.blueAccent),
              title: Text(fileName, style: const TextStyle(color: Colors.white, fontWeight: FontWeight.bold)),
              trailing: IconButton(
                icon: const Icon(Icons.delete, color: Colors.redAccent),
                onPressed: () => _deleteFile(file), // Çöp kutusuna basınca siler
              ),
              onTap: () {
                // Dosyaya tıklanınca okuma ekranına gönder
                Navigator.push(
                  context,
                  MaterialPageRoute(builder: (context) => LogPage(logFile: file)),
                );
              },
            ),
          );
        },
      ),
    );
  }
}