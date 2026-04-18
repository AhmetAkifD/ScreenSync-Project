using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using Windows.Devices.WiFiDirect;
using ScreenSync.Network;

namespace ScreenSync.Desktop
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private ScreenWindow? _screenWindow;
        private WiFiDirectAdvertisementPublisher _publisher;
        private WiFiDirectConnectionListener _listener;
        private WiFiDirectDevice _connectedDevice;

        private const int VIDEO_PORT = 50000;

        // Ağ ve Görüntü Çözücü Sınıflarımız
        private FFmpegDecoder _decoder;
        private TcpReceiver _tcpReceiver;

        public MainWindow()
        {
            InitializeComponent();
        }

        private void BtnStartDiscovery_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _listener = new WiFiDirectConnectionListener();
                _listener.ConnectionRequested += OnConnectionRequested;

                _publisher = new WiFiDirectAdvertisementPublisher();
                _publisher.Advertisement.ListenStateDiscoverability = WiFiDirectAdvertisementListenStateDiscoverability.Normal;
                _publisher.Advertisement.IsAutonomousGroupOwnerEnabled = true;

                _publisher.StatusChanged += (s, args) => {
                    Debug.WriteLine($"[WiFiDirect] Durum: {args.Status}");

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
                StatusText.Text = "Cihaz aranıyor...";
                StatusText.Foreground = Brushes.Orange;

                Debug.WriteLine("[WiFiDirect] Yayın başlatıldı.");
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Hata oluştu: {ex.Message}");
            }
        }

        private void StartNetworkReceiver()
        {
            _decoder = new FFmpegDecoder();
            _tcpReceiver = new TcpReceiver();

            _tcpReceiver.OnError += (msg) => Debug.WriteLine($"[AĞ HATASI] {msg}");

            _tcpReceiver.OnDisconnected += () =>
            {
                Dispatcher.Invoke(() => {
                    Debug.WriteLine("[AĞ] Cihaz ekran paylaşımını durdurdu (TCP Bağlantısı kapandı).");
                    if (_screenWindow != null)
                    {
                        _screenWindow.Close();
                        _screenWindow = null;
                    }
                    
                    if (_connectedDevice != null) 
                    {
                        StatusText.Text = "Cihaz bağlandı. Ekran paylaşımı bekleniyor...";
                        StatusText.Foreground = Brushes.LightGreen;
                    }
                });
            };

            _tcpReceiver.OnFrameReceived += (frameData) =>
            {
                var image = _decoder.DecodeFrame(frameData);
                if (image != null)
                {
                    Dispatcher.Invoke(() => {
                        if (_screenWindow == null)
                        {
                            _screenWindow = new ScreenWindow();
                            _screenWindow.Closed += (s, ev) => _screenWindow = null;
                            _screenWindow.Show();
                            
                            StatusText.Text = "Ekran aktarılıyor...";
                            StatusText.Foreground = Brushes.Cyan;
                        }

                        if (_screenWindow != null)
                        {
                            _screenWindow.ScreenViewer.Source = image;
                        }
                    });
                }
            };

            // Port sabitimizi buraya bağladık
            _ = _tcpReceiver.StartAsync(VIDEO_PORT);
        }

        private async void OnConnectionRequested(WiFiDirectConnectionListener sender, WiFiDirectConnectionRequestedEventArgs args)
        {
            var request = args.GetConnectionRequest();

            if (_connectedDevice != null)
            {
                Debug.WriteLine($"[GÜVENLİK] {_connectedDevice.DeviceId} zaten bağlı. Yeni gelen bağlantı isteği ({request.DeviceInformation.Name}) reddedildi.");
                return;
            }

            Debug.WriteLine($"[WiFiDirect] KAPIDA BİRİ VAR: {request.DeviceInformation.Name}");

            try
            {
                _connectedDevice = await WiFiDirectDevice.FromIdAsync(request.DeviceInformation.Id);

                // YENİ: Cihazın fiziksel olarak kopup kopmadığını dinleyen kulak
                // YENİ: İkinci parametreye 'args' dedik ve durumu 'sender.ConnectionStatus' üzerinden okuyoruz
                _connectedDevice.ConnectionStatusChanged += (sender, args) =>
                {
                    if (sender.ConnectionStatus == WiFiDirectConnectionStatus.Disconnected)
                    {
                        Dispatcher.Invoke(() => {
                            Debug.WriteLine("[WiFiDirect] Cihaz bağlantısı koptu. Kapı tekrar açılıyor.");
                            _connectedDevice?.Dispose();
                            _connectedDevice = null;

                            if (_screenWindow != null)
                            {
                                _screenWindow.Close();
                                _screenWindow = null;
                            }

                            _tcpReceiver?.Stop();

                            // Kapıyı tekrar yayına açıyoruz ki bir daha bağlanılabilsin
                            if (_publisher.Status != WiFiDirectAdvertisementPublisherStatus.Started)
                                _publisher.Start();

                            BtnStartDiscovery.Content = "Yayınlanıyor... (PC Görünür)";
                            StatusText.Text = "Ekran Paylaşımı Bekleniyor...";
                            StatusText.Foreground = Brushes.LightGray;
                        });
                    }
                };

                if (_publisher != null)
                {
                    _publisher.Stop();
                }

                StartNetworkReceiver();

                Dispatcher.Invoke(() => {
                    BtnStartDiscovery.Content = "Bağlanıldı";
                    StatusText.Text = "Cihaz bağlandı. Ekran paylaşımı bekleniyor...";
                    StatusText.Foreground = Brushes.LightGreen;
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[WiFiDirect] Bağlantı reddedildi veya hata: {ex.Message}");
                _connectedDevice = null;
            }
        }
    }
}