using System.Security.Policy;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Windows.Devices.WiFiDirect;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using LibVLCSharp.Shared;
using System.IO;

namespace ScreenSync.Desktop
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private WiFiDirectAdvertisementPublisher _publisher;
        private WiFiDirectConnectionListener _listener; // Bağlantıları dinleyecek nesne
        private WiFiDirectDevice _connectedDevice;      // Bağlanan cihazı tutacağımız nesne
        private UdpClient _udpServer;
        private const int VIDEO_PORT = 50000; // Paketleri göndereceğimiz özel port
        private LibVLC _libVLC;
        private LibVLCSharp.Shared.MediaPlayer _mediaPlayer;
        private UdpVideoStream _videoStream;
        private bool _isListening = false;
        public MainWindow()
        {
            InitializeComponent();
        }

        private void BtnStartDiscovery_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 1. Kapıcıyı (Listener) oluştur ve kapı çaldığında ne yapacağını söyle
                _listener = new WiFiDirectConnectionListener();
                _listener.ConnectionRequested += OnConnectionRequested;

                // 2. Yayıncı ayarları
                _publisher = new WiFiDirectAdvertisementPublisher();
                _publisher.Advertisement.ListenStateDiscoverability = WiFiDirectAdvertisementListenStateDiscoverability.Normal;
                _publisher.Advertisement.IsAutonomousGroupOwnerEnabled = true;

                _publisher.StatusChanged += (s, args) => {
                    // Durumu hem Visual Studio Output'a hem de UI'a basalım
                    Debug.WriteLine($"[WiFiDirect] Durum: {args.Status}");

                    // UI thread'ine erişmek için Dispatcher kullanıyoruz
                    Dispatcher.Invoke(() => {
                        if (args.Status == WiFiDirectAdvertisementPublisherStatus.Aborted)
                        {
                            MessageBox.Show("Yayın durduruldu. Lütfen Wi-Fi'ın açık olduğundan emin olun.");
                        }
                    });
                };

                _publisher.Start();

                BtnStartDiscovery.Content = "Yayınlanıyor... (PC Görünür)";
                BtnStartDiscovery.IsEnabled = false;

                Debug.WriteLine("[WiFiDirect] Yayın başlatıldı.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hata oluştu: {ex.Message}");
            }
        }

        // Telefon bağlantı isteği gönderdiğinde burası tetiklenecek
        private async void OnConnectionRequested(WiFiDirectConnectionListener sender, WiFiDirectConnectionRequestedEventArgs args)
        {
            if (_connectedDevice != null)
            {
                System.Diagnostics.Debug.WriteLine($"[GÜVENLİK] {_connectedDevice.DeviceId} zaten bağlı. Yeni gelen bağlantı isteği reddedildi.");
                return;
            }

            var request = args.GetConnectionRequest();
            System.Diagnostics.Debug.WriteLine($"[WiFiDirect] KAPIDA BİRİ VAR: {request.DeviceInformation.Name}");

            try
            {
                _connectedDevice = await WiFiDirectDevice.FromIdAsync(request.DeviceInformation.Id);

                if (_publisher != null)
                {
                    _publisher.Stop();
                }

                // YENİ EKLENEN KISIM: Telefon bağlandığı an VLC'yi başlat
                StartVideoPlayer();

                Dispatcher.Invoke(() => {
                    BtnStartDiscovery.Content = "Bağlanıldı (Korumalı Mod)";
                    MessageBox.Show($"{request.DeviceInformation.Name} bağlandı. Ekran aktarımı başlıyor!", "Sistem Hazır");
                });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[WiFiDirect] Bağlantı reddedildi veya hata: {ex.Message}");
                _connectedDevice = null;
            }
        }
        private void StartVideoPlayer()
        {
            // VLC motorunu ayağa kaldırıyoruz
            Core.Initialize();
            _libVLC = new LibVLC();
            _libVLC.Log += (sender, e) => {
                // Sadece hata ve uyarıları görelim ki ekran spam dolmasın (Debug mesajlarını eliyoruz)
                    System.Diagnostics.Debug.WriteLine($"[VLC LOG] {e.Level}: {e.Message}");
            };
            _mediaPlayer = new LibVLCSharp.Shared.MediaPlayer(_libVLC);

            // Arayüzdeki VideoView kontrolüne player'ı bağlıyoruz (Bunu Dispatcher içinde yapmalıyız)
            Dispatcher.Invoke(() => {
                ScreenViewer.MediaPlayer = _mediaPlayer;
            });
            _videoStream = new UdpVideoStream(50000);
            // VLC'ye udp:// linki vermek yerine doğrudan bu akışı yediriyoruz
            var mediaInput = new StreamMediaInput(_videoStream);
            var media = new Media(_libVLC, mediaInput);


            // HAYAT KURTARAN AYARLAR:
            media.AddOption(":demux=h264"); // Gelen verinin ham NAL Unit olduğunu söylüyoruz
            media.AddOption(":network-caching=300"); // Gecikmeyi (latency) minimuma indirmek için önbelleği çok küçültüyoruz
            media.AddOption(":clock-jitter=0");

            _mediaPlayer.Play(media);
            System.Diagnostics.Debug.WriteLine("[VLC] Video dinleyicisi 50000 portunda başlatıldı.");
        }
    }
    public class UdpVideoStream : Stream
    {
        private readonly UdpClient _udpClient;
        private IPEndPoint _endPoint;
        private byte[] _leftoverBuffer;
        private int _leftoverOffset;
        private int _leftoverLength;

        public UdpVideoStream(int port)
        {
            _udpClient = new UdpClient(port);
            _endPoint = new IPEndPoint(IPAddress.Any, port);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            try
            {
                int bytesCopied = 0;

                // Önceki paketten artan veri varsa, kaybetmemek için önce onu VLC'ye veriyoruz
                if (_leftoverLength > 0)
                {
                    bytesCopied = Math.Min(_leftoverLength, count);
                    Buffer.BlockCopy(_leftoverBuffer, _leftoverOffset, buffer, offset, bytesCopied);

                    _leftoverOffset += bytesCopied;
                    _leftoverLength -= bytesCopied;

                    if (_leftoverLength == 0) _leftoverBuffer = null;

                    return bytesCopied;
                }

                // Artan yoksa UDP'den yeni paket bekle (VLC bu satırda paket gelene kadar bekler)
                byte[] data = _udpClient.Receive(ref _endPoint);

                bytesCopied = Math.Min(data.Length, count);
                Buffer.BlockCopy(data, 0, buffer, offset, bytesCopied);

                // Eğer UDP'den gelen paket, VLC'nin o an istediğinden büyükse artanı saklıyoruz
                if (data.Length > count)
                {
                    _leftoverBuffer = data;
                    _leftoverOffset = count;
                    _leftoverLength = data.Length - count;
                }

                return bytesCopied;
            }
            catch
            {
                return 0; // Kapanma veya hata durumu
            }
        }

        // Stream sınıfının zorunlu ama kullanmayacağımız diğer ayarları
        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => throw new NotSupportedException();
        public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
        public override void Flush() { }
        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
        public override void SetLength(long value) => throw new NotSupportedException();
        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}