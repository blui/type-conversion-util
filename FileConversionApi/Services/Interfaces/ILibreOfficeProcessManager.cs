using FileConversionApi.Models;

namespace FileConversionApi.Services.Interfaces;

/// <summary>
/// Interface for managing LibreOffice process execution
/// </summary>
public interface ILibreOfficeProcessManager
{
    /// <summary>
    /// Convert a document using LibreOffice
    /// </summary>
    Task<ConversionResult> ConvertAsync(string inputPath, string outputPath, string targetFormat);
}
