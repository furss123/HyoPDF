namespace HyoPDF.Core.PageOperations;

public interface IPageCommand
{
    string Description { get; }
    void Undo();
    void Redo();
}
