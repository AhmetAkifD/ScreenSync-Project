using System;
using System.Threading.Tasks;

namespace ScreenSync.Network.Transport
{
    public interface ITransport
    {
        Task ConnectAsync();
        Task DisconnectAsync();
        event EventHandler<byte[]> OnDataReceived;
        Task SendDataAsync(byte[] data);
    }
}