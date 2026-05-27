using FileConversionApi.Models;

namespace FileConversionApi.Services.Interfaces;

/// <summary>
/// Owns the soffice.exe child-process lifecycle for a single conversion: per-conversion
/// profile dir, --headless argv via ArgumentList (no shell), linked-CTS timeout, kill-on-cancel,
/// and File.Move of the produced output back to the caller-requested path. Returns a typed
/// FailureReason.Timeout on internal timeout; throws OperationCanceledException only when the
/// caller's token fired.
/// </summary>
public interface ILibreOfficeProcessManager
{
    /// <summary>
    /// Spawns soffice.exe, waits for it under the linked cancellation token, and returns
    /// the typed conversion outcome.
    /// </summary>
    Task<ConversionResult> ConvertAsync(
        string inputPath,
        string outputPath,
        string targetFormat,
        CancellationToken cancellationToken = default);
}
