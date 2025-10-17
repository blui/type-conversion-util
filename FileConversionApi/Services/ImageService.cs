using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Formats.Bmp;
using SixLabors.ImageSharp.Formats.Tiff;
using SixLabors.ImageSharp.Processing;
using ImageMagick;
using Svg;

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
    public async Task<ConversionResult> ConvertPsdToImageAsync(string inputPath, string outputPath, string targetFormat = "png")
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Converting PSD to image: {InputPath} -> {OutputPath}", inputPath, outputPath);

            // Ensure output directory exists
            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // Use ImageMagick for PSD processing (SixLabors doesn't support PSD natively)
            using var image = new MagickImage(inputPath);

            // Flatten layers if it's a multi-layer PSD
            // Note: Magick.NET automatically handles layers, no explicit flatten needed for basic conversion

            // Set format based on target
            var magickFormat = targetFormat.ToLowerInvariant() switch
            {
                "jpg" or "jpeg" => MagickFormat.Jpeg,
                "png" => MagickFormat.Png,
                "bmp" => MagickFormat.Bmp,
                "tiff" or "tif" => MagickFormat.Tiff,
                _ => MagickFormat.Png
            };

            image.Format = magickFormat;

            // Save the image
            await image.WriteAsync(outputPath);

            stopwatch.Stop();

            _logger.LogInformation("PSD to image conversion completed successfully in {Time}ms", stopwatch.ElapsedMilliseconds);

            return new ConversionResult
            {
                Success = true,
                OutputPath = outputPath,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                ConversionMethod = "ImageMagick"
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "PSD conversion failed");

            return new ConversionResult
            {
                Success = false,
                Error = $"PSD conversion failed: {ex.Message}",
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    /// <inheritdoc/>
    public async Task<ConversionResult> ConvertSvgToImageAsync(string inputPath, string outputPath, string targetFormat = "png")
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Converting SVG to image: {InputPath} -> {OutputPath}", inputPath, outputPath);

            // Ensure output directory exists
            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // Load SVG content
            var svgContent = await File.ReadAllTextAsync(inputPath);

            // Convert SVG to image using Svg library
            var svgDocument = SvgDocument.FromSvg<SvgDocument>(svgContent);
            using var bitmap = svgDocument.Draw();

            // Convert bitmap to byte array
            var bitmapData = bitmap.LockBits(
                new System.Drawing.Rectangle(0, 0, bitmap.Width, bitmap.Height),
                System.Drawing.Imaging.ImageLockMode.ReadOnly,
                bitmap.PixelFormat);

            try
            {
                var length = bitmapData.Stride * bitmapData.Height;
                var pixelData = new byte[length];
                System.Runtime.InteropServices.Marshal.Copy(bitmapData.Scan0, pixelData, 0, length);

                // Convert to SixLabors Image for saving
                using var image = Image.LoadPixelData<SixLabors.ImageSharp.PixelFormats.Bgra32>(
                    pixelData,
                    bitmap.Width,
                    bitmap.Height);

                // Configure encoder based on target format
                IImageEncoder encoder = targetFormat.ToLowerInvariant() switch
                {
                    "jpg" or "jpeg" => new JpegEncoder { Quality = 90 },
                    "png" => new PngEncoder { CompressionLevel = PngCompressionLevel.Level6 },
                    "bmp" => new BmpEncoder(),
                    "tiff" or "tif" => new TiffEncoder(),
                    _ => new PngEncoder()
                };

                await image.SaveAsync(outputPath, encoder);
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
            }

            stopwatch.Stop();

            _logger.LogInformation("SVG to image conversion completed successfully in {Time}ms", stopwatch.ElapsedMilliseconds);

            return new ConversionResult
            {
                Success = true,
                OutputPath = outputPath,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                ConversionMethod = "Svg+ImageSharp"
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "SVG conversion failed");

            return new ConversionResult
            {
                Success = false,
                Error = $"SVG conversion failed: {ex.Message}",
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    /// <inheritdoc/>
    public async Task<ConversionResult> ConvertMultiPageTiffAsync(string inputPath, string outputPath, int pageIndex = 0)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Extracting page {PageIndex} from multi-page TIFF: {InputPath} -> {OutputPath}",
                pageIndex, inputPath, outputPath);

            // Ensure output directory exists
            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // Use ImageMagick for multi-page TIFF processing
            using var images = new MagickImageCollection(inputPath);

            if (pageIndex >= images.Count)
            {
                return new ConversionResult
                {
                    Success = false,
                    Error = $"Page index {pageIndex} is out of range. TIFF has {images.Count} pages.",
                    ProcessingTimeMs = stopwatch.ElapsedMilliseconds
                };
            }

            // Extract the specified page
            using var pageImage = (MagickImage)images[pageIndex].Clone();

            // Save as PNG (or other format as needed)
            pageImage.Format = MagickFormat.Png;
            await pageImage.WriteAsync(outputPath);

            stopwatch.Stop();

            _logger.LogInformation("Multi-page TIFF extraction completed successfully in {Time}ms", stopwatch.ElapsedMilliseconds);

            return new ConversionResult
            {
                Success = true,
                OutputPath = outputPath,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                ConversionMethod = "ImageMagick",
                AdditionalInfo = $"Extracted page {pageIndex} from {images.Count} total pages"
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Multi-page TIFF processing failed");

            return new ConversionResult
            {
                Success = false,
                Error = $"Multi-page TIFF processing failed: {ex.Message}",
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    /// <inheritdoc/>
    public async Task<ConversionResult> ConvertTiffToPdfAsync(string inputPath, string outputPath)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Converting multi-page TIFF to PDF: {InputPath} -> {OutputPath}", inputPath, outputPath);

            // Ensure output directory exists
            var outputDir = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDir) && !Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
            }

            // Load multi-page TIFF
            using var images = new MagickImageCollection(inputPath);

            if (images.Count == 0)
            {
                return new ConversionResult
                {
                    Success = false,
                    Error = "TIFF file contains no pages",
                    ProcessingTimeMs = stopwatch.ElapsedMilliseconds
                };
            }

            // Create PDF from all pages
            using var pdfStream = File.Create(outputPath);
            using var writer = new iText.Kernel.Pdf.PdfWriter(pdfStream);
            using var pdf = new iText.Kernel.Pdf.PdfDocument(writer);
            using var document = new iText.Layout.Document(pdf);

            for (int i = 0; i < images.Count; i++)
            {
                using var pageStream = new MemoryStream();
                var pageImage = (MagickImage)images[i].Clone();
                pageImage.Format = MagickFormat.Jpeg; // Convert to JPEG for PDF embedding
                await pageImage.WriteAsync(pageStream);
                pageStream.Position = 0;

                var imageData = iText.IO.Image.ImageDataFactory.Create(pageStream.ToArray());
                var pdfImage = new iText.Layout.Element.Image(imageData);

                // Scale image to fit page
                var pageSize = pdf.GetDefaultPageSize();
                pdfImage.ScaleToFit(pageSize.GetWidth() - 50, pageSize.GetHeight() - 50);

                document.Add(pdfImage);

                // Add page break for all but the last page
                if (i < images.Count - 1)
                {
                    document.Add(new iText.Layout.Element.AreaBreak());
                }
            }

            document.Close();

            stopwatch.Stop();

            _logger.LogInformation("Multi-page TIFF to PDF conversion completed successfully in {Time}ms. Pages: {PageCount}",
                stopwatch.ElapsedMilliseconds, images.Count);

            return new ConversionResult
            {
                Success = true,
                OutputPath = outputPath,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds,
                ConversionMethod = "ImageMagick+iText7",
                AdditionalInfo = $"{images.Count} pages converted to PDF"
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "TIFF to PDF conversion failed");

            return new ConversionResult
            {
                Success = false,
                Error = $"TIFF to PDF conversion failed: {ex.Message}",
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
