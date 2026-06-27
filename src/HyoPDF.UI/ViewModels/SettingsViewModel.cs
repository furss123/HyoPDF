using CommunityToolkit.Mvvm.ComponentModel;
using HyoPDF.Core.Localization;
using HyoPDF.Core.Settings;
using HyoPDF.Core.Themes;

namespace HyoPDF.UI.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly ILocalSettingsStore _settingsStore;
    private readonly ILocalizationService _localization;
    private readonly IThemeService _themeService;
    private bool _isLoading;

    [ObservableProperty]
    private AppThemePreference _selectedTheme = AppThemePreference.System;

    [ObservableProperty]
    private string _selectedLanguage = "ko";

    [ObservableProperty]
    private int _defaultZoom = 100;

    [ObservableProperty]
    private bool _sidebarVisible = true;

    public AppThemePreference[] ThemeOptions { get; } =
        [AppThemePreference.System, AppThemePreference.Light, AppThemePreference.Dark];

    public string[] LanguageOptions { get; } = ["ko", "en"];

    public event EventHandler<AppSettings>? SettingsChanged;

    public SettingsViewModel(
        ILocalSettingsStore settingsStore,
        ILocalizationService localization,
        IThemeService themeService)
    {
        _settingsStore = settingsStore;
        _localization = localization;
        _themeService = themeService;
    }

    public void LoadFromStore()
    {
        _isLoading = true;
        var settings = _settingsStore.Load();
        SelectedTheme = settings.Theme;
        SelectedLanguage = settings.Language;
        DefaultZoom = Math.Clamp(settings.DefaultZoom, 25, 500);
        SidebarVisible = settings.SidebarVisible;
        _isLoading = false;

        ApplyTheme(SelectedTheme);
        _localization.SetCulture(SelectedLanguage);
    }

    public AppSettings CaptureSettings() => new()
    {
        Theme = SelectedTheme,
        Language = SelectedLanguage,
        DefaultZoom = Math.Clamp(DefaultZoom, 25, 500),
        SidebarVisible = SidebarVisible,
        LastWindowSize = _settingsStore.Load().LastWindowSize
    };

    public void SaveWindowSize(double width, double height)
    {
        var settings = _settingsStore.Load();
        settings.LastWindowSize = new WindowSize
        {
            Width = Math.Max(900, width),
            Height = Math.Max(600, height)
        };
        _settingsStore.Save(settings);
    }

    private void Persist()
    {
        if (_isLoading)
            return;

        var settings = _settingsStore.Load();
        settings.Theme = SelectedTheme;
        settings.Language = SelectedLanguage;
        settings.DefaultZoom = Math.Clamp(DefaultZoom, 25, 500);
        settings.SidebarVisible = SidebarVisible;
        _settingsStore.Save(settings);
        SettingsChanged?.Invoke(this, settings);
    }

    private void ApplyTheme(AppThemePreference preference) => _themeService.SetPreference(preference);

    partial void OnSelectedThemeChanged(AppThemePreference value)
    {
        ApplyTheme(value);
        Persist();
    }

    partial void OnSelectedLanguageChanged(string value)
    {
        if (_isLoading)
            return;

        _localization.SetCulture(value);
        Persist();
    }

    partial void OnDefaultZoomChanged(int value) => Persist();

    partial void OnSidebarVisibleChanged(bool value) => Persist();
}
