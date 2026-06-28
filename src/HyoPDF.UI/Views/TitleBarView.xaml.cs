using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HyoPDF.UI.ViewModels;

namespace HyoPDF.UI.Views;

public partial class TitleBarView : UserControl
{
    public TitleBarView()
    {
        InitializeComponent();
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;

        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;

        var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
        var pdf = files.FirstOrDefault(f => f.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase));
        if (pdf is null)
            return;

        if (DataContext is MainViewModel vm)
            vm.OpenNewTab(pdf);

        e.Handled = true;
    }

    private void MinimizeClick(object sender, RoutedEventArgs e) =>
        Window.GetWindow(this)!.WindowState = WindowState.Minimized;

    private void MaximizeClick(object sender, RoutedEventArgs e) =>
        Window.GetWindow(this)?.ToggleMaximize();

    private void CloseClick(object sender, RoutedEventArgs e) =>
        Window.GetWindow(this)?.Close();
}

internal static class WindowExtensions
{
    public static void ToggleMaximize(this Window window)
    {
        window.WindowState = window.WindowState == WindowState.Maximized
            ? WindowState.Normal
            : WindowState.Maximized;
    }
}
