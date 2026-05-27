using System.Diagnostics;
using System.Runtime.InteropServices;
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
    public async Task<ConversionResult> ConvertAsync(
        string inputPath,
        string outputPath,
        string targetFormat,
        CancellationToken cancellationToken = default)
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
            var templateDirName = PathSanitizer.GetSafeDirectoryName(profileTemplate);
            _logger.LogWarning("Profile template not found - Template: {TemplateDir}, LibreOffice will create profile", templateDirName);
            _logger.LogDebug("Full profile template path for debugging: {TemplatePath}", profileTemplate);
        }

        // Convert Windows path to file URI for -env:UserInstallation
        var userProfileUri = new Uri(tempProfileDir).AbsoluteUri;

        try
        {
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

            _logger.LogInformation("Executing LibreOffice conversion to format {TargetFormat} - File: {FileName}",
                targetFormat, PathSanitizer.GetSafeFileName(inputPath));
            _logger.LogDebug("Full LibreOffice executable path for debugging: {Executable}", executablePath);

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

            // Link the caller's token (client disconnect via HttpContext.RequestAborted) with the
            // per-engine internal timeout so a single WaitForExitAsync covers both signals.
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_config.TimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            try
            {
                await process.WaitForExitAsync(linkedCts.Token);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // Client cancelled. Kill the running process tree to release the profile lock so the
                // finally block below can clean it up, then rethrow so the controller drops the
                // response without synthesizing a body.
                await TryKillAndWaitAsync(process);
                throw;
            }
            catch (OperationCanceledException)
            {
                await TryKillAndWaitAsync(process);
                return new ConversionResult
                {
                    Success = false,
                    Error = $"LibreOffice conversion timed out after {_config.TimeoutSeconds} seconds",
                    FailureReason = FailureReason.Timeout
                };
            }

            var exitCode = process.ExitCode;
            var output = await outputTask;
            var error = await errorTask;

            _logger.LogInformation("LibreOffice process completed with exit code {ExitCode}", exitCode);
            _logger.LogDebug("LibreOffice raw output for debugging - Output: '{Output}', Error: '{Error}'",
                output, error);

            // Check for specific LibreOffice failure exit codes
            // The DllNotFound exit code is a Windows-specific NTSTATUS constant.
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (exitCode == Constants.WindowsExitCodes.DllNotFound)
                {
                    _logger.LogError("LibreOffice failed with exit code {ExitCode} (DLL_NOT_FOUND). Missing Visual C++ Redistributable or LibreOffice dependencies.", Constants.WindowsExitCodes.DllNotFound);
                    return new ConversionResult
                    {
                        Success = false,
                        Error = $"LibreOffice process failed to start. The server may be missing Visual C++ Redistributable (2015-2022) or LibreOffice dependencies. Exit code: {Constants.WindowsExitCodes.DllNotFound}"
                    };
                }
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

                _logger.LogError("LibreOffice process failed with exit code {ExitCode}", exitCode);
                _logger.LogDebug("LibreOffice raw failure detail for debugging: {ErrorMessage}", errorMessage);
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

            return new ConversionResult
            {
                Success = true,
                OutputPath = outputPath
            };
        }
        finally
        {
            // Guaranteed profile-dir cleanup on every exit path (DLL-not-found, exit code != 0,
            // output-not-found, rename failure, timeout, success). Best-effort: never throws.
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
        }
    }

    /// <summary>
    /// Kills the timed-out LibreOffice process tree, then waits a short bounded interval for it to
    /// exit so it releases the lock on its profile directory before the caller deletes it. The wait
    /// is bounded because the process is already misbehaving on the timeout path; an unbounded wait
    /// would block the request thread worse than the orphan it prevents. Never throws.
    /// </summary>
    private static async Task TryKillAndWaitAsync(Process process)
    {
        try
        {
            process.Kill(entireProcessTree: true);
        }
        catch (InvalidOperationException)
        {
            // Process already exited between the cancellation and the kill; nothing to do.
        }

        using var killWaitCts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try
        {
            await process.WaitForExitAsync(killWaitCts.Token);
        }
        catch (OperationCanceledException)
        {
            // The killed process did not exit within the bounded wait; proceed best-effort so the
            // request thread is never blocked indefinitely and the profile-dir cleanup can run.
        }
    }

    /// <summary>
    /// Validates target format against whitelist to prevent command injection.
    /// </summary>
    private static bool IsValidTargetFormat(string targetFormat)
    {
        if (string.IsNullOrWhiteSpace(targetFormat))
            return false;

        // Ensure format contains only alphanumeric characters (prevent injection)
        if (!System.Text.RegularExpressions.Regex.IsMatch(targetFormat, "^[a-zA-Z0-9]+$"))
            return false;

        // Use centralized format definitions
        return Constants.SupportedFormats.ConversionTargets.Contains(targetFormat);
    }

    /// <summary>
    /// Recursively copies a directory and all its contents
    /// </summary>
    private static void CopyDirectory(string sourceDir, string destDir)
    {
        Directory.CreateDirectory(destDir);

        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var fileName = Path.GetFileName(file);
            var destFile = Path.Combine(destDir, fileName);
            File.Copy(file, destFile, overwrite: true);
        }

        foreach (var subDir in Directory.GetDirectories(sourceDir))
        {
            var dirName = Path.GetFileName(subDir);
            var destSubDir = Path.Combine(destDir, dirName);
            CopyDirectory(subDir, destSubDir);
        }
    }
}
