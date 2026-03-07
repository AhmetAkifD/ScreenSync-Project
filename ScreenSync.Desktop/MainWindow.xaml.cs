using System.Security.Policy;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Windows.Devices.WiFiDirect;
using System.Diagnostics;

namespace ScreenSync.Desktop
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private WiFiDirectAdvertisementPublisher _publisher;
        private WiFiDirectConnectionListener _listener; // Bağlantıları dinleyecek nesne
        private WiFiDirectDevice _connectedDevice;      // Bağlanan cihazı tutacağımız nesne
        public MainWindow()
        {
            InitializeComponent();
        }
        private void BtnStartDiscovery_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 1. Kapıcıyı (Listener) oluştur ve kapı çaldığında ne yapacağını söyle
                _listener = new WiFiDirectConnectionListener();
                _listener.ConnectionRequested += OnConnectionRequested;

                // 2. Yayıncı ayarları
                _publisher = new WiFiDirectAdvertisementPublisher();
                _publisher.Advertisement.ListenStateDiscoverability = WiFiDirectAdvertisementListenStateDiscoverability.Normal;
                _publisher.Advertisement.IsAutonomousGroupOwnerEnabled = true;

                _publisher.StatusChanged += (s, args) => {
                    // Durumu hem Visual Studio Output'a hem de UI'a basalım
                    Debug.WriteLine($"[WiFiDirect] Durum: {args.Status}");

                    // UI thread'ine erişmek için Dispatcher kullanıyoruz
                    Dispatcher.Invoke(() => {
                        if (args.Status == WiFiDirectAdvertisementPublisherStatus.Aborted)
                        {
                            MessageBox.Show("Yayın durduruldu. Lütfen Wi-Fi'ın açık olduğundan emin olun.");
                        }
                    });
                };

                _publisher.Start();

                BtnStartDiscovery.Content = "Yayınlanıyor... (PC Görünür)";
                BtnStartDiscovery.IsEnabled = false;

                Debug.WriteLine("[WiFiDirect] Yayın başlatıldı.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hata oluştu: {ex.Message}");
            }
        }
        // Telefon bağlantı isteği gönderdiğinde burası tetiklenecek
        private async void OnConnectionRequested(WiFiDirectConnectionListener sender, WiFiDirectConnectionRequestedEventArgs args)
        {
            var request = args.GetConnectionRequest();
            Debug.WriteLine($"[WiFiDirect] KAPIDA BİRİ VAR: {request.DeviceInformation.Name}");

            try
            {
                // Bağlantıyı kabul et ve cihazı içeri al
                _connectedDevice = await WiFiDirectDevice.FromIdAsync(request.DeviceInformation.Id);

                Dispatcher.Invoke(() => {
                    MessageBox.Show($"{request.DeviceInformation.Name} başarıyla bağlandı!", "Bağlantı Kuruldu");
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WiFiDirect] Bağlantı reddedildi veya hata: {ex.Message}");
            }
        }
    }
}