using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Diagnostics;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using FileConversionApi.Models;

namespace FileConversionApi.Services;

/// <summary>
/// Orchestrates document conversions and delegates to specialized services.
/// </summary>
public class DocumentService : IDocumentService
{
    private readonly ILogger<DocumentService> _logger;
    private readonly IPdfService _pdfService;
    private readonly ILibreOfficeService _libreOfficeService;
    private readonly ISpreadsheetService _spreadsheetService;

    // Conversion handler mappings
    private readonly Dictionary<string, Func<string, string, Task<ConversionResult>>> _handlers;

    public DocumentService(
        ILogger<DocumentService> logger,
        IPdfService pdfService,
        ILibreOfficeService libreOfficeService,
        ISpreadsheetService spreadsheetService)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _pdfService = pdfService ?? throw new ArgumentNullException(nameof(pdfService));
        _libreOfficeService = libreOfficeService ?? throw new ArgumentNullException(nameof(libreOfficeService));
        _spreadsheetService = spreadsheetService ?? throw new ArgumentNullException(nameof(spreadsheetService));

        // Initialize conversion handlers
        _handlers = new Dictionary<string, Func<string, string, Task<ConversionResult>>>
        {
            // Microsoft Office formats
            ["doc-pdf"] = ConvertWithLibreOfficeAsync("pdf"),
            ["docx-pdf"] = ConvertWithLibreOfficeAsync("pdf"),
            ["doc-txt"] = ConvertWithLibreOfficeAsync("txt"),
            ["docx-txt"] = ConvertWithLibreOfficeAsync("txt"),
            ["doc-html"] = ConvertWithLibreOfficeAsync("html"),
            ["doc-htm"] = ConvertWithLibreOfficeAsync("html"),
            ["doc-docx"] = ConvertWithLibreOfficeAsync("docx"),
            ["docx-doc"] = ConvertWithLibreOfficeAsync("doc"),
            ["pdf-doc"] = ConvertWithLibreOfficeAsync("doc"),
            ["pdf-docx"] = PdfToDocxAsync,
            ["txt-doc"] = TxtToDocAsync,
            ["txt-docx"] = TxtToDocxAsync,
            ["pdf-txt"] = _pdfService.ExtractTextFromPdfAsync,
            ["xlsx-csv"] = _spreadsheetService.XlsxToCsvAsync,
            ["csv-xlsx"] = _spreadsheetService.CsvToXlsxAsync,
            ["xlsx-pdf"] = ConvertWithLibreOfficeAsync("pdf"),
            ["pptx-pdf"] = ConvertWithLibreOfficeAsync("pdf"),
            ["txt-pdf"] = _pdfService.CreatePdfFromTextAsync,
            ["xml-pdf"] = XmlToPdfAsync,
            ["html-pdf"] = HtmlToPdfAsync,
            ["htm-pdf"] = HtmlToPdfAsync
        };
    }

    private Func<string, string, Task<ConversionResult>> ConvertWithLibreOfficeAsync(string targetFormat)
    {
        return (input, output) => _libreOfficeService.ConvertAsync(input, output, targetFormat);
    }

    /// <inheritdoc/>
    public async Task<ConversionResult> ConvertAsync(string inputPath, string outputPath, string inputFormat, string targetFormat)
    {
        var conversionKey = $"{inputFormat}-{targetFormat}";

        try
        {
            if (!_handlers.TryGetValue(conversionKey, out var handler))
            {
                return new ConversionResult
                {
                    Success = false,
                    Error = $"Conversion from {inputFormat} to {targetFormat} is not supported"
                };
            }

            var result = await handler(inputPath, outputPath);

            if (result.Success)
            {
                _logger.LogInformation("Conversion completed: {InputFormat} to {TargetFormat}", inputFormat, targetFormat);
            }
            else
            {
                _logger.LogError("Conversion failed: {Error}", result.Error);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Conversion error: {InputFormat} to {TargetFormat}", inputFormat, targetFormat);
            return new ConversionResult
            {
                Success = false,
                Error = $"Conversion failed: {ex.Message}"
            };
        }
    }

    private async Task<ConversionResult> PdfToDocxAsync(string inputPath, string outputPath)
    {
        return await _libreOfficeService.ConvertAsync(inputPath, outputPath, "docx");
    }

    private async Task<ConversionResult> TxtToDocxAsync(string inputPath, string outputPath)
    {
        return await TxtToWordDocumentAsync(inputPath, outputPath);
    }

    private async Task<ConversionResult> XmlToPdfAsync(string inputPath, string outputPath)
    {
        return await TextContentToPdfAsync(inputPath, outputPath);
    }

    private async Task<ConversionResult> HtmlToPdfAsync(string inputPath, string outputPath)
    {
        return await TextContentToPdfAsync(inputPath, outputPath);
    }

    private async Task<ConversionResult> TextContentToPdfAsync(string inputPath, string outputPath)
    {
        var textContent = await File.ReadAllTextAsync(inputPath);

        await using var stream = File.Create(outputPath);
        using var writer = new iText.Kernel.Pdf.PdfWriter(stream);
        using var pdf = new iText.Kernel.Pdf.PdfDocument(writer);
        using var document = new iText.Layout.Document(pdf);

        var font = iText.Kernel.Font.PdfFontFactory.CreateFont(iText.IO.Font.Constants.StandardFonts.HELVETICA);
        var paragraph = new iText.Layout.Element.Paragraph(textContent)
            .SetFont(font)
            .SetFontSize(10);

        document.Add(paragraph);

        return new ConversionResult
        {
            Success = true,
            OutputPath = outputPath,
            ConversionMethod = "iText7"
        };
    }

    private async Task<ConversionResult> TxtToDocAsync(string inputPath, string outputPath)
    {
        return await TxtToWordDocumentAsync(inputPath, outputPath);
    }

    private async Task<ConversionResult> TxtToWordDocumentAsync(string inputPath, string outputPath)
    {
        var text = await File.ReadAllTextAsync(inputPath);

        var body = new Body();
        foreach (var line in text.Split('\n'))
        {
            var paragraph = new Paragraph();
            var run = new Run();
            var textElement = new Text(line.TrimEnd('\r'));
            run.Append(textElement);
            paragraph.Append(run);
            body.Append(paragraph);
        }

        var document = new Document();
        document.Append(body);

        await using var fileStream = File.Create(outputPath);
        document.Save(fileStream);

        return new ConversionResult
        {
            Success = true,
            OutputPath = outputPath,
            ConversionMethod = "DocumentFormat.OpenXml"
        };
    }
}
