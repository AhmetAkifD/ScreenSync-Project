using ScreenSync.Desktop;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.IO;

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

        public void UpdateStatus(string message)
        {
            _mainWindow.Dispatcher.Invoke(() => {
                // Eski StatusText yerine yeni eklediğimiz TxtLightStatus'u kullanıyoruz
                _mainWindow.TxtLightStatus.Text = message;
                // Sistemden genel bir bilgi geldiğinde ışığı turuncu yapıyoruz (Örn: Dinleniyor...)
                _mainWindow.StatusLight.Fill = Brushes.Orange;
            });
        }

        public void HandleStreamStopped()
        {
            _mainWindow.Dispatcher.Invoke(() => {
                _mainWindow.TxtLightStatus.Text = "Bağlantı koptu. Yeni bağlantı bekleniyor...";
                // Bağlantı koptuğunda ışık anında kırmızıya dönecek
                _mainWindow.StatusLight.Fill = Brushes.Red;

                _screenWindow?.Close();
                _screenWindow = null;
            });
        }

        public void DisplayImage(WriteableBitmap image)
        {
            _mainWindow.Dispatcher.Invoke(() => {
                if (_screenWindow == null)
                {
                    _screenWindow = new ScreenWindow();
                    _screenWindow.Closed += (s, e) => _screenWindow = null;
                    _screenWindow.Show();

                    // Sen "Yayını Başlat" dediğinde ve pencere açıldığında ışık Turkuaz olacak
                    _mainWindow.TxtLightStatus.Text = "Ekran Aktarılıyor...";
                    _mainWindow.StatusLight.Fill = Brushes.Cyan;
                }
                _screenWindow.ScreenViewer.Source = image;
            });
        }

        public bool SetupAdbReverse()
        {
            try
            {
                // Bilgisayardaki kullanıcı adından (ahmet) yola çıkarak ADB'nin yerini buluyoruz
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string adbPath = Path.Combine(localAppData, @"Android\Sdk\platform-tools\adb.exe");

                // Eğer Android Studio varsayılan yere kurmadıysa, sistem "PATH" üzerinden 'adb' komutunu dener
                string fileName = File.Exists(adbPath) ? adbPath : "adb";

                // Gizli bir CMD işlemi hazırlıyoruz
                ProcessStartInfo processInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = "reverse tcp:50000 tcp:50000",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true // Siyah CMD ekranı çıkmasın!
                };

                using (Process process = Process.Start(processInfo))
                {
                    process.WaitForExit(); // Komutun bitmesini bekle
                    string error = process.StandardError.ReadToEnd();

                    if (process.ExitCode == 0)
                    {
                        // Başarılı
                        return true;
                    }
                    else
                    {
                        // Telefon takılı değilse veya USB hata ayıklama kapalıysa
                        MessageBox.Show($"ADB Hatası: Telefonun USB Hata Ayıklama modunda takılı olduğundan emin olun.\nDetay: {error}",
                                        "Bağlantı Kurulamadı", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"ADB komutu çalıştırılamadı. Android SDK yüklü mü?\nHata: {ex.Message}",
                                "Sistem Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
    }
}
