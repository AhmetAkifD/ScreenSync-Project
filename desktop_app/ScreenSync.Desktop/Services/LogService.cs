using System;
using System.Collections.ObjectModel;
using System.Windows;

namespace ScreenSync.Desktop.Services
{
    public static class LogService
    {
        // ObservableCollection, içine veri eklendiğinde UI'ı otomatik günceller
        public static ObservableCollection<string> Logs { get; } = new ObservableCollection<string>();

        public static void Info(string message) => AddLog("INFO", message);
        public static void Error(string message) => AddLog("ERROR", message);

        private static void AddLog(string level, string message)
        {
            // UI Thread'e güvenli bir şekilde veri eklemek için Application.Current.Dispatcher kullanılır
            Application.Current.Dispatcher.Invoke(() =>
            {
                string logEntry = $"[{DateTime.Now:HH:mm:ss}] [{level}] {message}";
                Logs.Add(logEntry);
            });
        }
    }
}