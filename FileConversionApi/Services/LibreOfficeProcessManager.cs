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

        // Use App_Data for LibreOffice profile - IIS_IUSRS has access here
        // Copy pre-initialized profile template to avoid initialization issues
        var profileBaseDir = Path.Combine(AppContext.BaseDirectory, "App_Data", "libreoffice-profiles");
        Directory.CreateDirectory(profileBaseDir);

        var tempProfileDir = Path.Combine(profileBaseDir, Guid.NewGuid().ToString());

        // Check if we have a bundled profile template to copy
        var profileTemplate = Path.Combine(AppContext.BaseDirectory, "libreoffice-profile-template");
        if (Directory.Exists(profileTemplate))
        {
            // Copy the pre-initialized template for this conversion
            CopyDirectory(profileTemplate, tempProfileDir);
            _logger.LogDebug("Copied LibreOffice profile template to: {ProfileDir}", tempProfileDir);
        }
        else
        {
            // Fallback: Let LibreOffice create profile (requires read access to LibreOffice/share)
            _logger.LogWarning("Profile template not found at {TemplatePath}, LibreOffice will create profile", profileTemplate);
            // Do NOT create directory - LibreOffice must create it
        }

        // Build command arguments for headless conversion
        // -env:UserInstallation specifies where LibreOffice stores its user profile
        // Convert Windows path to proper file URI (file:///C:/path/to/dir format)
        var userProfileUri = new Uri(tempProfileDir).AbsoluteUri;
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
                if (!Directory.Exists(tempProfileDir))
                {
                    _logger.LogError("User profile directory does not exist: {ProfileDir}", tempProfileDir);
                }
                else
                {
                    _logger.LogDebug("User profile directory exists: {ProfileDir}", tempProfileDir);
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

        // Clean up temporary LibreOffice profile directory
        try
        {
            if (Directory.Exists(tempProfileDir))
            {
                Directory.Delete(tempProfileDir, recursive: true);
                _logger.LogDebug("Cleaned up temporary LibreOffice profile: {ProfileDir}", tempProfileDir);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to clean up temporary LibreOffice profile: {ProfileDir}", tempProfileDir);
        }

        return new ConversionResult
        {
            Success = true,
            OutputPath = outputPath
        };
    }

    /// <summary>
    /// Recursively copies a directory and all its contents
    /// </summary>
    private static void CopyDirectory(string sourceDir, string destDir)
    {
        // Create destination directory
        Directory.CreateDirectory(destDir);

        // Copy all files
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var fileName = Path.GetFileName(file);
            var destFile = Path.Combine(destDir, fileName);
            File.Copy(file, destFile, overwrite: true);
        }

        // Recursively copy subdirectories
        foreach (var subDir in Directory.GetDirectories(sourceDir))
        {
            var dirName = Path.GetFileName(subDir);
            var destSubDir = Path.Combine(destDir, dirName);
            CopyDirectory(subDir, destSubDir);
        }
    }
}
