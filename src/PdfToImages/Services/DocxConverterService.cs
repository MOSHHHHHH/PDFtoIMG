using System.IO;
using System.IO.Packaging;
using DocumentFormat.OpenXml;
using PDFtoImage;
using PdfToImages.Models;
using SkiaSharp;
using W = DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;

namespace PdfToImages.Services;

/// <summary>Image-only Word export. Raster and XML memory are bounded to one page.
/// The destination is published only after the entire package has closed successfully.</summary>
public sealed class DocxConverterService
{
    public Task<ConversionResult> ConvertAsync(ConversionJob job, ConversionSettings settings,
        IProgress<int> progress, CancellationToken cancellationToken) =>
        Task.Run(() => Convert(job, settings, progress, cancellationToken), cancellationToken);

    private static ConversionResult Convert(ConversionJob job, ConversionSettings settings,
        IProgress<int> progress, CancellationToken cancellationToken)
    {
        string? temporaryPath = null;
        try
        {
            if (FileNamingService.HasInvalidChars(job.BookName))
                throw new ArgumentException("שם המסמך אינו תקין לשמירה ב־Windows.");
            if (settings.Dpi is < 72 or > 1200)
                throw new ArgumentException("הרזולוציה חייבת להיות בטווח 72–1200 DPI.");

            // Keep a single read handle for a stable source snapshot, without loading the PDF into RAM.
            using var source = File.OpenRead(job.SourcePath);
            int count = Conversion.GetPageCount(source, leaveOpen: true);
            if (count <= 0) throw new InvalidDataException("הקובץ אינו מכיל עמודים.");
            Directory.CreateDirectory(settings.OutputRoot);
            temporaryPath = Path.Combine(settings.OutputRoot, $".pdftoimg-{Guid.NewGuid():N}.tmp");
            // Write-only OPC/ZIP mode streams image data to disk. Read/write ZIP mode
            // buffers entries and can retain all compressed images in a long document.
            // XML is spooled separately so ZIP entries are never open concurrently.
            using (var xml = new FileStream(temporaryPath + ".xml", FileMode.CreateNew,
                       FileAccess.ReadWrite, FileShare.None, 64 * 1024, FileOptions.DeleteOnClose))
            using (var container = Package.Open(temporaryPath, FileMode.CreateNew, FileAccess.Write))
            {
                // Stream XML directly instead of keeping a growing document tree in memory.
                using (var writer = OpenXmlWriter.Create(xml))
                {
                    writer.WriteStartElement(new W.Document());
                    writer.WriteStartElement(new W.Body());
                    for (int page = 0; page < count; page++)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        source.Position = 0;
                        var size = Conversion.GetPageSize(source, new Index(page), leaveOpen: true);
                        double width = size.Width, height = size.Height;
                        if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0 || height <= 0)
                            throw new InvalidDataException($"מידות לא תקינות בעמוד {page + 1}.");
                        // Avoid a single enormous page exhausting the process. Never silently lower DPI.
                        if (Math.Ceiling(width * settings.Dpi / 72) * Math.Ceiling(height * settings.Dpi / 72) > 80_000_000)
                            throw new InvalidDataException($"עמוד {page + 1} גדול מדי ברזולוציה זו. בחרו DPI נמוך יותר.");

                        source.Position = 0;
                        using (var bitmap = Conversion.ToImage(source, new Index(page), leaveOpen: true,
                                   options: new RenderOptions(Dpi: settings.Dpi)))
                        using (var image = bitmap.Encode(SKEncodedImageFormat.Png, 100)
                                   ?? throw new IOException("קידוד תמונת העמוד נכשל."))
                        using (var imageStream = image.AsStream())
                        {
                            var imageUri = new Uri($"/word/media/page{page + 1}.png", UriKind.Relative);
                            var part = container.CreatePart(imageUri, "image/png", CompressionOption.Normal);
                            using (var target = part.GetStream(FileMode.Create, FileAccess.Write)) imageStream.CopyTo(target);
                            string relationship = $"rId{page + 1}";
                            var section = CreateSection(width, height);
                            long pageWidth = section.GetFirstChild<W.PageSize>()!.Width!.Value * 635L;
                            long pageHeight = section.GetFirstChild<W.PageSize>()!.Height!.Value * 635L;
                            double scale = Math.Min(pageWidth / width, pageHeight / height);
                            long cx = Math.Max(1, (long)Math.Round(width * scale));
                            long cy = Math.Max(1, (long)Math.Round(height * scale));
                            // Anchor to the page, not the text line: a full-height inline image would
                            // push the paragraph mark onto a blank page in Word. The only paragraph
                            // occupies one twip. Each non-final paragraph ends its own next-page section.
                            var properties = new W.ParagraphProperties(
                                new W.SpacingBetweenLines { Before = "0", After = "0", Line = "1", LineRule = W.LineSpacingRuleValues.Exact },
                                new W.ParagraphMarkRunProperties(new W.FontSize { Val = "2" }));
                            if (page < count - 1) properties.Append(section);
                            writer.WriteElement(new W.Paragraph(properties,
                                new W.Run(new W.Drawing(CreateDrawing(relationship, (uint)page + 1, cx, cy)))));
                            if (page == count - 1) writer.WriteElement(section);
                        }
                        progress.Report(page + 1);
                    }
                    writer.WriteEndElement(); // body
                    writer.WriteEndElement(); // document
                }
                var main = container.CreatePart(new Uri("/word/document.xml", UriKind.Relative),
                    "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml", CompressionOption.Normal);
                container.CreateRelationship(main.Uri, TargetMode.Internal,
                    "http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument");
                for (int page = 1; page <= count; page++)
                    main.CreateRelationship(new Uri($"media/page{page}.png", UriKind.Relative), TargetMode.Internal,
                        "http://schemas.openxmlformats.org/officeDocument/2006/relationships/image", $"rId{page}");
                xml.Position = 0;
                using var documentStream = main.GetStream(FileMode.Create, FileAccess.Write);
                xml.CopyTo(documentStream);
            }
            cancellationToken.ThrowIfCancellationRequested();
            // Never replace user files. File.Move is the final atomic collision check, including
            // another process creating the same name while conversion was running.
            string destination = PublishUnique(temporaryPath, settings.OutputRoot, job.BookName);
            temporaryPath = null;
            return new ConversionResult { Success = true, PagesWritten = count, OutputPath = destination };
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return new ConversionResult
            {
                Success = false, PagesWritten = 0,
                ErrorMessage = $"לא ניתן ליצור מסמך Word (ייתכן שה־PDF פגום, מוצפן או שהיעד אינו זמין): {ex.Message}"
            };
        }
        finally
        {
            if (temporaryPath != null)
            {
                try { File.Delete(temporaryPath); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }
    }

    private static string PublishUnique(string temporary, string root, string name)
    {
        for (int suffix = 1; ; suffix++)
        {
            var destination = Path.Combine(root, suffix == 1 ? $"{name}.docx" : $"{name} ({suffix}).docx");
            try { File.Move(temporary, destination); return destination; }
            catch (IOException) when (File.Exists(destination) || Directory.Exists(destination)) { }
        }
    }

    private static W.SectionProperties CreateSection(double widthPoints, double heightPoints)
    {
        // Word's maximum paper dimension is 22 inches. Scale oversized pages uniformly;
        // very narrow dimensions use the minimum canvas while the image keeps its aspect ratio.
        double scale = Math.Min(1, 1584 / Math.Max(widthPoints, heightPoints));
        uint width = (uint)Math.Clamp(Math.Round(widthPoints * 20 * scale), 144, 31680);
        uint height = (uint)Math.Clamp(Math.Round(heightPoints * 20 * scale), 144, 31680);
        return new W.SectionProperties(
            new W.SectionType { Val = W.SectionMarkValues.NextPage },
            new W.PageSize { Width = width, Height = height,
                Orient = width > height ? W.PageOrientationValues.Landscape : W.PageOrientationValues.Portrait },
            new W.PageMargin { Top = 0, Right = 0, Bottom = 0, Left = 0, Header = 0, Footer = 0, Gutter = 0 });
    }

    private static DW.Anchor CreateDrawing(string relationship, uint id, long cx, long cy)
    {
        var picture = new PIC.Picture(
            new PIC.NonVisualPictureProperties(
                new PIC.NonVisualDrawingProperties { Id = id, Name = $"עמוד {id}" },
                new PIC.NonVisualPictureDrawingProperties(new A.PictureLocks { NoChangeAspect = true })),
            new PIC.BlipFill(new A.Blip { Embed = relationship },
                new A.Stretch(new A.FillRectangle())),
            new PIC.ShapeProperties(
                new A.Transform2D(new A.Offset { X = 0, Y = 0 }, new A.Extents { Cx = cx, Cy = cy }),
                new A.PresetGeometry(new A.AdjustValueList()) { Preset = A.ShapeTypeValues.Rectangle }));
        return new DW.Anchor(
            new DW.SimplePosition { X = 0, Y = 0 },
            new DW.HorizontalPosition(new DW.PositionOffset("0")) { RelativeFrom = DW.HorizontalRelativePositionValues.Page },
            new DW.VerticalPosition(new DW.PositionOffset("0")) { RelativeFrom = DW.VerticalRelativePositionValues.Page },
            new DW.Extent { Cx = cx, Cy = cy },
            new DW.EffectExtent { LeftEdge = 0, TopEdge = 0, RightEdge = 0, BottomEdge = 0 },
            new DW.WrapNone(),
            new DW.DocProperties { Id = id, Name = $"עמוד PDF {id}" },
            new DW.NonVisualGraphicFrameDrawingProperties(new A.GraphicFrameLocks { NoChangeAspect = true }),
            new A.Graphic(new A.GraphicData(picture) { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
        {
            DistanceFromTop = 0, DistanceFromBottom = 0, DistanceFromLeft = 0, DistanceFromRight = 0,
            SimplePos = false, RelativeHeight = 0, BehindDoc = true, Locked = true,
            LayoutInCell = true, AllowOverlap = true
        };
    }
}
