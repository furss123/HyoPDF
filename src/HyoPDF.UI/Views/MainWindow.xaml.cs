using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using HyoPDF.Core.Localization;
using HyoPDF.Core.Settings;
using HyoPDF.UI.Helpers;
using HyoPDF.UI.Services;
using HyoPDF.UI.ViewModels;
using MaterialDesignThemes.Wpf;
using Microsoft.Extensions.DependencyInjection;

namespace HyoPDF.UI.Views;

public partial class MainWindow : Window
{
    private const double MinRestoreWidth = 580;
    private const double MaxRestoreWidth = 1600;
    private const double MinRestoreHeight = 440;
    private const double MaxRestoreHeight = 1200;
    private const int WmExitSizeMove = 0x0232;

    private readonly MainViewModel _viewModel;
    private readonly ILocalizationService _localization;
    private readonly IToastService _toastService;
    private readonly IServiceProvider _serviceProvider;
    private WindowState _previousWindowState;
    private WindowStyle _previousWindowStyle;
    private ResizeMode _previousResizeMode = ResizeMode.CanResize;
    private bool _userResizedThisSession;
    private double _savedSidebarWidth = 180;
    private bool _isApplyingSidebarLayout;

    public MainWindow(
        MainViewModel viewModel,
        ILocalSettingsStore settingsStore,
        ILocalizationService localization,
        IToastService toastService,
        IServiceProvider serviceProvider)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _localization = localization;
        _toastService = toastService;
        _serviceProvider = serviceProvider;
        DataContext = viewModel;

        var settings = settingsStore.Load();
        WindowState = WindowState.Normal;
        if (settings.UserResized)
        {
            var (width, height) = PrimaryMonitorPlacement.ClampSize(
                settings.LastWindowSize.Width,
                settings.LastWindowSize.Height);
            Width = Math.Min(width, MaxRestoreWidth);
            Height = Math.Min(height, MaxRestoreHeight);
        }
        else
        {
            var (width, height) = PrimaryMonitorPlacement.GetDefaultSize();
            Width = width;
            Height = height;
        }

        PrimaryMonitorPlacement.PlaceOnPrimaryMonitor(this);

        var toastHost = new ToastHost(toastService);
        Grid.SetRowSpan(toastHost, 4);
        Panel.SetZIndex(toastHost, 1000);
        RootGrid.Children.Add(toastHost);

        viewModel.FullscreenChanged += (_, fullscreen) => ApplyFullscreen(fullscreen);
        viewModel.ActiveTabContentChanged += (_, _) =>
        {
            if (viewModel.Viewer.IsFullscreen)
                ApplyFullscreen(true);
        };

        viewModel.ShowAboutRequested += (_, _) => ShowAbout();
        viewModel.FocusSearchRequested += (_, _) => FocusSearchBox();
        viewModel.MergeDialogRequested += (_, _) => ShowMergeDialog();
        viewModel.SplitDialogRequested += (_, _) => ShowSplitDialog();
        viewModel.CompressDialogRequested += (_, _) => ShowCompressDialog();
        viewModel.SettingsDialogRequested += (_, _) => ShowSettingsDialog();
        viewModel.PrintDialogRequested += (_, _) => ShowPrintDialog();

        viewModel.PropertyChanged += OnViewModelPropertyChanged;
        Loaded += (_, _) => ApplySidebarLayout(viewModel.IsSidebarExpanded);

        PreviewKeyDown += OnPreviewKeyDown;
        Closing += OnClosing;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.IsSidebarExpanded))
            ApplySidebarLayout(_viewModel.IsSidebarExpanded);
    }

    private void ToggleSidebar_Click(object sender, RoutedEventArgs e) =>
        _viewModel.IsSidebarExpanded = !_viewModel.IsSidebarExpanded;

    private void ApplySidebarLayout(bool expanded)
    {
        if (_isApplyingSidebarLayout)
            return;

        _isApplyingSidebarLayout = true;
        try
        {
            if (expanded)
            {
                SidebarColumn.MinWidth = 160;
                SidebarColumn.MaxWidth = 400;
                SidebarColumn.Width = new GridLength(Math.Clamp(_savedSidebarWidth, 160, 400));
                SidebarSplitterColumn.Width = new GridLength(4);
                SidebarGridSplitter.Visibility = Visibility.Visible;
                SidebarToggleIcon.Kind = PackIconKind.ChevronLeft;
                SidebarToggleButton.ToolTip = "사이드바 접기";
            }
            else
            {
                if (SidebarColumn.ActualWidth > 1)
                    _savedSidebarWidth = SidebarColumn.ActualWidth;

                SidebarColumn.MinWidth = 0;
                SidebarColumn.MaxWidth = double.PositiveInfinity;
                SidebarColumn.Width = new GridLength(0);
                SidebarSplitterColumn.Width = new GridLength(0);
                SidebarGridSplitter.Visibility = Visibility.Collapsed;
                SidebarToggleIcon.Kind = PackIconKind.ChevronRight;
                SidebarToggleButton.ToolTip = "사이드바 펼치기";
            }
        }
        finally
        {
            _isApplyingSidebarLayout = false;
        }
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (IsTextInputFocused(e.OriginalSource as DependencyObject))
            return;

        var modifiers = Keyboard.Modifiers;

        if (e.Key == Key.Escape)
        {
            _viewModel.EscapeCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (modifiers == (ModifierKeys.Control | ModifierKeys.Shift) && e.Key == Key.Tab)
        {
            _viewModel.PreviousTabCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (modifiers == ModifierKeys.Control)
        {
            switch (e.Key)
            {
                case Key.O:
                    _viewModel.OpenFileCommand.Execute(null);
                    e.Handled = true;
                    return;
                case Key.W:
                    _viewModel.CloseTabCommand.Execute(_viewModel.ActiveTab);
                    e.Handled = true;
                    return;
                case Key.Tab:
                    _viewModel.NextTabCommand.Execute(null);
                    e.Handled = true;
                    return;
                case Key.P:
                    _viewModel.ShowPrintCommand.Execute(null);
                    e.Handled = true;
                    return;
                case Key.F:
                    _viewModel.FocusSearchCommand.Execute(null);
                    e.Handled = true;
                    return;
                case Key.Z:
                    _viewModel.UndoCommand.Execute(null);
                    e.Handled = true;
                    return;
                case Key.Y:
                    _viewModel.RedoCommand.Execute(null);
                    e.Handled = true;
                    return;
                case Key.Add:
                case Key.OemPlus:
                    _viewModel.Viewer.ZoomInCommand.Execute(null);
                    e.Handled = true;
                    return;
                case Key.Subtract:
                case Key.OemMinus:
                    _viewModel.Viewer.ZoomOutCommand.Execute(null);
                    e.Handled = true;
                    return;
                case Key.D0:
                case Key.NumPad0:
                    _viewModel.Viewer.ResetZoomCommand.Execute(null);
                    e.Handled = true;
                    return;
                case Key.C:
                    if (TryExecutePageClipboardCommand(_viewModel.Page.CopySelectedCommand))
                    {
                        e.Handled = true;
                        return;
                    }
                    break;
                case Key.X:
                    if (TryExecutePageClipboardCommand(_viewModel.Page.CutSelectedCommand))
                    {
                        e.Handled = true;
                        return;
                    }
                    break;
                case Key.V:
                    if (ShouldHandlePageClipboardShortcuts() && _viewModel.Page.CanPaste)
                    {
                        _viewModel.Page.PasteCommand.Execute(null);
                        e.Handled = true;
                        return;
                    }
                    break;
            }
        }

        if (modifiers == ModifierKeys.None)
        {
            switch (e.Key)
            {
                case Key.F11:
                    _viewModel.Viewer.ToggleFullscreenCommand.Execute(null);
                    e.Handled = true;
                    return;
                case Key.Delete:
                    if (TryExecutePageClipboardCommand(_viewModel.Page.DeleteSelectedCommand))
                    {
                        e.Handled = true;
                        return;
                    }
                    break;
                case Key.Left:
                    _viewModel.Viewer.PrevPageCommand.Execute(null);
                    e.Handled = true;
                    return;
                case Key.Right:
                    _viewModel.Viewer.NextPageCommand.Execute(null);
                    e.Handled = true;
                    return;
            }
        }
    }

    private bool TryExecutePageClipboardCommand(ICommand command)
    {
        if (!ShouldHandlePageClipboardShortcuts())
            return false;

        if (!_viewModel.Viewer.HasDocument)
            return false;

        if (!command.CanExecute(null))
            return false;

        command.Execute(null);
        return true;
    }

    private bool ShouldHandlePageClipboardShortcuts()
    {
        if (_viewModel.Page.IsPanelOpen)
            return true;

        var focused = Keyboard.FocusedElement as DependencyObject;
        return focused is not null && IsDescendantOf(focused, SidebarArea);
    }

    private static bool IsDescendantOf(DependencyObject? node, DependencyObject ancestor)
    {
        while (node is not null)
        {
            if (ReferenceEquals(node, ancestor))
                return true;

            node = VisualTreeHelper.GetParent(node) as DependencyObject
                   ?? LogicalTreeHelper.GetParent(node) as DependencyObject;
        }

        return false;
    }

    private static bool IsTextInputFocused(DependencyObject? source)
    {
        for (var element = source; element is not null; element = VisualTreeHelper.GetParent(element))
        {
            if (element is TextBoxBase)
                return true;

            if (element is ComboBox { IsEditable: true })
                return true;
        }

        return false;
    }

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);
        if (PresentationSource.FromVisual(this) is HwndSource source)
            source.AddHook(WndProc);
    }

    private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
    {
        if (msg == WmExitSizeMove && WindowState == WindowState.Normal)
            _userResizedThisSession = true;

        return IntPtr.Zero;
    }

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_userResizedThisSession)
            _viewModel.Settings.SaveWindowSize(ActualWidth, ActualHeight, userResized: true);
    }

    private void FocusSearchBox() => Toolbar.FocusSearch();

    private void ShowAbout()
    {
        var about = new AboutWindow(_localization) { Owner = this };
        about.ShowDialog();
    }

    private void ShowMergeDialog()
    {
        var viewModel = _serviceProvider.GetRequiredService<MergeViewModel>();
        viewModel.PrepareForDialog(_viewModel.CurrentDocumentPath);
        var dialog = new MergeDialog(viewModel) { Owner = this };
        dialog.ShowDialog();
    }

    private void ShowSplitDialog()
    {
        var defaultDir = _viewModel.CurrentDocumentPath is { } path
            ? System.IO.Path.GetDirectoryName(path)
            : null;

        var dialog = new SplitDialog(defaultDir) { Owner = this };
        if (dialog.ShowDialog() != true)
            return;

        _viewModel.Page.SplitAtPoints(dialog.SplitPoints, dialog.OutputDirectory);
        _toastService.Show(_localization.GetString("SplitComplete"), ToastType.Success);
    }

    private void ShowCompressDialog()
    {
        _viewModel.Compress.PrepareForDialog(_viewModel.CurrentDocumentPath);
        var dialog = new CompressDialog(_viewModel.Compress) { Owner = this };
        dialog.ShowDialog();
    }

    private void ShowPrintDialog()
    {
        try
        {
            var path = _viewModel.CurrentDocumentPath;
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
            {
                _toastService.Show(_localization.GetString("PrintNoDocument"), ToastType.Error);
                return;
            }

            var printViewModel = _serviceProvider.GetRequiredService<PrintViewModel>();
            printViewModel.PrepareForDialog(path, _viewModel.Viewer.PageCount, _viewModel.Viewer.CurrentPageIndex);

            var dialog = new PrintDialog(printViewModel) { Owner = this };
            dialog.ShowDialog();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Print] Open dialog: {ex}");
            _toastService.Show(_localization.GetString("PrintError"), ToastType.Error);
        }
    }

    private void ShowSettingsDialog()
    {
        try
        {
            var dialog = new SettingsDialog(_viewModel.Settings) { Owner = this };
            dialog.ShowDialog();
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Settings] Open dialog: {ex}");
            _toastService.Show(_localization.GetString("SettingsOpenFailed"), ToastType.Error);
        }
    }

    private void ApplyFullscreen(bool fullscreen)
    {
        if (fullscreen)
        {
            _previousWindowState = WindowState;
            _previousWindowStyle = WindowStyle;
            _previousResizeMode = ResizeMode;
            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Maximized;
            ResizeMode = ResizeMode.NoResize;
        }
        else
        {
            WindowStyle = _previousWindowStyle;
            WindowState = _previousWindowState;
            ResizeMode = _previousResizeMode;
        }
    }

    public void OpenFileFromPath(string path) => _viewModel.OpenFileFromPath(path);

    public void OpenFileWhenReady(string path)
    {
        if (IsLoaded)
        {
            OpenFileFromPath(path);
            return;
        }

        RoutedEventHandler? handler = null;
        handler = (_, _) =>
        {
            Loaded -= handler!;
            OpenFileFromPath(path);
        };
        Loaded += handler;
    }

    public void FocusViewerArea() => ViewerArea.Focus();

    public void ScrollViewerToPage(int index) => ViewerViewControl.ScrollToPage(index);
}
