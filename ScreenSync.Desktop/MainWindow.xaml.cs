using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Policy;
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
using Windows.Devices.WiFiDirect;
using static System.Net.Mime.MediaTypeNames;

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
        private UdpVideoStream _videoStream;
        private bool _isListening = false;
        private TcpListener _tcpListener;
        private bool _isReceiving = false;
        private FFmpegDecoder _decoder;
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

        private async Task StartTcpReceiver()
        {
            try
            {
                // 1. Portu dinlemeye başlıyoruz (VLC'nin yaptığı işi artık biz yapıyoruz)
                _tcpListener = new TcpListener(IPAddress.Any, 50000);
                _tcpListener.Start();
                System.Diagnostics.Debug.WriteLine("[TCP] 50000 portunda telefon bekleniyor...");
                _decoder = new FFmpegDecoder();

                // 2. Telefonun bağlantı isteğini kabul et
                TcpClient client = await _tcpListener.AcceptTcpClientAsync();
                System.Diagnostics.Debug.WriteLine("[TCP] TELEFON BAĞLANDI! Veri tüneli açıldı.");

                NetworkStream stream = client.GetStream();
                _isReceiving = true;

                // 3. Kusursuz Veri Okuma Döngüsü (Scrcpy Ayrıştırıcısı)
                while (_isReceiving)
                {
                    // ADIM A: Sadece 4 Byte (Başlık) Oku
                    byte[] headerBytes = new byte[4];
                    int headerRead = await stream.ReadAsync(headerBytes, 0, 4);

                    if (headerRead == 0) break; // Telefon bağlantıyı kesti

                    // KOTLIN (Big-Endian) -> C# (Little-Endian) Çevirisi
                    if (BitConverter.IsLittleEndian)
                    {
                        Array.Reverse(headerBytes);
                    }

                    // İşte paketin gerçek boyutu!
                    int frameSize = BitConverter.ToInt32(headerBytes, 0);

                    // ADIM B: Başlıktan öğrendiğimiz boyut kadar asıl H.264 verisini oku
                    byte[] frameData = new byte[frameSize];
                    int totalRead = 0;

                    // TCP paketi yolda parçalasa bile (örn: yarısı önce, yarısı sonra gelse)
                    // tamamını alana kadar döngüde bekleyip veriyi eksiksiz birleştiriyoruz.
                    while (totalRead < frameSize)
                    {
                        int read = await stream.ReadAsync(frameData, totalRead, frameSize - totalRead);
                        if (read == 0) throw new Exception("Veri okunurken bağlantı koptu.");
                        totalRead += read;
                    }

                    // Test için logluyoruz. EMSGSIZE hataları tarih oldu mu görelim.
                    System.Diagnostics.Debug.WriteLine($"[TCP DECODER] Eksiksiz Kare Yakalandı -> Boyut: {frameSize} byte");

                    // ADIM C: Asıl görüntü çözme (FFmpeg) işlemi buraya gelecek
                    // DecodeAndRenderFrame(frameData);
                    WriteableBitmap image = _decoder.DecodeFrame(frameData);
                    if (image != null)
                    {
                        Dispatcher.Invoke(() => {
                            ScreenViewer.Source = image; // Görüntüyü ekrana bas!
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[TCP HATASI] {ex.Message}");
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
                _ = StartTcpReceiver();

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