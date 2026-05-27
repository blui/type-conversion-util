using System.Linq;
using System.Net;
using System.Net.Http.Json;
using FileConversionApi.Models;
using FileConversionApi.Tests.Fixtures;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace FileConversionApi.Tests.Integration;

/// <summary>
/// Asserts the forced-timeout path returns 408 and leaves no temp or LibreOffice profile
/// artifacts behind. The test injects a <c>LibreOffice:TimeoutSeconds=1</c> override so
/// the real document trips the typed timeout branch in
/// <see cref="FileConversionApi.Services.LibreOfficeProcessManager"/>; the response is
/// asserted against the structured-error contract and the App_Data subdirectories under
/// <c>libreoffice-profiles</c> and <c>temp/{uploads,converted}</c> are asserted to
/// contain no surviving operation directories. The cleanup invariants are additionally
/// re-checked by the shared <see cref="AppDataSnapshot"/> at dispose time.
/// </summary>
[Collection("AppDataSnapshot")]
public sealed class TimeoutCleanupTests
{
    private const string DocxContentType =
        "application/vnd.openxmlformats-officedocument.wordprocessingml.document";

    [Fact]
    public async Task Convert_TemplateDocxToHtml_TimeoutForced_Returns408WithCleanProfileAndTempDirs()
    {
        if (!SamplesLocator.BundledEnginesPresent)
        {
            return;
        }

        using var baseFactory = new ConversionApiFactory();
        using var factory = baseFactory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration(c => c.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["LibreOffice:TimeoutSeconds"] = "1"
            }));
        });
        using var client = factory.CreateClient();

        var bytes = await File.ReadAllBytesAsync(SamplesLocator.TemplateDocxPath());
        using var form = MultipartForms.BuildConvertForm(bytes, "template.docx", DocxContentType, "html");

        using var response = await client.PostAsync("/api/convert", form);

        Assert.Equal(HttpStatusCode.RequestTimeout, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<ErrorResponse>();
        Assert.NotNull(body);
        Assert.False(string.IsNullOrEmpty(body!.OperationId));
        Assert.True(response.Headers.TryGetValues("X-Operation-Id", out var headerValues));
        Assert.Equal(body.OperationId, headerValues!.Single());
        Assert.NotNull(body.Details);
        Assert.Contains(body.Details!, d => d.Contains("operation ID", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(body.Details!, d => d.Contains("App_Data", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(body.Details!, d => d.Contains("soffice.exe", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(body.Details!, d => d.Contains(@"C:\", StringComparison.OrdinalIgnoreCase));

        AssertNoOperationSubdirectory("libreoffice-profiles", body.OperationId!);
        AssertNoOperationSubdirectory(Path.Combine("temp", "uploads"), body.OperationId!);
        AssertNoOperationSubdirectory(Path.Combine("temp", "converted"), body.OperationId!);
    }

    private static void AssertNoOperationSubdirectory(string relativePath, string operationId)
    {
        var baseDir = Path.Combine(AppContext.BaseDirectory, "App_Data", relativePath);
        var operationDir = Path.Combine(baseDir, operationId);
        Assert.False(Directory.Exists(operationDir),
            $"Operation directory survived the timeout path: {operationDir}");
    }
}
