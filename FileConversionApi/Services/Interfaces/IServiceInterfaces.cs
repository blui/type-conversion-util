using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace FileConversionApi.Services;

/// <summary>
/// Interface for conversion validation services
/// </summary>
public interface IConversionValidator
{
    bool IsValidConversion(string inputFormat, string targetFormat);
    List<string> GetSupportedInputFormats();
    List<string> GetSupportedTargetFormats(string inputFormat);
    ValidationResult ValidateConversion(string inputFormat, string targetFormat);
}

/// <summary>
/// Interface for performance monitoring services
/// </summary>
public interface IPerformanceMonitor
{
    void StartOperation(string operationName);
    void EndOperation(string operationName);
    PerformanceMetrics GetMetrics();
}

/// <summary>
/// Performance metrics
/// </summary>
public class PerformanceMetrics
{
    public long TotalOperations { get; set; }
    public double AverageResponseTime { get; set; }
    public Dictionary<string, long> OperationCounts { get; set; } = new();
}

/// <summary>
/// Interface for telemetry services
/// </summary>
public interface ITelemetryService
{
    Task LogConversionAsync(ConversionTelemetry telemetry);
    Task LogErrorAsync(ErrorTelemetry telemetry);
}

/// <summary>
/// Conversion telemetry data
/// </summary>
public class ConversionTelemetry
{
    public string InputFormat { get; set; } = string.Empty;
    public string TargetFormat { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public long ProcessingTimeMs { get; set; }
    public bool Success { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Error telemetry data
/// </summary>
public class ErrorTelemetry
{
    public string Operation { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
    public string? StackTrace { get; set; }
    public DateTime Timestamp { get; set; }
}

/// <summary>
/// Interface for semaphore/concurrency control services
/// </summary>
public interface ISemaphoreService
{
    Task AcquireAsync();
    void Release();
    int CurrentCount { get; }
    int AvailableCount { get; }
}
