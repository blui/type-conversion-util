using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FileConversionApi.Models;
using FileConversionApi.Services.Interfaces;

namespace FileConversionApi.Services;

/// <summary>
/// LibreOffice integration service
/// Coordinates LibreOffice operations through dedicated components
/// </summary>
public class LibreOfficeService : ILibreOfficeService
{
    private readonly ILogger<LibreOfficeService> _logger;
    private readonly ILibreOfficeProcessManager _processManager;
    private readonly ILibreOfficePathResolver _pathResolver;

    public LibreOfficeService(
        ILogger<LibreOfficeService> logger,
        ILibreOfficeProcessManager processManager,
        ILibreOfficePathResolver pathResolver)
    {
        _logger = logger;
        _processManager = processManager;
        _pathResolver = pathResolver;
    }

    /// <inheritdoc/>
    public async Task<ConversionResult> ConvertAsync(string inputPath, string outputPath, string targetFormat)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Ensure output directory exists
            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // Delegate conversion to process manager
            var result = await _processManager.ConvertAsync(inputPath, outputPath, targetFormat);

            stopwatch.Stop();
            result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "LibreOffice conversion failed for {InputPath}", inputPath);

            return new ConversionResult
            {
                Success = false,
                Error = $"LibreOffice conversion failed: {ex.Message}",
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    /// <inheritdoc/>
    public async Task<bool> IsAvailableAsync()
    {
        try
        {
            // Check if LibreOffice executable exists and is accessible
            var executablePath = await _pathResolver.GetExecutablePathAsync();
            return File.Exists(executablePath);
        }
        catch
        {
            return false;
        }
    }

}
