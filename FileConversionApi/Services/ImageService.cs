using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Processing;

namespace FileConversionApi.Services;

/// <summary>
/// Image processing service implementation
/// Handles image format conversions and PDF generation from images
/// </summary>
public class ImageService : IImageService
{
    private readonly ILogger<ImageService> _logger;

    public ImageService(ILogger<ImageService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<ConversionResult> ConvertToPdfAsync(string inputPath, string outputPath, string inputFormat)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Converting image to PDF: {InputPath} -> {OutputPath}",
                inputPath, outputPath);

            // Ensure output directory exists
            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // Load image
            using var image = await Image.LoadAsync(inputPath);

            // Create PDF with the image
            await using var stream = File.Create(outputPath);
            using var writer = new iText.Kernel.Pdf.PdfWriter(stream);
            using var pdf = new iText.Kernel.Pdf.PdfDocument(writer);
            using var document = new iText.Layout.Document(pdf);

            // Convert image to byte array for PDF embedding
            using var imageStream = new MemoryStream();
            await image.SaveAsJpegAsync(imageStream);
            imageStream.Position = 0;

            var imageData = iText.IO.Image.ImageDataFactory.Create(imageStream.ToArray());
            var pdfImage = new iText.Layout.Element.Image(imageData);

            // Scale image to fit page if too large
            var pageSize = pdf.GetDefaultPageSize();
            if (image.Width > pageSize.GetWidth() || image.Height > pageSize.GetHeight())
            {
                pdfImage.ScaleToFit(pageSize.GetWidth() - 50, pageSize.GetHeight() - 50);
            }

            document.Add(pdfImage);
            document.Close();

            stopwatch.Stop();

            _logger.LogInformation("Image to PDF conversion completed successfully in {Time}ms",
                stopwatch.ElapsedMilliseconds);

            return new ConversionResult
            {
                Success = true,
                OutputPath = outputPath,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                ConversionMethod = "ImageSharp+iText7"
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Image to PDF conversion failed");

            return new ConversionResult
            {
                Success = false,
                Error = $"Image conversion failed: {ex.Message}",
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    /// <inheritdoc/>
    public async Task<ConversionResult> ConvertFormatAsync(string inputPath, string outputPath, string inputFormat, string targetFormat)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Converting image format: {InputFormat} to {TargetFormat}",
                inputFormat, targetFormat);

            // Ensure output directory exists
            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // Load image
            using var image = await Image.LoadAsync(inputPath);

            // Configure encoder based on target format
            IImageEncoder encoder = targetFormat.ToLowerInvariant() switch
            {
                "jpg" or "jpeg" => new JpegEncoder { Quality = 90 },
                "png" => new PngEncoder { CompressionLevel = PngCompressionLevel.Level6 },
                "bmp" => new BmpEncoder(),
                "tiff" or "tif" => new TiffEncoder(),
                _ => throw new NotSupportedException($"Target format '{targetFormat}' is not supported")
            };

            // Save with appropriate encoder
            await image.SaveAsync(outputPath, encoder);

            stopwatch.Stop();

            _logger.LogInformation("Image format conversion completed successfully in {Time}ms",
                stopwatch.ElapsedMilliseconds);

            return new ConversionResult
            {
                Success = true,
                OutputPath = outputPath,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                ConversionMethod = "ImageSharp"
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Image format conversion failed");

            return new ConversionResult
            {
                Success = false,
                Error = $"Format conversion failed: {ex.Message}",
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
    }
}
