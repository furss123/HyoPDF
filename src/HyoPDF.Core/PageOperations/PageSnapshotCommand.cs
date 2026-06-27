using System.IO;

namespace HyoPDF.Core.PageOperations;

public sealed class PageSnapshotCommand : IPageCommand
{
    private readonly string _documentPath;
    private readonly string _undoSnapshot;
    private readonly string _redoSnapshot;
    private readonly Action? _onRestored;

    public PageSnapshotCommand(
        string description,
        string documentPath,
        string undoSnapshot,
        string redoSnapshot,
        Action? onRestored = null)
    {
        Description = description;
        _documentPath = documentPath;
        _undoSnapshot = undoSnapshot;
        _redoSnapshot = redoSnapshot;
        _onRestored = onRestored;
    }

    public string Description { get; }

    public void Undo()
    {
        File.Copy(_undoSnapshot, _documentPath, overwrite: true);
        _onRestored?.Invoke();
    }

    public void Redo()
    {
        File.Copy(_redoSnapshot, _documentPath, overwrite: true);
        _onRestored?.Invoke();
    }
}
