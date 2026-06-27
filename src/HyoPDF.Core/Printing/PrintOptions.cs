namespace HyoPDF.Core.Printing;

public sealed class PrintOptions
{
    public string FilePath { get; set; } = string.Empty;
    public int[]? PageRange { get; set; }
    public int PagesPerSheet { get; set; } = 1;
    public bool Duplex { get; set; }
    public bool FitToPage { get; set; } = true;
    public int Copies { get; set; } = 1;
    public string? PrinterName { get; set; }
    public bool Preview { get; set; }
}
