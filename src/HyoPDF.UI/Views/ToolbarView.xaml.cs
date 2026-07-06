using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using HyoPDF.UI.ViewModels;

namespace HyoPDF.UI.Views;

public partial class ToolbarView : UserControl
{
    private MainViewModel? _mainVm;
    private ViewerViewModel? _wiredViewer;
    private bool _isUpdatingPageInput;
    private bool _suppressLostFocusNavigation;

    public ToolbarView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    public void FocusSearch()
    {
        if (!SearchBox.IsEnabled)
            return;

        SearchBox.Focus();
        SearchBox.SelectAll();
    }

    private void SearchBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            GetActiveViewer()?.SearchCommand.Execute(null);
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            if (GetActiveViewer() is { } viewer)
                viewer.SearchQuery = string.Empty;
            Dispatcher.BeginInvoke(FocusViewerDeferred, DispatcherPriority.Input);
        }
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is MainViewModel oldVm)
            oldVm.PropertyChanged -= OnMainViewModelPropertyChanged;

        UnwireViewer();
        _mainVm = e.NewValue as MainViewModel;

        if (_mainVm is not null)
        {
            _mainVm.PropertyChanged += OnMainViewModelPropertyChanged;
            WireViewer(GetActiveViewer());
            RestorePageNumber();
        }
    }

    private void OnMainViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(MainViewModel.Viewer) or nameof(MainViewModel.HasOpenDocument))
        {
            WireViewer(GetActiveViewer());
            if (!PageInputBox.IsKeyboardFocused)
                RestorePageNumber();
        }
    }

    private void WireViewer(ViewerViewModel? viewer)
    {
        if (ReferenceEquals(_wiredViewer, viewer))
            return;

        UnwireViewer();
        _wiredViewer = viewer;

        if (_wiredViewer is not null)
            _wiredViewer.PropertyChanged += OnViewerPropertyChanged;
    }

    private void UnwireViewer()
    {
        if (_wiredViewer is null)
            return;

        _wiredViewer.PropertyChanged -= OnViewerPropertyChanged;
        _wiredViewer = null;
    }

    private ViewerViewModel? GetActiveViewer() => _mainVm?.ActiveTab?.Viewer;

    private void OnViewerPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ViewerViewModel.CurrentPageIndex)
            or nameof(ViewerViewModel.CurrentPageNumber)
            or nameof(ViewerViewModel.PageCount)
            or nameof(ViewerViewModel.HasDocument))
        {
            if (!PageInputBox.IsKeyboardFocused)
                RestorePageNumber();
        }
    }

    private void PageInputBox_PreviewTextInput(object sender, TextCompositionEventArgs e) =>
        e.Handled = e.Text.Any(c => !char.IsDigit(c));

    private void PageInputBox_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Escape))
            return;

        e.Handled = true;

        if (e.Key == Key.Enter)
            NavigateToInputPage(moveFocus: true);
        else
        {
            RestorePageNumber();
            Dispatcher.BeginInvoke(FocusViewerDeferred, DispatcherPriority.Input);
        }
    }

    private void PageInputBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Escape)
            e.Handled = true;
    }

    private void PageInputBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (_suppressLostFocusNavigation)
            return;

        NavigateToInputPage(moveFocus: false);
    }

    private void NavigateToInputPage(bool moveFocus)
    {
        if (_isUpdatingPageInput)
            return;

        var viewer = GetActiveViewer();
        if (viewer is null || !viewer.HasDocument || viewer.PageCount == 0)
        {
            RestorePageNumber();
            return;
        }

        try
        {
            var text = PageInputBox.Text?.Trim();
            if (string.IsNullOrEmpty(text) || !int.TryParse(text, out var pageNum))
            {
                RestorePageNumber();
                return;
            }

            var pageIndex = Math.Clamp(pageNum - 1, 0, viewer.PageCount - 1);
            viewer.NavigateToPage(pageIndex);

            if (Window.GetWindow(this) is MainWindow mainWindow)
                mainWindow.ScrollViewerToPage(pageIndex);

            _isUpdatingPageInput = true;
            try
            {
                PageInputBox.Text = (pageIndex + 1).ToString();
            }
            finally
            {
                _isUpdatingPageInput = false;
            }

            if (!moveFocus)
                return;

            _suppressLostFocusNavigation = true;
            Dispatcher.BeginInvoke(() =>
            {
                try
                {
                    FocusViewerDeferred();
                }
                finally
                {
                    _suppressLostFocusNavigation = false;
                }
            }, DispatcherPriority.Input);
        }
        catch
        {
            _suppressLostFocusNavigation = false;
            RestorePageNumber();
        }
    }

    private void FocusViewerDeferred()
    {
        if (Window.GetWindow(this) is MainWindow mainWindow)
            mainWindow.FocusViewerArea();
    }

    private void RestorePageNumber()
    {
        var viewer = GetActiveViewer();
        if (viewer is null)
            return;

        _isUpdatingPageInput = true;
        try
        {
            PageInputBox.Text = viewer.HasDocument && viewer.PageCount > 0
                ? (viewer.CurrentPageIndex + 1).ToString()
                : "1";
        }
        finally
        {
            _isUpdatingPageInput = false;
        }
    }

    private void ToolbarScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is not ScrollViewer sv)
            return;

        sv.ScrollToHorizontalOffset(sv.HorizontalOffset - e.Delta / 3.0);
        e.Handled = true;
    }
}
