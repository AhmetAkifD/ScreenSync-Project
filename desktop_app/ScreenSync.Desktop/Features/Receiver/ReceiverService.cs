using System;
using ScreenSync.Network.Transport;

namespace ScreenSync.Desktop.Features.Receiver
{
    public class ReceiverService
    {
        private readonly ITransport _transport;

        public ReceiverService(ITransport transport)
        {
            _transport = transport;
            _transport.OnDataReceived += OnDataReceived;
        }

        private void OnDataReceived(object sender, byte[] data)
        {
            // TODO: Decode data and render to UI (ScreenWindow)
        }
    }
}