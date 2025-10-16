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

    /// <summary>
    /// Convert PSD file to standard image format
    /// </summary>
    Task<ConversionResult> ConvertPsdToImageAsync(string inputPath, string outputPath, string targetFormat = "png");

    /// <summary>
    /// Convert SVG file to raster image format
    /// </summary>
    Task<ConversionResult> ConvertSvgToImageAsync(string inputPath, string outputPath, string targetFormat = "png");

    /// <summary>
    /// Extract specific page from multi-page TIFF
    /// </summary>
    Task<ConversionResult> ConvertMultiPageTiffAsync(string inputPath, string outputPath, int pageIndex = 0);

    /// <summary>
    /// Convert multi-page TIFF to PDF
    /// </summary>
    Task<ConversionResult> ConvertTiffToPdfAsync(string inputPath, string outputPath);
}
