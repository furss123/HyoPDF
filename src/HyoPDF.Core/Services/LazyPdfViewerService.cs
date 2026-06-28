using System.Windows.Media;
using HyoPDF.Core.Models;
using PdfiumViewer;

namespace HyoPDF.Core.Services;

public sealed class LazyPdfViewerService : IPdfViewerService, IDisposable
{
    private PdfViewerService? _inner;

    private PdfViewerService Inner => _inner ??= new PdfViewerService();

    public bool HasDocument => _inner?.HasDocument ?? false;

    public string? CurrentPath => _inner?.CurrentPath;

    public PdfDocument? OpenFile(string path) => Inner.OpenFile(path);

    public Task<PdfDocument?> OpenFileAsync(string path) => Inner.OpenFileAsync(path);

    public int GetPageCount() => _inner?.GetPageCount() ?? 0;

    public ImageSource RenderPage(int pageIndex, int dpi, int rotation = 0) =>
        Inner.RenderPage(pageIndex, dpi, rotation);

    public ImageSource RenderPageThumbnail(int pageIndex, int widthPx) =>
        Inner.RenderPageThumbnail(pageIndex, widthPx);

    public List<BookmarkItem> GetBookmarks() => _inner?.GetBookmarks() ?? [];

    public void AddBookmark(string filePath, string title, int pageIndex) =>
        Inner.AddBookmark(filePath, title, pageIndex);

    public List<SearchResult> SearchText(string query) => _inner?.SearchText(query) ?? [];

    public System.Windows.Size GetPageSize(int pageIndex) =>
        _inner?.GetPageSize(pageIndex) ?? System.Windows.Size.Empty;

    public void CloseFile() => _inner?.CloseFile();

    public void Dispose()
    {
        _inner?.Dispose();
        _inner = null;
    }
}
