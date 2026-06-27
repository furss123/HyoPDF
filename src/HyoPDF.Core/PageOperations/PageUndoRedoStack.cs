namespace HyoPDF.Core.PageOperations;

public sealed class PageUndoRedoStack
{
    public const int MaxDepth = 20;

    private readonly List<IPageCommand> _undo = [];
    private readonly List<IPageCommand> _redo = [];

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;

    public event EventHandler? StackChanged;

    public void Push(IPageCommand command)
    {
        _undo.Add(command);
        if (_undo.Count > MaxDepth)
            _undo.RemoveAt(0);

        _redo.Clear();
        StackChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Undo()
    {
        if (!CanUndo) return;

        var command = _undo[^1];
        _undo.RemoveAt(_undo.Count - 1);
        command.Undo();
        _redo.Add(command);
        StackChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Redo()
    {
        if (!CanRedo) return;

        var command = _redo[^1];
        _redo.RemoveAt(_redo.Count - 1);
        command.Redo();
        _undo.Add(command);
        StackChanged?.Invoke(this, EventArgs.Empty);
    }

    public void Clear()
    {
        _undo.Clear();
        _redo.Clear();
        StackChanged?.Invoke(this, EventArgs.Empty);
    }
}
