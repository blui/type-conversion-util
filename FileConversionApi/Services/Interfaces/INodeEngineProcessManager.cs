using System.Threading.Tasks;
using FileConversionApi.Models;

namespace FileConversionApi.Services;

/// <summary>
/// Drives the bundled Node engine as a child process. The single supported direction is
/// PDF-&gt;HTML (hop 2 of the docx/doc-&gt;PDF-&gt;HTML pipeline; implemented by
/// engine/pdf-to-html.mjs).
/// </summary>
public interface INodeEngineProcessManager
{
    /// <summary>
    /// Convert an intermediate PDF to a single self-contained HTML file via
    /// engine/pdf-to-html.mjs. Used as hop 2 of the docx/doc-&gt;HTML pipeline.
    /// </summary>
    /// <param name="inputPath">Path to the source PDF.</param>
    /// <param name="outputPath">Path the self-contained HTML is written to.</param>
    /// <param name="cancellationToken">Surfaces client disconnect to the engine process wait.</param>
    Task<ConversionResult> ConvertPdfToHtmlAsync(
        string inputPath,
        string outputPath,
        CancellationToken cancellationToken = default);
}
