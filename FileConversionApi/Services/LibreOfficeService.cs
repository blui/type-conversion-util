using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FileConversionApi.Models;
using FileConversionApi.Services.Interfaces;
using FileConversionApi.Utilities;

namespace FileConversionApi.Services;

/// <summary>
/// Coordinates LibreOffice integration and delegates operations to process manager and path resolver.
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
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _processManager = processManager ?? throw new ArgumentNullException(nameof(processManager));
        _pathResolver = pathResolver ?? throw new ArgumentNullException(nameof(pathResolver));
    }

    /// <inheritdoc/>
    public async Task<ConversionResult> ConvertAsync(string inputPath, string outputPath, string targetFormat)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Ensure output directory exists
            FileSystemHelper.EnsureDirectoryExists(outputPath);

            // Delegate conversion to process manager
            var result = await _processManager.ConvertAsync(inputPath, outputPath, targetFormat);

            stopwatch.Stop();
            result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var inputFileName = PathSanitizer.GetSafeFileName(inputPath);
            _logger.LogError(ex, "LibreOffice conversion failed for file: {InputFile}", inputFileName);
            _logger.LogDebug("Full input path for debugging: {InputPath}", inputPath);

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
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "LibreOffice availability check failed");
            return false;
        }
    }

}
