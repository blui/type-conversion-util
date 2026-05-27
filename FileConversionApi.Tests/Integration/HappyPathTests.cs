using System.Net;
using FileConversionApi.Tests.Fixtures;
using Xunit;

namespace FileConversionApi.Tests.Integration;

/// <summary>
/// Real-engine happy-path coverage for the scoped DOC/DOCX -&gt; HTML/HTM branch. Each
/// test posts a real document through <see cref="ConversionApiFactory"/> with no DI
/// substitution, then asserts HTTP 200, the <c>text/html</c> content type, a non-empty
/// body, and the self-containment regex floor. Every test early-returns green when
/// <see cref="SamplesLocator.BundledEnginesPresent"/> is false so a clean CI runner
/// without staged engines does not fail the suite.
/// </summary>
[Collection("AppDataSnapshot")]
public sealed class HappyPathTests : IClassFixture<BinaryDocFixture>
{
    private const string DocxContentType =
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    private const string DocContentType = "application/msword";

    private readonly BinaryDocFixture _binaryDocFixture;

    /// <summary>
    /// Captures the shared <see cref="BinaryDocFixture"/> for the doc-to-html test. The
    /// [Collection("AppDataSnapshot")] attribute attaches the App_Data leak guard at the
    /// collection level without requiring this constructor to take the fixture as a parameter.
    /// </summary>
    /// <param name="binaryDocFixture">Ephemeral .doc generator captured for the doc-to-html test.</param>
    public HappyPathTests(BinaryDocFixture binaryDocFixture)
    {
        _binaryDocFixture = binaryDocFixture ?? throw new ArgumentNullException(nameof(binaryDocFixture));
    }

    [Fact]
    public async Task Convert_TemplateDocxToHtml_RealEngines_Returns200WithSelfContainedHtml()
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

        var body = await AssertHtmlSuccess(response);
        FidelityFloor.AssertNoFileUri(body);
        FidelityFloor.AssertAllImagesUseDataUri(body);
        FidelityFloor.AssertNoExternalStylesheet(body);
    }

    /// <summary>Same input docx posted under both target-format aliases yields the same inline-image count, the byte-stable property of the alias contract.</summary>
    [Fact]
    public async Task Convert_TemplateDocxToHtm_RealEngines_Returns200WithSelfContainedHtml()
    {
        if (!SamplesLocator.BundledEnginesPresent)
        {
            return;
        }

        using var factory = new ConversionApiFactory();
        using var client = factory.CreateClient();
        var bytes = await File.ReadAllBytesAsync(SamplesLocator.TemplateDocxPath());

        using var htmForm = MultipartForms.BuildConvertForm(bytes, "template.docx", DocxContentType, "htm");
        using var htmResponse = await client.PostAsync("/api/convert", htmForm);
        var htmBody = await AssertHtmlSuccess(htmResponse);
        FidelityFloor.AssertNoFileUri(htmBody);
        FidelityFloor.AssertAllImagesUseDataUri(htmBody);
        FidelityFloor.AssertNoExternalStylesheet(htmBody);

        using var htmlForm = MultipartForms.BuildConvertForm(bytes, "template.docx", DocxContentType, "html");
        using var htmlResponse = await client.PostAsync("/api/convert", htmlForm);
        var htmlBody = await AssertHtmlSuccess(htmlResponse);
        FidelityFloor.AssertNoFileUri(htmlBody);
        FidelityFloor.AssertAllImagesUseDataUri(htmlBody);
        FidelityFloor.AssertNoExternalStylesheet(htmlBody);

        // Alias-contract floor: byte-stable count of inline PNG payloads and img tags across the
        // two aliases. FidelityFloor doesn't expose count comparisons, so the equality assertions
        // stay local.
        Assert.Equal(FidelityFloor.CountInlinePngImages(htmlBody), FidelityFloor.CountInlinePngImages(htmBody));
        Assert.Equal(FidelityFloor.CountImgTags(htmlBody), FidelityFloor.CountImgTags(htmBody));
    }

    [Fact]
    public async Task Convert_GeneratedDocToHtml_RealEngines_Returns200WithSelfContainedHtml()
    {
        if (!SamplesLocator.BundledEnginesPresent)
        {
            return;
        }
        if (string.IsNullOrEmpty(_binaryDocFixture.GeneratedDocPath))
        {
            return;
        }

        using var factory = new ConversionApiFactory();
        using var client = factory.CreateClient();

        var bytes = await File.ReadAllBytesAsync(_binaryDocFixture.GeneratedDocPath);
        using var form = MultipartForms.BuildConvertForm(bytes, "template.doc", DocContentType, "html");

        using var response = await client.PostAsync("/api/convert", form);

        var body = await AssertHtmlSuccess(response);
        FidelityFloor.AssertNoFileUri(body);
        FidelityFloor.AssertAllImagesUseDataUri(body);
        FidelityFloor.AssertNoExternalStylesheet(body);
    }

    private static async Task<string> AssertHtmlSuccess(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(response.Content.Headers.ContentType);
        Assert.Equal("text/html", response.Content.Headers.ContentType!.MediaType);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(body.Length > 0, "Response body is empty.");
        return body;
    }
}
