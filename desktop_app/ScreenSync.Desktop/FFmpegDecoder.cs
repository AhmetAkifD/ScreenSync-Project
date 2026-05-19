using System;
using System.Diagnostics;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FFmpeg.AutoGen;
using ScreenSync.Desktop.Services;
namespace ScreenSync.Desktop
{
    public unsafe class FFmpegDecoder
    {
        private AVCodec* _codec;
        private AVCodecContext* _codecContext;
        private AVFrame* _frame;
        private AVPacket* _packet;
        private SwsContext* _swsContext;

        // Çözünürlük takibi için
        private int _lastWidth = 0;
        private int _lastHeight = 0;

        public FFmpegDecoder()
        {
            ffmpeg.RootPath = AppDomain.CurrentDomain.BaseDirectory;

            _codec = ffmpeg.avcodec_find_decoder(AVCodecID.AV_CODEC_ID_H264);
            if (_codec == null) throw new Exception("H264 Codec bulunamadı!");

            _codecContext = ffmpeg.avcodec_alloc_context3(_codec);
            ffmpeg.avcodec_open2(_codecContext, _codec, null);

            _frame = ffmpeg.av_frame_alloc();
            _packet = ffmpeg.av_packet_alloc();
        }

        public WriteableBitmap DecodeFrame(byte[] frameData)
        {
            WriteableBitmap bitmap = null;

            try
            {
                fixed (byte* pData = frameData)
                {
                    _packet->data = pData;
                    _packet->size = frameData.Length;

                    int sendRes = ffmpeg.avcodec_send_packet(_codecContext, _packet);

                    if (sendRes != 0)
                    {
                        LogService.Info($"[FFMPEG-HATA] avcodec_send_packet başarısız oldu. Hata Kodu: {sendRes}");
                        return null;
                    }

                    int receiveRes = ffmpeg.avcodec_receive_frame(_codecContext, _frame);

                    if (receiveRes == 0)
                    {
                        int width = _frame->width;
                        int height = _frame->height;

                        if (_swsContext == null || width != _lastWidth || height != _lastHeight)
                        {
                            LogService.Info($"[FFMPEG-BİLGİ] Çözünürlük Yenileniyor! Eski: {_lastWidth}x{_lastHeight} -> Yeni: {width}x{height}");

                            if (_swsContext != null) ffmpeg.sws_freeContext(_swsContext);

                            _swsContext = ffmpeg.sws_getContext(
                                width, height, (AVPixelFormat)_frame->format,
                                width, height, AVPixelFormat.AV_PIX_FMT_BGRA,
                                1, null, null, null);

                            _lastWidth = width;
                            _lastHeight = height;
                        }

                        // UI İşlemlerinde çökme var mı diye kontrol ediyoruz
                        App.Current.Dispatcher.Invoke(() =>
                        {
                            try
                            {
                                bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
                                bitmap.Lock();

                                byte_ptrArray4 dstData = new byte_ptrArray4();
                                dstData[0] = (byte*)bitmap.BackBuffer;
                                int_array4 dstLinesize = new int_array4();
                                dstLinesize[0] = bitmap.BackBufferStride;

                                ffmpeg.sws_scale(_swsContext, _frame->data, _frame->linesize, 0, height, dstData, dstLinesize);

                                bitmap.AddDirtyRect(new System.Windows.Int32Rect(0, 0, width, height));
                                bitmap.Unlock();
                            }
                            catch (Exception ex)
                            {
                                LogService.Info($"[FFMPEG-UI-HATA] Görüntü ekrana basılırken çöktü: {ex.Message}");
                            }
                        });
                    }
                    // EAGAIN (daha fazla veri lazım) veya EOF (bitti) dışındaki tüm garip hataları logla
                    else if (receiveRes != ffmpeg.AVERROR(ffmpeg.EAGAIN) && receiveRes != ffmpeg.AVERROR_EOF)
                    {
                        LogService.Info($"[FFMPEG-HATA] avcodec_receive_frame başarısız oldu. Hata Kodu: {receiveRes}");
                    }
                }
            }
            catch (Exception ex)
            {
                LogService.Info($"[FFMPEG-CRITICAL] DecodeFrame metodu içinde ölümcül bir çöküş yaşandı!\nMesaj: {ex.Message}\nStack: {ex.StackTrace}");
            }

            return bitmap;
        }
    }
}