using System.IO;
using PdfToImages.Models;
using PDFtoImage;
using SkiaSharp;

namespace PdfToImages.Services;

public enum ImageFormat
{
    Png,
    Jpeg
}

public class ConversionSettings
{
    public int Dpi { get; init; } = 300;
    public ImageFormat Format { get; init; } = ImageFormat.Png;
    public string OutputRoot { get; init; } = string.Empty;
    /// <summary>JPEG quality (1-100). Ignored for PNG.</summary>
    public int JpegQuality { get; init; } = 92;
}

public class ConversionResult
{
    public bool Success { get; init; }
    public int PagesWritten { get; init; }
    public string? OutputPath { get; init; }
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Wraps the PDFtoImage (PDFium) library. Every public method is defensive:
/// a broken/encrypted/unsupported PDF must produce a clear error result,
/// never an unhandled exception that could bring down the whole batch.
/// Pages are written atomically (temp file -> rename) so a crash or
/// disk-full mid-page never leaves a corrupt image file behind.
/// </summary>
public class PdfConverterService
{
    /// <summary>Reads the page count only - used right after a file is added, to drive the padding width.</summary>
    public int GetPageCount(string pdfPath)
    {
        using var stream = File.OpenRead(pdfPath);
        return Conversion.GetPageCount(stream);
    }

    public Task<ConversionResult> ConvertAsync(
        ConversionJob job,
        ConversionSettings settings,
        IProgress<int> progress,
        CancellationToken cancellationToken)
    {
        // ConvertPagesSync does only synchronous file I/O and PDFium calls - there is
        // no real await anywhere in that work. Running it directly on an "async" method
        // does NOT move it off the calling thread; without this Task.Run, the entire
        // page loop executes on the UI thread and freezes the window for the whole
        // conversion, even though the method is awaited. Task.Run is what actually
        // offloads it to a thread-pool thread.
        return Task.Run(() => ConvertPagesSync(job, settings, progress, cancellationToken), cancellationToken);
    }

    private ConversionResult ConvertPagesSync(
        ConversionJob job,
        ConversionSettings settings,
        IProgress<int> progress,
        CancellationToken cancellationToken)
    {
        string outputFolder = FileNamingService.BuildOutputFolder(settings.OutputRoot, job.BookName);

        try
        {
            Directory.CreateDirectory(outputFolder);
        }
        catch (Exception ex)
        {
            return new ConversionResult
            {
                Success = false,
                ErrorMessage = $"לא ניתן ליצור את תיקיית הפלט: {ex.Message}"
            };
        }

        int pageCount;
        try
        {
            using var countStream = File.OpenRead(job.SourcePath);
            pageCount = Conversion.GetPageCount(countStream);
        }
        catch (Exception ex)
        {
            return new ConversionResult
            {
                Success = false,
                ErrorMessage = $"לא ניתן לקרוא את הקובץ (ייתכן שהוא פגום או מוצפן): {ex.Message}"
            };
        }

        if (pageCount <= 0)
        {
            return new ConversionResult { Success = false, ErrorMessage = "הקובץ אינו מכיל עמודים." };
        }

        string extension = settings.Format == ImageFormat.Png ? "png" : "jpg";
        int written = 0;

        for (int pageIndex = 0; pageIndex < pageCount; pageIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            int pageNumberOneBased = pageIndex + 1;
            string fileName = FileNamingService.BuildFileName(job.BookName, pageNumberOneBased, pageCount, extension);
            string finalPath = Path.Combine(outputFolder, fileName);
            string tempPath = finalPath + $".{Guid.NewGuid():N}.tmp";

            try
            {
                using (var pdfStream = File.OpenRead(job.SourcePath))
                using (SKBitmap bitmap = Conversion.ToImage(
                           pdfStream,
                           page: new Index(pageIndex),
                           options: new RenderOptions(Dpi: settings.Dpi)))
                {
                    using var fs = File.Create(tempPath);
                    if (settings.Format == ImageFormat.Png)
                    {
                        bitmap.Encode(fs, SKEncodedImageFormat.Png, 100);
                    }
                    else
                    {
                        bitmap.Encode(fs, SKEncodedImageFormat.Jpeg, settings.JpegQuality);
                    }
                }

                // Atomic publish: only replace the final file once the temp file
                // was written and closed successfully.
                File.Move(tempPath, finalPath, overwrite: true);

                written++;
                progress.Report(pageNumberOneBased);
            }
            catch (Exception ex)
            {
                TryDeleteTempFile(tempPath);
                return new ConversionResult
                {
                    Success = false,
                    PagesWritten = written,
                    ErrorMessage = $"שגיאה בעמוד {pageNumberOneBased}: {ex.Message}"
                };
            }
        }

        return new ConversionResult { Success = true, PagesWritten = written };
    }

    private static void TryDeleteTempFile(string path)
    {
        try
        {
            if (File.Exists(path)) File.Delete(path);
        }
        catch
        {
            // Best-effort cleanup only - never let cleanup failure mask the real error.
        }
    }
}
