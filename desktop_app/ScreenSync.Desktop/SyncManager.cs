using System;
using System.Windows.Media.Imaging;
using ScreenSync.Network;

namespace ScreenSync.Desktop
{
    public class SyncManager
    {
        private TcpServer _tcpServer;
        private FFmpegDecoder _decoder;

        // Arayüze fırlatacağımız olaylar
        public event Action<string> OnStatusChanged;
        public event Action<WriteableBitmap> OnImageDecoded;
        public event Action OnStreamStopped;

        public SyncManager()
        {
            _decoder = new FFmpegDecoder();
            _tcpServer = new TcpServer();

            _tcpServer.OnClientConnected += () => OnStatusChanged?.Invoke("Cihaz Bağlandı! Görüntü bekleniyor...");
            _tcpServer.OnDisconnected += () => OnStreamStopped?.Invoke();
            _tcpServer.OnError += (err) => OnStatusChanged?.Invoke($"Hata: {err}");

            // Ağdan veri gelince -> Çözücüye ver -> Çözülen resmi arayüze fırlat
            _tcpServer.OnFrameReceived += (frameData) =>
            {
                var image = _decoder.DecodeFrame(frameData);
                if (image != null)
                {
                    OnImageDecoded?.Invoke(image);
                }
            };
        }

        public void StartServer(int port)
        {
            OnStatusChanged?.Invoke($"Sunucu {port} portunda dinleniyor... (Kabloyu veya Wi-Fi'ı bağlayın)");
            _ = _tcpServer.StartListeningAsync(port);
        }

        public void StopAll()
        {
            _tcpServer?.Stop();
        }
    }
}