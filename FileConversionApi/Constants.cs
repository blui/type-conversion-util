namespace FileConversionApi;

/// <summary>
/// Application-wide constants for configuration, limits, and standard values.
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
    /// MIME content types for file formats
    /// </summary>
    public static class ContentTypes
    {
        public const string Pdf = "application/pdf";
        public const string Doc = "application/msword";
        public const string Docx = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
        public const string Xlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
        public const string Pptx = "application/vnd.openxmlformats-officedocument.presentationml.presentation";
        public const string Txt = "text/plain";
        public const string Csv = "text/csv";
        public const string Html = "text/html";
        public const string Xml = "application/xml";
        public const string OctetStream = "application/octet-stream";
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

        /// <summary>
        /// Default fallback filename when sanitization fails
        /// </summary>
        public const string DefaultFileName = "file";
    }

    /// <summary>
    /// Supported file format constants
    /// </summary>
    public static class SupportedFormats
    {
        /// <summary>
        /// All supported file formats for upload and conversion
        /// </summary>
        public static readonly HashSet<string> All = new(StringComparer.OrdinalIgnoreCase)
        {
            "pdf", "doc", "docx", "xlsx", "pptx", "txt", "html", "htm", "csv", "xml"
        };

        /// <summary>
        /// Formats supported for LibreOffice conversion output
        /// </summary>
        public static readonly HashSet<string> ConversionTargets = new(StringComparer.OrdinalIgnoreCase)
        {
            "pdf", "doc", "docx", "txt", "html", "htm", "csv", "xlsx", "pptx"
        };
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

    /// <summary>
    /// Windows system exit codes for process diagnostics
    /// </summary>
    public static class WindowsExitCodes
    {
        /// <summary>
        /// Exit code 0xC0000135 (-1073741515): STATUS_DLL_NOT_FOUND
        /// Indicates a required DLL dependency is missing (e.g., Visual C++ Redistributable)
        /// </summary>
        public const int DllNotFound = -1073741515;
    }
}

