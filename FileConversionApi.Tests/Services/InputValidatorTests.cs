using FileConversionApi.Models;
using FileConversionApi.Services;
using FileConversionApi.Tests.Fixtures;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace FileConversionApi.Tests.Services;

/// <summary>
/// Direct unit tests on <see cref="InputValidator.ValidateFile"/>,
/// <see cref="InputValidator.ValidateConversion"/>, and the private magic-byte branch
/// of <c>VerifyFileContent</c>. Construction uses <see cref="NullLogger{T}"/> and
/// <see cref="Options.Create{T}(T)"/> so no HTTP pipeline is started.
/// </summary>
public sealed class InputValidatorTests
{
    private static InputValidator NewValidator(long maxFileSize = 52428800)
        => new(NullLogger<InputValidator>.Instance,
               Options.Create(new FileHandlingConfig { MaxFileSize = maxFileSize }));

    [Fact]
    public void ValidateFile_EmptyFile_ReturnsInvalid()
    {
        var validator = NewValidator();
        var file = new StubFormFile(Array.Empty<byte>(), "test.docx");

        var result = validator.ValidateFile(file);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("required", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateFile_OversizedDocx_ReturnsInvalid()
    {
        var validator = NewValidator(maxFileSize: 100);
        var payload = new byte[200];
        payload[0] = 0x50;
        payload[1] = 0x4B;
        payload[2] = 0x03;
        payload[3] = 0x04;
        var file = new StubFormFile(payload, "test.docx");

        var result = validator.ValidateFile(file);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("exceeds maximum", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateFile_PathTraversalFilename_ReturnsInvalid()
    {
        var validator = NewValidator();
        var payload = new byte[] { 0x50, 0x4B, 0x03, 0x04 };
        var file = new StubFormFile(payload, "../../etc/passwd.docx");

        var result = validator.ValidateFile(file);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("invalid characters", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateFile_DocxWithPdfBytes_ReturnsInvalid()
    {
        var validator = NewValidator();
        var payload = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D };
        var file = new StubFormFile(payload, "test.docx");

        var result = validator.ValidateFile(file);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("does not match the extension", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateConversion_PdfToHtml_ReturnsInvalid()
    {
        var validator = NewValidator();

        var result = validator.ValidateConversion("pdf", "html");

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("not supported", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateFile_DocxBelowMagicByteLength_ReturnsInvalid()
    {
        // A 1-byte payload trips VerifyFileContent's truncation branch: ReadAtLeast returns 1,
        // less than the 4-byte signature requirement, so the validator rejects before the
        // signature comparison. Distinct from ValidateFile_DocxWithPdfBytes_ReturnsInvalid above
        // (which trips the signature-mismatch branch); both surface the same public error text,
        // but only the public ValidateFile API is exercised here.
        var validator = NewValidator();
        var payload = new byte[] { 0x50 };
        var file = new StubFormFile(payload, "tiny.docx");

        var result = validator.ValidateFile(file);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("does not match the extension", StringComparison.OrdinalIgnoreCase));
    }
}
