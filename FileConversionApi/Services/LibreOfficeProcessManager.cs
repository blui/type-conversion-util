using System.Diagnostics;
using Microsoft.Extensions.Logging;
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
    private const int DEFAULT_TIMEOUT_MS = 60000;

    public LibreOfficeProcessManager(
        ILogger<LibreOfficeProcessManager> logger,
        ILibreOfficePathResolver pathResolver)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _pathResolver = pathResolver ?? throw new ArgumentNullException(nameof(pathResolver));
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

        // Wait for completion with configurable timeout
        var completed = await Task.Run(() => process.WaitForExit(DEFAULT_TIMEOUT_MS));

        if (!completed)
        {
            process.Kill();
            return new ConversionResult
            {
                Success = false,
                Error = $"LibreOffice conversion timed out after {DEFAULT_TIMEOUT_MS}ms"
            };
        }

        var exitCode = process.ExitCode;
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();

        _logger.LogInformation("LibreOffice process completed. ExitCode: {ExitCode}, Output: '{Output}', Error: '{Error}'",
            exitCode, output, error);

        // Check if output file exists regardless of exit code
        if (!File.Exists(outputPath))
        {
            _logger.LogError("Output file not found at: {OutputPath}", outputPath);

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
                Error = $"Output file was not created: {outputPath}"
            };
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
