abstract class BaseTransport {
  Future<void> connect();
  Future<void> disconnect();
  Stream<List<int>> get dataStream;
  Future<void> sendData(List<int> data);
}