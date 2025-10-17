using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FileConversionApi.Models;
using FileConversionApi.Services.Interfaces;

namespace FileConversionApi.Services;

/// <summary>
/// Resolves LibreOffice executable paths with multiple fallback strategies
/// Prioritizes bundled runtime over system installations
/// </summary>
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

    /// <inheritdoc/>
    public async Task<string> GetExecutablePathAsync()
    {
        // Get the application base directory
        var appDirectory = AppContext.BaseDirectory;

        // Check for bundled LibreOffice runtime in the application directory
        var bundledExecutable = Path.Combine(appDirectory, "LibreOffice", "program", "soffice.exe");
        if (File.Exists(bundledExecutable))
        {
            _logger.LogInformation("Using bundled LibreOffice executable: {Path}", bundledExecutable);
            return bundledExecutable;
        }

        // Check configured executable path
        if (!string.IsNullOrEmpty(_config.ExecutablePath) && File.Exists(_config.ExecutablePath))
        {
            _logger.LogInformation("Using configured LibreOffice executable: {Path}", _config.ExecutablePath);
            return _config.ExecutablePath;
        }

        // Check standard LibreOffice installation
        var standardPath = @"C:\Program Files\LibreOffice\program\soffice.exe";
        if (File.Exists(standardPath))
        {
            _logger.LogWarning("Using system LibreOffice installation (not recommended for deployment): {Path}", standardPath);
            return standardPath;
        }

        // Check 32-bit LibreOffice installation (fallback)
        var x86Path = @"C:\Program Files (x86)\LibreOffice\program\soffice.exe";
        if (File.Exists(x86Path))
        {
            _logger.LogWarning("Using system 32-bit LibreOffice installation (not recommended for deployment): {Path}", x86Path);
            return x86Path;
        }

        // Final fallback - use bundled path even if it doesn't exist (will fail gracefully)
        _logger.LogError("No LibreOffice executable found. Application requires bundled LibreOffice runtime.");
        return bundledExecutable;
    }
}
