using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using FileConversionApi.Utils;
using System.IO;

namespace FileConversionApi.Tests;

public class ConversionValidatorTests : IDisposable
{
    private readonly ConversionValidator _validator;
    private readonly string _testDirectory;
    private readonly Mock<ILogger<ConversionValidator>> _loggerMock;

    public ConversionValidatorTests()
    {
        _loggerMock = new Mock<ILogger<ConversionValidator>>();
        _validator = new ConversionValidator(_loggerMock.Object);
        _testDirectory = Path.Combine(Path.GetTempPath(), "FileConversionTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    [Theory]
    [InlineData("docx", "pdf")]
    [InlineData("xlsx", "csv")]
    [InlineData("csv", "xlsx")]
    [InlineData("jpg", "png")]
    [InlineData("pdf", "txt")]
    public void ValidateDocxToPdfAsync_WithValidFiles_ReturnsValidResult(string inputFormat, string outputFormat)
    {
        // Arrange
        var inputFile = CreateTestFile($"test.{inputFormat}", 1000);
        var outputFile = CreateTestFile($"output.{outputFormat}", 2000);

        // Act
        var result = _validator.ValidateDocxToPdfAsync(inputFile, outputFile).GetAwaiter().GetResult();

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.Issues.Should().BeEmpty();
        result.Info.Should().ContainKey("docxSize");
        result.Info.Should().ContainKey("pdfSize");
    }

    [Fact]
    public void ValidateDocxToPdfAsync_WithMissingInputFile_ReturnsInvalidResult()
    {
        // Arrange
        var inputFile = Path.Combine(_testDirectory, "nonexistent.docx");
        var outputFile = CreateTestFile("output.pdf", 1000);

        // Act
        var result = _validator.ValidateDocxToPdfAsync(inputFile, outputFile).GetAwaiter().GetResult();

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Issues.Should().ContainSingle().Which.Should().Contain("Source DOCX file not found");
    }

    [Fact]
    public void ValidateDocxToPdfAsync_WithMissingOutputFile_ReturnsInvalidResult()
    {
        // Arrange
        var inputFile = CreateTestFile("test.docx", 1000);
        var outputFile = Path.Combine(_testDirectory, "nonexistent.pdf");

        // Act
        var result = _validator.ValidateDocxToPdfAsync(inputFile, outputFile).GetAwaiter().GetResult();

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Issues.Should().ContainSingle().Which.Should().Contain("Output PDF file not found");
    }

    [Fact]
    public void ValidateDocxToPdfAsync_WithTooSmallPdfFile_ReturnsInvalidResult()
    {
        // Arrange
        var inputFile = CreateTestFile("test.docx", 50000); // 50KB
        var outputFile = CreateTestFile("output.pdf", 5000); // 5KB - too small

        // Act
        var result = _validator.ValidateDocxToPdfAsync(inputFile, outputFile).GetAwaiter().GetResult();

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Issues.Should().ContainSingle().Which.Should().Contain("PDF file too small");
    }

    [Theory]
    [InlineData("xlsx", "csv")]
    [InlineData("csv", "xlsx")]
    public void SpreadsheetValidation_WithValidFiles_ReturnsValidResult(string inputFormat, string outputFormat)
    {
        // Arrange
        var inputFile = CreateTestFile($"test.{inputFormat}", 1000);
        var outputFile = CreateTestFile($"output.{outputFormat}", 500);

        // Act
        var result = inputFormat == "xlsx"
            ? _validator.ValidateXlsxToCsvAsync(inputFile, outputFile).GetAwaiter().GetResult()
            : _validator.ValidateCsvToXlsxAsync(inputFile, outputFile).GetAwaiter().GetResult();

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.Issues.Should().BeEmpty();
    }

    [Fact]
    public void ValidateImageConversionAsync_WithValidFiles_ReturnsValidResult()
    {
        // Arrange
        var inputFile = CreateTestFile("test.jpg", 50000);
        var outputFile = CreateTestFile("output.png", 25000);

        // Act
        var result = _validator.ValidateImageConversionAsync(inputFile, outputFile, "JPG->PNG").GetAwaiter().GetResult();

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeTrue();
        result.Issues.Should().BeEmpty();
        result.ConversionType.Should().Be("JPG->PNG");
    }

    [Fact]
    public void ValidateImageConversionAsync_WithTooSmallOutput_ReturnsInvalidResult()
    {
        // Arrange
        var inputFile = CreateTestFile("test.jpg", 50000);
        var outputFile = CreateTestFile("output.png", 500); // Too small

        // Act
        var result = _validator.ValidateImageConversionAsync(inputFile, outputFile, "JPG->PNG").GetAwaiter().GetResult();

        // Assert
        result.Should().NotBeNull();
        result.IsValid.Should().BeFalse();
        result.Issues.Should().ContainSingle().Which.Should().Contain("Output image file too small");
    }

    private string CreateTestFile(string fileName, long sizeInBytes)
    {
        var filePath = Path.Combine(_testDirectory, fileName);
        var data = new byte[sizeInBytes];
        for (int i = 0; i < sizeInBytes; i++)
        {
            data[i] = (byte)(i % 256);
        }
        File.WriteAllBytes(filePath, data);
        return filePath;
    }
}
