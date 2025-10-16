using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using FileConversionApi.Utils;

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
/// Interface for configuration validator services
/// </summary>
public interface IConfigValidator
{
    ValidationResult ValidateConfiguration();
    T GetValidatedConfig<T>(string sectionName) where T : new();
    Dictionary<string, object> GetConfigurationHealth();
}

/// <summary>
/// Interface for DOCX preprocessor services
/// </summary>
public interface IDocxPreProcessor
{
    Task<PreprocessingResult> ProcessAsync(string inputPath, string outputPath);
}

/// <summary>
/// Interface for preprocessing services
/// </summary>
public interface IPreprocessingService
{
    Task<PreprocessingResult> PreprocessDocxAsync(string inputPath, string outputPath);
    PreprocessingCapabilities GetCapabilities();
}

/// <summary>
/// Preprocessing capabilities information
/// </summary>
public class PreprocessingCapabilities
{
    public bool Available { get; set; }
    public string[]? SupportedFormats { get; set; }
    public string[]? Features { get; set; }
}

/// <summary>
/// Preprocessing result
/// </summary>
public class PreprocessingResult
{
    public bool Success { get; set; }
    public string? InputPath { get; set; }
    public string? OutputPath { get; set; }
    public PreprocessingFixes? Fixes { get; set; }
    public long ProcessingTimeMs { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Track what fixes were applied during preprocessing
/// </summary>
public class PreprocessingFixes
{
    public int FontsNormalized { get; set; }
    public int ThemeColorsConverted { get; set; }
    public int StylesSimplified { get; set; }
    public int ParagraphsAdjusted { get; set; }
    public int BoldFixed { get; set; }
}

/// <summary>
/// Configuration validation rule
/// </summary>
public class ConfigValidationRule
{
    public string Type { get; set; } = string.Empty;
    public bool Required { get; set; }
    public object? DefaultValue { get; set; }
    public double? Min { get; set; }
    public double? Max { get; set; }
    public string? Pattern { get; set; }
    public string[]? AllowedValues { get; set; }
}

/// <summary>
/// Validation result
/// </summary>
public class ValidationResult
{
    public bool IsValid { get; set; }
    public List<string> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public Dictionary<string, object> Info { get; set; } = new();
}
