using System.Threading.Tasks;
using FileConversionApi.Models;

namespace FileConversionApi.Services;

/// <summary>
/// Interface for document conversion services
/// Orchestrates document processing and conversion operations
/// </summary>
public interface IDocumentService
{
    /// <summary>
    /// Convert document between supported formats
    /// </summary>
    Task<ConversionResult> ConvertAsync(string inputPath, string outputPath, string inputFormat, string targetFormat);
}
