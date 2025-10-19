using System.Threading.Tasks;

namespace FileConversionApi.Services;

/// <summary>
/// Interface for document conversion engine
/// Handles high-level conversion coordination
/// </summary>
public interface IConversionEngine
{
    /// <summary>
    /// Convert DOC file to PDF using LibreOffice
    /// </summary>
    Task<ConversionResult> DocToPdfAsync(string inputPath, string outputPath);

    /// <summary>
    /// Convert DOCX file to PDF using LibreOffice
    /// </summary>
    Task<ConversionResult> DocxToPdfAsync(string inputPath, string outputPath);

    /// <summary>
    /// Convert XLSX file to PDF using LibreOffice
    /// </summary>
    Task<ConversionResult> XlsxToPdfAsync(string inputPath, string outputPath);

    /// <summary>
    /// Convert PPTX file to PDF using LibreOffice
    /// </summary>
    Task<ConversionResult> PptxToPdfAsync(string inputPath, string outputPath);

    /// <summary>
    /// Convert ODT file to PDF using LibreOffice
    /// </summary>
    Task<ConversionResult> OdtToPdfAsync(string inputPath, string outputPath);

    /// <summary>
    /// Convert ODS file to PDF using LibreOffice
    /// </summary>
    Task<ConversionResult> OdsToPdfAsync(string inputPath, string outputPath);

    /// <summary>
    /// Convert ODP file to PDF using LibreOffice
    /// </summary>
    Task<ConversionResult> OdpToPdfAsync(string inputPath, string outputPath);
}

/// <summary>
/// Result of a conversion operation
/// </summary>
public class ConversionResult
{
    public bool Success { get; set; }
    public string? Error { get; set; }
    public string? OutputPath { get; set; }
    public long? ProcessingTimeMs { get; set; }
    public string? ConversionMethod { get; set; }
    public string? AdditionalInfo { get; set; }
}
