import '../../core/network/transport/base_transport.dart';

class SenderService {
  final BaseTransport transport;

  SenderService({required this.transport});

  Future<void> startScreenCapture() async {
    // TODO: Capture screen and send via transport
  }

  Future<void> stopScreenCapture() async {
    // TODO: Stop capturing
  }
}