using System.Threading.Tasks;
using FileConversionApi.Models;

namespace FileConversionApi.Services;

/// <summary>
/// Two-hop docx/doc-&gt;HTML pipeline: LibreOffice produces an intermediate PDF (hop 1),
/// then the bundled Node engine renders a single self-contained HTML (hop 2).
/// </summary>
public interface IDocxToHtmlPipeline
{
    /// <summary>
    /// Convert a Word document to a single self-contained HTML file via the two-hop pipeline.
    /// </summary>
    /// <param name="inputDocPath">Path to the source .doc/.docx.</param>
    /// <param name="outputHtmlPath">Path the self-contained HTML is written to.</param>
    /// <param name="cancellationToken">Surfaces client disconnect (HttpContext.RequestAborted) to the underlying process waits.</param>
    Task<ConversionResult> ConvertAsync(
        string inputDocPath,
        string outputHtmlPath,
        CancellationToken cancellationToken = default);
}
