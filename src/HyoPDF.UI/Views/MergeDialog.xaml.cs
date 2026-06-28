using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using HyoPDF.UI.ViewModels;

namespace HyoPDF.UI.Views;

public partial class MergeDialog : Window
{
    private readonly MergeViewModel _viewModel;
    private Point _dragStart;
    private int _dragIndex = -1;

    public MergeDialog(MergeViewModel viewModel)
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
        if (!HasPdfFiles(e))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.Copy;
        e.Handled = true;
    }

    private void OnWindowDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;

        var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
        _viewModel.AddFilesFromPaths(files);
        e.Handled = true;
    }

    private void OnListMouseDown(object sender, MouseButtonEventArgs e)
    {
        var listItem = FindAncestor<ListBoxItem>((DependencyObject)e.OriginalSource);
        if (listItem is not null)
        {
            _dragIndex = FileList.ItemContainerGenerator.IndexFromContainer(listItem);
            FileList.SelectedIndex = _dragIndex;
        }
        else
        {
            _dragIndex = -1;
        }

        _dragStart = e.GetPosition(FileList);
    }

    private void OnListMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragIndex < 0)
            return;

        var pos = e.GetPosition(FileList);
        if ((pos - _dragStart).Length <= 6)
            return;

        DragDrop.DoDragDrop(FileList, _dragIndex, DragDropEffects.Move);
        HideDropIndicator();
    }

    private void OnListDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(int)))
        {
            e.Effects = DragDropEffects.Move;
            ShowDropIndicator(GetDropIndex(e.GetPosition(FileList)));
            e.Handled = true;
            return;
        }

        if (HasPdfFiles(e))
        {
            e.Effects = DragDropEffects.Copy;
            HideDropIndicator();
            e.Handled = true;
            return;
        }

        e.Effects = DragDropEffects.None;
        e.Handled = true;
    }

    private void OnListDragLeave(object sender, DragEventArgs e)
    {
        HideDropIndicator();
        e.Handled = true;
    }

    private void OnListDrop(object sender, DragEventArgs e)
    {
        HideDropIndicator();

        if (e.Data.GetDataPresent(typeof(int)))
        {
            var source = (int)e.Data.GetData(typeof(int))!;
            var target = GetDropIndex(e.GetPosition(FileList));
            if (source >= 0 && target >= 0 && source != target)
                _viewModel.ReorderFile(source, target);

            e.Handled = true;
            return;
        }

        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
            _viewModel.AddFilesFromPaths(files);
            e.Handled = true;
        }
    }

    private int GetDropIndex(Point position)
    {
        for (var i = 0; i < FileList.Items.Count; i++)
        {
            if (FileList.ItemContainerGenerator.ContainerFromIndex(i) is not FrameworkElement element)
                continue;

            var top = element.TranslatePoint(new Point(0, 0), FileList).Y;
            var bottom = top + element.ActualHeight;
            if (position.Y < top + element.ActualHeight / 2)
                return i;
            if (position.Y <= bottom)
                return i + 1;
        }

        return FileList.Items.Count;
    }

    private void ShowDropIndicator(int index)
    {
        if (FileList.Items.Count == 0)
        {
            DropIndicator.Visibility = Visibility.Collapsed;
            return;
        }

        var clamped = Math.Clamp(index, 0, FileList.Items.Count);
        double y;

        if (clamped >= FileList.Items.Count)
        {
            if (FileList.ItemContainerGenerator.ContainerFromIndex(FileList.Items.Count - 1) is not FrameworkElement last)
            {
                DropIndicator.Visibility = Visibility.Collapsed;
                return;
            }

            y = last.TranslatePoint(new Point(0, last.ActualHeight), FileList).Y;
        }
        else if (FileList.ItemContainerGenerator.ContainerFromIndex(clamped) is FrameworkElement element)
        {
            y = element.TranslatePoint(new Point(0, 0), FileList).Y;
        }
        else
        {
            DropIndicator.Visibility = Visibility.Collapsed;
            return;
        }

        DropIndicator.Margin = new Thickness(8, y, 8, 0);
        DropIndicator.Visibility = Visibility.Visible;
    }

    private void HideDropIndicator()
    {
        DropIndicator.Visibility = Visibility.Collapsed;
    }

    private static bool HasPdfFiles(DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return false;

        var files = (string[])e.Data.GetData(DataFormats.FileDrop)!;
        return files.Any(f => f.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase));
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match)
                return match;

            current = VisualTreeHelper.GetParent(current);
        }

        return null;
    }
}
