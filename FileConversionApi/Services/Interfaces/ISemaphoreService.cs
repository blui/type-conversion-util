namespace FileConversionApi.Services;

/// <summary>
/// Interface for semaphore and concurrency control services.
/// </summary>
public interface ISemaphoreService
{
    Task AcquireAsync();
    void Release();
    int CurrentCount { get; }
}
