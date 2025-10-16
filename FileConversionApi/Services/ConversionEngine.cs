using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FileConversionApi.Models;

namespace FileConversionApi.Services;

/// <summary>
/// Document conversion engine implementation
/// Coordinates high-level document conversions using LibreOffice
/// </summary>
public class ConversionEngine : IConversionEngine
{
    private readonly ILogger<ConversionEngine> _logger;
    private readonly LibreOfficeConfig _libreOfficeConfig;
    private readonly ILibreOfficeService _libreOfficeService;

    public ConversionEngine(
        ILogger<ConversionEngine> logger,
        IOptions<LibreOfficeConfig> libreOfficeConfig,
        ILibreOfficeService libreOfficeService)
    {
        _logger = logger;
        _libreOfficeConfig = libreOfficeConfig.Value;
        _libreOfficeService = libreOfficeService;
    }

    /// <inheritdoc/>
    public async Task<ConversionResult> DocxToPdfAsync(string inputPath, string outputPath)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Converting DOCX to PDF: {InputPath} -> {OutputPath}",
                inputPath, outputPath);

            var result = await _libreOfficeService.ConvertAsync(inputPath, outputPath, "pdf");

            stopwatch.Stop();

            result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
            result.ConversionMethod = "LibreOffice";

            if (result.Success)
            {
                _logger.LogInformation("DOCX to PDF conversion completed successfully in {Time}ms",
                    result.ProcessingTimeMs);
            }
            else
            {
                _logger.LogError("DOCX to PDF conversion failed: {Error}", result.Error);
            }

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Unexpected error during DOCX to PDF conversion");

            return new ConversionResult
            {
                Success = false,
                Error = $"Conversion failed: {ex.Message}",
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    /// <inheritdoc/>
    public async Task<ConversionResult> XlsxToPdfAsync(string inputPath, string outputPath)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Converting XLSX to PDF: {InputPath} -> {OutputPath}",
                inputPath, outputPath);

            var result = await _libreOfficeService.ConvertAsync(inputPath, outputPath, "pdf");

            stopwatch.Stop();

            result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
            result.ConversionMethod = "LibreOffice";

            if (result.Success)
            {
                _logger.LogInformation("XLSX to PDF conversion completed successfully in {Time}ms",
                    result.ProcessingTimeMs);
            }
            else
            {
                _logger.LogError("XLSX to PDF conversion failed: {Error}", result.Error);
            }

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Unexpected error during XLSX to PDF conversion");

            return new ConversionResult
            {
                Success = false,
                Error = $"Conversion failed: {ex.Message}",
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    /// <inheritdoc/>
    public async Task<ConversionResult> PptxToPdfAsync(string inputPath, string outputPath)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Converting PPTX to PDF: {InputPath} -> {OutputPath}",
                inputPath, outputPath);

            var result = await _libreOfficeService.ConvertAsync(inputPath, outputPath, "pdf");

            stopwatch.Stop();

            result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
            result.ConversionMethod = "LibreOffice";

            if (result.Success)
            {
                _logger.LogInformation("PPTX to PDF conversion completed successfully in {Time}ms",
                    result.ProcessingTimeMs);
            }
            else
            {
                _logger.LogError("PPTX to PDF conversion failed: {Error}", result.Error);
            }

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Unexpected error during PPTX to PDF conversion");

            return new ConversionResult
            {
                Success = false,
                Error = $"Conversion failed: {ex.Message}",
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
    }
}
