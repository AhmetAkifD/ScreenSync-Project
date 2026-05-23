using ScreenSync.Desktop.Services;
using System;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace ScreenSync.Desktop.Tools
{
    internal class SyncManagerTools
    {
        private SyncManager _syncManager;

        public SyncManagerTools(SyncManager syncManager)
        {
            _syncManager = syncManager;
        }

        // --- 1. KOMUT GÖNDERME (WRITER) ---
        // Tüm komutlar bu filtreden geçer, BOM (görünmez boşluk) hatası engellenir.
        public void SendCommandToDevice(NetworkStream commandStream, string command)
        {
            if (commandStream == null || !commandStream.CanWrite)
            {
                LogService.Error($"[{command}] gönderilemedi: Ağ akışı (Stream) kapalı veya geçersiz.");
                return;
            }

            try
            {
                LogService.Info($"Telefona komut gönderiliyor: {command}");
                
                // BOM karakterini kapatan UTF8 ayarı: new UTF8Encoding(false)
                var writer = new StreamWriter(commandStream, new UTF8Encoding(false), leaveOpen: true) { AutoFlush = true };
                writer.WriteLine(command);
            }
            catch (Exception ex)
            {
                LogService.Error($"Komut gönderme hatası ({command}): {ex.Message}");
            }
        }

        // --- 2. KOMUT AYRIŞTIRMA VE YÖNLENDİRME (PARSER) ---
        // Gelen string ifadeleri analiz edip ana sınıftaki olayları tetikler.
        public void ProcessIncomingCommand(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return;

            try
            {
                if (line.StartsWith("HELO|"))
                {
                    string deviceName = line.Split('|')[1];
                    LogService.Info($"Cihaz el sıkışması başarılı. Cihaz: {deviceName}");
                    _syncManager.TriggerDeviceReady(deviceName);
                }
                else if (line == "REQ_STREAM")
                {
                    LogService.Info("Cihazdan yayın izni (REQ_STREAM) geldi.");
                    _syncManager.TriggerStreamRequested();
                }
                else if (line.StartsWith("SETTINGS|")) // YENİ EKLENEN AYARLAR BLOĞU
                {
                    string[] parts = line.Split('|');
                    if (parts.Length == 3 && int.TryParse(parts[1], out int newBitrate))
                    {
                        string newResolution = parts[2];
                        // Singleton üzerinden ayarları güncelliyoruz
                        StreamSettings.Instance.UpdateSettings(newBitrate, newResolution);
                    }
                    else
                    {
                        LogService.Error("Ayarlar komutu hatalı formatta geldi.");
                    }
                }
                else if (line == "STREAM_STOPPED")
                {
                    LogService.Info("Cihaz yayını kendi durdurdu (STREAM_STOPPED).");
                    _syncManager.TriggerStreamStopped();
                }
                else
                {
                    LogService.Info($"Cihazdan bilinmeyen komut: {line}");
                }
            }
            catch (Exception ex)
            {
                LogService.Error($"Gelen komut işlenirken hata oluştu: {ex.Message}");
            }
        }
    }
}