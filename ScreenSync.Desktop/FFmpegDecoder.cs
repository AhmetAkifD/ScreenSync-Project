using System;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using FFmpeg.AutoGen;

namespace ScreenSync.Desktop
{
    public unsafe class FFmpegDecoder
    {
        private AVCodec* _codec;
        private AVCodecContext* _codecContext;
        private AVFrame* _frame;
        private AVFrame* _rgbaFrame;
        private AVPacket* _packet;
        private SwsContext* _swsContext;

        public FFmpegDecoder()
        {
            // YENİ: FFmpeg'e DLL'lerin nerede olduğunu açıkça gösteriyoruz (EXE'nin olduğu klasör)
            ffmpeg.RootPath = AppDomain.CurrentDomain.BaseDirectory;
            
            // Eski avdevice_register_all() satırını sildik çünkü hem artık desteklenmiyor hem de bize sadece H264 Codec lazım.

            _codec = ffmpeg.avcodec_find_decoder(AVCodecID.AV_CODEC_ID_H264);
            if (_codec == null) throw new Exception("H264 Codec bulunamadı!");

            _codecContext = ffmpeg.avcodec_alloc_context3(_codec);
            ffmpeg.avcodec_open2(_codecContext, _codec, null);

            _frame = ffmpeg.av_frame_alloc();
            _rgbaFrame = ffmpeg.av_frame_alloc();
            _packet = ffmpeg.av_packet_alloc();
        }

        public WriteableBitmap DecodeFrame(byte[] frameData)
        {
            WriteableBitmap bitmap = null;

            fixed (byte* pData = frameData)
            {
                _packet->data = pData;
                _packet->size = frameData.Length;

                // 1. Paketi çözücüye (Decoder) gönder
                if (ffmpeg.avcodec_send_packet(_codecContext, _packet) == 0)
                {
                    // 2. Çözülmüş resmi (Frame) al
                    if (ffmpeg.avcodec_receive_frame(_codecContext, _frame) == 0)
                    {
                        int width = _frame->width;
                        int height = _frame->height;

                        // İlk kare geldiğinde renk dönüştürücüyü (YUV -> BGRA) ayarla
                        if (_swsContext == null)
                        {
                            _swsContext = ffmpeg.sws_getContext(
                                width, height, (AVPixelFormat)_frame->format,
                                width, height, AVPixelFormat.AV_PIX_FMT_BGRA,
                                1, null, null, null); // 1 = SWS_FAST_BILINEAR
                        }

                        // WPF'in anlayacağı resim formatını (WriteableBitmap) hazırla
                        App.Current.Dispatcher.Invoke(() =>
                        {
                            bitmap = new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgra32, null);
                            bitmap.Lock();

                            byte_ptrArray4 dstData = new byte_ptrArray4();
                            dstData[0] = (byte*)bitmap.BackBuffer;
                            int_array4 dstLinesize = new int_array4();
                            dstLinesize[0] = bitmap.BackBufferStride;

                            // 3. Çözülen kareyi WPF resmine kopyala
                            ffmpeg.sws_scale(_swsContext, _frame->data, _frame->linesize, 0, height, dstData, dstLinesize);

                            bitmap.AddDirtyRect(new System.Windows.Int32Rect(0, 0, width, height));
                            bitmap.Unlock();
                        });
                    }
                }
            }
            return bitmap;
        }
    }
}