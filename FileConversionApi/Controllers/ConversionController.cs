using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FileConversionApi.Services;
using FileConversionApi.Models;
using FileConversionApi.Utilities;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;

namespace FileConversionApi.Controllers;

/// <summary>
/// Converts files between document formats.
/// </summary>
[ApiController]
[Route("api")]
[Produces("application/json")]
public class ConversionController : ControllerBase
{
    private readonly ILogger<ConversionController> _logger;
    private readonly IDocumentService _documentService;
    private readonly IInputValidator _inputValidator;
    private readonly ISemaphoreService _semaphoreService;
    private readonly FileHandlingConfig _fileConfig;

    public ConversionController(
        ILogger<ConversionController> logger,
        IDocumentService documentService,
        IInputValidator inputValidator,
        ISemaphoreService semaphoreService,
        IOptions<FileHandlingConfig> fileConfig)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _documentService = documentService ?? throw new ArgumentNullException(nameof(documentService));
        _inputValidator = inputValidator ?? throw new ArgumentNullException(nameof(inputValidator));
        _semaphoreService = semaphoreService ?? throw new ArgumentNullException(nameof(semaphoreService));
        _fileConfig = fileConfig?.Value ?? throw new ArgumentNullException(nameof(fileConfig));
    }

    /// <summary>
    /// Returns API information and available endpoints.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiInfo), 200)]
    public IActionResult GetApiInfo()
    {
        var apiInfo = new ApiInfo
        {
            Name = "File Conversion API",
            Version = "1.0.0",
            Description = "Office document conversion service",
            SupportedFormats = new ApiFormats
            {
                Input = _inputValidator.GetSupportedInputFormats(),
                Output = _inputValidator.GetSupportedTargetFormats("pdf")
            },
            Endpoints = new List<ApiEndpoint>
            {
                new() { Method = "GET", Path = "/api", Description = "API information" },
                new() { Method = "GET", Path = "/api/supported-formats", Description = "Supported formats" },
                new() { Method = "POST", Path = "/api/convert", Description = "File conversion" },
                new() { Method = "GET", Path = "/health", Description = "Health check" }
            }
        };

        return Ok(apiInfo);
    }

    /// <summary>
    /// Returns supported file formats and conversion paths.
    /// </summary>
    [HttpGet("supported-formats")]
    [ProducesResponseType(typeof(SupportedFormatsResponse), 200)]
    public IActionResult GetSupportedFormats()
    {
        var response = new SupportedFormatsResponse
        {
            Documents = new DocumentFormats
            {
                // Derived from the single authoritative matrix; no inline pair list to drift.
                Input = Constants.SupportedFormats.ConversionMatrix.Keys.ToList(),
                Conversions = Constants.SupportedFormats.ConversionMatrix.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value.ToList())
            }
        };

        return Ok(response);
    }

    /// <summary>
    /// Converts uploaded file to specified format.
    /// </summary>
    [HttpPost("convert")]
    [ProducesResponseType(typeof(ConversionResponse), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 400)]
    [ProducesResponseType(typeof(ErrorResponse), 500)]
    public async Task<IActionResult> ConvertFile(
        IFormFile file,
        [FromForm, Required] string targetFormat,
        [FromQuery] bool? metadata = null,
        CancellationToken cancellationToken = default)
    {
        var operationId = UniqueIdGenerator.GenerateId();
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("Conversion request started - ID: {OperationId}", operationId);

        // A missing file part binds to null. Reject it before deriving the conversion scope (which
        // reads file.FileName) so the request returns a 400 client error rather than faulting into
        // the 500 path. An absent file has no determinable input format, so it is never in scope.
        if (file is null)
        {
            _logger.LogWarning("File validation failed: {Errors}", "File is required and cannot be empty");
            return ScopedBadRequest(false, operationId, "Invalid file",
                new List<string> { "File is required and cannot be empty" });
        }

        // The structured-error contract (operationId field + X-Operation-Id header, typed 408/500
        // mapping, generic body) applies only to the DOC/DOCX->HTML/HTM pairs; every other pair
        // keeps its existing error responses unchanged. Scope is derived from the client-supplied
        // extension and the requested target format.
        var requestInputFormat = Path.GetExtension(file.FileName)?.TrimStart('.').ToLowerInvariant();
        var isScopedHtmlConversion = IsScopedHtmlConversion(requestInputFormat, targetFormat);

        // Declared here so the operation directories are in scope for the cleanup finally, which
        // covers every exit including the outer catch. Empty until the directories are created.
        var operationUploadDir = string.Empty;
        var operationOutputDir = string.Empty;

        try
        {
            var fileValidation = _inputValidator.ValidateFile(file);
            if (!fileValidation.IsValid)
            {
                _logger.LogWarning("File validation failed: {Errors}", string.Join(", ", fileValidation.Errors!));
                return ScopedBadRequest(isScopedHtmlConversion, operationId, "Invalid file", fileValidation.Errors);
            }

            var inputFormat = requestInputFormat;
            if (string.IsNullOrEmpty(inputFormat))
            {
                return ScopedBadRequest(isScopedHtmlConversion, operationId, "Unable to determine input file format", null);
            }

            var conversionValidation = _inputValidator.ValidateConversion(inputFormat, targetFormat);
            if (!conversionValidation.IsValid)
            {
                _logger.LogWarning("Conversion validation failed: {Errors}",
                    string.Join(", ", conversionValidation.Errors!));
                return ScopedBadRequest(isScopedHtmlConversion, operationId, "Unsupported conversion", conversionValidation.Errors);
            }

            await _semaphoreService.AcquireAsync();

            try
            {
                // Create isolated directories to preserve original filenames
                var tempUploadDir = GetAbsolutePath(_fileConfig.TempDirectory);
                var tempOutputDir = GetAbsolutePath(_fileConfig.OutputDirectory);

                operationUploadDir = Path.Combine(tempUploadDir, operationId);
                operationOutputDir = Path.Combine(tempOutputDir, operationId);

                Directory.CreateDirectory(operationUploadDir);
                Directory.CreateDirectory(operationOutputDir);

                // Defer operation-directory cleanup to Response.OnCompleted so the streaming
                // PhysicalFileResult (below) can read from operationOutputDir during result
                // execution, which runs AFTER the action method's finally blocks fire. The
                // callback runs once per request and is the single owner of operation-dir
                // cleanup from this point forward; the outer finally no longer does it.
                var uploadDirToClean = operationUploadDir;
                var outputDirToClean = operationOutputDir;
                HttpContext.Response.OnCompleted(() =>
                {
                    CleanupOperationDirectories(uploadDirToClean, outputDirToClean);
                    return Task.CompletedTask;
                });

                // Preserve original filename for field codes like {FILENAME}
                var sanitizedFileName = SanitizeFileName(file.FileName, inputFormat);
                var tempInputPath = Path.Combine(operationUploadDir, sanitizedFileName);

                var originalFileNameWithoutExt = Path.GetFileNameWithoutExtension(sanitizedFileName);
                var tempOutputPath = Path.Combine(operationOutputDir, $"{originalFileNameWithoutExt}.{targetFormat}");

                // useAsync: true selects the I/O Completion Port code path; without it, async file
                // I/O degrades to thread-pool calls of synchronous Win32 APIs and blocks a pool
                // thread for the whole copy. Matters under concurrent load even at the default
                // 50MB request cap.
                await using (var stream = new FileStream(
                    tempInputPath, FileMode.Create, FileAccess.Write, FileShare.None,
                    bufferSize: 4096, useAsync: true))
                {
                    await file.CopyToAsync(stream);
                }

                ConversionResult result = await _documentService.ConvertAsync(
                    tempInputPath, tempOutputPath, inputFormat, targetFormat, cancellationToken);

                stopwatch.Stop();
                _logger.LogInformation(
                    "Conversion completed - Input: {InputFormat}, Target: {TargetFormat}, Size: {FileSize} bytes, Time: {ProcessingTime}ms, Success: {Success}",
                    inputFormat,
                    targetFormat,
                    file.Length,
                    stopwatch.ElapsedMilliseconds,
                    result.Success);

                if (!result.Success)
                {
                    _logger.LogError("Conversion failed - ID: {OperationId}, Input: {InputFormat}, Target: {TargetFormat}",
                        operationId, inputFormat, targetFormat);
                    _logger.LogDebug("Conversion raw failure detail for debugging: {Error}", result.Error);

                    if (isScopedHtmlConversion)
                    {
                        // Typed mapping mirroring ExceptionHandlingMiddleware.GetErrorDetails: a timeout
                        // (set at either hop's catch) is 408, every other failure is 500. The decision
                        // never inspects result.Error text. The body is a fixed, path-free message so
                        // raw engine/LibreOffice output is never serialized to the client (CWE-209).
                        var statusCode = result.FailureReason == FailureReason.Timeout
                            ? StatusCodes.Status408RequestTimeout
                            : StatusCodes.Status500InternalServerError;

                        Response.Headers["X-Operation-Id"] = operationId;
                        return StatusCode(statusCode, new ErrorResponse
                        {
                            Error = "Conversion failed",
                            Details = new List<string> { $"An unexpected error occurred. Contact support with operation ID: {operationId}" },
                            OperationId = operationId
                        });
                    }

                    return StatusCode(500, new ErrorResponse
                    {
                        Error = "Conversion failed",
                        Details = new List<string> { result.Error ?? "Unknown error" }
                    });
                }

                var downloadName = $"{Path.GetFileNameWithoutExtension(file.FileName)}.{targetFormat}";
                var contentType = GetContentType(targetFormat);

                if (metadata == true)
                {
                    // The metadata response needs the bytes inline in the JSON body, so this path
                    // still buffers. Same-process cost as before; only the streaming branch changes.
                    var fileBytes = await System.IO.File.ReadAllBytesAsync(tempOutputPath);
                    return Ok(new ConversionResponse
                    {
                        Success = true,
                        FileName = downloadName,
                        FileSize = fileBytes.Length,
                        ProcessingTimeMs = result.ProcessingTimeMs ?? stopwatch.ElapsedMilliseconds,
                        ConversionMethod = result.ConversionMethod,
                        ContentType = contentType,
                        Data = fileBytes
                    });
                }

                // PhysicalFile streams from disk through Kestrel's sendfile-style path; the
                // previous ReadAllBytesAsync buffered the entire output (up to MaxFileSize) into a
                // managed byte[] before responding. The temp output file is cleaned up in the
                // outer finally, after Kestrel has finished writing the response body.
                return PhysicalFile(tempOutputPath, contentType, downloadName);
            }
            finally
            {
                _semaphoreService.Release();
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The client gave up. Don't synthesize a 500 body for a client that's no longer
            // reading; rethrow so ASP.NET drops the response. Stopwatch + operationId are still
            // logged so the post-mortem trail exists.
            stopwatch.Stop();
            _logger.LogInformation(
                "Conversion cancelled by client - Operation ID: {OperationId}, Elapsed: {Elapsed}ms",
                operationId, stopwatch.ElapsedMilliseconds);
            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error in operation {Operation} at {Timestamp}: {Error} (Operation ID: {OperationId})",
                "file_conversion",
                DateTime.UtcNow,
                ex.Message,
                operationId);

            if (isScopedHtmlConversion)
            {
                Response.Headers["X-Operation-Id"] = operationId;
                return StatusCode(500, new ErrorResponse
                {
                    Error = "Internal server error",
                    Details = new List<string> { $"An unexpected error occurred. Contact support with operation ID: {operationId}" },
                    OperationId = operationId
                });
            }

            return StatusCode(500, new ErrorResponse
            {
                Error = "Internal server error",
                Details = new List<string> { $"An unexpected error occurred. Contact support with operation ID: {operationId}" }
            });
        }
        // Operation-directory cleanup is owned by Response.OnCompleted (registered immediately
        // after directory creation) so the streaming PhysicalFileResult can read from disk during
        // result execution, which happens AFTER this method's finally blocks would have fired.
        // Early returns that exit before directory creation leave both dir vars as empty strings,
        // so no cleanup is needed for those paths.
    }

    /// <summary>
    /// Determines whether a conversion request falls inside the hardened-error-contract scope:
    /// a DOC or DOCX input targeting HTML or HTM. The scope is keyed on the
    /// (inputFormat, targetFormat) pair so the contract narrows behavior only for those pairs.
    /// </summary>
    private static bool IsScopedHtmlConversion(string? inputFormat, string targetFormat)
    {
        if (string.IsNullOrEmpty(inputFormat) || string.IsNullOrEmpty(targetFormat))
        {
            return false;
        }

        var normalizedSource = inputFormat.ToLowerInvariant();
        var normalizedTarget = targetFormat.ToLowerInvariant();
        var isHtmlTarget = normalizedTarget is "html" or "htm";
        var isDocSource = normalizedSource is "doc" or "docx";

        return isDocSource && isHtmlTarget;
    }

    /// <summary>
    /// Builds the 400 response for a request, attaching the operationId field and X-Operation-Id
    /// header when the request is in scope and leaving non-scoped pairs unchanged.
    /// </summary>
    private IActionResult ScopedBadRequest(bool isScoped, string operationId, string error, List<string>? details)
    {
        if (isScoped)
        {
            Response.Headers["X-Operation-Id"] = operationId;
            return BadRequest(new ErrorResponse
            {
                Error = error,
                Details = details,
                OperationId = operationId
            });
        }

        return BadRequest(new ErrorResponse
        {
            Error = error,
            Details = details
        });
    }

    private static string GetAbsolutePath(string path)
    {
        if (Path.IsPathRooted(path))
        {
            return path;
        }

        return Path.Combine(AppContext.BaseDirectory, path);
    }

    private static string SanitizeFileName(string fileName, string requiredExtension)
    {
        if (string.IsNullOrWhiteSpace(requiredExtension))
            throw new ArgumentException("Required extension cannot be null or empty", nameof(requiredExtension));

        var extensionWithDot = requiredExtension.StartsWith('.') ? requiredExtension : $".{requiredExtension}";

        if (extensionWithDot.Length > Constants.FileHandling.MaxExtensionLength)
        {
            extensionWithDot = extensionWithDot.Substring(0, Constants.FileHandling.MaxExtensionLength);
        }

        if (string.IsNullOrWhiteSpace(fileName))
            return Constants.FileHandling.DefaultFileName + extensionWithDot;

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));

        var existingExtension = Path.GetExtension(sanitized);
        if (!existingExtension.Equals(extensionWithDot, StringComparison.OrdinalIgnoreCase))
        {
            sanitized = Path.GetFileNameWithoutExtension(sanitized) + extensionWithDot;
        }

        if (sanitized.Length <= Constants.FileHandling.MaxSanitizedFileNameLength)
            return sanitized;

        var nameWithoutExtension = Path.GetFileNameWithoutExtension(sanitized);
        var maxNameLength = Constants.FileHandling.MaxSanitizedFileNameLength - extensionWithDot.Length;

        if (maxNameLength > 0 && nameWithoutExtension.Length > 0)
        {
            var truncatedName = nameWithoutExtension.Substring(0, Math.Min(maxNameLength, nameWithoutExtension.Length));
            return truncatedName + extensionWithDot;
        }

        return Constants.FileHandling.DefaultFileName + extensionWithDot;
    }

    private void CleanupOperationDirectories(params string[] directories)
    {
        foreach (var directory in directories)
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                    var dirName = PathSanitizer.GetSafeDirectoryName(directory);
                    _logger.LogDebug("Cleaned up operation directory: {Directory}", dirName);
                }
            }
            catch (Exception ex)
            {
                var dirName = PathSanitizer.GetSafeDirectoryName(directory);
                _logger.LogWarning(ex, "Failed to clean up operation directory: {Directory}", dirName);
                _logger.LogDebug("Full directory path for debugging: {Directory}", directory);
            }
        }
    }

    private static string GetContentType(string format)
    {
        return format.ToLowerInvariant() switch
        {
            "pdf" => Constants.ContentTypes.Pdf,
            "doc" => Constants.ContentTypes.Doc,
            "docx" => Constants.ContentTypes.Docx,
            "xlsx" => Constants.ContentTypes.Xlsx,
            "pptx" => Constants.ContentTypes.Pptx,
            "txt" => Constants.ContentTypes.Txt,
            "csv" => Constants.ContentTypes.Csv,
            "html" or "htm" => Constants.ContentTypes.Html,
            "xml" => Constants.ContentTypes.Xml,
            _ => Constants.ContentTypes.OctetStream
        };
    }
}
