import 'dart:async';
import 'dart:io';
import 'dart:typed_data';
import 'base_transport.dart';

class WirelessTransport implements BaseTransport {
  final String ipAddress;
  final int port;
  Socket? _socket;
  final _dataController = StreamController<List<int>>.broadcast();

  // Constructor with IP and Port
  WirelessTransport({required this.ipAddress, this.port = 50005});

  @override
  Future<void> connect() async {
    try {
      _socket = await Socket.connect(ipAddress, port);
      _socket!.listen(_handleData, onError: (e) => disconnect(), onDone: disconnect);
    } catch (e) {
      throw Exception("Bağlantı hatası: $e");
    }
  }

  List<int> _buffer = [];
  int? _expectedLength;

  void _handleData(List<int> data) {
    _buffer.addAll(data);

    while (true) {
      if (_expectedLength == null) {
        if (_buffer.length >= 4) {
          final bytes = Uint8List.fromList(_buffer.sublist(0, 4));
          // C# tarafı BigEndian (Reverse edilmiş) gönderiyor
          _expectedLength = ByteData.sublistView(bytes).getInt32(0, Endian.big);
          _buffer = _buffer.sublist(4);
        } else {
          break;
        }
      }

      if (_expectedLength != null && _buffer.length >= _expectedLength!) {
        final frameData = _buffer.sublist(0, _expectedLength!);
        _dataController.add(frameData);
        _buffer = _buffer.sublist(_expectedLength!);
        _expectedLength = null;
      } else {
        break;
      }
    }
  }

  @override
  Future<void> disconnect() async {
    await _socket?.close();
    _socket = null;
  }

  @override
  Stream<List<int>> get dataStream => _dataController.stream;

  @override
  Future<void> sendData(List<int> data) async {
    if (_socket != null) {
      final lengthBytes = ByteData(4)..setInt32(0, data.length, Endian.big);
      _socket!.add(lengthBytes.buffer.asUint8List());
      _socket!.add(data);
    }
  }
}