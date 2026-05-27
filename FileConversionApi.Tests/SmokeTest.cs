using Xunit;

namespace FileConversionApi.Tests;

/// <summary>
/// Asserts the xUnit runner discovers and executes tests in this project.
/// </summary>
public sealed class SmokeTest
{
    [Fact]
    public void Smoke_TestRunnerDiscoversAndRuns_ReturnsGreen()
    {
        Assert.True(true);
    }
}
