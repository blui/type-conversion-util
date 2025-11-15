namespace FileConversionApi.Models;

/// <summary>
/// Represents the outcome of a document conversion operation.
/// </summary>
public class ConversionResult
{
    /// <summary>
    /// Indicates whether the conversion completed successfully.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Error message if the conversion failed, otherwise null.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// File system path to the converted output file.
    /// </summary>
    public string? OutputPath { get; set; }

    /// <summary>
    /// Time taken to complete the conversion in milliseconds.
    /// </summary>
    public long? ProcessingTimeMs { get; set; }

    /// <summary>
    /// Technology or library used to perform the conversion (e.g., "LibreOffice", "iText7").
    /// </summary>
    public string? ConversionMethod { get; set; }

    /// <summary>
    /// Supplementary information about the conversion process.
    /// </summary>
    public string? AdditionalInfo { get; set; }
}
