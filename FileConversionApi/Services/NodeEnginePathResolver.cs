using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FileConversionApi.Models;
using FileConversionApi.Utilities;

namespace FileConversionApi.Services;

/// <summary>
/// Resolves the node.exe used to run the bundled PDF-&gt;HTML engine.
/// Mirrors <see cref="LibreOfficePathResolver"/>: a search strategy that prefers the
/// bundled runtime, with a two-gate security check (file name + directory containment)
/// applied to every file-path candidate before it is returned.
/// </summary>
public class NodeEnginePathResolver
{
    private const string NodeExecutableName = "node.exe";

    private readonly ILogger<NodeEnginePathResolver> _logger;
    private readonly NodeEngineConfig _config;

    public NodeEnginePathResolver(
        ILogger<NodeEnginePathResolver> logger,
        IOptions<NodeEngineConfig> config)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// Resolves the node.exe path using a search strategy.
    /// Searches in this order:
    /// 1. Bundled runtime under the application's engine directory (recommended for deployment)
    /// 2. Configured path from appsettings (Engine:NodePath); must still pass the two-gate check
    /// 3. PATH literal "node.exe" (development fallback only; logged at Warning, never used when
    ///    a bundled runtime is staged)
    /// </summary>
    public Task<string> GetNodePathAsync()
    {
        var engineBaseDir = Path.Combine(AppContext.BaseDirectory, "engine");

        // Strategy 1: bundled node.exe under <app>/engine/node/node.exe
        var bundled = Path.Combine(engineBaseDir, "node", NodeExecutableName);
        if (File.Exists(bundled) && IsValidBundledExecutable(bundled, engineBaseDir))
        {
            _logger.LogInformation("Using bundled Node runtime: {File}", PathSanitizer.GetSafeFileName(bundled));
            _logger.LogDebug("Full bundled Node path for debugging: {Path}", bundled);
            return Task.FromResult(bundled);
        }

        // Strategy 2: configured path (Engine:NodePath); subject to the same two-gate check
        if (!string.IsNullOrEmpty(_config.NodePath) && File.Exists(_config.NodePath))
        {
            if (IsValidBundledExecutable(_config.NodePath, engineBaseDir))
            {
                _logger.LogInformation("Using configured Node runtime: {File}", PathSanitizer.GetSafeFileName(_config.NodePath));
                _logger.LogDebug("Full configured Node path for debugging: {Path}", _config.NodePath);
                return Task.FromResult(_config.NodePath);
            }

            _logger.LogError("Configured Node path failed security validation: {File}", PathSanitizer.GetSafeFileName(_config.NodePath));
            _logger.LogDebug("Full configured Node path for debugging: {Path}", _config.NodePath);
        }

        // Strategy 3: PATH literal. Development fallback only; the bundled runtime is the
        // production contract (staged by deploy.ps1); when it is absent we defer to the OS PATH
        // resolution of "node.exe" so local development works, but flag it loudly. The bare name
        // cannot satisfy the directory-containment gate by design, so containment is intentionally
        // not asserted here; the name gate is.
        _logger.LogWarning(
            "Bundled Node runtime not found under '{EngineDir}'. Falling back to PATH '{Node}' (development only).",
            PathSanitizer.GetSafeDirectoryName(engineBaseDir), NodeExecutableName);
        return Task.FromResult(NodeExecutableName);
    }

    /// <summary>
    /// Validates a candidate node.exe with two independent gates, both required:
    /// (a) the file name must equal node.exe (case-insensitive), rejecting a renamed binary;
    /// (b) the normalized path must be contained within the engine base directory, rejecting
    /// traversal and sibling-prefix matches (e.g. "&lt;engine&gt;-evil" must not satisfy "&lt;engine&gt;").
    /// </summary>
    private bool IsValidBundledExecutable(string executablePath, string engineBaseDir)
    {
        try
        {
            var fullPath = Path.GetFullPath(executablePath);

            // Gate 1: the file must be named node.exe.
            var fileName = Path.GetFileName(fullPath);
            if (!fileName.Equals(NodeExecutableName, StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Node executable validation failed: not {Expected} - {FileName}", NodeExecutableName, fileName);
                return false;
            }

            // Gate 2: the path must be contained within the engine base directory.
            if (!IsPathInDirectory(fullPath, engineBaseDir))
            {
                _logger.LogWarning("Node executable validation failed: path not within engine directory - {File}", PathSanitizer.GetSafeFileName(fullPath));
                _logger.LogDebug("Full path for debugging: {Path} (engine base: {EngineDir})", fullPath, engineBaseDir);
                return false;
            }

            return true;
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _logger.LogError(ex, "Node executable path validation failed with exception for file: {File}", PathSanitizer.GetSafeFileName(executablePath));
            return false;
        }
    }

    /// <summary>
    /// Validates that a file path is within a specified directory.
    /// Prevents path traversal attacks by ensuring the child path is truly contained within the parent.
    /// </summary>
    private static bool IsPathInDirectory(string childPath, string parentPath)
    {
        var normalizedChild = Path.GetFullPath(childPath);
        var normalizedParent = Path.GetFullPath(parentPath);

        // Append the directory separator so "C:\Program Files Malicious" cannot match "C:\Program Files".
        if (!normalizedParent.EndsWith(Path.DirectorySeparatorChar.ToString()))
        {
            normalizedParent += Path.DirectorySeparatorChar;
        }

        return normalizedChild.StartsWith(normalizedParent, StringComparison.OrdinalIgnoreCase);
    }
}
