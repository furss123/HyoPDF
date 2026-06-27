using System.Collections.ObjectModel;
using System.IO;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HyoPDF.Core.Localization;
using HyoPDF.Core.PageOperations;
using HyoPDF.Core.Services;
using HyoPDF.UI.Services;
using Microsoft.Win32;

namespace HyoPDF.UI.ViewModels;

public partial class PageViewModel : ObservableObject
{
    private readonly IPageService _pageService;
    private readonly IPdfViewerService _pdfViewer;
    private readonly ViewerViewModel _viewer;
    private readonly IToastService _toastService;
    private readonly ILocalizationService _localization;
    private readonly PageUndoRedoStack _undoStack = new();

    private int _lastSelectedIndex = -1;

    [ObservableProperty]
    private bool _isPanelOpen;

    public ObservableCollection<int> SelectedPageIndices { get; } = [];

    public PageUndoRedoStack UndoStack => _undoStack;

    public ICommand DeleteSelectedCommand { get; }
    public ICommand CopySelectedCommand { get; }
    public ICommand MoveSelectedCommand { get; }
    public ICommand RotateSelectedCommand { get; }
    public ICommand RotateSelected90Command { get; }
    public ICommand RotateSelected180Command { get; }
    public ICommand RotateSelected270Command { get; }
    public ICommand ExtractSelectedCommand { get; }
    public ICommand MergeCommand { get; }
    public ICommand SplitCommand { get; }
    public ICommand UndoCommand { get; }
    public ICommand RedoCommand { get; }
    public ICommand TogglePanelCommand { get; }

    public event EventHandler? MergeDialogRequested;
    public event EventHandler? SplitDialogRequested;

    public PageViewModel(
        IPageService pageService,
        IPdfViewerService pdfViewer,
        ViewerViewModel viewer,
        IToastService toastService,
        ILocalizationService localization)
    {
        _pageService = pageService;
        _pdfViewer = pdfViewer;
        _viewer = viewer;
        _toastService = toastService;
        _localization = localization;

        DeleteSelectedCommand = new RelayCommand(DeleteSelected, CanOperateOnSelection);
        CopySelectedCommand = new RelayCommand<int?>(CopySelected, _ => CanOperateOnSelection());
        MoveSelectedCommand = new RelayCommand<int>(MoveSelected, _ => CanOperateOnSelection());
        RotateSelectedCommand = new RelayCommand<int>(RotateSelected, _ => CanOperateOnSelection());
        RotateSelected90Command = new RelayCommand(() => RotateSelected(90), CanOperateOnSelection);
        RotateSelected180Command = new RelayCommand(() => RotateSelected(180), CanOperateOnSelection);
        RotateSelected270Command = new RelayCommand(() => RotateSelected(270), CanOperateOnSelection);
        ExtractSelectedCommand = new RelayCommand(ExtractSelected, CanOperateOnSelection);
        MergeCommand = new RelayCommand(ShowMergeDialog, () => _viewer.HasDocument);
        SplitCommand = new RelayCommand(ShowSplitDialog, () => _viewer.HasDocument);
        UndoCommand = new RelayCommand(Undo, () => _undoStack.CanUndo);
        RedoCommand = new RelayCommand(Redo, () => _undoStack.CanRedo);
        TogglePanelCommand = new RelayCommand(() => IsPanelOpen = !IsPanelOpen);

        _undoStack.StackChanged += (_, _) =>
        {
            (UndoCommand as RelayCommand)?.NotifyCanExecuteChanged();
            (RedoCommand as RelayCommand)?.NotifyCanExecuteChanged();
        };
    }

    public void SyncSelectionFromPages()
    {
        SelectedPageIndices.Clear();
        foreach (var page in _viewer.Pages.Where(p => p.IsSelected))
            SelectedPageIndices.Add(page.PageIndex);
    }

    public void SelectPage(int pageIndex, bool ctrl, bool shift)
    {
        if (!_viewer.HasDocument) return;

        if (shift && _lastSelectedIndex >= 0)
        {
            var start = Math.Min(_lastSelectedIndex, pageIndex);
            var end = Math.Max(_lastSelectedIndex, pageIndex);
            if (!ctrl)
                ClearSelection();

            for (var i = start; i <= end; i++)
                SetPageSelected(i, true);
        }
        else if (ctrl)
        {
            TogglePageSelected(pageIndex);
            _lastSelectedIndex = pageIndex;
        }
        else
        {
            ClearSelection();
            SetPageSelected(pageIndex, true);
            _lastSelectedIndex = pageIndex;
        }

        SyncSelectionFromPages();
    }

    public void ClearSelection()
    {
        foreach (var page in _viewer.Pages)
            page.IsSelected = false;
        SelectedPageIndices.Clear();
        _lastSelectedIndex = -1;
    }

    private void SetPageSelected(int pageIndex, bool selected)
    {
        var page = _viewer.Pages.FirstOrDefault(p => p.PageIndex == pageIndex);
        if (page is not null)
            page.IsSelected = selected;
    }

    private void TogglePageSelected(int pageIndex)
    {
        var page = _viewer.Pages.FirstOrDefault(p => p.PageIndex == pageIndex);
        if (page is null) return;
        page.IsSelected = !page.IsSelected;
    }

    private bool CanOperateOnSelection() =>
        _viewer.HasDocument && SelectedPageIndices.Count > 0;

    private string? CurrentPath => _pdfViewer.CurrentPath;

    private void ExecuteWithUndo(string description, Action operation)
    {
        var path = CurrentPath;
        if (path is null || !File.Exists(path)) return;

        var undoSnapshot = PageFileHelper.CreateSnapshot(path);
        _pdfViewer.CloseFile();
        operation();
        var redoSnapshot = PageFileHelper.CreateSnapshot(path);

        _undoStack.Push(new PageSnapshotCommand(description, path, undoSnapshot, redoSnapshot, () => ReloadDocument(path)));
        ReloadDocument(path);
        NotifyOperationCommands();
    }

    private void ReloadDocument(string? path = null)
    {
        path ??= CurrentPath;
        if (path is null) return;

        var pageIndex = _viewer.CurrentPageIndex;
        _viewer.LoadDocument(path);
        if (pageIndex < _viewer.PageCount)
            _viewer.CurrentPageIndex = pageIndex;

        ClearSelection();
    }

    private void DeleteSelected()
    {
        var indices = SelectedPageIndices.ToList();
        ExecuteWithUndo("Delete pages", () =>
            _pageService.DeletePages(CurrentPath!, indices));
    }

    private void CopySelected(int? insertAt)
    {
        var indices = SelectedPageIndices.OrderBy(i => i).ToList();
        var target = insertAt ?? (_viewer.PageCount);
        ExecuteWithUndo("Copy pages", () =>
            _pageService.CopyPages(CurrentPath!, indices, target));
    }

    public void MoveSelected(int targetIndex)
    {
        var indices = SelectedPageIndices.OrderBy(i => i).ToList();
        ExecuteWithUndo("Move pages", () =>
            _pageService.MovePages(CurrentPath!, indices, targetIndex));
    }

    private void RotateSelected(int rotation)
    {
        var indices = SelectedPageIndices.ToList();
        ExecuteWithUndo($"Rotate {rotation}°", () =>
            _pageService.RotatePages(CurrentPath!, indices, rotation));
    }

    private void ExtractSelected()
    {
        var path = CurrentPath;
        if (path is null) return;

        var dialog = new SaveFileDialog
        {
            Filter = "PDF files (*.pdf)|*.pdf",
            FileName = $"{Path.GetFileNameWithoutExtension(path)}_extract.pdf"
        };

        if (dialog.ShowDialog() != true) return;

        var indices = SelectedPageIndices.OrderBy(i => i).ToList();
        _pageService.ExtractPages(path, indices, dialog.FileName);
        _toastService.Show(_localization.GetString("ExportComplete"), ToastType.Success);
    }

    public void MergeFiles(IReadOnlyList<string> paths, string outputPath, bool intoCurrent)
    {
        if (paths.Count == 0) return;

        if (intoCurrent)
        {
            var current = CurrentPath;
            if (current is null) return;
            ExecuteWithUndo("Merge PDFs", () => _pageService.MergePdfs(paths.ToList(), current));
            return;
        }

        _pageService.MergePdfs(paths.ToList(), outputPath);
    }

    public void SplitAtPoints(IReadOnlyList<int> splitPoints, string outputDir)
    {
        var path = CurrentPath;
        if (path is null) return;
        _pageService.SplitPdf(path, splitPoints.ToList(), outputDir);
    }

    private void ShowMergeDialog() => MergeDialogRequested?.Invoke(this, EventArgs.Empty);
    private void ShowSplitDialog() => SplitDialogRequested?.Invoke(this, EventArgs.Empty);

    private void Undo()
    {
        _undoStack.Undo();
        NotifyOperationCommands();
    }

    private void Redo()
    {
        _undoStack.Redo();
        NotifyOperationCommands();
    }

    public void OnDocumentClosed() => _undoStack.Clear();

    private void NotifyOperationCommands()
    {
        (DeleteSelectedCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (CopySelectedCommand as RelayCommand<int?>)?.NotifyCanExecuteChanged();
        (MoveSelectedCommand as RelayCommand<int>)?.NotifyCanExecuteChanged();
        (RotateSelectedCommand as RelayCommand<int>)?.NotifyCanExecuteChanged();
        (RotateSelected90Command as RelayCommand)?.NotifyCanExecuteChanged();
        (RotateSelected180Command as RelayCommand)?.NotifyCanExecuteChanged();
        (RotateSelected270Command as RelayCommand)?.NotifyCanExecuteChanged();
        (ExtractSelectedCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }

    partial void OnIsPanelOpenChanged(bool value) => NotifyOperationCommands();
}
