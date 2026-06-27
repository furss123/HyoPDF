namespace HyoPDF.Core.Models;

public sealed class BookmarkItem
{
    public string Title { get; set; } = string.Empty;
    public int PageIndex { get; set; }
    public List<BookmarkItem> Children { get; set; } = [];
}
