using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading; // Timer için gerekli

namespace ScreenSync.Desktop.User_Controls
{
    public partial class DeviceBoxes : UserControl
    {
        private bool _isFavorite = false;

        // Dışarıya fırlatılacak olaylar (Events)
        public event Action<string> OnStartClicked;
        public event Action<DeviceBoxes, bool> OnFavoriteToggled;

        // YENİ: Kabul ve Red olayları
        public event Action<string> OnStreamApproved;
        public event Action<string> OnStreamRejected;

        public string DeviceName => TxtDeviceName.Text;

        public enum DeviceStatus { Disconnected, Ready, Streaming }
        public DeviceStatus CurrentStatus { get; private set; } = DeviceStatus.Disconnected;
        public enum ConnectionType { Usb, Wifi }

        // YENİ: Meksika Dalgası (LED) için değişkenler
        private DispatcherTimer _waveTimer;
        private int _waveStep = 0;

        public DeviceBoxes(string deviceName, string ipAddress)
        {
            InitializeComponent();
            TxtDeviceName.Text = deviceName;
            TxtIpAddress.Text = ipAddress;

            // Timer Ayarları (Her 250 milisaniyede bir tetiklenir)
            _waveTimer = new DispatcherTimer();
            _waveTimer.Interval = TimeSpan.FromMilliseconds(250);
            _waveTimer.Tick += WaveTimer_Tick;
        }

        // --- YENİ EKLENEN: SİHİRLİ İSTEK MODU ---
        public void SetRequestMode(bool isRequesting)
        {
            if (isRequesting)
            {
                // 1. Arka planı eski cool gri renginde TUTUYORUZ
                MainBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#2C2F33"));

                // YENİ: Kartın çevresine parlak turuncu bir sınır (kenarlık) ekliyoruz
                MainBorder.BorderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#FF9800")); // Parlak neon turuncu
                MainBorder.BorderThickness = new Thickness(2); // Zarif ama görünür bir kalınlık

                // 2. Normal butonları gizle, Onay/Red butonlarını göster (Aynı kalıyor)
                PanelNormalActions.Visibility = Visibility.Collapsed;
                PanelRequestActions.Visibility = Visibility.Visible;

                // 3. Meksika dalgasını başlat (Aynı kalıyor)
                _waveStep = 0;
                _waveTimer.Start();
            }
            else
            {
                // YENİ: Sınırı (kenarlığı) tamamen kaldırıyoruz
                MainBorder.BorderThickness = new Thickness(0);

                // (Geri kalan temizlik işlemleri aynı kalıyor)
                PanelNormalActions.Visibility = Visibility.Visible;
                PanelRequestActions.Visibility = Visibility.Collapsed;
                _waveTimer.Stop();
                SetStatus(CurrentStatus);
            }
        }

        // Timer her tık ettiğinde LED'lerin sırayla yanıp sönmesini (Meksika Dalgası) sağlar
        private void WaveTimer_Tick(object sender, EventArgs e)
        {
            LightDisconnected.Opacity = (_waveStep == 0) ? 1.0 : 0.2;
            LightReady.Opacity = (_waveStep == 1) ? 1.0 : 0.2;
            LightStreaming.Opacity = (_waveStep == 2) ? 1.0 : 0.2;

            _waveStep++;
            if (_waveStep > 2) _waveStep = 0; // Başa sar
        }

        // --- YENİ EKLENEN: ONAY VE RED BUTON TIKLAMALARI ---
        private void BtnApprove_Click(object sender, RoutedEventArgs e)
        {
            SetRequestMode(false); // Kartı normale döndür
            OnStreamApproved?.Invoke(TxtIpAddress.Text); // Ana sayfaya "Kabul Edildi" diye bağır
        }

        private void BtnReject_Click(object sender, RoutedEventArgs e)
        {
            SetRequestMode(false); // Kartı normale döndür
            OnStreamRejected?.Invoke(TxtIpAddress.Text); // Ana sayfaya "Reddedildi" diye bağır
        }

        // Mevcut Başlat Butonu
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            OnStartClicked?.Invoke(TxtIpAddress.Text);
        }

        // Mevcut Favori Butonu
        private void BtnFavorite_Click(object sender, RoutedEventArgs e)
        {
            SetFavorite(!_isFavorite);
            OnFavoriteToggled?.Invoke(this, _isFavorite);
        }

        public void SetFavorite(bool isFavorite)
        {
            _isFavorite = isFavorite;
            IconFavorite.Fill = isFavorite ? new SolidColorBrush(Colors.Gold) : new SolidColorBrush(Color.FromRgb(85, 85, 85));
            BtnFavorite.ToolTip = isFavorite ? "Favorilerden Çıkar" : "Favorilere Ekle";
        }

        // Mevcut Durum Güncelleyici
        public void SetStatus(DeviceStatus status)
        {
            CurrentStatus = status;

            // Eğer o an İstek Modu aktifse (dalga devam ediyorsa) bu ayarlamayı yapma, dalgayı bozmasın
            if (_waveTimer.IsEnabled) return;

            LightDisconnected.Opacity = (status == DeviceStatus.Disconnected) ? 1.0 : 0.2;
            LightReady.Opacity = (status == DeviceStatus.Ready) ? 1.0 : 0.2;
            LightStreaming.Opacity = (status == DeviceStatus.Streaming) ? 1.0 : 0.2;
        }

        // Mevcut Bağlantı Güncelleyici
        public void SetConnection(ConnectionType type)
        {
            LightUsb.Opacity = (type == ConnectionType.Usb) ? 1.0 : 0.2;
            LightWifi.Opacity = (type == ConnectionType.Wifi) ? 1.0 : 0.2;
        }
    }
}