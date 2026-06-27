using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using HyoPDF.Core.Localization;
using HyoPDF.Core.Settings;
using HyoPDF.UI.Services;
using HyoPDF.UI.ViewModels;

namespace HyoPDF.UI.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly ILocalizationService _localization;
    private readonly IToastService _toastService;
    private WindowState _previousWindowState;
    private WindowStyle _previousWindowStyle;
    private ResizeMode _previousResizeMode = ResizeMode.CanResize;

    public MainWindow(
        MainViewModel viewModel,
        ILocalSettingsStore settingsStore,
        ILocalizationService localization,
        IToastService toastService)
    {
        InitializeComponent();
        _viewModel = viewModel;
        _localization = localization;
        _toastService = toastService;
        DataContext = viewModel;

        var size = settingsStore.Load().LastWindowSize;
        Width = size.Width;
        Height = size.Height;

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
        viewModel.PrintDialogRequested += (_, _) => ShowPrintDialog();
        viewModel.CompressDialogRequested += (_, _) => ShowCompressDialog();
        viewModel.SettingsDialogRequested += (_, _) => ShowSettingsDialog();

        PreviewKeyDown += OnPreviewKeyDown;
        Closing += OnClosing;
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
                    _viewModel.Page.DeleteSelectedCommand.Execute(null);
                    e.Handled = true;
                    return;
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

    private void OnClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        _viewModel.Settings.SaveWindowSize(ActualWidth, ActualHeight);
    }

    private void FocusSearchBox() => Toolbar.FocusSearch();

    private void ShowAbout()
    {
        var about = new AboutWindow { Owner = this };
        about.ShowDialog();
    }

    private void ShowMergeDialog()
    {
        var dialog = new MergeDialog(_viewModel.CurrentDocumentPath) { Owner = this };
        if (dialog.ShowDialog() != true || dialog.OutputPath is null)
            return;

        _viewModel.Page.MergeFiles(
            dialog.SelectedFiles,
            dialog.OutputPath,
            dialog.MergeIntoCurrent);

        _toastService.Show(_localization.GetString("MergeComplete"), ToastType.Success);
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

    private void ShowPrintDialog()
    {
        if (!_viewModel.Viewer.HasDocument)
            return;

        _viewModel.Print.PrepareForDialog(
            _viewModel.CurrentDocumentPath,
            _viewModel.Viewer.PageCount,
            _viewModel.Viewer.CurrentPageIndex);

        var dialog = new PrintDialog(_viewModel.Print) { Owner = this };
        dialog.ShowDialog();
    }

    private void ShowCompressDialog()
    {
        _viewModel.Compress.PrepareForDialog(_viewModel.CurrentDocumentPath);
        var dialog = new CompressDialog(_viewModel.Compress) { Owner = this };
        dialog.ShowDialog();
    }

    private void ShowSettingsDialog()
    {
        var dialog = new SettingsDialog(_viewModel.Settings) { Owner = this };
        dialog.ShowDialog();
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
}
