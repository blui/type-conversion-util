using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FileConversionApi.Models;

namespace FileConversionApi.Services;

/// <summary>
/// Manages concurrency control for conversion operations.
/// </summary>
public class SemaphoreService : ISemaphoreService
{
    private readonly ILogger<SemaphoreService> _logger;
    private readonly SemaphoreSlim _semaphore;

    public SemaphoreService(
        ILogger<SemaphoreService> logger,
        IOptions<ConcurrencyConfig> concurrencyConfig)
    {
        _logger = logger;
        var maxConcurrency = concurrencyConfig.Value.MaxConcurrentConversions;
        _semaphore = new SemaphoreSlim(maxConcurrency);

        _logger.LogInformation("Semaphore initialized with max concurrency: {MaxConcurrency}", maxConcurrency);
    }

    public async Task AcquireAsync()
    {
        await _semaphore.WaitAsync();
    }

    public void Release()
    {
        _semaphore.Release();
    }

    public int CurrentCount => _semaphore.CurrentCount;
}
