using FileConversionApi.Models;
using iText.IO.Font.Constants;
using iText.IO.Image;
using iText.Kernel.Font;
using iText.Kernel.Geom;
using iText.Kernel.Pdf;
using iText.Kernel.Pdf.Canvas;
using Microsoft.Extensions.Logging;

namespace FileConversionApi.Services;

/// <summary>
/// Reverses pipeline-output HTML (see <see cref="PipelineOutputHtmlExtractor"/>) back to a
/// PDF by emitting one PDF page per <c>.page</c> div, sized exactly to the source raster's
/// pixel dimensions. The page raster is drawn at full bleed and the text-layer spans are
/// written behind it with the PDF text-rendering mode set to invisible, so the output is
/// visually identical to the original docx-&gt;pdf hop while staying searchable and
/// text-selectable.
///
/// Coordinate systems differ: HTML places origin at top-left with Y growing down; PDF
/// places origin at bottom-left with Y growing up. The Y-flip happens once per span when
/// the text matrix is set.
/// </summary>
internal static class PipelineOutputHtmlToPdfRenderer
{
    public static ConversionResult Render(
        IReadOnlyList<PipelineOutputHtmlPage> pages,
        string outputPath,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentNullException.ThrowIfNull(outputPath);
        ArgumentNullException.ThrowIfNull(logger);

        cancellationToken.ThrowIfCancellationRequested();

        using var stream = File.Create(outputPath);
        using var writer = new PdfWriter(stream);
        using var document = new PdfDocument(writer);

        // One font is enough: the text layer is invisible (search/select only), so visual
        // metrics don't matter beyond "the glyph box lands roughly under the visible word."
        var font = PdfFontFactory.CreateFont(StandardFonts.HELVETICA);

        for (int i = 0; i < pages.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            RenderPage(document, pages[i], font, logger, i);
        }

        return new ConversionResult
        {
            Success = true,
            OutputPath = outputPath,
            ConversionMethod = "PipelineOutputHtmlToPdfRenderer"
        };
    }

    private static void RenderPage(
        PdfDocument document,
        PipelineOutputHtmlPage page,
        PdfFont font,
        ILogger logger,
        int pageIndex)
    {
        var pageSize = new PageSize(page.WidthPx, page.HeightPx);
        var pdfPage = document.AddNewPage(pageSize);
        var canvas = new PdfCanvas(pdfPage);

        var image = ImageDataFactory.CreatePng(page.PngBytes);
        canvas.AddImageFittedIntoRectangle(
            image,
            new Rectangle(0, 0, page.WidthPx, page.HeightPx),
            asInline: false);

        if (page.Spans.Count == 0)
        {
            return;
        }

        canvas.BeginText();
        canvas.SetTextRenderingMode(PdfCanvasConstants.TextRenderingMode.INVISIBLE);

        foreach (var span in page.Spans)
        {
            WriteSpan(canvas, span, font, page.HeightPx, logger, pageIndex);
        }

        canvas.EndText();
    }

    private static void WriteSpan(
        PdfCanvas canvas,
        PipelineOutputHtmlSpan span,
        PdfFont font,
        int pageHeightPx,
        ILogger logger,
        int pageIndex)
    {
        // PDF baseline is at (left, pageHeight - top - fontSize) — the forward emitter set
        // CSS top to the glyph's top in HTML coordinates, so subtract font height to land
        // on the baseline in PDF coordinates.
        var baselineY = (float)(pageHeightPx - span.TopPx - span.FontSizePx);
        var x = (float)span.LeftPx;

        if (Math.Abs(span.RotationRadians) < 1e-6)
        {
            canvas.SetTextMatrix(1, 0, 0, 1, x, baselineY);
        }
        else
        {
            var cos = (float)Math.Cos(span.RotationRadians);
            var sin = (float)Math.Sin(span.RotationRadians);
            canvas.SetTextMatrix(cos, sin, -sin, cos, x, baselineY);
        }

        canvas.SetFontAndSize(font, (float)span.FontSizePx);

        var safeText = MakeWinAnsiSafe(span.Text);
        if (safeText.Length == 0)
        {
            return;
        }

        try
        {
            canvas.ShowText(safeText);
        }
        catch (Exception ex)
        {
            // Standard-14 Helvetica is WinAnsi-only; a Cp1252-unmappable char that survived
            // the sanitizer (e.g. a private-use codepoint) still trips PdfException. Drop
            // the offending span rather than failing the whole render — the visible page
            // image is unaffected and search merely loses one word.
            logger.LogDebug(
                ex,
                "Skipping unencodable invisible-text span on page {Page}: '{Text}'",
                pageIndex + 1,
                safeText);
        }
    }

    // Helvetica with the standard WinAnsi encoding accepts Cp1252 codepoints (basic ASCII
    // plus Latin-1 plus Cp1252's 0x80-0x9F extension that covers €, curly quotes, dashes,
    // etc.). Characters outside that range are replaced with '?' so the round-trip degrades
    // one glyph at a time instead of aborting the page.
    private static string MakeWinAnsiSafe(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var buffer = new char[text.Length];
        int written = 0;
        foreach (var c in text)
        {
            if (c == '\t' || c == '\n' || c == '\r')
            {
                buffer[written++] = ' ';
            }
            else if (c < 0x20)
            {
                continue;
            }
            else if (c <= 0xFF)
            {
                buffer[written++] = c;
            }
            else if (IsWinAnsiExtension(c))
            {
                buffer[written++] = c;
            }
            else
            {
                buffer[written++] = '?';
            }
        }
        return new string(buffer, 0, written);
    }

    // The 27 Unicode codepoints Cp1252 defines in the 0x80-0x9F range that ISO-8859-1 leaves
    // undefined (curly quotes, dashes, the euro sign, and friends). Helvetica's WinAnsi encoding
    // accepts them, so they belong in the invisible text layer rather than degrading to '?'.
    private const string WinAnsiExtensionChars =
        "€‚ƒ„…†‡ˆ‰Š‹ŒŽ" +
        "‘’“”•–—˜™š›œžŸ";

    private static bool IsWinAnsiExtension(char c) => WinAnsiExtensionChars.IndexOf(c) >= 0;
}
