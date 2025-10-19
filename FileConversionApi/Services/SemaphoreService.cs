using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FileConversionApi.Models;

namespace FileConversionApi.Services;

/// <summary>
/// Semaphore service for concurrency control
/// Manages concurrent conversion operations and resource access
/// </summary>
public class SemaphoreService : ISemaphoreService
{
    private readonly ILogger<SemaphoreService> _logger;
    private readonly SemaphoreSlim _conversionSemaphore;
    private readonly SemaphoreSlim _fileAccessSemaphore;
    private readonly ConcurrentDictionary<string, SemaphoreSlim> _resourceSemaphores;
    private readonly int _maxConcurrency;
    private readonly int _maxQueueSize;

    // Legacy methods for backward compatibility
    private readonly SemaphoreSlim _simpleSemaphore;

    public SemaphoreService(
        ILogger<SemaphoreService> logger,
        IOptions<ConcurrencyConfig> concurrencyConfig)
    {
        _logger = logger;
        _maxConcurrency = concurrencyConfig.Value.MaxConcurrentConversions;
        _maxQueueSize = concurrencyConfig.Value.MaxQueueSize;

        _conversionSemaphore = new SemaphoreSlim(_maxConcurrency);
        _fileAccessSemaphore = new SemaphoreSlim(5); // Default file access limits
        _resourceSemaphores = new ConcurrentDictionary<string, SemaphoreSlim>();
        _simpleSemaphore = new SemaphoreSlim(_maxConcurrency);

        _logger.LogInformation("Semaphore initialized with max concurrency: {MaxConcurrency}",
            _maxConcurrency);
    }

    /// <inheritdoc/>
    public async Task<SemaphoreLock> AcquireConversionLockAsync(string operationId, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await _conversionSemaphore.WaitAsync(cancellationToken);
        stopwatch.Stop();

        _logger.LogDebug("Conversion lock acquired for {OperationId} in {Time}ms", operationId, stopwatch.ElapsedMilliseconds);

        return new SemaphoreLock(() => _conversionSemaphore.Release(), operationId, stopwatch.ElapsedMilliseconds);
    }

    /// <inheritdoc/>
    public async Task<SemaphoreLock> AcquireFileAccessLockAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await _fileAccessSemaphore.WaitAsync(cancellationToken);
        stopwatch.Stop();

        _logger.LogDebug("File access lock acquired for {FilePath} in {Time}ms", filePath, stopwatch.ElapsedMilliseconds);

        return new SemaphoreLock(() => _fileAccessSemaphore.Release(), filePath, stopwatch.ElapsedMilliseconds);
    }

    /// <inheritdoc/>
    public async Task<SemaphoreLock> AcquireResourceLockAsync(string resourceKey, int maxConcurrency = 1, CancellationToken cancellationToken = default)
    {
        var semaphore = _resourceSemaphores.GetOrAdd(resourceKey, key => new SemaphoreSlim(maxConcurrency));

        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        await semaphore.WaitAsync(cancellationToken);
        stopwatch.Stop();

        _logger.LogDebug("Resource lock acquired for {ResourceKey} in {Time}ms", resourceKey, stopwatch.ElapsedMilliseconds);

        return new SemaphoreLock(() => semaphore.Release(), resourceKey, stopwatch.ElapsedMilliseconds);
    }

    /// <inheritdoc/>
    public SemaphoreStats GetStats()
    {
        return new SemaphoreStats
        {
            Timestamp = DateTime.UtcNow,
            ConversionSemaphore = new SemaphoreInfo
            {
                CurrentCount = _conversionSemaphore.CurrentCount,
                AvailableWaits = _conversionSemaphore.CurrentCount
            },
            FileAccessSemaphore = new SemaphoreInfo
            {
                CurrentCount = _fileAccessSemaphore.CurrentCount,
                AvailableWaits = _fileAccessSemaphore.CurrentCount
            },
            ResourceSemaphores = _resourceSemaphores.ToDictionary(
                kvp => kvp.Key,
                kvp => new SemaphoreInfo
                {
                    CurrentCount = kvp.Value.CurrentCount,
                    AvailableWaits = kvp.Value.CurrentCount
                })
        };
    }

    /// <inheritdoc/>
    public async Task<bool> TryAcquireConversionLockAsync(string operationId, TimeSpan timeout)
    {
        var result = await _conversionSemaphore.WaitAsync(timeout);
        if (result)
        {
            _logger.LogDebug("Conversion lock acquired for {OperationId} within timeout", operationId);
        }
        else
        {
            _logger.LogWarning("Failed to acquire conversion lock for {OperationId} within {Timeout}ms",
                operationId, timeout.TotalMilliseconds);
        }
        return result;
    }

    // Legacy methods for backward compatibility with existing controller code
    public async Task AcquireAsync()
    {
        _logger.LogDebug("Acquiring legacy semaphore. Current count: {CurrentCount}", _simpleSemaphore.CurrentCount);
        await _simpleSemaphore.WaitAsync();
        _logger.LogDebug("Legacy semaphore acquired. Current count: {CurrentCount}", _simpleSemaphore.CurrentCount);
    }

    public void Release()
    {
        _simpleSemaphore.Release();
        _logger.LogDebug("Legacy semaphore released. Current count: {CurrentCount}", _simpleSemaphore.CurrentCount);
    }

    public int CurrentCount => _simpleSemaphore.CurrentCount;
}
