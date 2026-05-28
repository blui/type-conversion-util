using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FileConversionApi.Models;
using FileConversionApi.Services.Interfaces;
using FileConversionApi.Utilities;

namespace FileConversionApi.Services;

/// <summary>
/// Owns the soffice.exe child-process lifecycle for a single conversion: per-conversion
/// profile dir, --headless argv via ArgumentList (no shell), linked-CTS timeout, kill-on-cancel,
/// and File.Move of the produced output back to the caller-requested path. Stamps
/// <see cref="ConversionResult.ProcessingTimeMs"/> on the result and exposes a lightweight
/// <see cref="IsAvailableAsync"/> probe for /health. Returns a typed
/// <see cref="FailureReason.Timeout"/> on internal timeout; throws OperationCanceledException
/// only when the caller's token fired.
/// </summary>
public class LibreOfficeProcessManager : ILibreOfficeProcessManager
{
    private readonly ILogger<LibreOfficeProcessManager> _logger;
    private readonly LibreOfficePathResolver _pathResolver;
    private readonly LibreOfficeConfig _config;

    public LibreOfficeProcessManager(
        ILogger<LibreOfficeProcessManager> logger,
        LibreOfficePathResolver pathResolver,
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
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Ensure the caller-requested output directory exists before LibreOffice writes its
            // produced file under --outdir; LibreOffice expects the directory to be present.
            FileSystemHelper.EnsureDirectoryExists(outputPath);

            var result = await ConvertCoreAsync(inputPath, outputPath, targetFormat, cancellationToken);

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
            var failedFileName = PathSanitizer.GetSafeFileName(inputPath);
            _logger.LogError(ex, "LibreOffice conversion failed for file: {InputFile}", failedFileName);
            _logger.LogDebug("Full input path for debugging: {InputPath}", inputPath);

            return new ConversionResult
            {
                Success = false,
                Error = $"LibreOffice conversion failed: {ex.Message}",
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    /// <summary>
    /// Returns true when the configured LibreOffice executable resolves to a file that exists.
    /// Used by the /health probe; does not invoke the engine.
    /// </summary>
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

    private async Task<ConversionResult> ConvertCoreAsync(
        string inputPath,
        string outputPath,
        string targetFormat,
        CancellationToken cancellationToken)
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

        if (!IsValidTargetFormat(targetFormat))
        {
            _logger.LogError("Invalid target format detected: {TargetFormat}", targetFormat);
            return new ConversionResult
            {
                Success = false,
                Error = $"Invalid target format: {targetFormat}"
            };
        }

        // LibreOffice writes its produced file as {inputStem}.{targetFormat} under --outdir; the
        // post-success step renames it to the caller-requested outputPath when they differ.
        var outputDirectory = Path.GetDirectoryName(outputPath) ?? Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar);
        var expectedOutputPath = Path.Combine(outputDirectory, $"{Path.GetFileNameWithoutExtension(inputPath)}.{targetFormat}");

        var profileBaseDir = Path.Combine(AppContext.BaseDirectory, "App_Data", "libreoffice-profiles");
        var tempProfileDir = PrepareProfileDirectory(profileBaseDir, _logger);
        try
        {
            var userProfileUri = new Uri(tempProfileDir).AbsoluteUri;
            var startInfo = BuildLibreOfficeStartInfo(executablePath, userProfileUri, targetFormat, outputDirectory, inputPath);

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

            var waitOutcome = await WaitForProcessWithLinkedTimeoutAsync(process, _config.TimeoutSeconds, cancellationToken);
            if (waitOutcome == ProcessWaitOutcome.Timeout)
            {
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

            if (exitCode != 0)
            {
                return HandleNonZeroExitAsync(exitCode, output, error, outputDirectory, tempProfileDir, _logger);
            }

            return FinalizeOutputFile(expectedOutputPath, outputPath, _logger);
        }
        finally
        {
            CleanupProfileDirectory(tempProfileDir);
        }
    }

    /// <summary>
    /// Creates the per-conversion profile base directory and seeds a unique sub-folder from the
    /// bundled <c>libreoffice-profile-template</c>. Returns the absolute path to the seeded
    /// profile directory. Logs (but does not fail) when the bundled template is missing;
    /// LibreOffice will fall back to creating a fresh profile.
    /// </summary>
    private static string PrepareProfileDirectory(string baseDir, ILogger logger)
    {
        Directory.CreateDirectory(baseDir);

        var tempProfileDir = Path.Combine(baseDir, UniqueIdGenerator.GenerateId());
        var profileTemplate = Path.Combine(AppContext.BaseDirectory, "libreoffice-profile-template");

        if (Directory.Exists(profileTemplate))
        {
            CopyDirectory(profileTemplate, tempProfileDir);
        }
        else
        {
            var templateDirName = PathSanitizer.GetSafeDirectoryName(profileTemplate);
            logger.LogWarning("Profile template not found - Template: {TemplateDir}, LibreOffice will create profile", templateDirName);
            logger.LogDebug("Full profile template path for debugging: {TemplatePath}", profileTemplate);
        }

        return tempProfileDir;
    }

    /// <summary>
    /// Builds the <see cref="ProcessStartInfo"/> argv for a headless soffice.exe conversion. Uses
    /// <see cref="ProcessStartInfo.ArgumentList"/> so every argument is escaped by the runtime
    /// (no shell parsing, no concatenation). Callers must validate <paramref name="targetFormat"/>
    /// beforehand; this method is a pure builder and does not re-check the whitelist.
    /// </summary>
    private static ProcessStartInfo BuildLibreOfficeStartInfo(
        string executablePath,
        string profileUri,
        string targetFormat,
        string outDir,
        string inputPath)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executablePath,
            WorkingDirectory = Path.GetDirectoryName(executablePath),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add("--headless");
        startInfo.ArgumentList.Add("--nofirststartwizard");
        startInfo.ArgumentList.Add($"-env:UserInstallation={profileUri}");
        startInfo.ArgumentList.Add("--convert-to");
        startInfo.ArgumentList.Add(targetFormat);
        startInfo.ArgumentList.Add("--outdir");
        startInfo.ArgumentList.Add(outDir);
        startInfo.ArgumentList.Add(inputPath);

        return startInfo;
    }

    /// <summary>
    /// Waits for the LibreOffice child process to exit, honoring both the caller's
    /// <paramref name="cancellationToken"/> (client disconnect) and an internal
    /// <paramref name="timeoutSeconds"/>-bounded timeout via a linked CTS. Returns
    /// <see cref="ProcessWaitOutcome.Succeeded"/> on normal exit, <see cref="ProcessWaitOutcome.Timeout"/>
    /// on the internal timeout (after killing the process tree), and rethrows
    /// <see cref="OperationCanceledException"/> on caller-token cancellation (after killing the
    /// process tree). The caller inspects <see cref="Process.ExitCode"/> after a Succeeded result.
    /// </summary>
    private static async Task<ProcessWaitOutcome> WaitForProcessWithLinkedTimeoutAsync(
        Process process,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            await process.WaitForExitAsync(linkedCts.Token);
            return ProcessWaitOutcome.Succeeded;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Client cancelled. Kill the running process tree so the profile lock is released for
            // the caller's finally-block cleanup, then rethrow so the controller drops the
            // response without synthesizing a body.
            await TryKillAndWaitAsync(process);
            throw;
        }
        catch (OperationCanceledException)
        {
            // Internal timeout fired. Kill the tree (same lock-release reason) and signal Timeout
            // to the caller, which converts it to a typed ConversionResult.
            await TryKillAndWaitAsync(process);
            return ProcessWaitOutcome.Timeout;
        }
    }

    /// <summary>
    /// Builds the <see cref="ConversionResult"/> for a non-zero LibreOffice exit. Treats the
    /// Windows DLL_NOT_FOUND NTSTATUS as a distinct case (operator-actionable message about
    /// missing VC++ redistributable); for exit code 1, probes the output directory for write
    /// permission and the profile directory for existence to surface the common root causes in
    /// logs; everything else collapses to a generic failure result with the engine's stderr/stdout
    /// embedded in the error message.
    /// </summary>
    private static ConversionResult HandleNonZeroExitAsync(
        int exitCode,
        string output,
        string error,
        string outputDirectory,
        string tempProfileDir,
        ILogger logger)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && exitCode == Constants.WindowsExitCodes.DllNotFound)
        {
            logger.LogError("LibreOffice failed with exit code {ExitCode} (DLL_NOT_FOUND). Missing Visual C++ Redistributable or LibreOffice dependencies.", Constants.WindowsExitCodes.DllNotFound);
            return new ConversionResult
            {
                Success = false,
                Error = $"LibreOffice process failed to start. The server may be missing Visual C++ Redistributable (2015-2022) or LibreOffice dependencies. Exit code: {Constants.WindowsExitCodes.DllNotFound}"
            };
        }

        var errorMessage = !string.IsNullOrEmpty(error) ? error : output;

        if (exitCode == 1)
        {
            logger.LogError("LibreOffice exit code 1. Common causes: unsupported/corrupted file, profile directory permissions, missing configuration, or unwritable output directory");

            try
            {
                var testFile = Path.Combine(outputDirectory, $"writetest_{UniqueIdGenerator.GenerateId()}.tmp");
                File.WriteAllText(testFile, "test");
                File.Delete(testFile);
            }
            catch (Exception ex)
            {
                var outputDirName = PathSanitizer.GetSafeDirectoryName(outputDirectory);
                logger.LogError(ex, "Output directory is NOT writable: {OutputDir}", outputDirName);
                logger.LogDebug("Full output directory path for debugging: {OutputDir}", outputDirectory);
            }

            if (!Directory.Exists(tempProfileDir))
            {
                var profileDirName = PathSanitizer.GetSafeDirectoryName(tempProfileDir);
                logger.LogError("User profile directory does not exist: {ProfileDir}", profileDirName);
                logger.LogDebug("Full profile directory path for debugging: {ProfileDir}", tempProfileDir);
            }
        }

        logger.LogError("LibreOffice process failed with exit code {ExitCode}", exitCode);
        logger.LogDebug("LibreOffice raw failure detail for debugging: {ErrorMessage}", errorMessage);

        return new ConversionResult
        {
            Success = false,
            Error = $"LibreOffice conversion failed with exit code {exitCode}: {errorMessage}"
        };
    }

    /// <summary>
    /// Post-success finalization: verifies the LibreOffice-produced file exists at the convention
    /// path and renames it to the caller-requested output path when the two differ. Returns a
    /// success <see cref="ConversionResult"/> when the file is in place, or a typed failure
    /// result when the expected file is missing or the rename fails.
    /// </summary>
    private static ConversionResult FinalizeOutputFile(string expectedOutputPath, string outputPath, ILogger logger)
    {
        if (!File.Exists(expectedOutputPath))
        {
            var expectedFileName = PathSanitizer.GetSafeFileName(expectedOutputPath);
            logger.LogError("Expected output file not found - File: {ExpectedFile}", expectedFileName);
            logger.LogDebug("Full expected output path for debugging: {ExpectedPath}", expectedOutputPath);

            var tempDir = Path.GetDirectoryName(outputPath);
            if (Directory.Exists(tempDir))
            {
                var tempFiles = Directory.GetFiles(tempDir, "*.*");
                var tempFileNames = tempFiles.Select(PathSanitizer.GetSafeFileName).ToArray();
                logger.LogInformation("Files in temp directory: {Files}", string.Join(", ", tempFileNames));
                logger.LogDebug("Full temp directory path for debugging: {TempDir}", tempDir);
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

                logger.LogError(ex, "Failed to rename output file from {ExpectedFile} to {TargetFile}",
                    expectedFileName, targetFileName);
                logger.LogDebug("Full paths - Expected: {ExpectedPath}, Target: {OutputPath}",
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

    /// <summary>
    /// Best-effort, never-throws cleanup of the per-conversion profile directory. Invoked from
    /// the <c>finally</c> block of <see cref="ConvertCoreAsync"/> so every exit path (success,
    /// non-zero exit, timeout, client cancel, rename failure) leaves no profile dir behind.
    /// </summary>
    private void CleanupProfileDirectory(string tempProfileDir)
    {
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

    /// <summary>
    /// Result of <see cref="WaitForProcessWithLinkedTimeoutAsync"/>: distinguishes the
    /// process-exited-normally case (caller inspects <see cref="Process.ExitCode"/>) from the
    /// internal-timeout case (the helper killed the tree; caller should return a typed
    /// <see cref="FailureReason.Timeout"/> failure). Client-token cancellation rethrows
    /// <see cref="OperationCanceledException"/> from the helper and never reaches an enum value.
    /// </summary>
    private enum ProcessWaitOutcome
    {
        Succeeded,
        Timeout
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
    /// Power-of-Ten rule 1 ceiling on the iterative directory-copy walk in
    /// <see cref="CopyDirectory"/>. The bundled libreoffice-profile-template is ~3 levels deep;
    /// this bound exists so a corrupted or attacker-controlled template cannot drive an
    /// unbounded walk. The walk throws <see cref="InvalidOperationException"/> when exceeded.
    /// </summary>
    private const int MaxProfileTemplateDepth = 8;

    /// <summary>
    /// Iteratively copies a directory tree (files + subdirectories) using an explicit stack so
    /// the walk has no implicit recursion depth (NASA Power of Ten rule 1). The relative depth
    /// is bounded by <see cref="MaxProfileTemplateDepth"/>; exceeding it throws
    /// <see cref="InvalidOperationException"/> naming the offending path.
    /// </summary>
    private static void CopyDirectory(string sourceDir, string destDir)
    {
        var stack = new Stack<(string Source, string Dest, int Depth)>();
        stack.Push((sourceDir, destDir, 0));

        while (stack.Count > 0)
        {
            var (currentSource, currentDest, depth) = stack.Pop();

            if (depth > MaxProfileTemplateDepth)
            {
                throw new InvalidOperationException(
                    $"Profile template depth exceeded {MaxProfileTemplateDepth} at path: {currentSource}");
            }

            Directory.CreateDirectory(currentDest);

            foreach (var file in Directory.GetFiles(currentSource))
            {
                var fileName = Path.GetFileName(file);
                var destFile = Path.Combine(currentDest, fileName);
                File.Copy(file, destFile, overwrite: true);
            }

            foreach (var subDir in Directory.GetDirectories(currentSource))
            {
                var dirName = Path.GetFileName(subDir);
                var destSubDir = Path.Combine(currentDest, dirName);
                stack.Push((subDir, destSubDir, depth + 1));
            }
        }
    }
}
