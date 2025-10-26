using System.Collections.Generic;

namespace FileConversionApi.Models;

/// <summary>
/// File handling configuration.
/// Controls file upload limits, temporary storage locations, and allowed file types.
/// </summary>
public class FileHandlingConfig
{
    /// <summary>
    /// Human-readable upload limit string (e.g., "50mb", "100mb").
    /// </summary>
    public string UploadLimit { get; set; } = "50mb";

    /// <summary>
    /// Maximum file size in bytes (52428800 bytes = 50 MB).
    /// Files larger than this will be rejected.
    /// </summary>
    public long MaxFileSize { get; set; } = 52428800;

    /// <summary>
    /// Maximum number of files that can be uploaded in a single request.
    /// </summary>
    public int MaxFilesPerRequest { get; set; } = 5;

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
    /// Use Windows-style path separators (backslash).
    /// Set false for Linux/Unix deployments (forward slash).
    /// </summary>
    public bool UseWindowsPaths { get; set; } = true;

    /// <summary>
    /// Apply NTFS permissions to temporary files (Windows security feature).
    /// Set false for non-Windows environments.
    /// </summary>
    public bool UseNTFSPermissions { get; set; } = true;

    /// <summary>
    /// Automatically delete temporary files after processing.
    /// </summary>
    public bool CleanupTempFiles { get; set; } = true;

    /// <summary>
    /// Hours to keep temporary files before automatic deletion.
    /// </summary>
    public int TempFileRetentionHours { get; set; } = 24;

    /// <summary>
    /// File extensions allowed for upload (without dot, e.g., "pdf", "docx").
    /// </summary>
    public List<string> AllowedExtensions { get; set; } = new();

    /// <summary>
    /// File extensions explicitly blocked for security (e.g., "exe", "bat").
    /// </summary>
    public List<string> BlockedExtensions { get; set; } = new();
}

/// <summary>
/// Security configuration.
/// Controls access restrictions, rate limiting, and CORS policies.
/// </summary>
public class SecurityConfig
{
    /// <summary>
    /// IP addresses or CIDR ranges allowed to access the API.
    /// Example: "192.168.1.0/24", "10.0.0.0/8", "127.0.0.1"
    /// Empty list means all IPs allowed (when EnableIPFiltering is false).
    /// </summary>
    public List<string> IPWhitelist { get; set; } = new();

    /// <summary>
    /// Enable IP address filtering based on IPWhitelist.
    /// Set true to restrict access to whitelisted IPs only.
    /// </summary>
    public bool EnableIPFiltering { get; set; } = false;

    /// <summary>
    /// Enable request rate limiting to prevent abuse.
    /// Limits configured in IpRateLimiting section.
    /// </summary>
    public bool EnableRateLimiting { get; set; } = true;

    /// <summary>
    /// Maximum HTTP request body size in bytes (for file uploads).
    /// </summary>
    public long MaxRequestSize { get; set; } = 52428800;

    /// <summary>
    /// Maximum seconds to wait for request processing before timeout.
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// CORS allowed origins (for browser-based API access).
    /// Example: "http://intranet.company.local", "http://localhost:3000"
    /// </summary>
    public List<string> AllowedOrigins { get; set; } = new();
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
/// Network configuration.
/// Controls connection limits and timeouts.
/// </summary>
public class NetworkConfig
{
    /// <summary>
    /// Allow connections from localhost (127.0.0.1).
    /// Useful for local testing and health checks.
    /// </summary>
    public bool AllowLocalhost { get; set; } = true;

    /// <summary>
    /// Maximum number of simultaneous HTTP connections.
    /// </summary>
    public int MaxConcurrentConnections { get; set; } = 100;

    /// <summary>
    /// Network request timeout in milliseconds.
    /// </summary>
    public int RequestTimeout { get; set; } = 300000;
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

/// <summary>
/// Document preprocessing configuration.
/// Controls DOCX pre-processing steps to improve LibreOffice conversion fidelity.
/// </summary>
public class PreprocessingConfig
{
    /// <summary>
    /// Enable DOCX preprocessing before PDF conversion.
    /// Recommended to keep enabled for best conversion quality.
    /// </summary>
    public bool EnableDocxPreprocessing { get; set; } = true;

    /// <summary>
    /// Replace problematic fonts with LibreOffice-compatible alternatives.
    /// Example: Aptos → Calibri, proprietary fonts → Liberation family
    /// </summary>
    public bool NormalizeFonts { get; set; } = true;

    /// <summary>
    /// Convert Office theme colors to explicit RGB values.
    /// Prevents color rendering differences in LibreOffice.
    /// </summary>
    public bool ConvertColors { get; set; } = true;

    /// <summary>
    /// Fix text effects formatting for LibreOffice compatibility.
    /// </summary>
    public bool FixTextEffects { get; set; } = true;

    /// <summary>
    /// Compress and optimize embedded images.
    /// Reduces file size but adds processing time (disabled by default).
    /// </summary>
    public bool OptimizeImages { get; set; } = false;

    /// <summary>
    /// Remove VBA macros during preprocessing (security and compatibility).
    /// </summary>
    public bool RemoveMacros { get; set; } = true;

    /// <summary>
    /// Validate DOCX structure before processing.
    /// Helps catch corrupted documents early.
    /// </summary>
    public bool ValidateStructure { get; set; } = true;
}
