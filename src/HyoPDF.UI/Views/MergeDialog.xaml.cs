using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Microsoft.Win32;

namespace HyoPDF.UI.Views;

public partial class MergeDialog : Window
{
    private readonly ObservableCollection<string> _files = [];
    private readonly string? _currentDocumentPath;
    private Point _dragStart;
    private int _dragIndex = -1;

    public IReadOnlyList<string> SelectedFiles => _files;
    public bool MergeIntoCurrent { get; private set; }
    public string? OutputPath { get; private set; }

    public MergeDialog(string? currentDocumentPath)
    {
        InitializeComponent();
        _currentDocumentPath = currentDocumentPath;
        FileList.ItemsSource = _files;

        if (!string.IsNullOrEmpty(currentDocumentPath) && File.Exists(currentDocumentPath))
            _files.Add(currentDocumentPath);
    }

    private void OnAddFiles(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog
        {
            Filter = "PDF files (*.pdf)|*.pdf",
            Multiselect = true
        };

        if (dialog.ShowDialog() != true) return;
        foreach (var file in dialog.FileNames)
        {
            if (!_files.Contains(file, StringComparer.OrdinalIgnoreCase))
                _files.Add(file);
        }
    }

    private void OnRemove(object sender, RoutedEventArgs e)
    {
        if (FileList.SelectedItem is string path)
            _files.Remove(path);
    }

    private void OnIncludeCurrent(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentDocumentPath) || !File.Exists(_currentDocumentPath))
            return;

        if (!_files.Contains(_currentDocumentPath, StringComparer.OrdinalIgnoreCase))
            _files.Insert(0, _currentDocumentPath);
    }

    private void OnMergeIntoCurrent(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_currentDocumentPath))
        {
            MessageBox.Show(this, "No document is open.", "Merge", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (_files.Count < 2)
        {
            MessageBox.Show(this, "Add at least two PDF files to merge.", "Merge", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (!_files.Contains(_currentDocumentPath, StringComparer.OrdinalIgnoreCase))
            _files.Add(_currentDocumentPath);

        MergeIntoCurrent = true;
        OutputPath = _currentDocumentPath;
        DialogResult = true;
    }

    private void OnMergeToFile(object sender, RoutedEventArgs e)
    {
        if (_files.Count < 1)
        {
            MessageBox.Show(this, "Add at least one PDF file.", "Merge", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Filter = "PDF files (*.pdf)|*.pdf",
            FileName = "merged.pdf"
        };

        if (dialog.ShowDialog() != true) return;

        OutputPath = dialog.FileName;
        MergeIntoCurrent = false;
        DialogResult = true;
    }

    private void OnListMouseDown(object sender, MouseButtonEventArgs e)
    {
        _dragIndex = FileList.SelectedIndex;
        _dragStart = e.GetPosition(FileList);
    }

    private void OnListMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || _dragIndex < 0) return;
        var pos = e.GetPosition(FileList);
        if ((pos - _dragStart).Length > 6)
            DragDrop.DoDragDrop(FileList, _dragIndex, DragDropEffects.Move);
    }

    private void OnListDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(int))) return;
        var source = (int)e.Data.GetData(typeof(int))!;
        var target = FileList.SelectedIndex;
        if (source < 0 || target < 0 || source == target) return;

        var item = _files[source];
        _files.RemoveAt(source);
        _files.Insert(target, item);
        FileList.SelectedIndex = target;
    }
}
