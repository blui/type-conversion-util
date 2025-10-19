using System.Collections.Generic;

namespace FileConversionApi.Models;

/// <summary>
/// File handling configuration
/// </summary>
public class FileHandlingConfig
{
    public string UploadLimit { get; set; } = "50mb";
    public long MaxFileSize { get; set; } = 52428800; // 50MB
    public int MaxFilesPerRequest { get; set; } = 5;
    public string TempDirectory { get; set; } = "App_Data\\temp\\uploads";
    public string OutputDirectory { get; set; } = "App_Data\\temp\\converted";
    public bool UseWindowsPaths { get; set; } = true;
    public bool UseNTFSPermissions { get; set; } = true;
    public bool CleanupTempFiles { get; set; } = true;
    public int TempFileRetentionHours { get; set; } = 24;
    public List<string> AllowedExtensions { get; set; } = new();
    public List<string> BlockedExtensions { get; set; } = new();
}

/// <summary>
/// Security configuration
/// </summary>
public class SecurityConfig
{
    public List<string> IPWhitelist { get; set; } = new();
    public bool EnableIPFiltering { get; set; } = false;
    public bool EnableRateLimiting { get; set; } = true;
    public long MaxRequestSize { get; set; } = 52428800;
    public int RequestTimeoutSeconds { get; set; } = 300;
}

/// <summary>
/// Security headers configuration
/// </summary>
public class SecurityHeadersConfig
{
    public bool NoSniff { get; set; } = true;
    public string ReferrerPolicy { get; set; } = "strict-origin-when-cross-origin";
    public string FrameOptions { get; set; } = "DENY";
    public string XssProtection { get; set; } = "1; mode=block";
    public string ContentSecurityPolicy { get; set; } = "default-src 'self'";
}

/// <summary>
/// Network configuration
/// </summary>
public class NetworkConfig
{
    public bool AllowLocalhost { get; set; } = true;
    public int MaxConcurrentConnections { get; set; } = 100;
    public int RequestTimeout { get; set; } = 300000;
}

/// <summary>
/// Concurrency control configuration
/// </summary>
public class ConcurrencyConfig
{
    public int MaxConcurrentConversions { get; set; } = 2;
    public int MaxQueueSize { get; set; } = 10;
    public ThreadPoolConfig ThreadPoolSettings { get; set; } = new();
}

public class ThreadPoolConfig
{
    public int MinThreads { get; set; } = 4;
    public int MaxThreads { get; set; } = 16;
}

/// <summary>
/// LibreOffice configuration
/// </summary>
public class LibreOfficeConfig
{
    public string SdkPath { get; set; } = "C:\\Program Files\\LibreOfficeSDK";
    public string ExecutablePath { get; set; } = "";
    public bool ForceBundled { get; set; } = false;
    public bool UseSdkIntegration { get; set; } = false;
    public string ConversionQuality { get; set; } = "high";
    public int TimeoutSeconds { get; set; } = 300;
    public int MaxConcurrentConversions { get; set; } = 2;
    public bool EnableLogging { get; set; } = true;
    public string TempDirectory { get; set; } = "App_Data\\temp\\libreoffice";
    public List<string> SupportedFormats { get; set; } = new();
}

/// <summary>
/// Preprocessing configuration
/// </summary>
public class PreprocessingConfig
{
    public bool EnableDocxPreprocessing { get; set; } = true;
    public bool NormalizeFonts { get; set; } = true;
    public bool ConvertColors { get; set; } = true;
    public bool FixTextEffects { get; set; } = true;
    public bool OptimizeImages { get; set; } = false;
    public bool RemoveMacros { get; set; } = true;
    public bool ValidateStructure { get; set; } = true;
}
