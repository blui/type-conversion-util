namespace FileConversionApi;

/// <summary>
/// Compile-time constants that must agree across every consumer in the pipeline.
/// Members are grouped by concern; each nested class carries its own summary.
/// </summary>
public static class Constants
{
    /// <summary>
    /// Tunables for the text-to-PDF wrapper.
    /// </summary>
    public static class TextProcessing
    {
        public const int DefaultLineLength = 80;
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

        /// <summary>
        /// The single authoritative input-&gt;targets conversion matrix. This is the one source of
        /// truth consumed by InputValidator (validation), DocumentService (handler cross-check), and
        /// ConversionController (supported-formats projection); no consumer keeps a private copy.
        /// </summary>
        public static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> ConversionMatrix =
            new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
            {
                ["doc"] = Set("pdf", "txt", "docx", "html", "htm"),
                ["docx"] = Set("pdf", "txt", "doc", "html", "htm"),
                ["pdf"] = Set("docx", "doc", "txt"),
                ["xlsx"] = Set("csv", "pdf"),
                ["csv"] = Set("xlsx"),
                ["pptx"] = Set("pdf"),
                ["txt"] = Set("pdf", "docx", "doc"),
                ["xml"] = Set("pdf"),
                ["html"] = Set("pdf"),
                ["htm"] = Set("pdf")
            };

        /// <summary>
        /// Builds a case-insensitive read-only set of target formats for the conversion matrix.
        /// </summary>
        private static IReadOnlySet<string> Set(params string[] targets) =>
            new HashSet<string>(targets, StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Security header name constants
    /// </summary>
    public static class SecurityHeaders
    {
        public const string XContentTypeOptions = "X-Content-Type-Options";
        public const string XFrameOptions = "X-Frame-Options";
        public const string ReferrerPolicy = "Referrer-Policy";
        public const string ContentSecurityPolicy = "Content-Security-Policy";
        public const string NoSniff = "nosniff";
    }

    /// <summary>
    /// API endpoint paths
    /// </summary>
    public static class ApiPaths
    {
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

