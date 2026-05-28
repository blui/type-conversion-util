using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using FileConversionApi.Models;
using Microsoft.Extensions.Logging;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace FileConversionApi.Services;

/// <summary>
/// Reverses pipeline-output HTML (see <see cref="PipelineOutputHtmlExtractor"/>) back to a
/// DOCX by emitting one OOXML section per <c>.page</c> div. Each section's page size is
/// computed from the source raster's pixel dimensions (forward pipeline RENDER_SCALE is
/// 2.0, so 1 CSS pixel = 0.5 PDF points = 10 OOXML twips), margins are zero, the page
/// image is embedded inline at full bleed, and the text-layer content is emitted ahead of
/// it as a hidden run (<c>w:vanish</c>) so Word's find/replace indexes it without showing
/// it in print output.
///
/// The visible content of every page is the rastered image itself, which is bit-identical
/// to what the forward pipeline already showed inside the HTML viewer; round-trip fidelity
/// of <c>docx-&gt;html-&gt;docx</c> is therefore "the same raster the browser was
/// displaying" rather than a reconstruction.
/// </summary>
internal static class PipelineOutputHtmlToDocxRenderer
{
    // Raster -> OOXML conversions. The forward emitter renders at RENDER_SCALE 2.0, so the
    // PNG's pixel dimensions are twice the source PDF's points. 1 PDF point = 20 twips and
    // 1 PDF point = 12700 EMU, so:
    //   widthTwips = widthPx / 2 * 20 = widthPx * 10
    //   widthEmu   = widthPx / 2 * 12700 = widthPx * 6350
    private const long TwipsPerRasterPixel = 10;
    private const long EmuPerRasterPixel = 6350;

    public static ConversionResult Render(
        IReadOnlyList<PipelineOutputHtmlPage> pages,
        string outputPath,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pages);
        ArgumentNullException.ThrowIfNull(outputPath);
        ArgumentNullException.ThrowIfNull(logger);

        cancellationToken.ThrowIfCancellationRequested();

        using var stream = File.Create(outputPath);
        using var package = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document);

        var mainPart = package.AddMainDocumentPart();
        var body = new Body();
        var document = new Document(body);

        for (int i = 0; i < pages.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AddPage(mainPart, body, pages[i], pageNumber: i + 1, isLastPage: i == pages.Count - 1);
        }

        document.Save(mainPart);

        return new ConversionResult
        {
            Success = true,
            OutputPath = outputPath,
            ConversionMethod = "PipelineOutputHtmlToDocxRenderer"
        };
    }

    private static void AddPage(
        MainDocumentPart mainPart,
        Body body,
        PipelineOutputHtmlPage page,
        int pageNumber,
        bool isLastPage)
    {
        var imagePart = mainPart.AddImagePart(ImagePartType.Png);
        using (var imageStream = new MemoryStream(page.PngBytes))
        {
            imagePart.FeedData(imageStream);
        }
        var relationshipId = mainPart.GetIdOfPart(imagePart);

        var hiddenText = ConcatenateSpanText(page.Spans);
        if (hiddenText.Length > 0)
        {
            body.AppendChild(BuildHiddenTextParagraph(hiddenText));
        }

        var emuWidth = page.WidthPx * EmuPerRasterPixel;
        var emuHeight = page.HeightPx * EmuPerRasterPixel;
        body.AppendChild(BuildPictureParagraph(relationshipId, emuWidth, emuHeight, pageNumber));

        var twipsWidth = page.WidthPx * TwipsPerRasterPixel;
        var twipsHeight = page.HeightPx * TwipsPerRasterPixel;
        var sectionProperties = BuildSectionProperties(twipsWidth, twipsHeight);

        if (isLastPage)
        {
            // OOXML places the document's final section's properties at body level (not
            // wrapped in a trailing paragraph), so do not emit a section-break paragraph
            // after the last page.
            body.AppendChild(sectionProperties);
        }
        else
        {
            var sectionBreak = new Paragraph(new ParagraphProperties(sectionProperties));
            body.AppendChild(sectionBreak);
        }
    }

    private static Paragraph BuildHiddenTextParagraph(string text)
    {
        var run = new Run(
            new RunProperties(new Vanish()),
            new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        return new Paragraph(run);
    }

    private static Paragraph BuildPictureParagraph(string relationshipId, long emuWidth, long emuHeight, int pageNumber)
    {
        // The docPr / cNvPr IDs are required-unique per OOXML; using the page number keeps
        // them stable and trivially unique across the whole document.
        var docPropertyId = (UInt32Value)(uint)pageNumber;
        var pictureName = $"Page{pageNumber}";

        var graphic = new A.Graphic(
            new A.GraphicData(
                new PIC.Picture(
                    new PIC.NonVisualPictureProperties(
                        new PIC.NonVisualDrawingProperties { Id = docPropertyId, Name = pictureName },
                        new PIC.NonVisualPictureDrawingProperties()),
                    new PIC.BlipFill(
                        new A.Blip { Embed = relationshipId },
                        new A.Stretch(new A.FillRectangle())),
                    new PIC.ShapeProperties(
                        new A.Transform2D(
                            new A.Offset { X = 0L, Y = 0L },
                            new A.Extents { Cx = emuWidth, Cy = emuHeight }),
                        new A.PresetGeometry(new A.AdjustValueList())
                        {
                            Preset = A.ShapeTypeValues.Rectangle
                        })))
            {
                Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture"
            });

        var inline = new DW.Inline(
            new DW.Extent { Cx = emuWidth, Cy = emuHeight },
            new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
            new DW.DocProperties { Id = docPropertyId, Name = pictureName },
            new DW.NonVisualGraphicFrameDrawingProperties(
                new A.GraphicFrameLocks { NoChangeAspect = true }),
            graphic)
        {
            DistanceFromTop = 0U,
            DistanceFromBottom = 0U,
            DistanceFromLeft = 0U,
            DistanceFromRight = 0U
        };

        var drawing = new Drawing(inline);
        return new Paragraph(new Run(drawing));
    }

    private static SectionProperties BuildSectionProperties(long twipsWidth, long twipsHeight)
    {
        return new SectionProperties(
            new PageSize { Width = (UInt32Value)(uint)twipsWidth, Height = (UInt32Value)(uint)twipsHeight },
            new PageMargin
            {
                Top = 0,
                Right = 0U,
                Bottom = 0,
                Left = 0U,
                Header = 0U,
                Footer = 0U,
                Gutter = 0U
            });
    }

    private static string ConcatenateSpanText(IReadOnlyList<PipelineOutputHtmlSpan> spans)
    {
        // The pdf.js text layer separates words via absolute positioning, not whitespace, so
        // joining the raw span text with a single space restores word boundaries for Word's
        // find/replace. The run is invisible, so the spacing is search-only with no visual effect.
        return string.Join(' ', spans.Select(span => span.Text)).Trim();
    }
}
