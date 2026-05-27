using System.Threading.Tasks;
using FileConversionApi.Models;

namespace FileConversionApi.Services;

/// <summary>
/// iText7-backed PDF operations: build a PDF from a string of plain text, or extract page text
/// out of an existing PDF. Both are synchronous under the hood; the Task surface lets callers
/// honor cancellation through the file-I/O boundaries.
/// </summary>
public interface IPdfService
{
    /// <summary>
    /// Writes <paramref name="text"/> into a new PDF at <paramref name="outputPath"/> using the
    /// project's wrap-and-paginate convention.
    /// </summary>
    Task<ConversionResult> CreatePdfFromTextAsync(string text, string outputPath, CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads <paramref name="inputPath"/> page-by-page and writes the concatenated text to
    /// <paramref name="outputPath"/>. Honors cancellation between pages.
    /// </summary>
    Task<ConversionResult> ExtractTextFromPdfAsync(string inputPath, string outputPath, CancellationToken cancellationToken = default);
}
