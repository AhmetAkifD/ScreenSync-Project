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
            
            SubscribeToEvents();
            
            this.Loaded += (s, e) => _tools.OpenLogConsole();
            this.Closed += (s, e) => _syncManager.StopAll();

            NetworkLogger.OnLogInfo += (msg) => LogService.Info(msg);
            NetworkLogger.OnLogError += (msg) => LogService.Error(msg);
        }

        private void SubscribeToEvents()
        {
            _syncManager.OnStatusChanged += (msg) => _tools.SetSystemStatus(msg, System.Windows.Media.Brushes.Orange);
            _syncManager.OnDeviceReady += (deviceName) => 
            {
                Dispatcher.Invoke(() => {
                    // 1. Senaryo: Cihaz ilk defa bağlanıyor
                    if (_activeDeviceBox == null)
                    {
                        _activeDeviceBox = _tools.CreateAndAttachDeviceBox(deviceName, "127.0.0.1 (USB)");

                        // MEVCUT KOD: Favori butonunu dinleme
                        _activeDeviceBox.OnFavoriteToggled += (card, isFavorite) =>
                        {
                            _tools.ToggleFavorite(card, isFavorite);
                        };

                        // --- YENİ EKLENEN 1: KABUL ET DİNLEYİCİSİ ---
                        _activeDeviceBox.OnStreamApproved += (ip) =>
                        {
                            Dispatcher.Invoke(() =>
                            {
                                LogService.Info("Kullanıcı kart üzerinden yayın isteğini ONAYLADI.");
                                _syncManager.ApproveStream();
                                _tools.SetStreamActiveState(_activeDeviceBox);

                                IsUserWantsToSee = true;
                                IsPopupOpen = false; // Kilidi açıyoruz ki yeni istek gelebilsin
                            });
                        };

                        // --- YENİ EKLENEN 2: REDDET DİNLEYİCİSİ ---
                        _activeDeviceBox.OnStreamRejected += (ip) =>
                        {
                            Dispatcher.Invoke(() =>
                            {
                                LogService.Error("Kullanıcı kart üzerinden yayın isteğini REDDETTİ.");
                                _syncManager.RejectStream();

                                IsPopupOpen = false; // Kilidi açıyoruz
                            });
                        };

                        _tools.PlayFadeInAnimation(_activeDeviceBox);
                    }
                    // 2. Senaryo: Cihaz daha önce bağlandı ve geçmişe atıldı (Geri dönüyor)
                    else
                    {
                        _tools.MoveToActive(_activeDeviceBox);
                    }
        
                    _tools.SetDeviceReadyState(deviceName, _activeDeviceBox);
                });
            };
            _syncManager.OnStreamRequested += () => _tools.HandleIncomingStreamRequest(_syncManager, _activeDeviceBox);
            _syncManager.OnImageDecoded += (img) => 
            {
                if (IsUserWantsToSee) _tools.ShowDecodedImage(img);
            };
            _syncManager.OnStreamStopped += () => 
            {
                Dispatcher.Invoke(() => {
                    if (_activeDeviceBox != null)
                    {
                        // Yayın koptuğunda veya durduğunda kartı geçmişe gönder
                        _tools.MoveToHistory(_activeDeviceBox);
                    }
        
                    _tools.SetDisconnectedState(_activeDeviceBox);
                    IsUserWantsToSee = false;
                });
            };
        }

        private async void BtnListenPort_Click(object sender, RoutedEventArgs e)
        {
            // Yeni Mimarideki RadioButton Durumlarını Oku
            bool isWireless = RbWireless.IsChecked == true;
            bool isSender = RbRoleSender.IsChecked == true;
            bool isTargetPhone = RbTargetPhone.IsChecked == true;
            
            bool isUdp = RbProtoUDP.IsChecked == true;

            int targetHeight = 720;
            if (CmbResolution.SelectedIndex == 0) targetHeight = 1080;
            if (CmbResolution.SelectedIndex == 2) targetHeight = 480;

            long jpegQuality = (long)SldQuality.Value;

            if (isWireless)
            {
                // KABLOSUZ (Wi-Fi) BAĞLANTI
                if (isSender && isTargetPhone)
                {
                    LogService.Info($"Kablosuz PC -> Telefon yayın başlatılıyor ({(isUdp ? "UDP" : "TCP")} - 50005, {targetHeight}p, Kalite: {jpegQuality})");
                    _tools.SetSystemStatus("Wi-Fi Yayını Başladı", System.Windows.Media.Brushes.Green);
                    BtnListenPort.IsEnabled = false;

                    // Yeni Mimari Servislerini Başlat
                    ScreenSync.Network.Transport.ITransport transport;
                    if (isUdp) transport = new ScreenSync.Network.Transport.UdpTransport();
                    else transport = new ScreenSync.Network.Transport.WirelessTransport();

                    var senderService = new ScreenSync.Desktop.Features.Sender.SenderService(transport)
                    {
                        TargetHeight = targetHeight,
                        JpegQuality = jpegQuality
                    };
                    
                    await senderService.StartCaptureAsync();
                }
                else
                {
                    LogService.Info("Diğer senaryolar henüz kodlanmadı. Sadece 'Kablosuz', 'Telefon' ve 'Ekranı Paylaş' senaryosu çalışır.");
                }
            }
            else
            {
                // KABLOLU (USB / ADB) Eski Mantık
                if (_tools.SetupAdbPortForwarding())
                {
                    _ = _syncManager.StartCommandServer(VIDEO_PORT);

                    BtnListenPort.IsEnabled = false;
                    _activeDeviceBox?.SetConnection(DeviceBoxes.ConnectionType.Usb);
                    
                    LogService.Info($"{VIDEO_PORT} portu dinleniyor.");
                }
            }
        }

        private void BtnShowStream_Click(object sender, RoutedEventArgs e)
        {
            IsUserWantsToSee = true;
            if (_activeDeviceBox != null)
            {
                _tools.SetStreamActiveState(_activeDeviceBox);
            }
            LogService.Info("Kullanıcı akışı izlemeye başladı.");
        }
    }
}