using Microsoft.AspNetCore.Http;
using FileConversionApi.Models;

namespace FileConversionApi.Services;

/// <summary>
/// Upload-time guards for the /api/convert endpoint: filename, extension, size, MIME type, and
/// magic-byte verification of file content against the claimed extension. Also exposes the
/// matrix queries the controller uses to build the /api/supported-formats response.
/// </summary>
public interface IInputValidator
{
    /// <summary>
    /// Runs every per-file guard (size, filename, extension, magic-byte signature, MIME type)
    /// and returns the aggregated result. IsValid is true only when every guard passes.
    /// </summary>
    ValidationResult ValidateFile(IFormFile file);

    /// <summary>
    /// Confirms (inputFormat, targetFormat) is a supported pair in the authoritative
    /// <see cref="Constants.SupportedFormats.ConversionMatrix"/>.
    /// </summary>
    ValidationResult ValidateConversion(string inputFormat, string targetFormat);

    /// <summary>
    /// Returns the keys of the conversion matrix (every format accepted as an upload input).
    /// </summary>
    List<string> GetSupportedInputFormats();

    /// <summary>
    /// Returns the target formats reachable from <paramref name="inputFormat"/>, or an empty
    /// list when the input format is unknown.
    /// </summary>
    List<string> GetSupportedTargetFormats(string inputFormat);
}
