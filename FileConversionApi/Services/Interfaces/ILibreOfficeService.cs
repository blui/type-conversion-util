using System.Threading.Tasks;
using FileConversionApi.Models;

namespace FileConversionApi.Services;

/// <summary>
/// Thin facade over <see cref="FileConversionApi.Services.Interfaces.ILibreOfficeProcessManager"/>.
/// Ensures the output directory exists, forwards the call to the process manager, and stamps
/// ProcessingTimeMs on the result. Does not own LibreOffice lifecycle, profile creation, or
/// argv construction (the process manager does).
/// </summary>
public interface ILibreOfficeService
{
    /// <summary>
    /// Forwards to the process manager and returns the timed result.
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
