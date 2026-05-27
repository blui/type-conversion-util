using FileConversionApi.Models;
using FileConversionApi.Services.Interfaces;

namespace FileConversionApi.Tests.Fixtures;

/// <summary>
/// Test double for the LibreOffice process-manager seam. Returns a fresh
/// <see cref="ConversionResult"/> copy on every call so a consumer that mutates the returned
/// instance (the real <see cref="FileConversionApi.Services.LibreOfficeService"/> writes
/// <see cref="ConversionResult.ProcessingTimeMs"/>) cannot stomp the template the next call
/// reads from. Exposes <see cref="CallCount"/> so assertions can verify the controller
/// dispatched into the seam without spawning soffice.
/// </summary>
internal sealed class FakeLibreOfficeProcessManager : ILibreOfficeProcessManager
{
    private readonly ConversionResult _template;

    /// <summary>
    /// Number of times <see cref="ConvertAsync"/> has been invoked on this instance.
    /// </summary>
    public int CallCount { get; private set; }

    /// <summary>
    /// Captures the result template the fake will copy and return on every call.
    /// </summary>
    /// <param name="cannedResult">The conversion result the fake copies. Must not be null.</param>
    public FakeLibreOfficeProcessManager(ConversionResult cannedResult)
    {
        _template = cannedResult ?? throw new ArgumentNullException(nameof(cannedResult));
    }

    /// <inheritdoc/>
    public Task<ConversionResult> ConvertAsync(
        string inputPath,
        string outputPath,
        string targetFormat,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        return Task.FromResult(new ConversionResult
        {
            Success = _template.Success,
            Error = _template.Error,
            OutputPath = _template.OutputPath,
            ProcessingTimeMs = _template.ProcessingTimeMs,
            ConversionMethod = _template.ConversionMethod,
            FailureReason = _template.FailureReason
        });
    }
}
