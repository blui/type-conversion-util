using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.ComponentModel.DataAnnotations;

namespace FileConversionApi.Services;

/// <summary>
/// Input validation service implementation
/// Validates file uploads and conversion requests
/// </summary>
public class InputValidator : IInputValidator
{
    private readonly ILogger<InputValidator> _logger;

    // Supported file formats (Office documents only)
    private readonly HashSet<string> _supportedFormats = new(StringComparer.OrdinalIgnoreCase)
    {
        "pdf", "doc", "docx", "xlsx", "pptx", "txt", "html", "htm", "csv", "xml",
        "rtf", "odt", "ods", "odp", "odg", "sxw", "sxc", "sxi", "sxd"
    };

    // Maximum file sizes (in bytes)
    private const long MaxFileSize = 50 * 1024 * 1024; // 50MB

    public InputValidator(ILogger<InputValidator> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public ValidationResult ValidateFile(IFormFile file)
    {
        var errors = new List<string>();

        // Check if file exists
        if (file == null || file.Length == 0)
        {
            errors.Add("File is required and cannot be empty");
            return new ValidationResult { IsValid = false, Errors = errors };
        }

        // Check file size
        if (file.Length > MaxFileSize)
        {
            errors.Add($"File size ({file.Length} bytes) exceeds maximum allowed size ({MaxFileSize} bytes)");
        }

        // Check filename
        if (string.IsNullOrWhiteSpace(file.FileName))
        {
            errors.Add("Filename is required");
        }
        else
        {
            // Validate filename format (basic security check)
            if (!IsValidFilename(file.FileName))
            {
                errors.Add("Filename contains invalid characters or is malformed");
            }
        }

        // Check file extension
        var extension = Path.GetExtension(file.FileName)?.TrimStart('.');
        if (string.IsNullOrEmpty(extension) || !_supportedFormats.Contains(extension))
        {
            errors.Add($"File type '{extension}' is not supported. Supported formats: {string.Join(", ", _supportedFormats)}");
        }

        // Check for potentially malicious content types
        // Allow application/octet-stream as fallback for files without proper MIME type
        if (!string.IsNullOrEmpty(file.ContentType) &&
            file.ContentType != "application/octet-stream" &&
            !IsValidContentType(file.ContentType))
        {
            errors.Add($"Content type '{file.ContentType}' is not allowed");
        }

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }

    /// <inheritdoc/>
    public ValidationResult ValidateConversion(string inputFormat, string targetFormat)
    {
        var errors = new List<string>();

        // Validate input format
        if (string.IsNullOrEmpty(inputFormat))
        {
            errors.Add("Input format is required");
        }
        else if (!_supportedFormats.Contains(inputFormat.ToLowerInvariant()))
        {
            errors.Add($"Input format '{inputFormat}' is not supported");
        }

        // Validate target format
        if (string.IsNullOrEmpty(targetFormat))
        {
            errors.Add("Target format is required");
        }
        else if (!_supportedFormats.Contains(targetFormat.ToLowerInvariant()))
        {
            errors.Add($"Target format '{targetFormat}' is not supported");
        }

        // Validate conversion compatibility (if both formats are valid)
        if (errors.Count == 0)
        {
            if (!IsValidConversion(inputFormat.ToLowerInvariant(), targetFormat.ToLowerInvariant()))
            {
                errors.Add($"Conversion from {inputFormat} to {targetFormat} is not supported");
            }
        }

        return new ValidationResult
        {
            IsValid = errors.Count == 0,
            Errors = errors
        };
    }

    /// <summary>
    /// Validate filename for security and format compliance
    /// </summary>
    private static bool IsValidFilename(string filename)
    {
        if (string.IsNullOrWhiteSpace(filename))
            return false;

        // Check for path traversal attempts
        if (filename.Contains("..") || filename.Contains("/") || filename.Contains("\\"))
            return false;

        // Check for invalid characters
        var invalidChars = Path.GetInvalidFileNameChars();
        if (filename.Any(c => invalidChars.Contains(c)))
            return false;

        // Check length
        if (filename.Length > 255)
            return false;

        // Check for hidden files
        if (filename.StartsWith("."))
            return false;

        return true;
    }

    /// <summary>
    /// Validate content type for security
    /// </summary>
    private static bool IsValidContentType(string contentType)
    {
        var allowedTypes = new[]
        {
            "application/pdf",
            "application/msword",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "application/vnd.ms-excel",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            "application/vnd.ms-powerpoint",
            "application/vnd.openxmlformats-officedocument.presentationml.presentation",
            "application/rtf",
            "application/vnd.oasis.opendocument.text",
            "application/vnd.oasis.opendocument.spreadsheet",
            "application/vnd.oasis.opendocument.presentation",
            "text/plain",
            "text/html",
            "text/csv",
            "text/xml",
            "application/xml"
        };

        return allowedTypes.Contains(contentType.ToLowerInvariant());
    }

    /// <inheritdoc/>
    public List<string> GetSupportedInputFormats()
    {
        return _supportedFormats.ToList();
    }

    /// <inheritdoc/>
    public List<string> GetSupportedTargetFormats(string inputFormat)
    {
        if (string.IsNullOrEmpty(inputFormat))
            return new List<string>();

        var normalizedInput = inputFormat.ToLowerInvariant();
        var supportedConversions = GetSupportedConversions();

        if (supportedConversions.TryGetValue(normalizedInput, out var targets))
        {
            return targets;
        }

        return new List<string>();
    }

    /// <summary>
    /// Check if conversion between formats is supported
    /// </summary>
    private static bool IsValidConversion(string inputFormat, string targetFormat)
    {
        var supportedConversions = GetSupportedConversions();
        return supportedConversions.TryGetValue(inputFormat, out var targets) &&
               targets.Contains(targetFormat);
    }

    /// <summary>
    /// Get dictionary of supported conversions
    /// </summary>
    private static Dictionary<string, List<string>> GetSupportedConversions()
    {
        return new Dictionary<string, List<string>>
        {
            ["doc"] = new() { "pdf", "txt", "docx", "rtf", "odt", "html", "htm" },
            ["docx"] = new() { "pdf", "txt", "doc" },
            ["pdf"] = new() { "docx", "doc", "txt" },
            ["xlsx"] = new() { "csv", "pdf" },
            ["csv"] = new() { "xlsx" },
            ["pptx"] = new() { "pdf" },
            ["txt"] = new() { "pdf", "docx", "doc" },
            ["xml"] = new() { "pdf" },
            ["html"] = new() { "pdf" },
            ["htm"] = new() { "pdf" },
            ["rtf"] = new() { "pdf" },
            ["odt"] = new() { "pdf", "docx" },
            ["ods"] = new() { "pdf", "xlsx" },
            ["odp"] = new() { "pdf", "pptx" },
            ["odg"] = new() { "pdf" },
            ["sxw"] = new() { "pdf" },
            ["sxc"] = new() { "pdf" },
            ["sxi"] = new() { "pdf" },
            ["sxd"] = new() { "pdf" }
        };
    }
}
