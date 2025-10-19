using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;

namespace FileConversionApi.Services;

/// <summary>
/// Interface for semaphore/concurrency control services
/// </summary>
public interface ISemaphoreService
{
    Task<SemaphoreLock> AcquireConversionLockAsync(string operationId, CancellationToken cancellationToken = default);
    Task<SemaphoreLock> AcquireFileAccessLockAsync(string filePath, CancellationToken cancellationToken = default);
    Task<SemaphoreLock> AcquireResourceLockAsync(string resourceKey, int maxConcurrency = 1, CancellationToken cancellationToken = default);
    SemaphoreStats GetStats();
    Task<bool> TryAcquireConversionLockAsync(string operationId, TimeSpan timeout);

    // Legacy methods for backward compatibility
    Task AcquireAsync();
    void Release();
    int CurrentCount { get; }
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

