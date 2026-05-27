namespace FileConversionApi.Models;

/// <summary>
/// Carries the success or failure outcome from a conversion service back to the controller.
/// Success populates <see cref="OutputPath"/>; failure populates <see cref="Error"/> and
/// <see cref="FailureReason"/> for status-code mapping in the controller.
/// </summary>
public class ConversionResult
{
    /// <summary>
    /// True when the conversion ran to completion and produced the file at <see cref="OutputPath"/>.
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Engine-level failure detail. Populated only when <see cref="Success"/> is false; never
    /// serialized to the client for scoped DOC/DOCX-to-HTML conversions (CWE-209), and demoted
    /// off Info-level logs everywhere else.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Disk location of the produced file when <see cref="Success"/> is true.
    /// </summary>
    public string? OutputPath { get; set; }

    /// <summary>
    /// Wall-clock time the conversion took. Stamped by the service layer (LibreOfficeService,
    /// PdfService, SpreadsheetService) so the controller logs it without restarting its own
    /// stopwatch.
    /// </summary>
    public long? ProcessingTimeMs { get; set; }

    /// <summary>
    /// Technology or library used to perform the conversion (e.g., "LibreOffice", "iText7").
    /// </summary>
    public string? ConversionMethod { get; set; }

    /// <summary>
    /// Categorizes the failure so callers map it to a status code without parsing the
    /// <see cref="Error"/> text. Defaults to <see cref="FailureReason.None"/>.
    /// </summary>
    public FailureReason FailureReason { get; set; } = FailureReason.None;
}
