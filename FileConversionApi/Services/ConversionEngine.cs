using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using FileConversionApi.Models;

namespace FileConversionApi.Services;

/// <summary>
/// Coordinates document conversions through LibreOffice
/// </summary>
public class ConversionEngine : IConversionEngine
{
    private readonly ILogger<ConversionEngine> _logger;
    private readonly ILibreOfficeService _libreOfficeService;

    public ConversionEngine(
        ILogger<ConversionEngine> logger,
        ILibreOfficeService libreOfficeService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _libreOfficeService = libreOfficeService ?? throw new ArgumentNullException(nameof(libreOfficeService));
    }

    /// <inheritdoc/>
    public async Task<ConversionResult> DocToPdfAsync(string inputPath, string outputPath)
    {
        return await ConvertToPdfAsync(inputPath, outputPath, "DOC");
    }

    /// <inheritdoc/>
    public async Task<ConversionResult> DocxToPdfAsync(string inputPath, string outputPath)
    {
        return await ConvertToPdfAsync(inputPath, outputPath, "DOCX");
    }

    /// <inheritdoc/>
    public async Task<ConversionResult> XlsxToPdfAsync(string inputPath, string outputPath)
    {
        return await ConvertToPdfAsync(inputPath, outputPath, "XLSX");
    }

    /// <inheritdoc/>
    public async Task<ConversionResult> PptxToPdfAsync(string inputPath, string outputPath)
    {
        return await ConvertToPdfAsync(inputPath, outputPath, "PPTX");
    }

    /// <inheritdoc/>
    public async Task<ConversionResult> OdtToPdfAsync(string inputPath, string outputPath)
    {
        return await ConvertToPdfAsync(inputPath, outputPath, "ODT");
    }

    /// <inheritdoc/>
    public async Task<ConversionResult> OdsToPdfAsync(string inputPath, string outputPath)
    {
        return await ConvertToPdfAsync(inputPath, outputPath, "ODS");
    }

    /// <inheritdoc/>
    public async Task<ConversionResult> OdpToPdfAsync(string inputPath, string outputPath)
    {
        return await ConvertToPdfAsync(inputPath, outputPath, "ODP");
    }

    private async Task<ConversionResult> ConvertToPdfAsync(string inputPath, string outputPath, string formatName)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Converting {Format} to PDF: {InputPath}", formatName, inputPath);

            var result = await _libreOfficeService.ConvertAsync(inputPath, outputPath, "pdf");

            stopwatch.Stop();
            result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
            result.ConversionMethod = "LibreOffice";

            if (result.Success)
            {
                _logger.LogInformation("{Format} to PDF conversion completed in {Time}ms",
                    formatName, result.ProcessingTimeMs);
            }
            else
            {
                _logger.LogError("{Format} to PDF conversion failed: {Error}", formatName, result.Error);
            }

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error converting {Format} to PDF: {Message}", formatName, ex.Message);

            return new ConversionResult
            {
                Success = false,
                Error = $"Conversion failed: {ex.Message}",
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
    }
}
