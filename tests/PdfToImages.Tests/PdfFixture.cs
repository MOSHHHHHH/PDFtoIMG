using System.Globalization;
using System.Text;

namespace PdfToImages.Tests;

internal static class PdfFixture
{
    // Real PDF fixtures with a distinct colored rectangle and page label on every page.
    // /Rotate exercises PDFium's effective page dimensions as well as media-box orientation.
    public static string Create(string root, string name, params (int Width, int Height, int Rotation)[] pages)
    {
        var objects = new List<string> { "<< /Type /Catalog /Pages 2 0 R >>", "" };
        var kids = new List<string>();
        for (int i = 0; i < pages.Length; i++)
        {
            var p = pages[i];
            int id = objects.Count + 1;
            kids.Add($"{id} 0 R");
            objects.Add($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {p.Width} {p.Height}] /Rotate {p.Rotation} /Resources << /Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> >> >> /Contents {id + 1} 0 R >>");
            string red = ((i % 3 + 1) / 4.0).ToString(CultureInfo.InvariantCulture);
            string stream = $"{red} 0.3 0.7 rg 10 10 {p.Width - 20} {p.Height - 20} re f\n0 0 0 rg BT /F1 20 Tf 20 40 Td (Page {i + 1}) Tj ET\n";
            objects.Add($"<< /Length {Encoding.ASCII.GetByteCount(stream)} >>\nstream\n{stream}endstream");
        }
        objects[1] = $"<< /Type /Pages /Count {pages.Length} /Kids [{string.Join(" ", kids)}] >>";
        var pdf = new StringBuilder("%PDF-1.4\n");
        var offsets = new List<int> { 0 };
        for (int i = 0; i < objects.Count; i++)
        {
            offsets.Add(Encoding.ASCII.GetByteCount(pdf.ToString()));
            pdf.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
        }
        int xref = Encoding.ASCII.GetByteCount(pdf.ToString());
        pdf.Append($"xref\n0 {offsets.Count}\n0000000000 65535 f \n");
        foreach (int offset in offsets.Skip(1)) pdf.Append($"{offset:D10} 00000 n \n");
        pdf.Append($"trailer\n<< /Size {offsets.Count} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
        string path = Path.Combine(root, name + ".pdf");
        File.WriteAllText(path, pdf.ToString(), Encoding.ASCII);
        return path;
    }
}
