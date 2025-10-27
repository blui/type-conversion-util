using System.Collections.Generic;

namespace FileConversionApi.Models;

/// <summary>
/// File handling configuration.
/// Controls file upload limits, temporary storage locations, and allowed file types.
/// </summary>
public class FileHandlingConfig
{
    /// <summary>
    /// Maximum file size in bytes (52428800 bytes = 50 MB).
    /// Files larger than this will be rejected.
    /// </summary>
    public long MaxFileSize { get; set; } = 52428800;

    /// <summary>
    /// Directory where uploaded files are temporarily stored before conversion.
    /// Can be relative (from app root) or absolute path.
    /// </summary>
    public string TempDirectory { get; set; } = "App_Data\\temp\\uploads";

    /// <summary>
    /// Directory where converted files are temporarily stored before download.
    /// Can be relative (from app root) or absolute path.
    /// </summary>
    public string OutputDirectory { get; set; } = "App_Data\\temp\\converted";

    /// <summary>
    /// File extensions allowed for upload (without dot, e.g., "pdf", "docx").
    /// </summary>
    public List<string> AllowedExtensions { get; set; } = new();
}

/// <summary>
/// Security configuration.
/// Controls CORS policies and API key authentication.
/// </summary>
public class SecurityConfig
{
    /// <summary>
    /// CORS allowed origins (for browser-based API access).
    /// Example: "http://intranet.company.local", "http://localhost:3000"
    /// </summary>
    public List<string> AllowedOrigins { get; set; } = new();

    /// <summary>
    /// Enable API key authentication.
    /// When true, all requests (except /health) must include a valid X-API-Key header.
    /// </summary>
    public bool RequireApiKey { get; set; } = false;

    /// <summary>
    /// Valid API keys for authentication.
    /// Generate secure keys (e.g., apikey_live_[32+ random characters]).
    /// Multiple keys allow for key rotation without downtime.
    /// </summary>
    public List<string> ApiKeys { get; set; } = new();
}

/// <summary>
/// HTTP security headers configuration.
/// Protects against common web vulnerabilities.
/// </summary>
public class SecurityHeadersConfig
{
    /// <summary>
    /// Enable X-Content-Type-Options: nosniff header.
    /// Prevents browser MIME-type sniffing attacks.
    /// </summary>
    public bool NoSniff { get; set; } = true;

    /// <summary>
    /// Referrer-Policy header value.
    /// Controls how much referrer information is sent with requests.
    /// </summary>
    public string ReferrerPolicy { get; set; } = "strict-origin-when-cross-origin";

    /// <summary>
    /// X-Frame-Options header value (DENY, SAMEORIGIN, or ALLOW-FROM uri).
    /// Prevents clickjacking attacks by controlling iframe embedding.
    /// </summary>
    public string FrameOptions { get; set; } = "DENY";

    /// <summary>
    /// X-XSS-Protection header value.
    /// Enables browser cross-site scripting (XSS) filters.
    /// </summary>
    public string XssProtection { get; set; } = "1; mode=block";

    /// <summary>
    /// Content-Security-Policy header value.
    /// Restricts resource loading to prevent XSS and data injection.
    /// </summary>
    public string ContentSecurityPolicy { get; set; } = "default-src 'self'";
}

/// <summary>
/// Concurrency control configuration.
/// Limits simultaneous operations to prevent resource exhaustion.
/// </summary>
public class ConcurrencyConfig
{
    /// <summary>
    /// Maximum document conversions that can run at the same time.
    /// Higher values = more throughput but more resource usage.
    /// </summary>
    public int MaxConcurrentConversions { get; set; } = 2;

    /// <summary>
    /// Maximum requests waiting in queue when at capacity.
    /// Requests beyond this are rejected with 503 Service Unavailable.
    /// </summary>
    public int MaxQueueSize { get; set; } = 10;
}

/// <summary>
/// LibreOffice configuration settings.
/// Controls how the application integrates with LibreOffice for document conversions.
/// </summary>
public class LibreOfficeConfig
{
    /// <summary>
    /// Direct path to soffice.exe executable.
    /// Leave empty to use automatic path resolution (bundled then system installation).
    /// </summary>
    public string ExecutablePath { get; set; } = "";

    /// <summary>
    /// Maximum seconds allowed for a single conversion operation before timeout.
    /// </summary>
    public int TimeoutSeconds { get; set; } = 300;
}
