namespace HyoPDF.Core.Services;

public sealed class PageClipboardService
{
    private PageClipboard? _clipboard;

    public event EventHandler? ClipboardChanged;

    public bool HasContent => _clipboard is not null;

    public ClipboardOperation? Operation => _clipboard?.Operation;

    public void Copy(string filePath, List<int> indices)
    {
        _clipboard = new PageClipboard
        {
            SourceFilePath = filePath,
            PageIndices = indices.ToList(),
            Operation = ClipboardOperation.Copy
        };
        ClipboardChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Cut(string filePath, List<int> indices)
    {
        _clipboard = new PageClipboard
        {
            SourceFilePath = filePath,
            PageIndices = indices.ToList(),
            Operation = ClipboardOperation.Cut
        };
        ClipboardChanged?.Invoke(this, EventArgs.Empty);
    }

    public PageClipboard? Paste() => _clipboard;

    public void Clear()
    {
        if (_clipboard is null)
            return;

        _clipboard = null;
        ClipboardChanged?.Invoke(this, EventArgs.Empty);
    }
}
