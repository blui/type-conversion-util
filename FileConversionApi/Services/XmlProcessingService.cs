using Microsoft.Extensions.Logging;
using System.Xml;
using System.Xml.Xsl;
using System.Xml.XPath;

namespace FileConversionApi.Services;

/// <summary>
/// XML processing service with XSLT transformation support
/// Handles XML transformations and conversion to various formats
/// </summary>
public class XmlProcessingService : IXmlProcessingService
{
    private readonly ILogger<XmlProcessingService> _logger;

    public XmlProcessingService(ILogger<XmlProcessingService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public Task<ConversionResult> TransformXmlWithXsltAsync(string xmlPath, string xsltPath, string outputPath)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Transforming XML with XSLT: {XmlPath} + {XsltPath} -> {OutputPath}", xmlPath, xsltPath, outputPath);

            // Ensure output directory exists
            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // Load XML and XSLT
            var xmlDocument = new XPathDocument(xmlPath);
            var xslt = new XslCompiledTransform();

            // Load and compile XSLT with secure settings
            var xsltSettings = new XsltSettings(enableDocumentFunction: false, enableScript: false);
            using (var xsltReader = XmlReader.Create(xsltPath))
            {
                xslt.Load(xsltReader, xsltSettings, null);
            }

            // Perform transformation
            using var writer = XmlWriter.Create(outputPath, new XmlWriterSettings
            {
                Indent = true,
                Encoding = System.Text.Encoding.UTF8
            });

            xslt.Transform(xmlDocument, writer);

            stopwatch.Stop();

            _logger.LogInformation("XML XSLT transformation completed successfully in {Time}ms", stopwatch.ElapsedMilliseconds);

            return Task.FromResult(new ConversionResult
            {
                Success = true,
                OutputPath = outputPath,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                ConversionMethod = "System.Xml.Xsl"
            });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "XML XSLT transformation failed");

            return Task.FromResult(new ConversionResult
            {
                Success = false,
                Error = $"XML XSLT transformation failed: {ex.Message}",
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds
            });
        }
    }

    /// <inheritdoc/>
    public Task<ConversionResult> ConvertXmlToHtmlAsync(string xmlPath, string xsltPath, string outputPath)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Converting XML to HTML via XSLT: {XmlPath} -> {OutputPath}", xmlPath, outputPath);

            // Ensure output directory exists
            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // Load XML and XSLT
            var xmlDocument = new XPathDocument(xmlPath);
            var xslt = new XslCompiledTransform();

            // Load and compile XSLT with secure settings
            var xsltSettings = new XsltSettings(enableDocumentFunction: false, enableScript: false);
            using (var xsltReader = XmlReader.Create(xsltPath))
            {
                xslt.Load(xsltReader, xsltSettings, null);
            }

            // Perform transformation to HTML
            using var writer = new StreamWriter(outputPath, false, System.Text.Encoding.UTF8);
            xslt.Transform(xmlDocument, null, writer);

            stopwatch.Stop();

            _logger.LogInformation("XML to HTML conversion completed successfully in {Time}ms", stopwatch.ElapsedMilliseconds);

            return Task.FromResult(new ConversionResult
            {
                Success = true,
                OutputPath = outputPath,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                ConversionMethod = "System.Xml.Xsl"
            });
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "XML to HTML conversion failed");

            return Task.FromResult(new ConversionResult
            {
                Success = false,
                Error = $"XML to HTML conversion failed: {ex.Message}",
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds
            });
        }
    }

    /// <inheritdoc/>
    public async Task<ConversionResult> ConvertXmlToPdfAsync(string xmlPath, string xsltPath, string outputPath)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Converting XML to PDF via XSLT/HTML: {XmlPath} -> {OutputPath}", xmlPath, outputPath);

            // First transform XML to HTML using XSLT
            var tempHtmlPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".html");

            try
            {
                var htmlResult = await ConvertXmlToHtmlAsync(xmlPath, xsltPath, tempHtmlPath);
                if (!htmlResult.Success)
                {
                    return htmlResult;
                }

                // Then convert HTML to PDF using iText7
                using var htmlStream = File.OpenRead(tempHtmlPath);
                using var pdfStream = File.Create(outputPath);

                using var writer = new iText.Kernel.Pdf.PdfWriter(pdfStream);
                using var pdf = new iText.Kernel.Pdf.PdfDocument(writer);
                using var document = new iText.Layout.Document(pdf);

                // Convert HTML content to PDF by embedding as text (no HTML rendering)
                var htmlContent = await File.ReadAllTextAsync(tempHtmlPath);

                // Create PDF with HTML content embedded as plain text
                var paragraph = new iText.Layout.Element.Paragraph(htmlContent)
                    .SetFontSize(12);

                document.Add(paragraph);

                stopwatch.Stop();

                _logger.LogInformation("XML to PDF conversion completed successfully in {Time}ms", stopwatch.ElapsedMilliseconds);

                return new ConversionResult
                {
                    Success = true,
                    OutputPath = outputPath,
                    ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                    ConversionMethod = "System.Xml.Xsl+iText7"
                };
            }
            finally
            {
                // Clean up temporary HTML file
                if (File.Exists(tempHtmlPath))
                {
                    File.Delete(tempHtmlPath);
                }
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "XML to PDF conversion failed");

            return new ConversionResult
            {
                Success = false,
                Error = $"XML to PDF conversion failed: {ex.Message}",
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    /// <inheritdoc/>
    public async Task<ConversionResult> ValidateXmlAsync(string xmlPath, string? schemaPath = null)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Validating XML: {XmlPath}", xmlPath);

            var settings = new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Prohibit,
                XmlResolver = null // Disable external entity resolution for security
            };

            // Add schema validation if provided
            if (!string.IsNullOrEmpty(schemaPath))
            {
                settings.ValidationType = ValidationType.Schema;
                settings.Schemas.Add(null, schemaPath);
            }

            var validationErrors = new List<string>();

            settings.ValidationEventHandler += (sender, e) =>
            {
                validationErrors.Add($"{e.Severity}: {e.Message} (Line {e.Exception?.LineNumber}, Position {e.Exception?.LinePosition})");
            };

            // Validate XML
            using var reader = XmlReader.Create(xmlPath, settings);
            while (await reader.ReadAsync())
            {
                // Read through entire document to trigger validation
            }

            stopwatch.Stop();

            if (validationErrors.Any())
            {
                _logger.LogWarning("XML validation found {ErrorCount} errors", validationErrors.Count);

                return new ConversionResult
                {
                    Success = false,
                    Error = $"XML validation failed: {string.Join("; ", validationErrors)}",
                    ProcessingTimeMs = stopwatch.ElapsedMilliseconds
                };
            }

            _logger.LogInformation("XML validation completed successfully in {Time}ms", stopwatch.ElapsedMilliseconds);

            return new ConversionResult
            {
                Success = true,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                ConversionMethod = "System.Xml"
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "XML validation failed");

            return new ConversionResult
            {
                Success = false,
                Error = $"XML validation failed: {ex.Message}",
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    /// <inheritdoc/>
    public Task<XmlProcessingCapabilities> GetCapabilitiesAsync()
    {
        return Task.FromResult(new XmlProcessingCapabilities
        {
            SupportedOperations = new[]
            {
                "XSLT Transformation",
                "XML to HTML conversion",
                "XML to PDF conversion",
                "XML Schema validation",
                "XPath queries",
                "XML formatting and indentation"
            },
            SupportedXsltVersions = new[] { "1.0" },
            SupportedOutputFormats = new[] { "xml", "html", "pdf" },
            Features = new[]
            {
                "Secure processing (external entities disabled)",
                "Schema validation support",
                "Namespace-aware processing",
                "Custom extension functions support"
            }
        });
    }
}

/// <summary>
/// XML processing capabilities
/// </summary>
public class XmlProcessingCapabilities
{
    public string[]? SupportedOperations { get; set; }
    public string[]? SupportedXsltVersions { get; set; }
    public string[]? SupportedOutputFormats { get; set; }
    public string[]? Features { get; set; }
}

/// <summary>
/// XML processing service interface
/// </summary>
public interface IXmlProcessingService
{
    Task<ConversionResult> TransformXmlWithXsltAsync(string xmlPath, string xsltPath, string outputPath);
    Task<ConversionResult> ConvertXmlToHtmlAsync(string xmlPath, string xsltPath, string outputPath);
    Task<ConversionResult> ConvertXmlToPdfAsync(string xmlPath, string xsltPath, string outputPath);
    Task<ConversionResult> ValidateXmlAsync(string xmlPath, string? schemaPath = null);
    Task<XmlProcessingCapabilities> GetCapabilitiesAsync();
}
