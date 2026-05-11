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

                    try
                    {
                        using NetworkStream stream = client.GetStream();
                        while (_isReceiving && client.Connected)
                        {
                            byte[] headerBytes = new byte[4];
                            int headerRead = await stream.ReadAsync(headerBytes, 0, 4);
                            if (headerRead < 4) break;

                            if (BitConverter.IsLittleEndian) Array.Reverse(headerBytes);
                            int frameSize = BitConverter.ToInt32(headerBytes, 0);

                            byte[] frameData = new byte[frameSize];
                            int totalRead = 0;

                            while (totalRead < frameSize)
                            {
                                int read = await stream.ReadAsync(frameData, totalRead, frameSize - totalRead);
                                if (read == 0) throw new Exception("Veri akışı kesildi.");
                                totalRead += read;
                            }

                            OnFrameReceived?.Invoke(frameData);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (_isReceiving && !ex.Message.Contains("iptal") && !ex.Message.Contains("aborted"))
                        {
                            OnError?.Invoke($"Sunucu hatası: {ex.Message}");
                        }
                    }
                    finally
                    {
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