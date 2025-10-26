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

    /// <summary>
    /// File handling constants
    /// </summary>
    public static class FileHandling
    {
        /// <summary>
        /// Maximum allowed filename length in characters
        /// </summary>
        public const int MaxFileNameLength = 255;

        /// <summary>
        /// Maximum sanitized filename length to avoid filesystem issues
        /// </summary>
        public const int MaxSanitizedFileNameLength = 200;

        /// <summary>
        /// Maximum file extension length including the dot (e.g., ".docx")
        /// </summary>
        public const int MaxExtensionLength = 50;
    }

    /// <summary>
    /// Security header name constants
    /// </summary>
    public static class SecurityHeaders
    {
        public const string XContentTypeOptions = "X-Content-Type-Options";
        public const string XFrameOptions = "X-Frame-Options";
        public const string XXssProtection = "X-XSS-Protection";
        public const string ReferrerPolicy = "Referrer-Policy";
        public const string ContentSecurityPolicy = "Content-Security-Policy";
        public const string NoSniff = "nosniff";
    }

    /// <summary>
    /// API endpoint paths
    /// </summary>
    public static class ApiPaths
    {
        public const string SwaggerJson = "/swagger/v1/swagger.json";
        public const string ApiDocs = "api-docs";
    }

    /// <summary>
    /// HTTP methods
    /// </summary>
    public static class HttpMethods
    {
        public const string Get = "GET";
        public const string Post = "POST";
    }

    /// <summary>
    /// Configuration validation constants
    /// </summary>
    public static class ConfigValidation
    {
        /// <summary>
        /// Maximum value for file size configuration (2GB)
        /// </summary>
        public const long MaxFileSize = int.MaxValue;
    }
}

