using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using FileConversionApi.Services;
using System.ComponentModel.DataAnnotations;

namespace FileConversionApi.Controllers;

/// <summary>
/// File conversion API controller
/// Handles file upload and conversion requests
/// </summary>
[ApiController]
[Route("api")]
[Produces("application/json")]
public class ConversionController : ControllerBase
{
    private readonly ILogger<ConversionController> _logger;
    private readonly IDocumentService _documentService;
    private readonly IInputValidator _inputValidator;
    private readonly IConversionValidator _conversionValidator;
    private readonly ISemaphoreService _semaphoreService;
    private readonly IPerformanceMonitor _performanceMonitor;
    private readonly ITelemetryService _telemetryService;

    public ConversionController(
        ILogger<ConversionController> logger,
        IDocumentService documentService,
        IInputValidator inputValidator,
        IConversionValidator conversionValidator,
        ISemaphoreService semaphoreService,
        IPerformanceMonitor performanceMonitor,
        ITelemetryService telemetryService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _documentService = documentService ?? throw new ArgumentNullException(nameof(documentService));
        _inputValidator = inputValidator ?? throw new ArgumentNullException(nameof(inputValidator));
        _conversionValidator = conversionValidator ?? throw new ArgumentNullException(nameof(conversionValidator));
        _semaphoreService = semaphoreService ?? throw new ArgumentNullException(nameof(semaphoreService));
        _performanceMonitor = performanceMonitor ?? throw new ArgumentNullException(nameof(performanceMonitor));
        _telemetryService = telemetryService ?? throw new ArgumentNullException(nameof(telemetryService));
    }

    /// <summary>
    /// Get API information and discovery
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiInfo), 200)]
    public IActionResult GetApiInfo()
    {
        var apiInfo = new ApiInfo
        {
            Name = "File Conversion API",
            Version = "2.0.0",
            Description = "Office document conversion service",
            SupportedFormats = new ApiFormats
            {
                Input = _conversionValidator.GetSupportedInputFormats(),
                Output = _conversionValidator.GetSupportedTargetFormats("pdf") // Sample target formats
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
    /// Get supported file formats and conversions
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
                    ["csv"] = new() { "xlsx", "pdf" },
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
    /// Convert uploaded file to target format
    /// </summary>
    [HttpPost("convert")]
    [RequestSizeLimit(50 * 1024 * 1024)] // 50MB limit
    [ProducesResponseType(typeof(ConversionResponse), 200)]
    [ProducesResponseType(typeof(ErrorResponse), 400)]
    [ProducesResponseType(typeof(ErrorResponse), 500)]
    public async Task<IActionResult> ConvertFile(
        IFormFile file,
        [FromForm, Required] string targetFormat,
        [FromQuery] bool? metadata = null)
    {
        var operationId = Guid.NewGuid().ToString();
        _logger.LogInformation("Conversion request started - ID: {OperationId}", operationId);

        try
        {
            // Start performance monitoring
            _performanceMonitor.StartOperation($"convert_{operationId}");

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
            var conversionValidation = _conversionValidator.ValidateConversion(inputFormat, targetFormat);
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
                // Create temporary input file
                var tempInputPath = Path.Combine(Path.GetTempPath(), $"{operationId}_input{Path.GetExtension(file.FileName)}");
                var tempOutputPath = Path.Combine(Path.GetTempPath(), $"{operationId}_output.{targetFormat}");

                await using (var stream = new FileStream(tempInputPath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Perform conversion based on input type
                // Perform conversion
                ConversionResult result = await _documentService.ConvertAsync(tempInputPath, tempOutputPath, inputFormat, targetFormat);

                // Clean up input file
                if (System.IO.File.Exists(tempInputPath))
                {
                    System.IO.File.Delete(tempInputPath);
                }

                // Log telemetry
                await _telemetryService.LogConversionAsync(new ConversionTelemetry
                {
                    InputFormat = inputFormat,
                    TargetFormat = targetFormat,
                    FileSize = file.Length,
                    ProcessingTimeMs = result.ProcessingTimeMs ?? 0,
                    Success = result.Success,
                    Error = result.Error
                });

                if (!result.Success)
                {
                    _logger.LogError("Conversion failed: {Error}", result.Error);

                    // Clean up failed output file
                    if (System.IO.File.Exists(tempOutputPath))
                    {
                        System.IO.File.Delete(tempOutputPath);
                    }

                    return StatusCode(500, new ErrorResponse
                    {
                        Error = "Conversion failed",
                        Details = new List<string> { result.Error ?? "Unknown error" }
                    });
                }

                // Return file with optional metadata
                var fileBytes = await System.IO.File.ReadAllBytesAsync(tempOutputPath);

                // Clean up output file
                System.IO.File.Delete(tempOutputPath);

                // End performance monitoring
                _performanceMonitor.EndOperation($"convert_{operationId}");

                if (metadata == true)
                {
                    return Ok(new ConversionResponse
                    {
                        Success = true,
                        FileName = $"{Path.GetFileNameWithoutExtension(file.FileName)}.{targetFormat}",
                        FileSize = fileBytes.Length,
                        ProcessingTimeMs = result.ProcessingTimeMs,
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
            _logger.LogError(ex, "Unexpected error during file conversion - Operation ID: {OperationId}", operationId);

            // Log error telemetry
            await _telemetryService.LogErrorAsync(new ErrorTelemetry
            {
                Operation = "file_conversion",
                Error = ex.Message,
                StackTrace = ex.StackTrace,
                Timestamp = DateTime.UtcNow
            });

            return StatusCode(500, new ErrorResponse
            {
                Error = "Internal server error",
                Details = new List<string> { $"An unexpected error occurred. Contact support with operation ID: {operationId}" }
            });
        }
    }

    /// <summary>
    /// Check if format is an image format
    /// </summary>

    /// <summary>
    /// Get content type for file format
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
            "png" => "image/png",
            "jpg" or "jpeg" => "image/jpeg",
            "gif" => "image/gif",
            "bmp" => "image/bmp",
            "tiff" or "tif" => "image/tiff",
            "svg" => "image/svg+xml",
            _ => "application/octet-stream"
        };
    }
}

/// <summary>
/// API information response
/// </summary>
public class ApiInfo
{
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public ApiFormats SupportedFormats { get; set; } = new();
    public List<ApiEndpoint> Endpoints { get; set; } = new();
}

/// <summary>
/// API formats
/// </summary>
public class ApiFormats
{
    public List<string> Input { get; set; } = new();
    public List<string> Output { get; set; } = new();
}

/// <summary>
/// API endpoint
/// </summary>
public class ApiEndpoint
{
    public string Method { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

/// <summary>
/// Supported formats response
/// </summary>
public class SupportedFormatsResponse
{
    public DocumentFormats Documents { get; set; } = new();
    public ImageFormats Images { get; set; } = new();
}

/// <summary>
/// Document formats
/// </summary>
public class DocumentFormats
{
    public List<string> Input { get; set; } = new();
    public Dictionary<string, List<string>> Conversions { get; set; } = new();
}

/// <summary>
/// Image formats
/// </summary>
public class ImageFormats
{
    public List<string> Input { get; set; } = new();
    public List<string> Output { get; set; } = new();
}

/// <summary>
/// Conversion response
/// </summary>
public class ConversionResponse
{
    public bool Success { get; set; }
    public string? FileName { get; set; }
    public long? FileSize { get; set; }
    public long? ProcessingTimeMs { get; set; }
    public string? ConversionMethod { get; set; }
    public string? ContentType { get; set; }
    public byte[]? Data { get; set; }
}

/// <summary>
/// Error response
/// </summary>
public class ErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public List<string>? Details { get; set; }
}
