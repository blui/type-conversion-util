using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FileConversionApi.Models;

namespace FileConversionApi.Services;

/// <summary>
/// Manages concurrency control for conversion operations. Disposable so the host releases the
/// underlying SemaphoreSlim on shutdown rather than leaving it to finalization.
/// </summary>
public class SemaphoreService : IDisposable
{
    private readonly ILogger<SemaphoreService> _logger;
    private readonly SemaphoreSlim _semaphore;
    private bool _disposed;

    public SemaphoreService(
        ILogger<SemaphoreService> logger,
        IOptions<ConcurrencyConfig> concurrencyConfig)
    {
        _logger = logger;
        var maxConcurrency = concurrencyConfig.Value.MaxConcurrentConversions;
        _semaphore = new SemaphoreSlim(maxConcurrency);

        _logger.LogInformation("Semaphore initialized with max concurrency: {MaxConcurrency}", maxConcurrency);
    }

    public Task AcquireAsync() => _semaphore.WaitAsync();

    public void Release()
    {
        _semaphore.Release();
    }

    public void Dispose()
    {
        if (_disposed) return;
        _semaphore.Dispose();
        _disposed = true;
    }
}
