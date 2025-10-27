using Microsoft.Extensions.Logging;
using System.Text;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Kernel.Font;
using iText.IO.Font.Constants;
using PdfDocument = iText.Kernel.Pdf.PdfDocument;
using FileConversionApi.Utilities;

namespace FileConversionApi.Services;

/// <summary>
/// PDF processing service implementation
/// Handles PDF generation and text extraction operations
/// </summary>
public class PdfService : IPdfService
{
    private readonly ILogger<PdfService> _logger;

    public PdfService(ILogger<PdfService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<ConversionResult> CreatePdfFromTextAsync(string text, string outputPath)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Creating PDF from text: {OutputPath}", outputPath);

            // Ensure output directory exists
            FileSystemHelper.EnsureDirectoryExists(outputPath);

            await using var stream = File.Create(outputPath);
            using var writer = new PdfWriter(stream);
            using var pdf = new PdfDocument(writer);
            using var document = new Document(pdf);

            // Configure fonts
            var font = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);

            // Split text into paragraphs
            var paragraphs = text.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var paragraph in paragraphs)
            {
                if (string.IsNullOrWhiteSpace(paragraph))
                    continue;

                // Simple word wrapping for the paragraph
                var lines = WrapText(paragraph, Constants.TextProcessing.DefaultLineLength);

                foreach (var line in lines)
                {
                    var paragraphElement = new Paragraph(line)
                        .SetFont(font)
                        .SetFontSize(12)
                        .SetMarginBottom(5);

                    document.Add(paragraphElement);
                }

                // Add space between paragraphs
                document.Add(new Paragraph("\n"));
            }

            document.Close();

            stopwatch.Stop();

            _logger.LogInformation("PDF creation completed successfully in {Time}ms", stopwatch.ElapsedMilliseconds);

            return new ConversionResult
            {
                Success = true,
                OutputPath = outputPath,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                ConversionMethod = "iText7"
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "PDF creation from text failed");

            return new ConversionResult
            {
                Success = false,
                Error = $"PDF creation failed: {ex.Message}",
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    /// <inheritdoc/>
    public async Task<ConversionResult> ExtractTextFromPdfAsync(string inputPath, string outputPath)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Extracting text from PDF: {InputPath}", inputPath);

            // Ensure output directory exists
            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // Open PDF document with iText
            using var reader = new iText.Kernel.Pdf.PdfReader(inputPath);
            using var document = new iText.Kernel.Pdf.PdfDocument(reader);

            var extractedText = new StringBuilder();

            // Extract text from each page
            for (int i = 1; i <= document.GetNumberOfPages(); i++)
            {
                var page = document.GetPage(i);
                var text = iText.Kernel.Pdf.Canvas.Parser.PdfTextExtractor.GetTextFromPage(page);

                if (!string.IsNullOrEmpty(text))
                {
                    extractedText.AppendLine(text);
                    extractedText.AppendLine(); // Add paragraph break
                }
            }

            // Write extracted text to output file
            await System.IO.File.WriteAllTextAsync(outputPath, extractedText.ToString());

            stopwatch.Stop();

            _logger.LogInformation("Text extraction completed successfully in {Time}ms. Pages: {PageCount}",
                stopwatch.ElapsedMilliseconds, document.GetNumberOfPages());

            return new ConversionResult
            {
                Success = true,
                OutputPath = outputPath,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                ConversionMethod = "iText7"
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "PDF text extraction failed");

            return new ConversionResult
            {
                Success = false,
                Error = $"Text extraction failed: {ex.Message}",
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    /// <summary>
    /// Simple text wrapping for PDF generation
    /// </summary>
    private static List<string> WrapText(string text, int maxLength)
    {
        var lines = new List<string>();
        var words = text.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        var currentLine = string.Empty;

        foreach (var word in words)
        {
            var testLine = string.IsNullOrEmpty(currentLine) ? word : $"{currentLine} {word}";

            if (testLine.Length > maxLength && !string.IsNullOrEmpty(currentLine))
            {
                lines.Add(currentLine);
                currentLine = word;
            }
            else
            {
                currentLine = testLine;
            }
        }

        if (!string.IsNullOrEmpty(currentLine))
        {
            lines.Add(currentLine);
        }

        return lines;
    }
}
