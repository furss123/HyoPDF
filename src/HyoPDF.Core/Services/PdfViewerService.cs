using System.Drawing;
using System.Windows;
using System.Windows.Media;
using HyoPDF.Core.Imaging;
using HyoPDF.Core.Models;
using PdfiumViewer;

namespace HyoPDF.Core.Services;

public sealed class PdfViewerService : IPdfViewerService, IDisposable
{
    private readonly object _lock = new();
    private PdfDocument? _document;
    private string? _currentPath;

    public bool HasDocument
    {
        get { lock (_lock) return _document is not null; }
    }

    public string? CurrentPath
    {
        get { lock (_lock) return _currentPath; }
    }

    public PdfDocument? OpenFile(string path)
    {
        lock (_lock)
        {
            CloseFileInternal();
            _document = PdfDocument.Load(path);
            _currentPath = path;
            return _document;
        }
    }

    public Task<PdfDocument?> OpenFileAsync(string path) =>
        Task.Run(() => OpenFile(path));

    public int GetPageCount()
    {
        lock (_lock)
            return _document?.PageCount ?? 0;
    }

    public ImageSource RenderPage(int pageIndex, int dpi, int rotation = 0)
    {
        lock (_lock)
        {
            if (_document is null)
                throw new InvalidOperationException("No PDF document is open.");

            var pdfRotation = ToPdfRotation(rotation);
            var pageSize = _document.PageSizes[pageIndex];
            var width = Math.Max(1, (int)(pageSize.Width * dpi / 72.0));
            var height = Math.Max(1, (int)(pageSize.Height * dpi / 72.0));

            if (rotation is 90 or 270)
                (width, height) = (height, width);

            using var image = _document.Render(
                pageIndex,
                width,
                height,
                dpi,
                dpi,
                pdfRotation,
                PdfRenderFlags.Annotations);

            return BitmapSourceHelper.FromImage(image);
        }
    }

    public ImageSource RenderPageThumbnail(int pageIndex, int widthPx)
    {
        lock (_lock)
        {
            if (_document is null)
                throw new InvalidOperationException("No PDF document is open.");

            var pageSize = _document.PageSizes[pageIndex];
            var heightPx = Math.Max(1, (int)(pageSize.Height / pageSize.Width * widthPx));

            using var image = _document.Render(
                pageIndex,
                widthPx,
                heightPx,
                96,
                96,
                PdfRenderFlags.Annotations);

            return BitmapSourceHelper.FromImage(image);
        }
    }

    public List<BookmarkItem> GetBookmarks()
    {
        lock (_lock)
        {
            if (_document is null)
                return [];

            return _document.Bookmarks
                .Select(MapBookmark)
                .ToList();
        }
    }

    public void AddBookmark(string filePath, string title, int pageIndex)
    {
        lock (_lock)
        {
            var reopen = string.Equals(_currentPath, filePath, StringComparison.OrdinalIgnoreCase)
                         && _document is not null;

            if (reopen)
                CloseFileInternal();

            try
            {
                PdfBookmarkWriter.AddOutline(filePath, title, pageIndex);
            }
            finally
            {
                // Reopen even if AddOutline threw - otherwise a failed bookmark
                // write leaves _document null permanently and every render call
                // fails with "No PDF document is open" until the user manually
                // reopens the file.
                if (reopen)
                {
                    _document = PdfDocument.Load(filePath);
                    _currentPath = filePath;
                }
            }
        }
    }

    public List<SearchResult> SearchText(string query)
    {
        lock (_lock)
        {
            if (_document is null || string.IsNullOrWhiteSpace(query))
                return [];

            var matches = _document.Search(query, matchCase: false, wholeWord: false);
            var results = new List<SearchResult>();
            foreach (var match in matches.Items)
            {
                var bounds = _document.GetTextBounds(match.TextSpan);
                foreach (var pdfRect in bounds)
                {
                    if (!pdfRect.IsValid) continue;
                    var rect = pdfRect.Bounds;
                    if (double.IsNaN(rect.X) || double.IsNaN(rect.Y) ||
                        double.IsNaN(rect.Width) || double.IsNaN(rect.Height) ||
                        rect.Width <= 0 || rect.Height <= 0)
                        continue;

                    results.Add(new SearchResult
                    {
                        PageIndex = match.Page,
                        Rect = new Rect(rect.X, rect.Y, rect.Width, rect.Height)
                    });
                }
            }

            return results;
        }
    }

    public System.Windows.Size GetPageSize(int pageIndex)
    {
        lock (_lock)
        {
            if (_document is null)
                return System.Windows.Size.Empty;

            if (pageIndex < 0 || pageIndex >= _document.PageCount)
                return System.Windows.Size.Empty;

            var size = _document.PageSizes[pageIndex];
            return new System.Windows.Size(size.Width, size.Height);
        }
    }

    public void CloseFile()
    {
        lock (_lock)
            CloseFileInternal();
    }

    private void CloseFileInternal()
    {
        _document?.Dispose();
        _document = null;
        _currentPath = null;
    }

    private static BookmarkItem MapBookmark(PdfBookmark bookmark) => new()
    {
        Title = bookmark.Title,
        PageIndex = bookmark.PageIndex,
        Children = bookmark.Children.Select(MapBookmark).ToList()
    };

    private static PdfRotation ToPdfRotation(int rotation) => rotation switch
    {
        90 => PdfRotation.Rotate90,
        180 => PdfRotation.Rotate180,
        270 => PdfRotation.Rotate270,
        _ => PdfRotation.Rotate0
    };

    public void Dispose() => CloseFile();
}
