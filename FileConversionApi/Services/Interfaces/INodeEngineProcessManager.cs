using System.Threading.Tasks;
using FileConversionApi.Models;

namespace FileConversionApi.Services;

/// <summary>
/// Drives the bundled Node PDF-&gt;HTML engine as a child process (hop 2 of the
/// docx/doc-&gt;PDF-&gt;HTML pipeline). The engine only performs PDF-&gt;HTML, so there is no
/// targetFormat parameter.
/// </summary>
public interface INodeEngineProcessManager
{
    /// <summary>
    /// Convert an intermediate PDF to a single self-contained HTML file.
    /// </summary>
    /// <param name="inputPath">Path to the source PDF.</param>
    /// <param name="outputPath">Path the self-contained HTML is written to.</param>
    /// <param name="cancellationToken">Surfaces client disconnect to the engine process wait.</param>
    Task<ConversionResult> ConvertAsync(
        string inputPath,
        string outputPath,
        CancellationToken cancellationToken = default);
}
