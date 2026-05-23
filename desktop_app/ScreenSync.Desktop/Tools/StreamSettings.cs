using ScreenSync.Desktop.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ScreenSync.Desktop.Tools
{
    public class StreamSettings
    {
        public int Bitrate { get; set; }
        public string Resolution { get; set; }

        // Singleton yapısı: Proje boyunca sadece tek bir instance yaşar
        private static StreamSettings _instance;
        public static StreamSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    // Uygulama ilk açıldığındaki varsayılan ayarlar
                    _instance = new StreamSettings { Bitrate = 4, Resolution = "720p" };
                }
                return _instance;
            }
        }

        // Ayarları güncelleyen metod
        public void UpdateSettings(int bitrate, string resolution)
        {
            Bitrate = bitrate;
            Resolution = resolution;
            LogService.Info($"[AYARLAR] Yayın ayarları güncellendi: {Resolution} / {Bitrate} Mbps");
        }
    }
}
