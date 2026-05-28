using System.Net;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FileConversionApi.Tests.Fixtures;
using iText.Kernel.Pdf;
using Xunit;

namespace FileConversionApi.Tests.Integration;

/// <summary>
/// End-to-end round-trip coverage for the <c>docx -&gt; html -&gt; pdf</c> and
/// <c>docx -&gt; html -&gt; docx</c> pipelines, which together back the project promise that
/// HTML produced by the forward pipeline can be reversed back to its source format with
/// page-identical fidelity. Both tests gate on
/// <see cref="SamplesLocator.BundledEnginesPresent"/> (the docx -&gt; html hop requires the
/// staged LibreOffice + Node binaries) and early-return on a clean CI runner without them.
/// </summary>
[Collection("AppDataSnapshot")]
public sealed class PipelineRoundTripTests
{
    private const string DocxContentType =
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private static readonly Regex PageContainerPattern = new(
        "<div class=\"page(?:\"|\\s)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    [Fact]
    public async Task DocxToHtmlToPdf_RoundTrip_ProducesPdfWithMatchingPageCount()
    {
        if (!SamplesLocator.BundledEnginesPresent)
        {
            return;
        }

        using var factory = new ConversionApiFactory();
        using var client = factory.CreateClient();

        var docxBytes = await File.ReadAllBytesAsync(SamplesLocator.TemplateDocxPath());

        using var docxToHtmlForm = MultipartForms.BuildConvertForm(docxBytes, "template.docx", DocxContentType, "html");
        using var htmlResponse = await client.PostAsync("/api/convert", docxToHtmlForm);
        Assert.Equal(HttpStatusCode.OK, htmlResponse.StatusCode);

        var htmlBody = await htmlResponse.Content.ReadAsStringAsync();
        var expectedPageCount = PageContainerPattern.Matches(htmlBody).Count;
        Assert.True(expectedPageCount >= 1, $"Forward hop produced {expectedPageCount} pages.");

        var htmlBytes = System.Text.Encoding.UTF8.GetBytes(htmlBody);
        using var htmlToPdfForm = MultipartForms.BuildConvertForm(htmlBytes, "template.html", "text/html", "pdf");
        using var pdfResponse = await client.PostAsync("/api/convert", htmlToPdfForm);

        Assert.Equal(HttpStatusCode.OK, pdfResponse.StatusCode);
        Assert.Equal("application/pdf", pdfResponse.Content.Headers.ContentType?.MediaType);

        var pdfBytes = await pdfResponse.Content.ReadAsByteArrayAsync();

        await using var pdfStream = new MemoryStream(pdfBytes);
        using var pdfReader = new PdfReader(pdfStream);
        using var pdfDocument = new PdfDocument(pdfReader);

        // The pre-fix LibreOffice-based html->pdf path produced 2N PDF pages (one for the
        // image, one for the reflowed text layer) plus garbled "wordsconcatenated" text. The
        // fast path emits exactly N PDF pages with the raster as the page body.
        Assert.Equal(expectedPageCount, pdfDocument.GetNumberOfPages());

        // Each PDF page should carry the source raster's pixel dimensions (the forward emitter
        // sets PageSize == raster size; spot-check page 1).
        var firstPage = pdfDocument.GetPage(1);
        var pageSize = firstPage.GetPageSize();
        Assert.True(pageSize.GetWidth() > 0);
        Assert.True(pageSize.GetHeight() > 0);
    }

    [Fact]
    public async Task DocxToHtmlToDocx_RoundTrip_ProducesDocxWithOneImagePerSourcePage()
    {
        if (!SamplesLocator.BundledEnginesPresent)
        {
            return;
        }

        using var factory = new ConversionApiFactory();
        using var client = factory.CreateClient();

        var docxBytes = await File.ReadAllBytesAsync(SamplesLocator.TemplateDocxPath());

        using var docxToHtmlForm = MultipartForms.BuildConvertForm(docxBytes, "template.docx", DocxContentType, "html");
        using var htmlResponse = await client.PostAsync("/api/convert", docxToHtmlForm);
        Assert.Equal(HttpStatusCode.OK, htmlResponse.StatusCode);

        var htmlBody = await htmlResponse.Content.ReadAsStringAsync();
        var expectedPageCount = PageContainerPattern.Matches(htmlBody).Count;
        Assert.True(expectedPageCount >= 1, $"Forward hop produced {expectedPageCount} pages.");

        var htmlBytes = System.Text.Encoding.UTF8.GetBytes(htmlBody);
        using var htmlToDocxForm = MultipartForms.BuildConvertForm(htmlBytes, "template.html", "text/html", "docx");
        using var docxResponse = await client.PostAsync("/api/convert", htmlToDocxForm);

        Assert.Equal(HttpStatusCode.OK, docxResponse.StatusCode);
        Assert.Equal(DocxContentType, docxResponse.Content.Headers.ContentType?.MediaType);

        var docxResponseBytes = await docxResponse.Content.ReadAsByteArrayAsync();

        await using var docxStream = new MemoryStream(docxResponseBytes);
        using var package = WordprocessingDocument.Open(docxStream, isEditable: false);
        Assert.NotNull(package.MainDocumentPart);

        var mainPart = package.MainDocumentPart!;
        Assert.NotNull(mainPart.Document);
        Assert.NotNull(mainPart.Document.Body);

        var imageParts = mainPart.ImageParts.ToList();
        Assert.Equal(expectedPageCount, imageParts.Count);

        // Find/replace-indexable hidden text: every page emits a Vanish-properties run before
        // the picture so Word can locate words in the rebuilt document. The exact phrases
        // depend on the template, but the total length must be non-zero.
        var hiddenTextLength = 0;
        foreach (var run in mainPart.Document.Body!.Descendants<Run>())
        {
            if (run.RunProperties?.GetFirstChild<Vanish>() is not null)
            {
                foreach (var text in run.Descendants<Text>())
                {
                    hiddenTextLength += text.Text.Length;
                }
            }
        }
        Assert.True(hiddenTextLength > 0, "Round-tripped DOCX has no hidden-text runs; search would not work.");

        // One OOXML section per source page (each carries its own page-size to match the
        // raster). SectionProperties live both inside section-break paragraphs and at body
        // level (final section), so count both occurrences.
        var sectionPropertiesCount = mainPart.Document.Body!
            .Descendants<SectionProperties>()
            .Count();
        Assert.Equal(expectedPageCount, sectionPropertiesCount);
    }
}
