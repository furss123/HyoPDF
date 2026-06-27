using System.IO;
using System.Text.Json;
using HyoPDF.Core.Models;
using HyoPDF.Core.Settings;

namespace HyoPDF.Core.Services;

public sealed class RecentFilesService : IRecentFilesService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    private readonly string _recentPath;
    private readonly string _legacySettingsPath;
    private List<RecentFile> _entries = [];

    public int MaxCount => 10;

    public RecentFilesService()
    {
        var folder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "HyoPDF");
        Directory.CreateDirectory(folder);
        _recentPath = Path.Combine(folder, "recent.json");
        _legacySettingsPath = Path.Combine(folder, "settings.json");
        Load();
    }

    public void Add(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return;

        _entries.RemoveAll(r => string.Equals(r.Path, path, StringComparison.OrdinalIgnoreCase));
        _entries.Insert(0, new RecentFile
        {
            Path = path,
            FileName = Path.GetFileName(path),
            LastOpenedAt = DateTime.UtcNow
        });

        if (_entries.Count > MaxCount)
            _entries = _entries.Take(MaxCount).ToList();

        Save();
    }

    public IReadOnlyList<RecentFile> GetAll() => _entries.AsReadOnly();

    public void Remove(string path)
    {
        var removed = _entries.RemoveAll(r => string.Equals(r.Path, path, StringComparison.OrdinalIgnoreCase));
        if (removed > 0)
            Save();
    }

    public void Clear()
    {
        if (_entries.Count == 0)
            return;

        _entries.Clear();
        Save();
    }

    private void Load()
    {
        if (File.Exists(_recentPath))
        {
            try
            {
                var json = File.ReadAllText(_recentPath);
                _entries = JsonSerializer.Deserialize<List<RecentFile>>(json, JsonOptions) ?? [];
                return;
            }
            catch
            {
                _entries = [];
            }
        }

        MigrateFromLegacySettings();
    }

    private void MigrateFromLegacySettings()
    {
        if (!File.Exists(_legacySettingsPath))
        {
            _entries = [];
            return;
        }

        try
        {
            var json = File.ReadAllText(_legacySettingsPath);
            var settings = JsonSerializer.Deserialize<LegacyAppSettings>(json, JsonOptions);
            if (settings?.RecentFiles is not { Count: > 0 })
            {
                _entries = [];
                return;
            }

            _entries = settings.RecentFiles
                .OrderByDescending(r => r.LastOpened)
                .Take(MaxCount)
                .Select(r => new RecentFile
                {
                    Path = r.Path,
                    FileName = Path.GetFileName(r.Path),
                    LastOpenedAt = r.LastOpened
                })
                .ToList();

            Save();
        }
        catch
        {
            _entries = [];
        }
    }

    private void Save()
    {
        var json = JsonSerializer.Serialize(_entries, JsonOptions);
        File.WriteAllText(_recentPath, json);
    }

    private sealed class LegacyAppSettings
    {
        public List<RecentFileEntry>? RecentFiles { get; set; }
    }
}
