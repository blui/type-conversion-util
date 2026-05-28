using System.Text;
using FileConversionApi.Services;
using Xunit;

namespace FileConversionApi.Tests.Services;

/// <summary>
/// Unit coverage for <see cref="PipelineOutputHtmlExtractor"/>. The shape under test is the
/// fixed format produced by <c>engine/pdf-to-html.mjs</c>'s <c>composePage</c> / <c>composeDocument</c>;
/// these tests use a hand-built two-page fixture so they do not require any engine binary.
/// </summary>
public sealed class PipelineOutputHtmlExtractorTests
{
    // 1x1 transparent PNG (smallest valid payload), base64-encoded. The extractor only verifies
    // base64 round-trips to non-empty bytes; the renderers verify the PNG header downstream.
    private const string OnePixelPngBase64 =
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNkYAAAAAYAAjCB0C8AAAAASUVORK5CYII=";

    private static string BuildPipelineHtml(int pageCount)
    {
        var sb = new StringBuilder();
        sb.Append("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\">")
          .Append("<title>Rendered document</title><style>html,body{margin:0;}</style></head><body>");

        for (int i = 0; i < pageCount; i++)
        {
            sb.Append("<div class=\"page\" style=\"width:100px;height:200px;\">")
              .Append("<img class=\"layer\" src=\"data:image/png;base64,").Append(OnePixelPngBase64)
              .Append("\" width=\"100\" height=\"200\" alt=\"page ").Append(i + 1).Append("\">")
              .Append("<div class=\"text layer\">")
              .Append("<span style=\"left:10.00px;top:20.00px;font-size:12.00px;\">Hello</span>")
              .Append("<span style=\"left:50.00px;top:20.00px;font-size:12.00px;\">World&amp;Co</span>")
              .Append("</div></div>");
        }
        sb.Append("</body></html>");
        return sb.ToString();
    }

    [Fact]
    public void LooksLikePipelineOutput_ReturnsTrue_ForPipelineEmittedHtml()
    {
        var html = BuildPipelineHtml(pageCount: 1);
        Assert.True(PipelineOutputHtmlExtractor.LooksLikePipelineOutput(html));
    }

    [Fact]
    public void LooksLikePipelineOutput_ReturnsFalse_ForArbitraryHtml()
    {
        var html = "<!doctype html><html><body><h1>hello</h1><p>not our format</p></body></html>";
        Assert.False(PipelineOutputHtmlExtractor.LooksLikePipelineOutput(html));
    }

    [Fact]
    public void LooksLikePipelineOutput_ReturnsFalse_ForEmptyInput()
    {
        Assert.False(PipelineOutputHtmlExtractor.LooksLikePipelineOutput(string.Empty));
    }

    [Fact]
    public void ExtractPages_ProducesOnePageDataPerPageContainer()
    {
        var html = BuildPipelineHtml(pageCount: 3);

        var pages = PipelineOutputHtmlExtractor.ExtractPages(html, CancellationToken.None);

        Assert.Equal(3, pages.Count);
        Assert.All(pages, p => Assert.Equal(100, p.WidthPx));
        Assert.All(pages, p => Assert.Equal(200, p.HeightPx));
        Assert.All(pages, p => Assert.True(p.PngBytes.Length > 8));
        // PNG 8-byte signature: 89 50 4E 47 0D 0A 1A 0A.
        Assert.All(pages, p =>
        {
            Assert.Equal(0x89, p.PngBytes[0]);
            Assert.Equal(0x50, p.PngBytes[1]);
            Assert.Equal(0x4E, p.PngBytes[2]);
            Assert.Equal(0x47, p.PngBytes[3]);
        });
    }

    [Fact]
    public void ExtractPages_DecodesHtmlEntitiesInSpanText()
    {
        var html = BuildPipelineHtml(pageCount: 1);

        var pages = PipelineOutputHtmlExtractor.ExtractPages(html, CancellationToken.None);
        var spans = pages[0].Spans;

        Assert.Equal(2, spans.Count);
        Assert.Equal("Hello", spans[0].Text);
        Assert.Equal("World&Co", spans[1].Text);
    }

    [Fact]
    public void ExtractPages_PreservesSpanPositionsAndFontSize()
    {
        var html = BuildPipelineHtml(pageCount: 1);

        var pages = PipelineOutputHtmlExtractor.ExtractPages(html, CancellationToken.None);
        var spans = pages[0].Spans;

        Assert.Equal(10.0, spans[0].LeftPx);
        Assert.Equal(20.0, spans[0].TopPx);
        Assert.Equal(12.0, spans[0].FontSizePx);
        Assert.Equal(0.0, spans[0].RotationRadians);

        Assert.Equal(50.0, spans[1].LeftPx);
    }

    [Fact]
    public void ExtractPages_ParsesRotatedSpan()
    {
        // The forward emitter only attaches the transform: clause when angle is non-zero.
        var html = "<!doctype html><html><body>" +
            "<div class=\"page\" style=\"width:100px;height:200px;\">" +
            "<img class=\"layer\" src=\"data:image/png;base64," + OnePixelPngBase64 +
            "\" width=\"100\" height=\"200\" alt=\"page 1\">" +
            "<div class=\"text layer\">" +
            "<span style=\"left:5.00px;top:5.00px;font-size:10.00px; transform:rotate(1.57rad);transform-origin:0 100%;\">Rotated</span>" +
            "</div></div></body></html>";

        var pages = PipelineOutputHtmlExtractor.ExtractPages(html, CancellationToken.None);

        Assert.Single(pages[0].Spans);
        Assert.Equal(1.57, pages[0].Spans[0].RotationRadians);
    }

    [Fact]
    public void ExtractPages_ThrowsWhenSignatureMatchesButNoPagesParse()
    {
        // The signature substring is present but the .page block is malformed (missing img),
        // so the per-page regex cannot match. The extractor surfaces this as an explicit
        // InvalidOperationException so a forward-emitter change cannot silently degrade
        // the reverse path to an empty document.
        var html = "<!doctype html><html><body>" +
            "<div class=\"page\" style=\"width:100px;height:200px;\">corrupted</div>" +
            "</body></html>";

        Assert.Throws<InvalidOperationException>(() =>
            PipelineOutputHtmlExtractor.ExtractPages(html, CancellationToken.None));
    }
}
