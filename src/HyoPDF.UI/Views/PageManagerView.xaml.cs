using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using HyoPDF.UI.ViewModels;

namespace HyoPDF.UI.Views;

public partial class PageManagerView : UserControl
{
    private Point _dragStart;
    private bool _isDragging;
    private int _dragSourceIndex = -1;

    public PageManagerView()
    {
        InitializeComponent();
    }

    private MainViewModel? Vm => DataContext as MainViewModel;

    private void OnThumbnailMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement { Tag: int pageIndex } element || Vm is null)
            return;

        Vm.Page.SelectPage(pageIndex, Keyboard.Modifiers == ModifierKeys.Control, Keyboard.Modifiers == ModifierKeys.Shift);
        _dragStart = e.GetPosition(this);
        _dragSourceIndex = pageIndex;
        _isDragging = false;
        element.CaptureMouse();
    }

    private void OnThumbnailPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragSourceIndex < 0 || Vm is null)
            return;

        var pos = e.GetPosition(this);
        if (!_isDragging && (pos - _dragStart).Length > 6)
        {
            _isDragging = true;
            DragDrop.DoDragDrop((DependencyObject)sender, _dragSourceIndex, DragDropEffects.Move);
        }
    }

    private void OnThumbnailMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement element)
            element.ReleaseMouseCapture();
        _isDragging = false;
        _dragSourceIndex = -1;
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(typeof(int)))
        {
            e.Effects = DragDropEffects.Move;
            e.Handled = true;
        }
    }

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (Vm is null || !e.Data.GetDataPresent(typeof(int)))
            return;

        var sourceIndex = (int)e.Data.GetData(typeof(int))!;
        var targetIndex = GetDropTargetIndex(e.GetPosition(ThumbnailGrid));
        if (targetIndex < 0)
            return;

        if (!Vm.Page.SelectedPageIndices.Contains(sourceIndex))
        {
            Vm.Page.ClearSelection();
            Vm.Page.SelectPage(sourceIndex, ctrl: false, shift: false);
        }

        Vm.Page.MoveSelected(targetIndex);
        e.Handled = true;
    }

    private int GetDropTargetIndex(Point position)
    {
        for (var i = 0; i < ThumbnailGrid.Items.Count; i++)
        {
            if (ThumbnailGrid.ItemContainerGenerator.ContainerFromIndex(i) is not FrameworkElement element)
                continue;

            var topLeft = element.TranslatePoint(new Point(0, 0), ThumbnailGrid);
            var bounds = new Rect(topLeft, new Size(element.ActualWidth, element.ActualHeight));
            if (bounds.Contains(position))
                return i;
        }

        return ThumbnailGrid.Items.Count - 1;
    }
}
