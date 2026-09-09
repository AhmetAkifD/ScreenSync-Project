import 'base_transport.dart';

class WiredTransport implements BaseTransport {
  @override
  Future<void> connect() async {
    // TODO: Implement USB/ADB connection
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