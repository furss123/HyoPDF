using System.Windows.Media;
using HyoPDF.Core.Models;
using PdfiumViewer;

namespace HyoPDF.Core.Services;

public interface IPdfViewerService
{
    bool HasDocument { get; }
    string? CurrentPath { get; }
    PdfDocument? OpenFile(string path);
    Task<PdfDocument?> OpenFileAsync(string path);
    int GetPageCount();
    ImageSource RenderPage(int pageIndex, int dpi, int rotation = 0);
    ImageSource RenderPageThumbnail(int pageIndex, int widthPx);
    List<BookmarkItem> GetBookmarks();
    void AddBookmark(string filePath, string title, int pageIndex);
    List<SearchResult> SearchText(string query);
    System.Windows.Size GetPageSize(int pageIndex);
    void CloseFile();
}
