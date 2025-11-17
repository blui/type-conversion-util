using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;
using FileConversionApi.Models;

namespace FileConversionApi.Services;

/// <summary>
/// Validates file uploads and conversion requests.
/// </summary>
public class InputValidator : IInputValidator
{
    private readonly ILogger<InputValidator> _logger;
    private readonly FileHandlingConfig _config;

    // Supported conversions (cached for performance)
    private static readonly Dictionary<string, List<string>> _supportedConversions = new()
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
    };

    public InputValidator(ILogger<InputValidator> logger, IOptions<FileHandlingConfig> fileHandlingConfig)
    {
        _logger = logger;
        _config = fileHandlingConfig?.Value ?? throw new ArgumentNullException(nameof(fileHandlingConfig));
    }

    /// <inheritdoc/>
    public ValidationResult ValidateFile(IFormFile file)
    {
        var errors = new List<string>();

        if (file == null || file.Length == 0)
        {
            errors.Add("File is required and cannot be empty");
            return new ValidationResult { IsValid = false, Errors = errors };
        }

        if (file.Length > _config.MaxFileSize)
        {
            errors.Add($"File size ({file.Length} bytes) exceeds maximum allowed size ({_config.MaxFileSize} bytes)");
        }

        if (string.IsNullOrWhiteSpace(file.FileName))
        {
            errors.Add("Filename is required");
        }
        else
        {
            if (!IsValidFilename(file.FileName))
            {
                errors.Add("Filename contains invalid characters or is malformed");
            }
        }

        var extension = Path.GetExtension(file.FileName)?.TrimStart('.');
        if (string.IsNullOrEmpty(extension) || !Constants.SupportedFormats.All.Contains(extension))
        {
            errors.Add($"File type '{extension}' is not supported. Supported formats: {string.Join(", ", Constants.SupportedFormats.All)}");
        }
        else
        {
            // Verify file content matches claimed extension (magic byte validation)
            if (!VerifyFileContent(file, extension))
            {
                errors.Add($"File content does not match the extension '{extension}'. The file may be corrupted or misidentified.");
            }
        }

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
        else if (!Constants.SupportedFormats.All.Contains(inputFormat.ToLowerInvariant()))
        {
            errors.Add($"Input format '{inputFormat}' is not supported");
        }

        // Validate target format
        if (string.IsNullOrEmpty(targetFormat))
        {
            errors.Add("Target format is required");
        }
        else if (!Constants.SupportedFormats.All.Contains(targetFormat.ToLowerInvariant()))
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
    /// Validates filename security.
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
    /// Validates content type for security.
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
        return Constants.SupportedFormats.All.ToList();
    }

    /// <inheritdoc/>
    public List<string> GetSupportedTargetFormats(string inputFormat)
    {
        if (string.IsNullOrEmpty(inputFormat))
            return new List<string>();

        var normalizedInput = inputFormat.ToLowerInvariant();
        return _supportedConversions.TryGetValue(normalizedInput, out var targets) ? targets : new List<string>();
    }

    /// <summary>
    /// Check if conversion between formats is supported.
    /// </summary>
    private static bool IsValidConversion(string inputFormat, string targetFormat)
    {
        return _supportedConversions.TryGetValue(inputFormat, out var targets) && targets.Contains(targetFormat);
    }

    /// <summary>
    /// Verifies file content matches the claimed extension using magic byte validation.
    /// Prevents file type confusion attacks and polyglot file uploads.
    /// </summary>
    private bool VerifyFileContent(IFormFile file, string extension)
    {
        try
        {
            using var stream = file.OpenReadStream();
            var buffer = new byte[8];
            var bytesRead = stream.Read(buffer, 0, buffer.Length);

            if (bytesRead < 4)
            {
                _logger.LogWarning("File too small for magic byte validation: {FileName}", file.FileName);
                return false;
            }

            var normalizedExtension = extension.ToLowerInvariant();

            return normalizedExtension switch
            {
                // PDF: %PDF (0x25 0x50 0x44 0x46)
                "pdf" => buffer[0] == 0x25 && buffer[1] == 0x50 && buffer[2] == 0x44 && buffer[3] == 0x46,

                // Office Open XML formats (DOCX, XLSX, PPTX): ZIP signature (0x50 0x4B 0x03 0x04)
                "docx" or "xlsx" or "pptx" => buffer[0] == 0x50 && buffer[1] == 0x4B && buffer[2] == 0x03 && buffer[3] == 0x04,

                // Legacy DOC: Microsoft Office signature (0xD0 0xCF 0x11 0xE0 0xA1 0xB1 0x1A 0xE1)
                "doc" => buffer[0] == 0xD0 && buffer[1] == 0xCF && buffer[2] == 0x11 && buffer[3] == 0xE0,

                // CSV: Text-based, validate as text
                "csv" => IsValidTextFile(buffer, bytesRead),

                // TXT: Text-based, validate as text
                "txt" => IsValidTextFile(buffer, bytesRead),

                // XML: Starts with '<' (0x3C) or BOM
                "xml" => IsValidXmlFile(buffer, bytesRead),

                // HTML/HTM: Starts with '<' or BOM
                "html" or "htm" => IsValidHtmlFile(buffer, bytesRead),

                _ => false
            };
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Magic byte validation failed for file: {FileName}", file.FileName);
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Magic byte validation failed for file: {FileName}", file.FileName);
            return false;
        }
        catch (ObjectDisposedException ex)
        {
            _logger.LogError(ex, "Magic byte validation failed for file: {FileName}", file.FileName);
            return false;
        }
        catch (NotSupportedException ex)
        {
            _logger.LogError(ex, "Magic byte validation failed for file: {FileName}", file.FileName);
            return false;
        }
    }

    /// <summary>
    /// Validates that file content appears to be valid text (UTF-8, UTF-16, or ASCII).
    /// </summary>
    private static bool IsValidTextFile(byte[] buffer, int bytesRead)
    {
        // Check for UTF-8 BOM (0xEF 0xBB 0xBF)
        if (bytesRead >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
            return true;

        // Check for UTF-16 BOM (0xFF 0xFE or 0xFE 0xFF)
        if (bytesRead >= 2 && ((buffer[0] == 0xFF && buffer[1] == 0xFE) || (buffer[0] == 0xFE && buffer[1] == 0xFF)))
            return true;

        // Check for printable ASCII/UTF-8 characters
        // Allow common text characters: letters, numbers, whitespace, punctuation
        for (int i = 0; i < bytesRead; i++)
        {
            byte b = buffer[i];

            // Allow: tab (0x09), newline (0x0A), carriage return (0x0D), printable ASCII (0x20-0x7E)
            // Also allow UTF-8 continuation bytes (0x80-0xBF) and start bytes (0xC0-0xF7)
            if (!(b == 0x09 || b == 0x0A || b == 0x0D ||
                  (b >= 0x20 && b <= 0x7E) ||
                  (b >= 0x80 && b <= 0xBF) ||
                  (b >= 0xC0 && b <= 0xF7)))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Validates that file content appears to be valid XML.
    /// </summary>
    private static bool IsValidXmlFile(byte[] buffer, int bytesRead)
    {
        // Check for UTF-8 BOM
        if (bytesRead >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
            return bytesRead > 3 && buffer[3] == 0x3C; // '<' after BOM

        // Check for UTF-16 BOM
        if (bytesRead >= 2 && ((buffer[0] == 0xFF && buffer[1] == 0xFE) || (buffer[0] == 0xFE && buffer[1] == 0xFF)))
            return true;

        // XML must start with '<' (0x3C), possibly preceded by whitespace
        for (int i = 0; i < bytesRead; i++)
        {
            if (buffer[i] == 0x3C) // '<'
                return true;

            // Allow leading whitespace
            if (buffer[i] != 0x20 && buffer[i] != 0x09 && buffer[i] != 0x0A && buffer[i] != 0x0D)
                return false;
        }

        return false;
    }

    /// <summary>
    /// Validates that file content appears to be valid HTML.
    /// </summary>
    private static bool IsValidHtmlFile(byte[] buffer, int bytesRead)
    {
        // HTML has same structure requirements as XML (starts with '<')
        return IsValidXmlFile(buffer, bytesRead);
    }
}
