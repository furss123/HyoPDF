using System.Collections.ObjectModel;

using System.Windows;

using System.Windows.Input;

using System.Windows.Media;
using System.Windows.Media.Imaging;

using System.Windows.Threading;

using CommunityToolkit.Mvvm.ComponentModel;

using CommunityToolkit.Mvvm.Input;

using HyoPDF.Core.Diagnostics;

using HyoPDF.Core.Localization;

using HyoPDF.Core.Models;

using HyoPDF.Core.Services;

using HyoPDF.UI.Services;

using HyoPDF.UI.Views;



namespace HyoPDF.UI.ViewModels;



public partial class ViewerViewModel : ObservableObject, IDisposable

{

    private const int ThumbnailWidthPx = 120;

    private const int VisiblePageBuffer = 1;

    private const int MaxRenderCacheSize = 10;

    private const int ZoomDebounceMs = 400;

    private const double ViewerHorizontalMargin = 48;



    private readonly IPdfViewerService _pdfViewer;

    private readonly IToastService _toastService;

    private readonly ILocalizationService _localization;

    private readonly SemaphoreSlim _thumbnailSemaphore = new(3);

    private readonly Dictionary<int, RenderCacheEntry> _renderCache = new();

    private readonly LinkedList<int> _renderCacheOrder = new();

    private readonly Dictionary<int, WeakReference<ImageSource>> _thumbnailCache = new();



    private CancellationTokenSource? _renderCancellation;

    private CancellationTokenSource? _thumbnailCancellation;

    private DispatcherTimer? _zoomDebounceTimer;

    private bool _isUpdatingZoomProgrammatically;

    private int _renderZoomLevel = 100;

    private int _renderGeneration;

    private int _lastVisibleStart = -1;

    private int _lastVisibleEnd = -1;

    private double _viewerAreaWidth;

    private bool _isClampingPageIndex;



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

    private bool _isFitToWidth;



    [ObservableProperty]

    private bool _isLoading;



    [ObservableProperty]

    private string _loadingMessage = string.Empty;



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

    public double ZoomFactor => _renderZoomLevel > 0 ? ZoomLevel / (double)_renderZoomLevel : 1.0;

    public ICommand NextPageCommand { get; }

    public ICommand PrevPageCommand { get; }

    public ICommand ZoomInCommand { get; }

    public ICommand ZoomOutCommand { get; }

    public ICommand RotateCommand { get; }

    public ICommand ToggleFullscreenCommand { get; }

    public ICommand SearchCommand { get; }

    public ICommand GoToPageCommand { get; }

    public ICommand ResetZoomCommand { get; }

    public ICommand ToggleFitToWidthCommand { get; }

    public ICommand AddBookmarkCommand { get; }



    public event EventHandler<int>? ScrollToPageRequested;



    public ViewerViewModel(IPdfViewerService pdfViewer, IToastService toastService, ILocalizationService localization)

    {

        _pdfViewer = pdfViewer;

        _toastService = toastService;

        _localization = localization;

        LoadingMessage = _localization.GetString("LoadingMessage");

        _thumbnailCancellation = new CancellationTokenSource();



        NextPageCommand = new RelayCommand(NextPage, () => HasDocument && CurrentPageIndex < PageCount - 1);

        PrevPageCommand = new RelayCommand(PrevPage, () => HasDocument && CurrentPageIndex > 0);

        ZoomInCommand = new RelayCommand(() => SetZoomLevelManually(Math.Min(ZoomLevel + 10, 500)));

        ZoomOutCommand = new RelayCommand(() => SetZoomLevelManually(Math.Max(ZoomLevel - 10, 25)));

        RotateCommand = new RelayCommand(Rotate);

        ToggleFullscreenCommand = new RelayCommand(() => IsFullscreen = !IsFullscreen);

        SearchCommand = new RelayCommand(ExecuteSearch);

        GoToPageCommand = new RelayCommand<int>(GoToPage);

        ResetZoomCommand = new RelayCommand(() => SetZoomLevelManually(100), () => HasDocument);

        ToggleFitToWidthCommand = new RelayCommand(ToggleFitToWidth, () => HasDocument);

        AddBookmarkCommand = new AsyncRelayCommand<int>(AddBookmarkAsync, CanAddBookmark);

    }



    public void LoadDocument(string path, int? initialPageIndex = null) =>
        _ = LoadDocumentAsync(path, initialPageIndex);

    public Task ReloadDocumentAsync(string path, int? initialPageIndex = null) =>
        LoadDocumentAsync(path, initialPageIndex);

    private async Task LoadDocumentAsync(string path, int? initialPageIndex = null)

    {

        CancelRenderTasks(resetThumbnailToken: false);

        ClearRenderCache();

        ClearThumbnailCache();

        IsLoading = true;

        LoadingMessage = _localization.GetString("LoadingPdfOpen");



        try

        {

            await _pdfViewer.OpenFileAsync(path);



            await Application.Current.Dispatcher.InvokeAsync(() =>

            {

                HasDocument = _pdfViewer.HasDocument;

                PageCount = _pdfViewer.GetPageCount();

                CurrentPageIndex = initialPageIndex.HasValue
                    ? Math.Clamp(initialPageIndex.Value, 0, Math.Max(0, PageCount - 1))
                    : 0;

                Rotation = 0;

                SearchQuery = string.Empty;

                SearchResults.Clear();

                Bookmarks.Clear();

                Pages.Clear();



                for (var i = 0; i < PageCount; i++)

                {

                    Pages.Add(new PdfPageItemViewModel

                    {

                        PageIndex = i,

                        PageNumber = i + 1

                    });

                }



                IsFitToWidth = true;

                if (_viewerAreaWidth > ViewerHorizontalMargin)

                    ApplyFitToWidth();

                else

                {

                    _isUpdatingZoomProgrammatically = true;

                    ZoomLevel = 100;

                    _isUpdatingZoomProgrammatically = false;

                }



                UpdateAllPageLayoutSizes();

                NotifyNavigationCommands();

                (ToggleFitToWidthCommand as RelayCommand)?.NotifyCanExecuteChanged();

            });



            LoadingMessage = string.Format(_localization.GetString("LoadingPagesCount"), PageCount);



            var initialEnd = Math.Max(0, Math.Min(PageCount - 1, 2));

            _lastVisibleStart = 0;

            _lastVisibleEnd = initialEnd;

            await RenderPageRangeAsync(0, initialEnd);

            _renderZoomLevel = ZoomLevel;

            NotifyZoomFactorChanged();



            _ = LoadBookmarksAsync();

        }

        catch (Exception ex)

        {

            System.Diagnostics.Debug.WriteLine($"[Viewer] LoadDocument failed: {ex}");
            FileLog.Write($"[Viewer] LoadDocument failed: {path}", ex);

            await Application.Current.Dispatcher.InvokeAsync(() =>

            {

                HasDocument = false;

                PageCount = 0;

                Pages.Clear();

            });

            _toastService.Show(_localization.GetString("OpenPdfFailed"), ToastType.Error);

        }

        finally

        {

            IsLoading = false;

            LoadingMessage = _localization.GetString("LoadingMessage");

        }



        _ = GenerateThumbnailsAsync();

    }



    public void CloseDocument()

    {

        CancelRenderTasks();

        ClearRenderCache();

        ClearThumbnailCache();

        _pdfViewer.CloseFile();

        HasDocument = false;

        IsFitToWidth = false;

        IsLoading = false;

        PageCount = 0;

        CurrentPageIndex = 0;

        CurrentPageImage = null;

        SearchResults.Clear();

        Bookmarks.Clear();

        Pages.Clear();

        _lastVisibleStart = -1;

        _lastVisibleEnd = -1;

        _renderZoomLevel = 100;

        NotifyZoomFactorChanged();

        NotifyNavigationCommands();

        (ToggleFitToWidthCommand as RelayCommand)?.NotifyCanExecuteChanged();

    }



    public void Dispose()

    {

        CancelRenderTasks();

        ClearRenderCache();

        ClearThumbnailCache();

        _zoomDebounceTimer?.Stop();

        _pdfViewer.CloseFile();

        if (_pdfViewer is IDisposable disposable)

            disposable.Dispose();

    }



    public void SetViewerAreaWidth(double width)

    {

        if (Math.Abs(_viewerAreaWidth - width) < 1)

            return;



        _viewerAreaWidth = width;



        if (IsFitToWidth && HasDocument)

            ApplyFitToWidth();

    }



    public void SetZoomLevelManually(int zoom)

    {

        IsFitToWidth = false;

        ZoomLevel = Math.Clamp(zoom, 25, 500);

    }



    public void UpdateVisiblePageRange(int first, int last)

    {

        if (!HasDocument || Pages.Count == 0)

            return;



        first = Math.Max(0, first - VisiblePageBuffer);

        last = Math.Min(Pages.Count - 1, last + VisiblePageBuffer);



        if (first == _lastVisibleStart && last == _lastVisibleEnd)

            return;



        _lastVisibleStart = first;

        _lastVisibleEnd = last;

        _ = RenderPageRangeAsync(first, last);

        _ = LoadThumbnailsForRangeAsync(first, last);

    }



    public void RequestVisiblePageRender()

    {

        if (_lastVisibleStart < 0 || _lastVisibleEnd < 0)

        {

            var index = Math.Clamp(CurrentPageIndex, 0, Math.Max(0, Pages.Count - 1));

            UpdateVisiblePageRange(index, index);

            return;

        }



        _ = RenderPageRangeAsync(_lastVisibleStart, _lastVisibleEnd);

    }



    public void LoadThumbnailsForRange(int start, int end) =>

        _ = LoadThumbnailsForRangeAsync(start, end);



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

        if (IsFitToWidth)

            ApplyFitToWidth();

        else

            ScheduleRenderRefresh(immediate: true);

    }



    public void NavigateToPage(int pageIndex) => GoToPage(pageIndex);

    private void GoToPage(int pageIndex)
    {
        if (!HasDocument)
            return;

        var total = PageCount;
        if (total == 0)
            return;

        pageIndex = Math.Clamp(pageIndex, 0, total - 1);

        try
        {
            if (CurrentPageIndex == pageIndex)
            {
                if (!IsUpdatingFromScroll)
                    ScrollToPageRequested?.Invoke(this, pageIndex);

                RequestVisiblePageRender();
                return;
            }

            CurrentPageIndex = pageIndex;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Viewer] GoToPage failed: {ex}");
        }
    }

    public void GoToBookmark(BookmarkItem bookmark) => GoToPage(bookmark.PageIndex);

    private bool CanAddBookmark(int pageIndex) =>
        HasDocument && pageIndex >= 0 && pageIndex < PageCount;

    private async Task AddBookmarkAsync(int pageIndex)
    {
        if (!CanAddBookmark(pageIndex))
            return;

        var path = _pdfViewer.CurrentPath;
        if (path is null)
            return;

        var dialog = new BookmarkNameDialog
        {
            Owner = Application.Current.MainWindow,
            BookmarkTitle = $"페이지 {pageIndex + 1}"
        };

        if (dialog.ShowDialog() != true)
            return;

        var title = dialog.BookmarkTitle.Trim();
        if (string.IsNullOrWhiteSpace(title))
            return;

        try
        {
            IsLoading = true;
            await Task.Run(() => _pdfViewer.AddBookmark(path, title, pageIndex));

            var thumbnail = await Task.Run(() => _pdfViewer.RenderPageThumbnail(pageIndex, 80));
            if (thumbnail is BitmapSource bitmap)
                bitmap.Freeze();

            var newBookmark = new BookmarkItem
            {
                Title = title,
                PageIndex = pageIndex,
                Thumbnail = thumbnail
            };

            await Application.Current.Dispatcher.InvokeAsync(() => Bookmarks.Add(newBookmark));

            _toastService.Show("북마크가 추가되었습니다", ToastType.Success);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Add bookmark failed: {ex}");
            FileLog.Write($"[Viewer] Add bookmark failed: {path}", ex);
            _toastService.Show("북마크 추가에 실패했습니다", ToastType.Error);
        }
        finally
        {
            IsLoading = false;
            (AddBookmarkCommand as AsyncRelayCommand<int>)?.NotifyCanExecuteChanged();
        }
    }



    private async Task LoadBookmarksAsync()
    {
        var rawBookmarks = _pdfViewer.GetBookmarks();

        await Application.Current.Dispatcher.InvokeAsync(() => Bookmarks.Clear());

        foreach (var bookmark in rawBookmarks)
        {
            await PopulateBookmarkThumbnailAsync(bookmark);
            await Application.Current.Dispatcher.InvokeAsync(() => Bookmarks.Add(bookmark));
        }
    }

    private async Task PopulateBookmarkThumbnailAsync(BookmarkItem bookmark)
    {
        if (bookmark.Thumbnail is null && HasDocument)
        {
            var thumbnail = await Task.Run(() => _pdfViewer.RenderPageThumbnail(bookmark.PageIndex, 80));
            if (thumbnail is BitmapSource bitmap)
                bitmap.Freeze();
            bookmark.Thumbnail = thumbnail;
        }

        foreach (var child in bookmark.Children)
            await PopulateBookmarkThumbnailAsync(child);
    }



    private void ToggleFitToWidth()

    {

        if (IsFitToWidth)

        {

            IsFitToWidth = false;

            return;

        }



        IsFitToWidth = true;

        ApplyFitToWidth();

    }



    private void ApplyFitToWidth()

    {

        if (!HasDocument || _viewerAreaWidth <= ViewerHorizontalMargin)

            return;



        var pageWidth = GetPageWidthInPoints(CurrentPageIndex);

        if (pageWidth <= 0)

            return;



        var fitZoom = (int)Math.Round(

            (_viewerAreaWidth - ViewerHorizontalMargin) / pageWidth * 100.0 * 72.0 / 96.0);



        _isUpdatingZoomProgrammatically = true;

        ZoomLevel = Math.Clamp(fitZoom, 25, 500);

        _isUpdatingZoomProgrammatically = false;

    }



    private double GetPageWidthInPoints(int pageIndex)

    {

        var pageSize = _pdfViewer.GetPageSize(pageIndex);

        return Rotation is 90 or 270 ? pageSize.Height : pageSize.Width;

    }



    private void ExecuteSearch()

    {

        SearchResults.Clear();



        if (!HasDocument || string.IsNullOrWhiteSpace(SearchQuery))

        {

            UpdatePageHighlights();

            RequestVisiblePageRender();

            return;

        }



        foreach (var result in _pdfViewer.SearchText(SearchQuery))

            SearchResults.Add(result);



        UpdatePageHighlights();



        var first = SearchResults.FirstOrDefault();

        if (first is not null)

            CurrentPageIndex = first.PageIndex;



        RequestVisiblePageRender();

    }



    partial void OnCurrentPageIndexChanged(int value)

    {
        if (!_isClampingPageIndex)
        {
            if (!HasDocument || PageCount == 0)
            {
                if (value != 0)
                {
                    _isClampingPageIndex = true;
                    try
                    {
                        CurrentPageIndex = 0;
                    }
                    finally
                    {
                        _isClampingPageIndex = false;
                    }

                    return;
                }
            }
            else
            {
                var clamped = Math.Clamp(value, 0, PageCount - 1);
                if (clamped != value)
                {
                    _isClampingPageIndex = true;
                    try
                    {
                        CurrentPageIndex = clamped;
                    }
                    finally
                    {
                        _isClampingPageIndex = false;
                    }

                    return;
                }
            }
        }

        try
        {
            OnPropertyChanged(nameof(CurrentPageNumber));

            UpdatePageHighlights();

            SyncCurrentPageImage();

            if (!IsUpdatingFromScroll)
                ScrollToPageRequested?.Invoke(this, value);

            NotifyNavigationCommands();

            (AddBookmarkCommand as AsyncRelayCommand<int>)?.NotifyCanExecuteChanged();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Viewer] OnCurrentPageIndexChanged failed: {ex}");
        }

    }



    partial void OnZoomLevelChanged(int value)

    {

        if (!_isUpdatingZoomProgrammatically)

            IsFitToWidth = false;



        NotifyZoomFactorChanged();

        ScheduleRenderRefresh(immediate: false);

    }



    private void NotifyZoomFactorChanged() => OnPropertyChanged(nameof(ZoomFactor));



    partial void OnSearchQueryChanged(string value)

    {

        if (string.IsNullOrWhiteSpace(value))

        {

            SearchResults.Clear();

            UpdatePageHighlights();

            RequestVisiblePageRender();

        }

    }



    partial void OnHasDocumentChanged(bool value)

    {

        NotifyNavigationCommands();

        (ToggleFitToWidthCommand as RelayCommand)?.NotifyCanExecuteChanged();

        (AddBookmarkCommand as AsyncRelayCommand<int>)?.NotifyCanExecuteChanged();

    }



    private void UpdatePageHighlights()

    {

        foreach (var page in Pages)

            UpdatePageHighlightsForPage(page);

    }



    private void UpdatePageHighlightsForPage(PdfPageItemViewModel page)

    {
        page.HighlightRects.Clear();

        if (!HasDocument || page.PageIndex < 0 || page.PageIndex >= PageCount)
            return;

        var pageSize = _pdfViewer.GetPageSize(page.PageIndex);

        if (pageSize.Width <= 0 || page.DisplayWidth <= 0) return;



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



    private void ScheduleRenderRefresh(bool immediate)

    {

        if (!HasDocument)

            return;



        _zoomDebounceTimer ??= new DispatcherTimer();

        _zoomDebounceTimer.Stop();

        _zoomDebounceTimer.Interval = TimeSpan.FromMilliseconds(immediate ? 0 : ZoomDebounceMs);

        _zoomDebounceTimer.Tick -= OnZoomDebounceTick;

        _zoomDebounceTimer.Tick += OnZoomDebounceTick;

        _zoomDebounceTimer.Start();

    }



    private void OnZoomDebounceTick(object? sender, EventArgs e)

    {

        _zoomDebounceTimer?.Stop();

        if (!HasDocument)

            return;



        _renderZoomLevel = ZoomLevel;

        NotifyZoomFactorChanged();

        ClearRenderCache();

        UpdateAllPageLayoutSizes();

        RequestVisiblePageRender();

    }



    private async Task RenderPageRangeAsync(int first, int last)

    {

        if (!HasDocument || first > last)

            return;



        CancelPageRenderTasks();

        _renderCancellation = new CancellationTokenSource();

        var token = _renderCancellation.Token;

        var generation = _renderGeneration;

        var dpi = GetRenderDpi();

        var rotation = Rotation;



        for (var i = first; i <= last; i++)

        {

            if (token.IsCancellationRequested || generation != _renderGeneration)

                return;



            var index = i;

            if (index >= Pages.Count)

                return;



            if (TryGetCachedRender(index, dpi, rotation, out var cached))

            {

                await Application.Current.Dispatcher.InvokeAsync(() =>

                {

                    if (index >= Pages.Count)

                        return;



                    var page = Pages[index];

                    page.PageImage = cached;

                    page.DisplayWidth = cached!.Width;

                    page.DisplayHeight = cached.Height;

                    UpdatePageHighlightsForPage(page);

                    if (index == CurrentPageIndex)

                        SyncCurrentPageImage();

                }, DispatcherPriority.Background);



                continue;

            }



            try

            {

                var image = await Task.Run(() => _pdfViewer.RenderPage(index, dpi, rotation), token);

                if (token.IsCancellationRequested || generation != _renderGeneration)

                    return;



                if (image is BitmapSource bitmap && !bitmap.IsFrozen)

                    bitmap.Freeze();



                await Application.Current.Dispatcher.InvokeAsync(() =>

                {

                    if (token.IsCancellationRequested || index >= Pages.Count)

                        return;



                    var page = Pages[index];

                    page.PageImage = image;

                    page.DisplayWidth = image.Width;

                    page.DisplayHeight = image.Height;

                    AddToRenderCache(index, dpi, rotation, image);

                    UpdatePageHighlightsForPage(page);

                    if (index == CurrentPageIndex)

                        SyncCurrentPageImage();

                }, DispatcherPriority.Background);

            }

            catch (OperationCanceledException)

            {

                return;

            }

            catch

            {

                // Page render can fail during rapid zoom; skip until next refresh.

            }

        }

    }



    private async Task GenerateThumbnailsAsync()

    {

        if (!HasDocument)

            return;



        var token = _thumbnailCancellation?.Token ?? CancellationToken.None;



        var count = _pdfViewer.GetPageCount();

        System.Diagnostics.Debug.WriteLine($"[Thumbnails] GetPageCount returned: {count}");



        await Application.Current.Dispatcher.InvokeAsync(() =>

        {

            if (Pages.Count != count)

            {

                Pages.Clear();

                for (var i = 0; i < count; i++)

                {

                    Pages.Add(new PdfPageItemViewModel

                    {

                        PageIndex = i,

                        PageNumber = i + 1

                    });

                }

            }

        });



        await Application.Current.Dispatcher.InvokeAsync(() =>

            System.Diagnostics.Debug.Assert(

                Pages.Count == count,

                $"Expected {count} thumbnails, got {Pages.Count}"));



        // Render outward from the current page so the sidebar populates where
        // the user is looking first, instead of always starting at page 0.
        var pivot = Math.Clamp(CurrentPageIndex, 0, Math.Max(0, count - 1));

        foreach (var i in EnumerateOutward(pivot, count))

        {

            if (token.IsCancellationRequested)

                break;



            try

            {

                await LoadThumbnailAtIndexAsync(i, token);

            }

            catch (OperationCanceledException)

            {

                break;

            }

            catch (Exception ex)

            {

                System.Diagnostics.Debug.WriteLine($"[Thumbnails] Page {i} failed: {ex.Message}");

            }

        }



        var loaded = 0;

        await Application.Current.Dispatcher.InvokeAsync(() =>

            loaded = Pages.Count(p => p.Thumbnail is not null));



        System.Diagnostics.Debug.WriteLine($"[Thumbnails] Complete: {loaded}/{count} rendered");

    }



    private async Task LoadThumbnailsForRangeAsync(int start, int end)

    {

        if (!HasDocument)

            return;



        start = Math.Max(0, start);

        end = Math.Min(Pages.Count - 1, end);

        if (end < start)

            return;



        var token = _thumbnailCancellation?.Token ?? CancellationToken.None;



        try

        {

            var tasks = Enumerable.Range(start, end - start + 1)

                .Select(i => LoadThumbnailAtIndexAsync(i, token));

            await Task.WhenAll(tasks);

        }

        catch (OperationCanceledException)

        {

            return;

        }

    }



    private static IEnumerable<int> EnumerateOutward(int pivot, int count)
    {
        if (pivot >= 0 && pivot < count)
            yield return pivot;

        for (var offset = 1; offset < count; offset++)
        {
            var forward = pivot + offset;
            if (forward < count)
                yield return forward;

            var backward = pivot - offset;
            if (backward >= 0)
                yield return backward;
        }
    }

    private async Task LoadThumbnailAtIndexAsync(int index, CancellationToken token)

    {

        if (token.IsCancellationRequested || index < 0 || index >= Pages.Count)

            return;



        if (Pages[index].Thumbnail is not null)

            return;



        if (_thumbnailCache.TryGetValue(index, out var weakRef) &&

            weakRef.TryGetTarget(out var cachedThumbnail))

        {

            await Application.Current.Dispatcher.InvokeAsync(() =>

            {

                if (index < Pages.Count)

                    Pages[index].Thumbnail = cachedThumbnail;

            }, DispatcherPriority.Background);

            return;

        }



        await _thumbnailSemaphore.WaitAsync(token);

        try

        {

            if (token.IsCancellationRequested || index >= Pages.Count || Pages[index].Thumbnail is not null)

                return;



            var thumbnail = await Task.Run(

                () => _pdfViewer.RenderPageThumbnail(index, ThumbnailWidthPx),

                token);



            if (token.IsCancellationRequested)

                return;



            if (thumbnail is BitmapSource bitmap && !bitmap.IsFrozen)

                bitmap.Freeze();



            _thumbnailCache[index] = new WeakReference<ImageSource>(thumbnail);



            await Application.Current.Dispatcher.InvokeAsync(() =>

            {

                if (index < Pages.Count)

                    Pages[index].Thumbnail = thumbnail;

            }, DispatcherPriority.Background);

        }

        catch (OperationCanceledException)

        {

            throw;

        }

        catch (Exception ex)

        {

            System.Diagnostics.Debug.WriteLine($"[Thumbnails] Page {index} failed: {ex.Message}");

        }

        finally

        {

            _thumbnailSemaphore.Release();

        }

    }



    private void SyncCurrentPageImage()

    {

        if (!HasDocument || CurrentPageIndex < 0 || CurrentPageIndex >= Pages.Count)

        {

            CurrentPageImage = null;

            CurrentPageWidth = 0;

            CurrentPageHeight = 0;

            return;

        }



        var page = Pages[CurrentPageIndex];

        CurrentPageImage = page.PageImage;

        CurrentPageWidth = page.DisplayWidth;

        CurrentPageHeight = page.DisplayHeight;

    }



    private void UpdateAllPageLayoutSizes()

    {

        var dpi = GetRenderDpi();

        foreach (var page in Pages)

            SetPageLayoutSize(page, dpi);

    }



    private void SetPageLayoutSize(PdfPageItemViewModel page, int dpi)

    {

        var size = _pdfViewer.GetPageSize(page.PageIndex);

        if (size.Width <= 0)

            return;



        var width = size.Width * dpi / 72.0;

        var height = size.Height * dpi / 72.0;

        if (Rotation is 90 or 270)

            (width, height) = (height, width);



        page.DisplayWidth = width;

        page.DisplayHeight = height;

    }



    private bool TryGetCachedRender(int pageIndex, int dpi, int rotation, out ImageSource? image)

    {

        if (_renderCache.TryGetValue(pageIndex, out var entry) &&

            entry.Dpi == dpi &&

            entry.Rotation == rotation)

        {

            image = entry.Image;

            _renderCacheOrder.Remove(pageIndex);

            _renderCacheOrder.AddLast(pageIndex);

            return true;

        }



        image = null;

        return false;

    }



    private void AddToRenderCache(int pageIndex, int dpi, int rotation, ImageSource image)

    {

        _renderCache[pageIndex] = new RenderCacheEntry(dpi, rotation, image);

        _renderCacheOrder.Remove(pageIndex);

        _renderCacheOrder.AddLast(pageIndex);



        while (_renderCacheOrder.Count > MaxRenderCacheSize)

        {

            var oldest = _renderCacheOrder.First!.Value;

            _renderCacheOrder.RemoveFirst();

            _renderCache.Remove(oldest);

        }

    }



    private void ClearRenderCache()

    {

        _renderCache.Clear();

        _renderCacheOrder.Clear();

        _renderGeneration++;

    }



    private void ClearThumbnailCache() => _thumbnailCache.Clear();



    private void CancelPageRenderTasks()

    {

        _renderCancellation?.Cancel();

        _renderCancellation = null;

    }



    private void CancelThumbnailTasks(bool reset = true)

    {

        _thumbnailCancellation?.Cancel();

        if (reset)
            _thumbnailCancellation = new CancellationTokenSource();

    }



    private void CancelRenderTasks(bool resetThumbnailToken = true)

    {

        CancelPageRenderTasks();

        CancelThumbnailTasks(resetThumbnailToken);

    }



    private int GetRenderDpi() => Math.Clamp((int)(96 * ZoomLevel / 100.0), 72, 300);



    private void NotifyNavigationCommands()

    {

        (NextPageCommand as RelayCommand)?.NotifyCanExecuteChanged();

        (PrevPageCommand as RelayCommand)?.NotifyCanExecuteChanged();

    }



    private sealed record RenderCacheEntry(int Dpi, int Rotation, ImageSource Image);

}


