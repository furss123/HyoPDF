namespace HyoPDF.Core.Models;

public sealed class RecentFile
{
    public string Path { get; set; } = string.Empty;
    public string FileName { get; set; } = string.Empty;
    public DateTime LastOpenedAt { get; set; }
}
