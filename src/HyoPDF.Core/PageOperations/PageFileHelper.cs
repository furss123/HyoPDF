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
        // Stage next to the original, then atomically rename over it, so a crash
        // mid-write can only ever leave behind a stray staging file - never a
        // truncated/corrupted original. A direct File.Copy(overwrite: true) has
        // no such guarantee: it writes into the original file in place.
        var stagingPath = originalPath + ".hyopdf-tmp";
        File.Copy(tempPath, stagingPath, overwrite: true);
        File.Move(stagingPath, originalPath, overwrite: true);
    }

    public static string CreateTempOutputPath()
    {
        return Path.Combine(Path.GetTempPath(), $"hyopdf_out_{Guid.NewGuid():N}.pdf");
    }
}
