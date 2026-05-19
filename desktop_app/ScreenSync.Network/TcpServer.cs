using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
namespace ScreenSync.Network
{
    public class TcpServer
    {
        public event Action<byte[]> OnFrameReceived;
        public event Action<string> OnError;
        public event Action OnDisconnected;
        public event Action OnClientConnected;

        private TcpListener _tcpListener;
        private bool _isReceiving = false;

        public async Task StartListeningAsync(int port)
        {
            try
            {
                _tcpListener = new TcpListener(IPAddress.Any, port);
                _tcpListener.Start();
                _isReceiving = true;

                while (_isReceiving)
                {
                    TcpClient client = await _tcpListener.AcceptTcpClientAsync();
                    OnClientConnected?.Invoke();
                    NetworkLogger.Info("[TCP-BİLGİ] Yeni cihaz bağlandı, veri okuma döngüsü başlıyor...");

                    try
                    {
                        using NetworkStream stream = client.GetStream();
                        while (_isReceiving && client.Connected)
                        {
                            byte[] headerBytes = new byte[4];
                            int headerRead = await stream.ReadAsync(headerBytes, 0, 4);

                            if (headerRead < 4)
                            {
                                NetworkLogger.Info($"[TCP-KOPMA] Header (boyut bilgisi) tam okunamadı. Okunan byte: {headerRead}");
                                break;
                            }

                            if (BitConverter.IsLittleEndian) Array.Reverse(headerBytes);
                            int frameSize = BitConverter.ToInt32(headerBytes, 0);

                            // Gelen boyut mantıksızsa (Çöp veri veya kayma varsa) yakala
                            if (frameSize <= 0 || frameSize > 50000000)
                            {
                                NetworkLogger.Info($"[TCP-HATA] İmkansız/Bozuk frame boyutu alındı: {frameSize} byte. Bağlantı kesiliyor.");
                                break;
                            }

                            byte[] frameData = new byte[frameSize];
                            int totalRead = 0;

                            while (totalRead < frameSize)
                            {
                                int read = await stream.ReadAsync(frameData, totalRead, frameSize - totalRead);
                                if (read == 0)
                                {
                                    NetworkLogger.Info($"[TCP-KOPMA] Frame verisi okunurken akış aniden kesildi! Beklenen: {frameSize}, Okunan: {totalRead}");
                                    throw new Exception("Veri akışı kesildi.");
                                }
                                totalRead += read;
                            }

                            // Olayı tetiklerken çökme oluyorsa (FFmpeg vs.) onu da ayrı yakalayalım
                            try
                            {
                                OnFrameReceived?.Invoke(frameData);
                            }
                            catch (Exception ex)
                            {
                                NetworkLogger.Info($"[TCP-EVENT-HATA] Frame gönderildikten sonra üst katman (FFmpeg/UI) çöktü:\nMesaj: {ex.Message}\nStack: {ex.StackTrace}");
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        if (_isReceiving && !ex.Message.Contains("iptal") && !ex.Message.Contains("aborted"))
                        {
                            NetworkLogger.Info($"[TCP-CRITICAL] TCP Ağ Döngüsü tamamen çöktü!\nMesaj: {ex.Message}\nStack: {ex.StackTrace}");
                            OnError?.Invoke($"Sunucu hatası: {ex.Message}");
                        }
                    }
                    finally
                    {
                        NetworkLogger.Info("[TCP-BİLGİ] Client temizleniyor ve Disconnected eventi fırlatılıyor.");
                        client.Dispose();
                        OnDisconnected?.Invoke();
                    }
                }
            }
            catch (Exception ex)
            {
                if (_isReceiving) OnError?.Invoke($"Sunucu hatası: {ex.Message}");
            }
            finally
            {
                _tcpListener?.Stop();
            }
        }

        public void Stop()
        {
            _isReceiving = false;
            _tcpListener?.Stop();
        }
    }
}