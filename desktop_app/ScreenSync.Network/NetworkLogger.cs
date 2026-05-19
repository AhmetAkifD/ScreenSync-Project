public static class NetworkLogger
{
    // Dinlemek isteyenler için olaylar (Events)
    public static event Action<string> OnLogInfo;
    public static event Action<string> OnLogError;

    // Network içinden log basmak için kullanılacak metotlar
    public static void Info(string message) => OnLogInfo?.Invoke(message);
    public static void Error(string message) => OnLogError?.Invoke(message);
}