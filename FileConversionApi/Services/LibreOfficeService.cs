using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FileConversionApi.Models;

namespace FileConversionApi.Services;

/// <summary>
/// LibreOffice SDK integration service
/// Handles direct LibreOffice operations via command line or SDK
/// </summary>
public class LibreOfficeService : ILibreOfficeService
{
    private readonly ILogger<LibreOfficeService> _logger;
    private readonly LibreOfficeConfig _config;

    public LibreOfficeService(
        ILogger<LibreOfficeService> logger,
        IOptions<LibreOfficeConfig> config)
    {
        _logger = logger;
        _config = config.Value;
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

            // Use CLI approach for LibreOffice conversion
            var result = await ConvertViaCliAsync(inputPath, outputPath, targetFormat);

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
            var executablePath = GetLibreOfficeExecutablePath();
            return File.Exists(executablePath);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Convert document using LibreOffice command line interface
    /// </summary>
    private async Task<ConversionResult> ConvertViaCliAsync(string inputPath, string outputPath, string targetFormat)
    {
        var executablePath = GetLibreOfficeExecutablePath();

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

        // Wait for completion with timeout
        var timeoutMs = 60000; // 60 seconds
        var completed = await Task.Run(() => process.WaitForExit(timeoutMs));

        if (!completed)
        {
            process.Kill();
            return new ConversionResult
            {
                Success = false,
                Error = $"LibreOffice conversion timed out after {timeoutMs}ms"
            };
        }

        var exitCode = process.ExitCode;
        var output = await process.StandardOutput.ReadToEndAsync();
        var error = await process.StandardError.ReadToEndAsync();

        if (exitCode != 0)
        {
            _logger.LogError("LibreOffice conversion failed with exit code {ExitCode}. Error: {Error}",
                exitCode, error);

            return new ConversionResult
            {
                Success = false,
                Error = $"LibreOffice conversion failed: {error}"
            };
        }

        // Verify output file was created
        if (!File.Exists(outputPath))
        {
            return new ConversionResult
            {
                Success = false,
                Error = $"Output file was not created: {outputPath}"
            };
        }

        return new ConversionResult
        {
            Success = true,
            OutputPath = outputPath
        };
    }

    /// <summary>
    /// Get the path to LibreOffice executable
    /// </summary>
    private string GetLibreOfficeExecutablePath()
    {
        // Check configured path first
        if (!string.IsNullOrEmpty(_config.ExecutablePath) && File.Exists(_config.ExecutablePath))
        {
            return _config.ExecutablePath;
        }

        // Check SDK path
        var sdkPath = Path.Combine(_config.SdkPath, "program", "soffice.exe");
        if (File.Exists(sdkPath))
        {
            return sdkPath;
        }

        // Check common installation paths
        var commonPaths = new[]
        {
            @"C:\Program Files\LibreOffice\program\soffice.exe",
            @"C:\Program Files (x86)\LibreOffice\program\soffice.exe"
        };

        foreach (var path in commonPaths)
        {
            if (File.Exists(path))
            {
                return path;
            }
        }

        // Default to SDK path as fallback
        return sdkPath;
    }
}
