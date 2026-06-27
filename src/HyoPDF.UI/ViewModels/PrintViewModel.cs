using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HyoPDF.Core.Printing;
using HyoPDF.Core.Services;

namespace HyoPDF.UI.ViewModels;

public partial class PrintViewModel : ObservableObject
{
    private readonly IPrintService _printService;
    private string? _filePath;
    private int _pageCount;
    private int _currentPageIndex;

    [ObservableProperty]
    private PrintPageRangeMode _pageRangeMode = PrintPageRangeMode.All;

    [ObservableProperty]
    private string _customRange = string.Empty;

    [ObservableProperty]
    private int _pagesPerSheet = 1;

    [ObservableProperty]
    private bool _isDuplex;

    [ObservableProperty]
    private bool _isFitToPage = true;

    [ObservableProperty]
    private int _copies = 1;

    [ObservableProperty]
    private string? _selectedPrinter;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private int _sheetPreviewColumns = 1;

    [ObservableProperty]
    private int _sheetPreviewRows = 1;

    public ObservableCollection<string> AvailablePrinters { get; } = [];
    public ObservableCollection<int> PagesPerSheetOptions { get; } = new(PrintLayoutHelper.SupportedPagesPerSheet);
    public ObservableCollection<string> SheetPreviewCells { get; } = [];

    [ObservableProperty]
    private bool _hasDocument;

    public bool IsAllPagesSelected
    {
        get => PageRangeMode == PrintPageRangeMode.All;
        set { if (value) PageRangeMode = PrintPageRangeMode.All; }
    }

    public bool IsCurrentPageSelected
    {
        get => PageRangeMode == PrintPageRangeMode.Current;
        set { if (value) PageRangeMode = PrintPageRangeMode.Current; }
    }

    public bool IsCustomRangeSelected
    {
        get => PageRangeMode == PrintPageRangeMode.Custom;
        set { if (value) PageRangeMode = PrintPageRangeMode.Custom; }
    }

    public event EventHandler? CloseRequested;

    public PrintViewModel(IPrintService printService)
    {
        _printService = printService;
        RefreshPrinters();
        UpdateSheetPreview();
    }

    public void PrepareForDialog(string? filePath, int pageCount, int currentPageIndex)
    {
        _filePath = filePath;
        _pageCount = pageCount;
        _currentPageIndex = Math.Clamp(currentPageIndex, 0, Math.Max(0, pageCount - 1));
        HasDocument = !string.IsNullOrEmpty(_filePath) && _pageCount > 0;
        ErrorMessage = null;
        PageRangeMode = PrintPageRangeMode.All;
        CustomRange = string.Empty;
        PagesPerSheet = 1;
        IsDuplex = false;
        IsFitToPage = true;
        Copies = 1;
        RefreshPrinters();
        UpdateSheetPreview();
    }

    [RelayCommand(CanExecute = nameof(HasDocument))]
    private void Print()
    {
        if (!TryBuildOptions(preview: false, out var options))
            return;

        try
        {
            _printService.PrintDocument(options);
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand(CanExecute = nameof(HasDocument))]
    private void PrintPreview()
    {
        if (!TryBuildOptions(preview: true, out var options))
            return;

        try
        {
            _printService.PrintDocument(options);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(this, EventArgs.Empty);

    private bool TryBuildOptions(bool preview, out PrintOptions options)
    {
        options = new PrintOptions();
        ErrorMessage = null;

        if (!HasDocument)
        {
            ErrorMessage = "No document is open.";
            return false;
        }

        try
        {
            options.FilePath = _filePath!;
            options.PageRange = ResolvePageRange();
            options.PagesPerSheet = PagesPerSheet;
            options.Duplex = IsDuplex;
            options.FitToPage = IsFitToPage;
            options.Copies = Math.Max(1, Copies);
            options.PrinterName = SelectedPrinter;
            options.Preview = preview;
            return true;
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            return false;
        }
    }

    private int[]? ResolvePageRange() => PageRangeMode switch
    {
        PrintPageRangeMode.All => null,
        PrintPageRangeMode.Current => [_currentPageIndex],
        PrintPageRangeMode.Custom => PageRangeParser.Parse(CustomRange, _pageCount),
        _ => null
    };

    private void RefreshPrinters()
    {
        AvailablePrinters.Clear();
        foreach (var printer in _printService.GetInstalledPrinters())
            AvailablePrinters.Add(printer);

        if (AvailablePrinters.Count == 0)
        {
            SelectedPrinter = null;
            return;
        }

        if (SelectedPrinter is null || !AvailablePrinters.Contains(SelectedPrinter))
            SelectedPrinter = AvailablePrinters[0];
    }

    private void UpdateSheetPreview()
    {
        var (columns, rows) = PrintLayoutHelper.GetGrid(PagesPerSheet);
        SheetPreviewColumns = columns;
        SheetPreviewRows = rows;

        SheetPreviewCells.Clear();
        for (var i = 1; i <= PagesPerSheet; i++)
            SheetPreviewCells.Add(i.ToString());
    }

    partial void OnPageRangeModeChanged(PrintPageRangeMode value)
    {
        OnPropertyChanged(nameof(IsAllPagesSelected));
        OnPropertyChanged(nameof(IsCurrentPageSelected));
        OnPropertyChanged(nameof(IsCustomRangeSelected));
        ErrorMessage = null;
    }

    partial void OnPagesPerSheetChanged(int value) => UpdateSheetPreview();

    partial void OnCustomRangeChanged(string value) => ErrorMessage = null;
}
