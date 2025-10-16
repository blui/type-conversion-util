using Microsoft.Extensions.Logging;

namespace FileConversionApi.Services;

/// <summary>
/// Performance monitoring service implementation
/// Tracks operation performance and metrics
/// </summary>
public class PerformanceMonitor : IPerformanceMonitor
{
    private readonly ILogger<PerformanceMonitor> _logger;
    private readonly Dictionary<string, OperationMetrics> _operationMetrics = new();
    private readonly object _lock = new();

    public PerformanceMonitor(ILogger<PerformanceMonitor> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public void StartOperation(string operationName)
    {
        var startTime = DateTime.UtcNow;

        lock (_lock)
        {
            if (!_operationMetrics.TryGetValue(operationName, out var metrics))
            {
                metrics = new OperationMetrics();
                _operationMetrics[operationName] = metrics;
            }

            metrics.StartTime = startTime;
            metrics.IsRunning = true;
        }

        _logger.LogDebug("Started monitoring operation: {OperationName}", operationName);
    }

    /// <inheritdoc/>
    public void EndOperation(string operationName)
    {
        var endTime = DateTime.UtcNow;

        lock (_lock)
        {
            if (_operationMetrics.TryGetValue(operationName, out var metrics) && metrics.IsRunning)
            {
                var duration = endTime - metrics.StartTime;
                metrics.TotalOperations++;
                metrics.TotalDuration += duration;
                metrics.IsRunning = false;

                _logger.LogInformation("Operation {OperationName} completed in {Duration}ms",
                    operationName, duration.TotalMilliseconds);
            }
        }
    }

    /// <inheritdoc/>
    public PerformanceMetrics GetMetrics()
    {
        lock (_lock)
        {
            var metrics = new PerformanceMetrics();

            foreach (var kvp in _operationMetrics)
            {
                var operationName = kvp.Key;
                var operationMetrics = kvp.Value;

                metrics.TotalOperations += operationMetrics.TotalOperations;
                metrics.OperationCounts[operationName] = operationMetrics.TotalOperations;

                if (operationMetrics.TotalOperations > 0)
                {
                    var avgDuration = operationMetrics.TotalDuration.TotalMilliseconds / operationMetrics.TotalOperations;
                    // Note: This is a simplified average - in production you'd want more sophisticated metrics
                }
            }

            return metrics;
        }
    }
}

/// <summary>
/// Internal operation metrics
/// </summary>
internal class OperationMetrics
{
    public int TotalOperations { get; set; }
    public TimeSpan TotalDuration { get; set; }
    public DateTime StartTime { get; set; }
    public bool IsRunning { get; set; }
}
