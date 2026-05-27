using System.Text.RegularExpressions;
using Xunit;

namespace FileConversionApi.Tests.Fixtures;

/// <summary>
/// Static assertion helpers for the docx-to-PDF-to-pdf.js pipeline's empirically-true
/// structural floor. Helpers are pure functions over the HTML response body; they produce
/// xUnit <see cref="Assert"/> failures with a concrete observed value when a predicate
/// breaks. The base64-decode helper bounds its inner loop by <see cref="MaxDataUris"/>.
/// </summary>
internal static class FidelityFloor
{
    private const int MaxDataUris = 100000;

    private static readonly byte[] PngSignature =
    {
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A
    };

    private static readonly Regex DataImageSrcPattern = new(
        "src=\"data:image/",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ImgTagPattern = new(
        "<img\\b",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex ImgDataSrcPattern = new(
        "<img\\b[^>]*\\bsrc=\"data:",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex LocalPathHrefPattern = new(
        "href=\"(?:file:|[a-zA-Z]:\\\\|\\.{1,2}[\\\\/]|[\\\\/])",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TextLayerSpanPattern = new(
        "<span style=\"left:",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex BodyInnerPattern = new(
        "<body[^>]*>(.*)</body>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex StylesheetLinkPattern = new(
        "<link\\b[^>]*rel=\"stylesheet\"",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    // Anchor the class to a value boundary (closing quote or whitespace before another token)
    // so siblings like 'page-broken', 'page2', or 'page rendered' don't match. pdf.js v5 emits
    // the bare 'page' class today; the boundary is defensive against a future emit-shape tweak.
    private static readonly Regex PageContainerPattern = new(
        "<div class=\"page(?:\"|\\s)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex TextLayerBlockPattern = new(
        "<div class=\"text layer\">(.*?)</div>",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.Compiled);

    private static readonly Regex InnerTagPattern = new(
        "<[^>]+>",
        RegexOptions.Compiled);

    private static readonly Regex FontFaceUrlPattern = new(
        "@font-face[^}]*url\\(",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex FontLinkPattern = new(
        "<link\\b[^>]*(?:rel=\"(?:preload|stylesheet)\"[^>]*as=\"font\"|\\.(?:woff2?|ttf|otf|eot))",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex PngDataUriPattern = new(
        "src=\"data:image/png;base64,([A-Za-z0-9+/=]+)\"",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Asserts the body contains at least one inline <c>data:image/</c> URI.
    /// </summary>
    public static void AssertHasInlineDataImage(string html)
    {
        ArgumentNullException.ThrowIfNull(html);
        var count = DataImageSrcPattern.Matches(html).Count;
        Assert.True(count >= 1, $"Expected at least one data: image src; observed {count}.");
    }

    /// <summary>
    /// Asserts every <c>&lt;img&gt;</c> tag uses a <c>data:</c> URI source. Self-containment promise.
    /// </summary>
    public static void AssertAllImagesUseDataUri(string html)
    {
        ArgumentNullException.ThrowIfNull(html);
        var imgTotal = ImgTagPattern.Matches(html).Count;
        var imgDataSrc = ImgDataSrcPattern.Matches(html).Count;
        Assert.Equal(imgTotal, imgDataSrc);
    }

    /// <summary>
    /// Returns the count of <c>&lt;img&gt;</c> tags in the body. Used by alias-contract tests
    /// that need to compare two responses to each other rather than against an absolute bound.
    /// </summary>
    public static int CountImgTags(string html)
    {
        ArgumentNullException.ThrowIfNull(html);
        return ImgTagPattern.Matches(html).Count;
    }

    /// <summary>
    /// Returns the count of inline <c>data:image/png;base64,...</c> payloads in the body.
    /// Used by alias-contract tests that need to compare two responses to each other.
    /// </summary>
    public static int CountInlinePngImages(string html)
    {
        ArgumentNullException.ThrowIfNull(html);
        return PngDataUriPattern.Matches(html).Count;
    }

    /// <summary>
    /// Asserts no <c>href</c> attribute points at a local-filesystem path.
    /// </summary>
    public static void AssertNoLocalPathHref(string html)
    {
        ArgumentNullException.ThrowIfNull(html);
        var localHrefCount = LocalPathHrefPattern.Matches(html).Count;
        Assert.Equal(0, localHrefCount);
    }

    /// <summary>
    /// Asserts the body contains no <c>file://</c> substring.
    /// </summary>
    public static void AssertNoFileUri(string html)
    {
        ArgumentNullException.ThrowIfNull(html);
        Assert.DoesNotContain("file://", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// Asserts the pdf.js text-layer overlay is present with at least one positioned span.
    /// </summary>
    public static void AssertTextLayerNonEmpty(string html)
    {
        ArgumentNullException.ThrowIfNull(html);
        var spanCount = TextLayerSpanPattern.Matches(html).Count;
        Assert.True(spanCount >= 1, $"Expected at least one text-layer span; observed {spanCount}.");
    }

    /// <summary>
    /// Asserts the inner content of the first <c>&lt;body&gt;</c> element is non-empty after trimming.
    /// </summary>
    public static void AssertBodyNonEmpty(string html)
    {
        ArgumentNullException.ThrowIfNull(html);
        var match = BodyInnerPattern.Match(html);
        Assert.True(match.Success, "Response body contains no <body> element.");
        var inner = match.Groups[1].Value.Trim();
        Assert.True(inner.Length > 0, "<body> element inner content is empty.");
    }

    /// <summary>
    /// Asserts the body declares no external stylesheet via <c>&lt;link rel="stylesheet"&gt;</c>.
    /// </summary>
    public static void AssertNoExternalStylesheet(string html)
    {
        ArgumentNullException.ThrowIfNull(html);
        var stylesheetCount = StylesheetLinkPattern.Matches(html).Count;
        Assert.Equal(0, stylesheetCount);
    }

    /// <summary>
    /// Asserts the response is internally self-consistent: the count of page-container divs
    /// equals the count of inline data-image src attributes, with at least one of each. This is
    /// the strongest contract assertable from the HTML response alone; asserting equality with
    /// the source PDF page count requires re-running the docx-to-PDF hop in-test (deferred).
    /// </summary>
    /// <param name="html">The HTML response body to scan.</param>
    public static void AssertPageContainerAndImageCountConsistent(string html)
    {
        ArgumentNullException.ThrowIfNull(html);
        var pageContainers = PageContainerPattern.Matches(html).Count;
        Assert.True(pageContainers >= 1, $"Expected at least one page container; observed {pageContainers}.");
        var dataImgCount = DataImageSrcPattern.Matches(html).Count;
        Assert.Equal(dataImgCount, pageContainers);
    }

    /// <summary>
    /// Asserts the count of page-container divs in the response equals the source PDF page count
    /// passed in by the caller, with at least one page container present. The caller is
    /// responsible for computing the PDF page count from the intermediate PDF artifact.
    /// </summary>
    /// <param name="html">The HTML response body to scan.</param>
    /// <param name="expectedPdfPageCount">The PDF page count produced by the docx-to-PDF hop. Must be non-negative.</param>
    public static void AssertPageContainerCountMatchesPdfPageCount(string html, int expectedPdfPageCount)
    {
        ArgumentNullException.ThrowIfNull(html);
        if (expectedPdfPageCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(expectedPdfPageCount), expectedPdfPageCount, "PDF page count must be non-negative.");
        }
        var pageContainers = PageContainerPattern.Matches(html).Count;
        Assert.True(pageContainers >= 1, $"Expected at least one page container; observed {pageContainers}.");
        Assert.Equal(expectedPdfPageCount, pageContainers);
    }

    /// <summary>
    /// Asserts the total length of tag-stripped text inside every text-layer block is greater than zero.
    /// </summary>
    public static void AssertExtractableTextNonEmpty(string html)
    {
        ArgumentNullException.ThrowIfNull(html);
        var blocks = TextLayerBlockPattern.Matches(html);
        int totalLength = 0;
        foreach (Match block in blocks)
        {
            var stripped = InnerTagPattern.Replace(block.Groups[1].Value, string.Empty).Trim();
            totalLength += stripped.Length;
        }
        Assert.True(totalLength > 0, $"Expected non-empty extractable text across pages; observed {totalLength} chars.");
    }

    /// <summary>
    /// Asserts the body declares no external font reference. The combined count of
    /// <c>@font-face url(...)</c> declarations plus any <c>&lt;link&gt;</c> that pulls
    /// a font resource must be zero.
    /// </summary>
    public static void AssertNoExternalFonts(string html)
    {
        ArgumentNullException.ThrowIfNull(html);
        var fontFaceUrlCount = FontFaceUrlPattern.Matches(html).Count;
        var fontLinkCount = FontLinkPattern.Matches(html).Count;
        var externalFontRefs = fontFaceUrlCount + fontLinkCount;
        Assert.Equal(0, externalFontRefs);
    }

    /// <summary>
    /// Asserts every <c>data:image/png;base64,...</c> payload decodes to a byte sequence longer than
    /// the PNG signature and begins with the canonical 8-byte PNG header. The decode loop is bounded
    /// by <see cref="MaxDataUris"/> so a pathological response cannot drive unbounded work.
    /// </summary>
    public static void AssertAllDataUriPngPayloadsDecodeNonZero(string html)
    {
        ArgumentNullException.ThrowIfNull(html);
        var matches = PngDataUriPattern.Matches(html);
        int decoded = 0;
        int bound = Math.Min(matches.Count, MaxDataUris);
        for (int i = 0; i < bound; i++)
        {
            var payload = matches[i].Groups[1].Value;
            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(payload);
            }
            catch (FormatException)
            {
                Assert.Fail($"data:image/png payload at index {i} is not valid base64.");
                return;
            }
            Assert.True(bytes.Length > PngSignature.Length,
                $"data:image/png payload at index {i} is {bytes.Length} bytes; expected more than {PngSignature.Length}.");
            for (int b = 0; b < PngSignature.Length; b++)
            {
                Assert.Equal(PngSignature[b], bytes[b]);
            }
            decoded++;
        }
        Assert.True(decoded >= 1, $"Expected at least one decoded PNG payload; observed {decoded}.");
    }
}
