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
        var outputDirectory = Path.GetDirectoryName(outputPath) ?? Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);
        var expectedOutputPath = Path.Combine(outputDirectory, expectedOutputFileName);

        // Create LibreOffice user profile directory in App_Data to avoid permission issues
        // This prevents "User Install Failed" errors when running under IIS
        var userProfileDir = Path.Combine(AppContext.BaseDirectory, "App_Data", "libreoffice-profile");
        try
        {
            Directory.CreateDirectory(userProfileDir);
            _logger.LogDebug("Created LibreOffice user profile directory: {ProfileDir}", userProfileDir);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create LibreOffice user profile directory: {ProfileDir}", userProfileDir);
        }

        // Verify input file exists and is readable
        if (!File.Exists(inputPath))
        {
            _logger.LogError("Input file does not exist: {InputPath}", inputPath);
            return new ConversionResult
            {
                Success = false,
                Error = $"Input file not found: {inputPath}"
            };
        }

        var inputFileInfo = new FileInfo(inputPath);
        _logger.LogDebug("Input file: {Path}, Size: {Size} bytes", inputPath, inputFileInfo.Length);

        // Build command arguments for headless conversion
        // -env:UserInstallation specifies where LibreOffice stores its user profile
        // Convert Windows path to proper file URI (file:///C:/path/to/dir format)
        var userProfileUri = new Uri(userProfileDir).AbsoluteUri;
        var arguments = $"--headless --nofirststartwizard -env:UserInstallation={userProfileUri} --convert-to {targetFormat} --outdir \"{outputDirectory}\" \"{inputPath}\"";

        _logger.LogInformation("Executing LibreOffice conversion: {Executable} {Arguments}",
            executablePath, arguments);
        _logger.LogDebug("Working directory: {WorkingDir}", Path.GetDirectoryName(executablePath));
        _logger.LogDebug("Output directory: {OutputDir}", outputDirectory);
        _logger.LogDebug("Expected output: {ExpectedPath}", expectedOutputPath);

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

        // Read output and error streams concurrently to prevent blocking
        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

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
        var output = await outputTask;
        var error = await errorTask;

        _logger.LogInformation("LibreOffice process completed. ExitCode: {ExitCode}, Output: '{Output}', Error: '{Error}'",
            exitCode, output, error);

        // Check for specific LibreOffice failure exit codes
        if (exitCode == -1073741515) // 0xC0000135 - DLL not found
        {
            _logger.LogError("LibreOffice failed with exit code -1073741515 (DLL_NOT_FOUND). Missing Visual C++ Redistributable or LibreOffice dependencies.");
            return new ConversionResult
            {
                Success = false,
                Error = "LibreOffice process failed to start. The server may be missing Visual C++ Redistributable (2015-2022) or LibreOffice dependencies. Exit code: -1073741515"
            };
        }

        if (exitCode != 0)
        {
            var errorMessage = !string.IsNullOrEmpty(error) ? error : output;

            // Log additional diagnostics for exit code 1 (generic error)
            if (exitCode == 1)
            {
                _logger.LogError("LibreOffice exit code 1 - Common causes:");
                _logger.LogError("  - File format not supported or corrupted");
                _logger.LogError("  - User profile directory permissions issue");
                _logger.LogError("  - Missing LibreOffice configuration files");
                _logger.LogError("  - Output directory not writable");

                // Check if output directory is writable
                try
                {
                    var testFile = Path.Combine(outputDirectory, $"writetest_{Guid.NewGuid()}.tmp");
                    File.WriteAllText(testFile, "test");
                    File.Delete(testFile);
                    _logger.LogDebug("Output directory is writable");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Output directory is NOT writable: {OutputDir}", outputDirectory);
                }

                // Check if user profile directory exists and is writable
                if (!Directory.Exists(userProfileDir))
                {
                    _logger.LogError("User profile directory does not exist: {ProfileDir}", userProfileDir);
                }
                else
                {
                    _logger.LogDebug("User profile directory exists: {ProfileDir}", userProfileDir);
                }
            }

            _logger.LogError("LibreOffice process failed with exit code {ExitCode}: {ErrorMessage}", exitCode, errorMessage);
            return new ConversionResult
            {
                Success = false,
                Error = $"LibreOffice conversion failed with exit code {exitCode}: {errorMessage}"
            };
        }

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
