using System.IO;
using System.Windows;
using System.Windows.Media;
using HyoPDF.UI.ViewModels;

namespace HyoPDF.UI.Views;

public partial class CompressDialog : Window
{
    private readonly CompressViewModel _viewModel;

    private static readonly SolidColorBrush DropZoneBorderDefault = new(Color.FromRgb(0x33, 0x33, 0x33));
    private static readonly SolidColorBrush DropZoneBackgroundDefault = new(Color.FromRgb(0x16, 0x16, 0x16));
    private static readonly SolidColorBrush DropZoneBorderActive = new(Color.FromRgb(0x00, 0x78, 0xD4));
    private static readonly SolidColorBrush DropZoneBackgroundActive = new(Color.FromArgb(15, 0x00, 0x78, 0xD4));

    public CompressDialog(CompressViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        viewModel.CloseRequested += OnCloseRequested;
        Closed += (_, _) => viewModel.CloseRequested -= OnCloseRequested;
    }

    private void OnCloseRequested(object? sender, EventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnWindowDragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            SetDropZoneDragState(false);
            return;
        }

        var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
        if (files.Any(f => f.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)))
        {
            e.Effects = DragDropEffects.Copy;
            SetDropZoneDragState(true);
        }
        else
        {
            e.Effects = DragDropEffects.None;
            SetDropZoneDragState(false);
        }

        e.Handled = true;
    }

    private void OnWindowDragLeave(object sender, DragEventArgs e)
    {
        SetDropZoneDragState(false);
        e.Handled = true;
    }

    private void OnDropZoneDragLeave(object sender, DragEventArgs e)
    {
        if (sender is not DependencyObject source)
            return;

        var position = e.GetPosition(this);
        var hit = InputHitTest(position);
        if (hit is DependencyObject hitObject && IsDescendantOf(hitObject, source))
            return;

        SetDropZoneDragState(false);
        e.Handled = true;
    }

    private void OnWindowDrop(object sender, DragEventArgs e)
    {
        SetDropZoneDragState(false);

        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;

        var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
        var pdf = files.FirstOrDefault(f => f.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase));
        if (pdf is null)
            return;

        SetInputPdf(pdf);
        e.Handled = true;
    }

    private void SetInputPdf(string pdf)
    {
        _viewModel.InputPath = pdf;
        var directory = Path.GetDirectoryName(pdf);
        var name = Path.GetFileNameWithoutExtension(pdf);
        _viewModel.OutputPath = Path.Combine(directory ?? string.Empty, $"{name}_compressed.pdf");
    }

    private void SetDropZoneDragState(bool isActive)
    {
        if (DropZone.Visibility != Visibility.Visible)
            return;

        DropZoneRect.Stroke = isActive ? DropZoneBorderActive : DropZoneBorderDefault;
        DropZoneRect.Fill = isActive ? DropZoneBackgroundActive : DropZoneBackgroundDefault;
    }

    private static bool IsDescendantOf(DependencyObject? current, DependencyObject ancestor)
    {
        while (current is not null)
        {
            if (ReferenceEquals(current, ancestor))
                return true;

            current = VisualTreeHelper.GetParent(current);
        }

        return false;
    }
}
