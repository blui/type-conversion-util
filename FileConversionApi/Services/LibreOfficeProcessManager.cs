using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FileConversionApi.Models;
using FileConversionApi.Services.Interfaces;

namespace FileConversionApi.Services;

/// <summary>
/// Manages LibreOffice process lifecycle for document conversions
/// Handles process spawning, timeout enforcement, and cleanup
/// </summary>
public class LibreOfficeProcessManager : ILibreOfficeProcessManager
{
    private readonly ILogger<LibreOfficeProcessManager> _logger;
    private readonly ILibreOfficePathResolver _pathResolver;
    private readonly LibreOfficeConfig _config;

    public LibreOfficeProcessManager(
        ILogger<LibreOfficeProcessManager> logger,
        ILibreOfficePathResolver pathResolver,
        IOptions<LibreOfficeConfig> libreOfficeConfig)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _pathResolver = pathResolver ?? throw new ArgumentNullException(nameof(pathResolver));
        _config = libreOfficeConfig?.Value ?? throw new ArgumentNullException(nameof(libreOfficeConfig));
    }

    /// <inheritdoc/>
    public async Task<ConversionResult> ConvertAsync(string inputPath, string outputPath, string targetFormat)
    {
        var executablePath = await _pathResolver.GetExecutablePathAsync();

        if (!File.Exists(executablePath))
        {
            return new ConversionResult
            {
                Success = false,
                Error = $"LibreOffice executable not found at {executablePath}"
            };
        }

        // Compute expected output file name based on LibreOffice naming convention
        var inputFileName = Path.GetFileNameWithoutExtension(inputPath);
        var expectedOutputFileName = $"{inputFileName}.{targetFormat}";
        var outputDirectory = Path.GetDirectoryName(outputPath) ?? string.Empty;
        var expectedOutputPath = Path.Combine(outputDirectory, expectedOutputFileName);

        // Build command arguments for headless conversion
        var arguments = $"--headless --convert-to {targetFormat} --outdir \"{Path.GetDirectoryName(outputPath)}\" \"{inputPath}\"";

        _logger.LogInformation("Executing LibreOffice conversion: {Executable} {Arguments}",
            executablePath, arguments);

        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            Arguments = arguments,
            WorkingDirectory = Path.GetDirectoryName(executablePath),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using var process = Process.Start(startInfo);

        if (process == null)
        {
            return new ConversionResult
            {
                Success = false,
                Error = "Failed to start LibreOffice process"
            };
        }

        // Wait for completion with timeout using proper async pattern
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_config.TimeoutSeconds));
        try
        {
            await process.WaitForExitAsync(cts.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill();
            return new ConversionResult
            {
                Success = false,
                Error = $"LibreOffice conversion timed out after {_config.TimeoutSeconds} seconds"
            };
        }

        var exitCode = process.ExitCode;
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();

        _logger.LogInformation("LibreOffice process completed. ExitCode: {ExitCode}, Output: '{Output}', Error: '{Error}'",
            exitCode, output, error);

        // Check if LibreOffice created the expected output file
        if (!File.Exists(expectedOutputPath))
        {
            _logger.LogError("Expected output file not found at: {ExpectedPath}", expectedOutputPath);

            // Log additional debugging info
            var tempDir = Path.GetDirectoryName(outputPath);
            if (Directory.Exists(tempDir))
            {
                var tempFiles = Directory.GetFiles(tempDir, "*.*");
                _logger.LogInformation("Files in temp directory: {Files}", string.Join(", ", tempFiles));
            }

            return new ConversionResult
            {
                Success = false,
                Error = $"Expected output file was not created: {expectedOutputPath}"
            };
        }

        // Move the file to the requested output path if different
        if (!string.Equals(expectedOutputPath, outputPath, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                File.Move(expectedOutputPath, outputPath);
                _logger.LogInformation("Renamed output file from {ExpectedPath} to {OutputPath}",
                    expectedOutputPath, outputPath);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to rename output file from {ExpectedPath} to {OutputPath}",
                    expectedOutputPath, outputPath);
                return new ConversionResult
                {
                    Success = false,
                    Error = $"Failed to rename output file: {ex.Message}"
                };
            }
        }

        if (exitCode != 0)
        {
            _logger.LogWarning("LibreOffice completed with non-zero exit code {ExitCode}, but output file exists. Error: {Error}",
                exitCode, error);
        }

        return new ConversionResult
        {
            Success = true,
            OutputPath = outputPath
        };
    }
}
