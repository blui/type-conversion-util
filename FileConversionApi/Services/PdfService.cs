using Microsoft.Extensions.Logging;
using System.Text;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Kernel.Font;
using iText.IO.Font.Constants;
using PdfDocument = iText.Kernel.Pdf.PdfDocument;
using FileConversionApi.Utilities;
using FileConversionApi.Models;

namespace FileConversionApi.Services;

/// <summary>
/// iText7-backed PDF operations. Both operations are sync under the hood; the Task surface
/// threads cancellation through the file-I/O boundaries (and, for text extraction, between
/// pages so a multi-thousand-page PDF aborts promptly).
/// </summary>
public class PdfService
{
    private readonly ILogger<PdfService> _logger;

    public PdfService(ILogger<PdfService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<ConversionResult> CreatePdfFromTextAsync(string text, string outputPath, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var outputFileName = PathSanitizer.GetSafeFileName(outputPath);
            _logger.LogInformation("Creating PDF from text - File: {OutputFile}", outputFileName);
            _logger.LogDebug("Full output path for debugging: {OutputPath}", outputPath);

            FileSystemHelper.EnsureDirectoryExists(outputPath);

            await using var stream = File.Create(outputPath);
            using var writer = new PdfWriter(stream);
            using var pdf = new PdfDocument(writer);
            using var document = new Document(pdf);

            var font = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);

            var paragraphs = text.Split(new[] { "\n\n" }, StringSplitOptions.RemoveEmptyEntries);

            foreach (var paragraph in paragraphs)
            {
                if (string.IsNullOrWhiteSpace(paragraph))
                    continue;

                var lines = WrapText(paragraph, Constants.TextProcessing.DefaultLineLength);

                foreach (var line in lines)
                {
                    var paragraphElement = new Paragraph(line)
                        .SetFont(font)
                        .SetFontSize(12)
                        .SetMarginBottom(5);

                    document.Add(paragraphElement);
                }

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
    public async Task<ConversionResult> ExtractTextFromPdfAsync(string inputPath, string outputPath, CancellationToken cancellationToken = default)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var inputFileName = PathSanitizer.GetSafeFileName(inputPath);
            _logger.LogInformation("Extracting text from PDF - File: {InputFile}", inputFileName);
            _logger.LogDebug("Full input path for debugging: {InputPath}", inputPath);

            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            using var reader = new iText.Kernel.Pdf.PdfReader(inputPath);
            using var document = new iText.Kernel.Pdf.PdfDocument(reader);

            var extractedText = new StringBuilder();

            for (int i = 1; i <= document.GetNumberOfPages(); i++)
            {
                // iText page extraction is synchronous; honor cancellation between pages so a
                // multi-thousand-page PDF can abort promptly when the client disconnects.
                cancellationToken.ThrowIfCancellationRequested();

                var page = document.GetPage(i);
                var text = iText.Kernel.Pdf.Canvas.Parser.PdfTextExtractor.GetTextFromPage(page);

                if (!string.IsNullOrEmpty(text))
                {
                    extractedText.AppendLine(text);
                    extractedText.AppendLine();
                }
            }

            await System.IO.File.WriteAllTextAsync(outputPath, extractedText.ToString(), cancellationToken);

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
    /// Greedy word-wrap to a fixed column width. Words longer than <paramref name="maxLength"/>
    /// are emitted on their own line rather than split (matches the PDF-rendering convention
    /// for unbreakable tokens like URLs).
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
