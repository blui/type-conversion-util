using System.Globalization;
using System.Text.RegularExpressions;

namespace FileConversionApi.Services;

/// <summary>
/// Recognizes and parses the self-contained HTML emitted by
/// <see cref="DocxToHtmlPipeline"/> (the bundled pdf-to-html.mjs engine's pdf.js-style
/// page-image + transparent text-layer shape). The forward pipeline composes each page as
/// <c>&lt;div class="page"&gt;&lt;img class="layer" src="data:image/png;base64,..."&gt;&lt;div class="text layer"&gt;...spans...&lt;/div&gt;&lt;/div&gt;</c>;
/// the html-&gt;pdf and html-&gt;docx reverse paths key off that exact shape to round-trip
/// each page raster losslessly. Arbitrary HTML that does not match this shape is left to
/// the existing LibreOffice / HtmlToOpenXml fallbacks.
/// </summary>
internal static class PipelineOutputHtmlExtractor
{
    // The forward emitter (engine/pdf-to-html.mjs composePage) writes this exact opening for
    // every page; presence of the literal substring is a sufficient, cheap signature check
    // before we commit to the full per-page regex scan over a multi-megabyte payload.
    private const string PipelineOutputSignature = "<div class=\"page\" style=\"width:";

    // Fixed upper bound on pages parsed from one document, mirroring the forward engine's own
    // MAX_PAGES ceiling (engine/pdf-to-html.mjs). Legitimate pipeline output never exceeds it;
    // the bound stops a crafted or corrupt payload from driving unbounded work in the reverse path.
    private const int MaxPages = 2000;

    private static readonly Regex PagePattern = new(
        "<div class=\"page\" style=\"width:(?<w>\\d+)px;height:(?<h>\\d+)px;\">" +
        "<img class=\"layer\" src=\"data:image/png;base64,(?<png>[A-Za-z0-9+/=]+)\" " +
        "width=\"\\d+\" height=\"\\d+\" alt=\"page \\d+\">" +
        "<div class=\"text layer\">(?<text>.*?)</div></div>",
        RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex SpanPattern = new(
        "<span style=\"left:(?<left>-?\\d+(?:\\.\\d+)?)px;top:(?<top>-?\\d+(?:\\.\\d+)?)px;" +
        "font-size:(?<size>\\d+(?:\\.\\d+)?)px;" +
        "(?: transform:rotate\\((?<rot>-?\\d+(?:\\.\\d+)?)rad\\);transform-origin:0 100%;)?\">" +
        "(?<text>.*?)</span>",
        RegexOptions.Singleline | RegexOptions.Compiled);

    /// <summary>
    /// True when <paramref name="html"/> begins with (or contains near the start of) the
    /// signature opening tag that the forward pipeline always emits. The check is a literal
    /// substring scan over the first few kilobytes and does not allocate.
    /// </summary>
    public static bool LooksLikePipelineOutput(string html)
    {
        ArgumentNullException.ThrowIfNull(html);
        // The forward pipeline's composeDocument prepends ~250 bytes of doctype/head/style
        // before the first .page container, so the signature lives well inside the first 4KB.
        // A bounded IndexOf avoids scanning the whole 12MB document for the negative case.
        int bound = Math.Min(html.Length, 4096);
        return html.AsSpan(0, bound).IndexOf(PipelineOutputSignature.AsSpan()) >= 0;
    }

    /// <summary>
    /// Parses every <c>.page</c> block in the source HTML and returns one
    /// <see cref="PipelineOutputHtmlPage"/> per page in document order. Throws
    /// <see cref="InvalidOperationException"/> when <see cref="LooksLikePipelineOutput"/>
    /// returns true but no page block parses, which indicates a forward-emitter change
    /// that needs a matching update here.
    /// </summary>
    public static IReadOnlyList<PipelineOutputHtmlPage> ExtractPages(
        string html,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(html);

        var pages = new List<PipelineOutputHtmlPage>();
        foreach (Match pageMatch in PagePattern.Matches(html))
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (pages.Count >= MaxPages)
            {
                throw new InvalidOperationException(
                    $"Pipeline-output HTML exceeds the {MaxPages}-page ceiling; refusing to continue.");
            }

            var width = int.Parse(pageMatch.Groups["w"].Value, CultureInfo.InvariantCulture);
            var height = int.Parse(pageMatch.Groups["h"].Value, CultureInfo.InvariantCulture);
            var pngBytes = Convert.FromBase64String(pageMatch.Groups["png"].Value);

            var spans = ExtractSpans(pageMatch.Groups["text"].Value, cancellationToken);

            pages.Add(new PipelineOutputHtmlPage(width, height, pngBytes, spans));
        }

        if (pages.Count == 0)
        {
            throw new InvalidOperationException(
                "HTML matched the pipeline-output signature but no .page blocks parsed; the " +
                "forward emitter (engine/pdf-to-html.mjs composePage) likely changed shape.");
        }

        return pages;
    }

    private static IReadOnlyList<PipelineOutputHtmlSpan> ExtractSpans(
        string textLayer,
        CancellationToken cancellationToken)
    {
        var spans = new List<PipelineOutputHtmlSpan>();
        foreach (Match spanMatch in SpanPattern.Matches(textLayer))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var left = double.Parse(spanMatch.Groups["left"].Value, CultureInfo.InvariantCulture);
            var top = double.Parse(spanMatch.Groups["top"].Value, CultureInfo.InvariantCulture);
            var size = double.Parse(spanMatch.Groups["size"].Value, CultureInfo.InvariantCulture);
            var rotGroup = spanMatch.Groups["rot"];
            var rotation = rotGroup.Success
                ? double.Parse(rotGroup.Value, CultureInfo.InvariantCulture)
                : 0.0;
            var text = DecodeEscapedText(spanMatch.Groups["text"].Value);

            spans.Add(new PipelineOutputHtmlSpan(left, top, size, rotation, text));
        }
        return spans;
    }

    // The forward emitter's escapeHtml() escapes &, <, >, and " in that fixed order. Decoding
    // in the same fixed order (with &amp; LAST, mirroring how the encoder applied it FIRST)
    // is the only way to round-trip text like '&lt;' written by an author through both sides
    // without it collapsing to '<'.
    private static string DecodeEscapedText(string escaped)
    {
        return escaped
            .Replace("&lt;", "<", StringComparison.Ordinal)
            .Replace("&gt;", ">", StringComparison.Ordinal)
            .Replace("&quot;", "\"", StringComparison.Ordinal)
            .Replace("&amp;", "&", StringComparison.Ordinal);
    }
}

/// <summary>
/// One page extracted from pipeline-output HTML: the raster dimensions, the page image as
/// raw PNG bytes (already base64-decoded), and the ordered text-layer spans for invisible /
/// hidden text emission in the reverse path.
/// </summary>
/// <param name="WidthPx">Page width in CSS pixels (matches the source PDF rendered at the
/// forward pipeline's RENDER_SCALE, currently 2.0 of PDF points).</param>
/// <param name="HeightPx">Page height in CSS pixels.</param>
/// <param name="PngBytes">Decoded PNG payload; the canonical 8-byte PNG header is present.</param>
/// <param name="Spans">Text-layer spans in document order.</param>
internal sealed record PipelineOutputHtmlPage(
    int WidthPx,
    int HeightPx,
    byte[] PngBytes,
    IReadOnlyList<PipelineOutputHtmlSpan> Spans);

/// <summary>
/// One text-layer span: top-left pixel position, font height in pixels, rotation in radians
/// (zero when the source PDF text was unrotated), and decoded text content. Coordinates use
/// the HTML convention (origin at top-left, Y increases downward); reverse renderers flip Y
/// when emitting to PDF.
/// </summary>
internal sealed record PipelineOutputHtmlSpan(
    double LeftPx,
    double TopPx,
    double FontSizePx,
    double RotationRadians,
    string Text);
