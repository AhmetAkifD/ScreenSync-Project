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

        public MainWindow()
        {
            InitializeComponent();
            _syncManager = new SyncManager();
            _mainWindowTools = new MainWindowTools(this);

            // Arka plandan gelen olayları dinliyoruz
            _syncManager.OnStatusChanged += _mainWindowTools.UpdateStatus;
            _syncManager.OnStreamStopped += _mainWindowTools.HandleStreamStopped;
            _syncManager.OnImageDecoded += _mainWindowTools.DisplayImage;

            this.Closed += (s, e) => _syncManager.StopAll();
        }

        private void BtnStartDiscovery_Click(object sender, RoutedEventArgs e)
        {
            BtnStartDiscovery.IsEnabled = false;
            _syncManager.StartServer(VIDEO_PORT);
        }

        private void BtnStartUsb_Click(object sender, RoutedEventArgs e)
        {
            // 1. Önce arka planda ADB köprüsünü kuruyoruz
            bool adbSuccess = _mainWindowTools.SetupAdbReverse();

            if (adbSuccess)
            {
                // 2. Başarılıysa sunucuyu (TcpServer) başlatıyoruz
                BtnStartUsb.IsEnabled = false;
                BtnStartDiscovery.IsEnabled = false; // İkisi aynı anda açılmasın
                _syncManager.StartServer(VIDEO_PORT);

                StatusText.Text = "USB Köprüsü Hazır. Telefondan yayını başlatın!";
                StatusText.Foreground = Brushes.Cyan;
            }
        }

        
    }
}