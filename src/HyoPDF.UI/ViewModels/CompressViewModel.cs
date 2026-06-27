using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HyoPDF.Core.Compression;
using HyoPDF.Core.Localization;
using HyoPDF.Core.Services;
using HyoPDF.UI.Helpers;
using HyoPDF.UI.Services;
using Microsoft.Win32;

namespace HyoPDF.UI.ViewModels;

public partial class CompressViewModel : ObservableObject
{
    private readonly ICompressService _compressService;
    private readonly ILocalizationService _localization;
    private readonly IToastService _toastService;
    private CancellationTokenSource? _cancellationTokenSource;

    [ObservableProperty]
    private CompressionLevel _selectedLevel = CompressionLevel.Level3;

    [ObservableProperty]
    private string? _inputPath;

    [ObservableProperty]
    private string? _outputPath;

    [ObservableProperty]
    private string _originalSizeText = "-";

    [ObservableProperty]
    private string _estimatedSizeText = "-";

    [ObservableProperty]
    private bool _isCompressing;

    [ObservableProperty]
    private double _progress;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private int _selectedLevelSlider = 3;

    public string Level1Label => _localization.GetString("CompressLevel1");
    public string Level2Label => _localization.GetString("CompressLevel2");
    public string Level3Label => _localization.GetString("CompressLevel3");
    public string Level4Label => _localization.GetString("CompressLevel4");
    public string Level5Label => _localization.GetString("CompressLevel5");

    public event EventHandler? CloseRequested;

    public CompressViewModel(
        ICompressService compressService,
        ILocalizationService localization,
        IToastService toastService)
    {
        _compressService = compressService;
        _localization = localization;
        _toastService = toastService;
        _localization.CultureChanged += (_, _) => RefreshLevelLabels();
    }

    public void PrepareForDialog(string? defaultInputPath)
    {
        ErrorMessage = null;
        Progress = 0;
        IsCompressing = false;
        InputPath = defaultInputPath;
        OutputPath = BuildDefaultOutputPath(defaultInputPath);
        UpdateSizeEstimates();
        SelectedLevel = CompressionLevel.Level3;
        SelectedLevelSlider = 3;
    }

    [RelayCommand]
    private void BrowseInput()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "PDF files (*.pdf)|*.pdf",
            FileName = InputPath
        };

        if (dialog.ShowDialog() != true)
            return;

        InputPath = dialog.FileName;
        OutputPath = BuildDefaultOutputPath(InputPath);
        UpdateSizeEstimates();
    }

    [RelayCommand]
    private void BrowseOutput()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "PDF files (*.pdf)|*.pdf",
            FileName = string.IsNullOrWhiteSpace(OutputPath) ? "compressed.pdf" : Path.GetFileName(OutputPath),
            InitialDirectory = string.IsNullOrWhiteSpace(OutputPath)
                ? null
                : Path.GetDirectoryName(OutputPath)
        };

        if (dialog.ShowDialog() != true)
            return;

        OutputPath = dialog.FileName;
    }

    [RelayCommand(CanExecute = nameof(CanCompress))]
    private async Task CompressAsync()
    {
        if (!CanCompress())
            return;

        ErrorMessage = null;
        IsCompressing = true;
        Progress = 0;
        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            var progress = new Progress<double>(value => Progress = value);
            await Task.Run(() => _compressService.CompressPdf(
                InputPath!,
                OutputPath!,
                SelectedLevel,
                progress,
                _cancellationTokenSource.Token));

            _toastService.Show(_localization.GetString("CompressComplete"), ToastType.Success);
            CloseRequested?.Invoke(this, EventArgs.Empty);
        }
        catch (OperationCanceledException)
        {
            ErrorMessage = _localization.GetString("CompressCancelled");
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            _toastService.Show(ex.Message, ToastType.Error);
        }
        finally
        {
            IsCompressing = false;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;
            CompressCommand.NotifyCanExecuteChanged();
        }
    }

    [RelayCommand]
    private void Cancel()
    {
        if (IsCompressing)
            _cancellationTokenSource?.Cancel();
        else
            CloseRequested?.Invoke(this, EventArgs.Empty);
    }

    private bool CanCompress() =>
        !IsCompressing &&
        !string.IsNullOrWhiteSpace(InputPath) &&
        File.Exists(InputPath) &&
        !string.IsNullOrWhiteSpace(OutputPath);

    private static string? BuildDefaultOutputPath(string? inputPath)
    {
        if (string.IsNullOrWhiteSpace(inputPath))
            return null;

        var directory = Path.GetDirectoryName(inputPath);
        var name = Path.GetFileNameWithoutExtension(inputPath);
        return Path.Combine(directory ?? string.Empty, $"{name}_compressed.pdf");
    }

    private void UpdateSizeEstimates()
    {
        if (string.IsNullOrWhiteSpace(InputPath) || !File.Exists(InputPath))
        {
            OriginalSizeText = "-";
            EstimatedSizeText = "-";
            return;
        }

        var original = new FileInfo(InputPath).Length;
        var estimated = _compressService.EstimateCompressedSize(original, SelectedLevel);
        OriginalSizeText = FileSizeFormatter.Format(original);
        EstimatedSizeText = FileSizeFormatter.Format(estimated);
    }

    private void RefreshLevelLabels()
    {
        OnPropertyChanged(nameof(Level1Label));
        OnPropertyChanged(nameof(Level2Label));
        OnPropertyChanged(nameof(Level3Label));
        OnPropertyChanged(nameof(Level4Label));
        OnPropertyChanged(nameof(Level5Label));
    }

    partial void OnSelectedLevelChanged(CompressionLevel value)
    {
        SelectedLevelSlider = value.ToSliderValue();
        UpdateSizeEstimates();
        CompressCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedLevelSliderChanged(int value)
    {
        SelectedLevel = CompressionLevelExtensions.FromSliderValue(value);
    }

    partial void OnInputPathChanged(string? value)
    {
        UpdateSizeEstimates();
        CompressCommand.NotifyCanExecuteChanged();
    }

    partial void OnOutputPathChanged(string? value) => CompressCommand.NotifyCanExecuteChanged();

    partial void OnIsCompressingChanged(bool value) => CompressCommand.NotifyCanExecuteChanged();
}
