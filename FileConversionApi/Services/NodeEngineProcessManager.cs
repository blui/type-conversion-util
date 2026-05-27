using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FileConversionApi.Models;
using FileConversionApi.Services.Interfaces;
using FileConversionApi.Utilities;

namespace FileConversionApi.Services;

/// <summary>
/// Manages the bundled Node PDF-&gt;HTML engine process: spawning with discrete argv,
/// timeout enforcement, and output verification. Mirrors <see cref="LibreOfficeProcessManager"/>.
/// </summary>
public class NodeEngineProcessManager : INodeEngineProcessManager
{
    private readonly ILogger<NodeEngineProcessManager> _logger;
    private readonly INodeEnginePathResolver _pathResolver;
    private readonly NodeEngineConfig _config;

    public NodeEngineProcessManager(
        ILogger<NodeEngineProcessManager> logger,
        INodeEnginePathResolver pathResolver,
        IOptions<NodeEngineConfig> config)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _pathResolver = pathResolver ?? throw new ArgumentNullException(nameof(pathResolver));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));
    }

    /// <inheritdoc/>
    public async Task<ConversionResult> ConvertAsync(
        string inputPath,
        string outputPath,
        CancellationToken cancellationToken = default)
    {
        // Engine CLI contract (LOCKED): node <engineScript> <inputPdf> <outputHtml>
        var engineScript = Path.Combine(AppContext.BaseDirectory, "engine", "pdf-to-html.mjs");
        if (!File.Exists(engineScript))
        {
            var scriptName = PathSanitizer.GetSafeFileName(engineScript);
            _logger.LogError("Engine script not found - File: {ScriptName}", scriptName);
            _logger.LogDebug("Full engine script path for debugging: {ScriptPath}", engineScript);

            return new ConversionResult
            {
                Success = false,
                Error = $"Engine script not found: {scriptName}"
            };
        }

        if (!File.Exists(inputPath))
        {
            var fileName = PathSanitizer.GetSafeFileName(inputPath);
            _logger.LogError("Input PDF does not exist - File: {FileName}", fileName);
            _logger.LogDebug("Full input path for debugging: {InputPath}", inputPath);

            return new ConversionResult
            {
                Success = false,
                Error = $"Input file not found: {fileName}"
            };
        }

        var nodePath = await _pathResolver.GetNodePathAsync();

        // Use ArgumentList (never an Arguments string, never a shell) so each token is escaped
        // and no injection is possible. Exactly three discrete tokens per the LOCKED CLI contract.
        var startInfo = new ProcessStartInfo
        {
            FileName = nodePath,
            WorkingDirectory = Path.GetDirectoryName(engineScript),
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        startInfo.ArgumentList.Add(engineScript);
        startInfo.ArgumentList.Add(inputPath);
        startInfo.ArgumentList.Add(outputPath);

        _logger.LogInformation("Executing Node PDF->HTML engine for input: {FileName}",
            PathSanitizer.GetSafeFileName(inputPath));

        Process? process;
        try
        {
            process = Process.Start(startInfo);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start Node engine process");
            return new ConversionResult
            {
                Success = false,
                Error = $"Failed to start Node engine process: {ex.Message}"
            };
        }

        if (process == null)
        {
            return new ConversionResult
            {
                Success = false,
                Error = "Failed to start Node engine process"
            };
        }

        using (process)
        {
            var outputTask = process.StandardOutput.ReadToEndAsync();
            var errorTask = process.StandardError.ReadToEndAsync();

            // Link the caller's token (client disconnect) with the per-engine timeout.
            using var timeoutCts = new CancellationTokenSource(TimeSpan.FromSeconds(_config.TimeoutSeconds));
            using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
            try
            {
                await process.WaitForExitAsync(linkedCts.Token);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                await TryKillAndWaitAsync(process);
                throw;
            }
            catch (OperationCanceledException)
            {
                await TryKillAndWaitAsync(process);
                _logger.LogError("Node engine conversion timed out after {TimeoutSeconds} seconds", _config.TimeoutSeconds);
                return new ConversionResult
                {
                    Success = false,
                    Error = $"Node engine conversion timed out after {_config.TimeoutSeconds} seconds",
                    FailureReason = FailureReason.Timeout
                };
            }

            var exitCode = process.ExitCode;
            var stdout = await outputTask;
            var stderr = await errorTask;

            if (!string.IsNullOrWhiteSpace(stdout))
            {
                _logger.LogDebug("Node engine raw stdout for debugging: {Stdout}", stdout.Trim());
            }

            if (exitCode != 0)
            {
                var detail = !string.IsNullOrWhiteSpace(stderr) ? stderr.Trim() : "(no stderr)";
                _logger.LogError("Node engine process failed with exit code {ExitCode}", exitCode);
                _logger.LogDebug("Node engine raw failure detail for debugging: {Detail}", detail);
                return new ConversionResult
                {
                    Success = false,
                    Error = $"Node engine conversion failed with exit code {exitCode}: {detail}"
                };
            }

            if (!File.Exists(outputPath))
            {
                var outputFileName = PathSanitizer.GetSafeFileName(outputPath);
                _logger.LogError("Node engine exited 0 but output HTML was not created - File: {OutputFile}", outputFileName);
                _logger.LogDebug("Full output path for debugging: {OutputPath}", outputPath);
                return new ConversionResult
                {
                    Success = false,
                    Error = $"Output file was not created: {outputFileName}"
                };
            }

            if (new FileInfo(outputPath).Length <= 0)
            {
                var outputFileName = PathSanitizer.GetSafeFileName(outputPath);
                _logger.LogError("Node engine exited 0 but output HTML is empty - File: {OutputFile}", outputFileName);
                return new ConversionResult
                {
                    Success = false,
                    Error = $"Output file is empty: {outputFileName}"
                };
            }

            _logger.LogInformation("Node engine conversion succeeded - File: {OutputFile}",
                PathSanitizer.GetSafeFileName(outputPath));

            return new ConversionResult
            {
                Success = true,
                OutputPath = outputPath
            };
        }
    }

    /// <summary>
    /// Kills the timed-out engine process tree, then waits a short bounded interval for it to exit
    /// so it releases its handle on the partially written output file before the caller deletes the
    /// operation directory. The wait is bounded because the process is already misbehaving on the
    /// timeout path; an unbounded wait would block the request thread worse than the orphan it
    /// prevents. Never throws.
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
            // request thread is never blocked indefinitely and the output-dir cleanup can run.
        }
    }
}
