import 'dart:async';
import 'dart:typed_data';
import 'package:flutter/foundation.dart';
import '../../core/network/transport/base_transport.dart';

class ReceiverService {
  final BaseTransport transport;
  
  // UI'ın dinleyebilmesi için bir notifier
  final ValueNotifier<Uint8List?> currentFrame = ValueNotifier<Uint8List?>(null);

  ReceiverService({required this.transport});

  void startReceiving() {
    transport.dataStream.listen((data) {
      // data zaten JPEG formatında byte dizisi olarak geliyor
      currentFrame.value = Uint8List.fromList(data);
    }, onError: (e) {
      debugPrint("Veri alım hatası: $e");
    });
  }

  void stopReceiving() {
    currentFrame.value = null;
    transport.disconnect();
  }
}