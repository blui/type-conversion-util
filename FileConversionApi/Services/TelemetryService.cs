using Microsoft.Extensions.Logging;

namespace FileConversionApi.Services;

/// <summary>
/// Telemetry service implementation
/// Logs conversion and error telemetry data
/// </summary>
public class TelemetryService : ITelemetryService
{
    private readonly ILogger<TelemetryService> _logger;

    public TelemetryService(ILogger<TelemetryService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task LogConversionAsync(ConversionTelemetry telemetry)
    {
        _logger.LogInformation(
            "Conversion completed - Input: {InputFormat}, Target: {TargetFormat}, Size: {FileSize} bytes, Time: {ProcessingTime}ms, Success: {Success}",
            telemetry.InputFormat,
            telemetry.TargetFormat,
            telemetry.FileSize,
            telemetry.ProcessingTimeMs,
            telemetry.Success);

        if (!telemetry.Success && !string.IsNullOrEmpty(telemetry.Error))
        {
            _logger.LogError("Conversion failed: {Error}", telemetry.Error);
        }

        // In a production system, this would also send telemetry to external monitoring systems
        await Task.CompletedTask; // Placeholder for async operations
    }

    /// <inheritdoc/>
    public async Task LogErrorAsync(ErrorTelemetry telemetry)
    {
        var logLevel = telemetry.Error.Contains("timeout", StringComparison.OrdinalIgnoreCase)
            ? LogLevel.Warning
            : LogLevel.Error;

        _logger.Log(logLevel,
            "Error occurred in operation {Operation} at {Timestamp}: {Error}",
            telemetry.Operation,
            telemetry.Timestamp,
            telemetry.Error);

        if (!string.IsNullOrEmpty(telemetry.StackTrace))
        {
            _logger.LogDebug("Stack trace: {StackTrace}", telemetry.StackTrace);
        }

        // In a production system, this would also send error telemetry to external monitoring systems
        await Task.CompletedTask; // Placeholder for async operations
    }
}
