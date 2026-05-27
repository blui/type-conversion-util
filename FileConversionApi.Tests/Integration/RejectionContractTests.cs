using System.Net;
using System.Net.Http.Json;
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
}
