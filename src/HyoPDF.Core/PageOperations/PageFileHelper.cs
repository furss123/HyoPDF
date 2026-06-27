using System.IO;

namespace HyoPDF.Core.PageOperations;

public static class PageFileHelper
{
    public static string CreateSnapshot(string sourcePath)
    {
        var snapshot = Path.Combine(Path.GetTempPath(), $"hyopdf_snap_{Guid.NewGuid():N}.pdf");
        File.Copy(sourcePath, snapshot, overwrite: true);
        return snapshot;
    }

    public static void ReplaceOriginal(string originalPath, string tempPath)
    {
        File.Copy(tempPath, originalPath, overwrite: true);
    }

    public static string CreateTempOutputPath()
    {
        return Path.Combine(Path.GetTempPath(), $"hyopdf_out_{Guid.NewGuid():N}.pdf");
    }
}
