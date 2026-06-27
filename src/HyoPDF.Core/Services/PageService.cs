using System.IO;
using PdfSharp.Pdf;
using PdfSharp.Pdf.IO;

namespace HyoPDF.Core.Services;

public sealed class PageService : IPageService
{
    public string DeletePages(string path, List<int> pageIndices)
    {
        using var source = OpenImport(path);
        var remove = pageIndices.Distinct().ToHashSet();
        var pages = Enumerable.Range(0, source.PageCount)
            .Where(i => !remove.Contains(i))
            .Select(i => source.Pages[i])
            .ToList();
        return SavePages(path, pages);
    }

    public string AddPage(string sourcePath, string insertPath, int insertAt)
    {
        using var source = OpenImport(sourcePath);
        using var insertDoc = OpenImport(insertPath);
        insertAt = Math.Clamp(insertAt, 0, source.PageCount);

        var pages = new List<PdfPage>();
        for (var i = 0; i < insertAt; i++)
            pages.Add(source.Pages[i]);
        for (var i = 0; i < insertDoc.PageCount; i++)
            pages.Add(insertDoc.Pages[i]);
        for (var i = insertAt; i < source.PageCount; i++)
            pages.Add(source.Pages[i]);

        return SavePages(sourcePath, pages);
    }

    public string CopyPages(string path, List<int> pageIndices, int insertAt)
    {
        using var source = OpenImport(path);
        var indices = pageIndices.Distinct().OrderBy(i => i).Where(i => i >= 0 && i < source.PageCount).ToList();
        insertAt = Math.Clamp(insertAt, 0, source.PageCount);

        var pages = Enumerable.Range(0, source.PageCount).Select(i => source.Pages[i]).ToList();
        pages.InsertRange(insertAt, indices.Select(i => source.Pages[i]));
        return SavePages(path, pages);
    }

    public string MovePages(string path, List<int> pageIndices, int targetIndex)
    {
        using var source = OpenImport(path);
        var toMove = pageIndices.Distinct().OrderBy(i => i).Where(i => i >= 0 && i < source.PageCount).ToList();
        var moving = toMove.ToHashSet();

        var kept = Enumerable.Range(0, source.PageCount)
            .Where(i => !moving.Contains(i))
            .Select(i => source.Pages[i])
            .ToList();

        var movedPages = toMove.Select(i => source.Pages[i]).ToList();
        targetIndex = Math.Clamp(targetIndex, 0, kept.Count);
        kept.InsertRange(targetIndex, movedPages);

        return SavePages(path, kept);
    }

    public string RotatePages(string path, List<int> pageIndices, int rotation)
    {
        if (rotation is not (90 or 180 or 270))
            throw new ArgumentOutOfRangeException(nameof(rotation), "Rotation must be 90, 180, or 270.");

        var tempPath = PageOperations.PageFileHelper.CreateTempOutputPath();
        using (var document = PdfReader.Open(path, PdfDocumentOpenMode.Modify))
        {
            var targets = pageIndices.Distinct().Where(i => i >= 0 && i < document.PageCount).ToHashSet();
            for (var i = 0; i < document.PageCount; i++)
            {
                if (!targets.Contains(i)) continue;
                document.Pages[i].Rotate = AddRotation(document.Pages[i].Rotate, rotation);
            }

            document.Save(tempPath);
        }

        PageOperations.PageFileHelper.ReplaceOriginal(path, tempPath);
        File.Delete(tempPath);
        return path;
    }

    public string ExtractPages(string path, List<int> pageIndices, string outputPath)
    {
        using var source = OpenImport(path);
        var indices = pageIndices.Distinct().OrderBy(i => i).Where(i => i >= 0 && i < source.PageCount).ToList();
        var pages = indices.Select(i => source.Pages[i]).ToList();
        WritePages(outputPath, pages);
        return outputPath;
    }

    public string MergePdfs(List<string> paths, string outputPath)
    {
        var output = new PdfDocument();
        foreach (var file in paths)
        {
            using var doc = OpenImport(file);
            for (var i = 0; i < doc.PageCount; i++)
                output.AddPage(doc.Pages[i]);
        }

        output.Save(outputPath);
        output.Close();
        return outputPath;
    }

    public IReadOnlyList<string> SplitPdf(string path, List<int> splitPoints, string outputDir)
    {
        Directory.CreateDirectory(outputDir);
        using var source = OpenImport(path);

        var boundaries = splitPoints
            .Where(p => p > 0 && p < source.PageCount)
            .Distinct()
            .OrderBy(p => p)
            .Prepend(0)
            .Append(source.PageCount)
            .Distinct()
            .OrderBy(p => p)
            .ToList();

        if (boundaries.Count < 2)
            boundaries = [0, source.PageCount];

        var outputs = new List<string>();
        var baseName = Path.GetFileNameWithoutExtension(path);

        for (var part = 0; part < boundaries.Count - 1; part++)
        {
            var start = boundaries[part];
            var end = boundaries[part + 1];
            if (start >= end) continue;

            var pages = Enumerable.Range(start, end - start).Select(i => source.Pages[i]).ToList();
            var outputPath = Path.Combine(outputDir, $"{baseName}_part{part + 1}.pdf");
            WritePages(outputPath, pages);
            outputs.Add(outputPath);
        }

        return outputs;
    }

    private static string SavePages(string path, IReadOnlyList<PdfPage> pages)
    {
        var tempPath = PageOperations.PageFileHelper.CreateTempOutputPath();
        WritePages(tempPath, pages);
        PageOperations.PageFileHelper.ReplaceOriginal(path, tempPath);
        File.Delete(tempPath);
        return path;
    }

    private static void WritePages(string outputPath, IReadOnlyList<PdfPage> pages)
    {
        var output = new PdfDocument();
        foreach (var page in pages)
            output.AddPage(page);

        output.Save(outputPath);
        output.Close();
    }

    private static PdfDocument OpenImport(string path) =>
        PdfReader.Open(path, PdfDocumentOpenMode.Import);

    private static int AddRotation(int current, int rotation)
    {
        var angles = new[] { 0, 90, 180, 270 };
        var normalized = ((current % 360) + 360) % 360;
        var index = Array.IndexOf(angles, normalized);
        if (index < 0) index = 0;
        var steps = rotation / 90;
        return angles[(index + steps) % 4];
    }
}
