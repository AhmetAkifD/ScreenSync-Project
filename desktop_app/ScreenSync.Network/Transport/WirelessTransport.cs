using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace ScreenSync.Network.Transport
{
    public class WirelessTransport : ITransport
    {
        private TcpListener _listener;
        private TcpClient _client;
        private NetworkStream _stream;
        private bool _isConnected;

        public event EventHandler<byte[]> OnDataReceived;

        public async Task ConnectAsync()
        {
            // Sunucu modunda başlatıyoruz (PC sunucu olacak)
            _listener = new TcpListener(IPAddress.Any, 50005);
            _listener.Start();
            
            _client = await _listener.AcceptTcpClientAsync();
            _stream = _client.GetStream();
            _isConnected = true;

            // Arka planda dinleme döngüsü (Eğer telefondan mesaj gelirse diye)
            _ = Task.Run(ReceiveLoopAsync);
        }

        private async Task ReceiveLoopAsync()
        {
            try
            {
                byte[] header = new byte[4];
                while (_isConnected && _client.Connected)
                {
                    int read = await _stream.ReadAsync(header, 0, 4);
                    if (read < 4) break;

                    if (BitConverter.IsLittleEndian) Array.Reverse(header);
                    int size = BitConverter.ToInt32(header, 0);

                    byte[] data = new byte[size];
                    int totalRead = 0;
                    while (totalRead < size)
                    {
                        read = await _stream.ReadAsync(data, totalRead, size - totalRead);
                        if (read == 0) break;
                        totalRead += read;
                    }
                    OnDataReceived?.Invoke(this, data);
                }
            }
            catch { }
            finally { await DisconnectAsync(); }
        }

        public Task DisconnectAsync()
        {
            _isConnected = false;
            _stream?.Dispose();
            _client?.Dispose();
            _listener?.Stop();
            return Task.CompletedTask;
        }

        public async Task SendDataAsync(byte[] data)
        {
            if (!_isConnected || _stream == null) return;
            
            try
            {
                // Boyutu 4 byte prefix olarak ekle
                byte[] lengthBytes = BitConverter.GetBytes(data.Length);
                if (BitConverter.IsLittleEndian) Array.Reverse(lengthBytes);

                await _stream.WriteAsync(lengthBytes, 0, 4);
                await _stream.WriteAsync(data, 0, data.Length);
                await _stream.FlushAsync();
            }
            catch
            {
                await DisconnectAsync();
            }
        }
    }
}