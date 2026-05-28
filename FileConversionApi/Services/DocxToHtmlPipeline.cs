using Microsoft.Extensions.Logging;
using FileConversionApi.Models;
using FileConversionApi.Services.Interfaces;
using FileConversionApi.Utilities;

namespace FileConversionApi.Services;

/// <summary>
/// Coordinates the two-hop docx/doc-&gt;HTML conversion: LibreOffice docx-&gt;PDF (hop 1, the
/// existing layer, unchanged) then the bundled Node engine PDF-&gt;HTML (hop 2).
/// </summary>
public class DocxToHtmlPipeline
{
    private readonly ILogger<DocxToHtmlPipeline> _logger;
    private readonly ILibreOfficeProcessManager _libreOffice;
    private readonly INodeEngineProcessManager _nodeEngine;

    public DocxToHtmlPipeline(
        ILogger<DocxToHtmlPipeline> logger,
        ILibreOfficeProcessManager libreOffice,
        INodeEngineProcessManager nodeEngine)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _libreOffice = libreOffice ?? throw new ArgumentNullException(nameof(libreOffice));
        _nodeEngine = nodeEngine ?? throw new ArgumentNullException(nameof(nodeEngine));
    }

    /// <inheritdoc/>
    public async Task<ConversionResult> ConvertAsync(
        string inputDocPath,
        string outputHtmlPath,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var outputDir = Path.GetDirectoryName(outputHtmlPath)
                ?? Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);
            var stem = Path.GetFileNameWithoutExtension(inputDocPath);
            var intermediatePdf = Path.Combine(outputDir, stem + ".pdf");

            try
            {
                // Hop 1: existing LibreOffice layer (unchanged). It File.Moves the produced PDF to intermediatePdf.
                var hop1 = await _libreOffice.ConvertAsync(inputDocPath, intermediatePdf, "pdf", cancellationToken);
                if (!hop1.Success)
                {
                    _logger.LogError("docx->html hop 1 (LibreOffice docx->PDF) failed - File: {InputFile}",
                        PathSanitizer.GetSafeFileName(inputDocPath));
                    _logger.LogDebug("Hop 1 raw failure detail for debugging: {Error}", hop1.Error);
                    return hop1;
                }

                // Hop 2: bundled Node engine. The engine writes one self-contained .html at outputHtmlPath
                // with all images inlined as data:image/png URIs and emits no sidecar file, so there is no
                // sidecar artifact to clean up after this hop.
                return await _nodeEngine.ConvertPdfToHtmlAsync(intermediatePdf, outputHtmlPath, cancellationToken);
            }
            finally
            {
                // Guaranteed cleanup of the intermediate PDF on every exit of the hop pipeline,
                // including a hop-2 exception. Best-effort: never throws, never changes the result.
                TryDeleteIntermediate(intermediatePdf);
            }
        }
        catch (OperationCanceledException)
        {
            // Propagate cancellation upward (client disconnect or internal timeout); the
            // DocumentService layer distinguishes which case fired.
            throw;
        }
        catch (Exception ex)
        {
            var inputFileName = PathSanitizer.GetSafeFileName(inputDocPath);
            _logger.LogError(ex, "docx->html pipeline failed for file: {InputFile}", inputFileName);
            _logger.LogDebug("Full input path for debugging: {InputPath}", inputDocPath);

            return new ConversionResult
            {
                Success = false,
                Error = $"docx->html conversion failed: {ex.Message}"
            };
        }
    }

    /// <summary>
    /// Best-effort deletion of the intermediate PDF. Never throws (cleanup must not fail a
    /// successful conversion).
    /// </summary>
    private void TryDeleteIntermediate(string intermediatePdf)
    {
        try
        {
            if (File.Exists(intermediatePdf))
            {
                File.Delete(intermediatePdf);
                _logger.LogDebug("Deleted intermediate PDF: {Path}", intermediatePdf);
            }
        }
        catch (Exception ex)
        {
            var fileName = PathSanitizer.GetSafeFileName(intermediatePdf);
            _logger.LogWarning(ex, "Failed to delete intermediate PDF: {File}", fileName);
        }
    }
}
