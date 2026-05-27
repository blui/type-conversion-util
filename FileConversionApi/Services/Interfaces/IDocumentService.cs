using System.Threading.Tasks;
using FileConversionApi.Models;

namespace FileConversionApi.Services;

/// <summary>
/// Routes a single (inputFormat, targetFormat) conversion to the engine that handles it
/// (LibreOffice, the docx-to-html two-hop pipeline, iText7, NPOI, or DocumentFormat.OpenXml).
/// The handler dispatch table is the only place an (input, target) pair maps to a service.
/// </summary>
public interface IDocumentService
{
    /// <summary>
    /// Dispatches the conversion to its registered handler and returns the typed result.
    /// Returns a ConversionResult with Success=false (rather than throwing) for unsupported
    /// pairs and engine failures; throws OperationCanceledException only when the caller's
    /// token fired (client disconnect) so the controller can drop the response cleanly.
    /// </summary>
    Task<ConversionResult> ConvertAsync(
        string inputPath,
        string outputPath,
        string inputFormat,
        string targetFormat,
        CancellationToken cancellationToken = default);
}
