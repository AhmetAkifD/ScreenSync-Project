import 'package:flutter/material.dart';
import 'log_service.dart';
class LogPage extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    return Scaffold(
      backgroundColor: Color(0xFF121212),
      appBar: AppBar(
        title: Text("Sistem Logları"),
        backgroundColor: Colors.black,
      ),
      body: ValueListenableBuilder<List<String>>(
        valueListenable: LogService.logs,
        builder: (context, logList, child) {
          return ListView.builder(
            itemCount: logList.length,
            itemBuilder: (context, index) {
              return Padding(
                padding: const EdgeInsets.symmetric(horizontal: 10, vertical: 2),
                child: Text(
                  logList[index],
                  style: TextStyle(color: Colors.greenAccent, fontFamily: 'monospace'),
                ),
              );
            },
          );
        },
      ),
    );
  }
}