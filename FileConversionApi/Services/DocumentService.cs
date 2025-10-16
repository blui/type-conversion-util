using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Diagnostics;
using CsvHelper;
using NPOI.XSSF.UserModel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;

namespace FileConversionApi.Services;

/// <summary>
/// Document conversion service implementation
/// Orchestrates document processing and delegates to specialized services
/// </summary>
public class DocumentService : IDocumentService
{
    private readonly ILogger<DocumentService> _logger;
    private readonly IConversionEngine _conversionEngine;
    private readonly IPdfService _pdfService;
    private readonly ILibreOfficeService _libreOfficeService;
    private readonly ISpreadsheetService _spreadsheetService;
    private readonly IXmlProcessingService _xmlProcessingService;

    // Conversion handler mappings
    private readonly Dictionary<string, Func<string, string, Task<ConversionResult>>> _handlers;

    public DocumentService(
        ILogger<DocumentService> logger,
        IConversionEngine conversionEngine,
        IPdfService pdfService,
        ILibreOfficeService libreOfficeService,
        ISpreadsheetService spreadsheetService,
        IXmlProcessingService xmlProcessingService)
    {
        _logger = logger;
        _conversionEngine = conversionEngine;
        _pdfService = pdfService;
        _libreOfficeService = libreOfficeService;
        _spreadsheetService = spreadsheetService;
        _xmlProcessingService = xmlProcessingService;

        // Initialize conversion handlers
        _handlers = new Dictionary<string, Func<string, string, Task<ConversionResult>>>
        {
            // Microsoft Office formats
            ["doc-pdf"] = _conversionEngine.DocxToPdfAsync,
            ["docx-pdf"] = _conversionEngine.DocxToPdfAsync,
            ["pdf-docx"] = PdfToDocxAsync,
            ["pdf-txt"] = _pdfService.ExtractTextFromPdfAsync,
            ["xlsx-csv"] = _spreadsheetService.XlsxToCsvAsync,
            ["csv-xlsx"] = _spreadsheetService.CsvToXlsxAsync,
            ["xlsx-pdf"] = _conversionEngine.XlsxToPdfAsync,
            ["pptx-pdf"] = _conversionEngine.PptxToPdfAsync,
            ["txt-pdf"] = _pdfService.CreatePdfFromTextAsync,
            ["txt-docx"] = TxtToDocxAsync,
            ["xml-pdf"] = XmlToPdfAsync,
            ["html-pdf"] = HtmlToPdfAsync,
            ["htm-pdf"] = HtmlToPdfAsync,

            // Advanced image formats
            ["psd-pdf"] = PsdToPdfAsync,
            ["svg-pdf"] = SvgToPdfAsync,
            ["psd-png"] = PsdToPngAsync,
            ["psd-jpg"] = PsdToJpgAsync,
            ["svg-png"] = SvgToPngAsync,
            ["svg-jpg"] = SvgToJpgAsync,
            ["tiff-pdf"] = TiffToPdfAsync,

            // LibreOffice native formats
            ["odt-pdf"] = _conversionEngine.OdtToPdfAsync,
            ["ods-pdf"] = _conversionEngine.OdsToPdfAsync,
            ["odp-pdf"] = _conversionEngine.OdpToPdfAsync,

            // OpenOffice formats (use LibreOffice for conversion)
            ["sxw-pdf"] = (input, output) => _libreOfficeService.ConvertAsync(input, output, "pdf"),
            ["sxc-pdf"] = (input, output) => _libreOfficeService.ConvertAsync(input, output, "pdf"),
            ["sxi-pdf"] = (input, output) => _libreOfficeService.ConvertAsync(input, output, "pdf"),
            ["sxd-pdf"] = (input, output) => _libreOfficeService.ConvertAsync(input, output, "pdf"),

            // RTF support
            ["rtf-pdf"] = (input, output) => _libreOfficeService.ConvertAsync(input, output, "pdf")
        };
    }

    /// <inheritdoc/>
    public async Task<ConversionResult> ConvertAsync(string inputPath, string outputPath, string inputFormat, string targetFormat)
    {
        var conversionKey = $"{inputFormat}-{targetFormat}";
        _logger.LogInformation("Converting {InputFormat} to {TargetFormat}: {InputPath}",
            inputFormat, targetFormat, inputPath);

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
                _logger.LogInformation("Conversion completed successfully: {OutputPath}", outputPath);
            }
            else
            {
                _logger.LogError("Conversion failed: {Error}", result.Error);
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during document conversion");
            return new ConversionResult
            {
                Success = false,
                Error = $"Conversion failed: {ex.Message}"
            };
        }
    }

    // Implemented conversion handlers
    private async Task<ConversionResult> PdfToDocxAsync(string inputPath, string outputPath)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Converting PDF to DOCX: {InputPath}", inputPath);

            // For now, use LibreOffice for PDF to DOCX conversion
            var result = await _libreOfficeService.ConvertAsync(inputPath, outputPath, "docx");

            stopwatch.Stop();
            result.ProcessingTimeMs = stopwatch.ElapsedMilliseconds;
            result.ConversionMethod = "LibreOffice";

            return result;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "PDF to DOCX conversion failed");
            return new ConversionResult
            {
                Success = false,
                Error = $"PDF to DOCX conversion failed: {ex.Message}",
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
    }


    private async Task<ConversionResult> TxtToDocxAsync(string inputPath, string outputPath)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Converting TXT to DOCX: {InputPath}", inputPath);

            var text = await File.ReadAllTextAsync(inputPath);

            // Create DOCX document
            var body = new Body();
            foreach (var line in text.Split('\n'))
            {
                var paragraph = new Paragraph();
                var run = new Run();
                var textElement = new Text(line);
                run.Append(textElement);
                paragraph.Append(run);
                body.Append(paragraph);
            }

            var document = new Document();
            document.Append(body);

            using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
            document.Save(fs);

            stopwatch.Stop();

            _logger.LogInformation("TXT to DOCX conversion completed successfully in {Time}ms",
                stopwatch.ElapsedMilliseconds);

            return new ConversionResult
            {
                Success = true,
                OutputPath = outputPath,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                ConversionMethod = "DocumentFormat.OpenXml"
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "TXT to DOCX conversion failed");
            return new ConversionResult
            {
                Success = false,
                Error = $"TXT to DOCX conversion failed: {ex.Message}",
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    private async Task<ConversionResult> XmlToPdfAsync(string inputPath, string outputPath)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Converting XML to PDF: {InputPath}", inputPath);

            var xmlContent = await File.ReadAllTextAsync(inputPath);

            // Create PDF with XML content
            await using var stream = File.Create(outputPath);
            using var writer = new iText.Kernel.Pdf.PdfWriter(stream);
            using var pdf = new iText.Kernel.Pdf.PdfDocument(writer);
            using var document = new iText.Layout.Document(pdf);

            // Add XML content as formatted text
            var font = iText.Kernel.Font.PdfFontFactory.CreateFont(iText.IO.Font.Constants.StandardFonts.HELVETICA);
            var paragraph = new iText.Layout.Element.Paragraph(xmlContent)
                .SetFont(font)
                .SetFontSize(10);

            document.Add(paragraph);

            stopwatch.Stop();

            _logger.LogInformation("XML to PDF conversion completed successfully in {Time}ms",
                stopwatch.ElapsedMilliseconds);

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
            _logger.LogError(ex, "XML to PDF conversion failed");
            return new ConversionResult
            {
                Success = false,
                Error = $"XML to PDF conversion failed: {ex.Message}",
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    private async Task<ConversionResult> HtmlToPdfAsync(string inputPath, string outputPath)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Converting HTML to PDF: {InputPath}", inputPath);

            var htmlContent = await File.ReadAllTextAsync(inputPath);

            // Create PDF with HTML content (simplified - could use a proper HTML to PDF library)
            await using var stream = File.Create(outputPath);
            using var writer = new iText.Kernel.Pdf.PdfWriter(stream);
            using var pdf = new iText.Kernel.Pdf.PdfDocument(writer);
            using var document = new iText.Layout.Document(pdf);

            // Add HTML content as plain text (basic implementation)
            var font = iText.Kernel.Font.PdfFontFactory.CreateFont(iText.IO.Font.Constants.StandardFonts.HELVETICA);
            var paragraph = new iText.Layout.Element.Paragraph(htmlContent)
                .SetFont(font)
                .SetFontSize(10);

            document.Add(paragraph);

            stopwatch.Stop();

            _logger.LogInformation("HTML to PDF conversion completed successfully in {Time}ms",
                stopwatch.ElapsedMilliseconds);

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
            _logger.LogError(ex, "HTML to PDF conversion failed");
            return new ConversionResult
            {
                Success = false,
                Error = $"HTML to PDF conversion failed: {ex.Message}",
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    // Advanced image processing handlers
    private async Task<ConversionResult> PsdToPdfAsync(string inputPath, string outputPath)
    {
        return await _imageService.ConvertToPdfAsync(inputPath, outputPath, "psd");
    }

    private async Task<ConversionResult> SvgToPdfAsync(string inputPath, string outputPath)
    {
        return await _imageService.ConvertToPdfAsync(inputPath, outputPath, "svg");
    }

    private async Task<ConversionResult> PsdToPngAsync(string inputPath, string outputPath)
    {
        return await _imageService.ConvertPsdToImageAsync(inputPath, outputPath, "png");
    }

    private async Task<ConversionResult> PsdToJpgAsync(string inputPath, string outputPath)
    {
        return await _imageService.ConvertPsdToImageAsync(inputPath, outputPath, "jpg");
    }

    private async Task<ConversionResult> SvgToPngAsync(string inputPath, string outputPath)
    {
        return await _imageService.ConvertSvgToImageAsync(inputPath, outputPath, "png");
    }

    private async Task<ConversionResult> SvgToJpgAsync(string inputPath, string outputPath)
    {
        return await _imageService.ConvertSvgToImageAsync(inputPath, outputPath, "jpg");
    }

    private async Task<ConversionResult> TiffToPdfAsync(string inputPath, string outputPath)
    {
        return await _imageService.ConvertTiffToPdfAsync(inputPath, outputPath);
    }
}
