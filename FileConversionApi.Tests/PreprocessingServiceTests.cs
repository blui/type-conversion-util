using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using FileConversionApi.Services;
using System.IO;

namespace FileConversionApi.Tests;

public class PreprocessingServiceTests : IDisposable
{
    private readonly PreprocessingService _service;
    private readonly Mock<ILogger<PreprocessingService>> _loggerMock;
    private readonly Mock<IDocxPreProcessor> _docxProcessorMock;
    private readonly string _testDirectory;

    public PreprocessingServiceTests()
    {
        _loggerMock = new Mock<ILogger<PreprocessingService>>();
        _docxProcessorMock = new Mock<IDocxPreProcessor>();
        _service = new PreprocessingService(_loggerMock.Object, _docxProcessorMock.Object);
        _testDirectory = Path.Combine(Path.GetTempPath(), "PreprocessingTests", Guid.NewGuid().ToString());
        Directory.CreateDirectory(_testDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    [Fact]
    public void GetCapabilities_ReturnsExpectedCapabilities()
    {
        // Act
        var capabilities = _service.GetCapabilities();

        // Assert
        capabilities.Should().NotBeNull();
        capabilities.Available.Should().BeTrue();
        capabilities.SupportedFormats.Should().Contain("docx");
        capabilities.Features.Should().NotBeNull();
        capabilities.Features.Should().Contain("Font normalization");
        capabilities.Features.Should().Contain("Theme color conversion");
        capabilities.Features.Should().Contain("LibreOffice compatibility optimization");
    }

    [Fact]
    public async Task PreprocessDocxAsync_WithSuccessfulProcessing_ReturnsSuccessResult()
    {
        // Arrange
        var inputPath = CreateTestFile("input.docx", 1000);
        var outputPath = Path.Combine(_testDirectory, "output.docx");

        var expectedResult = new PreprocessingResult
        {
            Success = true,
            InputPath = inputPath,
            OutputPath = outputPath,
            ProcessingTimeMs = 150,
            Fixes = new PreprocessingFixes
            {
                FontsNormalized = 5,
                ThemeColorsConverted = 3,
                StylesSimplified = 2,
                BoldFixed = 1
            }
        };

        _docxProcessorMock
            .Setup(x => x.ProcessAsync(inputPath, outputPath))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _service.PreprocessDocxAsync(inputPath, outputPath);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.InputPath.Should().Be(inputPath);
        result.OutputPath.Should().Be(outputPath);
        result.ProcessingTimeMs.Should().Be(150);
        result.Fixes.Should().NotBeNull();
        result.Fixes!.FontsNormalized.Should().Be(5);
        result.Fixes.ThemeColorsConverted.Should().Be(3);
        result.Fixes.StylesSimplified.Should().Be(2);
        result.Fixes.BoldFixed.Should().Be(1);
    }

    [Fact]
    public async Task PreprocessDocxAsync_WithFailedProcessing_ReturnsFallbackResult()
    {
        // Arrange
        var inputPath = CreateTestFile("input.docx", 1000);
        var outputPath = Path.Combine(_testDirectory, "output.docx");

        var processorException = new Exception("Processing failed");
        _docxProcessorMock
            .Setup(x => x.ProcessAsync(inputPath, outputPath))
            .ThrowsAsync(processorException);

        // Act
        var result = await _service.PreprocessDocxAsync(inputPath, outputPath);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Error.Should().Be("Processing failed");
        result.ProcessingTimeMs.Should().Be(0);
    }

    [Fact]
    public async Task PreprocessDocxAsync_WithNullInput_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _service.PreprocessDocxAsync(null!, "output.docx"));
    }

    [Fact]
    public async Task PreprocessDocxAsync_WithNullOutput_ThrowsArgumentNullException()
    {
        // Arrange
        var inputPath = CreateTestFile("input.docx", 1000);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            _service.PreprocessDocxAsync(inputPath, null!));
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
