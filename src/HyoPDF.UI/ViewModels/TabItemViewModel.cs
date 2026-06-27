using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;

namespace HyoPDF.UI.ViewModels;

public partial class TabItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string? _filePath;

    [ObservableProperty]
    private bool _isSidebarExpanded = true;

    public ViewerViewModel Viewer { get; }
    public PageViewModel Page { get; }

    public string FileName => string.IsNullOrEmpty(FilePath) ? "Welcome" : Path.GetFileName(FilePath);

    public TabItemViewModel(ViewerViewModel viewer, PageViewModel page)
    {
        Viewer = viewer;
        Page = page;
    }

    partial void OnFilePathChanged(string? value)
    {
        OnPropertyChanged(nameof(FileName));
    }
}
