using System.Net;
using System.Net.Http.Json;
using System.Text;
using FileConversionApi.Models;
using FileConversionApi.Services;
using FileConversionApi.Services.Interfaces;
using FileConversionApi.Tests.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace FileConversionApi.Tests.Integration;

/// <summary>
/// Rejection-path coverage for POST /api/convert: unsupported pair, oversized upload,
/// magic-byte mismatch, and missing-file part. Asserts the scope boundary defined by
/// <c>ConversionController.IsScopedHtmlConversion</c>: scoped DOC/DOCX -&gt; HTML/HTM
/// 400s carry the operationId field plus X-Operation-Id header; every other 400 does not.
/// </summary>
public sealed class RejectionContractTests
{
    private static readonly byte[] DocxMagicBytes = new byte[] { 0x50, 0x4B, 0x03, 0x04 };
    private static readonly byte[] PdfMagicBytes = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D };

    // Minimal byte payload for the xml rejection row below. The validator looks at extension +
    // declared content-type for xml (no magic-byte signature), so any plausible body is accepted
    // at the format-sniff layer and the rejection fires at the conversion-matrix lookup instead.
    private static readonly byte[] TrivialXmlBytes = Encoding.UTF8.GetBytes("<?xml version=\"1.0\"?><root/>");

    private static ConversionApiFactory NewFactoryWithSuccessfulFakes()
    {
        var canned = new ConversionResult { Success = true };
        return new ConversionApiFactory(services =>
        {
            services.Replace(ServiceDescriptor.Singleton<ILibreOfficeProcessManager>(_ => new FakeLibreOfficeProcessManager(canned)));
            services.Replace(ServiceDescriptor.Singleton<INodeEngineProcessManager>(_ => new FakeNodeEngineProcessManager(canned)));
        });
    }

    [Fact]
    public async Task Convert_PdfToHtml_Returns400_NotScoped_NoOperationId()
    {
        using var factory = NewFactoryWithSuccessfulFakes();
        using var client = factory.CreateClient();
        using var form = MultipartForms.BuildConvertForm(PdfMagicBytes, "input.pdf", "application/pdf", "html");

        using var response = await client.PostAsync("/api/convert", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(body);
        Assert.Null(body!.OperationId);
        Assert.False(response.Headers.Contains("X-Operation-Id"));
    }

    [Fact]
    public async Task Convert_DocxToPptx_Returns400_NotScoped_NoOperationId()
    {
        using var factory = NewFactoryWithSuccessfulFakes();
        using var client = factory.CreateClient();
        using var form = MultipartForms.BuildConvertForm(
            DocxMagicBytes,
            "input.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "pptx");

        using var response = await client.PostAsync("/api/convert", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(body);
        Assert.Null(body!.OperationId);
        Assert.False(response.Headers.Contains("X-Operation-Id"));
    }

    [Fact]
    public async Task Convert_DocxOversized_Returns400_Scoped_WithOperationIdHeader()
    {
        var canned = new ConversionResult { Success = true };
        using var baseFactory = new ConversionApiFactory(services =>
        {
            services.Replace(ServiceDescriptor.Singleton<ILibreOfficeProcessManager>(_ => new FakeLibreOfficeProcessManager(canned)));
            services.Replace(ServiceDescriptor.Singleton<INodeEngineProcessManager>(_ => new FakeNodeEngineProcessManager(canned)));
        });
        using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration(c => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["FileHandling:MaxFileSize"] = "100"
            }));
        });
        using var client = factory.CreateClient();

        var payload = new byte[200];
        payload[0] = 0x50;
        payload[1] = 0x4B;
        payload[2] = 0x03;
        payload[3] = 0x04;
        using var form = MultipartForms.BuildConvertForm(
            payload,
            "input.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "html");

        using var response = await client.PostAsync("/api/convert", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrEmpty(body!.OperationId));
        Assert.True(response.Headers.TryGetValues("X-Operation-Id", out var headerValues));
        Assert.Equal(body.OperationId, headerValues!.Single());
    }

    [Fact]
    public async Task Convert_DocxWithPdfBytes_Returns400_Scoped_WithOperationIdHeader()
    {
        using var factory = NewFactoryWithSuccessfulFakes();
        using var client = factory.CreateClient();
        using var form = MultipartForms.BuildConvertForm(
            PdfMagicBytes,
            "input.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "html");

        using var response = await client.PostAsync("/api/convert", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrEmpty(body!.OperationId));
        Assert.True(response.Headers.TryGetValues("X-Operation-Id", out var headerValues));
        Assert.Equal(body.OperationId, headerValues!.Single());
    }

    [Fact]
    public async Task Convert_MissingFilePart_Returns400_NotScoped_NoOperationId()
    {
        using var factory = NewFactoryWithSuccessfulFakes();
        using var client = factory.CreateClient();
        using var form = new MultipartFormDataContent
        {
            { new StringContent("html"), "targetFormat" }
        };

        using var response = await client.PostAsync("/api/convert", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(body);
        Assert.Null(body!.OperationId);
        Assert.False(response.Headers.Contains("X-Operation-Id"));
    }

    // Defends the explicit decision to drop xml as a source format from the conversion matrix.
    // xml -> pdf was previously routed through a naive iText text-dump that threw at the
    // BouncyCastle adapter boundary; no real renderer is in scope (XSL-FO or equivalent would
    // be a separate milestone), so the matrix row stays out. Re-adding xml -> pdf would need
    // a real renderer; this rejection contract makes any such regression a test failure rather
    // than a runtime surprise.

    [Fact]
    public async Task Convert_XmlToPdf_Returns400_NotScoped_NoOperationId()
    {
        using var factory = NewFactoryWithSuccessfulFakes();
        using var client = factory.CreateClient();
        using var form = MultipartForms.BuildConvertForm(TrivialXmlBytes, "input.xml", "application/xml", "pdf");

        using var response = await client.PostAsync("/api/convert", form);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(body);
        Assert.Null(body!.OperationId);
        Assert.False(response.Headers.Contains("X-Operation-Id"));
    }
}
