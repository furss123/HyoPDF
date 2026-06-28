namespace HyoPDF.Core.Settings;

public sealed class AppSettings
{
    public AppThemePreference Theme { get; set; } = AppThemePreference.System;
    public string Language { get; set; } = "ko";
    public int DefaultZoom { get; set; } = 100;
    public bool SidebarVisible { get; set; } = true;
    public double ThumbnailSize { get; set; } = 120;
    public bool UserResized { get; set; }
    public WindowSize LastWindowSize { get; set; } = new();
}
