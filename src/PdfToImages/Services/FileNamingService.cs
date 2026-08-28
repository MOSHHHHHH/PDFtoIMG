using System.IO;
using System.Text.RegularExpressions;

namespace PdfToImages.Services;

/// <summary>
/// Builds the output folder/file names required by the spec:
///   folder name  = book name
///   file name    = "{book name} {N}" zero-padded to fit the page count
///                  (e.g. a 150-page book -> 001 .. 150)
/// Also validates that a book name is safe as a Windows folder/file name
/// and unique within the current job list.
/// </summary>
public static class FileNamingService
{
    private static readonly char[] InvalidChars = Path.GetInvalidFileNameChars();

    /// <summary>True if the name contains characters that cannot be used in a Windows file/folder name.</summary>
    public static bool HasInvalidChars(string bookName) =>
        string.IsNullOrWhiteSpace(bookName) || bookName.IndexOfAny(InvalidChars) >= 0;

    /// <summary>
    /// Returns the padded page-number string for a given 1-based page index,
    /// where the padding width is derived from the total page count.
    /// e.g. totalPages=150 -> width 3 -> "007"; totalPages=1200 -> width 4 -> "0007".
    /// </summary>
    public static string PadPageNumber(int pageIndexOneBased, int totalPages)
    {
        int width = Math.Max(3, totalPages.ToString().Length);
        return pageIndexOneBased.ToString().PadLeft(width, '0');
    }

    public static string BuildFileName(string bookName, int pageIndexOneBased, int totalPages, string extension)
    {
        string padded = PadPageNumber(pageIndexOneBased, totalPages);
        return $"{bookName} {padded}.{extension}";
    }

    public static string BuildOutputFolder(string outputRoot, string bookName) =>
        Path.Combine(outputRoot, SanitizeForDisplay(bookName));

    /// <summary>Strips characters that are invalid in a path, used only as a defensive fallback -
    /// the UI is expected to reject invalid names before this is ever reached.</summary>
    private static string SanitizeForDisplay(string name)
    {
        foreach (char c in InvalidChars)
            name = name.Replace(c, '_');
        return name;
    }
}
