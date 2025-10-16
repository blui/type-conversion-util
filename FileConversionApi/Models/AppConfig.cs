using System.Collections.Generic;

namespace FileConversionApi.Models;

/// <summary>
/// Main application configuration class
/// </summary>
public class AppConfig
{
    public ServerConfig Server { get; set; } = new();
    public FileHandlingConfig FileHandling { get; set; } = new();
    public RateLimitingConfig RateLimiting { get; set; } = new();
    public CorsConfig Cors { get; set; } = new();
    public SecurityHeadersConfig SecurityHeaders { get; set; } = new();
    public CustomLoggingConfig CustomLogging { get; set; } = new();
    public ConcurrencyConfig Concurrency { get; set; } = new();
    public TimeoutConfig Timeouts { get; set; } = new();
    public NetworkConfig Network { get; set; } = new();
    public HealthCheckConfig HealthChecks { get; set; } = new();
    public LibreOfficeConfig LibreOffice { get; set; } = new();
    public SSLConfig SSL { get; set; } = new();
    public SecurityConfig Security { get; set; } = new();
    public PreprocessingConfig Preprocessing { get; set; } = new();
}

/// <summary>
/// Server configuration
/// </summary>
public class ServerConfig
{
    public int Port { get; set; } = 3000;
    public string Host { get; set; } = "localhost";
    public string Environment { get; set; } = "Development";
}

/// <summary>
/// File handling configuration
/// </summary>
public class FileHandlingConfig
{
    public string UploadLimit { get; set; } = "50mb";
    public long MaxFileSize { get; set; } = 52428800; // 50MB
    public string TempDirectory { get; set; } = "./temp";
    public string OutputDirectory { get; set; } = "./temp/converted";
}

/// <summary>
/// Rate limiting configuration
/// </summary>
public class RateLimitingConfig
{
    public bool EnableEndpointRateLimiting { get; set; } = true;
    public bool StackBlockedRequests { get; set; } = false;
    public int HttpStatusCode { get; set; } = 429;
    public List<RateLimitRule> GeneralRules { get; set; } = new();
}

public class RateLimitRule
{
    public string Endpoint { get; set; } = "*";
    public string Period { get; set; } = "1m";
    public int Limit { get; set; } = 30;
}

/// <summary>
/// CORS configuration
/// </summary>
public class CorsConfig
{
    public bool AllowAnyOrigin { get; set; } = true;
    public bool AllowAnyMethod { get; set; } = true;
    public bool AllowAnyHeader { get; set; } = true;
    public bool AllowCredentials { get; set; } = true;
}

/// <summary>
/// Security headers configuration
/// </summary>
public class SecurityHeadersConfig
{
    public string ContentSecurityPolicy { get; set; } = "default-src 'self'";
    public HstsConfig Hsts { get; set; } = new();
    public bool NoSniff { get; set; } = true;
    public string ReferrerPolicy { get; set; } = "strict-origin-when-cross-origin";
}

public class HstsConfig
{
    public bool Enabled { get; set; } = false;
    public int MaxAge { get; set; } = 31536000;
    public bool IncludeSubDomains { get; set; } = false;
    public bool Preload { get; set; } = false;
}

/// <summary>
/// Custom logging configuration
/// </summary>
public class CustomLoggingConfig
{
    public string Level { get; set; } = "Information";
    public bool IncludeScopes { get; set; } = false;
    public string FilePath { get; set; } = "./logs/file-conversion-api-.log";
    public string RollingInterval { get; set; } = "Day";
    public int RetainedFileCountLimit { get; set; } = 7;
}

/// <summary>
/// Concurrency control configuration
/// </summary>
public class ConcurrencyConfig
{
    public int MaxConcurrentConversions { get; set; } = 2;
    public int MaxQueueSize { get; set; } = 10;
}

/// <summary>
/// Timeout configuration
/// </summary>
public class TimeoutConfig
{
    public int DocumentConversion { get; set; } = 60000;
    public int ImageConversion { get; set; } = 30000;
    public int HttpClientTimeout { get; set; } = 30000;
}

/// <summary>
/// Network configuration
/// </summary>
public class NetworkConfig
{
    public bool TrustProxy { get; set; } = false;
    public bool KeepAlive { get; set; } = true;
    public int KeepAliveTimeout { get; set; } = 65000;
}

/// <summary>
/// Health check configuration
/// </summary>
public class HealthCheckConfig
{
    public bool Enabled { get; set; } = true;
    public string Path { get; set; } = "/health";
    public string DetailedPath { get; set; } = "/health/detailed";
    public int Timeout { get; set; } = 5000;
    public bool IncludeSystemInfo { get; set; } = false;
}

/// <summary>
/// LibreOffice configuration
/// </summary>
public class LibreOfficeConfig
{
    public string SdkPath { get; set; } = "C:\\Program Files\\LibreOfficeSDK";
    public string ExecutablePath { get; set; } = "";
    public bool ForceBundled { get; set; } = true;
    public string ConversionQuality { get; set; } = "high";
}

/// <summary>
/// SSL configuration
/// </summary>
public class SSLConfig
{
    public bool Enabled { get; set; } = false;
    public string CertificatePath { get; set; } = "";
    public string CertificatePassword { get; set; } = "";
    public bool AcceptSelfSigned { get; set; } = true;
}

/// <summary>
/// Security configuration
/// </summary>
public class SecurityConfig
{
    public List<string> IPWhitelist { get; set; } = new();
    public bool EnableAdvancedSecurity { get; set; } = true;
}

/// <summary>
/// Preprocessing configuration
/// </summary>
public class PreprocessingConfig
{
    public bool EnableDocxPreprocessing { get; set; } = true;
    public bool NormalizeFonts { get; set; } = true;
    public bool ConvertColors { get; set; } = true;
}
