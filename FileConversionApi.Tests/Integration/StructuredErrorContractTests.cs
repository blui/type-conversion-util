using System.Linq;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using FileConversionApi.Models;
using FileConversionApi.Services;
using FileConversionApi.Services.Interfaces;
using FileConversionApi.Tests.Fixtures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace FileConversionApi.Tests.Integration;

/// <summary>
/// Structured-error-contract coverage for POST /api/convert on the scoped DOC/DOCX -&gt;
/// HTML/HTM branch. <see cref="FailureReason.Timeout"/> maps to HTTP 408; every other
/// failure maps to 500. Both carry the operationId field, the X-Operation-Id header,
/// and a generic Details body that does not leak the input filename or any internal path.
/// </summary>
public sealed class StructuredErrorContractTests
{
    private static readonly byte[] DocxMagicBytes = new byte[] { 0x50, 0x4B, 0x03, 0x04 };

    private static ConversionApiFactory NewFactoryWithFakes(ConversionResult libreOfficeResult, ConversionResult nodeResult)
    {
        return new ConversionApiFactory(services =>
        {
            services.Replace(ServiceDescriptor.Singleton<ILibreOfficeProcessManager>(_ => new FakeLibreOfficeProcessManager(libreOfficeResult)));
            services.Replace(ServiceDescriptor.Singleton<INodeEngineProcessManager>(_ => new FakeNodeEngineProcessManager(nodeResult)));
        });
    }

    private static MultipartFormDataContent BuildDocxToHtmlForm()
    {
        var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(DocxMagicBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document");
        form.Add(fileContent, "file", "input.docx");
        form.Add(new StringContent("html"), "targetFormat");
        return form;
    }

    [Fact]
    public async Task Convert_DocxToHtml_FakeReturnsTimeout_Returns408WithOperationIdHeader()
    {
        var timeoutResult = new ConversionResult
        {
            Success = false,
            Error = "LibreOffice conversion timed out after 300 seconds",
            FailureReason = FailureReason.Timeout
        };
        using var factory = NewFactoryWithFakes(timeoutResult, new ConversionResult { Success = true });
        using var client = factory.CreateClient();
        using var form = BuildDocxToHtmlForm();

        using var response = await client.PostAsync("/api/convert", form);

        Assert.Equal(HttpStatusCode.RequestTimeout, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrEmpty(body!.OperationId));
        Assert.True(response.Headers.TryGetValues("X-Operation-Id", out var headerValues));
        Assert.Equal(body.OperationId, headerValues!.Single());
        Assert.NotNull(body.Details);
        Assert.Contains(body.Details!, d => d.Contains("operation ID", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Convert_DocxToHtml_FakeReturnsEngineError_Returns500WithOperationIdHeader()
    {
        var engineErrorResult = new ConversionResult
        {
            Success = false,
            Error = "engine exit 1",
            FailureReason = FailureReason.EngineError
        };
        using var factory = NewFactoryWithFakes(engineErrorResult, new ConversionResult { Success = true });
        using var client = factory.CreateClient();
        using var form = BuildDocxToHtmlForm();

        using var response = await client.PostAsync("/api/convert", form);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrEmpty(body!.OperationId));
        Assert.True(response.Headers.TryGetValues("X-Operation-Id", out var headerValues));
        Assert.Equal(body.OperationId, headerValues!.Single());
    }

    [Fact]
    public async Task Convert_DocxToHtml_GenericErrorBody_NoInternalPathLeaked()
    {
        var leakyResult = new ConversionResult
        {
            Success = false,
            Error = @"C:\App_Data\temp\uploads\op123\input.docx not found (soffice.exe exit 1)",
            FailureReason = FailureReason.EngineError
        };
        using var factory = NewFactoryWithFakes(leakyResult, new ConversionResult { Success = true });
        using var client = factory.CreateClient();
        using var form = BuildDocxToHtmlForm();

        using var response = await client.PostAsync("/api/convert", form);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(body);
        Assert.NotNull(body!.Details);
        var details = body.Details!;
        Assert.Contains(details, d => d.Contains("operation ID", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(details, d => d.Contains("App_Data", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(details, d => d.Contains("soffice.exe", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(details, d => d.Contains(@"C:\", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(details, d => d.Contains("input.docx", StringComparison.OrdinalIgnoreCase));
    }
}
