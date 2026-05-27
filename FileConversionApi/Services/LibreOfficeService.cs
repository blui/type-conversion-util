using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FileConversionApi.Models;
using FileConversionApi.Services.Interfaces;
using FileConversionApi.Utilities;

namespace FileConversionApi.Services;

/// <summary>
/// Thin facade over the LibreOffice process manager. Resolves the executable through
/// <see cref="ILibreOfficePathResolver"/>, then forwards conversion calls to
/// <see cref="ILibreOfficeProcessManager"/>; collects stopwatch timing on the call.
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
    public async Task<ConversionResult> ConvertAsync(
        string inputPath,
        string outputPath,
        string targetFormat,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            FileSystemHelper.EnsureDirectoryExists(outputPath);

            var result = await _processManager.ConvertAsync(inputPath, outputPath, targetFormat, cancellationToken);

            stopwatch.Stop();
            result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;

            return result;
        }
        catch (OperationCanceledException)
        {
            // Propagate cancellation upward; DocumentService distinguishes client-cancel from timeout.
            throw;
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
