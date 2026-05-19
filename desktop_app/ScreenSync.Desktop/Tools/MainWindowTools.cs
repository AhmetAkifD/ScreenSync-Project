using ScreenSync.Desktop.Services;
using ScreenSync.Desktop.User_Controls;
using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static ScreenSync.Desktop.User_Controls.DeviceBoxes;

namespace ScreenSync.Desktop.Tools
{
    internal class MainWindowTools
    {
        private MainWindow _mainWindow;
        private ScreenWindow? _screenWindow;

        public MainWindowTools(MainWindow mainWindow)
        {
            _mainWindow = mainWindow;
        }

        // --- 1. ARAYÜZ (UI) KURULUM METOTLARI ---

        public DeviceBoxes CreateAndAttachDeviceBox(string deviceName, string ipAddress)
        {
            var deviceBox = new DeviceBoxes(deviceName, ipAddress);
            deviceBox.SetStatus(DeviceStatus.Disconnected);
            deviceBox.SetConnection(ConnectionType.Usb);
            
            _mainWindow.PanelActiveDevices.Children.Add(deviceBox);
            return deviceBox;
        }

        public void OpenLogConsole()
        {
            LogWindow logWindow = new LogWindow();
            logWindow.DataContext = new { Logs = LogService.Logs };
            logWindow.Show();
            
            // Başlangıçta ana pencerenin arkasında kalmaması için
            logWindow.Activate(); 
            LogService.Info("WPF Arayüzü yüklendi ve Log ekranı başlatıldı.");
        }

        // --- 2. DURUM GÜNCELLEME METOTLARI ---

        public void SetSystemStatus(string message, Brush lightColor)
        {
            _mainWindow.Dispatcher.Invoke(() => {
                _mainWindow.TxtLightStatus.Text = message;
                _mainWindow.StatusLight.Fill = lightColor;
            });
        }

        public void SetDeviceReadyState(string deviceName, DeviceBoxes activeDeviceBox)
        {
            _mainWindow.Dispatcher.Invoke(() => {
                SetSystemStatus($"{deviceName} Bağlandı, Yayın Bekleniyor...", Brushes.Yellow);
                activeDeviceBox.SetStatus(DeviceStatus.Ready);
                LogService.Info($"Cihaz el sıkışması tamamlandı: {deviceName}");
            });
        }

        public void SetStreamActiveState(DeviceBoxes activeDeviceBox)
        {
            _mainWindow.Dispatcher.Invoke(() => {
                SetSystemStatus("Yayın Aktif!", Brushes.Green);
                activeDeviceBox.SetStatus(DeviceStatus.Streaming);
                
                _mainWindow.BtnShowStream.IsEnabled = false;
                _mainWindow.BtnShowStream.Content = "Yayın Aktif";
            });
        }

        public void SetDisconnectedState(DeviceBoxes activeDeviceBox)
        {
            _mainWindow.Dispatcher.Invoke(() => {
                SetSystemStatus("Bağlantı koptu. Yeni bağlantı bekleniyor...", Brushes.Red);
                
                _mainWindow.BtnShowStream.IsEnabled = false;
                _mainWindow.BtnListenPort.IsEnabled = true;
                _mainWindow.BtnListenPort.Content = "Portu Dinlemeye Başla";
                
                activeDeviceBox.SetStatus(DeviceStatus.Disconnected);

                _screenWindow?.Close();
                _screenWindow = null;
            });
        }

        // --- 3. İŞ MANTIĞI VE VİDEO YÖNETİMİ ---

        public void HandleIncomingStreamRequest(SyncManager syncManager, DeviceBoxes activeDeviceBox)
        {
            if (_mainWindow.IsPopupOpen || _mainWindow.IsUserWantsToSee) 
            {
                LogService.Info("Zaten aktif bir yayın veya istek var. Yeni istek reddedildi.");
                return; 
            }

            _mainWindow.Dispatcher.Invoke(() => {
                _mainWindow.IsPopupOpen = true; // Değişkeni burada güncelliyoruz
                LogService.Info("Telefondan yayın isteği geldi, kullanıcı onayı bekleniyor...");

                var result = MessageBox.Show(
                    "Galaxy A56 cihazı ekranını paylaşmak istiyor. Onaylıyor musunuz?",
                    "Gelen Yayın İsteği",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Information
                );

                _mainWindow.IsPopupOpen = false;

                if (result == MessageBoxResult.Yes)
                {
                    LogService.Info("Kullanıcı yayın isteğini ONAYLADI.");
                    syncManager.ApproveStream();
                    SetStreamActiveState(activeDeviceBox);
            
                    _mainWindow.IsUserWantsToSee = true; // Yayını izleme izni verildi
                }
                else
                {
                    LogService.Error("Kullanıcı yayın isteğini REDDETTİ.");
                    syncManager.RejectStream();
                }
            });
        }

        public void ShowDecodedImage(WriteableBitmap image)
        {
            _mainWindow.Dispatcher.Invoke(() => {
                if (_screenWindow == null)
                {
                    _screenWindow = new ScreenWindow();
                    _screenWindow.Closed += (s, e) => _screenWindow = null;
                    _screenWindow.Show();

                    SetSystemStatus("Ekran Aktarılıyor...", Brushes.Cyan);
                }

                // ESKİ HALİ: _screenWindow.ScreenViewer.Source = image;
                // YENİ HALİ: Artık akıllı karşılama metodumuzu kullanıyoruz!
                _screenWindow.UpdateFrame(image);
            });
        }

        // --- 4. TERMINAL (CMD) VE AĞ YÖNETİMİ ---

        public bool SetupAdbPortForwarding()
        {
            try
            {
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string adbPath = Path.Combine(localAppData, @"Android\Sdk\platform-tools\adb.exe");
                string fileName = File.Exists(adbPath) ? adbPath : "adb";

                RunCmdCommand($"{fileName} reverse --remove-all");
                LogService.Info("Eski ADB tünelleri temizlendi.");
                
                // İki port için ProcessStartInfo kod tekrarından kurtulup yardımcı CMD metoduna gönderiyoruz
                bool isPort50000Ready = RunCmdCommand($"{fileName} reverse tcp:50000 tcp:50000");
                bool isPort50001Ready = RunCmdCommand($"{fileName} reverse tcp:50001 tcp:50001");

                if (!isPort50000Ready || !isPort50001Ready)
                {
                    return false;
                }

                LogService.Info("ADB Reverse port yönlendirmeleri CMD üzerinden başarıyla açıldı.");
                return true;
            }
            catch (Exception ex)
            {
                LogService.Error($"ADB komutu çalıştırılamadı. Hata: {ex.Message}");
                MessageBox.Show($"ADB komutu çalıştırılamadı. Android SDK yüklü mü?\nHata: {ex.Message}",
                                "Sistem Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }

        private bool RunCmdCommand(string command)
        {
            ProcessStartInfo processInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/c {command}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using (Process process = Process.Start(processInfo))
            {
                process.WaitForExit();
                if (process.ExitCode != 0)
                {
                    string error = process.StandardError.ReadToEnd();
                    LogService.Error($"CMD Hatası: {error}");
                    MessageBox.Show($"Terminal Hatası: {error}", "Bağlantı Kurulamadı", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }
            return true;
        }
        
        public void PlayFadeInAnimation(UIElement element)
        {
            var fadeIn = new System.Windows.Media.Animation.DoubleAnimation
            {
                From = 0,
                To = 1,
                Duration = TimeSpan.FromMilliseconds(500)
            };
            element.BeginAnimation(UIElement.OpacityProperty, fadeIn);
        }
        
        public void MoveToHistory(DeviceBoxes card)
        {
            _mainWindow.Dispatcher.Invoke(() => {
                // Aktif panelden çıkar
                if (_mainWindow.PanelActiveDevices.Children.Contains(card))
                {
                    _mainWindow.PanelActiveDevices.Children.Remove(card);
                }

                // Eğer geçmiş panelinde zaten yoksa ekle
                if (!_mainWindow.PanelHistoryDevices.Children.Contains(card))
                {
                    _mainWindow.PanelHistoryDevices.Children.Add(card);
                }

                // Geçmişte olduğu belli olsun diye biraz karartalım (Opacity)
                card.Opacity = 0.6;
                // Kartın içindeki butonları/etkileşimleri kapatabilirsin
                card.IsEnabled = false; 
        
                LogService.Info($"{card.TxtDeviceName} geçmiş bağlantılara taşındı.");
            });
        }

        public void MoveToActive(DeviceBoxes card)
        {
            _mainWindow.Dispatcher.Invoke(() => {
                // Geçmişten çıkar
                if (_mainWindow.PanelHistoryDevices.Children.Contains(card))
                {
                    _mainWindow.PanelHistoryDevices.Children.Remove(card);
                }

                // Aktife geri koy
                if (!_mainWindow.PanelActiveDevices.Children.Contains(card))
                {
                    _mainWindow.PanelActiveDevices.Children.Add(card);
                }

                // Görünürlüğü ve etkileşimi geri aç
                card.Opacity = 1.0;
                card.IsEnabled = true;
        
                // Şık bir giriş animasyonu
                PlayFadeInAnimation(card);
            });
        }

        public void ToggleFavorite(DeviceBoxes card, bool isFavorite)
        {
            _mainWindow.Dispatcher.Invoke(() => {
                // 1. Önce kartı her yerden acımasızca söküyoruz (Nerede olduğunu umursamadan)
                if (_mainWindow.PanelActiveDevices.Children.Contains(card))
                    _mainWindow.PanelActiveDevices.Children.Remove(card);

                if (_mainWindow.PanelHistoryDevices.Children.Contains(card))
                    _mainWindow.PanelHistoryDevices.Children.Remove(card);

                if (_mainWindow.PanelFavorites.Children.Contains(card))
                    _mainWindow.PanelFavorites.Children.Remove(card);

                // 2. Yeni evine yerleştiriyoruz
                if (isFavorite)
                {
                    _mainWindow.PanelFavorites.Children.Add(card);
                    card.Opacity = 1.0; // Favorilerde her zaman parlak dursun
                    LogService.Info($"{card.DeviceName} favorilere eklendi.");
                }
                else
                {
                    _mainWindow.PanelHistoryDevices.Children.Add(card);
                    card.Opacity = 0.6; // Senin isteğine göre şimdilik geçmişe gidiyor (soluk)
                    LogService.Info($"{card.DeviceName} favorilerden çıkarıldı, geçmişe gönderildi.");
                }
            });
        }
    }
}