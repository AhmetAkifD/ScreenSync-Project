import 'dart:async';
import 'dart:io';
import 'dart:typed_data';
import 'base_transport.dart';

class UdpTransport implements BaseTransport {
  final String ipAddress;
  final int port;
  RawDatagramSocket? _socket;
  final _dataController = StreamController<List<int>>.broadcast();

  int _currentFrameId = -1;
  Map<int, Uint8List> _fragments = {};
  int _expectedFragments = 0;

  UdpTransport({required this.ipAddress, this.port = 50005});

  @override
  Future<void> connect() async {
    try {
      _socket = await RawDatagramSocket.bind(InternetAddress.anyIPv4, 0);
      
      // Send a dummy packet to punch hole / let server know our IP & Port
      _socket!.send([0x01], InternetAddress(ipAddress), port);

      _socket!.listen((RawSocketEvent event) {
        if (event == RawSocketEvent.read) {
          Datagram? datagram = _socket!.receive();
          if (datagram != null) {
            _handleData(datagram.data);
          }
        }
      });
    } catch (e) {
      throw Exception("UDP Bağlantı hatası: $e");
    }
  }

  void _handleData(List<int> data) {
    if (data.length < 8) return;

    final byteData = ByteData.sublistView(Uint8List.fromList(data));
    int frameId = byteData.getInt32(0, Endian.big);
    int totalFragments = byteData.getInt16(4, Endian.big);
    int fragmentIndex = byteData.getInt16(6, Endian.big);

    // Yeni bir kare (frame) başladıysa öncekini çöpe at
    if (frameId > _currentFrameId) {
      _currentFrameId = frameId;
      _fragments.clear();
      _expectedFragments = totalFragments;
    }

    // Eski bir karenin paketi geç geldiyse yoksay
    if (frameId < _currentFrameId) return;

    // Payload'ı kaydet
    _fragments[fragmentIndex] = Uint8List.fromList(data.sublist(8));

    // Tüm parçalar ulaştıysa birleştir ve ekrana yolla
    if (_fragments.length == _expectedFragments) {
      BytesBuilder builder = BytesBuilder();
      for (int i = 0; i < _expectedFragments; i++) {
        if (_fragments.containsKey(i)) {
          builder.add(_fragments[i]!);
        } else {
          return; // Eksik parça var (UDP kaybı) - bu frame çizilmez
        }
      }
      _dataController.add(builder.toBytes());
      _fragments.clear();
    }
  }

  @override
  Future<void> disconnect() async {
    _socket?.close();
    _socket = null;
  }

  @override
  Stream<List<int>> get dataStream => _dataController.stream;

  @override
  Future<void> sendData(List<int> data) async {
    // Mobil şu an veri göndermiyor, sadece alıcı
  }
}