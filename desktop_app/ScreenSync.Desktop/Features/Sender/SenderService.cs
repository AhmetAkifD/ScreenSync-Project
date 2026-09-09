using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using ScreenSync.Network.Transport;

namespace ScreenSync.Desktop.Features.Sender
{
    public class SenderService
    {
        private readonly ITransport _transport;
        private bool _isCapturing;
        private CancellationTokenSource _cts;

        public SenderService(ITransport transport)
        {
            _transport = transport;
        }

        public async Task StartCaptureAsync()
        {
            if (_isCapturing) return;
            
            // Transport bağlanmasını bekle
            await _transport.ConnectAsync();
            
            _isCapturing = true;
            _cts = new CancellationTokenSource();
            
            _ = Task.Run(() => CaptureLoop(_cts.Token));
        }

        private async Task CaptureLoop(CancellationToken token)
        {
            // Ana ekranın çözünürlüğünü al
            Rectangle bounds = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
            
            while (_isCapturing && !token.IsCancellationRequested)
            {
                try
                {
                    using (Bitmap bitmap = new Bitmap(bounds.Width, bounds.Height))
                    {
                        using (Graphics g = Graphics.FromImage(bitmap))
                        {
                            g.CopyFromScreen(Point.Empty, Point.Empty, bounds.Size);
                        }

                        using (MemoryStream ms = new MemoryStream())
                        {
                            // JPEG olarak kaydet, kalite ve boyut dengesi
                            bitmap.Save(ms, ImageFormat.Jpeg);
                            byte[] imageBytes = ms.ToArray();

                            // Ağ üzerinden gönder
                            await _transport.SendDataAsync(imageBytes);
                        }
                    }

                    // Saniyede ~30 kare (33ms)
                    await Task.Delay(33, token);
                }
                catch
                {
                    break;
                }
            }
        }

        public async Task StopCaptureAsync()
        {
            _isCapturing = false;
            _cts?.Cancel();
            await _transport.DisconnectAsync();
        }
    }
}