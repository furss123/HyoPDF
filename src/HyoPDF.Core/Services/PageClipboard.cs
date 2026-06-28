namespace HyoPDF.Core.Services;

public enum ClipboardOperation
{
    Copy,
    Cut
}

public sealed class PageClipboard
{
    public List<int> PageIndices { get; set; } = [];

    public string SourceFilePath { get; set; } = string.Empty;

    public ClipboardOperation Operation { get; set; }
}
