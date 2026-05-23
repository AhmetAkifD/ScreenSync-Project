using ScreenSync.Desktop.Services;
using ScreenSync.Desktop.Tools;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;

namespace ScreenSync.Desktop
{
    public partial class ScreenWindow : Window
    {
        private int _lastVideoWidth = 0;
        private int _lastVideoHeight = 0;

        public ScreenWindow()
        {
            InitializeComponent();
        }

        // Ana sayfadan (veya SyncManager'dan) gelen görüntüyü buraya basacaðýz
        public void UpdateFrame(WriteableBitmap image)
        {
            Dispatcher.Invoke(() => {
                // 1. Resmi ekrana bas
                ScreenViewer.Source = image;

                // 2. Çözünürlük deðiþimi kontrolü (Sadece ilk açýlýþta ve telefon döndüðünde çalýþýr)
                if (_lastVideoWidth != image.PixelWidth || _lastVideoHeight != image.PixelHeight)
                {
                    _lastVideoWidth = image.PixelWidth;
                    _lastVideoHeight = image.PixelHeight;

                    // 3. Ýþte þimdi o meþhur fonksiyonu çalýþtýrýyoruz!
                    AdjustWindowSize(_lastVideoWidth, _lastVideoHeight);
                }
            });
        }

        private void AdjustWindowSize(double videoWidth, double videoHeight)
        {
            // 1. Görüntünün %85'e kadar büyümesine izin veriyoruz (0.5 çok küçüktü)
            double maxAllowedHeight = SystemParameters.WorkArea.Height * 0.85;
            double maxAllowedWidth = SystemParameters.WorkArea.Width * 0.85;

            double ratio = videoWidth / videoHeight;
            double targetWidth;
            double targetHeight;

            if (videoHeight > videoWidth) // DÝKEY MOD
            {
                targetHeight = maxAllowedHeight;
                targetWidth = targetHeight * ratio;

                if (targetWidth > maxAllowedWidth)
                {
                    targetWidth = maxAllowedWidth;
                    targetHeight = targetWidth / ratio;
                }
            }
            else // YATAY MOD
            {
                targetWidth = maxAllowedWidth;
                targetHeight = targetWidth / ratio;

                if (targetHeight > maxAllowedHeight)
                {
                    targetHeight = maxAllowedHeight;
                    targetWidth = targetHeight * ratio;
                }
            }

            // 2. Resmin "sabit kalma" inatçýlýðýný kýrýyoruz (Esnek býrakýyoruz)
            ScreenViewer.Width = double.NaN;
            ScreenViewer.Height = double.NaN;

            // 3. Resim yerine direkt olarak Window'un (Pencerenin) boyutunu ayarlýyoruz!
            // +40 piksel, Windows'un üstteki baþlýk çubuðu (kapat/küçült alaný) içindir.
            this.Width = targetWidth;
            this.Height = targetHeight + 40;
        }

        private void ScreenViewer_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            LogService.Info("[MOUSE-TIK] Sol týk algýlandý. Koordinat hesaplama baþlýyor...");

            // Týklanan noktanýn hedefini ScreenViewer olarak deðiþtirdik
            Point clickPoint = e.GetPosition(ScreenViewer);
            LogService.Info($"[MOUSE-TIK] WPF Týklanan Koordinat (Ham): X={clickPoint.X}, Y={clickPoint.Y}");

            // Geniþlik ve yükseklik referanslarýný da ScreenViewer'dan alýyoruz
            double imageWidth = ScreenViewer.ActualWidth;
            double imageHeight = ScreenViewer.ActualHeight;
            LogService.Info($"[MOUSE-TIK] WPF Görüntü Boyutlarý: Geniþlik={imageWidth}, Yükseklik={imageHeight}");

            // Telefonun fiziksel çözünürlüðü (Dikey kullaným için)
            double phoneWidth = 1080;
            double phoneHeight = 2340;

            int tapX = (int)((clickPoint.X / imageWidth) * phoneWidth);
            int tapY = (int)((clickPoint.Y / imageHeight) * phoneHeight);
            LogService.Info($"[MOUSE-TIK] Android'e gönderilecek hesaplanmýþ koordinat: X={tapX}, Y={tapY}");

            Task.Run(() => {
                // 1. Týpký senin port yönlendirme metodunda yaptýðýn gibi ADB'nin tam yolunu buluyoruz
                string localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                string adbPath = System.IO.Path.Combine(localAppData, @"Android\Sdk\platform-tools\adb.exe");

                // Yolda boþluk olma ihtimaline karþý exe yolunu týrnak içine alýyoruz
                string adbExe = System.IO.File.Exists(adbPath) ? $"\"{adbPath}\"" : "adb";

                LogService.Info("[MOUSE-TIK-CMD] ADB shell input tap komutu hazýrlanýyor...");

                // 2. Komutu dinamik adb yolu ile oluþturuyoruz
                string command = $"{adbExe} shell input tap {tapX} {tapY}";

                LogService.Info($"[MOUSE-TIK-CMD] Çalýþtýrýlacak komut: {command}");

                try
                {
                    bool result = MainWindowTools.RunCmdCommand(command);

                    if (result)
                    {
                        LogService.Info("[MOUSE-TIK-CMD] Komut baþarýyla CMD'ye iletildi.");
                    }
                    else
                    {
                        LogService.Error("[MOUSE-TIK-CMD] Komut çalýþtýrýlamadý! RunCmdCommand false döndü.");
                    }
                }
                catch (Exception ex)
                {
                    LogService.Error($"[MOUSE-TIK-CMD] Kritik Hata: {ex.Message}");
                }
            });
        }
    }
}