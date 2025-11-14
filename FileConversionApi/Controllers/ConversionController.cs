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
                Input = new List<string> {
                    // Microsoft Office
                    "doc", "docx", "pdf", "xlsx", "csv", "pptx", "txt",
                    // Other
                    "xml", "html", "htm"
                },
                Conversions = new Dictionary<string, List<string>>
                {
                    ["doc"] = new() { "pdf", "txt", "docx", "html", "htm" },
                    ["docx"] = new() { "pdf", "txt", "doc" },
                    ["pdf"] = new() { "docx", "doc", "txt" },
                    ["xlsx"] = new() { "csv", "pdf" },
                    ["csv"] = new() { "xlsx" },
                    ["pptx"] = new() { "pdf" },
                    ["txt"] = new() { "pdf", "docx", "doc" },
                    ["xml"] = new() { "pdf" },
                    ["html"] = new() { "pdf" },
                    ["htm"] = new() { "pdf" }
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
        var operationId = UniqueIdGenerator.GenerateId();
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation("Conversion request started - ID: {OperationId}", operationId);

        try
        {
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

            var inputFormat = Path.GetExtension(file.FileName)?.TrimStart('.').ToLowerInvariant();
            if (string.IsNullOrEmpty(inputFormat))
            {
                return BadRequest(new ErrorResponse { Error = "Unable to determine input file format" });
            }

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

            await _semaphoreService.AcquireAsync();

            try
            {
                // Create isolated directories to preserve original filenames
                var tempUploadDir = GetAbsolutePath(_fileConfig.TempDirectory);
                var tempOutputDir = GetAbsolutePath(_fileConfig.OutputDirectory);

                var operationUploadDir = Path.Combine(tempUploadDir, operationId);
                var operationOutputDir = Path.Combine(tempOutputDir, operationId);

                Directory.CreateDirectory(operationUploadDir);
                Directory.CreateDirectory(operationOutputDir);

                // Preserve original filename for field codes like {FILENAME}
                var sanitizedFileName = SanitizeFileName(file.FileName, inputFormat);
                var tempInputPath = Path.Combine(operationUploadDir, sanitizedFileName);

                var originalFileNameWithoutExt = Path.GetFileNameWithoutExtension(sanitizedFileName);
                var tempOutputPath = Path.Combine(operationOutputDir, $"{originalFileNameWithoutExt}.{targetFormat}");

                await using (var stream = new FileStream(tempInputPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                ConversionResult result = await _documentService.ConvertAsync(tempInputPath, tempOutputPath, inputFormat, targetFormat);

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
                    CleanupOperationDirectories(operationUploadDir, operationOutputDir);

                    return StatusCode(500, new ErrorResponse
                    {
                        Error = "Conversion failed",
                        Details = new List<string> { result.Error ?? "Unknown error" }
                    });
                }

                var fileBytes = await System.IO.File.ReadAllBytesAsync(tempOutputPath);
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

                return base.File(fileBytes, GetContentType(targetFormat),
                    $"{Path.GetFileNameWithoutExtension(file.FileName)}.{targetFormat}");
            }
            finally
            {
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
        var extensionWithDot = requiredExtension.StartsWith('.') ? requiredExtension : $".{requiredExtension}";

        if (string.IsNullOrWhiteSpace(fileName))
            return Constants.FileHandling.DefaultFileName + extensionWithDot;

        var invalidChars = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));

        // Ensure the sanitized filename has the correct extension
        var existingExtension = Path.GetExtension(sanitized);
        if (!existingExtension.Equals(extensionWithDot, StringComparison.OrdinalIgnoreCase))
        {
            sanitized = Path.GetFileNameWithoutExtension(sanitized) + extensionWithDot;
        }

        if (sanitized.Length <= Constants.FileHandling.MaxSanitizedFileNameLength)
            return sanitized;

        // Extension too long - use default filename
        if (extensionWithDot.Length >= Constants.FileHandling.MaxSanitizedFileNameLength)
            return Constants.FileHandling.DefaultFileName + extensionWithDot;

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
                    _logger.LogDebug("Cleaned up operation directory: {Directory}", directory);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean up operation directory: {Directory}", directory);
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
