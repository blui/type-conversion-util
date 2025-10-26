using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using FileConversionApi.Services;
using FileConversionApi.Models;
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
            Version = "0.3.0",
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
                Input = new List<string> {
                    // Microsoft Office
                    "doc", "docx", "pdf", "xlsx", "csv", "pptx", "txt",
                    // LibreOffice native
                    "odt", "ods", "odp", "odg", "odf",
                    // OpenOffice
                    "sxw", "sxc", "sxi", "sxd",
                    // Other
                    "rtf", "xml", "html", "htm"
                },
                Conversions = new Dictionary<string, List<string>>
                {
                    ["doc"] = new() { "pdf", "txt", "docx", "rtf", "odt", "html" },
                    ["docx"] = new() { "pdf", "txt", "doc" },
                    ["pdf"] = new() { "docx", "txt" },
                    ["xlsx"] = new() { "csv", "pdf" },
                    ["csv"] = new() { "xlsx" },
                    ["pptx"] = new() { "pdf" },
                    ["txt"] = new() { "pdf", "docx" },
                    ["xml"] = new() { "pdf" },
                    ["html"] = new() { "pdf" },
                    ["htm"] = new() { "pdf" },
                    ["odt"] = new() { "pdf", "docx" },
                    ["ods"] = new() { "pdf", "xlsx" },
                    ["odp"] = new() { "pdf", "pptx" },
                    ["odg"] = new() { "pdf" },
                    ["rtf"] = new() { "pdf" },
                    ["sxw"] = new() { "pdf" },
                    ["sxc"] = new() { "pdf" },
                    ["sxi"] = new() { "pdf" },
                    ["sxd"] = new() { "pdf" }
                }
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
        [FromQuery] bool? metadata = null)
    {
        var operationId = Guid.NewGuid().ToString();
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("Conversion request started - ID: {OperationId}", operationId);

        try
        {
            // Validate input file
            var fileValidation = _inputValidator.ValidateFile(file);
            if (!fileValidation.IsValid)
            {
                _logger.LogWarning("File validation failed: {Errors}", string.Join(", ", fileValidation.Errors!));
                return BadRequest(new ErrorResponse
                {
                    Error = "Invalid file",
                    Details = fileValidation.Errors
                });
            }

            // Determine input format from file extension
            var inputFormat = Path.GetExtension(file.FileName)?.TrimStart('.').ToLowerInvariant();
            if (string.IsNullOrEmpty(inputFormat))
            {
                return BadRequest(new ErrorResponse { Error = "Unable to determine input file format" });
            }

            // Validate conversion
            var conversionValidation = _inputValidator.ValidateConversion(inputFormat, targetFormat);
            if (!conversionValidation.IsValid)
            {
                _logger.LogWarning("Conversion validation failed: {Errors}",
                    string.Join(", ", conversionValidation.Errors!));
                return BadRequest(new ErrorResponse
                {
                    Error = "Unsupported conversion",
                    Details = conversionValidation.Errors
                });
            }

            // Acquire semaphore for concurrency control
            await _semaphoreService.AcquireAsync();

            try
            {
                // Create operation-specific subdirectories for complete isolation
                // This preserves original filenames for true 1:1 conversion fidelity
                var tempUploadDir = GetAbsolutePath(_fileConfig.TempDirectory);
                var tempOutputDir = GetAbsolutePath(_fileConfig.OutputDirectory);

                var operationUploadDir = Path.Combine(tempUploadDir, operationId);
                var operationOutputDir = Path.Combine(tempOutputDir, operationId);

                Directory.CreateDirectory(operationUploadDir);
                Directory.CreateDirectory(operationOutputDir);

                // Preserve exact original filename for 1:1 conversion fidelity
                // Field codes like {FILENAME} in headers/footers will evaluate correctly
                var sanitizedFileName = SanitizeFileName(file.FileName);
                var tempInputPath = Path.Combine(operationUploadDir, sanitizedFileName);

                var originalFileNameWithoutExt = Path.GetFileNameWithoutExtension(sanitizedFileName);
                var tempOutputPath = Path.Combine(operationOutputDir, $"{originalFileNameWithoutExt}.{targetFormat}");

                // Save uploaded file with exact original name
                await using (var stream = new FileStream(tempInputPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Perform conversion
                ConversionResult result = await _documentService.ConvertAsync(tempInputPath, tempOutputPath, inputFormat, targetFormat);

                // Log conversion result
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
                    _logger.LogError("Conversion failed: {Error}", result.Error);

                    // Clean up operation directories
                    CleanupOperationDirectories(operationUploadDir, operationOutputDir);

                    return StatusCode(500, new ErrorResponse
                    {
                        Error = "Conversion failed",
                        Details = new List<string> { result.Error ?? "Unknown error" }
                    });
                }

                // Read converted file before cleanup
                var fileBytes = await System.IO.File.ReadAllBytesAsync(tempOutputPath);

                // Clean up operation directories after reading output
                CleanupOperationDirectories(operationUploadDir, operationOutputDir);

                if (metadata == true)
                {
                    return Ok(new ConversionResponse
                    {
                        Success = true,
                        FileName = $"{Path.GetFileNameWithoutExtension(file.FileName)}.{targetFormat}",
                        FileSize = fileBytes.Length,
                        ProcessingTimeMs = result.ProcessingTimeMs ?? stopwatch.ElapsedMilliseconds,
                        ConversionMethod = result.ConversionMethod,
                        ContentType = GetContentType(targetFormat),
                        Data = fileBytes
                    });
                }

                // Return file directly
                return base.File(fileBytes, GetContentType(targetFormat),
                    $"{Path.GetFileNameWithoutExtension(file.FileName)}.{targetFormat}");
            }
            finally
            {
                // Always release semaphore
                _semaphoreService.Release();
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Error in operation {Operation} at {Timestamp}: {Error} (Operation ID: {OperationId})",
                "file_conversion",
                DateTime.UtcNow,
                ex.Message,
                operationId);

            return StatusCode(500, new ErrorResponse
            {
                Error = "Internal server error",
                Details = new List<string> { $"An unexpected error occurred. Contact support with operation ID: {operationId}" }
            });
        }
    }

    /// <summary>
    /// Resolves a path to absolute form.
    /// If the path is already absolute, returns it unchanged.
    /// If relative, combines it with the application's base directory.
    /// </summary>
    private static string GetAbsolutePath(string path)
    {
        if (Path.IsPathRooted(path))
        {
            return path;
        }

        return Path.Combine(AppContext.BaseDirectory, path);
    }

    /// <summary>
    /// Sanitizes a filename by removing unsafe characters and enforcing length limits.
    /// Preserves original filename structure for document field codes.
    /// </summary>
    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "document";

        // Replace invalid characters with underscores
        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));

        // Enforce length limit
        if (sanitized.Length <= Constants.FileHandling.MaxSanitizedFileNameLength)
            return sanitized;

        var ext = Path.GetExtension(sanitized);
        var maxNameLength = Constants.FileHandling.MaxSanitizedFileNameLength - ext.Length;
        var name = Path.GetFileNameWithoutExtension(sanitized);

        return maxNameLength > 0 ? name[..maxNameLength] + ext : "file" + ext;
    }

    /// <summary>
    /// Cleans up operation-specific temporary directories.
    /// Deletes all files and the directory itself to ensure no orphaned files remain.
    /// </summary>
    private void CleanupOperationDirectories(params string[] directories)
    {
        foreach (var directory in directories)
        {
            try
            {
                if (Directory.Exists(directory))
                {
                    Directory.Delete(directory, recursive: true);
                    _logger.LogDebug("Cleaned up operation directory: {Directory}", directory);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean up operation directory: {Directory}", directory);
            }
        }
    }

    /// <summary>
    /// Maps file extensions to MIME content types for HTTP responses.
    /// </summary>
    private static string GetContentType(string format)
    {
        return format.ToLowerInvariant() switch
        {
            "pdf" => "application/pdf",
            "doc" => "application/msword",
            "docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "xlsx" => "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "pptx" => "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            "txt" => "text/plain",
            "csv" => "text/csv",
            "rtf" => "application/rtf",
            "odt" => "application/vnd.oasis.opendocument.text",
            "ods" => "application/vnd.oasis.opendocument.spreadsheet",
            "odp" => "application/vnd.oasis.opendocument.presentation",
            "html" or "htm" => "text/html",
            "xml" => "application/xml",
            _ => "application/octet-stream"
        };
    }
}
