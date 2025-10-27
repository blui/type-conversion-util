using Microsoft.Extensions.Logging;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace FileConversionApi.Services;

/// <summary>
/// DOCX Pre-Processor for improved LibreOffice compatibility
/// Normalizes formatting to reduce conversion issues
/// </summary>
public class DocxPreProcessor : IDocxPreProcessor
{
    private readonly ILogger<DocxPreProcessor> _logger;

    /// <summary>
    /// Font substitution map - replace problematic fonts with LibreOffice-friendly alternatives
    /// </summary>
    private readonly Dictionary<string, string> _fontMap = new()
    {
        // Office theme fonts -> standard fonts
        {"Calibri Light", "Calibri"},
        {"Segoe UI Light", "Segoe UI"},

        // Custom fonts -> safe alternatives
        {"Aptos", "Calibri"},
        {"Aptos Narrow", "Arial Narrow"},
        {"Arial Narrow", "Arial"},

        // Proprietary fonts -> open equivalents
        {"Times New Roman", "Liberation Serif"},
        {"Arial", "Liberation Sans"},
        {"Courier New", "Liberation Mono"},

        // Keep these safe fonts as-is (LibreOffice handles well)
        {"Calibri", "Calibri"},
        {"Verdana", "Verdana"},
        {"Georgia", "Georgia"},
        {"Tahoma", "Tahoma"},
    };

    /// <summary>
    /// Theme color mappings (Office theme colors to RGB)
    /// </summary>
    private readonly Dictionary<string, string> _themeColorMap = new()
    {
        {"accent1", "4472C4"}, // Blue
        {"accent2", "ED7D31"}, // Orange
        {"accent3", "A5A5A5"}, // Gray
        {"accent4", "FFC000"}, // Yellow
        {"accent5", "5B9BD5"}, // Light Blue
        {"accent6", "70AD47"}, // Green
        {"dark1", "000000"},   // Black
        {"dark2", "44546A"},   // Dark Gray
        {"light1", "FFFFFF"},  // White
        {"light2", "E7E6E6"},  // Light Gray
    };

    public DocxPreProcessor(ILogger<DocxPreProcessor> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc/>
    public async Task<PreprocessingResult> ProcessAsync(string inputPath, string outputPath)
    {
        var fixes = new PreprocessingFixes();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation("Pre-processing DOCX for LibreOffice compatibility: {InputPath}", inputPath);

            // Copy input to output first
            await using (var inputStream = File.OpenRead(inputPath))
            await using (var outputStream = File.Create(outputPath))
            {
                await inputStream.CopyToAsync(outputStream);
            }

            // Process the document
            using var document = WordprocessingDocument.Open(outputPath, true);

            // Process main document
            if (document.MainDocumentPart?.Document != null)
            {
                ProcessDocument(document.MainDocumentPart.Document, fixes);
            }

            // Process styles
            if (document.MainDocumentPart?.StyleDefinitionsPart?.Styles != null)
            {
                ProcessStyles(document.MainDocumentPart.StyleDefinitionsPart.Styles, fixes);
            }

            // Remove page borders from all sections
            if (document.MainDocumentPart?.Document?.Body != null)
            {
                RemovePageBorders(document.MainDocumentPart.Document.Body, fixes);
            }

            document.Save();
            document.Dispose();

            stopwatch.Stop();

            _logger.LogInformation("DOCX pre-processing completed in {Time}ms", stopwatch.ElapsedMilliseconds);
            _logger.LogInformation("Fixes applied: Fonts={Fonts}, Colors={Colors}, Styles={Styles}, Bold={Bold}, PageBorders={PageBorders}",
                fixes.FontsNormalized, fixes.ThemeColorsConverted, fixes.StylesSimplified, fixes.BoldFixed, fixes.PageBordersRemoved);

            return new PreprocessingResult
            {
                Success = true,
                InputPath = inputPath,
                OutputPath = outputPath,
                Fixes = fixes,
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "DOCX pre-processing failed");

            return new PreprocessingResult
            {
                Success = false,
                Error = $"Pre-processing failed: {ex.Message}",
                ProcessingTimeMs = stopwatch.ElapsedMilliseconds
            };
        }
    }

    /// <summary>
    /// Process main document content
    /// </summary>
    private void ProcessDocument(Document document, PreprocessingFixes fixes)
    {
        var body = document.Body;
        if (body == null) return;

        // Process paragraphs
        foreach (var paragraph in body.Descendants<Paragraph>())
        {
            ProcessParagraph(paragraph, fixes);
        }

        // Process tables
        foreach (var table in body.Descendants<Table>())
        {
            ProcessTable(table, fixes);
        }
    }

    /// <summary>
    /// Process paragraph properties and runs
    /// </summary>
    private void ProcessParagraph(Paragraph paragraph, PreprocessingFixes fixes)
    {
        // Process paragraph properties
        if (paragraph.ParagraphProperties != null)
        {
            NormalizeParagraphProperties(paragraph.ParagraphProperties, fixes);
        }

        // Process runs (text segments with formatting)
        foreach (var run in paragraph.Descendants<Run>())
        {
            ProcessRun(run, fixes);
        }
    }

    /// <summary>
    /// Process run properties (font, color, bold, etc.)
    /// </summary>
    private void ProcessRun(Run run, PreprocessingFixes fixes)
    {
        if (run.RunProperties == null) return;

        var runProps = run.RunProperties;

        // Normalize fonts
        if (runProps.RunFonts != null)
        {
            NormalizeFonts(runProps.RunFonts, fixes);
        }

        // Convert theme colors to RGB
        if (runProps.Color != null)
        {
            ConvertThemeColor(runProps.Color, fixes);
        }

        // Fix bold formatting
        if (runProps.Bold != null || runProps.BoldComplexScript != null)
        {
            FixBoldFormatting(runProps, fixes);
        }
    }

    /// <summary>
    /// Process table formatting
    /// </summary>
    private void ProcessTable(Table table, PreprocessingFixes fixes)
    {
        // Process table properties
        var tableProps = table.GetFirstChild<TableProperties>();
        if (tableProps != null)
        {
            // Could add table property normalization here if needed
        }

        // Process table rows and cells
        foreach (var row in table.Descendants<TableRow>())
        {
            foreach (var cell in row.Descendants<TableCell>())
            {
                foreach (var paragraph in cell.Descendants<Paragraph>())
                {
                    ProcessParagraph(paragraph, fixes);
                }
            }
        }
    }

    /// <summary>
    /// Process document styles
    /// </summary>
    private void ProcessStyles(Styles styles, PreprocessingFixes fixes)
    {
        foreach (var style in styles.Descendants<Style>())
        {
            if (style.StyleRunProperties != null)
            {
                // Process style-level formatting similar to run properties
                var styleRunProps = style.StyleRunProperties;

                if (styleRunProps.RunFonts != null)
                {
                    NormalizeFonts(styleRunProps.RunFonts, fixes);
                }

                if (styleRunProps.Color != null)
                {
                    ConvertThemeColor(styleRunProps.Color, fixes);
                }

                if (styleRunProps.Bold != null)
                {
                    FixBoldFormatting(styleRunProps, fixes);
                }
            }
        }
    }

    /// <summary>
    /// Normalize paragraph properties for better compatibility
    /// </summary>
    private void NormalizeParagraphProperties(ParagraphProperties paraProps, PreprocessingFixes fixes)
    {
        // Could add paragraph spacing normalization here if needed
        // For now, we focus on the most critical formatting issues
    }

    /// <summary>
    /// Normalize font references
    /// </summary>
    private void NormalizeFonts(RunFonts runFonts, PreprocessingFixes fixes)
    {
        var properties = runFonts.GetType().GetProperties();

        foreach (var prop in properties)
        {
            if (prop.PropertyType == typeof(string) && prop.Name.Contains("Theme"))
            {
                // Remove theme font references (force explicit fonts)
                prop.SetValue(runFonts, null);
                continue;
            }

            var fontName = prop.GetValue(runFonts) as string;
            if (!string.IsNullOrEmpty(fontName) && _fontMap.TryGetValue(fontName, out var replacement))
            {
                if (fontName != replacement)
                {
                    prop.SetValue(runFonts, replacement);
                    fixes.FontsNormalized++;
                }
            }
        }
    }

    /// <summary>
    /// Convert theme colors to explicit RGB values
    /// </summary>
    private void ConvertThemeColor(Color color, PreprocessingFixes fixes)
    {
        var themeColor = color.ThemeColor?.ToString();
        if (!string.IsNullOrEmpty(themeColor) && _themeColorMap.TryGetValue(themeColor, out var rgbColor))
        {
            color.Val = rgbColor;
            color.ThemeColor = null; // Remove theme reference
            fixes.ThemeColorsConverted++;
        }

        // Convert AUTO color to black
        if (color.Val?.Value == "AUTO")
        {
            color.Val = "000000";
        }
    }

    /// <summary>
    /// Fix bold formatting for consistency
    /// </summary>
    private void FixBoldFormatting(object runProps, PreprocessingFixes fixes)
    {
        var propsType = runProps.GetType();
        var boldProp = propsType.GetProperty("Bold");
        var boldCsProp = propsType.GetProperty("BoldComplexScript");

        // Fix regular bold
        if (boldProp != null)
        {
            var bold = boldProp.GetValue(runProps) as Bold;
            if (bold != null && bold.Val == null)
            {
                bold.Val = new DocumentFormat.OpenXml.OnOffValue(true);
                fixes.BoldFixed++;
            }
        }

        // Fix complex script bold
        if (boldCsProp != null)
        {
            var boldCs = boldCsProp.GetValue(runProps) as BoldComplexScript;
            if (boldCs != null && boldCs.Val == null)
            {
                boldCs.Val = new DocumentFormat.OpenXml.OnOffValue(true);
                fixes.BoldFixed++;
            }
        }
    }

    /// <summary>
    /// Remove page borders from all sections
    /// Fixes LibreOffice PDF conversion adding unwanted borders
    /// </summary>
    private void RemovePageBorders(Body body, PreprocessingFixes fixes)
    {
        // Find all section properties in the document
        var sectionProps = body.Descendants<SectionProperties>();

        foreach (var sectionProp in sectionProps)
        {
            // Find and remove PageBorders element if it exists
            var pageBorders = sectionProp.GetFirstChild<PageBorders>();
            if (pageBorders != null)
            {
                pageBorders.Remove();
                fixes.PageBordersRemoved++;
                _logger.LogDebug("Removed page border from section");
            }
        }
    }
}

/// <summary>
/// Preprocessing result
/// </summary>
public class PreprocessingResult
{
    public bool Success { get; set; }
    public string? InputPath { get; set; }
    public string? OutputPath { get; set; }
    public PreprocessingFixes? Fixes { get; set; }
    public long ProcessingTimeMs { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Track what fixes were applied during preprocessing
/// </summary>
public class PreprocessingFixes
{
    public int FontsNormalized { get; set; }
    public int ThemeColorsConverted { get; set; }
    public int StylesSimplified { get; set; }
    public int ParagraphsAdjusted { get; set; }
    public int BoldFixed { get; set; }
    public int PageBordersRemoved { get; set; }
}

/// <summary>
/// DOCX preprocessor interface
/// </summary>
public interface IDocxPreProcessor
{
    Task<PreprocessingResult> ProcessAsync(string inputPath, string outputPath);
}
