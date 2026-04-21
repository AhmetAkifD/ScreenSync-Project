using ScreenSync.Desktop.Tools;
using ScreenSync.Desktop.User_Controls;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static ScreenSync.Desktop.User_Controls.DeviceBoxes;

namespace ScreenSync.Desktop
{
    public partial class MainWindow : Window
    {
        private ScreenWindow? _screenWindow;
        private MainWindowTools _mainWindowTools;
        private SyncManager _syncManager;
        private const int VIDEO_PORT = 50000;
        private bool _isUserWantsToSee = false; // Kullanıcı "Yayını Başlat" dedi mi?
        // Ekranda yöneteceğimiz aktif cihaz kutusu
        private DeviceBoxes _activeDeviceBox;

        public MainWindow()
        {
            InitializeComponent();
            _syncManager = new SyncManager();
            _mainWindowTools = new MainWindowTools(this);

            // 1. KUTUYU OLUŞTUR
            _activeDeviceBox = new DeviceBoxes("Galaxy A56", "127.0.0.1 (USB)");

            // Başlangıç durumlarını set ediyoruz
            _activeDeviceBox.SetStatus(DeviceStatus.Disconnected);
            _activeDeviceBox.SetConnection(ConnectionType.Usb); // Başlangıçta USB varsayıyoruz

            PanelActiveDevices.Children.Add(_activeDeviceBox);

            // Olayları dinle
            _syncManager.OnStatusChanged += _mainWindowTools.UpdateStatus;
            _syncManager.OnStreamStopped += HandleStreamStopped;

            _syncManager.OnImageDecoded += (img) => {
                if (_isUserWantsToSee) _mainWindowTools.DisplayImage(img);
            };

            // VERİ GELDİĞİ AN (LED'ler burada güncelleniyor)
            _syncManager.OnFirstDataDetected += () => {
                Dispatcher.Invoke(() => {
                    StatusLight.Fill = Brushes.Green;
                    TxtLightStatus.Text = "Veri Akışı Sağlandı!";
                    BtnShowStream.IsEnabled = true;

                    // Kutu LED'i: Artık cihaz "Ready" (Hazır) konumunda
                    _activeDeviceBox.SetStatus(DeviceStatus.Ready);
                });
            };

            this.Closed += (s, e) => _syncManager.StopAll();
        }

        private void BtnListenPort_Click(object sender, RoutedEventArgs e)
        {
            if (_mainWindowTools.SetupAdbReverse())
            {
                _syncManager.StartServer(VIDEO_PORT);
                BtnListenPort.IsEnabled = false;
                BtnListenPort.Content = "Port Dinleniyor...";

                // ADB Reverse başarılıysa kesinlikle USB üzerindeyiz
                _activeDeviceBox.SetConnection(ConnectionType.Usb);
            }
        }

        private void BtnShowStream_Click(object sender, RoutedEventArgs e)
        {
            _isUserWantsToSee = true;
            BtnShowStream.IsEnabled = false;
            BtnShowStream.Content = "Yayın Aktif";

            // Kutu LED'i: Yayın başladığı için "Streaming" moduna geçiyoruz
            _activeDeviceBox.SetStatus(DeviceStatus.Streaming);
        }

        private void HandleStreamStopped()
        {
            Dispatcher.Invoke(() => {
                _isUserWantsToSee = false;

                StatusLight.Fill = Brushes.Red;
                TxtLightStatus.Text = "Bağlantı Koptu";
                BtnShowStream.IsEnabled = false;
                BtnListenPort.IsEnabled = true;
                BtnListenPort.Content = "Portu Dinlemeye Başla";

                // Kutu LED'i: Bağlantı koptuğu için "Disconnected" durumuna geri dön
                _activeDeviceBox.SetStatus(DeviceStatus.Disconnected);

                _mainWindowTools.HandleStreamStopped();
            });
        }


    }
}