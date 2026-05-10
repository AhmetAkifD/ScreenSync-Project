using ScreenSync.Network;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace ScreenSync.Desktop
{
    public class SyncManager
    {
        private TcpServer _tcpServer;
        private FFmpegDecoder _decoder;

        // KOMUT KANALI İÇİN YENİ NESNELER
        private TcpListener _commandListener;
        private NetworkStream _commandStream;

        // Arayüze fırlatacağımız olaylar
        public event Action<string> OnStatusChanged;
        public event Action<WriteableBitmap> OnImageDecoded;
        public event Action OnStreamStopped;

        // YENİ OLAYLAR (El Sıkışma İçin)
        public event Action<string> OnDeviceReady; // Cihaz bağlandığında (Yeşil Işık)
        public event Action OnStreamRequested; // Telefondan istek geldiğinde (Popup)

        private bool _isFirstFrame = true;

        public SyncManager()
        {
            _decoder = new FFmpegDecoder();
            _tcpServer = new TcpServer(); // Bu artık sadece 50001 (Video) için kullanılacak

            _tcpServer.OnDisconnected += () => OnStreamStopped?.Invoke();
            _tcpServer.OnError += (err) => OnStatusChanged?.Invoke($"Video Hatası: {err}");

            _tcpServer.OnFrameReceived += (frameData) =>
            {
                var image = _decoder.DecodeFrame(frameData);
                if (image != null) OnImageDecoded?.Invoke(image);
            };
        }

        // 1. ADIM: Sadece Komut Kanalını Dinlemeye Başla
        public async Task StartCommandServer(int port)
        {
            try
            {
                _commandListener = new TcpListener(IPAddress.Any, port);
                _commandListener.Start();
                OnStatusChanged?.Invoke($"Komut kanalı {port} portunda dinleniyor...");

                while (true)
                {
                    var client = await _commandListener.AcceptTcpClientAsync();
                    _commandStream = client.GetStream();
                    _ = Task.Run(ListenForCommands); // Arka planda dinlemeye başla
                }
            }
            catch { /* Hata yönetimi */ }
        }

        // 2. ADIM: Gelen Komutları Oku ve Olay Fırlat
        private async Task ListenForCommands()
        {
            try
            {
                using var reader = new StreamReader(_commandStream, System.Text.Encoding.UTF8, leaveOpen: true);
                while (true)
                {
                    string line = await reader.ReadLineAsync();
                    if (line == null) break;

                    if (line.StartsWith("HELO|"))
                    {
                        string deviceName = line.Split('|')[1];
                        OnDeviceReady?.Invoke(deviceName);
                    }
                    else if (line == "REQ_STREAM")
                    {
                        OnStreamRequested?.Invoke();
                    }
                }
            }
            catch { OnStreamStopped?.Invoke(); }
        }

        // 3. ADIM: PC'den Onay Verildiğinde Telefondaki Yayını Tetikle
        public void ApproveStream()
        {
            if (_commandStream != null)
            {
                Debug.WriteLine("[C#] Telefona ONAY (APPROVE_STREAM) gönderiliyor...");

                // DÜZELTME BURADA: "new System.Text.UTF8Encoding(false)" kullanarak o görünmez BOM karakterini kapatıyoruz!
                var writer = new StreamWriter(_commandStream, new System.Text.UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
                writer.WriteLine("APPROVE_STREAM");

                _isFirstFrame = true;
                _tcpServer.Stop();

                Debug.WriteLine("[C#] 50001 (Video) portu dinlenmeye başlandı!");
                _ = _tcpServer.StartListeningAsync(50001);
            }
        }

        public void RejectStream()
        {
            if (_commandStream != null)
            {
                Debug.WriteLine("[C#] Telefona RED (REJECT_STREAM) gönderiliyor...");

                // BOM'u burada da kapatıyoruz
                var writer = new StreamWriter(_commandStream, new System.Text.UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
                writer.WriteLine("REJECT_STREAM");
            }
        }

        public void StopAll()
        {
            _commandListener?.Stop();
            _tcpServer?.Stop();
        }
    }
}