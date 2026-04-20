using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace ScreenSync.Desktop.User_Controls
{
    /// <summary>
    /// Interaction logic for DeviceBoxes.xaml
    /// </summary>
    public partial class DeviceBoxes : UserControl
    {
        private bool _isFavorite = false;
        public event Action<string> OnStartClicked; // Cihaz IP'sini dışarı fırlatır
        public DeviceBoxes(string deviceName, string ipAddress)
        {
            InitializeComponent();
            TxtDeviceName.Text = deviceName;
            TxtIpAddress.Text = ipAddress;
        }

        

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            // Butona basıldığında bu cihazın IP'sini ana sayfaya haber veriyoruz
            OnStartClicked?.Invoke(TxtIpAddress.Text);
        }

        // Yıldız butonuna tıklandığında (Tıklanınca rengi kendi içinde değiştirir)
        private void BtnFavorite_Click(object sender, RoutedEventArgs e)
        {
            SetFavorite(!_isFavorite); // State'i tersine çevir ve rengi güncelle
            // İleride ana sayfaya "bu cihaz favori yapıldı" haberi uçurmak için event Action<string, bool> fırlatılabilir.
        }

        // DeviceBoxes.xaml.cs içinde
        public void UpdateStatus(bool isConnected, bool isReady, bool isStreaming)
        {
            // Durum LED'leri
            LightDisconnected.Opacity = isConnected ? 0.2 : 1.0;
            LightReady.Opacity = isReady ? 1.0 : 0.2;
            LightStreaming.Opacity = isStreaming ? 1.0 : 0.2;
        }

        public void SetConnectionType(bool isUsb)
        {
            // Bağlantı LED'leri
            LightUsb.Opacity = isUsb ? 1.0 : 0.2;
            LightWifi.Opacity = !isUsb ? 1.0 : 0.2;
        }
        
        // Favori durumunu dışarıdan ayarlayan metot (Encapsulation Master)
        public void SetFavorite(bool isFavorite)
        {
            _isFavorite = isFavorite;
            // Yıldız rengini güncelle (Favoriyse altın sarısı, değilse sönük gri)
            IconFavorite.Fill = isFavorite ? new SolidColorBrush(Colors.Gold) : new SolidColorBrush(Color.FromRgb(85, 85, 85));
            // Tooltip'i güncelle
            BtnFavorite.ToolTip = isFavorite ? "Favorilerden Çıkar" : "Favorilere Ekle";
        }
    }
}
