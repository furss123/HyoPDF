using System.Windows.Media;

namespace HyoPDF.Core.Models;

public sealed class BookmarkItem
{
    public string Title { get; set; } = string.Empty;

    public int PageIndex { get; set; }

    public int PageNumber => PageIndex + 1;

    public ImageSource? Thumbnail { get; set; }

    public List<BookmarkItem> Children { get; set; } = [];
}
