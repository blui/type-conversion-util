using System.Net;
using System.Text;
using FileConversionApi.Tests.Fixtures;
using Xunit;

namespace FileConversionApi.Tests.Integration;

/// <summary>
/// Real-engine happy-path coverage for the html -&gt; pdf path. The handler injects a
/// print-CSS block into the source HTML and routes the preprocessed file through
/// LibreOffice's <c>writer_web_pdf_Export</c> filter, so the test gates on
/// <see cref="SamplesLocator.BundledEnginesPresent"/> (which requires the staged
/// LibreOffice binary) and early-returns green on a CI runner without it.
/// </summary>
public sealed class HtmlToPdfTests
{
    private const string PdfContentType = "application/pdf";

    // %PDF- (the 5-byte ISO 32000-1 PDF file signature).
    private static readonly byte[] PdfMagicBytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D };

    [Fact]
    public async Task Convert_TrivialHtmlToPdf_RealLibreOffice_Returns200WithValidPdf()
    {
        if (!SamplesLocator.BundledEnginesPresent)
        {
            return;
        }

        using var factory = new ConversionApiFactory();
        using var client = factory.CreateClient();

        var htmlBytes = Encoding.UTF8.GetBytes(
            "<!doctype html><html><head><title>t</title></head><body><h1>html-to-pdf</h1>" +
            "<p>real LibreOffice render via writer_web_pdf_Export</p></body></html>");
        using var form = MultipartForms.BuildConvertForm(htmlBytes, "sample.html", "text/html", "pdf");

        using var response = await client.PostAsync("/api/convert", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Content.Headers.ContentType);
        Assert.Equal(PdfContentType, response.Content.Headers.ContentType!.MediaType);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(
            bytes.Length >= PdfMagicBytes.Length,
            $"Response body length {bytes.Length} is shorter than the PDF magic byte prefix.");
        for (int i = 0; i < PdfMagicBytes.Length; i++)
        {
            Assert.Equal(PdfMagicBytes[i], bytes[i]);
        }
    }
}
