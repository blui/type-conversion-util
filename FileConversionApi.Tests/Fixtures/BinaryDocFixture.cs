using FileConversionApi.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FileConversionApi.Tests.Fixtures;

/// <summary>
/// Generates an ephemeral OLE2 .doc at test-class setup via a real LibreOffice round-trip
/// from <c>samples/template.docx</c>. The generated file lives under
/// <c>bin/.../Fixtures/Generated/</c>, is recreated on every test run, and never leaves
/// the gitignored <c>bin</c> tree. The fixture is safe to compose with test classes that
/// gate on <see cref="SamplesLocator.BundledEnginesPresent"/>: when the gate is false
/// <see cref="GeneratedDocPath"/> stays empty and dependent tests early-return.
/// </summary>
public sealed class BinaryDocFixture : IAsyncLifetime
{
    private const string GeneratedFileName = "template.doc";

    private static readonly byte[] Ole2Magic =
    {
        0xD0, 0xCF, 0x11, 0xE0
    };

    /// <summary>
    /// Absolute path to the generated OLE2 .doc, populated by <see cref="InitializeAsync"/>.
    /// Empty when bundled engines are absent, in which case the dependent test must early-return.
    /// </summary>
    public string GeneratedDocPath { get; private set; } = string.Empty;

    /// <inheritdoc/>
    public async Task InitializeAsync()
    {
        if (!SamplesLocator.BundledEnginesPresent)
        {
            return;
        }

        await using var factory = new ConversionApiFactory();
        var manager = factory.Services.GetRequiredService<ILibreOfficeProcessManager>();

        var input = SamplesLocator.TemplateDocxPath();
        var outputDir = Path.Combine(AppContext.BaseDirectory, "Fixtures", "Generated");
        Directory.CreateDirectory(outputDir);
        var output = Path.Combine(outputDir, GeneratedFileName);

        var result = await manager.ConvertAsync(input, output, "doc");
        if (!result.Success || !File.Exists(output))
        {
            throw new InvalidOperationException(
                "BinaryDocFixture failed to generate template.doc via LibreOffice round-trip: "
                + (result.Error ?? "output file not found"));
        }

        VerifyOle2Magic(output);
        GeneratedDocPath = output;
    }

    /// <inheritdoc/>
    public Task DisposeAsync()
    {
        if (!string.IsNullOrEmpty(GeneratedDocPath) && File.Exists(GeneratedDocPath))
        {
            try
            {
                File.Delete(GeneratedDocPath);
            }
            catch (IOException)
            {
                // Best-effort cleanup; bin is gitignored and the next test run rewrites the file.
            }
        }
        return Task.CompletedTask;
    }

    private static void VerifyOle2Magic(string path)
    {
        var header = new byte[Ole2Magic.Length];
        using (var stream = File.OpenRead(path))
        {
            // Stream.Read is allowed to return fewer bytes than requested even when more are
            // available, which would let a short read fail the magic-byte comparison on a
            // perfectly valid file. Accumulate reads until the header buffer is full or the
            // stream reports genuine EOF. The loop is bounded by header.Length (4 iterations).
            int total = 0;
            while (total < header.Length)
            {
                int read = stream.Read(header, total, header.Length - total);
                if (read == 0)
                {
                    break;
                }
                total += read;
            }
            if (total < header.Length)
            {
                throw new InvalidOperationException(
                    $"BinaryDocFixture: generated file is shorter than the OLE2 header ({total} bytes).");
            }
        }
        for (int i = 0; i < Ole2Magic.Length; i++)
        {
            if (header[i] != Ole2Magic[i])
            {
                throw new InvalidOperationException(
                    "BinaryDocFixture: generated file does not begin with the OLE2 magic 0xD0 0xCF 0x11 0xE0.");
            }
        }
    }
}
