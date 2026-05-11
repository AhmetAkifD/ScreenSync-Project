using ScreenSync.Desktop.Services;
using ScreenSync.Desktop.Tools;
using ScreenSync.Desktop.User_Controls;
using System.Windows;

namespace ScreenSync.Desktop
{
    public partial class MainWindow : Window
    {
        private MainWindowTools _tools;
        private SyncManager _syncManager;
        private DeviceBoxes _activeDeviceBox;
        
        private const int VIDEO_PORT = 50000;
        internal bool IsPopupOpen = false;
        internal bool IsUserWantsToSee = false;

        public MainWindow()
        {
            InitializeComponent();
            
            _tools = new MainWindowTools(this);
            _syncManager = new SyncManager();
            _activeDeviceBox = _tools.CreateAndAttachDeviceBox("Galaxy A56", "127.0.0.1 (USB)");
            
            SubscribeToEvents();
            
            this.Loaded += (s, e) => _tools.OpenLogConsole();
            this.Closed += (s, e) => _syncManager.StopAll();
        }

        private void SubscribeToEvents()
        {
            _syncManager.OnStatusChanged += (msg) => _tools.SetSystemStatus(msg, System.Windows.Media.Brushes.Orange);
            _syncManager.OnDeviceReady += (deviceName) => _tools.SetDeviceReadyState(deviceName, _activeDeviceBox);
            _syncManager.OnStreamRequested += () => _tools.HandleIncomingStreamRequest(_syncManager, _activeDeviceBox);
            _syncManager.OnImageDecoded += (img) => 
            {
                if (IsUserWantsToSee) _tools.ShowDecodedImage(img);
            };
            _syncManager.OnStreamStopped += () => 
            {
                _tools.SetDisconnectedState(_activeDeviceBox);
                IsUserWantsToSee = false;
                LogService.Info("Yayın durduruldu ve arayüz sıfırlandı.");
            };
        }

        private void BtnListenPort_Click(object sender, RoutedEventArgs e)
        {
            if (_tools.SetupAdbPortForwarding())
            {
                _ = _syncManager.StartCommandServer(VIDEO_PORT);

                BtnListenPort.IsEnabled = false;
                BtnListenPort.Content = "Tünel Açık";
                _activeDeviceBox.SetConnection(DeviceBoxes.ConnectionType.Usb);
                
                LogService.Info($"Komut sunucusu {VIDEO_PORT} portunda dinlenmeye başlandı.");
            }
        }

        private void BtnShowStream_Click(object sender, RoutedEventArgs e)
        {
            IsUserWantsToSee = true;
            _tools.SetStreamActiveState(_activeDeviceBox);
            LogService.Info("Kullanıcı akışı izlemeye başladı.");
        }
    }
}