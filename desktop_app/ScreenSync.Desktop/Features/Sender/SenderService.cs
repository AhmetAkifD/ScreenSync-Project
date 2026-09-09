using System.Threading.Tasks;
using ScreenSync.Network.Transport;

namespace ScreenSync.Desktop.Features.Sender
{
    public class SenderService
    {
        private readonly ITransport _transport;

        public SenderService(ITransport transport)
        {
            _transport = transport;
        }

        public Task StartCaptureAsync()
        {
            // TODO: Implement desktop capture and send over _transport
            return Task.CompletedTask;
        }

        public Task StopCaptureAsync()
        {
            return Task.CompletedTask;
        }
    }
}