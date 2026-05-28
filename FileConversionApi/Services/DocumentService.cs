using Microsoft.Extensions.Logging;
using System.Collections.Generic;
using System.Diagnostics;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Wordprocessing;
using FileConversionApi.Models;
using FileConversionApi.Services.Interfaces;
using FileConversionApi.Utilities;

namespace FileConversionApi.Services;

/// <summary>
/// Routes a single (inputFormat, targetFormat) conversion to the engine that handles it
/// (LibreOffice, the docx-to-html two-hop pipeline, iText7, NPOI, or DocumentFormat.OpenXml).
/// The handler dispatch table is the only place an (input, target) pair maps to a service.
/// </summary>
public class DocumentService
{
    private readonly ILogger<DocumentService> _logger;
    private readonly PdfService _pdfService;
    private readonly ILibreOfficeProcessManager _libreOfficeProcessManager;
    private readonly SpreadsheetService _spreadsheetService;
    private readonly DocxToHtmlPipeline _docxToHtmlPipeline;

    // Conversion handler mappings. CancellationToken is the last parameter so client-disconnect
    // (HttpContext.RequestAborted) and the per-engine internal timeouts both reach the underlying
    // process.WaitForExitAsync calls through a linked CTS at each process manager.
    private readonly Dictionary<string, Func<string, string, CancellationToken, Task<ConversionResult>>> _handlers;

    public DocumentService(
        ILogger<DocumentService> logger,
        PdfService pdfService,
        ILibreOfficeProcessManager libreOfficeProcessManager,
        SpreadsheetService spreadsheetService,
        DocxToHtmlPipeline docxToHtmlPipeline)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _pdfService = pdfService ?? throw new ArgumentNullException(nameof(pdfService));
        _libreOfficeProcessManager = libreOfficeProcessManager ?? throw new ArgumentNullException(nameof(libreOfficeProcessManager));
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

            // docx->html/htm via the two-hop pipeline; htm uses the identical delegate as html
            ["docx-html"] = ConvertToHtmlAsync,
            ["docx-htm"] = ConvertToHtmlAsync,

            // html/htm -> pdf via LibreOffice's writer_web_pdf_Export filter, after injecting a
            // small print-CSS block into the source HTML so each .page container from the
            // docx->html pipeline output ends up on its own PDF page. Arbitrary HTML without
            // .page containers degrades to LibreOffice's default pagination (the page-break
            // rule has no targets and is a no-op). The two source aliases share one handler
            // because html and htm are interchangeable on the request side.
            ["html-pdf"] = HtmlToPdfAsync,
            ["htm-pdf"] = HtmlToPdfAsync,

            // html/htm -> docx via the in-process HtmlToOpenXml.HtmlConverter (no engine
            // subprocess). The legacy MS Word 97 .doc target stays out of scope on purpose;
            // OpenXml writes only the OOXML container format, not the OLE2 binary one.
            ["html-docx"] = HtmlToDocxAsync,
            ["htm-docx"] = HtmlToDocxAsync
        };

        // VAL-02: enforce matrix<->handler agreement structurally at startup (fail-fast).
        VerifyHandlerMatrixConsistency();
    }

    private Func<string, string, CancellationToken, Task<ConversionResult>> ConvertWithLibreOfficeAsync(string targetFormat)
    {
        return (input, output, ct) => _libreOfficeProcessManager.ConvertAsync(input, output, targetFormat, ct);
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

    /// <summary>
    /// Dispatches the (<paramref name="inputFormat"/>, <paramref name="targetFormat"/>) pair to its
    /// registered handler and runs it. Returns a failed result for an unregistered pair, maps an
    /// internal-timeout <see cref="OperationCanceledException"/> to a typed
    /// <see cref="FailureReason.Timeout"/> result, and rethrows caller-token cancellation so the
    /// controller can drop the response.
    /// </summary>
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

    // Print-time CSS injected into the source HTML before LibreOffice imports it. The first
    // three rules force each <div class="page"> from the docx->html pipeline output onto its
    // own PDF page and strip the screen-only chrome (8px margin, drop shadow) those divs carry.
    // The @page block then zeros LibreOffice's default print margins so the .page content
    // fills the PDF page edge-to-edge. Arbitrary HTML without .page containers is unaffected
    // by the .page selector rules.
    private const string HtmlPdfPrintCss =
        "<style>" +
        "@page{margin:0}" +
        "body{margin:0}" +
        ".page{page-break-before:always !important;margin:0 !important;box-shadow:none !important}" +
        ".page:first-child{page-break-before:avoid !important}" +
        "</style>";

    private async Task<ConversionResult> HtmlToPdfAsync(string inputPath, string outputPath, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var html = await File.ReadAllTextAsync(inputPath, cancellationToken);

        // Fast path: HTML produced by our own docx->html pipeline is a pdf.js-style page-image
        // + transparent-text-layer document. LibreOffice's HTML import discards absolute
        // positioning and reflows the text layer as visible body content, doubling the page
        // count and corrupting word boundaries. Bypass it: extract each .page raster and write
        // it directly into the PDF with the text layer reproduced behind it as invisible text.
        if (PipelineOutputHtmlExtractor.LooksLikePipelineOutput(html))
        {
            var pages = PipelineOutputHtmlExtractor.ExtractPages(html, cancellationToken);
            return PipelineOutputHtmlToPdfRenderer.Render(pages, outputPath, _logger, cancellationToken);
        }

        var preprocessed = InjectPrintCss(html);

        var tempDir = Path.Combine(Path.GetTempPath(), "html-pdf-" + UniqueIdGenerator.GenerateId());
        Directory.CreateDirectory(tempDir);
        // The file must keep an .html extension so LibreOffice routes the import through its
        // HTML filter rather than treating the bytes as plain text.
        var preprocPath = Path.Combine(tempDir, "input.html");

        try
        {
            await File.WriteAllTextAsync(preprocPath, preprocessed, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            return await _libreOfficeProcessManager.ConvertAsync(preprocPath, outputPath, "pdf", cancellationToken);
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, recursive: true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean up html->pdf temp directory");
            }
        }
    }

    private static string InjectPrintCss(string html)
    {
        // Inject the style block immediately before </head> so it overrides any author styles
        // that come earlier in the document. Fragments without a </head> get the block
        // prepended; LibreOffice will wrap the input in a synthetic head/body during import
        // and the early <style> still applies.
        var idx = html.IndexOf("</head>", StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            return HtmlPdfPrintCss + html;
        }
        return html.Substring(0, idx) + HtmlPdfPrintCss + html.Substring(idx);
    }

    private async Task<ConversionResult> HtmlToDocxAsync(string inputPath, string outputPath, CancellationToken cancellationToken)
    {
        // HtmlToOpenXml.HtmlConverter parses the source HTML and writes its content directly
        // into the WordprocessingDocument's main part. The whole pipeline is in-process; there
        // is no engine subprocess to spawn and no native binary to bundle, so no path resolver
        // is involved and the per-engine timeout that wraps the Node and LibreOffice handlers
        // does not apply here. Cancellation is honored at the file-read boundary above and at
        // the file-write boundary below; the converter itself is synchronous CPU work after the
        // HTML string is in memory.
        cancellationToken.ThrowIfCancellationRequested();
        var html = await File.ReadAllTextAsync(inputPath, cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();

        // Fast path: HTML produced by our own docx->html pipeline carries each page as a
        // pre-rasterised image plus a positioned-span text layer. HtmlToOpenXml cannot read
        // absolute positioning, so it dumps the page image followed by all spans as a flat
        // wall of text with no word boundaries. Bypass it: rebuild the OOXML directly with
        // one section per page raster and the text layer reproduced as a hidden run for
        // find/replace indexing.
        if (PipelineOutputHtmlExtractor.LooksLikePipelineOutput(html))
        {
            var pages = PipelineOutputHtmlExtractor.ExtractPages(html, cancellationToken);
            return PipelineOutputHtmlToDocxRenderer.Render(pages, outputPath, _logger, cancellationToken);
        }

        await using var outputStream = File.Create(outputPath);
        using var package = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Create(
            outputStream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document);

        var mainPart = package.MainDocumentPart ?? package.AddMainDocumentPart();
        if (mainPart.Document is null)
        {
            new Document(new Body()).Save(mainPart);
        }

        var converter = new HtmlToOpenXml.HtmlConverter(mainPart);
        await converter.ParseBody(html);
        // The conditional above (plus the HtmlConverter call) guarantees mainPart.Document is
        // non-null at this point; the C# flow analyzer can't see through Save(part) so we assert.
        mainPart.Document!.Save();

        return new ConversionResult
        {
            Success = true,
            OutputPath = outputPath,
            ConversionMethod = "HtmlToOpenXml"
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
