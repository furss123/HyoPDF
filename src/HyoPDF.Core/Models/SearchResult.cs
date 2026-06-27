using System.Windows;

namespace HyoPDF.Core.Models;

public sealed class SearchResult
{
    public int PageIndex { get; set; }
    public Rect Rect { get; set; }
}
