using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Diagnostics;
using FileConversionApi.Models;
using FileConversionApi.Utilities;

namespace FileConversionApi.Services;

/// <summary>
/// Coordinates document conversions through LibreOffice
/// </summary>
public class ConversionEngine : IConversionEngine
{
    private readonly ILogger<ConversionEngine> _logger;
    private readonly ILibreOfficeService _libreOfficeService;
    private readonly IPreprocessingService _preprocessingService;
    private readonly PreprocessingConfig _preprocessingConfig;

    public ConversionEngine(
        ILogger<ConversionEngine> logger,
        ILibreOfficeService libreOfficeService,
        IPreprocessingService preprocessingService,
        IOptions<PreprocessingConfig> preprocessingConfig)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _libreOfficeService = libreOfficeService ?? throw new ArgumentNullException(nameof(libreOfficeService));
        _preprocessingService = preprocessingService ?? throw new ArgumentNullException(nameof(preprocessingService));
        _preprocessingConfig = preprocessingConfig?.Value ?? throw new ArgumentNullException(nameof(preprocessingConfig));
    }

    /// <inheritdoc/>
    public async Task<ConversionResult> DocToPdfAsync(string inputPath, string outputPath)
    {
        // For DOC files: Convert DOC→DOCX first, preprocess, then convert to PDF
        // This allows us to remove page borders and apply other optimizations
        if (_preprocessingConfig.EnableDocxPreprocessing)
        {
            _logger.LogInformation("Converting DOC to DOCX for preprocessing before PDF conversion");

            var tempDocx = FileSystemHelper.GetTempFilePath("docx");

            try
            {
                // Step 1: Convert DOC to DOCX
                var docxResult = await _libreOfficeService.ConvertAsync(inputPath, tempDocx, "docx");
                if (!docxResult.Success)
                {
                    return docxResult;
                }

                // Step 2: Preprocess the DOCX
                var preprocessedDocx = FileSystemHelper.GetTempFilePath("docx");
                var preprocessResult = await _preprocessingService.PreprocessDocxAsync(tempDocx, preprocessedDocx);

                // Step 3: Convert preprocessed DOCX to PDF
                var pdfResult = await ConvertToPdfAsync(
                    preprocessResult.Success ? preprocessedDocx : tempDocx,
                    outputPath,
                    "DOC (via DOCX)"
                );

                // Cleanup temp files
                FileSystemHelper.SafeDeleteFile(tempDocx);
                FileSystemHelper.SafeDeleteFile(preprocessedDocx);

                return pdfResult;
            }
            catch (Exception ex)
            {
                FileSystemHelper.SafeDeleteFile(tempDocx);
                _logger.LogError(ex, "Error in DOC to PDF conversion with preprocessing");
                throw;
            }
        }

        return await ConvertToPdfAsync(inputPath, outputPath, "DOC");
    }

    /// <inheritdoc/>
    public async Task<ConversionResult> DocxToPdfAsync(string inputPath, string outputPath)
    {
        // Preprocess DOCX if enabled (removes page borders, normalizes fonts, etc.)
        if (_preprocessingConfig.EnableDocxPreprocessing)
        {
            _logger.LogInformation("Preprocessing DOCX before PDF conversion");

            var preprocessedPath = FileSystemHelper.GetTempFilePath("docx");

            try
            {
                var preprocessResult = await _preprocessingService.PreprocessDocxAsync(inputPath, preprocessedPath);

                var pdfResult = await ConvertToPdfAsync(
                    preprocessResult.Success ? preprocessedPath : inputPath,
                    outputPath,
                    "DOCX"
                );

                // Cleanup temp file
                FileSystemHelper.SafeDeleteFile(preprocessedPath);

                return pdfResult;
            }
            catch (Exception ex)
            {
                FileSystemHelper.SafeDeleteFile(preprocessedPath);
                _logger.LogError(ex, "Error in DOCX to PDF conversion with preprocessing");
                throw;
            }
        }

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
