using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace FileConversionApi.Services;

/// <summary>
/// Interface for semaphore and concurrency control services.
/// Manages resource locking to prevent concurrent access issues.
/// </summary>
public interface ISemaphoreService
{
    /// <summary>
    /// Acquires a lock for a conversion operation.
    /// </summary>
    Task<SemaphoreLock> AcquireConversionLockAsync(string operationId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Acquires a lock for file access operations.
    /// </summary>
    Task<SemaphoreLock> AcquireFileAccessLockAsync(string filePath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Acquires a lock for a specific resource with custom concurrency limit.
    /// </summary>
    Task<SemaphoreLock> AcquireResourceLockAsync(string resourceKey, int maxConcurrency = 1, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets statistics about current semaphore usage.
    /// </summary>
    SemaphoreStats GetStats();

    /// <summary>
    /// Attempts to acquire a conversion lock with a timeout.
    /// </summary>
    Task<bool> TryAcquireConversionLockAsync(string operationId, TimeSpan timeout);

    /// <summary>
    /// Acquires the default semaphore (legacy method for backward compatibility).
    /// </summary>
    Task AcquireAsync();

    /// <summary>
    /// Releases the default semaphore (legacy method for backward compatibility).
    /// </summary>
    void Release();

    /// <summary>
    /// Gets the current count of available semaphore slots.
    /// </summary>
    int CurrentCount { get; }
}

/// <summary>
/// Disposable lock wrapper for safe semaphore release.
/// Implements full dispose pattern with finalizer to prevent semaphore leaks.
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
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Only release managed resources during explicit disposal
                // The action delegate may reference managed objects that could be finalized
                _releaseAction();
            }
            _disposed = true;
        }
    }

    ~SemaphoreLock()
    {
        Dispose(false);
    }
}

/// <summary>
/// Statistics about semaphore usage across the application.
/// </summary>
public class SemaphoreStats
{
    public DateTime Timestamp { get; set; }
    public SemaphoreInfo ConversionSemaphore { get; set; } = new();
    public SemaphoreInfo FileAccessSemaphore { get; set; } = new();
    public Dictionary<string, SemaphoreInfo> ResourceSemaphores { get; set; } = new();
}

/// <summary>
/// Information about a specific semaphore instance.
/// </summary>
public class SemaphoreInfo
{
    public int CurrentCount { get; set; }
    public int AvailableWaits { get; set; }
}
