import 'dart:io';
import 'package:flutter/material.dart';
import 'log_service.dart';

class LogPage extends StatelessWidget {
  final File logFile;

  const LogPage({Key? key, required this.logFile}) : super(key: key);

  Future<List<String>> _readLogLines() async {
    try {
      final content = await logFile.readAsString();
      // İçeriği satır satır bölüp boş olanları filtreliyoruz
      return content.split('\n').where((line) => line.trim().isNotEmpty).toList();
    } catch (e) {
      return ["Dosya okunamadı: $e"];
    }
  }

  @override
  Widget build(BuildContext context) {
    // Başlıkta dosyanın tarihini gösterelim
    final titleDate = logFile.path.split('/').last.replaceAll('LogSession_', '').replaceAll('.txt', '');

    return Scaffold(
      backgroundColor: const Color(0xFF121212),
      appBar: AppBar(
        title: Text(titleDate, style: const TextStyle(fontSize: 16)),
        backgroundColor: Colors.black,
      ),
      body: FutureBuilder<List<String>>(
        future: _readLogLines(),
        builder: (context, snapshot) {
          if (snapshot.connectionState == ConnectionState.waiting) {
            return const Center(child: CircularProgressIndicator(color: Colors.white));
          }

          if (snapshot.hasError || !snapshot.hasData || snapshot.data!.isEmpty) {
            return const Center(child: Text("Log kaydı boş.", style: TextStyle(color: Colors.grey)));
          }

          final logList = snapshot.data!;

          return ListView.builder(
            itemCount: logList.length,
            itemBuilder: (context, index) {
              return Padding(
                padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 2),
                child: SelectableText( // Kullanıcı logları kopyalayabilsin diye Text yerine SelectableText kullandık
                  logList[index],
                  style: TextStyle(
                    color: LogService.getLogColor(logList[index]),
                    fontWeight: FontWeight.w500,
                    fontFamily: 'monospace',
                  ),
                ),
              );
            },
          );
        },
      ),
    );
  }
}