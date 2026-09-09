using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ScreenSync.Network.Transport
{
    public class UdpTransport : ITransport
    {
        private UdpClient _udpServer;
        private IPEndPoint _clientEndpoint;
        private bool _isConnected;
        private const int MAX_PAYLOAD = 60000;
        private int _frameId = 0;

        public event EventHandler<byte[]> OnDataReceived;

        public async Task ConnectAsync()
        {
            _udpServer = new UdpClient(50005);
            _isConnected = true;
            
            // Wait for a hello packet from the client to know where to send video
            UdpReceiveResult result = await _udpServer.ReceiveAsync();
            _clientEndpoint = result.RemoteEndPoint;

            _ = Task.Run(ReceiveLoopAsync);
        }

        private async Task ReceiveLoopAsync()
        {
            while (_isConnected)
            {
                try
                {
                    UdpReceiveResult result = await _udpServer.ReceiveAsync();
                    // We can process incoming commands from phone here if needed
                }
                catch { break; }
            }
        }

        public Task DisconnectAsync()
        {
            _isConnected = false;
            _udpServer?.Close();
            return Task.CompletedTask;
        }

        public async Task SendDataAsync(byte[] data)
        {
            if (!_isConnected || _clientEndpoint == null) return;

            try
            {
                _frameId++;
                int totalFragments = (int)Math.Ceiling(data.Length / (double)MAX_PAYLOAD);

                for (int i = 0; i < totalFragments; i++)
                {
                    int offset = i * MAX_PAYLOAD;
                    int length = Math.Min(MAX_PAYLOAD, data.Length - offset);
                    
                    // Header: 4 bytes FrameID, 2 bytes TotalFragments, 2 bytes FragmentIndex
                    byte[] packet = new byte[8 + length];
                    byte[] frameBytes = BitConverter.GetBytes(_frameId);
                    if (BitConverter.IsLittleEndian) Array.Reverse(frameBytes);
                    
                    byte[] totalBytes = BitConverter.GetBytes((short)totalFragments);
                    if (BitConverter.IsLittleEndian) Array.Reverse(totalBytes);
                    
                    byte[] indexBytes = BitConverter.GetBytes((short)i);
                    if (BitConverter.IsLittleEndian) Array.Reverse(indexBytes);

                    Buffer.BlockCopy(frameBytes, 0, packet, 0, 4);
                    Buffer.BlockCopy(totalBytes, 0, packet, 4, 2);
                    Buffer.BlockCopy(indexBytes, 0, packet, 6, 2);
                    Buffer.BlockCopy(data, offset, packet, 8, length);

                    await _udpServer.SendAsync(packet, packet.Length, _clientEndpoint);
                }
            }
            catch
            {
                await DisconnectAsync();
            }
        }
    }
}