import '../../core/network/transport/base_transport.dart';

class ReceiverService {
  final BaseTransport transport;

  ReceiverService({required this.transport});

  void startReceiving() {
    transport.dataStream.listen((data) {
      // TODO: Decode and render data
    });
  }
}