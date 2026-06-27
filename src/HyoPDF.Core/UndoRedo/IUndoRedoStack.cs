namespace HyoPDF.Core.UndoRedo;

public interface IUndoRedoStack
{
    bool CanUndo { get; }
    bool CanRedo { get; }
    void Push(IUndoableAction action);
    void Undo();
    void Redo();
    void Clear();
}

public interface IUndoableAction
{
    string Description { get; }
    void Undo();
    void Redo();
}
