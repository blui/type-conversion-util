using Microsoft.Extensions.Logging;
using System.Threading;

namespace FileConversionApi.Utils;

/// <summary>
/// Semaphore service for concurrency control
/// Manages concurrent access to shared resources and conversion operations
/// </summary>
public class SemaphoreService : ISemaphoreService
{
    private readonly ILogger<SemaphoreService> _logger;
    private readonly SemaphoreSlim _conversionSemaphore;
    private readonly SemaphoreSlim _fileAccessSemaphore;
    private readonly Dictionary<string, SemaphoreSlim> _resourceSemaphores;

    // Default concurrency limits
    private const int DefaultMaxConcurrentConversions = 2;
    private const int DefaultMaxFileAccess = 5;

    public SemaphoreService(ILogger<SemaphoreService> logger)
    {
        _logger = logger;
        _conversionSemaphore = new SemaphoreSlim(DefaultMaxConcurrentConversions);
        _fileAccessSemaphore = new SemaphoreSlim(DefaultMaxFileAccess);
        _resourceSemaphores = new Dictionary<string, SemaphoreSlim>();

        _logger.LogInformation("Semaphore service initialized with max concurrent conversions: {MaxConversions}, max file access: {MaxFileAccess}",
            DefaultMaxConcurrentConversions, DefaultMaxFileAccess);
    }

    /// <inheritdoc/>
    public async Task<SemaphoreLock> AcquireConversionLockAsync(string operationId, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogDebug("Acquiring conversion lock for operation: {OperationId}", operationId);

            await _conversionSemaphore.WaitAsync(cancellationToken);
            stopwatch.Stop();

            _logger.LogDebug("Conversion lock acquired for operation: {OperationId} after {Time}ms", operationId, stopwatch.ElapsedMilliseconds);

            return new SemaphoreLock(
                () => _conversionSemaphore.Release(),
                $"Conversion-{operationId}",
                stopwatch.ElapsedMilliseconds
            );
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            _logger.LogWarning("Conversion lock acquisition cancelled for operation: {OperationId} after {Time}ms", operationId, stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Failed to acquire conversion lock for operation: {OperationId}", operationId);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<SemaphoreLock> AcquireFileAccessLockAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogDebug("Acquiring file access lock for: {FilePath}", filePath);

            await _fileAccessSemaphore.WaitAsync(cancellationToken);
            stopwatch.Stop();

            _logger.LogDebug("File access lock acquired for: {FilePath} after {Time}ms", filePath, stopwatch.ElapsedMilliseconds);

            return new SemaphoreLock(
                () => _fileAccessSemaphore.Release(),
                $"FileAccess-{Path.GetFileName(filePath)}",
                stopwatch.ElapsedMilliseconds
            );
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            _logger.LogWarning("File access lock acquisition cancelled for: {FilePath} after {Time}ms", filePath, stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Failed to acquire file access lock for: {FilePath}", filePath);
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<SemaphoreLock> AcquireResourceLockAsync(string resourceKey, int maxConcurrency = 1, CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogDebug("Acquiring resource lock for: {ResourceKey}", resourceKey);

            // Get or create semaphore for this resource
            var semaphore = GetOrCreateResourceSemaphore(resourceKey, maxConcurrency);

            await semaphore.WaitAsync(cancellationToken);
            stopwatch.Stop();

            _logger.LogDebug("Resource lock acquired for: {ResourceKey} after {Time}ms", resourceKey, stopwatch.ElapsedMilliseconds);

            return new SemaphoreLock(
                () => semaphore.Release(),
                $"Resource-{resourceKey}",
                stopwatch.ElapsedMilliseconds
            );
        }
        catch (OperationCanceledException)
        {
            stopwatch.Stop();
            _logger.LogWarning("Resource lock acquisition cancelled for: {ResourceKey} after {Time}ms", resourceKey, stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Failed to acquire resource lock for: {ResourceKey}", resourceKey);
            throw;
        }
    }

    /// <inheritdoc/>
    public SemaphoreStats GetStats()
    {
        var stats = new SemaphoreStats
        {
            Timestamp = DateTime.UtcNow,
            ConversionSemaphore = new SemaphoreInfo
            {
                CurrentCount = _conversionSemaphore.CurrentCount,
                AvailableWaits = DefaultMaxConcurrentConversions - _conversionSemaphore.CurrentCount
            },
            FileAccessSemaphore = new SemaphoreInfo
            {
                CurrentCount = _fileAccessSemaphore.CurrentCount,
                AvailableWaits = DefaultMaxFileAccess - _fileAccessSemaphore.CurrentCount
            },
            ResourceSemaphores = new Dictionary<string, SemaphoreInfo>()
        };

        foreach (var kvp in _resourceSemaphores)
        {
            stats.ResourceSemaphores[kvp.Key] = new SemaphoreInfo
            {
                CurrentCount = kvp.Value.CurrentCount,
                AvailableWaits = kvp.Key.Contains("1") ? 1 - kvp.Value.CurrentCount : 0 // Simplified for demo
            };
        }

        return stats;
    }

    /// <inheritdoc/>
    public async Task<bool> TryAcquireConversionLockAsync(string operationId, TimeSpan timeout)
    {
        try
        {
            _logger.LogDebug("Trying to acquire conversion lock for operation: {OperationId} with timeout: {Timeout}", operationId, timeout);

            var result = await _conversionSemaphore.WaitAsync(timeout);

            if (result)
            {
                _logger.LogDebug("Conversion lock acquired for operation: {OperationId}", operationId);
                // Release immediately since this is just a test
                _conversionSemaphore.Release();
                return true;
            }
            else
            {
                _logger.LogWarning("Failed to acquire conversion lock for operation: {OperationId} within timeout", operationId);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error testing conversion lock acquisition for operation: {OperationId}", operationId);
            return false;
        }
    }

    /// <summary>
    /// Get or create a resource-specific semaphore
    /// </summary>
    private SemaphoreSlim GetOrCreateResourceSemaphore(string resourceKey, int maxConcurrency)
    {
        lock (_resourceSemaphores)
        {
            if (!_resourceSemaphores.TryGetValue(resourceKey, out var semaphore))
            {
                semaphore = new SemaphoreSlim(maxConcurrency);
                _resourceSemaphores[resourceKey] = semaphore;

                _logger.LogInformation("Created new resource semaphore for: {ResourceKey} with max concurrency: {MaxConcurrency}",
                    resourceKey, maxConcurrency);
            }

            return semaphore;
        }
    }

    /// <summary>
    /// Dispose of all semaphores
    /// </summary>
    public void Dispose()
    {
        _conversionSemaphore.Dispose();
        _fileAccessSemaphore.Dispose();

        foreach (var semaphore in _resourceSemaphores.Values)
        {
            semaphore.Dispose();
        }

        _resourceSemaphores.Clear();

        _logger.LogInformation("Semaphore service disposed");
    }
}

/// <summary>
/// Semaphore lock wrapper for safe disposal
/// </summary>
public class SemaphoreLock : IDisposable
{
    private readonly Action _releaseAction;
    private readonly string _lockName;
    private readonly long _acquisitionTimeMs;
    private bool _disposed;

    public SemaphoreLock(Action releaseAction, string lockName, long acquisitionTimeMs)
    {
        _releaseAction = releaseAction;
        _lockName = lockName;
        _acquisitionTimeMs = acquisitionTimeMs;
    }

    public string LockName => _lockName;
    public long AcquisitionTimeMs => _acquisitionTimeMs;

    public void Dispose()
    {
        if (!_disposed)
        {
            _releaseAction();
            _disposed = true;
        }
    }
}

/// <summary>
/// Semaphore statistics
/// </summary>
public class SemaphoreStats
{
    public DateTime Timestamp { get; set; }
    public SemaphoreInfo ConversionSemaphore { get; set; } = new();
    public SemaphoreInfo FileAccessSemaphore { get; set; } = new();
    public Dictionary<string, SemaphoreInfo> ResourceSemaphores { get; set; } = new();
}

/// <summary>
/// Individual semaphore information
/// </summary>
public class SemaphoreInfo
{
    public int CurrentCount { get; set; }
    public int AvailableWaits { get; set; }
}

/// <summary>
/// Semaphore service interface
/// </summary>
public interface ISemaphoreService
{
    Task<SemaphoreLock> AcquireConversionLockAsync(string operationId, CancellationToken cancellationToken = default);
    Task<SemaphoreLock> AcquireFileAccessLockAsync(string filePath, CancellationToken cancellationToken = default);
    Task<SemaphoreLock> AcquireResourceLockAsync(string resourceKey, int maxConcurrency = 1, CancellationToken cancellationToken = default);
    SemaphoreStats GetStats();
    Task<bool> TryAcquireConversionLockAsync(string operationId, TimeSpan timeout);
}
