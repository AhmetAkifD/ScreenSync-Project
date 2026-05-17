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
        public void UpdateFrame(BitmapSource frame)
        {
            Dispatcher.Invoke(() =>
            {
                ScreenViewer.Source = frame;

                // Çözünürlük deðiþti mi kontrolü (Ýlk açýlýþ veya telefonun yan çevrilmesi)
                if (_lastVideoWidth != frame.PixelWidth || _lastVideoHeight != frame.PixelHeight)
                {
                    _lastVideoWidth = frame.PixelWidth;
                    _lastVideoHeight = frame.PixelHeight;

                    AdjustWindowSize(_lastVideoWidth, _lastVideoHeight);
                }
            });
        }

        private void AdjustWindowSize(double videoWidth, double videoHeight)
        {
            // Genel boyutu küçülttük: Ekranýn max %60'ýný kullanacak þekilde güvenli sýnýr çekiyoruz
            double maxAllowedHeight = SystemParameters.WorkArea.Height * 0.60;
            double maxAllowedWidth = SystemParameters.WorkArea.Width * 0.60;

            double ratio = videoWidth / videoHeight;

            double targetWidth;
            double targetHeight;

            // 1. DURUM: DÝKEY MOD (Telefon dik tutulurken)
            if (videoHeight > videoWidth)
            {
                targetHeight = maxAllowedHeight;
                targetWidth = targetHeight * ratio;

                // Eðer geniþlik sýnýrý aþýlýrsa orantýyý koruyarak geniþliðe göre küçült
                if (targetWidth > maxAllowedWidth)
                {
                    targetWidth = maxAllowedWidth;
                    targetHeight = targetWidth / ratio;
                }
            }
            // 2. DURUM: YATAY MOD (Telefon yan çevrildiðinde veya oyun/video açýldýðýnda)
            else
            {
                targetWidth = maxAllowedWidth;
                targetHeight = targetWidth / ratio;

                // Eðer yükseklik sýnýrý aþýlýrsa orantýyý koruyarak yüksekliðe göre küçült
                if (targetHeight > maxAllowedHeight)
                {
                    targetHeight = maxAllowedHeight;
                    targetWidth = targetHeight * ratio;
                }
            }

            // Hesaplanan milimetrik boyutlarý doðrudan resme basýyoruz
            ScreenViewer.Width = targetWidth;
            ScreenViewer.Height = targetHeight;
        }
    }
}