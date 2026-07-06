using System.IO;
using System.util;
using HyoPDF.Core.PageOperations;
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

        // Write to a temp file first and swap it in atomically - writing
        // straight to filePath would leave a truncated/corrupted PDF behind if
        // the process is interrupted mid-write.
        var tempPath = PageFileHelper.CreateTempOutputPath();
        File.WriteAllBytes(tempPath, output.ToArray());
        try
        {
            PageFileHelper.ReplaceOriginal(filePath, tempPath);
        }
        finally
        {
            File.Delete(tempPath);
        }
    }
}
