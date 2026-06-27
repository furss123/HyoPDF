using System.IO;
using System.Text.Json;

namespace HyoPDF.Core.Settings;

public sealed class LocalSettingsStore : ILocalSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _settingsPath;

    public LocalSettingsStore()
    {
        var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        var folder = Path.Combine(appData, "HyoPDF");
        Directory.CreateDirectory(folder);
        _settingsPath = Path.Combine(folder, "settings.json");
    }

    public AppSettings Load()
    {
        if (!File.Exists(_settingsPath))
            return new AppSettings();

        try
        {
            var json = File.ReadAllText(_settingsPath);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            MigrateLegacySettings(json, settings);
            return settings;
        }
        catch
        {
            return new AppSettings();
        }
    }

    public void Save(AppSettings settings)
    {
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(_settingsPath, json);
    }

    private static void MigrateLegacySettings(string json, AppSettings settings)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;

        if (root.TryGetProperty("SidebarExpanded", out var sidebar) &&
            !root.TryGetProperty("SidebarVisible", out _))
        {
            settings.SidebarVisible = sidebar.GetBoolean();
        }

        if (root.TryGetProperty("LastZoom", out var zoom))
        {
            settings.DefaultZoom = (int)Math.Clamp(zoom.GetDouble(), 25, 500);
        }
    }
}
