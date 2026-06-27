using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HyoPDF.Core.Models;
using HyoPDF.Core.Services;

namespace HyoPDF.UI.ViewModels;

public partial class ViewerViewModel : ObservableObject
{
    private const int ThumbnailWidthPx = 120;
    private readonly IPdfViewerService _pdfViewer;

    [ObservableProperty]
    private int _currentPageIndex;

    public int CurrentPageNumber => CurrentPageIndex + 1;

    public bool IsUpdatingFromScroll { get; set; }

    [ObservableProperty]
    private int _pageCount;

    [ObservableProperty]
    private int _zoomLevel = 100;

    [ObservableProperty]
    private int _rotation;

    [ObservableProperty]
    private bool _isFullscreen;

    [ObservableProperty]
    private bool _hasDocument;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private ImageSource? _currentPageImage;

    [ObservableProperty]
    private double _currentPageWidth;

    [ObservableProperty]
    private double _currentPageHeight;

    public ObservableCollection<SearchResult> SearchResults { get; } = [];
    public ObservableCollection<BookmarkItem> Bookmarks { get; } = [];
    public ObservableCollection<PdfPageItemViewModel> Pages { get; } = [];

    public ICommand NextPageCommand { get; }
    public ICommand PrevPageCommand { get; }
    public ICommand ZoomInCommand { get; }
    public ICommand ZoomOutCommand { get; }
    public ICommand RotateCommand { get; }
    public ICommand ToggleFullscreenCommand { get; }
    public ICommand SearchCommand { get; }
    public ICommand GoToPageCommand { get; }
    public ICommand ResetZoomCommand { get; }

    public event EventHandler? RenderRefreshRequested;
    public event EventHandler<int>? ScrollToPageRequested;

    public ViewerViewModel(IPdfViewerService pdfViewer)
    {
        _pdfViewer = pdfViewer;

        NextPageCommand = new RelayCommand(NextPage, () => HasDocument && CurrentPageIndex < PageCount - 1);
        PrevPageCommand = new RelayCommand(PrevPage, () => HasDocument && CurrentPageIndex > 0);
        ZoomInCommand = new RelayCommand(() => ZoomLevel = Math.Min(ZoomLevel + 10, 500));
        ZoomOutCommand = new RelayCommand(() => ZoomLevel = Math.Max(ZoomLevel - 10, 25));
        RotateCommand = new RelayCommand(Rotate);
        ToggleFullscreenCommand = new RelayCommand(() => IsFullscreen = !IsFullscreen);
        SearchCommand = new RelayCommand(ExecuteSearch);
        GoToPageCommand = new RelayCommand<int>(GoToPage);
        ResetZoomCommand = new RelayCommand(() => ZoomLevel = 100, () => HasDocument);
    }

    public void LoadDocument(string path)
    {
        _pdfViewer.OpenFile(path);
        HasDocument = _pdfViewer.HasDocument;
        PageCount = _pdfViewer.GetPageCount();
        CurrentPageIndex = 0;
        Rotation = 0;
        ZoomLevel = 100;
        SearchQuery = string.Empty;
        SearchResults.Clear();
        Bookmarks.Clear();
        Pages.Clear();

        foreach (var bookmark in _pdfViewer.GetBookmarks())
            Bookmarks.Add(bookmark);

        for (var i = 0; i < PageCount; i++)
        {
            Pages.Add(new PdfPageItemViewModel
            {
                PageIndex = i,
                PageNumber = i + 1
            });
        }

        RefreshAllPages();
        _ = LoadThumbnailsAsync();
        NotifyNavigationCommands();
    }

    public void CloseDocument()
    {
        _pdfViewer.CloseFile();
        HasDocument = false;
        PageCount = 0;
        CurrentPageIndex = 0;
        CurrentPageImage = null;
        SearchResults.Clear();
        Bookmarks.Clear();
        Pages.Clear();
        NotifyNavigationCommands();
    }

    private void NextPage()
    {
        if (CurrentPageIndex < PageCount - 1)
            CurrentPageIndex++;
    }

    private void PrevPage()
    {
        if (CurrentPageIndex > 0)
            CurrentPageIndex--;
    }

    private void Rotate()
    {
        Rotation = (Rotation + 90) % 360;
        RefreshAllPages();
    }

    private void GoToPage(int pageIndex)
    {
        if (pageIndex < 0 || pageIndex >= PageCount) return;
        CurrentPageIndex = pageIndex;
    }

    public void GoToBookmark(BookmarkItem bookmark) => GoToPage(bookmark.PageIndex);

    private void ExecuteSearch()
    {
        SearchResults.Clear();

        if (!HasDocument || string.IsNullOrWhiteSpace(SearchQuery))
        {
            UpdatePageHighlights();
            RefreshAllPages();
            return;
        }

        foreach (var result in _pdfViewer.SearchText(SearchQuery))
            SearchResults.Add(result);

        UpdatePageHighlights();

        var first = SearchResults.FirstOrDefault();
        if (first is not null)
            CurrentPageIndex = first.PageIndex;

        RefreshAllPages();
    }

    partial void OnCurrentPageIndexChanged(int value)
    {
        OnPropertyChanged(nameof(CurrentPageNumber));
        UpdatePageHighlights();
        RenderCurrentPage();
        if (!IsUpdatingFromScroll)
            ScrollToPageRequested?.Invoke(this, value);
        NotifyNavigationCommands();
    }

    partial void OnZoomLevelChanged(int value) => RefreshAllPages();

    partial void OnSearchQueryChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            SearchResults.Clear();
            UpdatePageHighlights();
            RefreshAllPages();
        }
    }

    partial void OnHasDocumentChanged(bool value) => NotifyNavigationCommands();

    private void UpdatePageHighlights()
    {
        foreach (var page in Pages)
        {
            page.HighlightRects.Clear();
            var pageSize = _pdfViewer.GetPageSize(page.PageIndex);
            if (pageSize.Width <= 0 || page.DisplayWidth <= 0) continue;

            var scale = page.DisplayWidth / pageSize.Width;
            foreach (var hit in SearchResults.Where(r => r.PageIndex == page.PageIndex))
            {
                page.HighlightRects.Add(new Rect(
                    hit.Rect.X * scale,
                    hit.Rect.Y * scale,
                    hit.Rect.Width * scale,
                    hit.Rect.Height * scale));
            }
        }
    }

    private void RefreshAllPages()
    {
        if (!HasDocument) return;

        var dpi = GetRenderDpi();
        for (var i = 0; i < Pages.Count; i++)
        {
            var page = Pages[i];
            try
            {
                page.PageImage = _pdfViewer.RenderPage(page.PageIndex, dpi, Rotation);
                if (page.PageImage is not null)
                {
                    page.DisplayWidth = page.PageImage.Width;
                    page.DisplayHeight = page.PageImage.Height;
                }
            }
            catch
            {
                // Page render can fail during rapid zoom; skip until next refresh.
            }
        }

        RenderCurrentPage();
        UpdatePageHighlights();
        RenderRefreshRequested?.Invoke(this, EventArgs.Empty);
    }

    private void RenderCurrentPage()
    {
        if (!HasDocument || CurrentPageIndex < 0 || CurrentPageIndex >= Pages.Count)
        {
            CurrentPageImage = null;
            CurrentPageWidth = 0;
            CurrentPageHeight = 0;
            return;
        }

        var page = Pages[CurrentPageIndex];
        if (page.PageImage is null)
        {
            var dpi = GetRenderDpi();
            page.PageImage = _pdfViewer.RenderPage(page.PageIndex, dpi, Rotation);
            page.DisplayWidth = page.PageImage?.Width ?? 0;
            page.DisplayHeight = page.PageImage?.Height ?? 0;
        }

        CurrentPageImage = page.PageImage;
        CurrentPageWidth = page.DisplayWidth;
        CurrentPageHeight = page.DisplayHeight;
    }

    public void LoadThumbnailsForRange(int start, int end)
    {
        if (!HasDocument) return;

        start = Math.Max(0, start);
        end = Math.Min(Pages.Count - 1, end);

        for (var i = start; i <= end; i++)
        {
            var page = Pages[i];
            if (page.Thumbnail is not null) continue;
            page.Thumbnail = _pdfViewer.RenderPageThumbnail(page.PageIndex, ThumbnailWidthPx);
        }
    }

    private int GetRenderDpi() => Math.Max(72, (int)(96 * (ZoomLevel / 100.0)));

    private async Task LoadThumbnailsAsync()
    {
        for (var i = 0; i < Pages.Count; i++)
        {
            var index = i;
            try
            {
                var thumbnail = await Task.Run(() => _pdfViewer.RenderPageThumbnail(index, ThumbnailWidthPx));
                await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                {
                    if (index < Pages.Count)
                        Pages[index].Thumbnail = thumbnail;
                });
            }
            catch
            {
                // Thumbnail generation is best-effort.
            }
        }
    }

    private void NotifyNavigationCommands()
    {
        (NextPageCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (PrevPageCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }
}
