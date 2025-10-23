namespace FileConversionApi;

/// <summary>
/// Application-wide constants
/// </summary>
public static class Constants
{
    /// <summary>
    /// File size limits
    /// </summary>
    public static class FileSizeLimits
    {
        public const long DefaultMaxFileSize = 52428800; // 50MB in bytes
        public const int OneMegabyte = 1024 * 1024;
    }

    /// <summary>
    /// Text processing constants
    /// </summary>
    public static class TextProcessing
    {
        public const int DefaultLineLength = 80;
        public const int DefaultFontSize = 12;
    }

    /// <summary>
    /// Error tracking constants
    /// </summary>
    public static class ErrorTracking
    {
        public const int MaxRecentErrors = 100;
        public const int ErrorHistoryRetentionMinutes = 60;
    }

    /// <summary>
    /// Concurrency limits
    /// </summary>
    public static class Concurrency
    {
        public const int DefaultFileAccessSemaphoreLimit = 5;
        public const int DefaultConversionSemaphoreLimit = 2;
    }

    /// <summary>
    /// HTTP constants
    /// </summary>
    public static class Http
    {
        public const string ApplicationJson = "application/json";
        public const string ApplicationPdf = "application/pdf";
        public const string ApplicationOctetStream = "application/octet-stream";
    }
}
