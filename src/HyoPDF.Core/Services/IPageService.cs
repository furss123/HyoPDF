namespace HyoPDF.Core.Services;

public interface IPageService
{
    string DeletePages(string path, List<int> pageIndices);
    string AddPage(string sourcePath, string insertPath, int insertAt);
    string CopyPages(string path, List<int> pageIndices, int insertAt);
    string MovePages(string path, List<int> pageIndices, int targetIndex);
    string RotatePages(string path, List<int> pageIndices, int rotation);
    string ExtractPages(string path, List<int> pageIndices, string outputPath);
    string MergePdfs(List<string> paths, string outputPath);
    IReadOnlyList<string> SplitPdf(string path, List<int> splitPoints, string outputDir);
}
