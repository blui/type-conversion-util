using Microsoft.Extensions.Logging;

namespace FileConversionApi.Services;

/// <summary>
/// Pre-processing service for document normalization
/// Handles document pre-processing to improve conversion quality and compatibility
/// </summary>
public class PreprocessingService : IPreprocessingService
{
    private readonly ILogger<PreprocessingService> _logger;
    private readonly IDocxPreProcessor _docxPreProcessor;

    public PreprocessingService(
        ILogger<PreprocessingService> logger,
        IDocxPreProcessor docxPreProcessor)
    {
        _logger = logger;
        _docxPreProcessor = docxPreProcessor;
    }

    /// <inheritdoc/>
    public async Task<PreprocessingResult> PreprocessDocxAsync(string inputPath, string outputPath)
    {
        try
        {
            _logger.LogInformation("Pre-processing DOCX file: {InputPath}", inputPath);

            var result = await _docxPreProcessor.ProcessAsync(inputPath, outputPath);

            if (result.Success)
            {
                _logger.LogInformation("DOCX pre-processing completed successfully");
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "DOCX pre-processing failed, proceeding with original file");

            // Return fallback result - allows conversion to continue with original file
            return new PreprocessingResult
            {
                Success = false,
                Error = ex.Message,
                ProcessingTimeMs = 0
            };
        }
    }

    /// <inheritdoc/>
    public PreprocessingCapabilities GetCapabilities()
    {
        return new PreprocessingCapabilities
        {
            Available = true,
            SupportedFormats = new[] { "docx" },
            Features = new[]
            {
                "Font normalization",
                "Theme color conversion",
                "Style simplification",
                "Bold formatting fixes",
                "LibreOffice compatibility optimization"
            }
        };
    }
}

/// <summary>
/// Preprocessing capabilities information
/// </summary>
public class PreprocessingCapabilities
{
    public bool Available { get; set; }
    public string[]? SupportedFormats { get; set; }
    public string[]? Features { get; set; }
}

/// <summary>
/// Preprocessing service interface
/// </summary>
public interface IPreprocessingService
{
    Task<PreprocessingResult> PreprocessDocxAsync(string inputPath, string outputPath);
    PreprocessingCapabilities GetCapabilities();
}
