using ScreenSync.Desktop.Network;
using ScreenSync.Desktop.Services;
using ScreenSync.Desktop.Tools;
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
        private SyncManagerTools _tools;
        private AudioReceiver _audioReceiver;

        // Komut Kanalı Nesneleri
        private TcpListener _commandListener;
        private NetworkStream _commandStream;

        // Dışarıya Açılan Olaylar (Events)
        public event Action<string> OnStatusChanged;
        public event Action<WriteableBitmap> OnImageDecoded;
        public event Action OnStreamStopped;
        public event Action<string> OnDeviceReady; 
        public event Action OnStreamRequested; 

        public SyncManager()
        {
            _tools = new SyncManagerTools(this);
            InitializeVideoComponents();
            _audioReceiver = new AudioReceiver();
        }

        private void InitializeVideoComponents()
        {
            _tcpServer?.Stop();

            var newServer = new TcpServer();
            var newDecoder = new FFmpegDecoder();

            newServer.OnDisconnected += () => { if (_tcpServer == newServer) TriggerStreamStopped(); };
            newServer.OnError += (err) => { if (_tcpServer == newServer) TriggerStatusChanged($"Video Hatası: {err}"); };
            newServer.OnFrameReceived += (frameData) =>
            {
                if (_tcpServer == newServer)
                {
                    var image = newDecoder.DecodeFrame(frameData);
                    if (image != null) OnImageDecoded?.Invoke(image);
                }
            };

            _tcpServer = newServer;
            _decoder = newDecoder;
        }

        // --- 1. AĞ DİNLEME (LISTENER) METOTLARI ---

        public async Task StartCommandServer(int port)
        {
            try
            {
                _commandListener?.Stop();
                _commandListener = new TcpListener(IPAddress.Any, port);
                _commandListener.Start();
                TriggerStatusChanged($"{port} portu dinleniyor...");

                while (true)
                {
                    var client = await _commandListener.AcceptTcpClientAsync();
                    _commandStream = client.GetStream();
            
                    LogService.Info("Komut kanalına yeni bir bağlantı kabul edildi.");
                    _ = Task.Run(ListenForCommandsAsync); 
                }
            }
            catch (Exception ex)
            {
                LogService.Error($"Komut sunucusu başlatılamadı: {ex.Message}");
            }
        }

        private async Task ListenForCommandsAsync()
        {
            try
            {
                using var reader = new StreamReader(_commandStream, System.Text.Encoding.UTF8, leaveOpen: true);
                while (true)
                {
                    string line = await reader.ReadLineAsync();
                    if (line == null) 
                    {
                        LogService.Info("Komut bağlantısı koptu (Gelen veri null).");
                        break;
                    }

                    // İşlemeyi Tools sınıfına devret
                    _tools.ProcessIncomingCommand(line);
                }
            }
            catch (Exception ex)
            {
                LogService.Error($"Komut dinleme döngüsünde hata: {ex.Message}");
                TriggerStreamStopped();
            }
        }

        // --- 2. DIŞARIYA AÇIK YÖNETİM METOTLARI ---

        public void ApproveStream()
        {
            _tools.SendCommandToDevice(_commandStream, "APPROVE_STREAM");
            InitializeVideoComponents();
            LogService.Info("50001 (Video) portu tertemiz bir şekilde dinlenmeye başlandı!");
            _ = _tcpServer.StartListeningAsync(50001);
            _audioReceiver.Start();
        }

        public void RejectStream()
        {
            _tools.SendCommandToDevice(_commandStream, "REJECT_STREAM");
        }

        // Kırmızı buton için durdurma yetkisi
        public void RequestStopStream()
        {
            _tools.SendCommandToDevice(_commandStream, "STOP_STREAM");
            _tcpServer?.Stop();
            _audioReceiver?.Stop();
        }

        public void StopAll()
        {
            _commandListener?.Stop();
            _tcpServer?.Stop();
            LogService.Info("Tüm sunucular ve soketler kapatıldı.");
        }

        // --- 3. İÇ TETİKLEYİCİLER (INTERNAL TRIGGERS) ---
        // Tools sınıfının ana sınıftaki (SyncManager) olayları tetikleyebilmesi için

        internal void TriggerDeviceReady(string deviceName) => OnDeviceReady?.Invoke(deviceName);
        internal void TriggerStreamRequested() => OnStreamRequested?.Invoke();
        internal void TriggerStreamStopped() => OnStreamStopped?.Invoke();
        internal void TriggerStatusChanged(string message) => OnStatusChanged?.Invoke(message);
    }
}