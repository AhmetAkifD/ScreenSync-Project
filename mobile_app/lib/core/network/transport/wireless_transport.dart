import 'base_transport.dart';

class WirelessTransport implements BaseTransport {
  @override
  Future<void> connect() async {
    // TODO: Implement TCP/UDP/WebRTC connection
  }

  @override
  Future<void> disconnect() async {
    // TODO: Implement disconnect
  }

  @override
  Stream<List<int>> get dataStream => const Stream.empty();

  @override
  Future<void> sendData(List<int> data) async {
    // TODO: Implement send
  }
}