using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Printing;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HyoPDF.Core.Localization;
using HyoPDF.Core.Printing;
using HyoPDF.Core.Services;
using HyoPDF.Core.Themes;
using HyoPDF.UI.Services;
using WpfPrintDialog = System.Windows.Controls.PrintDialog;

namespace HyoPDF.UI.ViewModels;

public partial class PrintViewModel : ObservableObject
{
    private readonly IPrintService _printService;
    private readonly ILocalizationService _localization;
    private readonly IToastService _toastService;
    private readonly TabsViewModel _documentTabs;
    private readonly IThemeService _themeService;
    private CancellationTokenSource? _previewCts;
    private string? _filePath;
    private int _currentPageIndex;

    public ObservableCollection<string> Printers { get; } = [];

    public ObservableCollection<string> PagesPerSheetOptions { get; } =
    [
        "1페이지", "2페이지", "4페이지", "6페이지", "9페이지", "16페이지"
    ];

    public event EventHandler? CloseRequested;
    public event EventHandler? CancelRequested;

    [ObservableProperty]
    private string? _selectedPrinter;

    [ObservableProperty]
    private PrintPageRangeMode _pageRangeMode = PrintPageRangeMode.All;

    [ObservableProperty]
    private string _customRange = string.Empty;

    [ObservableProperty]
    private int _copies = 1;

    [ObservableProperty]
    private bool _collate = true;

    [ObservableProperty]
    private string _selectedPagesPerSheet = "1페이지";

    [ObservableProperty]
    private bool _duplex;

    [ObservableProperty]
    private bool _grayscale;

    [ObservableProperty]
    private int _previewPageIndex;

    [ObservableProperty]
    private ImageSource? _previewImage;

    [ObservableProperty]
    private bool _isPrinting;

    [ObservableProperty]
    private int _totalPages;

    [ObservableProperty]
    private string _previewPageText = "0 / 0";

    [ObservableProperty]
    private string _totalSheetsText = string.Empty;

    [ObservableProperty]
    private bool _isDarkTheme = true;

    public bool IsAllPages
    {
        get => PageRangeMode == PrintPageRangeMode.All;
        set { if (value) PageRangeMode = PrintPageRangeMode.All; }
    }

    public bool IsCurrentPage
    {
        get => PageRangeMode == PrintPageRangeMode.Current;
        set { if (value) PageRangeMode = PrintPageRangeMode.Current; }
    }

    public bool IsCustomRange
    {
        get => PageRangeMode == PrintPageRangeMode.Custom;
        set { if (value) PageRangeMode = PrintPageRangeMode.Custom; }
    }

    public bool IsFitToPage
    {
        get => FitMode == PrintFitMode.FitToPage;
        set { if (value) FitMode = PrintFitMode.FitToPage; }
    }

    public bool IsActualSize
    {
        get => FitMode == PrintFitMode.ActualSize;
        set { if (value) FitMode = PrintFitMode.ActualSize; }
    }

    [ObservableProperty]
    private PrintFitMode _fitMode = PrintFitMode.FitToPage;

    public int PagesPerSheet => ParsePagesPerSheetLabel(SelectedPagesPerSheet);

    public string DialogBackground => IsDarkTheme ? "#111111" : "#F5F5F5";
    public string LeftPanelBackground => IsDarkTheme ? "#161616" : "#EBEBEB";
    public string PreviewBackground => IsDarkTheme ? "#0A0A0A" : "#E0E0E0";
    public string BottomBarBackground => IsDarkTheme ? "#161616" : "#EBEBEB";
    public string BottomBarBorder => IsDarkTheme ? "#2A2A2A" : "#D0D0D0";
    public string InputBackground => IsDarkTheme ? "#1E1E1E" : "#FFFFFF";
    public string InputBorder => IsDarkTheme ? "#333333" : "#D0D0D0";
    public string TextPrimary => IsDarkTheme ? "#E0E0E0" : "#1A1A1A";
    public string TextSecondary => IsDarkTheme ? "#909090" : "#606060";
    public string SectionLabel => IsDarkTheme ? "#909090" : "#606060";
    public string MutedButtonForeground => IsDarkTheme ? "#909090" : "#606060";
    public string NavButtonBorder => IsDarkTheme ? "#2A2A2A" : "#D0D0D0";
    public string PanelDivider => IsDarkTheme ? "#2A2A2A" : "#D0D0D0";

    public string CustomRangeBorderBrush => IsCustomRangeInvalid ? "#C42B1C" : InputBorder;

    public bool IsCustomRangeInvalid =>
        PageRangeMode == PrintPageRangeMode.Custom &&
        (string.IsNullOrWhiteSpace(CustomRange) || !TryResolvePageRange(out _, out _));

    public PrintViewModel(
        IPrintService printService,
        ILocalizationService localization,
        IToastService toastService,
        TabsViewModel documentTabs,
        IThemeService themeService)
    {
        _printService = printService;
        _localization = localization;
        _toastService = toastService;
        _documentTabs = documentTabs;
        _themeService = themeService;
        _themeService.ThemeChanged += OnThemeChanged;
        ApplyTheme(_themeService.CurrentTheme);
    }

    public void PrepareForDialog(string? filePath, int totalPages, int currentPageIndex)
    {
        _filePath = filePath;
        _currentPageIndex = Math.Clamp(currentPageIndex, 0, Math.Max(0, totalPages - 1));
        TotalPages = Math.Max(0, totalPages);
        PreviewPageIndex = 0;

        PageRangeMode = PrintPageRangeMode.All;
        CustomRange = string.Empty;
        Copies = 1;
        Collate = true;
        SelectedPagesPerSheet = "1페이지";
        FitMode = PrintFitMode.FitToPage;
        Duplex = false;
        Grayscale = false;
        IsPrinting = false;

        ApplyTheme(_themeService.CurrentTheme);
        LoadPrinters();
        PrintCommand.NotifyCanExecuteChanged();
        _ = UpdatePreviewAsync();
        UpdateTotalSheets();
    }

    private void OnThemeChanged(object? sender, AppTheme theme) => ApplyTheme(theme);

    private void ApplyTheme(AppTheme theme)
    {
        IsDarkTheme = theme == AppTheme.Dark;
        RefreshThemeColors();
    }

    private void RefreshThemeColors()
    {
        OnPropertyChanged(nameof(DialogBackground));
        OnPropertyChanged(nameof(LeftPanelBackground));
        OnPropertyChanged(nameof(PreviewBackground));
        OnPropertyChanged(nameof(BottomBarBackground));
        OnPropertyChanged(nameof(BottomBarBorder));
        OnPropertyChanged(nameof(InputBackground));
        OnPropertyChanged(nameof(InputBorder));
        OnPropertyChanged(nameof(TextPrimary));
        OnPropertyChanged(nameof(TextSecondary));
        OnPropertyChanged(nameof(SectionLabel));
        OnPropertyChanged(nameof(MutedButtonForeground));
        OnPropertyChanged(nameof(NavButtonBorder));
        OnPropertyChanged(nameof(PanelDivider));
        OnPropertyChanged(nameof(CustomRangeBorderBrush));
    }

    private void LoadPrinters()
    {
        Printers.Clear();
        foreach (var printer in _printService.GetInstalledPrinters())
            Printers.Add(printer);

        SelectedPrinter = _printService.GetDefaultPrinterName() ?? Printers.FirstOrDefault();
    }

    private static int ParsePagesPerSheetLabel(string? label) => label switch
    {
        "2페이지" => 2,
        "4페이지" => 4,
        "6페이지" => 6,
        "9페이지" => 9,
        "16페이지" => 16,
        _ => 1
    };

    [RelayCommand]
    private void Cancel() => CancelRequested?.Invoke(this, EventArgs.Empty);

    [RelayCommand(CanExecute = nameof(CanPrint))]
    private async Task PrintAsync()
    {
        if (!TryBuildOptions(out var options))
        {
            _toastService.Show(_localization.GetString("PrintInvalidRange"), ToastType.Error);
            return;
        }

        if (string.IsNullOrWhiteSpace(_filePath))
            return;

        var wasOpen = _documentTabs.IsPathOpen(_filePath);
        if (wasOpen)
            _documentTabs.ReleaseFileHandle(_filePath);

        IsPrinting = true;
        PrintCommand.NotifyCanExecuteChanged();

        try
        {
            var printDialog = CreatePrintDialog();
            _toastService.Show(_localization.GetString("PrintPreparing"), ToastType.Info);
            await _printService.PrintWithWpfOptionsAsync(_filePath, options, printDialog);
            _toastService.Show(_localization.GetString("PrintCompleted"), ToastType.Success);
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Print] {ex}");
            _toastService.Show(_localization.GetString("PrintError"), ToastType.Error);
        }
        finally
        {
            IsPrinting = false;
            PrintCommand.NotifyCanExecuteChanged();

            if (wasOpen)
                _documentTabs.ReloadFileIfTabExists(_filePath);
        }
    }

    private bool CanPrint() => !IsPrinting && TotalPages > 0 && !string.IsNullOrWhiteSpace(SelectedPrinter);

    [RelayCommand(CanExecute = nameof(CanMovePreviewBack))]
    private void PrevPage()
    {
        if (PreviewPageIndex > 0)
            PreviewPageIndex--;
    }

    [RelayCommand(CanExecute = nameof(CanMovePreviewForward))]
    private void NextPage()
    {
        var count = GetPreviewPageCount();
        if (PreviewPageIndex < count - 1)
            PreviewPageIndex++;
    }

    private bool CanMovePreviewBack() => PreviewPageIndex > 0;
    private bool CanMovePreviewForward() => PreviewPageIndex < GetPreviewPageCount() - 1;

    private WpfPrintDialog CreatePrintDialog()
    {
        var dialog = new WpfPrintDialog();
        if (string.IsNullOrWhiteSpace(SelectedPrinter))
            return dialog;

        try
        {
            dialog.PrintQueue = new LocalPrintServer().GetPrintQueue(SelectedPrinter);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Print] PrintQueue: {ex.Message}");
        }

        return dialog;
    }

    private bool TryBuildOptions(out PrintOptions options)
    {
        options = new PrintOptions();
        if (!TryResolvePageRange(out var pageRange, out _))
            return false;

        options.FilePath = _filePath ?? string.Empty;
        options.PageRange = pageRange;
        options.PagesPerSheet = PagesPerSheet;
        options.Copies = Math.Clamp(Copies, 1, 99);
        options.Collate = Collate;
        options.Duplex = Duplex;
        options.Grayscale = Grayscale;
        options.FitMode = FitMode;
        options.FitToPage = FitMode == PrintFitMode.FitToPage;
        options.PrinterName = SelectedPrinter;
        return true;
    }

    private bool TryResolvePageRange(out int[] pageRange, out string? error)
    {
        pageRange = [];
        error = null;

        if (TotalPages <= 0)
            return false;

        switch (PageRangeMode)
        {
            case PrintPageRangeMode.All:
                pageRange = Enumerable.Range(0, TotalPages).ToArray();
                return true;
            case PrintPageRangeMode.Current:
                pageRange = [Math.Clamp(_currentPageIndex, 0, TotalPages - 1)];
                return true;
            case PrintPageRangeMode.Custom:
                if (string.IsNullOrWhiteSpace(CustomRange))
                    return false;
                return PageRangeParser.TryParse(CustomRange, TotalPages, out pageRange, out error);
            default:
                return false;
        }
    }

    private int GetSelectedPageCount() =>
        TryResolvePageRange(out var range, out _) ? range.Length : 0;

    private int GetPreviewPageCount() => GetSelectedPageCount();

    private int GetPreviewSourcePageIndex()
    {
        if (!TryResolvePageRange(out var range, out _) || range.Length == 0)
            return 0;

        var index = Math.Clamp(PreviewPageIndex, 0, range.Length - 1);
        return range[index];
    }

    private async Task UpdatePreviewAsync()
    {
        _previewCts?.Cancel();
        _previewCts = new CancellationTokenSource();
        var token = _previewCts.Token;

        if (string.IsNullOrWhiteSpace(_filePath) || TotalPages <= 0)
        {
            PreviewImage = null;
            PreviewPageText = "0 / 0";
            return;
        }

        try
        {
            var pageIndex = GetPreviewSourcePageIndex();
            var image = await _printService.RenderPagePreviewAsync(_filePath, pageIndex, Grayscale, token);
            if (token.IsCancellationRequested)
                return;

            PreviewImage = image;
            var count = GetPreviewPageCount();
            var displayIndex = count > 0 ? Math.Clamp(PreviewPageIndex, 0, count - 1) + 1 : 0;
            PreviewPageText = $"{displayIndex} / {count}";
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"[Print] Preview: {ex}");
        }
    }

    private void UpdateTotalSheets()
    {
        var pages = GetSelectedPageCount();
        var sheets = pages <= 0
            ? 0
            : (int)Math.Ceiling(pages / (double)Math.Max(1, PagesPerSheet)) * Math.Max(1, Copies);
        TotalSheetsText = string.Format(_localization.GetString("PrintTotalSheetsFormat"), sheets);
        OnPropertyChanged(nameof(CustomRangeBorderBrush));
        OnPropertyChanged(nameof(IsCustomRangeInvalid));
    }

    partial void OnPageRangeModeChanged(PrintPageRangeMode value)
    {
        OnPropertyChanged(nameof(IsAllPages));
        OnPropertyChanged(nameof(IsCurrentPage));
        OnPropertyChanged(nameof(IsCustomRange));
        PreviewPageIndex = 0;
        UpdateTotalSheets();
        _ = UpdatePreviewAsync();
    }

    partial void OnCustomRangeChanged(string value)
    {
        UpdateTotalSheets();
        _ = UpdatePreviewAsync();
    }

    partial void OnCopiesChanged(int value) => UpdateTotalSheets();
    partial void OnSelectedPagesPerSheetChanged(string value) => UpdateTotalSheets();
    partial void OnFitModeChanged(PrintFitMode value)
    {
        OnPropertyChanged(nameof(IsFitToPage));
        OnPropertyChanged(nameof(IsActualSize));
    }

    partial void OnGrayscaleChanged(bool value) => _ = UpdatePreviewAsync();

    partial void OnPreviewPageIndexChanged(int value)
    {
        PrevPageCommand.NotifyCanExecuteChanged();
        NextPageCommand.NotifyCanExecuteChanged();
        _ = UpdatePreviewAsync();
    }

    partial void OnTotalPagesChanged(int value) => UpdateTotalSheets();
    partial void OnSelectedPrinterChanged(string? value) => PrintCommand.NotifyCanExecuteChanged();
    partial void OnIsPrintingChanged(bool value) => PrintCommand.NotifyCanExecuteChanged();
    partial void OnIsDarkThemeChanged(bool value) => RefreshThemeColors();
}
