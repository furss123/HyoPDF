using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HyoPDF.Core.Localization;
using HyoPDF.Core.Services;
using HyoPDF.UI.Helpers;
using HyoPDF.UI.Services;
using Microsoft.Win32;

namespace HyoPDF.UI.ViewModels;

public partial class MergeViewModel : ObservableObject
{
    private readonly IPageService _pageService;
    private readonly ILocalizationService _localization;
    private readonly IToastService _toastService;

    public ObservableCollection<MergeFileItem> Files { get; } = [];

    [ObservableProperty]
    private string _outputPath = string.Empty;

    public bool CanMerge => Files.Count >= 2 && !string.IsNullOrWhiteSpace(OutputPath);

    public event EventHandler? CloseRequested;

    public MergeViewModel(
        IPageService pageService,
        ILocalizationService localization,
        IToastService toastService)
    {
        _pageService = pageService;
        _localization = localization;
        _toastService = toastService;
        Files.CollectionChanged += OnFilesChanged;
    }

    public void PrepareForDialog(string? currentDocumentPath)
    {
        Files.Clear();
        OutputPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "병합결과.pdf");

        if (!string.IsNullOrEmpty(currentDocumentPath) && File.Exists(currentDocumentPath))
            AddFileIfNew(currentDocumentPath);

        NotifyCanMergeChanged();
    }

    [RelayCommand]
    private void AddFiles()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "PDF files (*.pdf)|*.pdf|All files (*.*)|*.*",
            Multiselect = true
        };

        var owner = Application.Current?.MainWindow as Window;
        if (dialog.ShowOpenDialog(owner) != true)
            return;

        AddFilesFromPaths(dialog.FileNames);
    }

    [RelayCommand]
    private void RemoveFile(MergeFileItem? item)
    {
        if (item is null)
            return;

        Files.Remove(item);
    }

    [RelayCommand]
    private void MoveUp(MergeFileItem? item)
    {
        if (item is null)
            return;

        var index = Files.IndexOf(item);
        if (index <= 0)
            return;

        Files.Move(index, index - 1);
    }

    [RelayCommand]
    private void MoveDown(MergeFileItem? item)
    {
        if (item is null)
            return;

        var index = Files.IndexOf(item);
        if (index < 0 || index >= Files.Count - 1)
            return;

        Files.Move(index, index + 1);
    }

    [RelayCommand]
    private void SelectOutput()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "PDF files (*.pdf)|*.pdf",
            FileName = Path.GetFileName(OutputPath),
            InitialDirectory = Path.GetDirectoryName(OutputPath)
        };

        if (dialog.ShowSaveDialog(Application.Current?.MainWindow as Window) != true)
            return;

        OutputPath = dialog.FileName;
    }

    [RelayCommand(CanExecute = nameof(CanMerge))]
    private void Merge()
    {
        try
        {
            var paths = Files.Select(f => f.FilePath).ToList();
            _pageService.MergePdfs(paths, OutputPath);
            _toastService.Show(_localization.GetString("MergeComplete"), ToastType.Success);
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception)
        {
            _toastService.Show("병합 실패", ToastType.Error);
        }
    }

    public void AddFilesFromPaths(IEnumerable<string> paths)
    {
        foreach (var path in paths)
        {
            if (path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                AddFileIfNew(path);
        }
    }

    public void ReorderFile(int sourceIndex, int targetIndex)
    {
        if (sourceIndex < 0 || sourceIndex >= Files.Count || targetIndex < 0 || targetIndex > Files.Count)
            return;

        if (sourceIndex == targetIndex || sourceIndex + 1 == targetIndex)
            return;

        var insertIndex = targetIndex;
        if (sourceIndex < insertIndex)
            insertIndex--;

        var item = Files[sourceIndex];
        Files.RemoveAt(sourceIndex);
        Files.Insert(insertIndex, item);
    }

    partial void OnOutputPathChanged(string value) => NotifyCanMergeChanged();

    private void OnFilesChanged(object? sender, NotifyCollectionChangedEventArgs e) => NotifyCanMergeChanged();

    private void AddFileIfNew(string path)
    {
        if (Files.Any(f => string.Equals(f.FilePath, path, StringComparison.OrdinalIgnoreCase)))
            return;

        Files.Add(new MergeFileItem(path));
    }

    private void NotifyCanMergeChanged()
    {
        OnPropertyChanged(nameof(CanMerge));
        MergeCommand.NotifyCanExecuteChanged();
    }
}
