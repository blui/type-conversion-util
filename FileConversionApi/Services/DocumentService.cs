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

    // Conversion handler mappings
    private readonly Dictionary<string, Func<string, string, Task<ConversionResult>>> _handlers;

    public DocumentService(
        ILogger<DocumentService> logger,
        IConversionEngine conversionEngine,
        IPdfService pdfService,
        ILibreOfficeService libreOfficeService)
    {
        _logger = logger;
        _conversionEngine = conversionEngine;
        _pdfService = pdfService;
        _libreOfficeService = libreOfficeService;

        // Initialize conversion handlers
        _handlers = new Dictionary<string, Func<string, string, Task<ConversionResult>>>
        {
            // Microsoft Office formats
            ["doc-pdf"] = _conversionEngine.DocxToPdfAsync,
            ["docx-pdf"] = _conversionEngine.DocxToPdfAsync,
            ["pdf-docx"] = PdfToDocxAsync,
            ["pdf-txt"] = _pdfService.ExtractTextFromPdfAsync,
            ["xlsx-csv"] = XlsxToCsvAsync,
            ["csv-xlsx"] = CsvToXlsxAsync,
            ["xlsx-pdf"] = _conversionEngine.XlsxToPdfAsync,
            ["pptx-pdf"] = _conversionEngine.PptxToPdfAsync,
            ["txt-pdf"] = _pdfService.CreatePdfFromTextAsync,
            ["txt-docx"] = TxtToDocxAsync,
            ["xml-pdf"] = XmlToPdfAsync,
            ["html-pdf"] = HtmlToPdfAsync,
            ["htm-pdf"] = HtmlToPdfAsync,

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

    private async Task<ConversionResult> XlsxToCsvAsync(string inputPath, string outputPath)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Converting XLSX to CSV: {InputPath}", inputPath);

            // Use NPOI to read XLSX and CsvHelper to write CSV
            using var fs = new FileStream(inputPath, FileMode.Open, FileAccess.Read);
            var workbook = new NPOI.XSSF.UserModel.XSSFWorkbook(fs);

            if (workbook.NumberOfSheets == 0)
            {
                return new ConversionResult
                {
                    Success = false,
                    Error = "No worksheets found in the Excel file"
                };
            }

            // Convert first sheet to CSV (can be enhanced for multi-sheet support)
            var sheet = (NPOI.XSSF.UserModel.XSSFSheet)workbook.GetSheetAt(0);
            var csvRecords = new List<string>();

            for (int i = sheet.FirstRowNum; i <= sheet.LastRowNum; i++)
            {
                var row = sheet.GetRow(i);
                if (row == null) continue;

                var csvRow = new List<string>();
                for (int j = row.FirstCellNum; j < row.LastCellNum; j++)
                {
                    var cell = row.GetCell(j);
                    var cellValue = cell?.ToString() ?? "";
                    csvRow.Add(cellValue);
                }

                csvRecords.Add(string.Join(",", csvRow.Select(v => $"\"{v.Replace("\"", "\"\"")}\"")));
            }

            await File.WriteAllLinesAsync(outputPath, csvRecords);

            stopwatch.Stop();

            _logger.LogInformation("XLSX to CSV conversion completed successfully in {Time}ms",
                stopwatch.ElapsedMilliseconds);

            return new ConversionResult
            {
                Success = true,
                OutputPath = outputPath,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                ConversionMethod = "NPOI+CsvHelper"
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "XLSX to CSV conversion failed");
            return new ConversionResult
            {
                Success = false,
                Error = $"XLSX to CSV conversion failed: {ex.Message}",
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    private async Task<ConversionResult> CsvToXlsxAsync(string inputPath, string outputPath)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Converting CSV to XLSX: {InputPath}", inputPath);

            // Use CsvHelper to read CSV and NPOI to write XLSX
            using var reader = new StreamReader(inputPath);
            using var csv = new CsvReader(reader, System.Globalization.CultureInfo.InvariantCulture);

            var records = new List<List<string>>();
            while (await csv.ReadAsync())
            {
                var record = new List<string>();
                for (int i = 0; csv.TryGetField<string>(i, out var field); i++)
                {
                    record.Add(field);
                }
                records.Add(record);
            }

            // Create XLSX workbook
            var workbook = new NPOI.XSSF.UserModel.XSSFWorkbook();
            var sheet = (NPOI.XSSF.UserModel.XSSFSheet)workbook.CreateSheet("Sheet1");

            for (int i = 0; i < records.Count; i++)
            {
                var row = sheet.CreateRow(i);
                var record = records[i];

                for (int j = 0; j < record.Count; j++)
                {
                    var cell = row.CreateCell(j);
                    cell.SetCellValue(record[j]);
                }
            }

            using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
            workbook.Write(fs);

            stopwatch.Stop();

            _logger.LogInformation("CSV to XLSX conversion completed successfully in {Time}ms",
                stopwatch.ElapsedMilliseconds);

            return new ConversionResult
            {
                Success = true,
                OutputPath = outputPath,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                ConversionMethod = "CsvHelper+NPOI"
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "CSV to XLSX conversion failed");
            return new ConversionResult
            {
                Success = false,
                Error = $"CSV to XLSX conversion failed: {ex.Message}",
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
}
