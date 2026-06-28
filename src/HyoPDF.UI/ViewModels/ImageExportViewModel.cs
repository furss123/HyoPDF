using System.Diagnostics;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HyoPDF.Core.Localization;
using HyoPDF.UI.Models;
using Microsoft.Win32;

namespace HyoPDF.UI.ViewModels;

public partial class ImageExportViewModel : ObservableObject
{
    private readonly ILocalizationService _localization;

    [ObservableProperty]
    private ImageExportFormat _selectedFormat = ImageExportFormat.Png;

    [ObservableProperty]
    private int _quality = 85;

    [ObservableProperty]
    private string _outputFolder = string.Empty;

    public bool IsQualityEnabled => SelectedFormat == ImageExportFormat.Jpg;

    public bool IsPngFormat
    {
        get => SelectedFormat == ImageExportFormat.Png;
        set { if (value) SelectedFormat = ImageExportFormat.Png; }
    }

    public bool IsJpgFormat
    {
        get => SelectedFormat == ImageExportFormat.Jpg;
        set { if (value) SelectedFormat = ImageExportFormat.Jpg; }
    }

    public bool IsBmpFormat
    {
        get => SelectedFormat == ImageExportFormat.Bmp;
        set { if (value) SelectedFormat = ImageExportFormat.Bmp; }
    }

    public string Title => _localization.GetString("ImageExportDialogTitle");
    public string FormatLabel => _localization.GetString("ImageExportFormat");
    public string QualityLabel => _localization.GetString("ImageExportQuality");
    public string QualityHint => _localization.GetString("ImageExportQualityHint");
    public string OutputFolderLabel => _localization.GetString("ImageExportOutputFolder");
    public string BrowseLabel => _localization.GetString("Browse");
    public string OpenFolderLabel => _localization.GetString("ImageExportOpenFolder");
    public string CancelLabel => _localization.GetString("Cancel");
    public string ExportLabel => _localization.GetString("ImageExportStart");

    public string QualityText => $"{Quality}%";

    public ImageExportViewModel(ILocalizationService localization)
    {
        _localization = localization;
        _localization.CultureChanged += (_, _) => NotifyLabelProperties();
    }

    public void Prepare(string? defaultFolder)
    {
        OutputFolder = string.IsNullOrWhiteSpace(defaultFolder) ? string.Empty : defaultFolder;
        SelectedFormat = ImageExportFormat.Png;
        Quality = 85;
    }

    public ImageExportOptions ToOptions() => new()
    {
        Format = SelectedFormat,
        Quality = Quality,
        OutputFolder = OutputFolder
    };

    public bool CanExport() => !string.IsNullOrWhiteSpace(OutputFolder);

    [RelayCommand(CanExecute = nameof(CanOpenOutputFolder))]
    private void OpenOutputFolder()
    {
        if (!CanOpenOutputFolder())
            return;

        Process.Start(new ProcessStartInfo
        {
            FileName = OutputFolder,
            UseShellExecute = true
        });
    }

    private bool CanOpenOutputFolder() =>
        !string.IsNullOrWhiteSpace(OutputFolder) && Directory.Exists(OutputFolder);

    partial void OnOutputFolderChanged(string value) =>
        OpenOutputFolderCommand.NotifyCanExecuteChanged();

    [RelayCommand]
    private void SelectFolder()
    {
        var dialog = new OpenFolderDialog
        {
            InitialDirectory = string.IsNullOrWhiteSpace(OutputFolder) ? null : OutputFolder
        };

        if (dialog.ShowDialog() == true)
            OutputFolder = dialog.FolderName;
    }

    partial void OnSelectedFormatChanged(ImageExportFormat value)
    {
        OnPropertyChanged(nameof(IsQualityEnabled));
        OnPropertyChanged(nameof(IsPngFormat));
        OnPropertyChanged(nameof(IsJpgFormat));
        OnPropertyChanged(nameof(IsBmpFormat));
    }

    partial void OnQualityChanged(int value) => OnPropertyChanged(nameof(QualityText));

    private void NotifyLabelProperties()
    {
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(FormatLabel));
        OnPropertyChanged(nameof(QualityLabel));
        OnPropertyChanged(nameof(QualityHint));
        OnPropertyChanged(nameof(OutputFolderLabel));
        OnPropertyChanged(nameof(BrowseLabel));
        OnPropertyChanged(nameof(OpenFolderLabel));
        OnPropertyChanged(nameof(CancelLabel));
        OnPropertyChanged(nameof(ExportLabel));
    }
}
