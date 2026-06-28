using System.ComponentModel;

using System.Windows;

using System.Windows.Controls;

using System.Windows.Input;

using System.Windows.Media;

using System.Windows.Media.Animation;

using System.Windows.Threading;

using HyoPDF.UI.ViewModels;



namespace HyoPDF.UI.Views;



public partial class ViewerView : UserControl

{

    private ViewerViewModel? _viewer;

    private MainViewModel? _mainViewModel;

    private bool _isScrollingProgrammatically;

    private DispatcherTimer? _scrollHideTimer;



    public ViewerView()

    {

        InitializeComponent();

        Loaded += OnLoaded;

        DataContextChanged += OnDataContextChanged;

        PreviewMouseWheel += OnPreviewMouseWheel;

        SizeChanged += OnSizeChanged;

    }



    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)

    {

        if (e.OldValue is MainViewModel oldVm)

        {

            oldVm.PropertyChanged -= OnMainViewModelPropertyChanged;

            UnhookViewer(oldVm.Viewer);

        }



        if (e.NewValue is MainViewModel newVm)

        {

            _mainViewModel = newVm;

            newVm.PropertyChanged += OnMainViewModelPropertyChanged;

            HookViewer(newVm.Viewer);

        }

    }



    private void OnMainViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)

    {

        if (e.PropertyName != nameof(MainViewModel.Viewer) || sender is not MainViewModel vm)

            return;



        UnhookViewer(_viewer);

        HookViewer(vm.Viewer);

    }



    private void OnLoaded(object sender, RoutedEventArgs e)

    {

        if (DataContext is MainViewModel vm)

        {

            _mainViewModel = vm;

            vm.PropertyChanged -= OnMainViewModelPropertyChanged;

            vm.PropertyChanged += OnMainViewModelPropertyChanged;

            HookViewer(vm.Viewer);

        }



        UpdateViewerAreaWidth();

        Dispatcher.BeginInvoke(RequestVisiblePages, System.Windows.Threading.DispatcherPriority.Loaded);

    }



    private void OnSizeChanged(object sender, SizeChangedEventArgs e) =>

        UpdateViewerAreaWidth();



    private void UpdateViewerAreaWidth()

    {

        if (_viewer is null || ActualWidth <= 0)

            return;



        _viewer.SetViewerAreaWidth(ActualWidth);

    }



    private void HookViewer(ViewerViewModel viewer)

    {

        _viewer = viewer;

        _viewer.ScrollToPageRequested -= OnScrollToPageRequested;

        _viewer.ScrollToPageRequested += OnScrollToPageRequested;

        UpdateViewerAreaWidth();

        Dispatcher.BeginInvoke(RequestVisiblePages, System.Windows.Threading.DispatcherPriority.Loaded);

    }



    private void UnhookViewer(ViewerViewModel? viewer)

    {

        if (viewer is null)

            return;



        viewer.ScrollToPageRequested -= OnScrollToPageRequested;

    }



    private void OnScrollToPageRequested(object? sender, int pageIndex) =>
        ScrollToPage(pageIndex);

    public void ScrollToPage(int pageIndex) => ScrollToPageIndex(pageIndex);

    private void ScrollToPageIndex(int pageIndex)
    {
        if (_viewer is null || pageIndex < 0 || pageIndex >= PagesList.Items.Count)
            return;

        void DoScroll()
        {
            try
            {
                _isScrollingProgrammatically = true;
                _viewer.IsUpdatingFromScroll = true;

                PagesList.ScrollIntoView(PagesList.Items[pageIndex]);
                PagesList.UpdateLayout();

                var scrollViewer = FindVisualChild<ScrollViewer>(PagesList);
                if (scrollViewer is not null &&
                    PagesList.ItemContainerGenerator.ContainerFromIndex(pageIndex) is FrameworkElement element)
                {
                    var top = element.TranslatePoint(new Point(0, 0), scrollViewer).Y + scrollViewer.VerticalOffset;
                    scrollViewer.ScrollToVerticalOffset(Math.Max(0, top - 24));
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Viewer] ScrollToPage failed: {ex}");
            }

            Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    RequestVisiblePages();
                }
                finally
                {
                    _isScrollingProgrammatically = false;
                    if (_viewer is not null)
                        _viewer.IsUpdatingFromScroll = false;
                }
            }, DispatcherPriority.ApplicationIdle);
        }

        if (Dispatcher.CheckAccess())
            DoScroll();
        else
            Dispatcher.BeginInvoke(DoScroll, DispatcherPriority.Loaded);
    }



    private void OnScrollChanged(object sender, ScrollChangedEventArgs e)

    {

        if (_isScrollingProgrammatically || _viewer is null || !_viewer.HasDocument)

            return;



        var scrollViewer = sender as ScrollViewer ?? FindVisualChild<ScrollViewer>(PagesList);

        if (scrollViewer is null)

            return;



        var center = scrollViewer.VerticalOffset + scrollViewer.ViewportHeight / 2;

        var bestIndex = 0;

        var bestDistance = double.MaxValue;



        for (var i = 0; i < PagesList.Items.Count; i++)

        {

            if (PagesList.ItemContainerGenerator.ContainerFromIndex(i) is not FrameworkElement element)

                continue;



            var top = element.TranslatePoint(new Point(0, 0), scrollViewer).Y + scrollViewer.VerticalOffset;

            var pageCenter = top + element.ActualHeight / 2;

            var distance = Math.Abs(pageCenter - center);

            if (distance < bestDistance)

            {

                bestDistance = distance;

                bestIndex = i;

            }

        }



        if (_viewer.CurrentPageIndex != bestIndex)

        {

            _viewer.IsUpdatingFromScroll = true;

            _viewer.CurrentPageIndex = bestIndex;

            _viewer.IsUpdatingFromScroll = false;

        }



        RequestVisiblePages();

        UpdateScrollPageIndicator(scrollViewer);

        ShowScrollIndicator();

    }



    private void ShowScrollIndicator()

    {

        if (_viewer is not { HasDocument: true })

            return;



        ScrollPageIndicator.Visibility = Visibility.Visible;

        ScrollPageIndicator.BeginAnimation(

            UIElement.OpacityProperty,

            new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(150))

            {

                FillBehavior = FillBehavior.HoldEnd

            });



        _scrollHideTimer?.Stop();

        _scrollHideTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.5) };

        _scrollHideTimer.Tick += OnScrollHideTimerTick;

        _scrollHideTimer.Start();

    }



    private void OnScrollHideTimerTick(object? sender, EventArgs e)

    {

        _scrollHideTimer?.Stop();



        var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(200))

        {

            FillBehavior = FillBehavior.HoldEnd

        };

        fadeOut.Completed += (_, _) => ScrollPageIndicator.Visibility = Visibility.Collapsed;

        ScrollPageIndicator.BeginAnimation(UIElement.OpacityProperty, fadeOut);

    }



    private void UpdateScrollPageIndicator(ScrollViewer scrollViewer)

    {

        if (_viewer is not { HasDocument: true })

            return;



        var currentPage = _viewer.CurrentPageIndex;

        var totalPages = _viewer.PageCount;

        ScrollPageText.Text = $"{currentPage + 1} / {totalPages} 페이지";



        var ratio = scrollViewer.VerticalOffset / Math.Max(1, scrollViewer.ScrollableHeight);

        var maxTop = Math.Max(0, scrollViewer.ActualHeight - 60);

        ScrollPageIndicator.Margin = new Thickness(0, ratio * maxTop, 20, 0);

    }



    private void RequestVisiblePages()

    {

        if (_viewer is null || !_viewer.HasDocument || PagesList.Items.Count == 0)

            return;



        var scrollViewer = FindVisualChild<ScrollViewer>(PagesList);

        if (scrollViewer is null)

        {

            _viewer.UpdateVisiblePageRange(0, Math.Min(2, PagesList.Items.Count - 1));

            return;

        }



        var (first, last) = GetVisiblePageRange(scrollViewer);

        _viewer.UpdateVisiblePageRange(first, last);

    }



    private (int first, int last) GetVisiblePageRange(ScrollViewer scrollViewer)

    {

        var top = scrollViewer.VerticalOffset;

        var bottom = top + scrollViewer.ViewportHeight;

        var first = 0;

        var last = 0;

        var foundFirst = false;



        for (var i = 0; i < PagesList.Items.Count; i++)

        {

            if (PagesList.ItemContainerGenerator.ContainerFromIndex(i) is not FrameworkElement element)

                continue;



            var itemTop = element.TranslatePoint(new Point(0, 0), scrollViewer).Y;

            var itemBottom = itemTop + element.ActualHeight;



            if (!foundFirst && itemBottom >= top)

            {

                first = i;

                foundFirst = true;

            }



            if (itemTop <= bottom)

                last = i;

        }



        if (!foundFirst)

            last = Math.Max(0, PagesList.Items.Count - 1);



        return (first, last);

    }



    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)

    {

        if (_viewer is null) return;

        if (Keyboard.Modifiers != ModifierKeys.Control) return;



        _viewer.SetZoomLevelManually(e.Delta > 0

            ? Math.Min(_viewer.ZoomLevel + 10, 500)

            : Math.Max(_viewer.ZoomLevel - 10, 25));



        e.Handled = true;

    }



    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject

    {

        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)

        {

            var child = VisualTreeHelper.GetChild(parent, i);

            if (child is T match)

                return match;



            var descendant = FindVisualChild<T>(child);

            if (descendant is not null)

                return descendant;

        }



        return null;

    }

}


