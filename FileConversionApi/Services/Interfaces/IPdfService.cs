using System.Threading.Tasks;

namespace FileConversionApi.Services;

/// <summary>
/// Interface for PDF processing services
/// Handles PDF generation and text extraction
/// </summary>
public interface IPdfService
{
    /// <summary>
    /// Create PDF from text content
    /// </summary>
    Task<ConversionResult> CreatePdfFromTextAsync(string text, string outputPath);

    /// <summary>
    /// Extract text from PDF file
    /// </summary>
    Task<ConversionResult> ExtractTextFromPdfAsync(string inputPath, string outputPath);
}
