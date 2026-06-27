using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HyoPDF.Core.Localization;
using HyoPDF.Core.Models;
using HyoPDF.Core.Services;
using HyoPDF.Core.Settings;
using HyoPDF.Core.UndoRedo;
using HyoPDF.UI.Services;
using Microsoft.Win32;

namespace HyoPDF.UI.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ILocalSettingsStore _settingsStore;
    private readonly IUndoRedoStack _undoRedoStack;
    private readonly ILocalizationService _localization;
    private readonly IRecentFilesService _recentFilesService;
    private readonly IToastService _toastService;
    private bool _isApplyingSettings;
    private bool _isApplyingTabSwitch;
    private TabItemViewModel? _wiredPageTab;
    private ViewerViewModel? _wiredFullscreenViewer;
    private TabItemViewModel? _lastActiveTab;

    [ObservableProperty]
    private bool _isSidebarExpanded = true;

    [ObservableProperty]
    private string _openLabel = string.Empty;

    [ObservableProperty]
    private string _searchLabel = string.Empty;

    [ObservableProperty]
    private string _bookmarksLabel = string.Empty;

    [ObservableProperty]
    private string _pagesLabel = string.Empty;

    [ObservableProperty]
    private string _aboutLabel = string.Empty;

    [ObservableProperty]
    private string _pageManagerLabel = string.Empty;

    [ObservableProperty]
    private string _dropPdfLabel = string.Empty;

    [ObservableProperty]
    private string _recentFilesLabel = string.Empty;

    [ObservableProperty]
    private string _noRecentDocumentsLabel = string.Empty;

    [ObservableProperty]
    private string _clearRecentLabel = string.Empty;

    [ObservableProperty]
    private string _compressLabel = string.Empty;

    [ObservableProperty]
    private string _settingsLabel = string.Empty;

    [ObservableProperty]
    private bool _isRecentFlyoutOpen;

    public TabsViewModel DocumentTabs { get; }
    public ObservableCollection<RecentFile> RecentFiles { get; } = [];
    public bool HasRecentFiles => RecentFiles.Count > 0;

    public TabItemViewModel? ActiveTab => DocumentTabs.ActiveTab;
    public ViewerViewModel Viewer => DocumentTabs.ActiveTab!.Viewer;
    public PageViewModel Page => DocumentTabs.ActiveTab!.Page;

    public PrintViewModel Print { get; }
    public CompressViewModel Compress { get; }
    public SettingsViewModel Settings { get; }

    public ICommand OpenFileCommand { get; }
    public ICommand CloseTabCommand { get; }
    public ICommand SwitchTabCommand { get; }
    public ICommand NextTabCommand { get; }
    public ICommand PreviousTabCommand { get; }
    public ICommand ToggleSidebarCommand { get; }
    public ICommand FocusSearchCommand { get; }
    public ICommand UndoCommand { get; }
    public ICommand RedoCommand { get; }
    public ICommand ShowAboutCommand { get; }
    public ICommand ShowPrintCommand { get; }
    public ICommand ShowCompressCommand { get; }
    public ICommand ShowSettingsCommand { get; }
    public ICommand ToggleRecentFlyoutCommand { get; }
    public ICommand EscapeCommand { get; }

    public event EventHandler? FocusSearchRequested;
    public event EventHandler? ShowAboutRequested;
    public event EventHandler? MergeDialogRequested;
    public event EventHandler? SplitDialogRequested;
    public event EventHandler? PrintDialogRequested;
    public event EventHandler? CompressDialogRequested;
    public event EventHandler? SettingsDialogRequested;
    public event EventHandler? ActiveTabContentChanged;
    public event EventHandler<bool>? FullscreenChanged;

    public MainViewModel(
        ILocalSettingsStore settingsStore,
        IUndoRedoStack undoRedoStack,
        ILocalizationService localization,
        IRecentFilesService recentFilesService,
        IToastService toastService,
        TabsViewModel documentTabs,
        PrintViewModel print,
        CompressViewModel compress,
        SettingsViewModel settings)
    {
        _settingsStore = settingsStore;
        _undoRedoStack = undoRedoStack;
        _localization = localization;
        _recentFilesService = recentFilesService;
        _toastService = toastService;
        DocumentTabs = documentTabs;
        Print = print;
        Compress = compress;
        Settings = settings;

        OpenFileCommand = new RelayCommand(OpenFile);
        CloseTabCommand = DocumentTabs.CloseTabCommand;
        SwitchTabCommand = DocumentTabs.SwitchTabCommand;
        NextTabCommand = DocumentTabs.SwitchToNextTabCommand;
        PreviousTabCommand = DocumentTabs.SwitchToPreviousTabCommand;
        ToggleSidebarCommand = new RelayCommand(() => IsSidebarExpanded = !IsSidebarExpanded);
        FocusSearchCommand = new RelayCommand(() => FocusSearchRequested?.Invoke(this, EventArgs.Empty));
        UndoCommand = new RelayCommand(Undo, CanUndo);
        RedoCommand = new RelayCommand(Redo, CanRedo);
        ShowAboutCommand = new RelayCommand(() => ShowAboutRequested?.Invoke(this, EventArgs.Empty));
        ShowPrintCommand = new RelayCommand(
            () => PrintDialogRequested?.Invoke(this, EventArgs.Empty),
            () => Viewer.HasDocument);
        ShowCompressCommand = new RelayCommand(() => CompressDialogRequested?.Invoke(this, EventArgs.Empty));
        ShowSettingsCommand = new RelayCommand(() => SettingsDialogRequested?.Invoke(this, EventArgs.Empty));
        ToggleRecentFlyoutCommand = new RelayCommand(() => IsRecentFlyoutOpen = !IsRecentFlyoutOpen);
        EscapeCommand = new RelayCommand(HandleEscape);

        DocumentTabs.ActiveTabChanged += (_, _) => OnActiveTabChanged();
        Settings.SettingsChanged += (_, appSettings) => ApplySettings(appSettings);

        LoadSettings();
        OnActiveTabChanged();
        RefreshLabels();
        _localization.CultureChanged += (_, _) => RefreshLabels();
    }

    public void OpenNewTab(string path) => OpenFileFromPath(path);

    public void OpenFileFromPath(string path)
    {
        if (!File.Exists(path))
            return;

        DocumentTabs.OpenNewTab(path);
        _recentFilesService.Add(path);
        RefreshRecentFiles();
    }

    [RelayCommand]
    private void OpenRecent(string path)
    {
        IsRecentFlyoutOpen = false;

        if (!File.Exists(path))
        {
            _recentFilesService.Remove(path);
            RefreshRecentFiles();
            _toastService.Show(_localization.GetString("FileNotFound"), ToastType.Error);
            return;
        }

        OpenFileFromPath(path);
    }

    [RelayCommand]
    private void RemoveRecent(RecentFile file)
    {
        _recentFilesService.Remove(file.Path);
        RefreshRecentFiles();
    }

    [RelayCommand]
    private void ClearRecent()
    {
        _recentFilesService.Clear();
        RefreshRecentFiles();
        IsRecentFlyoutOpen = false;
    }

    private void HandleEscape()
    {
        if (Viewer.IsFullscreen)
            Viewer.IsFullscreen = false;
        else if (Page.IsPanelOpen)
            Page.IsPanelOpen = false;
    }

    private bool CanUndo() => Page.UndoStack.CanUndo || _undoRedoStack.CanUndo;

    private bool CanRedo() => Page.UndoStack.CanRedo || _undoRedoStack.CanRedo;

    private void Undo()
    {
        if (Page.UndoStack.CanUndo)
            Page.UndoCommand.Execute(null);
        else
            _undoRedoStack.Undo();
    }

    private void Redo()
    {
        if (Page.UndoStack.CanRedo)
            Page.RedoCommand.Execute(null);
        else
            _undoRedoStack.Redo();
    }

    private void OnActiveTabChanged()
    {
        if (_lastActiveTab is not null)
            _lastActiveTab.IsSidebarExpanded = IsSidebarExpanded;

        if (_wiredPageTab is not null)
        {
            _wiredPageTab.Page.MergeDialogRequested -= OnMergeDialogRequested;
            _wiredPageTab.Page.SplitDialogRequested -= OnSplitDialogRequested;
            _wiredPageTab.Page.UndoStack.StackChanged -= OnPageUndoStackChanged;
        }

        if (_wiredFullscreenViewer is not null)
            _wiredFullscreenViewer.PropertyChanged -= OnViewerFullscreenChanged;

        _lastActiveTab = DocumentTabs.ActiveTab;
        _wiredPageTab = _lastActiveTab;

        if (_wiredPageTab is not null)
        {
            _wiredPageTab.Page.MergeDialogRequested += OnMergeDialogRequested;
            _wiredPageTab.Page.SplitDialogRequested += OnSplitDialogRequested;
            _wiredPageTab.Page.UndoStack.StackChanged += OnPageUndoStackChanged;

            _wiredFullscreenViewer = _wiredPageTab.Viewer;
            _wiredFullscreenViewer.PropertyChanged += OnViewerFullscreenChanged;

            _isApplyingTabSwitch = true;
            IsSidebarExpanded = _wiredPageTab.IsSidebarExpanded;
            _isApplyingTabSwitch = false;
        }

        OnPropertyChanged(nameof(ActiveTab));
        OnPropertyChanged(nameof(Viewer));
        OnPropertyChanged(nameof(Page));
        ActiveTabContentChanged?.Invoke(this, EventArgs.Empty);

        (ShowPrintCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (UndoCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (RedoCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }

    private void OnMergeDialogRequested(object? sender, EventArgs e) =>
        MergeDialogRequested?.Invoke(this, EventArgs.Empty);

    private void OnSplitDialogRequested(object? sender, EventArgs e) =>
        SplitDialogRequested?.Invoke(this, EventArgs.Empty);

    private void OnPageUndoStackChanged(object? sender, EventArgs e)
    {
        (UndoCommand as RelayCommand)?.NotifyCanExecuteChanged();
        (RedoCommand as RelayCommand)?.NotifyCanExecuteChanged();
    }

    private void OnViewerFullscreenChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ViewerViewModel.IsFullscreen) && sender is ViewerViewModel viewer)
            FullscreenChanged?.Invoke(this, viewer.IsFullscreen);
    }

    private void RefreshLabels()
    {
        OpenLabel = _localization.GetString("Open");
        SearchLabel = _localization.GetString("Search");
        BookmarksLabel = _localization.GetString("Bookmarks");
        PagesLabel = _localization.GetString("Pages");
        AboutLabel = _localization.GetString("About");
        PageManagerLabel = _localization.GetString("PageManager");
        DropPdfLabel = _localization.GetString("DropPdfHere");
        RecentFilesLabel = _localization.GetString("RecentFiles");
        NoRecentDocumentsLabel = _localization.GetString("NoRecentDocuments");
        ClearRecentLabel = _localization.GetString("ClearRecent");
        CompressLabel = _localization.GetString("Compress");
        SettingsLabel = _localization.GetString("Settings");
    }

    private void LoadSettings()
    {
        Settings.LoadFromStore();
        var settings = _settingsStore.Load();
        ApplySettings(settings);
        RefreshRecentFiles();
    }

    private void ApplySettings(AppSettings settings)
    {
        _isApplyingSettings = true;
        IsSidebarExpanded = settings.SidebarVisible;

        if (DocumentTabs.ActiveTab is not null)
        {
            DocumentTabs.ActiveTab.IsSidebarExpanded = settings.SidebarVisible;
            DocumentTabs.ActiveTab.Viewer.ZoomLevel = Math.Clamp(settings.DefaultZoom, 25, 500);
        }

        _isApplyingSettings = false;
    }

    private void RefreshRecentFiles()
    {
        RecentFiles.Clear();
        foreach (var entry in _recentFilesService.GetAll())
            RecentFiles.Add(entry);
        OnPropertyChanged(nameof(HasRecentFiles));
    }

    private void OpenFile()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "PDF files (*.pdf)|*.pdf",
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
            OpenFileFromPath(dialog.FileName);
    }

    partial void OnIsSidebarExpandedChanged(bool value)
    {
        if (_isApplyingSettings || _isApplyingTabSwitch)
            return;

        if (DocumentTabs.ActiveTab is not null)
            DocumentTabs.ActiveTab.IsSidebarExpanded = value;

        if (Settings.SidebarVisible != value)
            Settings.SidebarVisible = value;
    }

    public string? CurrentDocumentPath => ActiveTab?.FilePath;
}
