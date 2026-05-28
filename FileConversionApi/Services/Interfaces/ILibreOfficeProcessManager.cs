using FileConversionApi.Models;

namespace FileConversionApi.Services.Interfaces;

/// <summary>
/// Owns the soffice.exe child-process lifecycle for a single conversion: per-conversion
/// profile dir, --headless argv via ArgumentList (no shell), linked-CTS timeout, kill-on-cancel,
/// and File.Move of the produced output back to the caller-requested path. Stamps
/// <see cref="ConversionResult.ProcessingTimeMs"/> on the result. Returns a typed
/// <see cref="FailureReason.Timeout"/> on internal timeout; throws OperationCanceledException
/// only when the caller's token fired. Exposed behind an interface as the integration-test
/// seam for the LibreOffice engine.
/// </summary>
public interface ILibreOfficeProcessManager
{
    /// <summary>
    /// Spawns soffice.exe, waits for it under the linked cancellation token, and returns
    /// the typed conversion outcome with <see cref="ConversionResult.ProcessingTimeMs"/> set.
    /// </summary>
    Task<ConversionResult> ConvertAsync(
        string inputPath,
        string outputPath,
        string targetFormat,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns true when the configured LibreOffice executable resolves to a file that exists.
    /// Used by the /health probe; does not invoke the engine.
    /// </summary>
    Task<bool> IsAvailableAsync();
}
