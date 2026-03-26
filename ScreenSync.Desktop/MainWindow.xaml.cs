using System;
using System.Diagnostics;
using System.Windows;
using Windows.Devices.WiFiDirect;
using ScreenSync.Network;

namespace ScreenSync.Desktop
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
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

            _tcpReceiver.OnFrameReceived += (frameData) =>
            {
                var image = _decoder.DecodeFrame(frameData);
                if (image != null)
                {
                    Dispatcher.Invoke(() => {
                        ScreenViewer.Source = image;
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

                if (_publisher != null)
                {
                    _publisher.Stop();
                }

                // Telefon bağlandığı an ağ dinleyicisini ve görüntü çözücüyü başlat
                StartNetworkReceiver();

                Dispatcher.Invoke(() => {
                    BtnStartDiscovery.Content = "Bağlanıldı (Korumalı Mod)";
                    MessageBox.Show($"{request.DeviceInformation.Name} bağlandı. Ekran aktarımı başlıyor!", "Sistem Hazır");
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