using System.Net;
using FileConversionApi.Tests.Fixtures;
using Xunit;

namespace FileConversionApi.Tests.Integration;

/// <summary>
/// Real-engine fidelity-floor coverage against <c>samples/template.docx</c>. The body-level
/// test runs every ported <see cref="FidelityFloor"/> helper against the HTTP response body;
/// the header-level test asserts the response is a single <c>text/html</c> body rather than
/// a multipart bundle, which is the HTTP-layer proxy for the single-self-contained-file
/// promise. Each test early-returns green when
/// <see cref="SamplesLocator.BundledEnginesPresent"/> is false.
/// </summary>
[Collection("AppDataSnapshot")]
public sealed class TemplateDocxFidelityTests
{
    private const string DocxContentType =
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    [Fact]
    public async Task Convert_TemplateDocxToHtml_RealEngines_PassesFidelityFloor()
    {
        if (!SamplesLocator.BundledEnginesPresent)
        {
            return;
        }

        using var factory = new ConversionApiFactory();
        using var client = factory.CreateClient();

        var bytes = await File.ReadAllBytesAsync(SamplesLocator.TemplateDocxPath());
        using var form = MultipartForms.BuildConvertForm(bytes, "template.docx", DocxContentType, "html");

        using var response = await client.PostAsync("/api/convert", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();

        FidelityFloor.AssertHasInlineDataImage(body);
        FidelityFloor.AssertAllImagesUseDataUri(body);
        FidelityFloor.AssertNoLocalPathHref(body);
        FidelityFloor.AssertNoFileUri(body);
        FidelityFloor.AssertTextLayerNonEmpty(body);
        FidelityFloor.AssertBodyNonEmpty(body);
        FidelityFloor.AssertNoExternalStylesheet(body);
        FidelityFloor.AssertPageContainerAndImageCountConsistent(body);
        FidelityFloor.AssertExtractableTextNonEmpty(body);
        FidelityFloor.AssertNoExternalFonts(body);
        FidelityFloor.AssertAllDataUriPngPayloadsDecodeNonZero(body);
    }

    [Fact]
    public async Task Convert_TemplateDocxToHtml_RealEngines_ResponseHeadersDeclareHtmlNotMultipart()
    {
        if (!SamplesLocator.BundledEnginesPresent)
        {
            return;
        }

        using var factory = new ConversionApiFactory();
        using var client = factory.CreateClient();

        var bytes = await File.ReadAllBytesAsync(SamplesLocator.TemplateDocxPath());
        using var form = MultipartForms.BuildConvertForm(bytes, "template.docx", DocxContentType, "html");

        using var response = await client.PostAsync("/api/convert", form);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Content.Headers.ContentType);
        var mediaType = response.Content.Headers.ContentType!.MediaType ?? string.Empty;
        Assert.Equal("text/html", mediaType);
        Assert.False(mediaType.StartsWith("multipart/", StringComparison.OrdinalIgnoreCase));
        Assert.NotEqual("application/zip", mediaType);
    }

}
