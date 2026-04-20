using ScreenSync.Desktop.Tools;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ScreenSync.Desktop
{
    public partial class MainWindow : Window
    {
        private ScreenWindow? _screenWindow;
        private MainWindowTools _mainWindowTools;
        private SyncManager _syncManager;
        private const int VIDEO_PORT = 50000;
        private bool _isUserWantsToSee = false; // Kullanıcı "Yayını Başlat" dedi mi?

        public MainWindow()
        {
            InitializeComponent();
            _syncManager = new SyncManager();
            _mainWindowTools = new MainWindowTools(this);

            // Arka plandan gelen olayları dinliyoruz
            _syncManager.OnStatusChanged += _mainWindowTools.UpdateStatus;
            _syncManager.OnStreamStopped += HandleStreamStopped;
            // ÖNEMLİ: Görüntü gelince hemen açma, önce bir kontrol et
            _syncManager.OnImageDecoded += (img) => {
                if (_isUserWantsToSee) _mainWindowTools.DisplayImage(img);
            };
            // YENİ: Veri gelince ışığı yak ve butonu aç
            _syncManager.OnFirstDataDetected += () => {
                Dispatcher.Invoke(() => {
                    StatusLight.Fill = Brushes.Green;
                    TxtLightStatus.Text = "Veri Akışı Sağlandı!";
                    BtnShowStream.IsEnabled = true; // Artık kullanıcı yayını başlatabilir
                });
            };
            this.Closed += (s, e) => _syncManager.StopAll();
        }

        private void BtnListenPort_Click(object sender, RoutedEventArgs e)
        {
            // Önce USB köprüsünü kur, sonra server'ı aç
            if (_mainWindowTools.SetupAdbReverse())
            {
                _syncManager.StartServer(VIDEO_PORT);
                BtnListenPort.IsEnabled = false;
                BtnListenPort.Content = "Port Dinleniyor...";
            }
        }

        private void BtnShowStream_Click(object sender, RoutedEventArgs e)
        {
            _isUserWantsToSee = true;
            BtnShowStream.IsEnabled = false;
            BtnShowStream.Content = "Yayın Aktif";
        }

        private void HandleStreamStopped()
        {
            Dispatcher.Invoke(() => {
                _isUserWantsToSee = false;
                StatusLight.Fill = Brushes.Red;
                TxtLightStatus.Text = "Bağlantı Koptu";
                BtnShowStream.IsEnabled = false;
                BtnListenPort.IsEnabled = true;
                _mainWindowTools.HandleStreamStopped();
            });
        }


    }
}