using Microsoft.AspNetCore.Http;

namespace FileConversionApi.Services;

/// <summary>
/// Interface for input validation services
/// Validates file uploads and conversion requests
/// </summary>
public interface IInputValidator
{
    /// <summary>
    /// Validate file upload
    /// </summary>
    ValidationResult ValidateFile(IFormFile file);

    /// <summary>
    /// Validate conversion request
    /// </summary>
    ValidationResult ValidateConversion(string inputFormat, string targetFormat);
}
