using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HyoPDF.Core.Diagnostics;
using HyoPDF.Core.Localization;
using HyoPDF.Core.PageOperations;
using HyoPDF.Core.Services;
using HyoPDF.UI.Models;
using HyoPDF.UI.Services;
using HyoPDF.UI.Views;
using Microsoft.Win32;

namespace HyoPDF.UI.ViewModels;

public partial class PageViewModel : ObservableObject
{
    private readonly IPageService _pageService;
    private readonly IPdfViewerService _pdfViewer;
    private readonly ViewerViewModel _viewer;
    private readonly IToastService _toastService;
    private readonly ILocalizationService _localization;
    private readonly PageClipboardService _clipboardService;
    private readonly PageUndoRedoStack _undoStack = new();

    private int _lastSelectedIndex = -1;

    [ObservableProperty]
    private bool _isPanelOpen;

    public ObservableCollection<int> SelectedPageIndices { get; } = [];

    public PageUndoRedoStack UndoStack => _undoStack;

    public bool CanPaste => _clipboardService.HasContent;

    public ICommand DeleteSelectedCommand { get; }
    public ICommand CopySelectedCommand { get; }
    public ICommand CutSelectedCommand { get; }
    public ICommand PasteCommand { get; }
    public ICommand MoveSelectedCommand { get; }
    public ICommand RotateSelectedCommand { get; }
    public ICommand RotateSelected90Command { get; }
    public ICommand RotateSelected180Command { get; }
    public ICommand RotateSelected270Command { get; }
    public ICommand ExtractSelectedCommand { get; }
    public ICommand ConvertToImageCommand { get; }
    public ICommand MergeCommand { get; }
    public ICommand SplitCommand { get; }
    public ICommand SplitFromHereCommand { get; }
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
        ILocalizationService localization,
        PageClipboardService clipboardService)
    {
        _pageService = pageService;
        _pdfViewer = pdfViewer;
        _viewer = viewer;
        _toastService = toastService;
        _localization = localization;
        _clipboardService = clipboardService;

        DeleteSelectedCommand = new AsyncRelayCommand(DeleteSelectedAsync, CanOperateOnThumbnailCommands);
        CopySelectedCommand = new RelayCommand(CopySelected, CanOperateOnThumbnailCommands);
        CutSelectedCommand = new RelayCommand(CutSelected, CanOperateOnThumbnailCommands);
        PasteCommand = new AsyncRelayCommand(PasteAsync, () => CanPaste && _viewer.HasDocument);
        MoveSelectedCommand = new RelayCommand<int>(MoveSelected, _ => CanOperateOnSelection());
        RotateSelectedCommand = new RelayCommand<int>(RotateSelected, _ => CanOperateOnSelection());
        RotateSelected90Command = new RelayCommand(() => RotateSelected(90), CanOperateOnSelection);
        RotateSelected180Command = new RelayCommand(() => RotateSelected(180), CanOperateOnSelection);
        RotateSelected270Command = new RelayCommand(() => RotateSelected(270), CanOperateOnSelection);
        ExtractSelectedCommand = new AsyncRelayCommand(ExtractSelectedAsync, CanOperateOnThumbnailCommands);
        ConvertToImageCommand = new AsyncRelayCommand(ConvertToImageAsync, CanOperateOnThumbnailCommands);
        MergeCommand = new RelayCommand(ShowMergeDialog);
        SplitCommand = new RelayCommand(ShowSplitDialog, () => _viewer.HasDocument);
        SplitFromHereCommand = new RelayCommand<int>(SplitFromHere, CanSplitFromHere);
        UndoCommand = new RelayCommand(Undo, () => _undoStack.CanUndo);
        RedoCommand = new RelayCommand(Redo, () => _undoStack.CanRedo);
        TogglePanelCommand = new RelayCommand(() => IsPanelOpen = !IsPanelOpen);

        _undoStack.StackChanged += (_, _) =>
        {
            (UndoCommand as RelayCommand)?.NotifyCanExecuteChanged();
            (RedoCommand as RelayCommand)?.NotifyCanExecuteChanged();
        };

        _clipboardService.ClipboardChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(CanPaste));
            (PasteCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        };
    }

    public void SyncSelectionFromPages()
    {
        SelectedPageIndices.Clear();
        foreach (var page in _viewer.Pages.Where(p => p.IsSelected))
            SelectedPageIndices.Add(page.PageIndex);
    }

    public void RefreshSelectionCommands() => NotifyOperationCommands();

    public void RefreshClipboardVisuals()
    {
        ClearCutIndicators();

        var clipboard = _clipboardService.Paste();
        if (clipboard?.Operation != ClipboardOperation.Cut)
            return;

        var path = CurrentPath;
        if (path is null || !string.Equals(clipboard.SourceFilePath, path, StringComparison.OrdinalIgnoreCase))
            return;

        foreach (var pageIndex in clipboard.PageIndices)
        {
            var page = _viewer.Pages.FirstOrDefault(p => p.PageIndex == pageIndex);
            if (page is not null)
                page.IsCut = true;
        }
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

    private bool CanOperateOnThumbnailCommands() => _viewer.HasDocument;

    private void EnsureSelectedIndices()
    {
        if (SelectedPageIndices.Count == 0 && _viewer.HasDocument)
            SelectedPageIndices.Add(_viewer.CurrentPageIndex);
    }

    private string? CurrentPath => _pdfViewer.CurrentPath;

    private void ExecuteWithUndo(string description, Action operation)
    {
        var path = CurrentPath;
        if (path is null || !File.Exists(path)) return;

        var undoSnapshot = PageFileHelper.CreateSnapshot(path);
        _pdfViewer.CloseFile();

        try
        {
            operation();
        }
        catch (Exception ex)
        {
            // Recover the viewer instead of leaving it on a closed document:
            // _pdfViewer.CloseFile() above already ran, so a failure here (locked
            // file, disk full, etc.) would otherwise strand the UI on a document
            // whose underlying handle is gone, with no visible error.
            Debug.WriteLine($"{description} failed: {ex}");
            FileLog.Write($"[Page] {description} failed: {path}", ex);
            _toastService.Show(_localization.GetString("PageOperationFailed"), ToastType.Error);
            ReloadDocument(path);
            return;
        }

        var redoSnapshot = PageFileHelper.CreateSnapshot(path);

        _undoStack.Push(new PageSnapshotCommand(description, path, undoSnapshot, redoSnapshot, () => ReloadDocument(path)));
        ReloadDocument(path);
        NotifyOperationCommands();
    }

    private void ReloadDocument(string? path = null)
    {
        path ??= CurrentPath;
        if (path is null || !File.Exists(path)) return;

        var pageCount = _pageService.GetPageCount(path);
        if (pageCount <= 0)
        {
            _viewer.CloseDocument();
            ClearSelection();
            ClearCutIndicators();
            return;
        }

        var pageIndex = Math.Clamp(_viewer.CurrentPageIndex, 0, pageCount - 1);
        _ = _viewer.ReloadDocumentAsync(path, pageIndex);
        ClearSelection();
        RefreshClipboardVisuals();
    }

    private void CopySelected()
    {
        EnsureSelectedIndices();
        if (SelectedPageIndices.Count == 0)
            return;

        var path = CurrentPath;
        if (path is null || !File.Exists(path))
            return;

        ClearCutIndicators();
        _clipboardService.Copy(path, SelectedPageIndices.ToList());
        _toastService.Show(_localization.GetString("CopyPagesComplete"), ToastType.Info);
    }

    private void CutSelected()
    {
        EnsureSelectedIndices();
        if (SelectedPageIndices.Count == 0)
            return;

        var path = CurrentPath;
        if (path is null || !File.Exists(path))
            return;

        var indices = SelectedPageIndices.ToList();
        _clipboardService.Cut(path, indices);

        ClearCutIndicators();
        foreach (var pageIndex in indices)
        {
            var page = _viewer.Pages.FirstOrDefault(p => p.PageIndex == pageIndex);
            if (page is not null)
                page.IsCut = true;
        }

        _toastService.Show(_localization.GetString("CutPagesComplete"), ToastType.Info);
    }

    private async Task PasteAsync()
    {
        if (!_clipboardService.HasContent)
            return;

        var path = CurrentPath;
        if (path is null || !File.Exists(path))
            return;

        var clipboard = _clipboardService.Paste()!;
        var insertAt = SelectedPageIndices.Count > 0
            ? SelectedPageIndices.Max() + 1
            : _viewer.PageCount;
        var savedIndex = _viewer.CurrentPageIndex;

        try
        {
            _viewer.IsLoading = true;
            var undoSnapshot = PageFileHelper.CreateSnapshot(path);
            _pdfViewer.CloseFile();

            await Task.Run(() => ExecutePasteOperation(clipboard, path, insertAt));

            if (clipboard.Operation == ClipboardOperation.Cut)
            {
                _clipboardService.Clear();
                ClearCutIndicators();
            }

            var redoSnapshot = PageFileHelper.CreateSnapshot(path);
            _undoStack.Push(new PageSnapshotCommand(
                clipboard.Operation == ClipboardOperation.Cut ? "Cut pages" : "Paste pages",
                path,
                undoSnapshot,
                redoSnapshot,
                () => ReloadDocument(path)));

            var newPageCount = await Task.Run(() => _pageService.GetPageCount(path));
            if (newPageCount <= 0)
            {
                _viewer.CloseDocument();
                SelectedPageIndices.Clear();
                ClearSelection();
                return;
            }

            var safeIndex = Math.Clamp(savedIndex, 0, newPageCount - 1);
            await _viewer.ReloadDocumentAsync(path, safeIndex);
            SelectedPageIndices.Clear();
            ClearSelection();
            RefreshClipboardVisuals();
            _toastService.Show(_localization.GetString("PastePagesComplete"), ToastType.Success);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Paste failed: {ex}");
            FileLog.Write($"[Page] Paste failed: {path}", ex);
            _toastService.Show(_localization.GetString("PastePagesFailed"), ToastType.Error);
        }
        finally
        {
            _viewer.IsLoading = false;
            NotifyOperationCommands();
        }
    }

    private void ExecutePasteOperation(PageClipboard clipboard, string destPath, int insertAt)
    {
        var indices = clipboard.PageIndices;

        if (clipboard.Operation == ClipboardOperation.Cut)
        {
            if (string.Equals(clipboard.SourceFilePath, destPath, StringComparison.OrdinalIgnoreCase))
                _pageService.MovePages(destPath, indices, insertAt);
            else
            {
                _pageService.CopyPages(clipboard.SourceFilePath, indices, destPath, insertAt);
                _pageService.DeletePages(
                    clipboard.SourceFilePath,
                    indices.OrderByDescending(i => i).ToList());
            }
        }
        else
        {
            _pageService.CopyPages(clipboard.SourceFilePath, indices, destPath, insertAt);
        }
    }

    private void ClearCutIndicators()
    {
        foreach (var page in _viewer.Pages)
            page.IsCut = false;
    }

    private async Task DeleteSelectedAsync()
    {
        EnsureSelectedIndices();
        Debug.WriteLine($"Delete called, pages: {string.Join(",", SelectedPageIndices)}");
        if (SelectedPageIndices.Count == 0)
            return;

        var path = CurrentPath;
        if (path is null || !File.Exists(path))
            return;

        var savedIndex = _viewer.CurrentPageIndex;
        var indicesToDelete = SelectedPageIndices.OrderByDescending(i => i).ToList();

        try
        {
            _viewer.IsLoading = true;
            var undoSnapshot = PageFileHelper.CreateSnapshot(path);
            _pdfViewer.CloseFile();

            await Task.Run(() => _pageService.DeletePages(path, indicesToDelete));

            var newPageCount = await Task.Run(() => _pageService.GetPageCount(path));

            var redoSnapshot = PageFileHelper.CreateSnapshot(path);
            _undoStack.Push(new PageSnapshotCommand(
                "Delete pages",
                path,
                undoSnapshot,
                redoSnapshot,
                () => ReloadDocument(path)));

            if (newPageCount <= 0)
            {
                _viewer.CloseDocument();
                SelectedPageIndices.Clear();
                ClearSelection();
                ClearCutIndicators();
                ClearClipboardIfSource(path);
                _toastService.Show(_localization.GetString("AllPagesDeleted"), ToastType.Info);
                return;
            }

            var safeIndex = Math.Max(0, Math.Min(savedIndex, newPageCount - 1));
            await _viewer.ReloadDocumentAsync(path, safeIndex);
            SelectedPageIndices.Clear();
            ClearSelection();
            RefreshClipboardVisuals();
            _toastService.Show(_localization.GetString("DeletePagesComplete"), ToastType.Success);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Delete failed: {ex}");
            FileLog.Write($"[Page] Delete failed: {path}", ex);
            _toastService.Show(_localization.GetString("DeletePagesFailed"), ToastType.Error);
        }
        finally
        {
            _viewer.IsLoading = false;
            NotifyOperationCommands();
        }
    }

    private void ClearClipboardIfSource(string path)
    {
        var clipboard = _clipboardService.Paste();
        if (clipboard is not null &&
            string.Equals(clipboard.SourceFilePath, path, StringComparison.OrdinalIgnoreCase))
        {
            _clipboardService.Clear();
            ClearCutIndicators();
        }
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

    private async Task ExtractSelectedAsync()
    {
        EnsureSelectedIndices();
        Debug.WriteLine($"Extract called, pages: {string.Join(",", SelectedPageIndices)}");
        if (SelectedPageIndices.Count == 0)
            return;

        var path = CurrentPath;
        if (path is null)
            return;

        var dialog = new SaveFileDialog
        {
            Filter = "PDF (*.pdf)|*.pdf",
            FileName = "추출된_페이지.pdf"
        };

        if (dialog.ShowDialog() != true)
            return;

        var indices = SelectedPageIndices.OrderBy(i => i).ToList();

        try
        {
            _viewer.IsLoading = true;
            await Task.Run(() => _pageService.ExtractPages(path, indices, dialog.FileName));
            _toastService.Show(_localization.GetString("ExtractPagesComplete"), ToastType.Success);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Extract failed: {ex}");
            FileLog.Write($"[Page] Extract failed: {path}", ex);
            _toastService.Show(_localization.GetString("ExtractPagesFailed"), ToastType.Error);
        }
        finally
        {
            _viewer.IsLoading = false;
        }
    }

    private async Task ConvertToImageAsync()
    {
        EnsureSelectedIndices();
        Debug.WriteLine("Convert called");
        if (SelectedPageIndices.Count == 0)
            return;

        var path = CurrentPath;
        if (path is null)
            return;

        var exportVm = new ImageExportViewModel(_localization);
        exportVm.Prepare(Path.GetDirectoryName(path));

        var dialog = new ImageExportDialog(exportVm)
        {
            Owner = Application.Current.MainWindow
        };

        if (dialog.ShowDialog() != true)
            return;

        var options = dialog.Result;
        if (string.IsNullOrWhiteSpace(options.OutputFolder))
            return;

        var indices = SelectedPageIndices.OrderBy(i => i).ToList();
        var rotation = _viewer.Rotation;
        var extension = GetImageExtension(options.Format);

        try
        {
            _viewer.IsLoading = true;
            await Task.Run(() =>
            {
                foreach (var pageIndex in indices)
                {
                    var image = _pdfViewer.RenderPage(pageIndex, 150, rotation);
                    if (image is not BitmapSource bitmap)
                        continue;

                    var outputPath = Path.Combine(options.OutputFolder, $"page_{pageIndex + 1}.{extension}");
                    SaveBitmapSource(bitmap, outputPath, options);
                }
            });

            _toastService.Show(_localization.GetString("ConvertToImageComplete"), ToastType.Success);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Convert failed: {ex}");
            FileLog.Write($"[Page] Convert to image failed: {path}", ex);
            _toastService.Show(_localization.GetString("ConvertToImageFailed"), ToastType.Error);
        }
        finally
        {
            _viewer.IsLoading = false;
        }
    }

    private static string GetImageExtension(ImageExportFormat format) => format switch
    {
        ImageExportFormat.Jpg => "jpg",
        ImageExportFormat.Bmp => "bmp",
        _ => "png"
    };

    private static void SaveBitmapSource(BitmapSource source, string path, ImageExportOptions options)
    {
        BitmapEncoder encoder = options.Format switch
        {
            ImageExportFormat.Jpg => new JpegBitmapEncoder { QualityLevel = options.Quality },
            ImageExportFormat.Bmp => new BmpBitmapEncoder(),
            _ => new PngBitmapEncoder()
        };

        encoder.Frames.Add(BitmapFrame.Create(source));
        using var stream = File.Create(path);
        encoder.Save(stream);
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

    private bool CanSplitFromHere(int pageIndex) =>
        _viewer.HasDocument && pageIndex > 0 && pageIndex < _viewer.PageCount;

    private void SplitFromHere(int pageIndex)
    {
        var path = CurrentPath;
        if (path is null) return;

        var dialog = new OpenFolderDialog
        {
            InitialDirectory = Path.GetDirectoryName(path) ?? string.Empty
        };

        if (dialog.ShowDialog() != true) return;

        SplitAtPoints([pageIndex], dialog.FolderName);
        _toastService.Show(_localization.GetString("SplitComplete"), ToastType.Success);
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

    public void OnDocumentClosed(string? documentPath = null)
    {
        var path = documentPath ?? CurrentPath;
        _undoStack.Clear();
        ClearCutIndicators();
        if (path is not null)
            ClearClipboardIfSource(path);
    }

    private void NotifyOperationCommands()
    {
        (DeleteSelectedCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        (CopySelectedCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (CutSelectedCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (PasteCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        (MoveSelectedCommand as RelayCommand<int>)?.NotifyCanExecuteChanged();
        (RotateSelectedCommand as RelayCommand<int>)?.NotifyCanExecuteChanged();
        (RotateSelected90Command as RelayCommand)?.NotifyCanExecuteChanged();
        (RotateSelected180Command as RelayCommand)?.NotifyCanExecuteChanged();
        (RotateSelected270Command as RelayCommand)?.NotifyCanExecuteChanged();
        (ExtractSelectedCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        (ConvertToImageCommand as AsyncRelayCommand)?.NotifyCanExecuteChanged();
        (SplitFromHereCommand as RelayCommand<int>)?.NotifyCanExecuteChanged();
    }

    partial void OnIsPanelOpenChanged(bool value) => NotifyOperationCommands();
}
