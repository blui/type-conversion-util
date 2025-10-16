using System.Threading.Tasks;

namespace FileConversionApi.Services;

/// <summary>
/// Interface for image processing services
/// Handles image format conversions and processing
/// </summary>
public interface IImageService
{
    /// <summary>
    /// Convert image to PDF format
    /// </summary>
    Task<ConversionResult> ConvertToPdfAsync(string inputPath, string outputPath, string inputFormat);

    /// <summary>
    /// Convert image between supported formats
    /// </summary>
    Task<ConversionResult> ConvertFormatAsync(string inputPath, string outputPath, string inputFormat, string targetFormat);
}
