using Microsoft.VisualBasic;
using NAudio.Wave; // NetworkLogger için senin namespace'in
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace ScreenSync.Desktop.Network
{
    public class AudioReceiver
    {
        private TcpListener _listener;
        private bool _isListening;

        // NAudio'nun ses oynatıcı ve havuz nesneleri
        private WaveOutEvent _waveOut;
        private BufferedWaveProvider _waveProvider;

        public void Start()
        {
            if (_isListening) return;

            try
            {
                _isListening = true;
                _listener = new TcpListener(IPAddress.Any, 50002);
                _listener.Start();

                // Android'den göndereceğimiz standart ses formatı: 44100Hz, 16-bit, Mono (Tek kanal)
                var waveFormat = new WaveFormat(44100, 16, 1);

                _waveProvider = new BufferedWaveProvider(waveFormat)
                {
                    DiscardOnBufferOverflow = true, // Gecikme/yığılma olursa eski sesleri atıp canlı tutar
                    BufferDuration = TimeSpan.FromSeconds(2) // 2 saniyelik güvenli havuz
                };

                _waveOut = new WaveOutEvent();
                _waveOut.Init(_waveProvider);
                _waveOut.Play();

                NetworkLogger.Info("[SES] 50002 portu dinleniyor, hoparlör tüneli açıldı!");

                // Dinleme döngüsünü arka planda başlat
                _ = Task.Run(ListenForAudioAsync);
            }
            catch (Exception ex)
            {
                NetworkLogger.Error($"[SES-HATA] Ses sunucusu başlatılamadı: {ex.Message}");
            }
        }

        private async Task ListenForAudioAsync()
        {
            try
            {
                while (_isListening)
                {
                    using TcpClient client = await _listener.AcceptTcpClientAsync();
                    NetworkLogger.Info("[SES] Telefondan ses bağlantısı geldi, aktarım başlıyor!");

                    using NetworkStream stream = client.GetStream();
                    byte[] buffer = new byte[4096]; // Sesi ufak paketler halinde okuyacağız

                    while (_isListening && client.Connected)
                    {
                        int bytesRead = await stream.ReadAsync(buffer, 0, buffer.Length);
                        if (bytesRead == 0) break; // Telefon bağlantıyı kesti

                        // Gelen ham ses verisini NAudio havuzuna fırlat, o otomatik çalacak!
                        _waveProvider.AddSamples(buffer, 0, bytesRead);
                    }
                    NetworkLogger.Info("[SES] Ses akışı kesildi.");
                }
            }
            catch (Exception ex)
            {
                if (_isListening && !ex.Message.Contains("iptal") && !ex.Message.Contains("aborted"))
                {
                    NetworkLogger.Error($"[SES-TCP-HATA] Dinleme döngüsü koptu: {ex.Message}");
                }
            }
        }

        public void Stop()
        {
            _isListening = false;
            _listener?.Stop();

            // Ses kaynaklarını güvenlice temizle
            if (_waveOut != null)
            {
                _waveOut.Stop();
                _waveOut.Dispose();
                _waveOut = null;
            }
            _waveProvider?.ClearBuffer();

            NetworkLogger.Info("[SES] Ses sunucusu ve hoparlör bağlantısı kapatıldı.");
        }
    }
}