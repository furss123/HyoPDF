using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HyoPDF.UI.ViewModels;

namespace HyoPDF.UI.Views;

public partial class ViewerView : UserControl
{
    private ViewerViewModel? _viewer;
    private MainViewModel? _mainViewModel;
    private bool _isScrollingProgrammatically;

    public ViewerView()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        DataContextChanged += OnDataContextChanged;
        PreviewMouseWheel += OnPreviewMouseWheel;
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
    }

    private void HookViewer(ViewerViewModel viewer)
    {
        _viewer = viewer;
        _viewer.ScrollToPageRequested -= OnScrollToPageRequested;
        _viewer.ScrollToPageRequested += OnScrollToPageRequested;
    }

    private void UnhookViewer(ViewerViewModel? viewer)
    {
        if (viewer is null)
            return;

        viewer.ScrollToPageRequested -= OnScrollToPageRequested;
    }

    private void OnScrollToPageRequested(object? sender, int pageIndex)
    {
        Dispatcher.BeginInvoke(() =>
        {
            if (pageIndex < 0 || pageIndex >= PagesItemsControl.Items.Count)
                return;

            _isScrollingProgrammatically = true;
            if (PagesItemsControl.ItemContainerGenerator.ContainerFromIndex(pageIndex) is FrameworkElement element)
                element.BringIntoView();
            _isScrollingProgrammatically = false;
        });
    }

    private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
    {
        if (_isScrollingProgrammatically || _viewer is null || !_viewer.HasDocument)
            return;

        var center = PageScrollViewer.VerticalOffset + PageScrollViewer.ViewportHeight / 2;
        var bestIndex = 0;
        var bestDistance = double.MaxValue;

        for (var i = 0; i < PagesItemsControl.Items.Count; i++)
        {
            if (PagesItemsControl.ItemContainerGenerator.ContainerFromIndex(i) is not FrameworkElement element)
                continue;

            var top = element.TranslatePoint(new Point(0, 0), PageScrollViewer).Y + PageScrollViewer.VerticalOffset;
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
    }

    private void OnPreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_viewer is null) return;
        if (Keyboard.Modifiers != ModifierKeys.Control) return;

        _viewer.ZoomLevel = e.Delta > 0
            ? Math.Min(_viewer.ZoomLevel + 10, 500)
            : Math.Max(_viewer.ZoomLevel - 10, 25);

        e.Handled = true;
    }
}
