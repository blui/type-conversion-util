using System.Net;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using FileConversionApi.Tests.Fixtures;
using Xunit;

namespace FileConversionApi.Tests.Integration;

/// <summary>
/// Real-engine happy-path coverage for the html -&gt; docx path. The handler runs
/// HtmlToOpenXml.HtmlConverter entirely in-process (no engine subprocess), so this test
/// does not gate on <see cref="SamplesLocator.BundledEnginesPresent"/>; a clean CI runner
/// without staged LibreOffice / Node still exercises the OOXML write path.
/// </summary>
public sealed class HtmlToDocxTests
{
    private const string DocxContentType =
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    // ZIP / OOXML container signature (DOCX = OOXML wrapped in a ZIP container).
    private static readonly byte[] DocxMagicBytes = new byte[] { 0x50, 0x4B, 0x03, 0x04 };

    [Fact]
    public async Task Convert_HtmlToDocx_HtmlToOpenXml_Returns200WithValidDocx()
    {
        using var factory = new ConversionApiFactory();
        using var client = factory.CreateClient();

        var htmlBytes = Encoding.UTF8.GetBytes(
            "<!doctype html><html><body>" +
            "<h1>html-to-docx</h1>" +
            "<p>An ordinary paragraph with <strong>bold</strong> and <em>italic</em> spans.</p>" +
            "<ul><li>first item</li><li>second item</li></ul>" +
            "</body></html>");
        using var form = MultipartForms.BuildConvertForm(htmlBytes, "sample.html", "text/html", "docx");

        using var response = await client.PostAsync("/api/convert", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Content.Headers.ContentType);
        Assert.Equal(DocxContentType, response.Content.Headers.ContentType!.MediaType);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(
            bytes.Length >= DocxMagicBytes.Length,
            $"Response body length {bytes.Length} is shorter than the DOCX magic byte prefix.");
        for (int i = 0; i < DocxMagicBytes.Length; i++)
        {
            Assert.Equal(DocxMagicBytes[i], bytes[i]);
        }

        // The DOCX must round-trip through DocumentFormat.OpenXml's validator. If the file is a
        // bare ZIP with wrong content but right magic bytes (a regression we should catch), Open
        // throws and the test fails loudly.
        await using var inputStream = new MemoryStream(bytes);
        using var package = WordprocessingDocument.Open(inputStream, isEditable: false);
        Assert.NotNull(package.MainDocumentPart);
        Assert.NotNull(package.MainDocumentPart!.Document);
        Assert.NotNull(package.MainDocumentPart.Document.Body);

        // Source HTML had a heading, a paragraph, and a 2-item list. The output document should
        // contain text content (the exact OOXML tree shape is HtmlConverter's contract, not
        // ours, so we sanity-check on body inner-text rather than on element counts).
        var bodyText = package.MainDocumentPart.Document.Body!.InnerText;
        Assert.Contains("html-to-docx", bodyText);
        Assert.Contains("first item", bodyText);
        Assert.Contains("second item", bodyText);
    }
}
