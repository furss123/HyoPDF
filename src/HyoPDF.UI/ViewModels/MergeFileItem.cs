using System.IO;
using HyoPDF.UI.Helpers;

namespace HyoPDF.UI.ViewModels;

public sealed class MergeFileItem
{
    public MergeFileItem(string filePath)
    {
        FilePath = filePath;
        FileName = Path.GetFileName(filePath);
        FileSize = File.Exists(filePath)
            ? FileSizeFormatter.Format(new FileInfo(filePath).Length)
            : "-";
    }

    public string FilePath { get; }
    public string FileName { get; }
    public string FileSize { get; }
}
