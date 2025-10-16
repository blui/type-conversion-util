using Microsoft.Extensions.Logging;

namespace FileConversionApi.Services;

/// <summary>
/// Conversion validation service implementation
/// Validates conversion compatibility and supported formats
/// </summary>
public class ConversionValidator : IConversionValidator
{
    private readonly ILogger<ConversionValidator> _logger;

    // Supported input formats
    private readonly List<string> _inputFormats = new()
    {
        // Microsoft Office formats
        "pdf", "doc", "docx", "xlsx", "csv", "pptx", "txt",
        // LibreOffice native formats
        "odt", "ods", "odp", "odg", "odf",
        // OpenOffice formats
        "sxw", "sxc", "sxi", "sxd",
        // Other document formats
        "rtf", "xml", "html", "htm",
        // Image formats
        "jpg", "jpeg", "png", "gif", "bmp", "tiff", "tif", "svg", "psd"
    };

    // Supported target formats
    private readonly List<string> _targetFormats = new()
    {
        // Primary targets
        "pdf", "docx", "txt", "csv",
        // Image targets
        "png", "jpg", "bmp",
        // LibreOffice native formats
        "odt", "ods", "odp"
    };

    public ConversionValidator(ILogger<ConversionValidator> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public bool IsValidConversion(string inputFormat, string targetFormat)
    {
        if (string.IsNullOrEmpty(inputFormat) || string.IsNullOrEmpty(targetFormat))
            return false;

        var normalizedInput = inputFormat.ToLowerInvariant();
        var normalizedTarget = targetFormat.ToLowerInvariant();

        // Check if formats are supported
        if (!_inputFormats.Contains(normalizedInput) || !_targetFormats.Contains(normalizedTarget))
            return false;

        // Define specific conversion rules
        return IsConversionSupported(normalizedInput, normalizedTarget);
    }

    /// <inheritdoc/>
    public List<string> GetSupportedInputFormats()
    {
        return new List<string>(_inputFormats);
    }

    /// <inheritdoc/>
    public List<string> GetSupportedTargetFormats(string inputFormat)
    {
        if (string.IsNullOrEmpty(inputFormat))
            return new List<string>();

        var normalizedInput = inputFormat.ToLowerInvariant();
        var supportedTargets = new List<string>();

        // Return supported target formats for the given input format
        foreach (var target in _targetFormats)
        {
            if (IsConversionSupported(normalizedInput, target))
            {
                supportedTargets.Add(target);
            }
        }

        return supportedTargets;
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
        else if (!_inputFormats.Contains(inputFormat.ToLowerInvariant()))
        {
            errors.Add($"Input format '{inputFormat}' is not supported");
        }

        // Validate target format
        if (string.IsNullOrEmpty(targetFormat))
        {
            errors.Add("Target format is required");
        }
        else if (!_targetFormats.Contains(targetFormat.ToLowerInvariant()))
        {
            errors.Add($"Target format '{targetFormat}' is not supported");
        }

        // Validate conversion compatibility
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
    /// Check if specific conversion pair is supported
    /// </summary>
    private static bool IsConversionSupported(string inputFormat, string targetFormat)
    {
        // LibreOffice-supported formats can generally convert to/from PDF and between compatible types
        var libreOfficeFormats = new[] { "doc", "docx", "xlsx", "pptx", "odt", "ods", "odp", "odg", "odf", "sxw", "sxc", "sxi", "sxd", "rtf" };

        // If both formats are LibreOffice-supported, they can generally convert between each other via LibreOffice
        if (libreOfficeFormats.Contains(inputFormat) && targetFormat == "pdf")
            return true;

        if (inputFormat == "pdf" && libreOfficeFormats.Contains(targetFormat))
            return true;

        return (inputFormat, targetFormat) switch
        {
            // Document conversions
            ("doc", "pdf") => true,
            ("doc", "txt") => true,
            ("doc", "docx") => true,
            ("docx", "pdf") => true,
            ("docx", "txt") => true,
            ("pdf", "docx") => true,
            ("pdf", "txt") => true,

            // Spreadsheet conversions
            ("xlsx", "csv") => true,
            ("xlsx", "pdf") => true,
            ("csv", "xlsx") => true,
            ("csv", "pdf") => true,

            // Presentation conversions
            ("pptx", "pdf") => true,

            // Text conversions
            ("txt", "pdf") => true,
            ("txt", "docx") => true,

            // XML/HTML conversions
            ("xml", "pdf") => true,
            ("html", "pdf") => true,
            ("htm", "pdf") => true,

            // Image conversions
            ("jpg", "pdf") => true,
            ("jpg", "png") => true,
            ("jpg", "bmp") => true,
            ("jpeg", "pdf") => true,
            ("jpeg", "png") => true,
            ("jpeg", "bmp") => true,
            ("png", "pdf") => true,
            ("png", "jpg") => true,
            ("png", "bmp") => true,
            ("gif", "pdf") => true,
            ("gif", "png") => true,
            ("gif", "jpg") => true,
            ("bmp", "pdf") => true,
            ("bmp", "jpg") => true,
            ("bmp", "png") => true,
            ("tiff", "pdf") => true,
            ("tiff", "jpg") => true,
            ("tiff", "png") => true,
            ("tif", "pdf") => true,
            ("tif", "jpg") => true,
            ("tif", "png") => true,
            ("svg", "pdf") => true,
            ("svg", "png") => true,

            // LibreOffice native format conversions
            ("odt", "pdf") => true,
            ("odt", "docx") => true,
            ("ods", "pdf") => true,
            ("ods", "xlsx") => true,
            ("odp", "pdf") => true,
            ("odp", "pptx") => true,
            ("odg", "pdf") => true,

            // Default: not supported
            _ => false
        };
    }
}
