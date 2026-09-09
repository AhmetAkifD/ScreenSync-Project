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
        
        public int TargetHeight { get; set; } = 720;
        public long JpegQuality { get; set; } = 50L;
        public int TargetFps { get; set; } = 30;

        public SenderService(ITransport transport)
        {
            _transport = transport;
        }

        public async Task StartCaptureAsync()
        {
            if (_isCapturing) return;
            
            await _transport.ConnectAsync();
            
            _isCapturing = true;
            _cts = new CancellationTokenSource();
            
            _ = Task.Run(() => CaptureLoop(_cts.Token));
        }

        private ImageCodecInfo GetEncoder(ImageFormat format)
        {
            ImageCodecInfo[] codecs = ImageCodecInfo.GetImageDecoders();
            foreach (ImageCodecInfo codec in codecs)
            {
                if (codec.FormatID == format.Guid)
                    return codec;
            }
            return null;
        }

        private async Task CaptureLoop(CancellationToken token)
        {
            Rectangle bounds = System.Windows.Forms.Screen.PrimaryScreen.Bounds;
            
            // Calculate Target Width maintaining aspect ratio
            float ratio = (float)bounds.Width / bounds.Height;
            int targetWidth = (int)(TargetHeight * ratio);

            ImageCodecInfo jpegEncoder = GetEncoder(ImageFormat.Jpeg);
            Encoder myEncoder = Encoder.Quality;
            EncoderParameters myEncoderParameters = new EncoderParameters(1);
            EncoderParameter myEncoderParameter = new EncoderParameter(myEncoder, JpegQuality);
            myEncoderParameters.Param[0] = myEncoderParameter;

            int delayMs = 1000 / TargetFps;

            while (_isCapturing && !token.IsCancellationRequested)
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                try
                {
                    // 1. Capture Original Screen
                    using (Bitmap original = new Bitmap(bounds.Width, bounds.Height))
                    {
                        using (Graphics gOriginal = Graphics.FromImage(original))
                        {
                            gOriginal.CopyFromScreen(Point.Empty, Point.Empty, bounds.Size);
                        }

                        // 2. Downscale and Compress
                        using (Bitmap resized = new Bitmap(original, new Size(targetWidth, TargetHeight)))
                        {
                            using (MemoryStream ms = new MemoryStream())
                            {
                                resized.Save(ms, jpegEncoder, myEncoderParameters);
                                byte[] imageBytes = ms.ToArray();
                                await _transport.SendDataAsync(imageBytes);
                            }
                        }
                    }

                    watch.Stop();
                    int timeTaken = (int)watch.ElapsedMilliseconds;
                    int sleepTime = delayMs - timeTaken;
                    
                    if (sleepTime > 0)
                    {
                        await Task.Delay(sleepTime, token);
                    }
                    else
                    {
                        // To avoid totally blocking thread
                        await Task.Delay(1, token);
                    }
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