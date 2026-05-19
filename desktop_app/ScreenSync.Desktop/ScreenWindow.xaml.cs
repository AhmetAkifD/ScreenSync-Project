using System.Windows;
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
            // %50 küçültmek için * 0.5 yapýyoruz (Senin kodunda 1 kalmýþtý)
            double maxAllowedHeight = SystemParameters.WorkArea.Height * 0.5;
            double maxAllowedWidth = SystemParameters.WorkArea.Width * 0.5;

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

            ScreenViewer.Width = targetWidth;
            ScreenViewer.Height = targetHeight;
        }
    }
}