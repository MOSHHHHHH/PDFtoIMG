using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Validation;
using PDFtoImage;
using PdfToImages.Models;
using PdfToImages.Services;
using SkiaSharp;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;

namespace PdfToImages.Tests;

public sealed class ConversionTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "pdftoimg-tests-" + Guid.NewGuid().ToString("N"));
    public ConversionTests() => Directory.CreateDirectory(_root);
    public void Dispose() => Directory.Delete(_root, true);
    private ConversionSettings Settings(string folder = "out", int dpi = 200) => new()
    { Dpi = dpi, OutputRoot = Path.Combine(_root, folder) };
    private sealed class PageProgress : IProgress<int>
    {
        public List<int> Pages { get; } = new();
        public Action<int>? OnPage { get; init; }
        public void Report(int value) { Pages.Add(value); OnPage?.Invoke(value); }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(3)]
    [InlineData(12)]
    [InlineData(125)]
    public async Task DocxPreservesEveryPageAndEmbedsExactRenderedPixels(int count)
    {
        var dimensions = Enumerable.Range(0, count).Select(i => (i % 3) switch
        {
            0 => (240, 360, 0), 1 => (360, 240, 0), _ => (240, 360, 90)
        }).ToArray();
        string source = PdfFixture.Create(_root, "ספר", dimensions);
        var job = new ConversionJob(source);
        var progress = new PageProgress();
        var result = await new DocxConverterService().ConvertAsync(job, Settings(), progress, default);
        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(count, result.PagesWritten);
        Assert.Equal(Enumerable.Range(1, count), progress.Pages);
        Assert.Empty(Directory.GetFiles(Settings().OutputRoot, "*.tmp"));
        string? artifacts = Environment.GetEnvironmentVariable("PDFTOIMG_TEST_ARTIFACTS");
        if (!string.IsNullOrWhiteSpace(artifacts))
        {
            Directory.CreateDirectory(artifacts);
            File.Copy(source, Path.Combine(artifacts, $"pages-{count}.pdf"), true);
            File.Copy(result.OutputPath!, Path.Combine(artifacts, $"pages-{count}.docx"), true);
        }
        using var doc = WordprocessingDocument.Open(result.OutputPath!, false);
        var main = doc.MainDocumentPart!;
        var errors = new OpenXmlValidator().Validate(doc).ToArray();
        Assert.True(errors.Length == 0, string.Join("\n", errors.Select(e => e.Description)));
        Assert.Equal(count, main.ImageParts.Count());
        Assert.Empty(main.ExternalRelationships);
        var body = main.Document.Body!;
        var paragraphs = body.Elements<W.Paragraph>().ToArray();
        var sections = body.Descendants<W.SectionProperties>().ToArray();
        Assert.Equal(count, paragraphs.Length);
        Assert.Equal(count, sections.Length);
        Assert.IsType<W.SectionProperties>(body.LastChild);
        Assert.Empty(body.Descendants<W.Break>());
        Assert.Empty(body.Descendants<W.Text>()); // no OCR, hidden text, or editable PDF content
        Assert.Equal(count - 1, paragraphs.Count(p => p.ParagraphProperties?.GetFirstChild<W.SectionProperties>() != null));
        for (int page = 0; page < count; page++)
        {
            using var input = File.OpenRead(source);
            var size = Conversion.GetPageSize(input, new Index(page), leaveOpen: true);
            var pageSize = sections[page].GetFirstChild<W.PageSize>()!;
            Assert.InRange(Math.Abs(pageSize.Width!.Value - size.Width * 20), 0, 1);
            Assert.InRange(Math.Abs(pageSize.Height!.Value - size.Height * 20), 0, 1);
            Assert.Equal(size.Width > size.Height ? W.PageOrientationValues.Landscape : W.PageOrientationValues.Portrait, pageSize.Orient!.Value);
            Assert.Equal(W.SectionMarkValues.NextPage, sections[page].GetFirstChild<W.SectionType>()!.Val!.Value);
            var anchor = Assert.Single(paragraphs[page].Descendants<DW.Anchor>());
            Assert.True(anchor.BehindDoc!.Value);
            var extent = anchor.GetFirstChild<DW.Extent>()!;
            Assert.InRange(Math.Abs((double)extent.Cx!.Value / extent.Cy!.Value - size.Width / size.Height), 0, .00001);
            var blip = Assert.Single(anchor.Descendants<A.Blip>());
            Assert.Null(blip.Link);
            var imagePart = Assert.IsType<ImagePart>(main.GetPartById(blip.Embed!));
            using var embedded = imagePart.GetStream();
            using var encoded = new MemoryStream();
            embedded.CopyTo(encoded);
            using var actual = SKBitmap.Decode(encoded.ToArray());
            var actualPixels = actual.Pixels;
            input.Position = 0;
            using var expected = Conversion.ToImage(input, new Index(page), options: new RenderOptions(Dpi: 200));
            Assert.Equal(expected.Width, actual.Width);
            Assert.Equal(expected.Height, actual.Height);
            var ep = expected.Pixels; var ap = actualPixels;
            int mismatch = Enumerable.Range(0, ep.Length).FirstOrDefault(i => ep[i] != ap[i], -1);
            Assert.True(mismatch == -1, $"Pixel mismatch page {page + 1}: index {mismatch}, expected {(mismatch >= 0 ? ep[mismatch] : default)} actual {(mismatch >= 0 ? ap[mismatch] : default)}, types {expected.ColorType}/{expected.AlphaType} vs {actual.ColorType}/{actual.AlphaType}");
        }
    }

    [Theory]
    [InlineData(200)]
    [InlineData(300)]
    [InlineData(600)]
    public async Task DocxUsesSelectedResolution(int dpi)
    {
        string source = PdfFixture.Create(_root, "dpi", (72, 144, 0));
        var result = await new DocxConverterService().ConvertAsync(new(source), Settings(dpi: dpi), new PageProgress(), default);
        Assert.True(result.Success, result.ErrorMessage);
        using var doc = WordprocessingDocument.Open(result.OutputPath!, false);
        using var image = Assert.Single(doc.MainDocumentPart!.ImageParts).GetStream();
        using var encoded = new MemoryStream();
        image.CopyTo(encoded);
        using var bitmap = SKBitmap.Decode(encoded.ToArray());
        Assert.Equal(dpi, bitmap.Width);
        Assert.Equal(2 * dpi, bitmap.Height);
    }

    [Fact]
    public async Task DestinationCreatedDuringConversionIsNeverOverwritten()
    {
        string source = PdfFixture.Create(_root, "race", (72, 144, 0));
        var settings = Settings();
        var final = Path.Combine(settings.OutputRoot, "race.docx");
        var progress = new PageProgress { OnPage = _ =>
        {
            Assert.False(File.Exists(final)); // no partially completed DOCX is exposed
            File.WriteAllText(final, "concurrent user file");
        } };
        var result = await new DocxConverterService().ConvertAsync(new(source), settings, progress, default);
        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal("concurrent user file", File.ReadAllText(final));
        Assert.Equal("race (2).docx", Path.GetFileName(result.OutputPath));
        Assert.DoesNotContain(Directory.GetFiles(settings.OutputRoot), p => p.Contains(".tmp"));
    }

    [Fact]
    public async Task ExistingFilesAndDuplicateNamesArePreserved()
    {
        var source = PdfFixture.Create(_root, "same", (120, 160, 0));
        var settings = Settings();
        Directory.CreateDirectory(settings.OutputRoot);
        var existing = Path.Combine(settings.OutputRoot, "same.docx");
        await File.WriteAllTextAsync(existing, "user file");
        var service = new DocxConverterService();
        var first = await service.ConvertAsync(new(source), settings, new PageProgress(), default);
        var second = await service.ConvertAsync(new(source), settings, new PageProgress(), default);
        Assert.True(first.Success, first.ErrorMessage);
        Assert.True(second.Success, second.ErrorMessage);
        Assert.Equal("same (2).docx", Path.GetFileName(first.OutputPath));
        Assert.Equal("same (3).docx", Path.GetFileName(second.OutputPath));
        Assert.Equal("user file", await File.ReadAllTextAsync(existing));
    }

    [Theory]
    [InlineData("corrupt")]
    [InlineData("encrypted")]
    [InlineData("missing")]
    public async Task UnreadablePdfDoesNotPublishOrOverwriteAnything(string kind)
    {
        string source = Path.Combine(_root, "broken.pdf");
        if (kind == "corrupt") await File.WriteAllTextAsync(source, "not a pdf");
        if (kind == "encrypted") File.Copy(Path.Combine(AppContext.BaseDirectory, "Fixtures/password-protected.pdf"), source);
        var settings = Settings();
        Directory.CreateDirectory(settings.OutputRoot);
        var existing = Path.Combine(settings.OutputRoot, "broken.docx");
        await File.WriteAllTextAsync(existing, "keep");
        var result = await new DocxConverterService().ConvertAsync(new(source), settings, new PageProgress(), default);
        Assert.False(result.Success);
        Assert.Equal(0, result.PagesWritten);
        Assert.NotEmpty(result.ErrorMessage!);
        Assert.Equal(new[] { existing }, Directory.GetFiles(settings.OutputRoot));
        Assert.Equal("keep", await File.ReadAllTextAsync(existing));
    }

    [Fact]
    public async Task CancellationMidDocumentCleansTemporaryPackage()
    {
        string source = PdfFixture.Create(_root, "cancel", (120, 160, 0), (120, 160, 0));
        using var cancel = new CancellationTokenSource();
        var progress = new PageProgress { OnPage = _ => cancel.Cancel() };
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => new DocxConverterService().ConvertAsync(
            new(source), Settings(), progress, cancel.Token));
        Assert.Empty(Directory.GetFiles(Settings().OutputRoot));
    }

    [Fact]
    public async Task FailureAfterFirstPageNeverPublishesPartialDocument()
    {
        string source = PdfFixture.Create(_root, "too-large", (120, 160, 0), (10000, 10000, 0));
        var progress = new PageProgress();
        var result = await new DocxConverterService().ConvertAsync(new(source), Settings(), progress, default);
        Assert.False(result.Success);
        Assert.Equal(new[] { 1 }, progress.Pages);
        Assert.Equal(0, result.PagesWritten);
        Assert.Empty(Directory.GetFiles(Settings().OutputRoot));
    }

    [Fact]
    public async Task OversizedPaperIsScaledToWordsLimitWithoutDistortion()
    {
        string source = PdfFixture.Create(_root, "poster", (1800, 900, 0));
        var result = await new DocxConverterService().ConvertAsync(new(source), Settings(dpi: 72), new PageProgress(), default);
        Assert.True(result.Success, result.ErrorMessage);
        using var doc = WordprocessingDocument.Open(result.OutputPath!, false);
        var size = Assert.Single(doc.MainDocumentPart!.Document.Descendants<W.PageSize>());
        Assert.Equal(31680U, size.Width!.Value);
        Assert.Equal(15840U, size.Height!.Value);
    }

    [Theory]
    [InlineData(ImageFormat.Png, "png")]
    [InlineData(ImageFormat.Jpeg, "jpg")]
    public async Task ExistingImageConversionStillRendersAllPages(ImageFormat format, string extension)
    {
        string source = PdfFixture.Create(_root, "images", (180, 240, 0), (240, 180, 0));
        var settings = new ConversionSettings { OutputRoot = Path.Combine(_root, "images-out"), Dpi = 150, Format = format };
        var progress = new PageProgress();
        var result = await new PdfConverterService().ConvertAsync(new(source), settings, progress, default);
        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(2, result.PagesWritten);
        Assert.Equal(new[] { 1, 2 }, progress.Pages);
        for (int page = 0; page < 2; page++)
        {
            var path = Path.Combine(settings.OutputRoot, "images", $"images {page + 1:D3}.{extension}");
            using var image = SKBitmap.Decode(path);
            Assert.NotNull(image);
            using var input = File.OpenRead(source);
            using var expected = Conversion.ToImage(input, new Index(page), options: new RenderOptions(Dpi: 150));
            Assert.Equal(expected.Width, image.Width);
            Assert.Equal(expected.Height, image.Height);
            if (format == ImageFormat.Png) Assert.Equal(expected.Pixels, image.Pixels);
            else
            {
                double meanError = expected.Bytes.Zip(image.Bytes, (a, b) => Math.Abs(a - b)).Average();
                Assert.InRange(meanError, 0, 5);
            }
        }
        // Re-running replaces existing images successfully and leaves no .tmp files.
        result = await new PdfConverterService().ConvertAsync(new(source), settings, new PageProgress(), default);
        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(2, Directory.GetFiles(Path.Combine(settings.OutputRoot, "images")).Length);
    }

    [Theory]
    [InlineData("CON")]
    [InlineData("aux.txt")]
    [InlineData("../escape")]
    [InlineData("bad.")]
    [InlineData("bad ")]
    [InlineData("a:b")]
    public async Task InvalidWindowsNamesCannotEscapeDestination(string name)
    {
        string source = PdfFixture.Create(_root, "valid", (120, 160, 0));
        var result = await new DocxConverterService().ConvertAsync(new(source) { BookName = name }, Settings(), new PageProgress(), default);
        Assert.False(result.Success);
        Assert.False(Directory.Exists(Settings().OutputRoot));
    }
}
