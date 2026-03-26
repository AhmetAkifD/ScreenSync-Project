using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace ScreenSync.Network
{
    public class TcpReceiver
    {
        // WPF tarafına "Yeni bir kare geldi" veya "Hata çıktı" diye bağıracağımız olaylar (Events)
        public event Action<byte[]> OnFrameReceived;
        public event Action<string> OnError;

        private TcpListener _tcpListener;
        private bool _isReceiving = false;

        public async Task StartAsync(int port)
        {
            try
            {
                _tcpListener = new TcpListener(IPAddress.Any, port);
                _tcpListener.Start();
                System.Diagnostics.Debug.WriteLine($"[TCP] {port} portunda dinleniyor...");

                TcpClient client = await _tcpListener.AcceptTcpClientAsync();
                System.Diagnostics.Debug.WriteLine("[TCP] Cihaz bağlandı! Veri akışı başlıyor.");

                NetworkStream stream = client.GetStream();
                _isReceiving = true;

                while (_isReceiving)
                {
                    // 1. Başlığı (4 Byte Boyut) Oku
                    byte[] headerBytes = new byte[4];
                    int headerRead = await stream.ReadAsync(headerBytes, 0, 4);
                    if (headerRead == 0) break;

                    if (BitConverter.IsLittleEndian)
                        Array.Reverse(headerBytes);

                    int frameSize = BitConverter.ToInt32(headerBytes, 0);

                    // 2. Asıl Veriyi Oku
                    byte[] frameData = new byte[frameSize];
                    int totalRead = 0;

                    while (totalRead < frameSize)
                    {
                        int read = await stream.ReadAsync(frameData, totalRead, frameSize - totalRead);
                        if (read == 0) throw new Exception("Bağlantı koptu.");
                        totalRead += read;
                    }

                    // 3. Veriyi Yakaladık! Arayüz projesine fırlatıyoruz
                    OnFrameReceived?.Invoke(frameData);
                }
            }
            catch (Exception ex)
            {
                OnError?.Invoke(ex.Message);
            }
        }

        public void Stop()
        {
            _isReceiving = false;
            _tcpListener?.Stop();
        }
    }
}