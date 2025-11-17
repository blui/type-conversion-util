using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FileConversionApi.Models;
using FileConversionApi.Services.Interfaces;

namespace FileConversionApi.Services;

public class LibreOfficePathResolver : ILibreOfficePathResolver
{
    private readonly ILogger<LibreOfficePathResolver> _logger;
    private readonly LibreOfficeConfig _config;

    public LibreOfficePathResolver(
        ILogger<LibreOfficePathResolver> logger,
        IOptions<LibreOfficeConfig> config)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// Resolves the LibreOffice executable path using a search strategy.
    /// Searches in this order:
    /// 1. Bundled runtime in application directory (recommended for deployment)
    /// 2. Configured executable path from appsettings
    /// 3. System Program Files directory (if available)
    /// 4. System Program Files (x86) directory (if available)
    /// </summary>
    public Task<string> GetExecutablePathAsync()
    {
        var appDirectory = AppContext.BaseDirectory;

        // Strategy 1: Check for bundled LibreOffice runtime alongside the application
        var bundledExecutable = Path.Combine(appDirectory, "LibreOffice", "program", "soffice.exe");
        if (File.Exists(bundledExecutable) && IsValidExecutablePath(bundledExecutable, appDirectory))
        {
            _logger.LogInformation("Using bundled LibreOffice executable: {Path}", bundledExecutable);
            return Task.FromResult(bundledExecutable);
        }

        // Strategy 2: Use configured path from appsettings
        if (!string.IsNullOrEmpty(_config.ExecutablePath) && File.Exists(_config.ExecutablePath))
        {
            if (!IsValidExecutablePath(_config.ExecutablePath, appDirectory))
            {
                _logger.LogError("Configured LibreOffice path failed security validation: {Path}", _config.ExecutablePath);
            }
            else
            {
                _logger.LogInformation("Using configured LibreOffice executable: {Path}", _config.ExecutablePath);
                return Task.FromResult(_config.ExecutablePath);
            }
        }

        // Strategy 3: Check standard Program Files directory (cross-drive compatible)
        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        if (!string.IsNullOrEmpty(programFiles))
        {
            var standardPath = Path.Combine(programFiles, "LibreOffice", "program", "soffice.exe");
            if (File.Exists(standardPath) && IsValidExecutablePath(standardPath, appDirectory))
            {
                _logger.LogWarning("Using system LibreOffice installation (not recommended for production): {Path}", standardPath);
                return Task.FromResult(standardPath);
            }
        }

        // Strategy 4: Check Program Files (x86) directory for 32-bit LibreOffice
        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        if (!string.IsNullOrEmpty(programFilesX86))
        {
            var x86Path = Path.Combine(programFilesX86, "LibreOffice", "program", "soffice.exe");
            if (File.Exists(x86Path) && IsValidExecutablePath(x86Path, appDirectory))
            {
                _logger.LogWarning("Using 32-bit system LibreOffice installation (not recommended for production): {Path}", x86Path);
                return Task.FromResult(x86Path);
            }
        }

        // No executable found - return bundled path for clear error messaging
        _logger.LogError("LibreOffice executable not found. Please bundle LibreOffice with the application or configure ExecutablePath in appsettings.");
        return Task.FromResult(bundledExecutable);
    }

    /// <summary>
    /// Validates executable path to prevent execution of arbitrary binaries.
    /// Ensures the path is to a legitimate LibreOffice executable.
    /// </summary>
    private bool IsValidExecutablePath(string executablePath, string appDirectory)
    {
        try
        {
            // Normalize and get full path to prevent path traversal
            var fullPath = Path.GetFullPath(executablePath);

            // Executable must be named soffice.exe (LibreOffice command-line tool)
            var fileName = Path.GetFileName(fullPath);
            if (!fileName.Equals("soffice.exe", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Executable validation failed: Not soffice.exe - {FileName}", fileName);
                return false;
            }

            // Must be in a "program" directory (standard LibreOffice structure)
            var directory = Path.GetDirectoryName(fullPath);
            if (string.IsNullOrEmpty(directory))
            {
                _logger.LogWarning("Executable validation failed: Unable to determine parent directory - {Path}", fullPath);
                return false;
            }

            var parentDir = Path.GetFileName(directory);
            if (!parentDir.Equals("program", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Executable validation failed: Not in 'program' directory - {Path}", fullPath);
                return false;
            }

            // If configured path, must be in whitelisted locations
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            // Normalize base paths to prevent path traversal attacks
            var normalizedAppDir = Path.GetFullPath(appDirectory);
            var normalizedProgramFiles = !string.IsNullOrEmpty(programFiles) ? Path.GetFullPath(programFiles) : null;
            var normalizedProgramFilesX86 = !string.IsNullOrEmpty(programFilesX86) ? Path.GetFullPath(programFilesX86) : null;

            // Check if path is within allowed directories
            // Ensure directory separator follows base path to prevent "C:\Program Files Malicious" matching "C:\Program Files"
            var isInAppDirectory = IsPathInDirectory(fullPath, normalizedAppDir);
            var isInProgramFiles = normalizedProgramFiles != null && IsPathInDirectory(fullPath, normalizedProgramFiles);
            var isInProgramFilesX86 = normalizedProgramFilesX86 != null && IsPathInDirectory(fullPath, normalizedProgramFilesX86);

            if (!isInAppDirectory && !isInProgramFiles && !isInProgramFilesX86)
            {
                _logger.LogWarning("Executable validation failed: Path not in whitelisted directory - {Path}", fullPath);
                return false;
            }

            return true;
        }
        catch (OutOfMemoryException)
        {
            throw;
        }
        catch (StackOverflowException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Executable path validation failed with exception for path: {Path}", executablePath);
            return false;
        }
    }

    /// <summary>
    /// Validates that a file path is within a specified directory.
    /// Prevents path traversal attacks by ensuring the child path is truly contained within the parent.
    /// </summary>
    private static bool IsPathInDirectory(string childPath, string parentPath)
    {
        // Ensure both paths are normalized
        var normalizedChild = Path.GetFullPath(childPath);
        var normalizedParent = Path.GetFullPath(parentPath);

        // Ensure parent path ends with directory separator for accurate comparison
        if (!normalizedParent.EndsWith(Path.DirectorySeparatorChar.ToString()))
        {
            normalizedParent += Path.DirectorySeparatorChar;
        }

        // Check if child starts with parent and is not equal to parent
        return normalizedChild.StartsWith(normalizedParent, StringComparison.OrdinalIgnoreCase);
    }
}
