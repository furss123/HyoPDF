using HyoPDF.Core.Printing;

namespace HyoPDF.Core.Services;

public interface IPrintService
{
    void PrintDocument(PrintOptions options);
    void PrintViaNative(string filePath);
    bool TryPrintViaNative(string filePath);
    Task PrintWithWpfOptionsAsync(
        string filePath,
        PrintOptions options,
        System.Windows.Controls.PrintDialog printDialog,
        CancellationToken cancellationToken = default);
    Task<System.Windows.Media.ImageSource?> RenderPagePreviewAsync(
        string filePath,
        int pageIndex,
        bool grayscale,
        CancellationToken cancellationToken = default);
    IReadOnlyList<string> GetInstalledPrinters();
    string? GetDefaultPrinterName();
    void ShowPrinterSettingsDialog(string printerName);
}