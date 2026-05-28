using System.Net;
using System.Text;
using FileConversionApi.Tests.Fixtures;
using Xunit;

namespace FileConversionApi.Tests.Integration;

/// <summary>
/// Real-engine happy-path coverage for the txt -&gt; pdf path. iText runs entirely in-process,
/// so this test does not gate on <see cref="SamplesLocator.BundledEnginesPresent"/>; a clean
/// CI runner without staged LibreOffice or Node engines still exercises the iText
/// <c>PdfWriter</c> construction this test exists to defend.
/// </summary>
public sealed class TextToPdfTests
{
    private const string PdfContentType = "application/pdf";

    // %PDF- (the 5-byte ISO 32000-1 PDF file signature).
    private static readonly byte[] PdfMagicBytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D };

    [Fact]
    public async Task Convert_TxtToPdf_RealIText_Returns200WithValidPdf()
    {
        using var factory = new ConversionApiFactory();
        using var client = factory.CreateClient();

        var textBytes = Encoding.UTF8.GetBytes("First paragraph.\n\nSecond paragraph.\n");
        using var form = MultipartForms.BuildConvertForm(textBytes, "sample.txt", "text/plain", "pdf");

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
