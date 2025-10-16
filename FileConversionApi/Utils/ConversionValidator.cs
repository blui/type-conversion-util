using Microsoft.Extensions.Logging;
using iText.Kernel.Pdf.Canvas.Parser;
using iText.Kernel.Pdf;
using System.Diagnostics;

namespace FileConversionApi.Utils;

/// <summary>
/// Post-conversion validation utility
/// Performs quality checks on converted documents to detect issues
/// </summary>
public class ConversionValidator : IConversionValidator
{
    private readonly ILogger<ConversionValidator> _logger;

    // Validation thresholds and constants
    private const long MinPdfSizeBytes = 10000; // Minimum PDF size (10KB) - smaller may indicate conversion failure
    private const double MaxPdfSizeMultiplier = 10.0; // PDF should not be more than 10x the source size
    private const double MaxTextExtractionRatio = 0.5; // Maximum ratio of extracted text to file size

    public ConversionValidator(ILogger<ConversionValidator> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ConversionValidationResult> ValidateDocxToPdfAsync(string docxPath, string pdfPath)
    {
        var result = new ConversionValidationResult
        {
            InputPath = docxPath,
            OutputPath = pdfPath,
            ConversionType = "DOCX->PDF",
            Issues = new List<string>(),
            Warnings = new List<string>(),
            Info = new Dictionary<string, object>()
        };

        try
        {
            // Check files exist
            if (!File.Exists(docxPath))
            {
                result.Issues.Add("Source DOCX file not found");
                result.IsValid = false;
                return result;
            }

            if (!File.Exists(pdfPath))
            {
                result.Issues.Add("Output PDF file not found");
                result.IsValid = false;
                return result;
            }

            // Get file sizes
            var docxInfo = new FileInfo(docxPath);
            var pdfInfo = new FileInfo(pdfPath);
            var docxSize = docxInfo.Length;
            var pdfSize = pdfInfo.Length;

            result.Info["docxSize"] = docxSize;
            result.Info["pdfSize"] = pdfSize;

            // Validate PDF size
            if (pdfSize < MinPdfSizeBytes)
            {
                result.Issues.Add($"PDF file too small ({pdfSize} bytes) - possible conversion failure");
                result.IsValid = false;
            }

            if (pdfSize > docxSize * MaxPdfSizeMultiplier)
            {
                result.Warnings.Add($"PDF file unusually large ({pdfSize} vs {docxSize} bytes source)");
            }

            // Extract and validate text content
            var textExtraction = await ExtractPdfTextAsync(pdfPath);
            if (textExtraction.Success)
            {
                result.Info["extractedTextLength"] = textExtraction.Text.Length;
                result.Info["pageCount"] = textExtraction.PageCount;

                // Check for empty PDF
                if (textExtraction.Text.Length == 0 && textExtraction.PageCount == 0)
                {
                    result.Issues.Add("PDF appears to be empty - no text or pages detected");
                    result.IsValid = false;
                }

                // Check text extraction ratio
                if (textExtraction.Text.Length > 0)
                {
                    var textRatio = (double)textExtraction.Text.Length / pdfSize;
                    if (textRatio > MaxTextExtractionRatio)
                    {
                        result.Warnings.Add($"Unusually high text extraction ratio ({textRatio:P2})");
                    }
                }
            }
            else
            {
                result.Warnings.Add($"Text extraction failed: {textExtraction.Error}");
            }

            // Validate file integrity
            if (!await IsValidPdfAsync(pdfPath))
            {
                result.Issues.Add("PDF file appears to be corrupted or invalid");
                result.IsValid = false;
            }

            result.IsValid = result.Issues.Count == 0;

            _logger.LogInformation("DOCX->PDF validation completed. Valid: {Valid}, Issues: {Issues}, Warnings: {Warnings}",
                result.IsValid, result.Issues.Count, result.Warnings.Count);

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DOCX->PDF validation failed");
            result.Issues.Add($"Validation error: {ex.Message}");
            result.IsValid = false;
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<ConversionValidationResult> ValidateXlsxToCsvAsync(string xlsxPath, string csvPath)
    {
        var result = new ConversionValidationResult
        {
            InputPath = xlsxPath,
            OutputPath = csvPath,
            ConversionType = "XLSX->CSV",
            Issues = new List<string>(),
            Warnings = new List<string>(),
            Info = new Dictionary<string, object>()
        };

        try
        {
            // Check files exist
            if (!File.Exists(xlsxPath))
            {
                result.Issues.Add("Source XLSX file not found");
                result.IsValid = false;
                return result;
            }

            if (!File.Exists(csvPath))
            {
                result.Issues.Add("Output CSV file not found");
                result.IsValid = false;
                return result;
            }

            // Get file sizes
            var xlsxInfo = new FileInfo(xlsxPath);
            var csvInfo = new FileInfo(csvPath);

            result.Info["xlsxSize"] = xlsxInfo.Length;
            result.Info["csvSize"] = csvInfo.Length;

            // Validate CSV content
            var csvContent = await File.ReadAllTextAsync(csvPath);
            if (string.IsNullOrWhiteSpace(csvContent))
            {
                result.Issues.Add("CSV file is empty");
                result.IsValid = false;
            }
            else
            {
                // Count lines and check for basic CSV structure
                var lines = csvContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
                result.Info["csvLines"] = lines.Length;

                if (lines.Length == 0)
                {
                    result.Issues.Add("CSV file contains no data lines");
                    result.IsValid = false;
                }
            }

            result.IsValid = result.Issues.Count == 0;

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "XLSX->CSV validation failed");
            result.Issues.Add($"Validation error: {ex.Message}");
            result.IsValid = false;
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<ConversionValidationResult> ValidateCsvToXlsxAsync(string csvPath, string xlsxPath)
    {
        var result = new ConversionValidationResult
        {
            InputPath = csvPath,
            OutputPath = xlsxPath,
            ConversionType = "CSV->XLSX",
            Issues = new List<string>(),
            Warnings = new List<string>(),
            Info = new Dictionary<string, object>()
        };

        try
        {
            // Check files exist
            if (!File.Exists(csvPath))
            {
                result.Issues.Add("Source CSV file not found");
                result.IsValid = false;
                return result;
            }

            if (!File.Exists(xlsxPath))
            {
                result.Issues.Add("Output XLSX file not found");
                result.IsValid = false;
                return result;
            }

            // Get file sizes
            var csvInfo = new FileInfo(csvPath);
            var xlsxInfo = new FileInfo(xlsxPath);

            result.Info["csvSize"] = csvInfo.Length;
            result.Info["xlsxSize"] = xlsxInfo.Length;

            // Basic XLSX validation (we can't easily read XLSX content without additional libraries)
            if (xlsxInfo.Length < 4096) // XLSX files are typically at least 4KB
            {
                result.Issues.Add("XLSX file unusually small - possible conversion failure");
                result.IsValid = false;
            }

            result.IsValid = result.Issues.Count == 0;

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CSV->XLSX validation failed");
            result.Issues.Add($"Validation error: {ex.Message}");
            result.IsValid = false;
        }

        return result;
    }

    /// <inheritdoc/>
    public async Task<ConversionValidationResult> ValidateImageConversionAsync(string inputPath, string outputPath, string conversionType)
    {
        var result = new ConversionValidationResult
        {
            InputPath = inputPath,
            OutputPath = outputPath,
            ConversionType = conversionType,
            Issues = new List<string>(),
            Warnings = new List<string>(),
            Info = new Dictionary<string, object>()
        };

        try
        {
            // Check files exist
            if (!File.Exists(inputPath))
            {
                result.Issues.Add("Source image file not found");
                result.IsValid = false;
                return result;
            }

            if (!File.Exists(outputPath))
            {
                result.Issues.Add("Output image file not found");
                result.IsValid = false;
                return result;
            }

            // Get file sizes
            var inputInfo = new FileInfo(inputPath);
            var outputInfo = new FileInfo(outputPath);

            result.Info["inputSize"] = inputInfo.Length;
            result.Info["outputSize"] = outputInfo.Length;

            // Check if output is reasonably sized
            if (outputInfo.Length < 1024) // Images should be at least 1KB
            {
                result.Issues.Add("Output image file too small - possible conversion failure");
                result.IsValid = false;
            }

            // Check if output is not excessively large (more than 50x input)
            if (outputInfo.Length > inputInfo.Length * 50)
            {
                result.Warnings.Add("Output image significantly larger than input");
            }

            result.IsValid = result.Issues.Count == 0;

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Image conversion validation failed");
            result.Issues.Add($"Validation error: {ex.Message}");
            result.IsValid = false;
        }

        return result;
    }

    /// <summary>
    /// Extract text from PDF for validation
    /// </summary>
    private async Task<PdfTextExtractionResult> ExtractPdfTextAsync(string pdfPath)
    {
        var result = new PdfTextExtractionResult();

        try
        {
            await using var pdfStream = new FileStream(pdfPath, FileMode.Open, FileAccess.Read);
            using var pdfDoc = new PdfDocument(new PdfReader(pdfStream));

            result.PageCount = pdfDoc.GetNumberOfPages();
            var text = new System.Text.StringBuilder();

            for (int i = 1; i <= result.PageCount; i++)
            {
                var page = pdfDoc.GetPage(i);
                var pageText = PdfTextExtractor.GetTextFromPage(page);
                text.Append(pageText);
            }

            result.Text = text.ToString();
            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.Error = ex.Message;
        }

        return result;
    }

    /// <summary>
    /// Basic PDF validity check
    /// </summary>
    private async Task<bool> IsValidPdfAsync(string pdfPath)
    {
        try
        {
            await using var pdfStream = new FileStream(pdfPath, FileMode.Open, FileAccess.Read);
            using var pdfDoc = new PdfDocument(new PdfReader(pdfStream));
            return pdfDoc.GetNumberOfPages() > 0;
        }
        catch
        {
            return false;
        }
    }
}

/// <summary>
/// PDF text extraction result
/// </summary>
internal class PdfTextExtractionResult
{
    public bool Success { get; set; }
    public string Text { get; set; } = string.Empty;
    public int PageCount { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Conversion validation result
/// </summary>
public class ConversionValidationResult
{
    public bool IsValid { get; set; } = true;
    public string? InputPath { get; set; }
    public string? OutputPath { get; set; }
    public string? ConversionType { get; set; }
    public List<string> Issues { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
    public Dictionary<string, object> Info { get; set; } = new();
}

/// <summary>
/// Conversion validator interface
/// </summary>
public interface IConversionValidator
{
    Task<ConversionValidationResult> ValidateDocxToPdfAsync(string docxPath, string pdfPath);
    Task<ConversionValidationResult> ValidateXlsxToCsvAsync(string xlsxPath, string csvPath);
    Task<ConversionValidationResult> ValidateCsvToXlsxAsync(string csvPath, string xlsxPath);
    Task<ConversionValidationResult> ValidateImageConversionAsync(string inputPath, string outputPath, string conversionType);
}
