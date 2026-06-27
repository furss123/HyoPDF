namespace HyoPDF.Core.Settings;

public sealed class RecentFileEntry
{
    public string Path { get; set; } = string.Empty;
    public DateTime LastOpened { get; set; }
}
