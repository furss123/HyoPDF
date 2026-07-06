using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HyoPDF.Core.Models;
using HyoPDF.UI.ViewModels;

namespace HyoPDF.UI.Views;

public partial class SidebarView : UserControl
{
    private ViewerViewModel? _viewer;
    private int _lastSelectedIndex = -1;

    public SidebarView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
    }

    private MainViewModel? ViewModel => DataContext as MainViewModel;

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is MainViewModel oldVm)
            oldVm.PropertyChanged -= OnMainViewModelPropertyChanged;

        if (e.NewValue is MainViewModel newVm)
        {
            newVm.PropertyChanged += OnMainViewModelPropertyChanged;
            HookViewer(newVm.Viewer);
        }
    }

    private void OnMainViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.Viewer) && sender is MainViewModel vm)
        {
            _lastSelectedIndex = -1;
            HookViewer(vm.Viewer);
        }
    }

    private void HookViewer(ViewerViewModel viewer)
    {
        if (_viewer is not null)
        {
            _viewer.PropertyChanged -= OnViewerPropertyChanged;
            _viewer.Pages.CollectionChanged -= OnThumbnailPagesChanged;
        }

        _viewer = viewer;
        _viewer.PropertyChanged += OnViewerPropertyChanged;
        _viewer.Pages.CollectionChanged += OnThumbnailPagesChanged;
    }

    private void OnViewerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewerViewModel.HasDocument) && _viewer is { HasDocument: false })
            _lastSelectedIndex = -1;
    }

    private void OnThumbnailPagesChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (ViewModel is not null)
            HookViewer(ViewModel.Viewer);
    }

    private void OnThumbnailScrollChanged(object sender, ScrollChangedEventArgs e) =>
        RequestVisibleThumbnails();

    private void ThumbnailScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (Keyboard.Modifiers == ModifierKeys.Control && ViewModel is not null)
        {
            e.Handled = true;
            var delta = e.Delta > 0 ? 16 : -16;
            ViewModel.ThumbnailSize = Math.Clamp(ViewModel.ThumbnailSize + delta, 60, 240);
            return;
        }

        ThumbnailScrollViewer.ScrollToVerticalOffset(
            ThumbnailScrollViewer.VerticalOffset - e.Delta / 3.0);
        e.Handled = true;
    }

    private void OnBookmarkSelected(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is BookmarkItem bookmark)
            ViewModel?.Viewer.GoToBookmark(bookmark);
    }

    private void ThumbnailItem_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: PdfPageItemViewModel item } || ViewModel is null)
            return;

        var allItems = ViewModel.Viewer.Pages;

        if (Keyboard.Modifiers == ModifierKeys.Control)
        {
            item.IsSelected = !item.IsSelected;
        }
        else if (Keyboard.Modifiers == ModifierKeys.Shift && _lastSelectedIndex >= 0)
        {
            var start = Math.Min(_lastSelectedIndex, item.PageIndex);
            var end = Math.Max(_lastSelectedIndex, item.PageIndex);
            foreach (var page in allItems)
                page.IsSelected = page.PageIndex >= start && page.PageIndex <= end;
        }
        else
        {
            foreach (var page in allItems)
                page.IsSelected = false;

            item.IsSelected = true;
            ViewModel.Viewer.CurrentPageIndex = item.PageIndex;
        }

        _lastSelectedIndex = item.PageIndex;
        SyncSelectedToPageVm();

        if (item.IsSelected)
            ViewModel.Viewer.GoToPageCommand.Execute(item.PageIndex);
    }

    private void ThumbnailItem_RightClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: PdfPageItemViewModel item } || ViewModel is null)
            return;

        if (!item.IsSelected)
        {
            foreach (var page in ViewModel.Viewer.Pages)
                page.IsSelected = false;

            item.IsSelected = true;
            _lastSelectedIndex = item.PageIndex;
            SyncSelectedToPageVm();
        }

        ViewModel.Viewer.CurrentPageIndex = item.PageIndex;
    }

    private void SyncSelectedToPageVm()
    {
        if (ViewModel?.Page is null)
            return;

        ViewModel.Page.SelectedPageIndices.Clear();
        foreach (var page in ViewModel.Viewer.Pages.Where(p => p.IsSelected))
            ViewModel.Page.SelectedPageIndices.Add(page.PageIndex);

        ViewModel.Page.RefreshSelectionCommands();
    }

    private void SidebarScrollViewer_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer sv)
            return;

        sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta / 3.0);
        e.Handled = true;
    }

    private void RequestVisibleThumbnails()
    {
        if (ViewModel?.Viewer is not { HasDocument: true } viewer || viewer.Pages.Count == 0)
            return;

        var viewport = ThumbnailScrollViewer.ViewportHeight;
        if (viewport <= 0)
        {
            viewer.LoadThumbnailsForRange(0, viewer.Pages.Count - 1);
            return;
        }

        var (first, last) = GetVisibleThumbnailRange();
        viewer.LoadThumbnailsForRange(first, last);
    }

    private (int first, int last) GetVisibleThumbnailRange()
    {
        var top = ThumbnailScrollViewer.VerticalOffset;
        var bottom = top + ThumbnailScrollViewer.ViewportHeight;
        var first = 0;
        var last = 0;
        var foundFirst = false;

        for (var i = 0; i < ThumbnailItemsControl.Items.Count; i++)
        {
            if (ThumbnailItemsControl.ItemContainerGenerator.ContainerFromIndex(i) is not FrameworkElement element)
                continue;

            var itemTop = element.TranslatePoint(new Point(0, 0), ThumbnailScrollViewer).Y;
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
            return (0, Math.Max(0, ThumbnailItemsControl.Items.Count - 1));

        return (first, last);
    }
}
