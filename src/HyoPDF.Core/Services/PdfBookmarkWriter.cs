using System.IO;
using System.util;
using iTextSharp.text.pdf;

namespace HyoPDF.Core.Services;

internal static class PdfBookmarkWriter
{
    public static void AddOutline(string filePath, string title, int pageIndex)
    {
        var bytes = File.ReadAllBytes(filePath);
        using var input = new MemoryStream(bytes);
        using var output = new MemoryStream();

        var reader = new PdfReader(input);
        var stamper = new PdfStamper(reader, output);

        var existing = SimpleBookmark.GetBookmark(reader);
        var outlines = existing != null
            ? existing.ToList()
            : new List<INullValueDictionary<string, object>>();

        var bookmark = new NullValueDictionary<string, object>
        {
            ["Title"] = title,
            ["Action"] = "GoTo",
            ["Page"] = $"{pageIndex + 1} Fit"
        };
        outlines.Add(bookmark);

        stamper.Outlines = outlines;
        stamper.Writer.ViewerPreferences = PdfWriter.PageModeUseOutlines;

        stamper.Close();
        reader.Close();

        File.WriteAllBytes(filePath, output.ToArray());
    }
}
