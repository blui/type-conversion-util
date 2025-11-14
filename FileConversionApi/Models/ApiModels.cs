namespace FileConversionApi.Models;

public class ApiInfo
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ApiFormats SupportedFormats { get; set; } = new();
    public List<ApiEndpoint> Endpoints { get; set; } = new();
}

public class ApiFormats
{
    public List<string> Input { get; set; } = new();
    public List<string> Output { get; set; } = new();
}

public class ApiEndpoint
{
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class SupportedFormatsResponse
{
    public DocumentFormats Documents { get; set; } = new();
}

public class DocumentFormats
{
    public List<string> Input { get; set; } = new();
    public Dictionary<string, List<string>> Conversions { get; set; } = new();
}

/// <summary>
/// Conversion result with metadata.
/// </summary>
public class ConversionResponse
{
    public bool Success { get; set; }
    public string? FileName { get; set; }
    public long? FileSize { get; set; }
    public long? ProcessingTimeMs { get; set; }
    public string? ConversionMethod { get; set; }
    public string? ContentType { get; set; }
    public byte[]? Data { get; set; }
}

public class ErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public List<string>? Details { get; set; }
}

public class HealthResponse
{
    public string Status { get; set; } = "Unknown";
    public DateTime Timestamp { get; set; }
    public Dictionary<string, ServiceHealth> Services { get; set; } = new();
}

public class ServiceHealth
{
    public string Status { get; set; } = "Unknown";
    public string? Message { get; set; }
}

public class DetailedHealthResponse : HealthResponse
{
    public SystemInformation SystemInfo { get; set; } = new();
    public List<HealthCheckDetail> HealthChecks { get; set; } = new();
}

public class SystemInformation
{
    public string OsVersion { get; set; } = string.Empty;
    public string FrameworkVersion { get; set; } = string.Empty;
    public int ProcessorCount { get; set; }
    public long WorkingSet { get; set; }
    public TimeSpan Uptime { get; set; }
}

public class HealthCheckDetail
{
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "Unknown";
    public string? Description { get; set; }
    public TimeSpan Duration { get; set; }
    public Dictionary<string, string?>? Data { get; set; }
}
