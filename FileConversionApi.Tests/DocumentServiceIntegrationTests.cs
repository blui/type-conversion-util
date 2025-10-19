using Xunit;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using FileConversionApi.Services;
using System.IO;

namespace FileConversionApi.Tests;

public class DocumentServiceIntegrationTests : IDisposable
{
    private readonly DocumentService _documentService;
    private readonly Mock<ILogger<DocumentService>> _loggerMock;
    private readonly Mock<IConversionEngine> _conversionEngineMock;
    private readonly Mock<IPdfService> _pdfServiceMock;
    private readonly Mock<ILibreOfficeService> _libreOfficeServiceMock;
    private readonly Mock<ISpreadsheetService> _spreadsheetServiceMock;
    private readonly string _testDirectory;

    public DocumentServiceIntegrationTests()
    {
        _loggerMock = new Mock<ILogger<DocumentService>>();
        _conversionEngineMock = new Mock<IConversionEngine>();
        _pdfServiceMock = new Mock<IPdfService>();
        _libreOfficeServiceMock = new Mock<ILibreOfficeService>();
        _spreadsheetServiceMock = new Mock<ISpreadsheetService>();

        _documentService = new DocumentService(
            _loggerMock.Object,
            _conversionEngineMock.Object,
            _pdfServiceMock.Object,
            _libreOfficeServiceMock.Object,
            _spreadsheetServiceMock.Object);

        _testDirectory = Path.Combine(Path.GetTempPath(), "DocumentServiceTests", Guid.NewGuid().ToString());
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
    [InlineData("docx", "pdf", "docx-pdf")]
    [InlineData("xlsx", "pdf", "xlsx-pdf")]
    [InlineData("pptx", "pdf", "pptx-pdf")]
    public async Task ConvertAsync_LibreOfficeFormats_ReturnsSuccess(string inputFormat, string outputFormat, string conversionKey)
    {
        // Arrange
        var inputPath = CreateTestFile($"test.{inputFormat}", 1000);
        var outputPath = Path.Combine(_testDirectory, $"output.{outputFormat}");

        var expectedResult = new ConversionResult
        {
            Success = true,
            OutputPath = outputPath,
            ProcessingTimeMs = 1000,
            ConversionMethod = "LibreOffice"
        };

        _conversionEngineMock
            .Setup(x => x.ConvertLibreOfficeFormatAsync(inputFormat, outputFormat, inputPath, outputPath))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _documentService.ConvertAsync(conversionKey, inputPath, outputPath);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.OutputPath.Should().Be(outputPath);
        result.ConversionMethod.Should().Be("LibreOffice");
    }

    [Fact]
    public async Task ConvertAsync_DocxToPdf_UsesConversionEngine()
    {
        // Arrange
        var inputPath = CreateTestFile("test.docx", 1000);
        var outputPath = Path.Combine(_testDirectory, "output.pdf");

        var expectedResult = new ConversionResult
        {
            Success = true,
            OutputPath = outputPath,
            ProcessingTimeMs = 500,
            ConversionMethod = "LibreOffice"
        };

        _conversionEngineMock
            .Setup(x => x.DocxToPdfAsync(inputPath, outputPath))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _documentService.ConvertAsync("docx-pdf", inputPath, outputPath);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.OutputPath.Should().Be(outputPath);
        result.ConversionMethod.Should().Be("LibreOffice");

        _conversionEngineMock.Verify(x => x.DocxToPdfAsync(inputPath, outputPath), Times.Once);
    }

    [Fact]
    public async Task ConvertAsync_PdfToDocx_UsesConversionEngine()
    {
        // Arrange
        var inputPath = CreateTestFile("test.pdf", 1000);
        var outputPath = Path.Combine(_testDirectory, "output.docx");

        var expectedResult = new ConversionResult
        {
            Success = true,
            OutputPath = outputPath,
            ProcessingTimeMs = 800,
            ConversionMethod = "LibreOffice"
        };

        _conversionEngineMock
            .Setup(x => x.ConvertLibreOfficeFormatAsync("pdf", "docx", inputPath, outputPath))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _documentService.ConvertAsync("pdf-docx", inputPath, outputPath);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.OutputPath.Should().Be(outputPath);
        result.ConversionMethod.Should().Be("LibreOffice");
    }

    [Fact]
    public async Task ConvertAsync_XlsxToCsv_UsesSpreadsheetService()
    {
        // Arrange
        var inputPath = CreateTestFile("test.xlsx", 1000);
        var outputPath = Path.Combine(_testDirectory, "output.csv");

        var expectedResult = new ConversionResult
        {
            Success = true,
            OutputPath = outputPath,
            ProcessingTimeMs = 300,
            ConversionMethod = "NPOI+CsvHelper"
        };

        _spreadsheetServiceMock
            .Setup(x => x.XlsxToCsvAsync(inputPath, outputPath))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _documentService.ConvertAsync("xlsx-csv", inputPath, outputPath);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.OutputPath.Should().Be(outputPath);
        result.ConversionMethod.Should().Be("NPOI+CsvHelper");

        _spreadsheetServiceMock.Verify(x => x.XlsxToCsvAsync(inputPath, outputPath), Times.Once);
    }

    [Fact]
    public async Task ConvertAsync_CsvToXlsx_UsesSpreadsheetService()
    {
        // Arrange
        var inputPath = CreateTestFile("test.csv", 500);
        var outputPath = Path.Combine(_testDirectory, "output.xlsx");

        var expectedResult = new ConversionResult
        {
            Success = true,
            OutputPath = outputPath,
            ProcessingTimeMs = 250,
            ConversionMethod = "CsvHelper+NPOI"
        };

        _spreadsheetServiceMock
            .Setup(x => x.CsvToXlsxAsync(inputPath, outputPath))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _documentService.ConvertAsync("csv-xlsx", inputPath, outputPath);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.OutputPath.Should().Be(outputPath);
        result.ConversionMethod.Should().Be("CsvHelper+NPOI");

        _spreadsheetServiceMock.Verify(x => x.CsvToXlsxAsync(inputPath, outputPath), Times.Once);
    }

    [Fact]
    public async Task ConvertAsync_PdfToTxt_UsesPdfService()
    {
        // Arrange
        var inputPath = CreateTestFile("test.pdf", 1000);
        var outputPath = Path.Combine(_testDirectory, "output.txt");

        var expectedResult = new ConversionResult
        {
            Success = true,
            OutputPath = outputPath,
            ProcessingTimeMs = 400,
            ConversionMethod = "iText7"
        };

        _pdfServiceMock
            .Setup(x => x.ExtractTextFromPdfAsync(inputPath, outputPath))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _documentService.ConvertAsync("pdf-txt", inputPath, outputPath);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.OutputPath.Should().Be(outputPath);
        result.ConversionMethod.Should().Be("iText7");

        _pdfServiceMock.Verify(x => x.ExtractTextFromPdfAsync(inputPath, outputPath), Times.Once);
    }

    [Fact]
    public async Task ConvertAsync_TxtToPdf_UsesPdfService()
    {
        // Arrange
        var inputPath = CreateTestFile("test.txt", 500);
        var outputPath = Path.Combine(_testDirectory, "output.pdf");

        var expectedResult = new ConversionResult
        {
            Success = true,
            OutputPath = outputPath,
            ProcessingTimeMs = 600,
            ConversionMethod = "PdfKit"
        };

        _pdfServiceMock
            .Setup(x => x.CreatePdfFromTextAsync(inputPath, outputPath))
            .ReturnsAsync(expectedResult);

        // Act
        var result = await _documentService.ConvertAsync("txt-pdf", inputPath, outputPath);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeTrue();
        result.OutputPath.Should().Be(outputPath);
        result.ConversionMethod.Should().Be("PdfKit");

        _pdfServiceMock.Verify(x => x.CreatePdfFromTextAsync(inputPath, outputPath), Times.Once);
    }

    [Fact]
    public async Task ConvertAsync_InvalidConversionKey_ReturnsError()
    {
        // Arrange
        var inputPath = CreateTestFile("test.docx", 1000);
        var outputPath = Path.Combine(_testDirectory, "output.pdf");

        // Act
        var result = await _documentService.ConvertAsync("invalid-conversion", inputPath, outputPath);

        // Assert
        result.Should().NotBeNull();
        result.Success.Should().BeFalse();
        result.Error.Should().Contain("Unsupported conversion");
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
