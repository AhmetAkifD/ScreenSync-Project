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
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

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
        private UdpClient _udpServer;
        private const int VIDEO_PORT = 50000; // Paketleri göndereceğimiz özel port
        private bool _isListening = false;
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
                _connectedDevice = await WiFiDirectDevice.FromIdAsync(request.DeviceInformation.Id);

                if (_publisher != null)
                {
                    _publisher.Stop();
                }

                // TELEFON BAĞLANDIĞI ANDA UDP SOKETİNİ AÇIYORUZ
                StartUdpListener();

                Dispatcher.Invoke(() => {
                    BtnStartDiscovery.Content = "Bağlanıldı (Korumalı Mod)";
                    MessageBox.Show($"{request.DeviceInformation.Name} başarıyla bağlandı. UDP Soketi açıldı!", "Sistem Hazır");
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WiFiDirect] Bağlantı reddedildi veya hata: {ex.Message}");
            }
        }

        private void StartUdpListener()
        {
            if (_isListening) return;

            try
            {
                // IPAddress.Any ile Wi-Fi Direct dahil tüm ağ kartlarındaki 50000 portunu dinlemeye başlıyoruz.
                _udpServer = new UdpClient(VIDEO_PORT);
                _isListening = true;

                Debug.WriteLine($"[UDP] {VIDEO_PORT} portu üzerinden dinleme başladı. Görüntü paketleri bekleniyor...");

                // UI'ı dondurmamak için sürekli dinleme işini arka plana (background task) atıyoruz
                Task.Run(async () => await ReceiveDataLoop());
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UDP] Soket açılamadı: {ex.Message}");
            }
        }

        private async Task ReceiveDataLoop()
        {
            while (_isListening)
            {
                try
                {
                    UdpReceiveResult result = await _udpServer.ReceiveAsync();
                    byte[] receivedBytes = result.Buffer;

                    // Test için gelen byte dizisini UTF-8 ile metne çeviriyoruz
                    string message = System.Text.Encoding.UTF8.GetString(receivedBytes);

                    // Gelen mesajı loglar yerine direkt arayüzdeki butonun üstüne yazdırıyoruz
                    Dispatcher.Invoke(() => {
                        LogList.Items.Add($"[{DateTime.Now:HH:mm:ss}] Gelen: {message}");

                        // Yeni veri geldikçe listenin otomatik en alta kaymasını sağlıyoruz
                        LogList.SelectedIndex = LogList.Items.Count - 1;
                        LogList.ScrollIntoView(LogList.SelectedItem);
                    });
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[UDP] Dinleme durdu veya hata: {ex.Message}");
                    break;
                }
            }
        }
    }
}