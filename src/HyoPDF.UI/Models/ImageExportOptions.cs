namespace HyoPDF.UI.Models;

public enum ImageExportFormat
{
    Png,
    Jpg,
    Bmp
}

public sealed class ImageExportOptions
{
    public ImageExportFormat Format { get; init; } = ImageExportFormat.Png;

    public int Quality { get; init; } = 85;

    public string OutputFolder { get; init; } = string.Empty;
}
