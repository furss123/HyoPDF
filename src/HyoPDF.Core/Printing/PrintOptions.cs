namespace HyoPDF.Core.Printing;

public sealed class PrintOptions
{
    public string FilePath { get; set; } = string.Empty;
    public int[]? PageRange { get; set; }
    public int PagesPerSheet { get; set; } = 1;
    public bool Duplex { get; set; }
    public bool FitToPage { get; set; } = true;
    public PrintFitMode FitMode { get; set; } = PrintFitMode.FitToPage;
    public int CustomScale { get; set; } = 100;
    public int Copies { get; set; } = 1;
    public bool Collate { get; set; } = true;
    public bool Grayscale { get; set; }
    public string? PrinterName { get; set; }
    public bool Preview { get; set; }
    public bool ShowPrintDialog { get; set; }
}
