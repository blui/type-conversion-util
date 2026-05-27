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
    private readonly IDocxToHtmlPipeline _docxToHtmlPipeline;

    // Conversion handler mappings. CancellationToken is the last parameter so client-disconnect
    // (HttpContext.RequestAborted) and the per-engine internal timeouts both reach the underlying
    // process.WaitForExitAsync calls through a linked CTS at each process manager.
    private readonly Dictionary<string, Func<string, string, CancellationToken, Task<ConversionResult>>> _handlers;

    public DocumentService(
        ILogger<DocumentService> logger,
        IPdfService pdfService,
        ILibreOfficeService libreOfficeService,
        ISpreadsheetService spreadsheetService,
        IDocxToHtmlPipeline docxToHtmlPipeline)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _pdfService = pdfService ?? throw new ArgumentNullException(nameof(pdfService));
        _libreOfficeService = libreOfficeService ?? throw new ArgumentNullException(nameof(libreOfficeService));
        _spreadsheetService = spreadsheetService ?? throw new ArgumentNullException(nameof(spreadsheetService));
        _docxToHtmlPipeline = docxToHtmlPipeline ?? throw new ArgumentNullException(nameof(docxToHtmlPipeline));

        _handlers = new Dictionary<string, Func<string, string, CancellationToken, Task<ConversionResult>>>
        {
            // Microsoft Office formats
            ["doc-pdf"] = ConvertWithLibreOfficeAsync("pdf"),
            ["docx-pdf"] = ConvertWithLibreOfficeAsync("pdf"),
            ["doc-txt"] = ConvertWithLibreOfficeAsync("txt"),
            ["docx-txt"] = ConvertWithLibreOfficeAsync("txt"),
            // doc->html/htm rebuilt through the two-hop pipeline (CONV-06): the legacy single-hop
            // StarWriter HTML filter is superseded, not patched. All four DOC/DOCX->HTML/HTM keys
            // now share the identical ConvertToHtmlAsync path.
            ["doc-html"] = ConvertToHtmlAsync,
            ["doc-htm"] = ConvertToHtmlAsync,
            ["doc-docx"] = ConvertWithLibreOfficeAsync("docx"),
            ["docx-doc"] = ConvertWithLibreOfficeAsync("doc"),
            ["pdf-doc"] = ConvertWithLibreOfficeAsync("doc"),
            ["pdf-docx"] = ConvertWithLibreOfficeAsync("docx"),
            ["txt-doc"] = ConvertWithLibreOfficeAsync("doc"),
            ["txt-docx"] = TxtToDocxAsync,
            ["pdf-txt"] = (input, output, ct) => _pdfService.ExtractTextFromPdfAsync(input, output, ct),
            ["xlsx-csv"] = (input, output, ct) => _spreadsheetService.XlsxToCsvAsync(input, output, ct),
            ["csv-xlsx"] = (input, output, ct) => _spreadsheetService.CsvToXlsxAsync(input, output, ct),
            ["xlsx-pdf"] = ConvertWithLibreOfficeAsync("pdf"),
            ["pptx-pdf"] = ConvertWithLibreOfficeAsync("pdf"),
            // CreatePdfFromTextAsync takes (text, outputPath); the handler dictionary is keyed by
            // (inputPath, outputPath), so read the file first and pass its contents through. Binding
            // the method group directly would feed the upload's temp PATH into the PDF body.
            ["txt-pdf"] = async (input, output, ct) =>
                await _pdfService.CreatePdfFromTextAsync(
                    await File.ReadAllTextAsync(input, ct), output, ct),
            ["xml-pdf"] = XmlToPdfAsync,
            ["html-pdf"] = HtmlToPdfAsync,
            ["htm-pdf"] = HtmlToPdfAsync,

            // docx->html/htm via the two-hop pipeline; htm uses the identical delegate as html
            ["docx-html"] = ConvertToHtmlAsync,
            ["docx-htm"] = ConvertToHtmlAsync
        };

        // VAL-02: enforce matrix<->handler agreement structurally at startup (fail-fast).
        VerifyHandlerMatrixConsistency();
    }

    private Func<string, string, CancellationToken, Task<ConversionResult>> ConvertWithLibreOfficeAsync(string targetFormat)
    {
        return (input, output, ct) => _libreOfficeService.ConvertAsync(input, output, targetFormat, ct);
    }

    private Task<ConversionResult> ConvertToHtmlAsync(string input, string output, CancellationToken ct) =>
        _docxToHtmlPipeline.ConvertAsync(input, output, ct);

    /// <summary>
    /// Structural VAL-02 cross-check: every Constants.SupportedFormats.ConversionMatrix
    /// {input}-{target} pair MUST have a registered handler, and every registered handler key
    /// MUST correspond to a matrix pair. A divergence is a programmer error, surfaced at boot via
    /// <see cref="InvalidOperationException"/> rather than as a per-request silent gap.
    /// </summary>
    private void VerifyHandlerMatrixConsistency()
    {
        var matrixPairs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entry in Constants.SupportedFormats.ConversionMatrix)
        {
            foreach (var target in entry.Value)
            {
                matrixPairs.Add($"{entry.Key}-{target}");
            }
        }

        var matrixWithoutHandler = matrixPairs
            .Where(pair => !_handlers.ContainsKey(pair))
            .OrderBy(pair => pair, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var handlerWithoutMatrix = _handlers.Keys
            .Where(key => !matrixPairs.Contains(key))
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (matrixWithoutHandler.Count > 0 || handlerWithoutMatrix.Count > 0)
        {
            throw new InvalidOperationException(
                "Conversion matrix and handler registrations diverge. " +
                $"Matrix pairs without a handler: [{string.Join(", ", matrixWithoutHandler)}]. " +
                $"Handlers without a matrix pair: [{string.Join(", ", handlerWithoutMatrix)}].");
        }
    }

    /// <inheritdoc/>
    public async Task<ConversionResult> ConvertAsync(
        string inputPath,
        string outputPath,
        string inputFormat,
        string targetFormat,
        CancellationToken cancellationToken = default)
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

            var result = await handler(inputPath, outputPath, cancellationToken);

            if (result.Success)
            {
                _logger.LogInformation("Conversion completed: {InputFormat} to {TargetFormat}", inputFormat, targetFormat);
            }
            else
            {
                _logger.LogError("Conversion failed: {InputFormat} to {TargetFormat}", inputFormat, targetFormat);
                _logger.LogDebug("Conversion raw failure detail for debugging: {Error}", result.Error);
            }

            return result;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // The caller (or HttpContext.RequestAborted upstream) cancelled. Rethrow so the
            // controller drops the response without synthesizing a body for a client that is
            // already gone. ASP.NET writes nothing in this case.
            _logger.LogInformation("Conversion cancelled by client: {InputFormat} to {TargetFormat}", inputFormat, targetFormat);
            throw;
        }
        catch (OperationCanceledException)
        {
            // Internal-timeout OCE (the per-engine timeout fired without client cancellation).
            // Preserve the typed signal so the controller maps it to 408.
            _logger.LogError("Conversion timed out: {InputFormat} to {TargetFormat}", inputFormat, targetFormat);
            return new ConversionResult
            {
                Success = false,
                Error = "Conversion timed out",
                FailureReason = FailureReason.Timeout
            };
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

    private async Task<ConversionResult> XmlToPdfAsync(string inputPath, string outputPath, CancellationToken cancellationToken)
    {
        return await TextContentToPdfAsync(inputPath, outputPath, cancellationToken);
    }

    private async Task<ConversionResult> HtmlToPdfAsync(string inputPath, string outputPath, CancellationToken cancellationToken)
    {
        return await TextContentToPdfAsync(inputPath, outputPath, cancellationToken);
    }

    private async Task<ConversionResult> TextContentToPdfAsync(string inputPath, string outputPath, CancellationToken cancellationToken)
    {
        var textContent = await File.ReadAllTextAsync(inputPath, cancellationToken);

        // iText7 is synchronous; honor cancellation at the file-read boundary above and after the
        // PDF object graph is built. The iText calls themselves cannot be interrupted partway.
        cancellationToken.ThrowIfCancellationRequested();

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

    private async Task<ConversionResult> TxtToDocxAsync(string inputPath, string outputPath, CancellationToken cancellationToken)
    {
        var text = await File.ReadAllTextAsync(inputPath, cancellationToken);

        var body = new Body();
        foreach (var line in text.Split('\n'))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var paragraph = new Paragraph();
            var run = new Run();
            var textElement = new Text(line.TrimEnd('\r'));
            run.Append(textElement);
            paragraph.Append(run);
            body.Append(paragraph);
        }

        var document = new Document(body);

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
