using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using FileConversionApi.Utils;
using System.Threading.Tasks;

namespace FileConversionApi.Tests;

public class SemaphoreServiceTests : IDisposable
{
    private readonly SemaphoreService _service;
    private readonly Mock<ILogger<SemaphoreService>> _loggerMock;

    public SemaphoreServiceTests()
    {
        _loggerMock = new Mock<ILogger<SemaphoreService>>();
        _service = new SemaphoreService(_loggerMock.Object);
    }

    public void Dispose()
    {
        _service.Dispose();
    }

    [Fact]
    public async Task AcquireConversionLockAsync_WithValidOperationId_ReturnsLock()
    {
        // Arrange
        var operationId = "test-operation-123";

        // Act
        var lockResult = await _service.AcquireConversionLockAsync(operationId);

        // Assert
        lockResult.Should().NotBeNull();
        lockResult.LockName.Should().Be($"Conversion-{operationId}");
        lockResult.AcquisitionTimeMs.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task AcquireFileAccessLockAsync_WithValidFilePath_ReturnsLock()
    {
        // Arrange
        var filePath = "C:\\test\\file.docx";

        // Act
        var lockResult = await _service.AcquireFileAccessLockAsync(filePath);

        // Assert
        lockResult.Should().NotBeNull();
        lockResult.LockName.Should().Be($"FileAccess-{Path.GetFileName(filePath)}");
        lockResult.AcquisitionTimeMs.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public async Task AcquireResourceLockAsync_WithValidResourceKey_ReturnsLock()
    {
        // Arrange
        var resourceKey = "database-connection";

        // Act
        var lockResult = await _service.AcquireResourceLockAsync(resourceKey, 1);

        // Assert
        lockResult.Should().NotBeNull();
        lockResult.LockName.Should().Be($"Resource-{resourceKey}");
        lockResult.AcquisitionTimeMs.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void GetStats_ReturnsSemaphoreStatistics()
    {
        // Act
        var stats = _service.GetStats();

        // Assert
        stats.Should().NotBeNull();
        stats.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        stats.ConversionSemaphore.Should().NotBeNull();
        stats.FileAccessSemaphore.Should().NotBeNull();
        stats.ResourceSemaphores.Should().NotBeNull();
    }

    [Fact]
    public async Task TryAcquireConversionLockAsync_WithAvailableLock_ReturnsTrue()
    {
        // Arrange
        var operationId = "test-try-acquire";
        var timeout = TimeSpan.FromSeconds(1);

        // Act
        var result = await _service.TryAcquireConversionLockAsync(operationId, timeout);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task TryAcquireConversionLockAsync_WithTimeout_ReturnsFalse()
    {
        // Arrange - First acquire all available locks
        var locks = new List<SemaphoreLock>();
        for (int i = 0; i < 2; i++) // Default max concurrent conversions is 2
        {
            locks.Add(await _service.AcquireConversionLockAsync($"operation-{i}"));
        }

        var timeout = TimeSpan.FromMilliseconds(100);

        // Act - Try to acquire another lock (should fail due to no available slots)
        var result = await _service.TryAcquireConversionLockAsync("should-fail", timeout);

        // Assert
        result.Should().BeFalse();

        // Cleanup
        foreach (var lockItem in locks)
        {
            lockItem.Dispose();
        }
    }

    [Fact]
    public async Task SemaphoreLock_Dispose_ReleasesLock()
    {
        // Arrange
        var operationId = "dispose-test";

        // Act
        var lockResult = await _service.AcquireConversionLockAsync(operationId);
        lockResult.Dispose();

        // Now try to acquire again - should succeed since lock was released
        var secondLock = await _service.AcquireConversionLockAsync("second-operation");

        // Assert
        secondLock.Should().NotBeNull();
        secondLock.Dispose();
    }

    [Fact]
    public async Task ConcurrentAccess_IsProperlyLimited()
    {
        // Arrange
        var tasks = new List<Task<SemaphoreLock>>();
        var acquiredLocks = new List<SemaphoreLock>();

        // Act - Try to acquire more locks than available (default is 2)
        for (int i = 0; i < 3; i++)
        {
            var task = _service.AcquireConversionLockAsync($"concurrent-{i}");
            tasks.Add(task);
        }

        // Wait for first two to complete
        var completedTasks = await Task.WhenAll(tasks.Take(2));
        acquiredLocks.AddRange(completedTasks);

        // Third task should still be running (no available semaphore slots)
        var thirdTask = tasks[2];
        var thirdCompleted = thirdTask.Wait(TimeSpan.FromMilliseconds(100));

        // Assert
        acquiredLocks.Should().HaveCount(2);
        thirdCompleted.Should().BeFalse(); // Third task should not have completed yet

        // Cleanup
        foreach (var lockItem in acquiredLocks)
        {
            lockItem.Dispose();
        }

        // Now the third task should complete
        var thirdLock = await thirdTask.WaitAsync(TimeSpan.FromSeconds(1));
        thirdLock.Dispose();
    }
}
