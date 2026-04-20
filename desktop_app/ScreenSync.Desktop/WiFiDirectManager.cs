using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Windows.Devices.WiFiDirect;

public class WiFiDirectManager
{
    private WiFiDirectAdvertisementPublisher _publisher;
    private WiFiDirectConnectionListener _listener;
    public WiFiDirectDevice ConnectedDevice { get; private set; }

    public event Action<string> OnStatusChanged;
    public event Action<WiFiDirectConnectionRequestedEventArgs> OnConnectionRequested;

    public void StartHost()
    {
        _listener = new WiFiDirectConnectionListener();
        _listener.ConnectionRequested += (s, e) => OnConnectionRequested?.Invoke(e);

        _publisher = new WiFiDirectAdvertisementPublisher();
        _publisher.Advertisement.IsAutonomousGroupOwnerEnabled = true; // PC Daima Patron
        _publisher.Start();

        OnStatusChanged?.Invoke("Yayın Başlatıldı. PC Görünür.");
    }

    public async Task<bool> AcceptConnection(WiFiDirectConnectionRequest request)
    {
        try
        {
            ConnectedDevice = await WiFiDirectDevice.FromIdAsync(request.DeviceInformation.Id);
            _publisher.Stop(); // Bağlanınca yayını durdur
            return true;
        }
        catch { return false; }
    }

    public void Reset()
    {
        ConnectedDevice?.Dispose();
        ConnectedDevice = null;
        _publisher?.Start();
    }
}