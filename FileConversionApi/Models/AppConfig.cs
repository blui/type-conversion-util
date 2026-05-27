using System.Collections.Generic;

namespace FileConversionApi.Models;

public class FileHandlingConfig
{
    // Default: 50 MB
    public long MaxFileSize { get; set; } = 52428800;

    // Supports relative or absolute paths
    public string TempDirectory { get; set; } = Path.Combine("App_Data", "temp", "uploads");
    public string OutputDirectory { get; set; } = Path.Combine("App_Data", "temp", "converted");

    // Extensions without dot (e.g., "pdf", "docx")
    public List<string> AllowedExtensions { get; set; } = new();
}

public class SecurityConfig
{
    // Example: "http://localhost:3000"
    public List<string> AllowedOrigins { get; set; } = new();

    // When enabled, all requests except /health require X-API-Key header
    public bool RequireApiKey { get; set; } = false;

    // Format: apikey_live_[32+ chars]. Multiple keys enable rotation
    public List<string> ApiKeys { get; set; } = new();
}

public class SecurityHeadersConfig
{
    // Prevents MIME-type sniffing attacks
    public bool NoSniff { get; set; } = true;

    // Controls referrer information sent with requests
    public string ReferrerPolicy { get; set; } = "strict-origin-when-cross-origin";

    // Prevents clickjacking via iframe embedding
    public string FrameOptions { get; set; } = "DENY";

    // Restricts resource loading to prevent XSS
    public string ContentSecurityPolicy { get; set; } = "default-src 'self'";
}

public class ConcurrencyConfig
{
    // Higher values increase throughput but consume more resources
    public int MaxConcurrentConversions { get; set; } = 2;

    // Excess requests return 503 Service Unavailable
    public int MaxQueueSize { get; set; } = 10;
}

public class LibreOfficeConfig
{
    // Leave empty for automatic resolution (bundled, then system)
    public string ExecutablePath { get; set; } = "";

    // Conversion timeout in seconds
    public int TimeoutSeconds { get; set; } = 300;
}

public class NodeEngineConfig
{
    // Leave empty for automatic resolution of the bundled node.exe
    public string NodePath { get; set; } = "";

    // PDF->HTML conversion timeout in seconds
    public int TimeoutSeconds { get; set; } = 120;
}
