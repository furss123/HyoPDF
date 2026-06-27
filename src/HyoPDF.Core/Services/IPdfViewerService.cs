using System.Windows.Media;
using HyoPDF.Core.Models;
using PdfiumViewer;

namespace HyoPDF.Core.Services;

public interface IPdfViewerService
{
    bool HasDocument { get; }
    string? CurrentPath { get; }
    PdfDocument? OpenFile(string path);
    int GetPageCount();
    ImageSource RenderPage(int pageIndex, int dpi, int rotation = 0);
    ImageSource RenderPageThumbnail(int pageIndex, int widthPx);
    List<BookmarkItem> GetBookmarks();
    List<SearchResult> SearchText(string query);
    System.Windows.Size GetPageSize(int pageIndex);
    void CloseFile();
}
