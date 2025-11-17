using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FileConversionApi.Models;
using FileConversionApi.Services.Interfaces;
using FileConversionApi.Utilities;

namespace FileConversionApi.Services;

/// <summary>
/// Manages LibreOffice process lifecycle including spawning, timeout enforcement, and cleanup.
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
            var fileName = PathSanitizer.GetSafeFileName(inputPath);
            _logger.LogError("Input file does not exist - File: {FileName}", fileName);
            _logger.LogDebug("Full input path for debugging: {InputPath}", inputPath);

            return new ConversionResult
            {
                Success = false,
                Error = $"Input file not found: {fileName}"
            };
        }

        // Use App_Data for LibreOffice profile (IIS_IUSRS has access)
        var profileBaseDir = Path.Combine(AppContext.BaseDirectory, "App_Data", "libreoffice-profiles");
        Directory.CreateDirectory(profileBaseDir);

        var tempProfileDir = Path.Combine(profileBaseDir, UniqueIdGenerator.GenerateId());

        var profileTemplate = Path.Combine(AppContext.BaseDirectory, "libreoffice-profile-template");
        if (Directory.Exists(profileTemplate))
        {
            CopyDirectory(profileTemplate, tempProfileDir);
        }
        else
        {
            _logger.LogWarning("Profile template not found at {TemplatePath}, LibreOffice will create profile", profileTemplate);
        }

        // Convert Windows path to file URI for -env:UserInstallation
        var userProfileUri = new Uri(tempProfileDir).AbsoluteUri;

        // Validate targetFormat to prevent command injection (defense in depth)
        if (!IsValidTargetFormat(targetFormat))
        {
            _logger.LogError("Invalid target format detected: {TargetFormat}", targetFormat);
            return new ConversionResult
            {
                Success = false,
                Error = $"Invalid target format: {targetFormat}"
            };
        }

        // Use ArgumentList instead of Arguments string to prevent command injection
        // Each argument is automatically escaped and prevents injection attacks
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = Path.GetDirectoryName(executablePath),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        // Build argument list with proper escaping
        startInfo.ArgumentList.Add("--headless");
        startInfo.ArgumentList.Add("--nofirststartwizard");
        startInfo.ArgumentList.Add($"-env:UserInstallation={userProfileUri}");
        startInfo.ArgumentList.Add("--convert-to");
        startInfo.ArgumentList.Add(targetFormat);
        startInfo.ArgumentList.Add("--outdir");
        startInfo.ArgumentList.Add(outputDirectory);
        startInfo.ArgumentList.Add(inputPath);

        _logger.LogInformation("Executing LibreOffice conversion: {Executable} with format {TargetFormat}",
            executablePath, targetFormat);

        using var process = Process.Start(startInfo);

        if (process == null)
        {
            return new ConversionResult
            {
                Success = false,
                Error = "Failed to start LibreOffice process"
            };
        }

        var outputTask = process.StandardOutput.ReadToEndAsync();
        var errorTask = process.StandardError.ReadToEndAsync();

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
        if (exitCode == Constants.WindowsExitCodes.DllNotFound)
        {
            _logger.LogError("LibreOffice failed with exit code {ExitCode} (DLL_NOT_FOUND). Missing Visual C++ Redistributable or LibreOffice dependencies.", Constants.WindowsExitCodes.DllNotFound);
            return new ConversionResult
            {
                Success = false,
                Error = $"LibreOffice process failed to start. The server may be missing Visual C++ Redistributable (2015-2022) or LibreOffice dependencies. Exit code: {Constants.WindowsExitCodes.DllNotFound}"
            };
        }

        if (exitCode != 0)
        {
            var errorMessage = !string.IsNullOrEmpty(error) ? error : output;

            if (exitCode == 1)
            {
                _logger.LogError("LibreOffice exit code 1. Common causes: unsupported/corrupted file, profile directory permissions, missing configuration, or unwritable output directory");

                try
                {
                    var testFile = Path.Combine(outputDirectory, $"writetest_{UniqueIdGenerator.GenerateId()}.tmp");
                    File.WriteAllText(testFile, "test");
                    File.Delete(testFile);
                }
                catch (Exception ex)
                {
                    var outputDirName = PathSanitizer.GetSafeDirectoryName(outputDirectory);
                    _logger.LogError(ex, "Output directory is NOT writable: {OutputDir}", outputDirName);
                    _logger.LogDebug("Full output directory path for debugging: {OutputDir}", outputDirectory);
                }

                if (!Directory.Exists(tempProfileDir))
                {
                    var profileDirName = PathSanitizer.GetSafeDirectoryName(tempProfileDir);
                    _logger.LogError("User profile directory does not exist: {ProfileDir}", profileDirName);
                    _logger.LogDebug("Full profile directory path for debugging: {ProfileDir}", tempProfileDir);
                }
            }

            _logger.LogError("LibreOffice process failed with exit code {ExitCode}: {ErrorMessage}", exitCode, errorMessage);
            return new ConversionResult
            {
                Success = false,
                Error = $"LibreOffice conversion failed with exit code {exitCode}: {errorMessage}"
            };
        }

        if (!File.Exists(expectedOutputPath))
        {
            var expectedFileName = PathSanitizer.GetSafeFileName(expectedOutputPath);
            _logger.LogError("Expected output file not found - File: {ExpectedFile}", expectedFileName);
            _logger.LogDebug("Full expected output path for debugging: {ExpectedPath}", expectedOutputPath);

            var tempDir = Path.GetDirectoryName(outputPath);
            if (Directory.Exists(tempDir))
            {
                var tempFiles = Directory.GetFiles(tempDir, "*.*");
                var tempFileNames = tempFiles.Select(PathSanitizer.GetSafeFileName).ToArray();
                _logger.LogInformation("Files in temp directory: {Files}", string.Join(", ", tempFileNames));
                _logger.LogDebug("Full temp directory path for debugging: {TempDir}", tempDir);
            }

            return new ConversionResult
            {
                Success = false,
                Error = $"Expected output file was not created: {expectedFileName}"
            };
        }

        if (!string.Equals(expectedOutputPath, outputPath, StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                File.Move(expectedOutputPath, outputPath);
            }
            catch (Exception ex)
            {
                var expectedFileName = PathSanitizer.GetSafeFileName(expectedOutputPath);
                var targetFileName = PathSanitizer.GetSafeFileName(outputPath);

                _logger.LogError(ex, "Failed to rename output file from {ExpectedFile} to {TargetFile}",
                    expectedFileName, targetFileName);
                _logger.LogDebug("Full paths - Expected: {ExpectedPath}, Target: {OutputPath}",
                    expectedOutputPath, outputPath);

                return new ConversionResult
                {
                    Success = false,
                    Error = $"Failed to rename output file: {ex.Message}"
                };
            }
        }

        try
        {
            if (Directory.Exists(tempProfileDir))
            {
                Directory.Delete(tempProfileDir, recursive: true);
            }
        }
        catch (Exception ex)
        {
            var profileDirName = PathSanitizer.GetSafeDirectoryName(tempProfileDir);
            _logger.LogWarning(ex, "Failed to clean up temporary LibreOffice profile: {ProfileDir}", profileDirName);
            _logger.LogDebug("Full profile directory path for debugging: {ProfileDir}", tempProfileDir);
        }

        return new ConversionResult
        {
            Success = true,
            OutputPath = outputPath
        };
    }

    /// <summary>
    /// Validates target format against whitelist to prevent command injection.
    /// </summary>
    private static bool IsValidTargetFormat(string targetFormat)
    {
        if (string.IsNullOrWhiteSpace(targetFormat))
            return false;

        // Whitelist of allowed output formats
        var allowedFormats = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "pdf", "doc", "docx", "txt", "html", "htm", "csv", "xlsx"
        };

        // Ensure format contains only alphanumeric characters (prevent injection)
        if (!System.Text.RegularExpressions.Regex.IsMatch(targetFormat, "^[a-zA-Z0-9]+$"))
            return false;

        return allowedFormats.Contains(targetFormat);
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
