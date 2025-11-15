using System.Threading.Tasks;
using FileConversionApi.Models;

namespace FileConversionApi.Services;

/// <summary>
/// Interface for LibreOffice SDK integration
/// Handles direct LibreOffice operations
/// </summary>
public interface ILibreOfficeService
{
    /// <summary>
    /// Execute LibreOffice conversion
    /// </summary>
    Task<ConversionResult> ConvertAsync(string inputPath, string outputPath, string targetFormat);

    /// <summary>
    /// Check if LibreOffice is available
    /// </summary>
    Task<bool> IsAvailableAsync();
}
