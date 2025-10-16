using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FileConversionApi.Models;

namespace FileConversionApi.Services;

/// <summary>
/// Semaphore service for concurrency control
/// Manages concurrent conversion operations
/// </summary>
public class SemaphoreService : ISemaphoreService
{
    private readonly ILogger<SemaphoreService> _logger;
    private readonly SemaphoreSlim _semaphore;
    private readonly int _maxConcurrency;
    private readonly int _maxQueueSize;

    public SemaphoreService(
        ILogger<SemaphoreService> logger,
        IOptions<ConcurrencyConfig> concurrencyConfig)
    {
        _logger = logger;
        _maxConcurrency = concurrencyConfig.Value.MaxConcurrentConversions;
        _maxQueueSize = concurrencyConfig.Value.MaxQueueSize;

        _semaphore = new SemaphoreSlim(_maxConcurrency, _maxQueueSize);

        _logger.LogInformation("Semaphore initialized with max concurrency: {MaxConcurrency}, max queue: {MaxQueue}",
            _maxConcurrency, _maxQueueSize);
    }

    /// <inheritdoc/>
    public async Task AcquireAsync()
    {
        _logger.LogDebug("Acquiring semaphore. Current count: {CurrentCount}", _semaphore.CurrentCount);

        await _semaphore.WaitAsync();

        _logger.LogDebug("Semaphore acquired. Current count: {CurrentCount}", _semaphore.CurrentCount);
    }

    /// <inheritdoc/>
    public void Release()
    {
        _semaphore.Release();
        _logger.LogDebug("Semaphore released. Current count: {CurrentCount}", _semaphore.CurrentCount);
    }

    /// <inheritdoc/>
    public int CurrentCount => _semaphore.CurrentCount;

    /// <inheritdoc/>
    public int AvailableCount => _maxConcurrency - (_maxConcurrency - _semaphore.CurrentCount);
}
