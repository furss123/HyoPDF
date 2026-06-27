namespace HyoPDF.Core.Settings;

public sealed class AppSettings
{
    public AppThemePreference Theme { get; set; } = AppThemePreference.System;
    public string Language { get; set; } = "ko";
    public int DefaultZoom { get; set; } = 100;
    public bool SidebarVisible { get; set; } = true;
    public WindowSize LastWindowSize { get; set; } = new();
}
