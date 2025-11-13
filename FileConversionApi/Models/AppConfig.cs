using System.Collections.Generic;

namespace FileConversionApi.Models;

/// <summary>
/// File handling configuration.
/// </summary>
public class FileHandlingConfig
{
    /// <summary>
    /// Maximum file size in bytes. Default: 50 MB.
    /// </summary>
    public long MaxFileSize { get; set; } = 52428800;

    /// <summary>
    /// Upload directory. Supports relative or absolute paths.
    /// </summary>
    public string TempDirectory { get; set; } = "App_Data\\temp\\uploads";

    /// <summary>
    /// Converted file output directory. Supports relative or absolute paths.
    /// </summary>
    public string OutputDirectory { get; set; } = "App_Data\\temp\\converted";

    /// <summary>
    /// Allowed file extensions without dot (e.g., "pdf", "docx").
    /// </summary>
    public List<string> AllowedExtensions { get; set; } = new();
}

/// <summary>
/// Security configuration.
/// </summary>
public class SecurityConfig
{
    /// <summary>
    /// CORS allowed origins. Example: "http://localhost:3000"
    /// </summary>
    public List<string> AllowedOrigins { get; set; } = new();

    /// <summary>
    /// Enable API key authentication. When enabled, all requests except /health require X-API-Key header.
    /// </summary>
    public bool RequireApiKey { get; set; } = false;

    /// <summary>
    /// Valid API keys. Use format: apikey_live_[32+ random characters]. Multiple keys enable rotation without downtime.
    /// </summary>
    public List<string> ApiKeys { get; set; } = new();
}

/// <summary>
/// HTTP security headers configuration.
/// </summary>
public class SecurityHeadersConfig
{
    /// <summary>
    /// Enable X-Content-Type-Options: nosniff to prevent MIME-type sniffing attacks.
    /// </summary>
    public bool NoSniff { get; set; } = true;

    /// <summary>
    /// Referrer-Policy header value. Controls referrer information sent with requests.
    /// </summary>
    public string ReferrerPolicy { get; set; } = "strict-origin-when-cross-origin";

    /// <summary>
    /// X-Frame-Options header value. Prevents clickjacking via iframe embedding control.
    /// </summary>
    public string FrameOptions { get; set; } = "DENY";

    /// <summary>
    /// X-XSS-Protection header value. Enables browser XSS filters.
    /// </summary>
    public string XssProtection { get; set; } = "1; mode=block";

    /// <summary>
    /// Content-Security-Policy header value. Restricts resource loading to prevent XSS.
    /// </summary>
    public string ContentSecurityPolicy { get; set; } = "default-src 'self'";
}

/// <summary>
/// Concurrency control configuration.
/// </summary>
public class ConcurrencyConfig
{
    /// <summary>
    /// Maximum concurrent conversions. Higher values increase throughput but consume more resources.
    /// </summary>
    public int MaxConcurrentConversions { get; set; } = 2;

    /// <summary>
    /// Maximum queued requests. Excess requests return 503 Service Unavailable.
    /// </summary>
    public int MaxQueueSize { get; set; } = 10;
}

/// <summary>
/// LibreOffice configuration.
/// </summary>
public class LibreOfficeConfig
{
    /// <summary>
    /// Path to soffice.exe. Leave empty for automatic resolution (bundled, then system).
    /// </summary>
    public string ExecutablePath { get; set; } = "";

    /// <summary>
    /// Conversion timeout in seconds.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 300;
}
