using ScreenSync.Desktop.Tools;
using ScreenSync.Desktop.User_Controls;
using System.Diagnostics;
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
        private bool _isPopupOpen = false;

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

            _syncManager.OnDeviceReady += (deviceName) => {
                Dispatcher.Invoke(() => {
                    StatusLight.Fill = Brushes.Yellow; // Bekleme durumu
                    TxtLightStatus.Text = $"{deviceName} Bağlandı, Yayın Bekleniyor...";
                    _activeDeviceBox.SetStatus(DeviceStatus.Ready); // Kutuyu Yeşil Yap
                });
            };

            _syncManager.OnStreamRequested += () => {
                if (_isPopupOpen) return;

                Dispatcher.Invoke(() => {
                    _isPopupOpen = true;

                    Debug.WriteLine("[C#] Ekrana yayın isteği Popup'ı çıkarıldı.");
                    var result = MessageBox.Show(
                        "Galaxy A56 cihazı ekranını paylaşmak istiyor. Onaylıyor musunuz?",
                        "Gelen Yayın İsteği",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Information
                    );

                    _isPopupOpen = false;

                    if (result == MessageBoxResult.Yes)
                    {
                        Debug.WriteLine("[C#] Kullanıcı popup'ta EVET'e bastı.");
                        _syncManager.ApproveStream();
                        _activeDeviceBox.SetStatus(DeviceStatus.Streaming);
                        _isUserWantsToSee = true;

                        StatusLight.Fill = Brushes.Green;
                        TxtLightStatus.Text = "Yayın Aktif!";
                    }
                    else
                    {
                        // İŞTE EKSİK OLAN KISIM BURASIYDI
                        Debug.WriteLine("[C#] Kullanıcı popup'ta HAYIR'a bastı.");
                        _syncManager.RejectStream();
                    }
                });
            };

            this.Closed += (s, e) => _syncManager.StopAll();
        }

        private void BtnListenPort_Click(object sender, RoutedEventArgs e)
        {
            if (_mainWindowTools.SetupAdbReverse())
            {
                // ARTIK VİDEO DEĞİL, SADECE KOMUT KANALINI (50000) AÇIYORUZ
                _ = _syncManager.StartCommandServer(50000);

                BtnListenPort.IsEnabled = false;
                BtnListenPort.Content = "Tünel Açık";
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