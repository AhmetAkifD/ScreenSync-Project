using System;
using System.Threading.Tasks;

namespace ScreenSync.Network.Transport
{
    public class WirelessTransport : ITransport
    {
        public event EventHandler<byte[]> OnDataReceived;

        public Task ConnectAsync()
        {
            // TODO: Implement TCP/UDP/WebRTC connection
            return Task.CompletedTask;
        }

        public Task DisconnectAsync()
        {
            // TODO: Implement disconnect
            return Task.CompletedTask;
        }

        public Task SendDataAsync(byte[] data)
        {
            // TODO: Implement send
            return Task.CompletedTask;
        }
    }
}