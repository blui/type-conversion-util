using FileConversionApi.Models;
using FileConversionApi.Services;

namespace FileConversionApi.Tests.Fixtures;

/// <summary>
/// Test double for the Node engine seam. Returns a fresh <see cref="ConversionResult"/> copy
/// on every call to keep the contract symmetric with the LibreOffice fake; a consumer that
/// mutates the returned instance cannot stomp the template.
/// </summary>
internal sealed class FakeNodeEngineProcessManager : INodeEngineProcessManager
{
    private readonly ConversionResult _template;

    /// <summary>
    /// Number of times the Node engine seam was invoked.
    /// </summary>
    public int CallCount { get; private set; }

    /// <summary>
    /// Captures the result template the fake will copy and return on every call.
    /// </summary>
    /// <param name="cannedResult">The conversion result the fake copies. Must not be null.</param>
    public FakeNodeEngineProcessManager(ConversionResult cannedResult)
    {
        _template = cannedResult ?? throw new ArgumentNullException(nameof(cannedResult));
    }

    /// <inheritdoc/>
    public Task<ConversionResult> ConvertPdfToHtmlAsync(
        string inputPath,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        CallCount++;
        return Task.FromResult(CopyTemplate());
    }

    private ConversionResult CopyTemplate() => new()
    {
        Success = _template.Success,
        Error = _template.Error,
        OutputPath = _template.OutputPath,
        ProcessingTimeMs = _template.ProcessingTimeMs,
        ConversionMethod = _template.ConversionMethod,
        FailureReason = _template.FailureReason
    };
}
